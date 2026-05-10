export function createJobsSection() {
  return {
        openJobDrawer(job) {
          this.jobDrawerData = job;
          this.jobDrawerTab = 'overview';
          this.jobDrawerHistory = [];
          this.jobDrawerLogs = [];
          this.showJobDrawer = true;
          this.loadJobDrawerHistory(job.group, job.name);
          document.body.style.overflow = 'hidden';
        },
        closeJobDrawer() {
          this.showJobDrawer = false;
          this.jobDrawerData = null;
          this.jobDrawerHistory = [];
          this.jobDrawerLogs = [];
          document.body.style.overflow = '';
        },
        async loadJobDrawerHistory(group, name) {
          this.jobDrawerHistoryLoading = true;
          try {
            const key = encodeURIComponent(group + '.' + name);
            const resp = await this.fetchApi('/history?job=' + key + '&limit=50');
            this.jobDrawerHistory = Array.isArray(resp?.data) ? resp.data : [];
          } catch(e) { this.jobDrawerHistory = []; }
          this.jobDrawerHistoryLoading = false;
        },
        async loadJobDrawerLogs(group, name) {
          this.jobDrawerLogsLoading = true;
          try {
            const resp = await this.fetchApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/logs');
            this.jobDrawerLogs = Array.isArray(resp?.logs) ? resp.logs : [];
          } catch(e) { this.jobDrawerLogs = []; }
          this.jobDrawerLogsLoading = false;
        },
        copyJobJson() {
          const d = this.jobDrawerData || this.jobDetailData;
          if (!d) return;
          const json = JSON.stringify(d, null, 2);
          if (navigator.clipboard) {
            navigator.clipboard.writeText(json).then(() => this.showToast('Job definition copied', 'success'));
          }
        },
        actionKey(type, group, name) {
          return [type || '', group || '', name || ''].join(':');
        },
        isActionPending(type, group, name) {
          return !!this.actionPending[this.actionKey(type, group, name)];
        },
        async withActionPending(type, group, name, callback) {
          const key = this.actionKey(type, group, name);
          if (this.actionPending[key]) return;
          this.actionPending = { ...this.actionPending, [key]: true };
          try {
            return await callback();
          } finally {
            const next = { ...this.actionPending };
            delete next[key];
            this.actionPending = next;
          }
        },
        triggerJobFromDrawer() {
          if (this.jobDrawerData) this.openTriggerJobModal(this.jobDrawerData.group, this.jobDrawerData.name);
        },
        pauseJobFromDrawer() {
          if (this.jobDrawerData) this.pauseJob(this.jobDrawerData.group, this.jobDrawerData.name);
        },
        resumeJobFromDrawer() {
          if (this.jobDrawerData) this.resumeJob(this.jobDrawerData.group, this.jobDrawerData.name);
        },
        duplicateJob(job) {
          this.newJob = {
            name: (job?.name || '') + '-copy',
            group: job?.group || 'DEFAULT',
            description: job?.description || '',
            jobType: job?.jobType || '',
            isDurable: job?.isDurable ?? job?.durable ?? false,
            durable: job?.durable || false,
            requestsRecovery: job?.requestsRecovery || false,
            disallowConcurrentExecution: job?.disallowConcurrentExecution || false,
            persistJobDataAfterExecution: job?.persistJobDataAfterExecution || false,
          };
          this.showCreateJobModal = true;
        },
        deletePendingActionKey() {
          return this.deletePending?.type === 'trigger' ? 'delete-trigger' : 'delete-job';
        },
        isDeletePending() {
          return this.isActionPending(this.deletePendingActionKey(), this.deletePending?.group, this.deletePending?.name);
        },
        deletePendingButtonLabel() {
          return this.isDeletePending() ? 'Deleting...' : 'Delete';
        },
        get jobDrawerTriggers() {
          if (!this.jobDrawerData) return [];
          return this.jobDrawerData.triggers || [];
        },
        get jobDrawerNextFire() {
          if (!this.jobDrawerData) return null;
          const triggers = this.jobDrawerData.triggers || [];
          if (!triggers.length) return null;
          const times = triggers.map(t => t.nextFireTime).filter(Boolean);
          if (!times.length) return null;
          return times.sort()[0];
        },
        jobDrawerLastFireLabel() {
          const lastTrigger = this.jobDrawerTriggers.find(t => t.lastFireTime);
          return lastTrigger?.lastFireTime ? this.relativeTimePhrase(lastTrigger.lastFireTime) : '—';
        },
        triggerFrequencyLabel(trig) {
          if (!trig || !(trig.intervalMs > 0)) return 'cron';
          const perHour = Math.round((3600000 / trig.intervalMs) * 10) / 10;
          const perDay = Math.round(perHour * 24);
          return perHour + '/hr · ' + perDay + '/day';
        },
        jobDrawerHistorySparklinePoints() {
          const history = this.jobDrawerHistory.slice().reverse().slice(-40);
          if (history.length < 2) return '';
          const maxDuration = Math.max(...history.map(x => x.duration || x.runTimeMs || 0), 1);
          const width = 300;
          const step = width / (history.length - 1);
          return history
            .map((x, i) => (i * step).toFixed(1) + ',' + (30 - (((x.duration || x.runTimeMs || 0) / maxDuration) * 28)).toFixed(1))
            .join(' ');
        },
        // ========================= JOB DETAIL MODAL METHODS (legacy) =========================
        openJobDetail(job) {
          this.openJobDrawer(job);
        },
        closeJobDetail() {
          this.closeJobDrawer();
        },

        jobStatusBadge(status) {
          switch (status) {
            case 'Executing': return 'badge badge-running';
            case 'Scheduled': return 'badge badge-normal';
            case 'Paused':    return 'badge badge-paused';
            case 'Durable':   return 'badge badge-idle';
            default:          return 'badge badge-idle';
          }
        },


        jobSuccessRate(group, name) {
          const key = group + '.' + name;
          const relevant = (this.history || []).filter(h => h.jobKey === key);
          if (!relevant.length) return null;
          const success = relevant.filter(h => h.success).length;
          return Math.round((success / relevant.length) * 100);
        },
        getJobLastExecution(group, name) {
          const key = group + '.' + name;
          const relevant = (this.history || []).filter(h => h.jobKey === key && h.fireTime);
          if (!relevant.length) return null;
          return relevant.reduce((latest, entry) => {
            if (!latest) return entry;
            return new Date(entry.fireTime).getTime() > new Date(latest.fireTime).getTime() ? entry : latest;
          }, null);
        },
        truncateText(value, max = 120) {
          if (!value) return '';
          const text = String(value);
          return text.length > max ? text.slice(0, Math.max(0, max - 1)) + '…' : text;
        },
        formatDurationAxis(ms) {
          if (!Number.isFinite(ms) || ms <= 0) return '0ms';
          if (ms >= 10000) return Math.round(ms / 1000) + 's';
          if (ms >= 1000) return (ms / 1000).toFixed(1) + 's';
          return Math.round(ms) + 'ms';
        },
        togglePinJob(group, name) {
          const key = group + '.' + name;
          const idx = this.pinnedJobs.indexOf(key);
          if (idx >= 0) this.pinnedJobs.splice(idx, 1);
          else this.pinnedJobs.push(key);
          localStorage.setItem('quartz-pinned-jobs', JSON.stringify(this.pinnedJobs));
        },
        isJobPinned(group, name) {
          return this.pinnedJobs.includes(group + '.' + name);
        },

        get sortedJobs() {
          return this.getSortedCollection('jobs', this.jobs || []);
        },

        filterJobs() {
          this.selectedJobIndex = -1;
        },

        get filteredJobs() {
          const q = (this.jobSearchQuery || '').trim().toLowerCase();
          if (!q) return this.sortedJobs;
          return this.sortedJobs.filter(j =>
            (j.name || '').toLowerCase().includes(q) ||
            (j.group || '').toLowerCase().includes(q) ||
            (j.jobType || '').toLowerCase().includes(q) ||
            (j.description || '').toLowerCase().includes(q)
          );
        },

        get jobsTotalPages() {
          return Math.max(1, Math.ceil((this.jobsTotal || 0) / this.jobsPageSize));
        },

        get pinnedJobDetails() {
          return (this.allJobs || []).filter(j => this.pinnedJobs.includes(j.group + '.' + j.name));
        },

        // Flat list: header items + job items interleaved.
        // Single x-for in template — avoids nested template x-for scope chain issues in Alpine.js.
        get jobRows() {
          const jobs = this.filteredJobs || [];
          const groups = {};
          jobs.forEach(j => {
            const g = j.group || 'Default';
            if (!groups[g]) groups[g] = [];
            groups[g].push(j);
          });
          const result = [];
          for (const [name, groupJobs] of Object.entries(groups)) {
            const isCollapsed = !!this.collapsedGroups[name];
            result.push({ key: '__h__' + name, isHeader: true, groupName: name, count: groupJobs.length, isCollapsed });
            if (!isCollapsed) {
              for (const job of groupJobs) {
                result.push({ key: job.group + '.' + job.name, isHeader: false, groupName: name, isCollapsed: false, count: 0, job });
              }
            }
          }
          return result;
        },

        async submitCreateJob() {
          if (!this.newJob.name) return;
          this.loading.global = true;
          try {
            const body = {};
            if (this.newJob.name) body.name = this.newJob.name;
            if (this.newJob.group && this.newJob.group !== 'DEFAULT') body.group = this.newJob.group;
            if (this.newJob.description) body.description = this.newJob.description;
            if (this.newJob.jobType) body.jobType = this.newJob.jobType;
            if (this.newJob.isDurable) body.isDurable = true;
            if (this.newJob.persistJobDataAfterExecution) body.persistJobDataAfterExecution = true;
            if (this.newJob.disallowConcurrentExecution) body.disallowConcurrentExecution = true;

            const res = await fetch(this._api('/jobs'), {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body)
            });
            if (!res.ok) throw new Error(await this.apiErrorMessage(res));

            this.showToast('Job ' + this.newJob.name + ' created', 'success');
            this.showCreateJobModal = false;
            this.newJob = { name: '', group: 'DEFAULT', description: '', jobType: '', isDurable: false, disallowConcurrentExecution: false, persistJobDataAfterExecution: false };
            await this.loadJobs();
          } catch (e) {
            this.showToast('Failed to create job: ' + e.message, 'error');
          }
          this.loading.global = false;
        },

        deleteJob(group, name) {
          this.deleteConfirmMessage = 'Are you sure you want to delete job ' + group + '.' + name + '?';
          this.deletePending = { type: 'job', group: group, name: name };
          this.showDeleteConfirm = true;
        },

        // Delete trigger from job inline details
        deleteJobTrigger(jobGroup, jobName, triggerGroup, triggerName) {
          this.deleteConfirmMessage = 'Are you sure you want to delete trigger ' + triggerGroup + '.' + triggerName + '?';
          this.deletePending = { type: 'trigger', group: triggerGroup, name: triggerName };
          this.showDeleteConfirm = true;
        },

        async executeDelete() {
          if (!this.deletePending) return;
          const { type, group, name } = this.deletePending;
          await this.withActionPending(type === 'job' ? 'delete-job' : 'delete-trigger', group, name, async () => {
            this.loading.global = true;
            try {
              const endpoint = type === 'job'
                ? '/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name)
                : '/triggers/' + encodeURIComponent(group) + '/' + encodeURIComponent(name);

              const res = await fetch(this._api(endpoint), { method: 'DELETE' });
              if (!res.ok) throw new Error(await this.apiErrorMessage(res));

              this.showToast((type === 'job' ? 'Job' : 'Trigger') + ' ' + group + '.' + name + ' deleted', 'success');
              this.showDeleteConfirm = false;
              this.deletePending = null;

              if (type === 'job') await this.loadJobs();
              else await this.loadTriggers();
            } catch (e) {
              this.showToast('Failed to delete: ' + e.message, 'error');
            }
            this.loading.global = false;
          });
        },

        openTriggerJobModal(group, name) {
          this.triggerJobTarget = { group: group, name: name };
          this.triggerJobDataMap = [{ key: '', value: '' }];
          this.showTriggerJobModal = true;
        },

        closeTriggerJobModal() {
          this.showTriggerJobModal = false;
          this.triggerJobTarget = null;
          this.triggerJobDataMap = [];
        },

        addTriggerJobParam() {
          this.triggerJobDataMap.push({ key: '', value: '' });
        },

        removeTriggerJobParam(index) {
          this.triggerJobDataMap.splice(index, 1);
          if (!this.triggerJobDataMap.length) this.triggerJobDataMap = [{ key: '', value: '' }];
        },

        async triggerJob(group, name) {
          await this.withActionPending('trigger', group, name, async () => {
            try {
              const payload = {
                dataMap: Object.fromEntries((this.triggerJobDataMap || []).filter(e => e.key).map(e => [e.key, e.value ?? '']))
              };
              await this.postApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/trigger', payload);
              this.pendingTriggers[group + '.' + name] = Date.now();
              this.showToast('Triggered ' + group + '.' + name, 'success');
              this.closeTriggerJobModal();
            } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
          });
        },

        async pauseJob(group, name) {
          await this.withActionPending('pause', group, name, async () => {
            try {
              await this.postApi('/jobs/' + group + '/' + name + '/pause');
              await this.loadJobs();
              this.showToast('Paused ' + group + '.' + name, 'info');
            } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
          });
        },

        async resumeJob(group, name) {
          await this.withActionPending('resume', group, name, async () => {
            try {
              await this.postApi('/jobs/' + group + '/' + name + '/resume');
              await this.loadJobs();
              this.showToast('Resumed ' + group + '.' + name, 'success');
            } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
          });
        },

        async pauseJobGroup(group) {
          try {
            await this.postApi('/jobs/group/' + encodeURIComponent(group) + '/pause');
            await this.loadJobs();
            this.showToast('Paused all jobs in group "' + group + '"', 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeJobGroup(group) {
          try {
            await this.postApi('/jobs/group/' + encodeURIComponent(group) + '/resume');
            await this.loadJobs();
            this.showToast('Resumed all jobs in group "' + group + '"', 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async interruptJob(group, name) {
          try {
            const res = await this.postApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/interrupt');
            if (res?.interrupted) {
              this.showToast(group + '.' + name + ' interrupted', 'success');
            } else {
              this.showToast(group + '.' + name + ' does not implement IInterruptableJob', 'warning');
            }
          } catch (e) { this.showToast('Failed to interrupt: ' + e.message, 'error'); }
        },

        startEditDataMap() {
          const map = (this.jobDrawerData && this.jobDrawerData.jobDataMap) || {};
          this.jobDrawerDataMapEdits = Object.entries(map).map(([k, v]) => ({
            key: k,
            value: typeof v === 'object' ? JSON.stringify(v) : String(v)
          }));
          this.jobDrawerDataMapEditing = true;
        },

        cancelEditDataMap() {
          this.jobDrawerDataMapEditing = false;
          this.jobDrawerDataMapEdits = [];
        },

        addDataMapRow() {
          this.jobDrawerDataMapEdits.push({ key: '', value: '' });
        },

        removeDataMapRow(idx) {
          this.jobDrawerDataMapEdits.splice(idx, 1);
        },

        async saveDataMap() {
          if (!this.jobDrawerData) return;
          const { group, name } = this.jobDrawerData;
          const jobDataMap = {};
          for (const row of this.jobDrawerDataMapEdits) {
            if (row.key.trim()) jobDataMap[row.key.trim()] = row.value;
          }
          try {
            await this.putApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name), { jobDataMap });
            this.jobDrawerDataMapEditing = false;
            this.jobDrawerDataMapEdits = [];
            await this.loadJobs();
            const refreshed = this.jobs.find(j => j.group === group && j.name === name);
            if (refreshed) this.jobDrawerData = refreshed;
            this.showToast('Data map saved', 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async exportJobs() {
          try {
            const data = await this.fetchApi('/export');
            const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'quartz-jobs-' + new Date().toISOString().slice(0,10) + '.json';
            a.click();
            URL.revokeObjectURL(url);
            this.showToast('Exported ' + (data.jobs?.length || 0) + ' jobs', 'success');
          } catch (e) { this.showToast('Export failed: ' + e.message, 'error'); }
        },

        async importJobs(event) {
          const file = event.target.files[0];
          if (!file) return;
          try {
            const text = await file.text();
            const payload = JSON.parse(text);
            const result = await this.postApi('/import', payload);
            this.showToast('Imported ' + result.jobsCreated + ' jobs, ' + result.triggersCreated + ' triggers' + (result.errors > 0 ? ' (' + result.errors + ' errors)' : ''), result.errors > 0 ? 'error' : 'success');
            await this.loadJobs();
            await this.loadTriggers();
            event.target.value = '';
          } catch (e) { this.showToast('Import failed: ' + e.message, 'error'); event.target.value = ''; }
        },

        // ========================= UI HELPERS =========================
        toggleJobExpand(group, name) {
          const key = group + '.' + name;
          this.expandedJobs[key] = !this.expandedJobs[key];
        },

        hasJobTriggers(job) {
          return job.triggers && job.triggers.length > 0;
        },

        isNewExecutingJob(ej) {
          return ej.fireInstanceId && !this.knownExecutingIds.has(ej.fireInstanceId);
        },
  };
}
