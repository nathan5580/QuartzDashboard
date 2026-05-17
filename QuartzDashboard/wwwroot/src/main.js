import { createApiSection } from './api.js';
import { createModalsSection } from './modals.js';
import { createSignalRSection } from './signalr.js';
import { createState } from './state.js';
import { mergeSections, createUtilsSection } from './utils.js';
import { createCalendarsSection } from './pages/calendars.js';
import { createHistorySection } from './pages/history.js';
import { createJobsSection } from './pages/jobs.js';
import { createOverviewSection } from './pages/overview.js';
import { createSettingsSection } from './pages/settings.js';
import { createTriggersSection } from './pages/triggers.js';

function createMainSection() {
  return {
        async init() {
          this.embedMode = new URLSearchParams(window.location.search).has('embed');
          document.body.classList.toggle('embed-mode', this.embedMode);
          this.isMobile = window.innerWidth < 768;
          window.addEventListener('resize', () => { this.isMobile = window.innerWidth < 768; });
          if (this.embedMode) this.sidebarOpen = false;

          // Sync Alpine theme state with the preloaded document theme.
          this.applyTheme(this.theme);

          // Apply row density
          if (this.rowDensity) document.body.setAttribute('data-density', this.rowDensity);

          // Load persistent settings
          try {
            const saved = JSON.parse(localStorage.getItem('quartz-settings') || localStorage.getItem('qd-settings') || '{}');
            if (saved.sidebarOpen !== undefined) this.sidebarOpen = saved.sidebarOpen;
            if (saved.graphChartMode) this.graphChartMode = saved.graphChartMode;
            if (saved.refreshInterval) this.settings.refreshInterval = saved.refreshInterval;
            if (saved.historyPageSize) this.historyPageSize = saved.historyPageSize;
            else if (saved.historyLimit) this.historyPageSize = saved.historyLimit;
            if (saved.collapsedGroups && Object.values(saved.collapsedGroups).some(v => !v)) {
              // Only restore if at least one group is NOT collapsed (avoid all-collapsed corrupted state)
              this.collapsedGroups = saved.collapsedGroups;
            }
            if (saved.rowDensity) this.rowDensity = saved.rowDensity;
            if (saved.soundAlerts !== undefined) this.soundAlerts = saved.soundAlerts;
            if (saved.desktopNotificationsEnabled !== undefined) this.desktopNotificationsEnabled = saved.desktopNotificationsEnabled;
            if (saved.historyFilterObj) this.historyFilterObj = { ...this.historyFilterObj, ...saved.historyFilterObj };
          } catch(_) {}

          // Setup keyboard shortcuts
          document.addEventListener('keydown', (e) => this.handleKeydown(e));
          document.addEventListener('fullscreenchange', () => { this.isFullscreen = !!document.fullscreenElement; });

          // Live-tick every second for executing-job duration display and countdowns
          setInterval(() => { this.currentTick = this.nowTick = Date.now(); }, 1000);

          this.updateFaviconBadge(this.faviconFailureCount || 0);

          // Start SignalR connection
          this.appBootPhase = 'Connecting to SignalR...';
          this.registerLifecycleCleanup();
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
          this.appBootPhase = 'Loading scheduler data...';
          await this.loadConfig();
          await this.refreshAll();
          this.appBootPhase = 'Loading history...';
          await this.loadHistory();
          await this.loadStats();
          this.loadHeatmap();

          this.appReady = true;

          this.startAutoRefresh();
          this.$watch('currentPage', (val) => { this.onPageChange(val); window.location.hash = val; });
          this.$watch('jobSearchQuery', () => {
            this.selectedJobIndex = -1;
          });
          this.$watch('triggersFilter', () => {
            this.debounce(() => {
              if (this.triggersPage !== 1) {
                this.triggersPage = 1;
                this.loadTriggers();
              }
            }, 'triggers-filter', 200);
          });
          this.$watch('historyFilterObj.search', () => {
            this.debounce(() => {
              this.historyCurrentPage = 1;
              this.loadHistory();
            }, 'history-filter-search', 200);
          });
          this.$watch('historyFilterObj.status', () => {
            this.historyCurrentPage = 1;
            this.loadHistory();
          });
          this.$watch('historyFilterObj.dateFrom', () => {
            this.historyCurrentPage = 1;
            this.loadHistory();
          });
          this.$watch('historyFilterObj.dateTo', () => {
            this.historyCurrentPage = 1;
            this.loadHistory();
          });
          this.$watch('globalSearchQuery', () => {
            if (this.globalSearchOpen) this.performGlobalSearch();
          });
          // Deep linking via URL hash
          const hash = window.location.hash.replace('#', '');
          const jobHashMatch = hash.match(/^jobs\/job\/(.+)$/);
          if (jobHashMatch) {
            this.navigateTo('jobs');
            const jobKey = decodeURIComponent(jobHashMatch[1]);
            this.$nextTick(() => {
              const job = (this.jobs || []).find(j => j.group + '.' + j.name === jobKey);
              if (job) this.openJobDrawer(job);
            });
          } else if (hash && this.navItems.find(n => n.id === hash)) {
            this.navigateTo(hash);
          }
          window.addEventListener('hashchange', () => {
            const h = window.location.hash.replace('#', '');
            const jobMatch = h.match(/^jobs\/job\/(.+)$/);
            if (jobMatch) {
              if (this.currentPage !== 'jobs') this.navigateTo('jobs');
              const jobKey = decodeURIComponent(jobMatch[1]);
              const job = (this.jobs || []).find(j => j.group + '.' + j.name === jobKey);
              if (job && (!this.showJobDrawer || this.jobDrawerData?.group + '.' + this.jobDrawerData?.name !== jobKey)) {
                this.openJobDrawer(job);
              }
            } else if (h && this.navItems.find(n => n.id === h) && this.currentPage !== h) {
              this.navigateTo(h);
            }
          });
          this.$watch('settings.refreshInterval', () => { this.startAutoRefresh(); this.saveSettings(); });
          this.$watch('sidebarOpen', () => this.saveSettings());
          this.$watch('graphChartMode', () => this.saveSettings());
          this.$watch('historyPageSize', () => this.saveSettings());
          this.$watch('collapsedGroups', () => this.saveSettings());
          this.$watch('soundAlerts', () => this.saveSettings());
          this.$watch('desktopNotificationsEnabled', () => this.saveSettings());
          this.$watch('historyFilterObj', () => this.saveSettings(), { deep: true });
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
          window.addEventListener('resize', () => {
            this.updateGraphSize();
            this.updateTimelineChart();
          });
        },
  };
}

export function dashboard() {
  return mergeSections(
    createState(),
    createUtilsSection(),
    createApiSection(),
    createSignalRSection(),
    createModalsSection(),
    createOverviewSection(),
    createJobsSection(),
    createTriggersSection(),
    createHistorySection(),
    createCalendarsSection(),
    createSettingsSection(),
    createMainSection(),
  );
}

window.dashboard = dashboard;

document.addEventListener('alpine:init', () => {
  if (window.Alpine?.data) {
    window.Alpine.data('dashboard', dashboard);
  }
});
