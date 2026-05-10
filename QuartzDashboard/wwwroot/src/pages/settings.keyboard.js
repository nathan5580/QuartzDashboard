export function createSettingsKeyboardSection() {
  return {
    handleKeydown(e) {
      const tagName = e.target?.tagName;
      const isTypingTarget = tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT' || e.target?.isContentEditable;

      // Command palette / global search: Cmd+K or Ctrl+K
      if ((e.metaKey || e.ctrlKey) && e.key === 'k' && !isTypingTarget) {
        e.preventDefault();
        this.openCommandPalette();
        return;
      }

      // Escape: close modals
      if (e.key === 'Escape') {
        if (this.globalSearchOpen) { this.closeGlobalSearch(); return; }
        if (this.showCommandPalette) { this.showCommandPalette = false; return; }
        if (this.showShortcutsHelp || this.showShortcutsModal) { this.showShortcutsHelp = false; this.showShortcutsModal = false; return; }
        if (this.showCronBuilder) { this.closeCronBuilder(); return; }
        if (this.showCreateJobModal) { this.showCreateJobModal = false; this.createJobErrors = {}; this.createJobSubmitted = false; return; }
        if (this.showCreateTriggerModal) { this.showCreateTriggerModal = false; return; }
        if (this.showTriggerJobModal) { this.closeTriggerJobModal(); return; }
        if (this.showTriggerDetailModal) { this.closeTriggerDetail(); return; }
        if (this.showDeleteConfirm) { this.showDeleteConfirm = false; return; }
        if (this.showHistoryDetail) { this.closeHistoryDetail(); return; }
        if (this.showSchedulerPicker) { this.showSchedulerPicker = false; return; }
        if (this.showJobDrawer) { this.closeJobDrawer(); return; }
        return;
      }

      // If command palette or global search is open, handle internally
      if (this.showCommandPalette || this.globalSearchOpen) return;

      // Skip when typing in an input
      if (isTypingTarget) return;

      // Number keys 1-N: switch pages (N = number of nav items)
      const num = parseInt(e.key);
      if (num >= 1 && num <= this.navItems.length && !e.metaKey && !e.ctrlKey && !e.altKey) {
        const idx = num - 1;
        if (idx < this.navItems.length) {
          this.navigateTo(this.navItems[idx].id);
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

      // /: open global search
      if (e.key === '/' && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
        this.openGlobalSearch();
        return;
      }

      if (e.key === 'f' && !e.ctrlKey && !e.metaKey) { this.toggleFullscreen(); e.preventDefault(); return; }

      // g + key: navigate to page
      if (e.key === 'g') {
        this._gPressed = true;
        setTimeout(() => { this._gPressed = false; }, 1000);
        return;
      }
      if (this._gPressed) {
        const map = { o: 'overview', j: 'jobs', t: 'triggers', h: 'history', e: 'executing', g: 'graph', l: 'timeline', s: 'settings', x: 'graph' };
        if (map[e.key]) { this.navigateTo(map[e.key]); this._gPressed = false; e.preventDefault(); return; }
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
        this.showShortcutsHelp = !this.showShortcutsHelp;
        this.showShortcutsModal = this.showShortcutsHelp;
        return;
      }

      // t: toggle theme
      if (e.key === 't' && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
        this.toggleTheme();
        return;
      }

      // [: toggle sidebar
      if (e.key === '[' && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
        this.sidebarOpen = !this.sidebarOpen;
        return;
      }
    },

    handleCommandPaletteTab(e) {
      const root = this.$refs && this.$refs.commandPaletteRoot;
      if (!root) return;
      const focusables = root.querySelectorAll('input, button, [href], [tabindex]:not([tabindex="-1"])');
      if (!focusables.length) return;
      const items = Array.from(focusables).filter(el => !el.hasAttribute('disabled') && el.offsetParent !== null);
      if (!items.length) return;
      const active = document.activeElement;
      const index = items.indexOf(active);
      const movingBack = !!e.shiftKey;
      const nextIndex = movingBack
        ? (index <= 0 ? items.length - 1 : index - 1)
        : (index === -1 || index >= items.length - 1 ? 0 : index + 1);
      const next = items[nextIndex];
      next?.focus?.();
      if (next?.classList?.contains('command-item')) {
        const idx = Number(next.getAttribute('data-cmd-idx'));
        if (Number.isFinite(idx)) this.commandPaletteIndex = idx;
      }
    },

    toggleFullscreen() {
      if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().then(() => { this.isFullscreen = true; }).catch(() => {});
      } else {
        document.exitFullscreen().then(() => { this.isFullscreen = false; }).catch(() => {});
      }
    },

    closeTransientUi() {
      this.globalSearchOpen = false;
      this.globalSearchQuery = '';
      this.globalSearchResults = { jobs: [], triggers: [], history: [] };
      this.showCommandPalette = false;
      this.commandPaletteQuery = '';
      this.showShortcutsHelp = false;
      this.showShortcutsModal = false;
      this.showCronBuilder = false;
      this.showCreateJobModal = false;
      this.createJobErrors = {};
      this.createJobSubmitted = false;
      this.showCreateTriggerModal = false;
      this.showEditTriggerModal = false;
      this.showCreateCalendarModal = false;
      this.showDeleteConfirm = false;
      this.closeTriggerJobModal?.();
      this.showTriggerDetailModal = false;
      this.triggerDetailData = null;
      this.showHistoryDetail = false;
      this.historyDetailData = null;
      this.showSchedulerPicker = false;
      this.rowActionsOpenFor = null;
      if (this.showJobDrawer) this.closeJobDrawer();
      else document.body.style.overflow = '';
    },

    navigateTo(page) {
      if (!page || !this.navItems.some(item => item.id === page)) return;
      this.closeTransientUi();
      this.currentPage = page;
    },
  };
}
