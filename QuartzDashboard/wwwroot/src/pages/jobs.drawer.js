export function createJobsDrawerSection() {
  return {
    openJobDrawer(job) {
      this.closeRowActionsMenu?.();
      // Remember which element triggered the drawer so we can restore focus on close.
      this._drawerReturnFocus = document.activeElement;
      this.jobDrawerData = job;
      this.jobDrawerTab = 'overview';
      this.jobDrawerHistory = [];
      this.jobDrawerLogs = [];
      this.showJobDrawer = true;
      this.loadJobDrawerHistory(job.group, job.name);
      document.body.style.overflow = 'hidden';
      window.location.hash = this.currentPage + '/job/' + encodeURIComponent(job.group + '.' + job.name);
      // Move focus into the dialog once Alpine has rendered the transitioned panel.
      requestAnimationFrame(() => requestAnimationFrame(() => {
        const dialog = document.querySelector('[role="dialog"][aria-labelledby="job-drawer-title"]');
        if (dialog) dialog.focus();
      }));
    },

    closeJobDrawer() {
      this.showJobDrawer = false;
      this.jobDrawerData = null;
      this.jobDrawerHistory = [];
      this.jobDrawerLogs = [];
      document.body.style.overflow = '';
      if (window.location.hash.includes('/job/')) {
        window.location.hash = this.currentPage;
      }
      // Restore focus to the originating element (table row, pinned card, etc.).
      const target = this._drawerReturnFocus;
      this._drawerReturnFocus = null;
      if (target && typeof target.focus === 'function' && document.body.contains(target)) {
        target.focus();
      }
    },

    async loadJobDrawerHistory(group, name) {
      this.jobDrawerHistoryLoading = true;
      try {
        const key = encodeURIComponent(group + '.' + name);
        const resp = await this.fetchApi('/history?job=' + key + '&limit=50');
        this.jobDrawerHistory = Array.isArray(resp?.data) ? resp.data : [];
      } catch (e) { this.jobDrawerHistory = []; }
      this.jobDrawerHistoryLoading = false;
    },

    async loadJobDrawerLogs(group, name) {
      this.jobDrawerLogsLoading = true;
      try {
        const resp = await this.fetchApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/logs');
        this.jobDrawerLogs = Array.isArray(resp?.logs) ? resp.logs : [];
      } catch (e) { this.jobDrawerLogs = []; }
      this.jobDrawerLogsLoading = false;
    },

    copyJobKey(group, name) {
      this.copyToClipboard((group || '') + '.' + (name || ''), 'Job key copied');
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
      if (this.config.readOnly) {
        this.showToast('Dashboard is in read-only mode.', 'warning');
        return;
      }
      if (this.jobDrawerData) this.openTriggerJobModal(this.jobDrawerData.group, this.jobDrawerData.name);
    },

    pauseJobFromDrawer() {
      if (this.config.readOnly) {
        this.showToast('Dashboard is in read-only mode.', 'warning');
        return;
      }
      if (this.jobDrawerData) this.pauseJob(this.jobDrawerData.group, this.jobDrawerData.name);
    },

    resumeJobFromDrawer() {
      if (this.config.readOnly) {
        this.showToast('Dashboard is in read-only mode.', 'warning');
        return;
      }
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
  };
}
