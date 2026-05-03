    function dashboard() {
      return {
        // ========================= STATE =========================
        currentPage: 'overview',
        sidebarOpen: true,
        lastRefreshed: null,
        loading: { global: false, jobs: false, triggers: false, executing: false, history: false, stats: false, timeline: false },
        errors: { jobs: null, triggers: null, executing: null, history: null, stats: null, timeline: null },
        retryCounts: { jobs: 0, triggers: 0, executing: 0, history: 0, stats: 0, timeline: 0 },
        maxRetries: 3,
        retryDelay: 3000,
        scheduler: { isStarted: false, isStandbyMode: false, numberOfJobsExecuted: 0 },
        healthData: null,
        jobs: [],
        triggers: [],
        executingJobs: [],
        history: [],
        historyTotal: 0,
        historyOffset: 0,
        historyLimit: 50,
        stats: {},
        executionBuckets: [],
        toast: { show: false, message: '', type: 'info' },
        autoRefreshTimer: null,
        config: { readOnly: false },
        currentTick: Date.now(),

        // Jobs page
        jobsFilter: '',
        expandedJobs: {},

        // Triggers page
        expandedTriggerGroups: {},

        // Executing page
        knownExecutingIds: new Set(),

        // History page
        historyFilter: '',
        maxHistoryDuration: 0,

        // Graph page
        graphView: 'live',
        graphHistoryData: [],
        graphWidth: 800,
        graphHeight: 320,
        graphMargin: { top: 20, right: 60, bottom: 30, left: 40 },
        graphTooltip: { show: false, x: 0, y: 0, bucket: { minute: '', count: 0, avgDurationMs: 0 } },
        graphMaxCount: 10,
        graphMaxDuration: 1000,
        graphData: [],
        graphChartMode: 'line',
        graphTimeRange: 15,
        graphSeries: { count: true, avgDuration: true, errorRate: true },
        graphCrosshair: { show: false, x: 0, data: null },

        // Sparklines
        sparklineW: 80,
        sparklineH: 30,

        // Settings
        settings: {
          refreshInterval: 5,
          autoRefreshPages: {
            overview: true,
            jobs: true,
            triggers: true,
            executing: true,
            history: true,
            graph: true,
            timeline: true,
            settings: false
          }
        },

        // ========================= JOB DETAIL MODAL STATE =========================
        showJobDetailModal: false,
        jobDetailData: null,
        jobDetailTab: 'metadata',
        jobDetailLogs: [],
        jobDetailLogsLoading: false,

        // ========================= SIGNALR / CONNECTION =========================
        // Debounce utility
        _debounceTimers: {},
        debounce(fn, key, ms) {
          if (this._debounceTimers[key]) clearTimeout(this._debounceTimers[key]);
          this._debounceTimers[key] = setTimeout(() => { delete this._debounceTimers[key]; fn(); }, ms);
        },

        signalRConnected: false,
        signalRPolling: false,
        connection: null,
        connectionAttempts: 0,
        pollingTimer: null,
        signalRTimeout: null,

        // ========================= TOAST QUEUE =========================
        toastQueue: [],
        toastIdCounter: 0,

        // ========================= JOB DETAIL MODAL METHODS =========================
        openJobDetail(job) {
          this.jobDetailData = job;
          this.jobDetailTab = 'metadata';
          this.showJobDetailModal = true;
          this.loadJobDetailLogs(job.group, job.name);
        },
        closeJobDetail() {
          this.showJobDetailModal = false;
          this.jobDetailData = null;
          this.jobDetailLogs = [];
        },
        async loadJobDetailLogs(group, name) {
          this.jobDetailLogsLoading = true;
          try {
            const resp = await this.fetchApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/logs');
            this.jobDetailLogs = resp.logs || [];
          } catch(e) {
            this.jobDetailLogs = ['Failed to load logs'];
          }
          this.jobDetailLogsLoading = false;
        },
        copyJobJson() {
          if (!this.jobDetailData) return;
          const json = JSON.stringify(this.jobDetailData, null, 2);
          if (navigator.clipboard) {
            navigator.clipboard.writeText(json).then(() => this.showToast('Job definition copied', 'success'));
          }
        },
        triggerJobFromModal() {
          if (this.jobDetailData) this.triggerJob(this.jobDetailData.group, this.jobDetailData.name);
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

        // Health computed
        get failedCount() { return this.history ? this.history.filter(h => h.success === false).length : 0; },
        get misfiredCount() { return Array.isArray(this.triggers) ? this.triggers.filter(t => t.state === 'Error').length : 0; },
        get successRate() {
          if (!this.history || !this.history.length) return 100;
          const successes = this.history.filter(h => h.success !== false).length;
          return Math.round(successes / this.history.length * 100);
        },
        get failedHistory() { return this.history ? this.history.filter(h => h.success === false) : []; },
        get poolUtilization() {
          const poolSize = this.scheduler.threadPoolSize || 10;
          const active = this.executingJobs.length;
          return poolSize > 0 ? (active / poolSize) * 100 : 0;
        },

        // ========================= COMMAND PALETTE =========================
        showJobDetailModal: false,
        jobDetailData: null,
        jobDetailTab: 'metadata',
        jobDetailLogs: [],
        jobDetailLogsLoading: false,

        showCommandPalette: false,
        commandPaletteQuery: '',
        commandPaletteIndex: 0,

        // ========================= LIGHT MODE =========================
        lightMode: false,

        // ========================= CREATE JOB MODAL =========================
        showCreateJobModal: false,
        newJob: {
          name: '',
          group: 'DEFAULT',
          description: '',
          jobType: '',
          isDurable: false,
          disallowConcurrentExecution: false,
          persistJobDataAfterExecution: false
        },

        // ========================= CREATE TRIGGER MODAL =========================
        showCreateTriggerModal: false,
        newTrigger: {
          name: '',
          group: 'DEFAULT',
          jobName: '',
          jobGroup: 'DEFAULT',
          description: '',
          triggerType: 'cron',
          cronExpression: '',
          intervalSeconds: null,
          repeatCount: -1,
          priority: 5,
          startTimeUtc: '',
          endTimeUtc: ''
        },

        // ========================= DELETE CONFIRM =========================
        showDeleteConfirm: false,
        deleteConfirmMessage: '',
        deletePending: null,

        // ========================= TIMELINE =========================
        timelineEvents: [],
        timelineWidth: 800,
        timelineHeight: 400,
        timelineRange: 60,
        timelineTooltip: { show: false, event: null, x: 0, y: 0, screenX: 0, screenY: 0 },
        timelineNowInterval: null,
        timelineAnimFrame: null,

        navItems: [
          { id: 'overview', label: 'Overview', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>' },
          { id: 'jobs', label: 'Jobs', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2"/><rect x="9" y="3" width="6" height="4" rx="1"/></svg>' },
          { id: 'triggers', label: 'Triggers', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>' },
          { id: 'executing', label: 'Executing', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><polygon points="10 8 16 12 10 16 10 8" fill="currentColor" stroke="none"/></svg>' },
          { id: 'history', label: 'History', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 8v4l3 3"/><path d="M3.05 11a9 9 0 1 0 .5-3M3 4v4h4"/></svg>' },
          { id: 'graph', label: 'Graph', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M18 20V10"/><path d="M12 20V4"/><path d="M6 20v-6"/></svg>' },
          { id: 'timeline', label: 'Timeline', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><line x1="3" y1="12" x2="21" y2="12"/><polyline points="8 7 3 12 8 17"/><polyline points="16 7 21 12 16 17"/></svg>' },
          { id: 'health', label: 'Health', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>' },
        { id: 'settings', label: 'Settings', icon: '<svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="3"/><path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"/></svg>' },
        ],

        // ========================= COMPUTED =========================
        get filteredJobs() {
          if (!this.jobsFilter) return this.jobs;
          const q = this.jobsFilter.toLowerCase();
          return this.jobs.filter(j =>
            j.name.toLowerCase().includes(q) ||
            j.group.toLowerCase().includes(q) ||
            (j.jobType || '').toLowerCase().includes(q)
          );
        },

        get groupedTriggers() {
          const groups = {};
          const list = Array.isArray(this.triggers) ? this.triggers : [];
          for (const t of list) {
            const key = t.jobGroup + '.' + t.jobName;
            if (!groups[key]) groups[key] = { jobName: key, triggers: [] };
            groups[key].triggers.push(t);
          }
          return Object.values(groups);
        },

        get filteredHistory() {
          if (!this.historyFilter) return this.history;
          const q = this.historyFilter.toLowerCase();
          return this.history.filter(h => (h.jobKey || '').toLowerCase().includes(q));
        },

        get statsLoading() {
          return this.loading.stats;
        },

        get connectionStatus() {
          if (this.signalRConnected) return 'Live';
          if (this.signalRPolling) {
            const secs = Math.floor((Date.now() - (this.lastPollingTime || Date.now())) / 1000);
            return 'Polling (' + secs + 's)';
          }
          return 'Connecting...';
        },

        // ========================= COMMAND PALETTE COMPUTED =========================
        get commandPaletteCommands() {
          const cmds = [];
          for (const item of this.navItems) {
            cmds.push({ id: 'nav-' + item.id, label: 'Go to ' + item.label, icon: item.icon, action: 'navigate', page: item.id, shortcut: this.navItems.indexOf(item) + 1 });
          }
          for (const job of this.jobs) {
            cmds.push({ id: 'trigger-' + job.group + '.' + job.name, label: 'Trigger job ' + job.group + '.' + job.name, icon: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M8 5v14l11-7z"/></svg>', action: 'triggerJob', group: job.group, name: job.name });
          }
          return cmds;
        },

        get filteredCommands() {
          if (!this.commandPaletteQuery) return this.commandPaletteCommands;
          const q = this.commandPaletteQuery.toLowerCase();
          return this.commandPaletteCommands.filter(c => c.label.toLowerCase().includes(q));
        },

        // ========================= TIMELINE COMPUTED =========================
        get timelineYLabels() {
          const labels = [];
          for (const evt of this.timelineEvents) {
            if (!labels.includes(evt.jobKey)) labels.push(evt.jobKey);
          }
          return labels;
        },

        get now() {
          return this.currentTick;
        },

        get timelineRangeMs() {
          return this.timelineRange * 60 * 1000;
        },

        get timelineVisibleEvents() {
          const cutoff = this.now - this.timelineRangeMs;
          return this.timelineEvents.filter(e => new Date(e.fireTime).getTime() >= cutoff);
        },

        get timelineVisibleLabels() {
          const labels = [];
          for (const evt of this.timelineVisibleEvents) {
            if (!labels.includes(evt.jobKey)) labels.push(evt.jobKey);
          }
          return labels;
        },

        get timelineLabelWidth() { return 160; },
        get timelineRowHeight() { return 52; },
        get timelineAxisHeight() { return 32; },
        get timelineChartHeight() {
          return Math.max(120, this.timelineVisibleLabels.length * this.timelineRowHeight + this.timelineAxisHeight + 16);
        },
          return 8 + idx * this.timelineRowHeight;
        },

        timelineXForTime(timeMs) {
          const w = this.timelineWidth - 16;
          const leftTime = this.now - this.timelineRangeMs;
          const frac = (timeMs - leftTime) / this.timelineRangeMs;
          return Math.max(0, Math.min(w, frac * w + 8));
        },

        timelineBarWidth(durationMs) {
          const w = this.timelineWidth - 16;
          return Math.max(4, (durationMs / this.timelineRangeMs) * w);
        },

        timelineYForJob(jobKey) {
          const idx = this.timelineVisibleLabels.indexOf(jobKey);
          if (idx === -1) return 20;
          return this.timelineRowY(idx) + this.timelineRowHeight / 2;
        },

        get timelineGridLines() {
          const ticks = 8;
          const lines = [];
          for (let i = 0; i <= ticks; i++) {
            const t = this.now - this.timelineRangeMs + (i / ticks) * this.timelineRangeMs;
            const x = this.timelineXForTime(t);
            const dt = new Date(t);
            lines.push({ x, label: dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: ticks <= 4 ? '2-digit' : undefined }) });
          }
          return lines;
        },

        get timelineStats() {
          const evts = this.timelineVisibleEvents;
          const total = evts.length;
          const success = evts.filter(e => e.success).length;
          const avgDur = total ? (evts.reduce((a, e) => a + (e.duration || 0), 0) / total) : 0;
          return { total, success, failed: total - success, avgDur };
        },

        // ========================= INIT =========================
        async init() {
          // Load theme preference
          const savedTheme = localStorage.getItem('quartz-dashboard-theme');
          if (savedTheme === 'light') {
            this.lightMode = true;
            document.documentElement.classList.remove('dark');
            document.documentElement.classList.add('light');
            document.body.classList.add('light');
          }

          // Setup keyboard shortcuts
          document.addEventListener('keydown', (e) => this.handleKeydown(e));

          // Live-tick every second for executing-job duration display
          setInterval(() => { this.currentTick = Date.now(); }, 1000);

          // Start SignalR connection
          await this.connectSignalR();

          // Fallback: if SignalR doesn't connect in 5 seconds, start polling
          this.signalRTimeout = setTimeout(() => {
            if (!this.signalRConnected) {
              this.signalRPolling = true;
              this.lastPollingTime = Date.now();
              this.startPollingFallback();
            }
          }, 5000);

          // Initial data load
          await this.refreshAll();
          await this.loadHistory();
          await this.loadStats();

          this.startAutoRefresh();
          this.$watch('currentPage', (val) => { this.onPageChange(val); });
          this.$watch('settings.refreshInterval', () => { this.startAutoRefresh(); });
          if (this.$refs && this.$refs.graphContainer) {
            this.updateGraphSize();
          }
          window.addEventListener('resize', () => this.updateGraphSize());

          // Start timeline now ticker (currentTick already ticks every 1s above)
          this.timelineNowInterval = setInterval(() => {}, 1000); // kept for cleanup compat
        },

        // ========================= SIGNALR =========================
        async connectSignalR() {
          try {
            this.connection = new signalR.HubConnectionBuilder()
              .withUrl(this._base() + '/hub')
              .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
              .build();

            this.connection.on('jobExecutedBatch', (events) => {
              events.forEach(e => this.handleJobExecuted(e));
            });

            this.connection.on('jobTriggeredBatch', (events) => {
              events.forEach(e => this.handleJobTriggered(e));
            });

            this.connection.on('schedulerStatus', (data) => {
              this.handleSchedulerStatus(data);
            });

            this.connection.on('jobsUpdated', (data) => {
              this.handleJobsUpdated(data);
            });

            this.connection.onreconnecting(() => {
              this.signalRConnected = false;
            });

            this.connection.onreconnected(() => {
              this.signalRConnected = true;
              this.signalRPolling = false;
              this.stopPollingFallback();
              this.refreshAll();
            });

            this.connection.onclose(() => {
              this.signalRConnected = false;
              if (!this.signalRPolling) {
                this.signalRPolling = true;
                this.lastPollingTime = Date.now();
                this.startPollingFallback();
              }
            });

            await this.connection.start();
            await this.connection.invoke('Subscribe');
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
          // Append to history
          if (data.jobKey) {
            this.history.unshift(data);
            if (this.history.length > 500) this.history.length = 500;
            // Update maxHistoryDuration
            const d = data.durationMs || 0;
            if (d > this.maxHistoryDuration) this.maxHistoryDuration = d;
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
          // Silently refresh jobs and triggers
          this.loadJobs();
          this.loadTriggers();
        },

        // ========================= POLLING FALLBACK =========================
        startPollingFallback() {
          this.stopPollingFallback();
          this.pollingTimer = setInterval(() => {
            this.refreshAll();
            this.lastPollingTime = Date.now();
          }, this.settings.refreshInterval * 1000);
        },

        stopPollingFallback() {
          if (this.pollingTimer) {
            clearInterval(this.pollingTimer);
            this.pollingTimer = null;
          }
        },

        // ========================= KEYBOARD SHORTCUTS =========================
        handleKeydown(e) {
          // Command palette: Cmd+K or Ctrl+K
          if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
            e.preventDefault();
            this.openCommandPalette();
            return;
          }

          // Escape: close modals
          if (e.key === 'Escape') {
            if (this.showCommandPalette) { this.showCommandPalette = false; return; }
            if (this.showCreateJobModal) { this.showCreateJobModal = false; return; }
            if (this.showCreateTriggerModal) { this.showCreateTriggerModal = false; return; }
            if (this.showDeleteConfirm) { this.showDeleteConfirm = false; return; }
            return;
          }

          // If command palette is open, handle arrow keys internally
          if (this.showCommandPalette) return;

          // Number keys 1-N: switch pages (N = number of nav items)
          const num = parseInt(e.key);
          if (num >= 1 && num <= this.navItems.length && !e.metaKey && !e.ctrlKey && !e.altKey) {
            const idx = num - 1;
            if (idx < this.navItems.length) {
              this.currentPage = this.navItems[idx].id;
              e.preventDefault();
            }
            return;
          }

          // r: refresh
          if (e.key === 'r' && !e.metaKey && !e.ctrlKey && !e.altKey && !e.shiftKey) {
            // Only if no input is focused
            if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA' && e.target.tagName !== 'SELECT') {
              e.preventDefault();
              this.refreshPage(this.currentPage);
            }
            return;
          }

          // /: focus search on jobs or history page
          if (e.key === '/' && !e.metaKey && !e.ctrlKey && !e.altKey) {
            if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
              if (this.currentPage === 'jobs') {
                e.preventDefault();
                // Focus the jobs search input - find it in DOM
                const input = document.querySelector('input[x-model="jobsFilter"]');
                if (input) input.focus();
              } else if (this.currentPage === 'history') {
                e.preventDefault();
                const input = document.querySelector('input[x-model="historyFilter"]');
                if (input) input.focus();
              }
            }
          }
        },

        // ========================= COMMAND PALETTE =========================
        openCommandPalette() {
          this.showCommandPalette = true;
          this.commandPaletteQuery = '';
          this.commandPaletteIndex = 0;
          this.$nextTick(() => {
            const input = this.$refs && this.$refs.commandPaletteInput;
            if (input) input.focus();
          });
        },

        commandPalettePrev() {
          if (this.filteredCommands.length === 0) return;
          this.commandPaletteIndex = (this.commandPaletteIndex - 1 + this.filteredCommands.length) % this.filteredCommands.length;
          this.scrollCommandIntoView();
        },

        commandPaletteNext() {
          if (this.filteredCommands.length === 0) return;
          this.commandPaletteIndex = (this.commandPaletteIndex + 1) % this.filteredCommands.length;
          this.scrollCommandIntoView();
        },

        scrollCommandIntoView() {
          this.$nextTick(() => {
            const list = this.$refs && this.$refs.commandPaletteList;
            if (!list) return;
            const items = list.querySelectorAll('.command-item');
            if (items[this.commandPaletteIndex]) {
              items[this.commandPaletteIndex].scrollIntoView({ block: 'nearest' });
            }
          });
        },

        commandPaletteSelect() {
          const cmds = this.filteredCommands;
          if (cmds.length > 0 && this.commandPaletteIndex < cmds.length) {
            this.executeCommand(cmds[this.commandPaletteIndex]);
          }
        },

        executeCommand(cmd) {
          this.showCommandPalette = false;
          if (cmd.action === 'navigate') {
            this.currentPage = cmd.page;
          } else if (cmd.action === 'triggerJob') {
            this.triggerJob(cmd.group, cmd.name);
          }
        },

        // ========================= LIGHT MODE =========================
        toggleLightMode() {
          this.lightMode = !this.lightMode;
          if (this.lightMode) {
            document.documentElement.classList.remove('dark');
            document.documentElement.classList.add('light');
            document.body.classList.add('light');
            localStorage.setItem('quartz-dashboard-theme', 'light');
          } else {
            document.documentElement.classList.remove('light');
            document.documentElement.classList.add('dark');
            document.body.classList.remove('light');
            localStorage.setItem('quartz-dashboard-theme', 'dark');
          }
        },

        // ========================= CREATE JOB =========================
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
            if (!res.ok) throw new Error(res.status + ' ' + res.statusText);

            this.showToast('Job ' + this.newJob.name + ' created', 'success');
            this.showCreateJobModal = false;
            this.newJob = { name: '', group: 'DEFAULT', description: '', jobType: '', isDurable: false, disallowConcurrentExecution: false, persistJobDataAfterExecution: false };
            await this.loadJobs();
          } catch (e) {
            this.showToast('Failed to create job: ' + e.message, 'error');
          }
          this.loading.global = false;
        },

        // ========================= CREATE TRIGGER =========================
        async submitCreateTrigger() {
          if (!this.newTrigger.name || !this.newTrigger.jobName) return;
          if (this.newTrigger.triggerType === 'cron' && !this.newTrigger.cronExpression) return;
          if (this.newTrigger.triggerType === 'simple' && !this.newTrigger.intervalSeconds) return;

          this.loading.global = true;
          try {
            const body = { name: this.newTrigger.name };
            if (this.newTrigger.group && this.newTrigger.group !== 'DEFAULT') body.group = this.newTrigger.group;
            body.jobName = this.newTrigger.jobName;
            if (this.newTrigger.jobGroup && this.newTrigger.jobGroup !== 'DEFAULT') body.jobGroup = this.newTrigger.jobGroup;
            if (this.newTrigger.description) body.description = this.newTrigger.description;

            if (this.newTrigger.triggerType === 'cron') {
              body.cronExpression = this.newTrigger.cronExpression;
            } else {
              body.intervalSeconds = this.newTrigger.intervalSeconds;
              if (this.newTrigger.repeatCount !== null && this.newTrigger.repeatCount !== undefined) {
                body.repeatCount = this.newTrigger.repeatCount;
              }
            }

            if (this.newTrigger.priority !== null && this.newTrigger.priority !== undefined) {
              body.priority = this.newTrigger.priority;
            }
            if (this.newTrigger.startTimeUtc) {
              const d = new Date(this.newTrigger.startTimeUtc);
              body.startTimeUtc = d.toISOString();
            }
            if (this.newTrigger.endTimeUtc) {
              const d = new Date(this.newTrigger.endTimeUtc);
              body.endTimeUtc = d.toISOString();
            }

            const res = await fetch(this._api('/triggers'), {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body)
            });
            if (!res.ok) throw new Error(res.status + ' ' + res.statusText);

            this.showToast('Trigger ' + this.newTrigger.name + ' created', 'success');
            this.showCreateTriggerModal = false;
            this.newTrigger = { name: '', group: 'DEFAULT', jobName: '', jobGroup: 'DEFAULT', description: '', triggerType: 'cron', cronExpression: '', intervalSeconds: null, repeatCount: -1, priority: 5, startTimeUtc: '', endTimeUtc: '' };
            await this.loadTriggers();
          } catch (e) {
            this.showToast('Failed to create trigger: ' + e.message, 'error');
          }
          this.loading.global = false;
        },

        // ========================= DELETE JOB =========================
        deleteJob(group, name) {
          this.deleteConfirmMessage = 'Are you sure you want to delete job ' + group + '.' + name + '?';
          this.deletePending = { type: 'job', group: group, name: name };
          this.showDeleteConfirm = true;
        },

        // ========================= DELETE TRIGGER =========================
        deleteTrigger(group, name) {
          this.deleteConfirmMessage = 'Are you sure you want to delete trigger ' + group + '.' + name + '?';
          this.deletePending = { type: 'trigger', group: group, name: name };
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
          this.loading.global = true;
          try {
            const { type, group, name } = this.deletePending;
            const endpoint = type === 'job'
              ? '/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name)
              : '/triggers/' + encodeURIComponent(group) + '/' + encodeURIComponent(name);

            const res = await fetch(this._api(endpoint), { method: 'DELETE' });
            if (!res.ok) {
              const text = await res.text();
              throw new Error(text || res.statusText);
            }

            this.showToast((type === 'job' ? 'Job' : 'Trigger') + ' ' + group + '.' + name + ' deleted', 'success');
            this.showDeleteConfirm = false;
            this.deletePending = null;

            if (type === 'job') await this.loadJobs();
            else await this.loadTriggers();
          } catch (e) {
            this.showToast('Failed to delete: ' + e.message, 'error');
          }
          this.loading.global = false;
        },

        // ========================= TIMELINE =========================
        async loadHealth() {
          try {
            this.healthData = await this.fetchApi('/health');
          } catch (e) {
            console.error('loadHealth:', e);
          }
        },

        async loadTimeline() {
          this.loading.timeline = true;
          try {
            const data = await this.fetchApi('/timeline');
            this.timelineEvents = data.slice(0, 50);
            this.errors.timeline = null; this.retryCounts.timeline = 0;
          } catch (e) {
            console.error('loadTimeline:', e);
            this.errors.timeline = e.message;
            this.showToast('Failed to load timeline: ' + e.message, 'error');
            this._retryLoad('timeline', () => this.loadTimeline());
          }
          this.loading.timeline = false;
        },

        addTimelineEvent(data) {
          const evt = {
            jobKey: data.jobKey || (data.jobName ? data.jobGroup + '.' + data.jobName : ''),
            triggerKey: data.triggerKey || (data.triggerName ? data.triggerGroup + '.' + data.triggerName : ''),
            fireTime: data.fireTime,
            duration: data.duration,
            durationMs: data.durationMs,
            success: data.success !== false
          };
          this.timelineEvents.unshift(evt);
          if (this.timelineEvents.length > 50) this.timelineEvents.length = 50;
        },

        updateGraphSize() {
          const container = this.$refs && this.$refs.graphContainer;
          if (container) {
            this.graphWidth = Math.max(400, container.clientWidth || 800);
          }
          const tlContainer = this.$refs && this.$refs.timelineContainer;
          if (tlContainer) {
            this.timelineWidth = Math.max(600, tlContainer.clientWidth - 144 || 800);
            this.timelineHeight = Math.max(200, tlContainer.clientHeight || 400);
          }
        },

        onPageChange(page) {
          if (page === 'history') this.loadHistory();
          if (page === 'graph') this.loadStats();
          if (page === 'triggers') this.loadTriggers();
          if (page === 'executing') this.loadExecutingJobs();
          if (page === 'timeline') {
            this.loadTimeline();
            this.$nextTick(() => this.updateGraphSize());
          }
        },

        // ========================= AUTO-REFRESH =========================
        startAutoRefresh() {
          if (this.autoRefreshTimer) clearInterval(this.autoRefreshTimer);
          const ms = this.settings.refreshInterval * 1000;
          this.autoRefreshTimer = setInterval(() => {
            // If SignalR is connected and the page supports real-time, skip polling refresh for those pages
            if (this.signalRConnected) {
              // Timeline, executing are real-time via SignalR
              if (this.currentPage === 'timeline' || this.currentPage === 'executing') return;
            }
            const page = this.currentPage;
            if (this.settings.autoRefreshPages[page]) {
              this.refreshPage(page);
            }
          }, ms);
        },

        toggleAutoRefresh(pageId) {
          this.settings.autoRefreshPages[pageId] = !this.settings.autoRefreshPages[pageId];
        },

        async refreshPage(page) {
          switch (page) {
            case 'overview': await this.refreshAll(); break;
            case 'jobs': await this.loadJobs(); break;
            case 'triggers': await this.loadTriggers(); break;
            case 'executing': await this.loadExecutingJobs(); break;
            case 'history': await this.loadHistory(); break;
            case 'graph': await this.loadStats(); break;
            case 'timeline': await this.loadTimeline(); break;
            case 'health': await this.loadHealth(); break;
          }
        },

        // ========================= TOAST =========================
        showToast(msg, type = 'info') {
          const id = ++this.toastIdCounter;
          this.toastQueue.push({ id: id, message: msg, type: type });
          if (this.toastQueue.length > 10) this.toastQueue.shift();
          // Also keep the legacy toast for backward compatibility
          this.toast = { show: true, message: msg, type };
          setTimeout(() => {
            this.toastQueue = this.toastQueue.filter(t => t.id !== id);
            this.toast.show = false;
          }, 3000);
        },

        // ========================= API =========================
        _base() { return window.__QUARTZ_BASE || '/quartz'; },
        _api(path) { return this._base() + '/api' + path; },

        async fetchApi(path) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url);
          if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
          return res.json();
        },

        async postApi(path) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url, { method: 'POST' });
          if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
          return res.json();
        },

        async refreshAll() {
          try {
            const [scheduler, jobsResp, triggersResp, executingResp] = await Promise.all([
              this.fetchApi('/scheduler').catch(() => this.scheduler),
              this.fetchApi('/jobs').catch(() => ({ data: this.jobs })),
              this.fetchApi('/triggers').catch(() => ({ data: this.triggers })),
              this.fetchApi('/executing').catch(() => ({ data: this.executingJobs })),
            ]);
            this.scheduler = scheduler;
            this.jobs = Array.isArray(jobsResp) ? jobsResp : (Array.isArray(jobsResp?.data) ? jobsResp.data : []);
            this.triggers = Array.isArray(triggersResp) ? triggersResp : (Array.isArray(triggersResp?.data) ? triggersResp.data : []);
            this.executingJobs = Array.isArray(executingResp) ? executingResp : (Array.isArray(executingResp?.data) ? executingResp.data : []);
            this.lastRefreshed = new Date();
          } catch (e) {
            console.error('Refresh error:', e);
            this.errors.jobs = 'Refresh failed: ' + e.message;
            this.errors.triggers = 'Refresh failed: ' + e.message;
            this.errors.executing = 'Refresh failed: ' + e.message;
            this.showToast('Refresh failed: ' + e.message, 'error');
          }
        },

        async loadJobs() {
          this.loading.jobs = true;
          try { const resp = await this.fetchApi('/jobs'); this.jobs = Array.isArray(resp) ? resp : (Array.isArray(resp?.data) ? resp.data : []); this.errors.jobs = null; this.retryCounts.jobs = 0; } catch (e) { console.error('loadJobs:', e); this.errors.jobs = e.message; this.showToast('Failed to load jobs: ' + e.message, 'error'); this._retryLoad('jobs', () => this.loadJobs()); }
          this.loading.jobs = false;
        },

        async loadTriggers() {
          this.loading.triggers = true;
          try {
            const resp = await this.fetchApi('/triggers');
            const list = Array.isArray(resp) ? resp : (resp.data ?? resp ?? []);
            this.triggers = Array.isArray(list) ? list : [];
            this.errors.triggers = null; this.retryCounts.triggers = 0;
            const groups = {};
            for (const t of this.triggers) {
              const key = (t.jobGroup || '') + '.' + (t.jobName || '');
              groups[key] = true;
            }
            this.expandedTriggerGroups = groups;
          } catch (e) { console.error('loadTriggers:', e); this.errors.triggers = e.message; this.showToast('Failed to load triggers: ' + e.message, 'error'); this._retryLoad('triggers', () => this.loadTriggers()); }
          this.loading.triggers = false;
        },

        async loadExecutingJobs() {
          this.loading.executing = true;
          const prevIds = this.knownExecutingIds;
          try {
            const resp = await this.fetchApi('/executing');
            this.executingJobs = Array.isArray(resp) ? resp : (resp.data || []);
            this.knownExecutingIds = new Set(this.executingJobs.map(ej => ej.fireInstanceId));
            this.errors.executing = null; this.retryCounts.executing = 0;
          } catch (e) { console.error('loadExecutingJobs:', e); this.errors.executing = e.message; this.showToast('Failed to load executing jobs: ' + e.message, 'error'); this._retryLoad('executing', () => this.loadExecutingJobs()); }
          this.loading.executing = false;
        },

        async loadHistory() {
          this.loading.history = true;
          try {
            this.historyOffset = 0;
            const resp = await this.fetchApi('/history?limit=' + this.historyLimit + '&offset=0');
            this.history = resp.data || [];
            this.historyTotal = resp.total || 0;
            this.maxHistoryDuration = 0;
            for (const h of this.history) {
              const d = h.duration || 0;
              if (d > this.maxHistoryDuration) this.maxHistoryDuration = d;
            }
            if (this.maxHistoryDuration === 0) this.maxHistoryDuration = 5000;
            this.errors.history = null; this.retryCounts.history = 0;
          } catch (e) { console.error('loadHistory:', e); this.errors.history = e.message; this.showToast('Failed to load history: ' + e.message, 'error'); this._retryLoad('history', () => this.loadHistory()); }
          this.loading.history = false;
        },

        async loadMoreHistory() {
          const nextOffset = this.historyOffset + this.historyLimit;
          if (nextOffset >= this.historyTotal) return;
          try {
            const resp = await this.fetchApi('/history?limit=' + this.historyLimit + '&offset=' + nextOffset);
            this.history = this.history.concat(resp.data || []);
            this.historyTotal = resp.total || 0;
            this.historyOffset = nextOffset;
            this.errors.history = null;
          } catch (e) { this.showToast('Failed to load more history: ' + e.message, 'error'); }
        },

        async loadStats() {
          this.loading.stats = true;
          try {
            this.stats = await this.fetchApi('/stats');
            this.executionBuckets = this.stats.executionBuckets || [];
            this.graphData = this.getGraphData();
            this.errors.stats = null; this.retryCounts.stats = 0;
          } catch (e) { console.error('loadStats:', e); this.errors.stats = e.message; this.showToast('Failed to load stats: ' + e.message, 'error'); this._retryLoad('stats', () => this.loadStats()); }
          this.loading.stats = false;
        },

        getGraphData() {
          const buckets = this.executionBuckets || [];
          if (this.graphView === 'live') {
            return buckets.slice(-Math.max(this.graphTimeRange, 1));
          }
          // 'history' view — return cached historical data (loaded separately)
          return this.graphHistoryData || [];
        },

        async loadGraphHistoryData() {
          try {
            this.graphHistoryData = await this.fetchApi('/stats/history');
            this.graphData = this.getGraphData();
          } catch (e) { this.showToast('Failed to load history graph: ' + e.message, 'error'); }
        },

        // ========================= JOB ACTIONS =========================
        async startScheduler() {
          this.loading.global = true;
          try {
            await this.postApi('/scheduler/start');
            this.showToast('Scheduler started', 'success');
            await this.refreshAll();
          } catch (e) { this.showToast('Failed to start: ' + e.message, 'error'); }
          this.loading.global = false;
        },

        async standbyScheduler() {
          this.loading.global = true;
          try {
            await this.postApi('/scheduler/standby');
            this.showToast('Scheduler on standby', 'info');
            await this.refreshAll();
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
          this.loading.global = false;
        },

        async triggerJob(group, name) {
          try {
            await this.postApi('/jobs/' + group + '/' + name + '/trigger');
            this.showToast('Triggered ' + group + '.' + name, 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async pauseJob(group, name) {
          try {
            await this.postApi('/jobs/' + group + '/' + name + '/pause');
            await this.loadJobs();
            this.showToast('Paused ' + group + '.' + name, 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeJob(group, name) {
          try {
            await this.postApi('/jobs/' + group + '/' + name + '/resume');
            await this.loadJobs();
            this.showToast('Resumed ' + group + '.' + name, 'success');
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

        async pauseTrigger(group, name) {
          try {
            await this.postApi('/triggers/' + group + '/' + name + '/pause');
            await this.loadTriggers();
            this.showToast('Paused trigger ' + group + '.' + name, 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeTrigger(group, name) {
          try {
            await this.postApi('/triggers/' + group + '/' + name + '/resume');
            await this.loadTriggers();
            this.showToast('Resumed trigger ' + group + '.' + name, 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        // ========================= UI HELPERS =========================
        toggleJobExpand(group, name) {
          const key = group + '.' + name;
          this.expandedJobs[key] = !this.expandedJobs[key];
        },

        toggleTriggerGroup(idx) {
          this.expandedTriggerGroups[idx] = !this.expandedTriggerGroups[idx];
        },

        hasJobTriggers(job) {
          return job.triggers && job.triggers.length > 0;
        },

        isNewExecutingJob(ej) {
          return ej.fireInstanceId && !this.knownExecutingIds.has(ej.fireInstanceId);
        },

        relativeTime(dateStr) {
          if (!dateStr) return '\u2014';
          const now = new Date();
          const target = new Date(dateStr);
          const diffMs = target - now;
          if (diffMs <= 0) return 'now';
          const secs = Math.floor(diffMs / 1000);
          if (secs < 60) return secs + 's';
          const mins = Math.floor(secs / 60);
          const remSecs = secs % 60;
          if (mins < 60) return mins + 'm ' + remSecs + 's';
          const hours = Math.floor(mins / 60);
          const remMins = mins % 60;
          return hours + 'h ' + remMins + 'm';
        },

        formatDuration(d) {
          if (!d) return '';
          if (typeof d === 'string') {
            return d.replace('PT', '').replace('H', 'h ').replace('M', 'm ').replace('S', 's');
          }
          if (typeof d === 'object') {
            const secs = (d.hours || 0) * 3600 + (d.minutes || 0) * 60 + (d.seconds || 0) + (d.milliseconds || 0) / 1000;
            if (secs < 1) return Math.round(secs * 1000) + 'ms';
            if (secs < 60) return secs.toFixed(1) + 's';
            return Math.floor(secs / 60) + 'm ' + Math.round(secs % 60) + 's';
          }
          return d;
        },

        formatDate(d) {
          if (!d) return '';
          const dt = new Date(d);
          if (isNaN(dt.getTime())) return '';
          return dt.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
        },

        formatTimeShort(d) {
          if (!d) return '';
          const dt = new Date(d);
          if (isNaN(dt.getTime())) return '';
          return dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        },

        // ========================= CHART ENGINE - COMPUTED PROPERTIES =========================
        graphX(idx) {
          const margin = this.graphMargin;
          const data = this.graphData;
          const w = this.graphWidth - margin.left - margin.right;
          return margin.left + (idx / Math.max(data.length - 1, 1)) * w;
        },

        graphY(val, maxVal) {
          const margin = this.graphMargin;
          const h = this.graphHeight - margin.top - margin.bottom;
          const max = maxVal || 1;
          return margin.top + h - (val / max) * h;
        },

        get graphGradientDefs() {
          return ChartEngine.gradientDefs('graph');
        },

        get graphYTicks() {
          const maxVal = Math.max(...this.graphData.map(b => b.count || 0), 10);
          if (maxVal > this.graphMaxCount) this.graphMaxCount = maxVal;
          return ChartEngine.yAxisTicks(maxVal, this.graphHeight, this.graphMargin, 5);
        },

        get graphXLabels() {
          return ChartEngine.xAxisTimeLabels(this.graphData, 'minute', this.graphWidth, this.graphMargin, 8).map(l => ({
            ...l,
            label: this.formatTimeShort(l.label)
          }));
        },

        get graphGridLines() {
          const maxVal = Math.max(...this.graphData.map(b => b.count || 0), 10);
          const ticks = ChartEngine.yAxisTicks(maxVal, this.graphHeight, this.graphMargin, 5);
          return ChartEngine.gridLines(ticks, this.graphWidth, this.graphMargin);
        },

        get graphCountLinePath() {
          if (!this.graphData || this.graphData.length < 2) return '';
          const maxVal = Math.max(...this.graphData.map(b => b.count || 0), 1);
          this.graphMaxCount = maxVal;
          const xScale = ChartEngine.scaleLinear(this.graphMargin.left, this.graphWidth - this.graphMargin.right, 0, this.graphData.length - 1);
          const yScale = ChartEngine.scaleLinear(this.graphHeight - this.graphMargin.bottom, this.graphMargin.top, 0, maxVal);
          return ChartEngine.smoothPath(this.graphData, null, 'count', xScale, yScale);
        },

        get graphCountAreaPath() {
          if (!this.graphData || this.graphData.length < 2) return '';
          const maxVal = Math.max(...this.graphData.map(b => b.count || 0), 1);
          const xScale = ChartEngine.scaleLinear(this.graphMargin.left, this.graphWidth - this.graphMargin.right, 0, this.graphData.length - 1);
          const yScale = ChartEngine.scaleLinear(this.graphHeight - this.graphMargin.bottom, this.graphMargin.top, 0, maxVal);
          return ChartEngine.areaPath(this.graphData, null, 'count', xScale, yScale, this.graphHeight - this.graphMargin.bottom);
        },

        get graphDurationLinePath() {
          if (!this.graphData || this.graphData.length < 2) return '';
          const maxVal = Math.max(...this.graphData.map(b => b.avgDurationMs || 0), 1);
          this.graphMaxDuration = maxVal;
          const xScale = ChartEngine.scaleLinear(this.graphMargin.left, this.graphWidth - this.graphMargin.right, 0, this.graphData.length - 1);
          const yScale = ChartEngine.scaleLinear(this.graphHeight - this.graphMargin.bottom, this.graphMargin.top, 0, maxVal);
          return ChartEngine.smoothPath(this.graphData, null, 'avgDurationMs', xScale, yScale);
        },

        get graphErrorLinePath() {
          if (!this.graphData || this.graphData.length < 2) return '';
          const maxVal = Math.max(...this.graphData.map(b => b.errorRate || 0), 0.01);
          const xScale = ChartEngine.scaleLinear(this.graphMargin.left, this.graphWidth - this.graphMargin.right, 0, this.graphData.length - 1);
          const yScale = ChartEngine.scaleLinear(this.graphHeight - this.graphMargin.bottom, this.graphMargin.top, 0, maxVal);
          return ChartEngine.smoothPath(this.graphData, null, 'errorRate', xScale, yScale);
        },

        get graphBarRects() {
          if (!this.graphData || !this.graphData.length) return [];
          const maxVal = Math.max(...this.graphData.map(b => b.count || 0), 1);
          this.graphMaxCount = maxVal;
          return ChartEngine.barRects(this.graphData, 'count', this.graphWidth, this.graphHeight, this.graphMargin);
        },

        get graphHeatCells() {
          if (!this.graphData || !this.graphData.length) return [];
          const rows = 8;
          const cols = Math.min(this.graphData.length, 60);
          return ChartEngine.heatmapCells(this.graphData, 'count', this.graphWidth, this.graphHeight, this.graphMargin, rows, cols);
        },

        get graphTooltipTime() {
          if (!this.graphCrosshair || !this.graphCrosshair.data) return '';
          return this.formatTimeShort(this.graphCrosshair.data.minute);
        },

        // ========================= GRAPH EVENT HANDLERS =========================
        onGraphMouseMove(event) {
          const container = this.$refs && this.$refs.graphContainer;
          if (!container) return;
          const rect = container.getBoundingClientRect();
          const svgX = event.clientX - rect.left;
          const margin = this.graphMargin;
          const data = this.graphData;
          if (!data || data.length < 2) return;
          const chartW = this.graphWidth - margin.left - margin.right;
          const normalizedX = (svgX - margin.left) / chartW;
          const idx = Math.round(normalizedX * (data.length - 1));
          const clampedIdx = Math.max(0, Math.min(data.length - 1, idx));
          this.graphCrosshair = {
            show: true,
            x: margin.left + (clampedIdx / Math.max(data.length - 1, 1)) * chartW,
            data: Object.assign({}, data[clampedIdx])
          };
        },

        loadGraphData() {
          this.loadStats();
        },

        // ========================= ERROR RECOVERY =========================
        async _retryLoad(page, loadFn) {
          this.retryCounts[page] = (this.retryCounts[page] || 0) + 1;
          if (this.retryCounts[page] <= this.maxRetries) {
            await new Promise(r => setTimeout(r, this.retryDelay));
            try {
              await loadFn();
              this.errors[page] = null;
              this.retryCounts[page] = 0;
            } catch (e2) {
              this.errors[page] = 'Retry ' + this.retryCounts[page] + '/' + this.maxRetries + ' failed: ' + e2.message;
              if (this.retryCounts[page] < this.maxRetries) {
                await this._retryLoad(page, loadFn);
              }
            }
          }
        },

        async retryAllFailed() {
          const pages = ['jobs', 'triggers', 'executing', 'history', 'stats', 'timeline'];
          for (const page of pages) {
            if (this.errors[page]) {
              this.errors[page] = null;
              this.retryCounts[page] = 0;
            }
          }
          await Promise.all([
            this.loadJobs(),
            this.loadTriggers(),
            this.loadExecutingJobs(),
            this.loadHistory(),
            this.loadStats(),
            this.loadTimeline(),
          ]);
        },
      }         // closes return object
    }           // closes dashboard()
    window.dashboard = dashboard;
