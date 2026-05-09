    function dashboard() {
      return {
        // ========================= STATE =========================
        currentPage: 'overview',
        sidebarOpen: true,
        lastRefreshed: null,
        loading: { global: false, jobs: false, triggers: false, executing: false, history: false, stats: false, timeline: false, calendars: false },
        errors: { jobs: null, triggers: null, executing: null, history: null, stats: null, timeline: null, calendars: null },
        retryCounts: { jobs: 0, triggers: 0, executing: 0, history: 0, stats: 0, timeline: 0, calendars: 0 },
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
        historyCurrentPage: 1,
        historyPageSize: 50,
        stats: {},
        executionBuckets: [],
        toast: { show: false, message: '', type: 'info' },
        autoRefreshTimer: null,
        config: { readOnly: false },
        currentTick: Date.now(),

        // Multi-scheduler
        schedulers: [],
        activeSchedulerName: '',

        // Jobs page
        jobsFilter: '',
        expandedJobs: {},

        // Triggers page
        expandedTriggerGroups: {},
        triggersFilter: '',

        // Executing page
        knownExecutingIds: new Set(),

        // History page
        historyFilter: '',
        historyFilterObj: { search: '', status: 'all', dateFrom: '', dateTo: '' },
        maxHistoryDuration: 0,
        historyExpandedRows: {},

        // Job run result feedback
        pendingTriggers: {},
        jobFlash: {},
        showTriggerJobModal: false,
        triggerJobTarget: null,
        triggerJobDataMap: [],

        // Job data map editing
        jobDrawerDataMapEditing: false,
        jobDrawerDataMapEdits: [],

        // Stats trend
        statsPrev: null,
        statsSnapshot: null,

        // Collapsible job groups
        collapsedGroups: {},

        // Now ticker (1s resolution, for countdowns and live durations)
        nowTick: Date.now(),

        // Keyboard navigation
        selectedJobIndex: -1,
        _gPressed: false,

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
            health: true,
            calendars: true,
            settings: false
          }
        },

        // ========================= JOB DETAIL DRAWER STATE =========================
        showJobDrawer: false,
        jobDrawerData: null,
        jobDrawerTab: 'overview',
        jobDrawerHistory: [],
        jobDrawerHistoryLoading: false,
        jobDrawerLogs: [],
        jobDrawerLogsLoading: false,
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

        // ========================= JOB DETAIL DRAWER METHODS =========================
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
        triggerJobFromDrawer() {
          if (this.jobDrawerData) this.openTriggerJobModal(this.jobDrawerData.group, this.jobDrawerData.name);
        },
        pauseJobFromDrawer() {
          if (this.jobDrawerData) this.pauseJob(this.jobDrawerData.group, this.jobDrawerData.name);
        },
        resumeJobFromDrawer() {
          if (this.jobDrawerData) this.resumeJob(this.jobDrawerData.group, this.jobDrawerData.name);
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

        // Health computed
        get failedCount() { return this.history ? this.history.filter(h => h.success === false).length : 0; },
        get misfiredCount() { return Array.isArray(this.triggers) ? this.triggers.filter(t => t.state === 'Error').length : 0; },
        get successRate() {
          if (!this.history || !this.history.length) return 100;
          const successes = this.history.filter(h => h.success !== false).length;
          return Math.round(successes / this.history.length * 100);
        },
        get failedHistory() { return this.history ? this.history.filter(h => h.success === false) : []; },
        jobSuccessRate(group, name) {
          const key = group + '.' + name;
          const relevant = (this.history || []).filter(h => h.jobKey === key);
          if (!relevant.length) return null;
          const success = relevant.filter(h => h.success).length;
          return Math.round((success / relevant.length) * 100);
        },
        get failuresByHour() {
          const now = new Date();
          now.setMinutes(0, 0, 0);
          const failed = this.history ? this.history.filter(h => h.success === false && h.fireTime) : [];
          const buckets = [];
          for (let i = 23; i >= 0; i--) {
            const start = new Date(now.getTime() - i * 60 * 60 * 1000);
            const end = new Date(start.getTime() + 60 * 60 * 1000);
            let count = 0;
            for (const item of failed) {
              const time = new Date(item.fireTime).getTime();
              if (time >= start.getTime() && time < end.getTime()) count++;
            }
            buckets.push({ hour: 23 - i, count: count, label: start.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) });
          }
          return buckets;
        },
        get poolUtilization() {
          const poolSize = this.scheduler.threadPoolSize || 10;
          const active = this.executingJobs.length;
          return poolSize > 0 ? (active / poolSize) * 100 : 0;
        },
        get recentFailures() {
          return (this.history || []).filter(h => !h.success).slice(0, 5);
        },
        get uptimePercent() {
          const h = this.history || [];
          if (!h.length) return 100;
          const success = h.filter(x => x.success).length;
          return Math.round((success / h.length) * 1000) / 10;
        },

        // ========================= COMMAND PALETTE =========================        showCommandPalette: false,
        commandPaletteQuery: '',
        commandPaletteIndex: 0,

        // ========================= SHORTCUTS MODAL =========================
        showShortcutsModal: false,
        shortcutsList: [
          { key: '1', label: 'Overview' },
          { key: '2', label: 'Jobs' },
          { key: '3', label: 'Triggers' },
          { key: '4', label: 'Executing' },
          { key: '5', label: 'History' },
          { key: '6', label: 'Graph' },
          { key: '7', label: 'Timeline' },
          { key: '8', label: 'Health' },
          { key: '9', label: 'Calendars' },
          { key: 'S', label: 'Settings' },
          { key: 'J/K', label: 'Prev / Next row' },
        ],

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
        showEditTriggerModal: false,
        editTriggerData: null,
        cronNextFires: [],
        cronValid: null,
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

        // ========================= CALENDARS =========================
        calendars: [],
        showCreateCalendarModal: false,
        newCalendar: {
          name: '',
          type: 'holiday',
          cronExpression: '',
          description: ''
        },

        // ========================= DELETE CONFIRM =========================
        showDeleteConfirm: false,
        deleteConfirmMessage: '',
        deletePending: null,

        // ========================= TIMELINE =========================
        timelineEvents: [],
        timelineWidth: 800,
        timelineHeight: 400,
        timelineRange: 10,
        timelineCursor: { show: false, x: 0, timeMs: 0, rowIdx: -1, events: [], bar: null },
        timelineNowInterval: null,
        timelineAnimFrame: null,

        navItems: [
          { id: 'overview', label: 'Overview', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>' },
          { id: 'jobs', label: 'Jobs', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2"/><rect x="9" y="3" width="6" height="4" rx="1"/></svg>' },
          { id: 'triggers', label: 'Triggers', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>' },
          { id: 'executing', label: 'Executing', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><polygon points="10 8 16 12 10 16 10 8" fill="currentColor" stroke="none"/></svg>' },
          { id: 'history', label: 'History', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 8v4l3 3"/><path d="M3.05 11a9 9 0 1 0 .5-3M3 4v4h4"/></svg>' },
          { id: 'graph', label: 'Graph', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M18 20V10"/><path d="M12 20V4"/><path d="M6 20v-6"/></svg>' },
          { id: 'timeline', label: 'Timeline', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><line x1="3" y1="12" x2="21" y2="12"/><polyline points="8 7 3 12 8 17"/><polyline points="16 7 21 12 16 17"/></svg>' },
          { id: 'health', label: 'Health', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>' },
          { id: 'calendars', label: 'Calendars', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>' },
          { id: 'settings', label: 'Settings', icon: '<svg style="width:20px;height:20px;flex-shrink:0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="3"/><path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"/></svg>' },
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
            if (!groups[key]) groups[key] = { jobName: key, jobGroup: t.jobGroup, jobNameOnly: t.jobName, triggers: [] };
            groups[key].triggers.push(t);
          }
          return Object.values(groups);
        },

        get filteredGroupedTriggers() {
          const q = (this.triggersFilter || '').toLowerCase().trim();
          if (!q) return this.groupedTriggers;
          return this.groupedTriggers
            .map(g => ({
              ...g,
              triggers: g.triggers.filter(t =>
                t.name.toLowerCase().includes(q) ||
                t.group.toLowerCase().includes(q) ||
                t.jobName.toLowerCase().includes(q) ||
                t.jobGroup.toLowerCase().includes(q) ||
                (t.state || '').toLowerCase().includes(q) ||
                (t.type || '').toLowerCase().includes(q) ||
                (t.scheduleDescription || '').toLowerCase().includes(q)
              )
            }))
            .filter(g => g.triggers.length > 0 || g.jobName.toLowerCase().includes(q));
        },

        get filteredHistory() {
          let arr = this.history || [];
          const f = this.historyFilterObj;
          if (f.search) {
            const q = f.search.toLowerCase();
            arr = arr.filter(h => (h.jobKey || '').toLowerCase().includes(q) || (h.triggerKey || '').toLowerCase().includes(q));
          }
          if (f.status === 'success') arr = arr.filter(h => h.success);
          if (f.status === 'error') arr = arr.filter(h => !h.success);
          return arr;
        },

        get historyFiltered() {
          return this.filteredHistory;
        },

        get historyPageCount() {
          const pageSize = Math.max(this.historyPageSize || this.historyLimit || 50, 1);
          const total = this.historyTotal || 0;
          return Math.max(1, Math.ceil(total / pageSize));
        },

        get historyPageNumbers() {
          const total = this.historyPageCount;
          const current = this.historyCurrentPage;
          if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

          const items = [1];
          const start = Math.max(2, current - 1);
          const end = Math.min(total - 1, current + 1);
          if (start > 2) items.push('…');
          for (let page = start; page <= end; page++) items.push(page);
          if (end < total - 1) items.push('…');
          items.push(total);
          return items;
        },

        // Flat list: header items + job items interleaved.
        // Single x-for in template — avoids nested template x-for scope chain issues in Alpine.js.
        get jobRows() {
          const jobs = this.jobsFilter ? this.filteredJobs : (this.jobs || []);
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
          // Add trigger names for quick navigation
          for (const t of this.triggers) {
            cmds.push({ id: 'view-trigger-' + t.key, label: 'View trigger ' + (t.key || t.name), icon: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>', action: 'navigate', page: 'triggers' });
          }
          return cmds;
        },

        get filteredCommands() {
          const q = this.commandPaletteQuery.toLowerCase().trim();
          if (!q) return this.commandPaletteCommands.slice(0, 15);
          const cmds = this.commandPaletteCommands.filter(c => c.label.toLowerCase().includes(q));
          // Also search recent history by job key
          if (cmds.length < 8) {
            const historyHits = (this.history || []).filter(h => h.jobKey && h.jobKey.toLowerCase().includes(q)).slice(0, 5);
            for (const h of historyHits) {
              cmds.push({ id: 'history-' + h.jobKey + '-' + h.fireTime, label: 'History: ' + h.jobKey, icon: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>', action: 'navigate', page: 'history' });
            }
          }
          return cmds;
        },

        // ========================= TIMELINE COMPUTED =========================
        get timelineYLabels() {
          const labels = [];
          for (const evt of this.timelineEvents) {
            if (!labels.includes(evt.jobKey)) labels.push(evt.jobKey);
          }
          return labels.sort((a, b) => a.localeCompare(b));
        },

        get now() {
          return this.currentTick;
        },

        get timelineRangeMs() {
          return this.timelineRange * 60 * 1000;
        },

        get timelineVisibleEvents() {
          const cutoff = Date.now() - this.timelineRangeMs;
          return this.timelineEvents.filter(e => new Date(e.fireTime).getTime() >= cutoff);
        },

        get timelineVisibleLabels() {
          const labels = [];
          for (const evt of this.timelineVisibleEvents) {
            if (!labels.includes(evt.jobKey)) labels.push(evt.jobKey);
          }
          return labels.sort((a, b) => a.localeCompare(b));
        },

        get timelineLabelWidth() { return 160; },
        get timelineRowHeight() { return 52; },
        get timelineAxisHeight() { return 32; },
        get timelineChartHeight() {
          return Math.max(120, this.timelineVisibleLabels.length * this.timelineRowHeight + this.timelineAxisHeight + 16);
        },

        timelineRowY(idx) {
          return 8 + idx * this.timelineRowHeight;
        },

        timelineXForTime(timeMs) {
          const chartWidth = Math.max(1, this.timelineWidth - this.timelineLabelWidth);
          const leftTime = this.now - this.timelineRangeMs;
          const frac = (timeMs - leftTime) / this.timelineRangeMs;
          return Math.max(0, Math.min(chartWidth, frac * chartWidth));
        },

        timelineBarWidth(durationMs) {
          const chartWidth = Math.max(1, this.timelineWidth - this.timelineLabelWidth);
          return Math.max(4, (durationMs / this.timelineRangeMs) * chartWidth);
        },

        timelineYForJob(jobKey) {
          const idx = this.timelineVisibleLabels.indexOf(jobKey);
          if (idx === -1) return 20;
          return this.timelineRowY(idx) + this.timelineRowHeight / 2;
        },

        get timelineGridLines() {
          const ticks = 8;
          const lines = [];
          const chartWidth = Math.max(1, this.timelineWidth - this.timelineLabelWidth);
          for (let i = 0; i <= ticks; i++) {
          const t = Date.now() - this.timelineRangeMs + (i / ticks) * this.timelineRangeMs;
            const x = (i / ticks) * chartWidth;
            const dt = new Date(t);
            const showSec = this.timelineRange <= 5;
            lines.push({ x, label: dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: showSec ? '2-digit' : undefined }) });
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

        getChartColors() {
          return this.lightMode
            ? {
                primary: '#4f46e5',
                text: '#374151',
                muted: '#6b7280',
                grid: 'rgba(17,24,39,0.08)',
                border: 'rgba(17,24,39,0.12)',
                panel: 'rgba(255,255,255,0.72)',
                rowAlt: 'rgba(79,70,229,0.04)',
                crosshair: 'rgba(55,65,81,0.18)'
              }
            : {
                primary: '#818cf8',
                text: '#9ca3af',
                muted: '#4b5563',
                grid: 'rgba(255,255,255,0.05)',
                border: 'rgba(255,255,255,0.06)',
                panel: 'rgba(0,0,0,0.2)',
                rowAlt: 'rgba(255,255,255,0.014)',
                crosshair: 'rgba(255,255,255,0.1)'
              };
        },

        // Render timeline Gantt chart via innerHTML (bypasses Alpine SVG namespace issues)
        updateTimelineChart() {
          const el = this.$refs && this.$refs.timelineChartWrap;
          if (!el) return;
          const evts = this.timelineVisibleEvents;
          const labels = this.timelineVisibleLabels;
          if (!evts.length || !labels.length) { el.innerHTML = ''; return; }

          const w = this.timelineWidth;
          const labelW = this.timelineLabelWidth;
          const rowH = this.timelineRowHeight;
          const axisH = this.timelineAxisHeight;
          const chartH = this.timelineChartHeight;
          const chartWidth = Math.max(1, w - labelW);
          const gridLines = this.timelineGridLines;
          const now = Date.now();
          const rangeMs = this.timelineRangeMs;
          const leftTime = now - rangeMs;
          const chartColors = this.getChartColors();

          // Per-job color palette
          const colorPalette = ['#818cf8','#34d399','#fbbf24','#f87171','#c084fc','#38bdf8','#fb923c'];
          const jobColors = {};
          labels.forEach((lbl, i) => { jobColors[lbl] = colorPalette[i % colorPalette.length]; });

          // Count executions per job in visible range
          const jobCounts = {};
          for (const evt of evts) { jobCounts[evt.jobKey] = (jobCounts[evt.jobKey] || 0) + 1; }

          const rowBg = labels.map((label, idx) => {
            const y = 8 + idx * rowH;
            return `<rect x="0" y="${y}" width="${w}" height="${rowH - 1}" fill="${idx % 2 === 0 ? chartColors.rowAlt : 'rgba(0,0,0,0)'}"/>`;
          }).join('');

          const rowLabels = labels.map((label, idx) => {
            const y = 8 + idx * rowH;
            const color = jobColors[label] || '#818cf8';
            const jobName = label.split('.').pop();
            const groupName = label.split('.')[0];
            const count = jobCounts[label] || 0;
            const truncated = jobName.length > 16 ? jobName.slice(0, 15) + '…' : jobName;
            return `
              <rect x="3" y="${y + 10}" width="3" height="${rowH - 20}" rx="1.5" fill="${color}" fill-opacity="0.85"/>
              <text x="12" y="${y + rowH / 2 - 5}" dominant-baseline="middle" fill="${chartColors.text}" font-size="11" font-family="ui-monospace,monospace">${truncated}</text>
              <text x="12" y="${y + rowH / 2 + 9}" dominant-baseline="middle" fill="${chartColors.muted}" font-size="9" font-family="ui-monospace,monospace">${groupName}</text>
              <text x="${labelW - 6}" y="${y + rowH / 2 + 1}" text-anchor="end" dominant-baseline="middle" fill="${color}" font-size="9" font-family="ui-monospace,monospace" opacity="0.9">${count}×</text>`;
          }).join('');

          const vGridLines = gridLines.map(gl =>
            `<line x1="${labelW + gl.x}" y1="0" x2="${labelW + gl.x}" y2="${chartH - axisH}" stroke="${chartColors.grid}" stroke-width="1" stroke-dasharray="2,3"/>`
          ).join('');

          // Build gradient defs per job
          const gradDefs = labels.map(lbl => {
            const color = jobColors[lbl];
            const id = 'tl-grad-' + lbl.replace(/[^a-zA-Z0-9]/g, '_');
            return `<linearGradient id="${id}" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0%" stop-color="${color}" stop-opacity="0.95"/>
              <stop offset="100%" stop-color="${color}" stop-opacity="0.65"/>
            </linearGradient>`;
          }).join('');

          const MIN_BAR_PX = 4; // minimum visible bar width in pixels
          const bars = evts.map(evt => {
            const jobIdx = labels.indexOf(evt.jobKey);
            if (jobIdx === -1) return '';

            const t = new Date(evt.fireTime).getTime();
            const durationMs = evt.duration || 0;

            // Compute raw chart-space positions (0 = left edge, chartWidth = "now")
            const rawStart = ((t - leftTime) / rangeMs) * chartWidth;
            const rawEnd   = rawStart + (durationMs / rangeMs) * chartWidth;

            // Clamp to visible area
            const clampedStart = Math.max(0, rawStart);
            const clampedEnd   = Math.min(chartWidth, rawEnd);

            // Skip bars entirely outside the visible window
            if (clampedStart >= chartWidth || clampedEnd <= 0) return '';

            // Ensure a minimum visible width; never push bar off the right edge
            const rawWidth   = clampedEnd - clampedStart;
            const barWidth   = Math.min(chartWidth - clampedStart, Math.max(MIN_BAR_PX, rawWidth));
            const barX       = labelW + clampedStart;
            const barY       = 8 + jobIdx * rowH;

            const color  = jobColors[evt.jobKey] || '#818cf8';
            const gradId = 'tl-grad-' + evt.jobKey.replace(/[^a-zA-Z0-9]/g, '_');
            const errorAttr = evt.errorMessage ? ` data-error="${evt.errorMessage.replace(/"/g, '&quot;')}"` : '';
            const successStroke = evt.success ? '' : ` stroke="#f87171" stroke-width="1.5" stroke-opacity="0.9"`;
            return `<rect x="${barX.toFixed(1)}" y="${barY + 8}" width="${barWidth.toFixed(1)}" height="${rowH - 16}" rx="3"
              fill="url(#${gradId})"${successStroke}
              style="cursor:pointer"
              class="tl-bar"
              data-key="${evt.jobKey}"
              data-trigger="${evt.triggerKey || ''}"
              data-time="${evt.fireTime}"
              data-dur="${evt.duration || 0}"
              data-success="${evt.success}"
              data-row="${jobIdx}"${errorAttr}/>`;
          }).join('');

          const nowX = labelW + chartWidth;
          const nowLine = `<line x1="${nowX}" y1="0" x2="${nowX}" y2="${chartH - axisH}" stroke="${chartColors.primary}" stroke-width="2" stroke-dasharray="4,3" opacity="0.9"/>
            <text x="${nowX - 3}" y="${chartH - axisH - 4}" text-anchor="end" fill="${chartColors.primary}" font-size="8" font-family="ui-monospace,monospace" opacity="0.8">now</text>`;

          const axisLabels = gridLines.map(gl =>
            `<text x="${labelW + gl.x}" y="${chartH - axisH + 18}" text-anchor="middle" fill="${chartColors.muted}" font-size="9" font-family="ui-monospace,monospace">${gl.label}</text>`
          ).join('');

          // Separator line between label panel and chart area
          const separator = `<line x1="${labelW}" y1="0" x2="${labelW}" y2="${chartH - axisH}" stroke="${chartColors.border}" stroke-width="1"/>`;

          el.innerHTML = `<svg width="${w}" height="${chartH}" style="width:100%;display:block;overflow:visible">
            <defs>
              <clipPath id="tl-clip"><rect x="${labelW}" y="0" width="${chartWidth}" height="${chartH - axisH}"/></clipPath>
              ${gradDefs}
            </defs>
            ${rowBg}
            <rect x="0" y="0" width="${labelW}" height="${chartH - axisH}" fill="${chartColors.panel}"/>
            ${rowLabels}
            ${separator}
            <g clip-path="url(#tl-clip)">
              ${vGridLines}
              ${bars}
              ${nowLine}
            </g>
            <line x1="0" y1="${chartH - axisH}" x2="${w}" y2="${chartH - axisH}" stroke="${chartColors.border}" stroke-width="1"/>
            ${axisLabels}
          </svg>`;

          // Build HTML action-button overlay for each timeline row
          const overlayParent = el.parentElement;
          let overlay = overlayParent.querySelector('.tl-action-overlay');
          if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'tl-action-overlay';
            overlay.style.cssText = 'position:absolute;top:0;left:0;pointer-events:none;width:100%;';
            overlayParent.style.position = 'relative';
            overlayParent.appendChild(overlay);
          }
          overlay.innerHTML = labels.map((lbl, rowIndex) => {
            const y = 8 + rowIndex * rowH;
            const parts = lbl.split('.');
            const grp = parts[0];
            const nm = parts.slice(1).join('.') || parts[0];
            return `<div class="tl-row-actions" data-row="${rowIndex}" style="position:absolute;top:${y}px;left:0;width:${labelW - 4}px;height:${rowH - 1}px;pointer-events:auto;display:flex;align-items:center;justify-content:flex-end;gap:2px;padding-right:24px;opacity:0;transition:opacity 0.15s;"
              onmouseenter="this.style.opacity=1" onmouseleave="this.style.opacity=0">
              <button title="Run Now" onclick="window.dashboard && document.querySelector('[x-data]')?._x_dataStack?.[0]?.triggerJob('${grp}','${nm}')" style="background:rgba(99,102,241,0.7);border:none;border-radius:4px;padding:2px 5px;cursor:pointer;color:#fff;font-size:10px;">▶</button>
              <button title="Pause" onclick="window.dashboard && document.querySelector('[x-data]')?._x_dataStack?.[0]?.pauseJob('${grp}','${nm}')" style="background:rgba(245,158,11,0.7);border:none;border-radius:4px;padding:2px 5px;cursor:pointer;color:#fff;font-size:10px;">⏸</button>
              <button title="Resume" onclick="window.dashboard && document.querySelector('[x-data]')?._x_dataStack?.[0]?.resumeJob('${grp}','${nm}')" style="background:rgba(52,211,153,0.7);border:none;border-radius:4px;padding:2px 5px;cursor:pointer;color:#fff;font-size:10px;">↺</button>
            </div>`;
          }).join('');
        },

        // ========================= INIT =========================
        async init() {
          // Sync Alpine lightMode state with what the inline script already applied to <html>
          const savedTheme = localStorage.getItem('quartz-dashboard-theme');
          const isLight = savedTheme === 'light' ||
            (!savedTheme && window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches);
          if (isLight) {
            this.lightMode = true;
            document.documentElement.classList.remove('dark');
            document.documentElement.classList.add('light');
            document.body.classList.add('light');
          }

          // Load persistent settings
          try {
            const saved = JSON.parse(localStorage.getItem('qd-settings') || '{}');
            if (saved.sidebarOpen !== undefined) this.sidebarOpen = saved.sidebarOpen;
            if (saved.graphChartMode) this.graphChartMode = saved.graphChartMode;
            if (saved.refreshInterval) this.settings.refreshInterval = saved.refreshInterval;
            if (saved.historyLimit) {
              this.historyLimit = saved.historyLimit;
              this.historyPageSize = saved.historyLimit;
            }
            if (saved.collapsedGroups && Object.values(saved.collapsedGroups).some(v => !v)) {
              // Only restore if at least one group is NOT collapsed (avoid all-collapsed corrupted state)
              this.collapsedGroups = saved.collapsedGroups;
            }
          } catch(_) {}

          // Setup keyboard shortcuts
          document.addEventListener('keydown', (e) => this.handleKeydown(e));

          // Live-tick every second for executing-job duration display and countdowns
          setInterval(() => { this.currentTick = Date.now(); this.nowTick = Date.now(); }, 1000);

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
          await this.loadConfig();
          await this.refreshAll();
          await this.loadHistory();
          await this.loadStats();

          this.startAutoRefresh();
          this.$watch('currentPage', (val) => { this.onPageChange(val); });
          this.$watch('settings.refreshInterval', () => { this.startAutoRefresh(); this.saveSettings(); });
          this.$watch('sidebarOpen', () => this.saveSettings());
          this.$watch('graphChartMode', () => this.saveSettings());
          this.$watch('historyLimit', () => this.saveSettings());
          this.$watch('collapsedGroups', () => this.saveSettings());
          if (this.$refs && this.$refs.graphContainer) {
            this.updateGraphSize();
          }
          // Use ResizeObserver for precise container-aware resizing
          const resizeObserver = new ResizeObserver(() => {
            this.updateGraphSize();
            this.updateGraphChart();
            this.updateTimelineChart();
          });
          this.$nextTick(() => {
            const gc = this.$refs.graphContainer;
            const tc = this.$refs.timelineContainer;
            if (gc) resizeObserver.observe(gc);
            if (tc) resizeObserver.observe(tc);
          });
          // Fallback for window resize
          window.addEventListener('resize', () => this.updateGraphSize());
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

              // Show failure toast for failed executions
              for (const e of events) {
                if (!e.success) {
                  this.showToast('⚠ ' + e.jobKey + ' failed' + (e.exceptionMessage ? ': ' + e.exceptionMessage.substring(0, 80) : ''), 'error');
                }
              }
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
            if (this.showShortcutsModal) { this.showShortcutsModal = false; return; }
            if (this.showCreateJobModal) { this.showCreateJobModal = false; return; }
            if (this.showCreateTriggerModal) { this.showCreateTriggerModal = false; return; }
            if (this.showDeleteConfirm) { this.showDeleteConfirm = false; return; }
            if (this.showJobDrawer) { this.closeJobDrawer(); return; }
            return;
          }

          // If command palette is open, handle arrow keys internally
          if (this.showCommandPalette) return;

          // Skip when typing in an input
          if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;

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
            e.preventDefault();
            this.refreshPage(this.currentPage);
            return;
          }

          // /: focus search on jobs or history page
          if (e.key === '/' && !e.metaKey && !e.ctrlKey && !e.altKey) {
            if (this.currentPage === 'jobs') {
              e.preventDefault();
              const input = document.querySelector('input[x-model="jobsFilter"]');
              if (input) input.focus();
            } else if (this.currentPage === 'history') {
              e.preventDefault();
              const input = document.querySelector('input[x-model="historyFilterObj.search"]');
              if (input) input.focus();
            }
            return;
          }

          // g + key: navigate to page
          if (e.key === 'g') {
            this._gPressed = true;
            setTimeout(() => { this._gPressed = false; }, 1000);
            return;
          }
          if (this._gPressed) {
            const map = { o: 'overview', j: 'jobs', t: 'triggers', h: 'history', e: 'executing', l: 'timeline', x: 'graph', s: 'settings' };
            if (map[e.key]) { this.currentPage = map[e.key]; this._gPressed = false; e.preventDefault(); return; }
          }

          // j/k: row navigation in jobs table
          if (this.currentPage === 'jobs' && (e.key === 'j' || e.key === 'k')) {
            const jobs = this.filteredJobs || this.jobs || [];
            if (!jobs.length) return;
            const cur = this.selectedJobIndex ?? -1;
            const next = e.key === 'j' ? Math.min(cur + 1, jobs.length - 1) : Math.max(cur - 1, 0);
            this.selectedJobIndex = next;
            e.preventDefault();
            return;
          }
          if (this.currentPage === 'jobs' && e.key === 'Enter' && this.selectedJobIndex >= 0) {
            const jobs = this.filteredJobs || this.jobs || [];
            const job = jobs[this.selectedJobIndex];
            if (job) this.openJobDrawer(job);
            return;
          }

          // ?: show keyboard shortcuts overlay
          if (e.key === '?' && !e.metaKey && !e.ctrlKey && !e.altKey) {
            e.preventDefault();
            this.showShortcutsModal = !this.showShortcutsModal;
            return;
          }

          // t: toggle theme
          if (e.key === 't' && !e.metaKey && !e.ctrlKey && !e.altKey) {
            e.preventDefault();
            this.toggleLightMode();
            return;
          }

          // [: toggle sidebar
          if (e.key === '[' && !e.metaKey && !e.ctrlKey && !e.altKey) {
            e.preventDefault();
            this.sidebarOpen = !this.sidebarOpen;
            return;
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
            this.openTriggerJobModal(cmd.group, cmd.name);
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
          this.$nextTick(() => {
            this.updateGraphChart();
            this.updateTimelineChart();
          });
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
        async validateCron(expr) {
          if (!expr || expr.length < 5) { this.cronNextFires = []; this.cronValid = null; return; }
          try {
            const resp = await this.postApi('/cron/describe', { expression: expr });
            this.cronValid = resp.valid;
            this.cronNextFires = (resp.nextFireTimes || []).map(t => new Date(t).toLocaleString());
          } catch { this.cronValid = false; this.cronNextFires = []; }
        },

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
            this.cronNextFires = [];
            this.cronValid = null;
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
            success: data.success !== false,
            errorMessage: data.errorMessage || data.exceptionMessage || null,
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
          if (page === 'graph') {
            this.loadStats();
            // Double-RAF ensures container is visible and laid out before measuring
            requestAnimationFrame(() => requestAnimationFrame(() => this.updateGraphSize()));
          }
          if (page === 'triggers') this.loadTriggers();
          if (page === 'executing') this.loadExecutingJobs();
          if (page === 'calendars') this.loadCalendars();
          if (page === 'timeline') {
            this.loadTimeline();
            requestAnimationFrame(() => requestAnimationFrame(() => this.updateGraphSize()));
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
            case 'calendars': await this.loadCalendars(); break;
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
        _api(path) {
          const base = this._base() + '/api' + path;
          if (this.activeSchedulerName && !path.includes('scheduler')) {
            const sep = base.includes('?') ? '&' : '?';
            return base + sep + 'scheduler=' + encodeURIComponent(this.activeSchedulerName);
          }
          return base;
        },

        async fetchApi(path) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url);
          if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
          return res.json();
        },

        async postApi(path, body) {
          const url = path.startsWith('http') ? path : this._api(path);
          const options = { method: 'POST', headers: {} };
          if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
          }
          const res = await fetch(url, options);
          if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
          return res.json();
        },

        async putApi(path, body) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body || {})
          });
          if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
          return res.json();
        },

        async loadConfig() {
          try {
            const cfg = await this.fetchApi('/config');
            if (cfg) {
              this.config = { ...this.config, ...cfg };
              if (cfg.title && cfg.title !== 'QuartzDash') {
                document.title = cfg.title;
              }
            }
          } catch(e) { /* best-effort */ }

          // Load scheduler list for multi-scheduler support
          try {
            const scheds = await this.fetchApi('/schedulers');
            if (Array.isArray(scheds)) {
              this.schedulers = scheds;
              const current = scheds.find(s => s.isCurrent);
              if (current) this.activeSchedulerName = current.name;
            }
          } catch(e) { /* single-scheduler mode — no /schedulers endpoint or error */ }
        },

        onSchedulerChange() {
          // Append ?scheduler=name to all API calls by storing the active name
          // fetchApi already appends it via _api()
          this.refreshAll();
          this.loadHistory();
          this.loadStats();
          this.loadCalendars();
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
            this.jobs = (Array.isArray(jobsResp) ? jobsResp : (Array.isArray(jobsResp?.data) ? jobsResp.data : []))
              .sort((a, b) => (a.group + '.' + a.name).localeCompare(b.group + '.' + b.name));
            this.triggers = (Array.isArray(triggersResp) ? triggersResp : (Array.isArray(triggersResp?.data) ? triggersResp.data : []))
              .sort((a, b) => (a.jobGroup + '.' + a.jobName + '/' + a.group + '.' + a.name).localeCompare(b.jobGroup + '.' + b.jobName + '/' + b.group + '.' + b.name));
            this.executingJobs = (Array.isArray(executingResp) ? executingResp : (Array.isArray(executingResp?.data) ? executingResp.data : []))
              .sort((a, b) => (a.jobGroup + '.' + a.jobName).localeCompare(b.jobGroup + '.' + b.jobName));
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
          try { const resp = await this.fetchApi('/jobs'); this.jobs = (Array.isArray(resp) ? resp : (Array.isArray(resp?.data) ? resp.data : [])).sort((a, b) => (a.group + '.' + a.name).localeCompare(b.group + '.' + b.name)); this.errors.jobs = null; this.retryCounts.jobs = 0; } catch (e) { console.error('loadJobs:', e); this.errors.jobs = e.message; this.showToast('Failed to load jobs: ' + e.message, 'error'); this._retryLoad('jobs', () => this.loadJobs()); }
          this.loading.jobs = false;
        },

        async loadTriggers() {
          this.loading.triggers = true;
          try {
            const resp = await this.fetchApi('/triggers');
            const list = Array.isArray(resp) ? resp : (resp.data ?? resp ?? []);
            this.triggers = (Array.isArray(list) ? list : [])
              .sort((a, b) => (a.jobGroup + '.' + a.jobName + '/' + a.group + '.' + a.name).localeCompare(b.jobGroup + '.' + b.jobName + '/' + b.group + '.' + b.name));
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
            this.executingJobs = (Array.isArray(resp) ? resp : (resp.data || []))
              .sort((a, b) => (a.jobGroup + '.' + a.jobName).localeCompare(b.jobGroup + '.' + b.jobName));
            this.knownExecutingIds = new Set(this.executingJobs.map(ej => ej.fireInstanceId));
            this.errors.executing = null; this.retryCounts.executing = 0;
          } catch (e) { console.error('loadExecutingJobs:', e); this.errors.executing = e.message; this.showToast('Failed to load executing jobs: ' + e.message, 'error'); this._retryLoad('executing', () => this.loadExecutingJobs()); }
          this.loading.executing = false;
        },

        async loadHistory(page) {
          this.loading.history = true;
          try {
            const pageSize = Math.max(this.historyPageSize || this.historyLimit || 50, 1);
            if (page) this.historyCurrentPage = page;
            if (this.historyCurrentPage < 1) this.historyCurrentPage = 1;
            this.historyLimit = pageSize;
            this.historyOffset = (this.historyCurrentPage - 1) * pageSize;
            const resp = await this.fetchApi('/history?limit=' + pageSize + '&offset=' + this.historyOffset);
            this.history = resp.data || [];
            this.historyTotal = resp.total || 0;
            this.maxHistoryDuration = 0;
            for (const h of this.history) {
              const d = h.duration || h.durationMs || 0;
              if (d > this.maxHistoryDuration) this.maxHistoryDuration = d;
            }
            if (this.maxHistoryDuration === 0) this.maxHistoryDuration = 5000;
            this.errors.history = null; this.retryCounts.history = 0;
          } catch (e) { console.error('loadHistory:', e); this.errors.history = e.message; this.showToast('Failed to load history: ' + e.message, 'error'); this._retryLoad('history', () => this.loadHistory()); }
          this.loading.history = false;
        },

        async historyGoToPage(page) {
          if (page < 1 || page > this.historyPageCount || page === this.historyCurrentPage) return;
          await this.loadHistory(page);
        },

        async loadStats() {
          this.loading.stats = true;
          try {
            const newStats = await this.fetchApi('/stats');
            newStats.totalJobs = this.jobs.length;
            newStats.totalTriggers = this.triggers.length;
            newStats.executing = this.executingJobs.length;
            newStats.totalExecutions = newStats.totalExecutions ?? this.scheduler.numberOfJobsExecuted ?? 0;
            // Track trend: move current to prev after 15s
            if (this.statsSnapshot && (Date.now() - this.statsSnapshot > 15000)) {
              this.statsPrev = Object.assign({}, this.stats);
              this.statsSnapshot = Date.now();
            } else if (!this.statsSnapshot) {
              this.statsSnapshot = Date.now();
            }
            this.stats = newStats;
            this.executionBuckets = this.stats.executionBuckets || [];
            this.graphData = this.getGraphData();
            this.errors.stats = null; this.retryCounts.stats = 0;
          } catch (e) { console.error('loadStats:', e); this.errors.stats = e.message; this.showToast('Failed to load stats: ' + e.message, 'error'); this._retryLoad('stats', () => this.loadStats()); }
          this.loading.stats = false;
        },

        getGraphData() {
          const buckets = this.executionBuckets || [];
          let data;
          if (this.graphView === 'live') {
            data = buckets.slice(-Math.max(this.graphTimeRange, 1));
          } else {
            return this.graphHistoryData || [];
          }
          // Pad with zero-buckets so line chart always has enough points to draw
          if (data.length < this.graphTimeRange) {
            const now = Date.now();
            const pad = [];
            for (let i = this.graphTimeRange - data.length; i > 0; i--) {
              const d = new Date(now - i * 60000);
              pad.push({ minute: d.toISOString(), label: d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }), count: 0, avgDurationMs: 0, errorRate: 0 });
            }
            data = [...pad, ...data];
          }
          return data;
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
          try {
            const payload = {
              dataMap: Object.fromEntries((this.triggerJobDataMap || []).filter(e => e.key).map(e => [e.key, e.value ?? '']))
            };
            await this.postApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/trigger', payload);
            this.pendingTriggers[group + '.' + name] = Date.now();
            this.showToast('Triggered ' + group + '.' + name, 'success');
            this.closeTriggerJobModal();
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

        async pauseTriggerGroup(group) {
          try {
            await this.postApi('/triggers/group/' + encodeURIComponent(group) + '/pause');
            await this.loadTriggers();
            this.showToast('Paused all triggers in group "' + group + '"', 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeTriggerGroup(group) {
          try {
            await this.postApi('/triggers/group/' + encodeURIComponent(group) + '/resume');
            await this.loadTriggers();
            this.showToast('Resumed all triggers in group "' + group + '"', 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
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

        openEditTrigger(trigger) {
          this.editTriggerData = {
            group: trigger.group,
            name: trigger.name,
            triggerType: (trigger.type || '').toLowerCase().includes('cron') ? 'cron' : 'simple',
            cronExpression: trigger.cronExpression || '',
            intervalSeconds: trigger.intervalSeconds || null,
            misfireInstruction: trigger.misfireInstructionValue || 'smartPolicy'
          };
          this.showEditTriggerModal = true;
        },

        async saveEditTrigger() {
          if (!this.editTriggerData) return;
          try {
            const body = {
              cronExpression: this.editTriggerData.triggerType === 'cron' ? this.editTriggerData.cronExpression : null,
              intervalSeconds: this.editTriggerData.triggerType === 'simple' ? this.editTriggerData.intervalSeconds : null,
              misfireInstruction: this.editTriggerData.misfireInstruction
            };
            await this.putApi('/triggers/' + encodeURIComponent(this.editTriggerData.group) + '/' + encodeURIComponent(this.editTriggerData.name), body);
            this.showEditTriggerModal = false;
            this.editTriggerData = null;
            await this.loadTriggers();
            this.showToast('Trigger updated', 'success');
          } catch (e) { this.showToast('Failed to update trigger: ' + e.message, 'error'); }
        },

        getMisfireOptions(triggerType) {
          if (triggerType === 'cron') {
            return [
              { value: 'smartPolicy', label: 'SmartPolicy' },
              { value: 'fireOnceNow', label: 'FireOnceNow' },
              { value: 'doNothing', label: 'DoNothing' },
              { value: 'ignoreMisfirePolicy', label: 'IgnoreMisfirePolicy' }
            ];
          }
          return [
            { value: 'smartPolicy', label: 'SmartPolicy' },
            { value: 'fireNow', label: 'FireNow' },
            { value: 'rescheduleNowWithExistingCount', label: 'RescheduleNowWithExistingCount' },
            { value: 'rescheduleNowWithRemainingCount', label: 'RescheduleNowWithRemainingCount' },
            { value: 'rescheduleNextWithRemainingCount', label: 'RescheduleNextWithRemainingCount' },
            { value: 'rescheduleNextWithExistingCount', label: 'RescheduleNextWithExistingCount' },
            { value: 'ignoreMisfirePolicy', label: 'IgnoreMisfirePolicy' }
          ];
        },

        async loadCalendars() {
          this.loading.calendars = true;
          try {
            const resp = await this.fetchApi('/calendars');
            this.calendars = Array.isArray(resp) ? resp : (resp.data || []);
            this.errors.calendars = null;
            this.retryCounts.calendars = 0;
          } catch (e) {
            console.error('loadCalendars:', e);
            this.errors.calendars = e.message;
            this.showToast('Failed to load calendars: ' + e.message, 'error');
          }
          this.loading.calendars = false;
        },

        async createCalendar() {
          try {
            const body = {
              name: this.newCalendar.name,
              type: this.newCalendar.type,
              description: this.newCalendar.description,
              cronExpression: this.newCalendar.type === 'cron' ? this.newCalendar.cronExpression : null
            };
            await this.postApi('/calendars', body);
            this.showCreateCalendarModal = false;
            this.newCalendar = { name: '', type: 'holiday', cronExpression: '', description: '' };
            await this.loadCalendars();
            this.showToast('Calendar created', 'success');
          } catch (e) { this.showToast('Failed to create calendar: ' + e.message, 'error'); }
        },

        async deleteCalendar(name) {
          try {
            const res = await fetch(this._api('/calendars/' + encodeURIComponent(name)), { method: 'DELETE' });
            if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
            await this.loadCalendars();
            this.showToast('Calendar deleted', 'success');
          } catch (e) { this.showToast('Failed to delete calendar: ' + e.message, 'error'); }
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

        // Render entire graph chart via innerHTML (bypasses Alpine SVG namespace issues)
        // Uses incremental update (only replaces data group) to eliminate flicker on refresh.
        updateGraphChart() {
          const el = this.$refs && this.$refs.graphChartWrap;
          if (!el) return;
          const data = this.graphData;
          if (!data || !data.length) { el.innerHTML = ''; return; }

          const w = this.graphWidth;
          const h = this.graphHeight;
          const margin = this.graphMargin;
          const mode = this.graphChartMode;
          const maxVal = Math.max(...data.map(b => b.count || 0), 1);
          const maxValAxis = Math.max(maxVal, 10);
          const yTicks = ChartEngine.yAxisTicks(maxValAxis, h, margin, 5);

          let xLabels = [];
          try { xLabels = ChartEngine.xAxisTimeLabels(data, 'minute', w, margin, 8); } catch(_){}

          const xScale = ChartEngine.scaleLinear(margin.left, w - margin.right, 0, data.length - 1);
          const chartColors = this.getChartColors();

          const gridLines = yTicks.map(t =>
            `<line x1="${margin.left}" y1="${t.y.toFixed(1)}" x2="${w - margin.right}" y2="${t.y.toFixed(1)}" stroke="${chartColors.grid}" stroke-width="0.5" stroke-dasharray="3,3"/>`
          ).join('');

          const yLabels = yTicks.map(t =>
            `<text x="${margin.left - 8}" y="${(t.y + 4).toFixed(1)}" text-anchor="end" fill="${chartColors.muted}" font-size="9" font-family="ui-monospace,monospace">${t.label}</text>`
          ).join('');

          const xLabelsSvg = xLabels.map(l => {
            let label = l.label || '';
            try {
              const dt = new Date(label);
              if (!isNaN(dt.getTime())) label = dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } catch(_) {}
            return `<text x="${l.x.toFixed(1)}" y="${h - margin.bottom + 14}" text-anchor="middle" fill="${chartColors.muted}" font-size="8" font-family="ui-monospace,monospace">${label}</text>`;
          }).join('');

          const dataSeries = this._buildGraphSeries(data, mode, w, h, margin, maxVal, maxValAxis, xScale, chartColors);

          const legendY = h - margin.bottom + 34;
          const legendTextY = h - margin.bottom + 37;

          // Incremental update: only replace data group when SVG structure is compatible
          const existingSvg = el.querySelector('svg.gc-svg');
          if (existingSvg &&
              existingSvg.dataset.dataLen === String(data.length) &&
              existingSvg.dataset.mode === mode) {
            const seriesG = existingSvg.querySelector('.gc-series');
            const yAxisG  = existingSvg.querySelector('.gc-yaxis');
            const xAxisG  = existingSvg.querySelector('.gc-xaxis');
            const gridG   = existingSvg.querySelector('.gc-grid');
            if (seriesG) seriesG.innerHTML = dataSeries;
            if (yAxisG)  yAxisG.innerHTML  = yLabels;
            if (xAxisG)  xAxisG.innerHTML  = xLabelsSvg;
            if (gridG)   gridG.innerHTML   = gridLines;
            return;
          }

          // Full rebuild (first render or structure change)
          el.innerHTML = `<svg class="gc-svg" width="${w}" height="${h + 50}" viewBox="0 0 ${w} ${h + 50}"
            style="width:100%;display:block;overflow:visible"
            data-data-len="${data.length}" data-mode="${mode}">
            <defs>
              <linearGradient id="gcCountGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="${chartColors.primary}" stop-opacity="0.18"/>
                <stop offset="100%" stop-color="${chartColors.primary}" stop-opacity="0"/>
              </linearGradient>
              <filter id="gcGlow">
                <feGaussianBlur stdDeviation="2" result="blur"/>
                <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
              </filter>
            </defs>
            <g class="gc-grid">${gridLines}</g>
            <g class="gc-yaxis">${yLabels}</g>
            <g class="gc-xaxis">${xLabelsSvg}</g>
            <line x1="${margin.left}" y1="${h - margin.bottom}" x2="${w - margin.right}" y2="${h - margin.bottom}" stroke="${chartColors.border}" stroke-width="1"/>
            <g class="gc-series">${dataSeries}</g>
            <g>
              <line x1="16" y1="${legendY}" x2="36" y2="${legendY}" stroke="${chartColors.primary}" stroke-width="2"/>
              <text x="40" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Count</text>
              <line x1="100" y1="${legendY}" x2="120" y2="${legendY}" stroke="#34d399" stroke-width="2" stroke-dasharray="6,3"/>
              <text x="124" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Avg Dur</text>
              <line x1="190" y1="${legendY}" x2="210" y2="${legendY}" stroke="#ef4444" stroke-width="1.5" stroke-dasharray="3,2"/>
              <text x="214" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Errors</text>
            </g>
          </svg>`;
        },

        _buildGraphSeries(data, mode, w, h, margin, maxVal, maxValAxis, xScale, chartColors) {
          if (mode === 'line' || mode === 'area') {
            if (data.length < 2) return '';
            const yScaleCount = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxVal > 0 ? maxVal : 1);
            const countPath = ChartEngine.smoothPath(data, null, 'count', xScale, yScaleCount);
            const countArea = ChartEngine.areaPath(data, null, 'count', xScale, yScaleCount, h - margin.bottom);
            const maxDur = Math.max(...data.map(b => b.avgDurationMs || 0), 1);
            const yScaleDur = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxDur);
            const durPath = ChartEngine.smoothPath(data, null, 'avgDurationMs', xScale, yScaleDur);
            const maxErr = Math.max(...data.map(b => b.errorRate || 0), 0.001);
            const yScaleErr = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxErr);
            const errPath = ChartEngine.smoothPath(data, null, 'errorRate', xScale, yScaleErr);
            return `
              <path d="${countArea}" fill="url(#gcCountGrad)"/>
              <path d="${countPath}" fill="none" stroke="${chartColors.primary}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" filter="url(#gcGlow)"/>
              <path d="${durPath}" fill="none" stroke="#34d399" stroke-width="1.5" stroke-dasharray="6,3" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="${errPath}" fill="none" stroke="#ef4444" stroke-width="1.5" stroke-dasharray="3,2" stroke-linecap="round" stroke-linejoin="round" opacity="0.8"/>`;
          } else if (mode === 'bar') {
            const barRects = ChartEngine.barRects(data, 'count', w, h, margin);
            return barRects.map(r =>
              `<rect x="${r.x}" y="${r.y}" width="${r.width}" height="${r.height}" rx="2" fill="${chartColors.primary}" fill-opacity="0.7"/>`
            ).join('');
          } else if (mode === 'heatmap') {
            const cells = ChartEngine.heatmapCells(data, 'count', w, h, margin, 8, Math.min(data.length, 60));
            return cells.map(c =>
              `<rect x="${c.x}" y="${c.y}" width="${c.width}" height="${c.height}" fill="${c.fill}" rx="1"/>`
            ).join('');
          }
          return '';
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

        onTimelineMouseMove(event) {
          const container = this.$refs && this.$refs.timelineContainer;
          if (!container) return;
          const rect = container.getBoundingClientRect();
          const mouseX = event.clientX - rect.left;
          const mouseY = event.clientY - rect.top;

          const labelW = this.timelineLabelWidth;
          const chartWidth = Math.max(1, this.timelineWidth - labelW);
          const rowH = this.timelineRowHeight;
          const labels = this.timelineVisibleLabels;
          const now = Date.now();
          const rangeMs = this.timelineRangeMs;
          const leftTime = now - rangeMs;
          const chartX = mouseX - labelW;

          if (chartX < 0 || chartX > chartWidth || !labels.length) {
            this.timelineCursor.show = false;
            return;
          }

          const timeMs = leftTime + (chartX / chartWidth) * rangeMs;
          const rowIdx = Math.floor((mouseY - 8) / rowH);

          // Find events whose bars overlap the cursor X (with a small pixel tolerance)
          const pxPerMs = chartWidth / rangeMs;
          const TOL_PX = 6;
          const nearEvents = this.timelineVisibleEvents.filter(e => {
            const t = new Date(e.fireTime).getTime();
            const barStart = (t - leftTime) * pxPerMs;
            const barEnd = barStart + Math.max(TOL_PX, (e.duration || 0) * pxPerMs);
            return chartX >= barStart - TOL_PX && chartX <= barEnd + TOL_PX;
          }).sort((a, b) => new Date(a.fireTime) - new Date(b.fireTime));

          const barEl = event.target && event.target.closest ? event.target.closest('.tl-bar') : null;

          this.timelineCursor = {
            show: true,
            x: mouseX,
            timeMs,
            rowIdx: (rowIdx >= 0 && rowIdx < labels.length) ? rowIdx : -1,
            events: nearEvents,
            bar: barEl ? {
              jobKey: barEl.dataset.key,
              triggerKey: barEl.dataset.trigger || '',
              fireTime: barEl.dataset.time,
              duration: parseFloat(barEl.dataset.dur) || 0,
              success: barEl.dataset.success !== 'false',
              errorMessage: barEl.dataset.error || null,
            } : null,
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
          const pages = ['jobs', 'triggers', 'executing', 'history', 'stats', 'timeline', 'calendars'];
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
            this.loadCalendars(),
          ]);
        },

        // ========================= PERSISTENT SETTINGS =========================
        saveSettings() {
          try {
            localStorage.setItem('qd-settings', JSON.stringify({
              sidebarOpen: this.sidebarOpen,
              graphChartMode: this.graphChartMode,
              refreshInterval: this.settings.refreshInterval,
              historyLimit: this.historyLimit,
              collapsedGroups: this.collapsedGroups,
            }));
          } catch(_) {}
        },

        // ========================= COUNTDOWN & LIVE DURATION =========================
        formatCountdown(isoString) {
          if (!isoString) return '—';
          const diff = new Date(isoString).getTime() - this.nowTick;
          if (diff < 0) return 'past';
          if (diff < 60000) return `in ${Math.floor(diff / 1000)}s`;
          if (diff < 3600000) return `in ${Math.floor(diff / 60000)}m ${Math.floor((diff % 60000) / 1000)}s`;
          if (diff < 86400000) return `in ${Math.floor(diff / 3600000)}h ${Math.floor((diff % 3600000) / 60000)}m`;
          return `in ${Math.floor(diff / 86400000)}d`;
        },

        triggerCountdown(nextFireTime) {
          if (!nextFireTime) return '';
          const diff = new Date(nextFireTime).getTime() - this.nowTick;
          if (diff <= 0) return 'now';
          if (diff < 60000) return Math.ceil(diff / 1000) + 's';
          if (diff < 3600000) return Math.ceil(diff / 60000) + 'm';
          if (diff < 86400000) return Math.floor(diff / 3600000) + 'h ' + Math.floor((diff % 3600000) / 60000) + 'm';
          return Math.floor(diff / 86400000) + 'd';
        },

        formatLiveDuration(startIso) {
          if (!startIso) return '—';
          const elapsed = this.nowTick - new Date(startIso).getTime();
          if (elapsed < 0) return '0s';
          const h = Math.floor(elapsed / 3600000);
          const m = Math.floor((elapsed % 3600000) / 60000);
          const s = Math.floor((elapsed % 60000) / 1000);
          if (h > 0) return `${h}:${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}`;
          return `${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}`;
        },

        // ========================= STATS TREND =========================
        statsTrend(key) {
          if (!this.statsPrev || this.stats[key] === undefined || this.statsPrev[key] === undefined) return null;
          return (this.stats[key] || 0) - (this.statsPrev[key] || 0);
        },

        // ========================= EXPORT HISTORY CSV =========================
        exportHistoryCSV() {
          const rows = this.filteredHistory || this.history;
          const header = 'Job Key,Trigger,Fire Time,Duration (ms),Status\n';
          const lines = rows.map(r =>
            [r.jobKey || '', r.triggerKey || '', r.fireTime || '', r.durationMs || r.duration || '', r.success !== false ? 'Success' : 'Error'].join(',')
          );
          const csv = header + lines.join('\n');
          const blob = new Blob([csv], { type: 'text/csv' });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url; a.download = 'quartz-history.csv'; a.click();
          URL.revokeObjectURL(url);
        },

        exportHistoryJSON() {
          const rows = this.filteredHistory || this.history;
          const json = JSON.stringify(rows, null, 2);
          const blob = new Blob([json], { type: 'application/json' });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url; a.download = 'quartz-history.json'; a.click();
          URL.revokeObjectURL(url);
        },
      }         // closes return object
    }           // closes dashboard()
    window.dashboard = dashboard;
