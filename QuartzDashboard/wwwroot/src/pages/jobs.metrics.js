export function createJobsMetricsSection() {
  return {
    jobStatusBadge(status) {
      switch (status) {
        case 'Executing': return 'badge badge-running';
        case 'Scheduled': return 'badge badge-normal';
        case 'Paused': return 'badge badge-paused';
        case 'Durable': return 'badge badge-idle';
        default: return 'badge badge-idle';
      }
    },

    get maxRenderedJobs() {
      const pageSize = Number(this.jobsPageSize) || 25;
      return Math.max(250, pageSize * 10);
    },

    get renderedJobsCount() {
      return Math.min((this.filteredJobs || []).length, this.maxRenderedJobs);
    },

    get isJobRenderCapped() {
      return (this.filteredJobs || []).length > this.maxRenderedJobs;
    },

    getJobHistoryMetricsMap() {
      const history = this.history || [];
      const first = history[0] || null;
      const last = history[history.length - 1] || null;
      const stamp = [
        history.length,
        first?.jobKey || '',
        first?.fireTime || '',
        last?.jobKey || '',
        last?.fireTime || ''
      ].join('|');

      if (this._jobHistoryMetricsCache?.stamp === stamp) {
        return this._jobHistoryMetricsCache.map;
      }

      const map = {};
      for (const entry of history) {
        const key = entry?.jobKey;
        if (!key) continue;
        if (!map[key]) map[key] = { total: 0, success: 0, lastEntry: null, lastFireTime: null, durations: [] };
        const metric = map[key];
        metric.total += 1;
        if (entry.success) metric.success += 1;
        if (metric.durations.length < 20) metric.durations.push(entry.durationMs ?? entry.duration ?? 0);

        const fireTs = entry.fireTime ? Date.parse(entry.fireTime) : NaN;
        if (!metric.lastEntry || (Number.isFinite(fireTs) && fireTs > (metric.lastFireTime || 0))) {
          metric.lastEntry = entry;
          metric.lastFireTime = Number.isFinite(fireTs) ? fireTs : metric.lastFireTime;
        }
      }

      this._jobHistoryMetricsCache = { stamp, map };
      return map;
    },

    jobSuccessRate(group, name) {
      const key = group + '.' + name;
      const metric = this.getJobHistoryMetricsMap()[key];
      if (!metric || !metric.total) return null;
      return Math.round((metric.success / metric.total) * 100);
    },

    getJobLastExecution(group, name) {
      const key = group + '.' + name;
      const metric = this.getJobHistoryMetricsMap()[key];
      return metric?.lastEntry || null;
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

    rowActionKey(group, name) {
      return (group || '') + '.' + (name || '');
    },

    toggleRowActionsMenu(group, name) {
      const key = this.rowActionKey(group, name);
      this.rowActionsOpenFor = this.rowActionsOpenFor === key ? null : key;
    },

    closeRowActionsMenu() {
      this.rowActionsOpenFor = null;
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
      const jobs = (this.filteredJobs || []).slice(0, this.maxRenderedJobs);
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

    jobInlineSparklinePoints(group, name) {
      const key = group + '.' + name;
      const metric = this.getJobHistoryMetricsMap()[key];
      const durations = metric?.durations;
      if (!durations || durations.length < 2) return '';
      const max = Math.max(...durations, 1);
      const w = 56, h = 18;
      const step = w / (durations.length - 1);
      return durations.map((d, i) => `${(i * step).toFixed(1)},${(h - (d / max) * (h - 2) - 1).toFixed(1)}`).join(' ');
    },

    jobGroupIsPaused(groupName) {
      const groupJobs = (this.jobs || []).filter(j => j.group === groupName);
      return groupJobs.length > 0 && groupJobs.every(j => j.status === 'Paused');
    },
  };
}
