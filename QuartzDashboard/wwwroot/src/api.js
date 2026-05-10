export function createApiSection() {
  return {
        async loadSchedulers() {
          try {
            const scheds = await this.fetchApi('/schedulers');
            if (!Array.isArray(scheds) || !scheds.length) return;
            this.schedulers = scheds;
            if (!this.activeSchedulerName) {
              const current = scheds.find(scheduler => scheduler.isCurrent);
              this.activeSchedulerName = (current || scheds[0] || {}).name || '';
            }
          } catch (_) {
            this.schedulers = [];
          }
        },

        async switchScheduler(name) {
          if (!name) return;
          this.activeSchedulerName = name;
          this.showSchedulerPicker = false;
          await Promise.all([
            this.refreshAll(),
            this.loadHistory(),
            this.loadStats(),
            this.loadTimeline(),
            this.loadHealth(),
            this.loadCalendars(),
          ]);
        },

        _base() { return window.__QUARTZ_BASE || '/quartz'; },
        _api(path) {
          const base = this._base() + '/api' + path;
          if (this.activeSchedulerName && !/^\/schedulers(?:[/?]|$)/.test(path)) {
            const sep = base.includes('?') ? '&' : '?';
            return base + sep + 'scheduler=' + encodeURIComponent(this.activeSchedulerName);
          }
          return base;
        },

        async apiErrorMessage(res) {
          let detail = '';
          try {
            const text = await res.text();
            if (text) {
              try {
                const json = JSON.parse(text);
                detail = json.error || json.Error || json.message || text;
              } catch {
                detail = text;
              }
            }
          } catch {
            detail = '';
          }
          const status = res.status + ' ' + res.statusText;
          return detail ? status + ': ' + detail : status;
        },

        async fetchApi(path) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url);
          if (!res.ok) throw new Error(await this.apiErrorMessage(res));
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
          if (!res.ok) throw new Error(await this.apiErrorMessage(res));
          return res.json();
        },

        async putApi(path, body) {
          const url = path.startsWith('http') ? path : this._api(path);
          const res = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body || {})
          });
          if (!res.ok) throw new Error(await this.apiErrorMessage(res));
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

          await this.loadSchedulers();
        },

        onSchedulerChange() {
          this.switchScheduler(this.activeSchedulerName);
        },

        async refreshAll() {
          try {
            const jobsOffset = (this.jobsPage - 1) * this.jobsPageSize;
            const triggersOffset = (this.triggersPage - 1) * this.triggersPageSize;
            const [scheduler, jobsResp, triggersResp, executingResp] = await Promise.all([
              this.fetchApi('/scheduler').catch(() => this.scheduler),
              this.fetchApi('/jobs?offset=' + jobsOffset + '&limit=' + this.jobsPageSize).catch(() => ({ data: this.jobs, total: this.jobsTotal })),
              this.fetchApi('/triggers?offset=' + triggersOffset + '&limit=' + this.triggersPageSize).catch(() => ({ data: this.triggers, total: this.triggersTotal })),
              this.fetchApi('/executing').catch(() => ({ data: this.executingJobs })),
            ]);
            this.scheduler = scheduler;
            this.jobs = (Array.isArray(jobsResp) ? jobsResp : (Array.isArray(jobsResp?.data) ? jobsResp.data : []))
              .sort((a, b) => (a.group + '.' + a.name).localeCompare(b.group + '.' + b.name));
            this.jobsTotal = Number.isFinite(jobsResp?.total) ? jobsResp.total : this.jobs.length;
            this.triggers = (Array.isArray(triggersResp) ? triggersResp : (Array.isArray(triggersResp?.data) ? triggersResp.data : []))
              .sort((a, b) => (a.jobGroup + '.' + a.jobName + '/' + a.group + '.' + a.name).localeCompare(b.jobGroup + '.' + b.jobName + '/' + b.group + '.' + b.name));
            this.triggersTotal = Number.isFinite(triggersResp?.total) ? triggersResp.total : this.triggers.length;
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

        async loadJobs(page) {
          this.loading.jobs = true;
          try {
            if (page) this.jobsPage = page;
            if (this.jobsPage < 1) this.jobsPage = 1;
            const offset = (this.jobsPage - 1) * this.jobsPageSize;
            const resp = await this.fetchApi('/jobs?offset=' + offset + '&limit=' + this.jobsPageSize);
            this.jobs = (Array.isArray(resp) ? resp : (Array.isArray(resp?.data) ? resp.data : []))
              .sort((a, b) => (a.group + '.' + a.name).localeCompare(b.group + '.' + b.name));
            this.jobsTotal = Number.isFinite(resp?.total) ? resp.total : this.jobs.length;
            const lastPage = Math.max(1, Math.ceil((this.jobsTotal || 0) / this.jobsPageSize));
            if (this.jobsPage > lastPage) {
              this.jobsPage = lastPage;
              return await this.loadJobs();
            }
            this.errors.jobs = null; this.retryCounts.jobs = 0;
          } catch (e) { console.error('loadJobs:', e); this.errors.jobs = e.message; this.showToast('Failed to load jobs: ' + e.message, 'error'); this._retryLoad('jobs', () => this.loadJobs()); }
          this.loading.jobs = false;
        },

        async jobsGoToPage(page) {
          if (page < 1 || page > this.jobsTotalPages || page === this.jobsPage) return;
          await this.loadJobs(page);
        },

        async jobsPrevPage() {
          await this.jobsGoToPage(this.jobsPage - 1);
        },

        async jobsNextPage() {
          await this.jobsGoToPage(this.jobsPage + 1);
        },

        async loadTriggers(page) {
          this.loading.triggers = true;
          try {
            if (page) this.triggersPage = page;
            if (this.triggersPage < 1) this.triggersPage = 1;
            const offset = (this.triggersPage - 1) * this.triggersPageSize;
            const resp = await this.fetchApi('/triggers?offset=' + offset + '&limit=' + this.triggersPageSize);
            const list = Array.isArray(resp) ? resp : (resp.data ?? resp ?? []);
            this.triggers = (Array.isArray(list) ? list : [])
              .sort((a, b) => (a.jobGroup + '.' + a.jobName + '/' + a.group + '.' + a.name).localeCompare(b.jobGroup + '.' + b.jobName + '/' + b.group + '.' + b.name));
            this.triggersTotal = Number.isFinite(resp?.total) ? resp.total : this.triggers.length;
            const lastPage = Math.max(1, Math.ceil((this.triggersTotal || 0) / this.triggersPageSize));
            if (this.triggersPage > lastPage) {
              this.triggersPage = lastPage;
              return await this.loadTriggers();
            }
            this.errors.triggers = null; this.retryCounts.triggers = 0;
            // Merge new groups into existing expanded state — preserve open/closed
            for (const t of this.triggers) {
              const key = (t.jobGroup || '') + '.' + (t.jobName || '');
              if (!(key in this.expandedTriggerGroups)) {
                this.expandedTriggerGroups[key] = true;
              }
            }
          } catch (e) { console.error('loadTriggers:', e); this.errors.triggers = e.message; this.showToast('Failed to load triggers: ' + e.message, 'error'); this._retryLoad('triggers', () => this.loadTriggers()); }
          this.loading.triggers = false;
        },

        async triggersGoToPage(page) {
          if (page < 1 || page > this.triggersTotalPages || page === this.triggersPage) return;
          await this.loadTriggers(page);
        },

        async triggersPrevPage() {
          await this.triggersGoToPage(this.triggersPage - 1);
        },

        async triggersNextPage() {
          await this.triggersGoToPage(this.triggersPage + 1);
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
            const params = new URLSearchParams({ limit: String(pageSize), offset: String(this.historyOffset) });
            if (this.historyFilterObj.search) params.set('search', this.historyFilterObj.search);
            if (this.historyFilterObj.status && this.historyFilterObj.status !== 'all') params.set('status', this.historyFilterObj.status);
            if (this.historyFilterObj.dateFrom) params.set('dateFrom', this.historyFilterObj.dateFrom);
            if (this.historyFilterObj.dateTo) params.set('dateTo', this.historyFilterObj.dateTo);
            const resp = await this.fetchApi('/history?' + params.toString());
            this.history = resp.data || [];
            this.historyTotal = resp.total || 0;
            const lastPage = Math.max(1, Math.ceil((this.historyTotal || 0) / pageSize));
            if (this.historyCurrentPage > lastPage) {
              this.historyCurrentPage = lastPage;
              return await this.loadHistory();
            }
            this.historyOffset = Number.isFinite(resp?.offset) ? resp.offset : this.historyOffset;
            this.maxHistoryDuration = 0;
            for (const h of this.history) {
              const d = h.duration || h.durationMs || 0;
              if (d > this.maxHistoryDuration) this.maxHistoryDuration = d;
            }
            if (this.maxHistoryDuration === 0) this.maxHistoryDuration = 5000;
            this.syncFaviconBadgeFromHistory(this.history);
            this.loadHeatmap();
            this.errors.history = null; this.retryCounts.history = 0;
          } catch (e) { console.error('loadHistory:', e); this.errors.history = e.message; this.showToast('Failed to load history: ' + e.message, 'error'); this._retryLoad('history', () => this.loadHistory()); }
          this.loading.history = false;
        },

        async historyGoToPage(page) {
          if (page < 1 || page > this.historyPageCount || page === this.historyCurrentPage) return;
          await this.loadHistory(page);
        },

        async historyPrevPage() {
          await this.historyGoToPage(this.historyCurrentPage - 1);
        },

        async historyNextPage() {
          await this.historyGoToPage(this.historyCurrentPage + 1);
        },

        async loadStats() {
          this.loading.stats = true;
          try {
            const newStats = await this.fetchApi('/stats');
            newStats.totalJobs = this.jobsTotal || this.jobs.length;
            newStats.totalTriggers = this.triggersTotal || this.triggers.length;
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
            this.syncFaviconBadgeFromHistory();
            this.errors.stats = null; this.retryCounts.stats = 0;
          } catch (e) { console.error('loadStats:', e); this.errors.stats = e.message; this.showToast('Failed to load stats: ' + e.message, 'error'); this._retryLoad('stats', () => this.loadStats()); }
          this.loading.stats = false;
        },

  };
}
