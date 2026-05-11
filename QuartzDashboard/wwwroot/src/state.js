export function createState() {
  return {
        appReady: false,
        appBootPhase: 'Initializing...',
        currentPage: 'overview',
        sidebarOpen: true,
        lastRefreshed: null,
        lastDataPulse: 0,
        loading: { global: false, jobs: false, triggers: false, executing: false, history: false, stats: false, timeline: false, calendars: false, health: false },
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
        faviconFailureCount: 0,
        acknowledgedFailureCount: 0,
        stats: {},
        executionBuckets: [],
        toast: { show: false, message: '', type: 'info' },
        autoRefreshTimer: null,
        config: { readOnly: false },
        currentTick: Date.now(),

        // Multi-scheduler
        schedulers: [],
        activeSchedulerName: '',
        showSchedulerPicker: false,
        embedMode: false,

        // Jobs page
        jobSearchQuery: '',
        jobSearchOpen: false,
        jobsPage: 1,
        jobsPageSize: 25,
        jobsTotal: 0,
        jobsSortCol: 'name',
        jobsSortDir: 'asc',
        expandedJobs: {},
        pinnedJobs: JSON.parse(localStorage.getItem('quartz-pinned-jobs') || '[]'),

        // Triggers page
        expandedTriggerGroups: {},
        triggersFilter: '',
        triggersPage: 1,
        triggersPageSize: 50,
        triggersTotal: 0,
        triggersSortCol: 'name',
        triggersSortDir: 'asc',
        showTriggerDetailModal: false,
        triggerDetailData: null,
        nextFires: [],
        nextFiresLoading: false,

        // Executing page
        knownExecutingIds: new Set(),

        // History page
        historyFilter: '',
        historyFilterObj: { search: '', status: 'all', dateFrom: '', dateTo: '', dateRange: 'all' },
        maxHistoryDuration: 0,
        historyExpandedRows: {},
        historySortCol: 'fireTime',
        historySortDir: 'desc',
        showHistoryDetail: false,
        historyDetailData: null,
        heatmapData: [],
        heatmapLoading: false,

        // Job run result feedback
        pendingTriggers: {},
        actionPending: {},
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
        isFullscreen: false,
        soundAlerts: JSON.parse(localStorage.getItem('quartz-sound-alerts') || 'false'),
        isMobile: false,
        mobileNavOpen: false,

        // Graph page
        showJobGraph: false,
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

        signalRConnected: false,
        signalRPolling: false,
        connection: null,
        connectionAttempts: 0,
        pollingTimer: null,
        signalRTimeout: null,

        // ========================= TOAST QUEUE =========================
        toastQueue: [],
        toastIdCounter: 0,
        toastTimers: {},


        showCommandPalette: false,
        commandPaletteQuery: '',
        commandPaletteIndex: 0,
        rowActionsOpenFor: null,

        // ========================= GLOBAL SEARCH =========================
        globalSearchQuery: '',
        globalSearchOpen: false,
        globalSearchResults: { jobs: [], triggers: [], history: [] },

        // ========================= SHORTCUTS MODAL =========================
        showShortcutsModal: false,
        showShortcutsHelp: false,
        shortcutsList: [
          { key: '?', label: 'Show shortcuts' },
          { key: 'G J', label: 'Go to Jobs' },
          { key: 'G T', label: 'Go to Triggers' },
          { key: 'G H', label: 'Go to History' },
          { key: 'G E', label: 'Go to Executing' },
          { key: 'G G', label: 'Go to Graph' },
          { key: 'G L', label: 'Go to Timeline' },
          { key: 'G S', label: 'Go to Settings' },
          { key: 'G O', label: 'Go to Overview' },
          { key: 'R', label: 'Refresh current page' },
          { key: '/ or Ctrl+K', label: 'Global search' },
          { key: 'Esc', label: 'Close modals/drawers' },
          { key: 'F', label: 'Toggle fullscreen' },
          { key: '1-9', label: 'Quick page navigation' },
        ],

        // ========================= THEME =========================
        // Dark is the default (app was designed dark-first). Toggle switches to light.
        theme: (() => {
          return localStorage.getItem('qd-theme') || 'dark';
        })(),
        applyTheme(theme = this.theme) {
          this.theme = theme;
          // Only set data-theme="light" to override; dark is the default (no attribute needed)
          if (theme === 'light') {
            document.documentElement.setAttribute('data-theme', 'light');
          } else {
            document.documentElement.removeAttribute('data-theme');
          }
          document.documentElement.classList.remove('dark', 'light');
          document.documentElement.classList.add(theme);
        },
        toggleTheme() {
          const nextTheme = this.theme === 'dark' ? 'light' : 'dark';
          this.applyTheme(nextTheme);
          localStorage.setItem('qd-theme', nextTheme);
          this.$nextTick?.(() => {
            this.updateGraphChart?.();
            this.updateTimelineChart?.();
          });
        },

        // ========================= CREATE JOB MODAL =========================
        showCreateJobModal: false,
        createJobErrors: {},
        createJobSubmitted: false,
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
        showCronBuilder: false,
        cronBuilderExpression: '0 * * * * ?',
        cronBuilderParts: { second: '0', minute: '*', hour: '*', dayOfMonth: '*', month: '*', dayOfWeek: '?' },
        cronBuilderPresets: [
          { label: 'Every second', expr: '* * * * * ?' },
          { label: 'Every minute', expr: '0 * * * * ?' },
          { label: 'Every 5 minutes', expr: '0 0/5 * * * ?' },
          { label: 'Every 15 minutes', expr: '0 0/15 * * * ?' },
          { label: 'Every hour', expr: '0 0 * * * ?' },
          { label: 'Every day at midnight', expr: '0 0 0 * * ?' },
          { label: 'Every Monday at 9am', expr: '0 0 9 ? * MON' },
          { label: 'Weekdays at 8am', expr: '0 0 8 ? * MON-FRI' },
        ],


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
  };
}
