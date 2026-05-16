export function createSignalRSection() {
  return {
        async connectSignalR() {
          try {
            this.connection = new signalR.HubConnectionBuilder()
              .withUrl(this._base() + '/hub')
              .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
              .build();

            this.connection.on('jobExecutedBatch', (events) => {
              events.forEach(e => this.handleJobExecuted(e));
              this.lastDataPulse = Date.now();
              this.lastRefreshed = new Date();

              // Show failure toast for failed executions
              for (const e of events) {
                if (!e.success) {
                  this.showToast('⚠ ' + e.jobKey + ' failed' + (e.exceptionMessage ? ': ' + e.exceptionMessage.substring(0, 80) : ''), 'error');
                  this.playAlertSound();
                  this.sendDesktopNotification?.('Job Failed: ' + e.jobKey, e.exceptionMessage ? e.exceptionMessage.substring(0, 120) : 'Execution failed');
                }
              }
            });

            this.connection.on('jobTriggeredBatch', (events) => {
              events.forEach(e => this.handleJobTriggered(e));
              this.lastDataPulse = Date.now();
              this.lastRefreshed = new Date();
            });

            this.connection.on('schedulerStatus', (data) => {
              this.handleSchedulerStatus(data);
              this.lastDataPulse = Date.now();
              this.lastRefreshed = new Date();
            });

            this.connection.on('jobsUpdated', (data) => {
              this.handleJobsUpdated(data);
            });

            this.connection.onreconnecting(() => {
              this.signalRConnected = false;
              this.showToast('Connection lost — reconnecting...', 'warning');
            });

            this.connection.onreconnected(() => {
              this.signalRConnected = true;
              this.signalRPolling = false;
              this.stopPollingFallback();
              this.refreshAll();
              this.showToast('Reconnected via SignalR', 'success');
            });

            this.connection.onclose(() => {
              this.signalRConnected = false;
              if (!this.signalRPolling) {
                this.signalRPolling = true;
                this.lastPollingTime = Date.now();
                this.startPollingFallback();
                this.showToast('SignalR closed — falling back to polling', 'warning');
              }
            });

            await this.connection.start();
            if (this.connection.state === 'Connected') {
              await this.connection.invoke('Subscribe');
            }
            this.signalRConnected = true;
            if (this.signalRTimeout) {
              clearTimeout(this.signalRTimeout);
              this.signalRTimeout = null;
            }
          } catch (err) {
            console.error('SignalR connection failed:', err);
            this.signalRConnected = false;
          }
        },

        handleJobExecuted(data) {
          // Append to history without breaking server-side pagination
          if (data.jobKey) {
            this.historyTotal = (this.historyTotal || 0) + 1;
            if (this.historyCurrentPage === 1) {
              this.history.unshift(data);
              if (this.history.length > this.historyPageSize) this.history.length = this.historyPageSize;
              const d = data.durationMs || 0;
              if (d > this.maxHistoryDuration) this.maxHistoryDuration = d;
            }
          }

          // Job run result feedback: check if this was a manually triggered job
          if (data.jobKey && this.pendingTriggers[data.jobKey] !== undefined) {
            const flashKey = data.jobKey;
            this.jobFlash[flashKey] = data.success !== false ? 'success' : 'error';
            delete this.pendingTriggers[flashKey];
            setTimeout(() => { delete this.jobFlash[flashKey]; }, 4000);
          }

          // Update executionBuckets if we have stats
          if (this.executionBuckets.length) {
            const now = new Date();
            const bucketMinute = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours(), now.getMinutes()).toISOString();
            let bucket = this.executionBuckets[this.executionBuckets.length - 1];
            if (bucket && bucket.minute === bucketMinute) {
              bucket.count = (bucket.count || 0) + 1;
              const prevAvg = bucket.avgDurationMs || 0;
              const prevCount = bucket.count - 1;
              bucket.avgDurationMs = prevCount > 0 ? (prevAvg * prevCount + (data.durationMs || 0)) / bucket.count : (data.durationMs || 0);
              if (data.success === false) {
                bucket.errorRate = ((bucket.errorRate || 0) * prevCount + 1) / bucket.count;
              }
            } else {
              this.executionBuckets.push({
                minute: bucketMinute,
                count: 1,
                avgDurationMs: data.durationMs || 0,
                errorRate: data.success === false ? 1 : 0
              });
              if (this.executionBuckets.length > 60) this.executionBuckets.shift();
            }
          }

          // Update stat counts
          this.scheduler.numberOfJobsExecuted = (this.scheduler.numberOfJobsExecuted || 0) + 1;

          // Remove from executingJobs if present
          if (data.fireInstanceId) {
            this.executingJobs = this.executingJobs.filter(ej => ej.fireInstanceId !== data.fireInstanceId);
          }

          // Update graph data
          this.graphData = this.getGraphData();

          // Add to timeline
          this.addTimelineEvent(data);
          if (this.currentPage === 'history') {
            this.clearFaviconBadge(this.getHistoryFailureCount(this.history));
          } else if (this.isFailureHistoryEntry(data)) {
            this.updateFaviconBadge((this.faviconFailureCount || 0) + 1);
          }
          this.loadHeatmap();
        },

        handleJobTriggered(data) {
          // Add to executingJobs
          if (data.jobName) {
            // Check if already there
            const exists = this.executingJobs.some(ej => ej.fireInstanceId === data.fireInstanceId);
            if (!exists) {
              this.executingJobs.push(data);
            }
            // Show brief toast if on executing page
            if (this.currentPage === 'executing') {
              this.showToast('Job triggered: ' + (data.jobName || data.jobKey), 'info');
            }
          }
        },

        handleSchedulerStatus(data) {
          if (data) {
            if (data.isStarted !== undefined) this.scheduler.isStarted = data.isStarted;
            if (data.isStandbyMode !== undefined) this.scheduler.isStandbyMode = data.isStandbyMode;
            if (data.numberOfJobsExecuted !== undefined) this.scheduler.numberOfJobsExecuted = data.numberOfJobsExecuted;
            if (data.name !== undefined) this.scheduler.name = data.name;
            if (data.instanceId !== undefined) this.scheduler.instanceId = data.instanceId;
            if (data.version !== undefined) this.scheduler.version = data.version;
          }
        },

        handleJobsUpdated(data) {
          // Coalesce bursts; silent=true so in-place update doesn't show spinner or flicker.
          this.debounce(() => this.loadJobs(undefined, true), 'signalr-jobs-updated-jobs', 200);
          this.debounce(() => this.loadTriggers(undefined, true), 'signalr-jobs-updated-triggers', 200);
        },

        // ========================= POLLING FALLBACK =========================
        startPollingFallback() {
          this.stopPollingFallback();
          this.pollingTimer = setInterval(() => {
            this.refreshAll(true);
            this.lastPollingTime = Date.now();
          }, this.settings.refreshInterval * 1000);
        },

        stopPollingFallback() {
          if (this.pollingTimer) {
            clearInterval(this.pollingTimer);
            this.pollingTimer = null;
          }
        },

        // Best-effort cleanup so the polling fallback (and any SignalR connection) doesn't
        // keep ticking after the user navigates away. pagehide covers bfcache restores on
        // Safari/Firefox; beforeunload covers regular navigations.
        registerLifecycleCleanup() {
          if (this._lifecycleRegistered) return;
          this._lifecycleRegistered = true;
          const cleanup = () => {
            this.stopPollingFallback();
            try { this.connection?.stop(); } catch (_) { /* ignore */ }
          };
          window.addEventListener('pagehide', cleanup);
          window.addEventListener('beforeunload', cleanup);
        },
  };
}
