export function createSettingsRefreshSection() {
  return {
    startAutoRefresh() {
      if (this.autoRefreshTimer) clearInterval(this.autoRefreshTimer);
      const ms = this.settings.refreshInterval * 1000;
      this.autoRefreshTimer = setInterval(() => {
        if (this.signalRConnected) {
          if (this.currentPage === 'timeline' || this.currentPage === 'executing') return;
        }
        const page = this.currentPage;
        if (this.settings.autoRefreshPages[page]) {
          this.refreshPage(page, true);
        }
      }, ms);
    },

    toggleAutoRefresh(pageId) {
      this.settings.autoRefreshPages[pageId] = !this.settings.autoRefreshPages[pageId];
    },

    async refreshPage(page, silent = false) {
      // Preserve scroll position so background refresh doesn't jump the view
      const mainEl = document.querySelector('main') || document.querySelector('.main-content');
      const scrollTop = mainEl ? mainEl.scrollTop : 0;

      switch (page) {
        case 'overview': await this.refreshAll(silent); break;
        case 'jobs': await this.loadJobs(undefined, silent); break;
        case 'triggers': await this.loadTriggers(undefined, silent); break;
        case 'executing': await this.loadExecutingJobs(silent); break;
        case 'history': await this.loadHistory(undefined, silent); break;
        case 'graph': await this.loadStats(); break;
        case 'timeline': await this.loadTimeline(); break;
        case 'health': await this.loadHealth(); break;
        case 'calendars': await this.loadCalendars(); break;
      }

      if (silent && mainEl && scrollTop > 0) {
        this.$nextTick?.(() => { mainEl.scrollTop = scrollTop; });
      }
    },

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
  };
}
