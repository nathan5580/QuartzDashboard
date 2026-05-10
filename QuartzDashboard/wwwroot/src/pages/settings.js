export function createSettingsSection() {
  return {
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

        getEmptyStateMessage(page) {
          const messages = {
            jobs: { icon: '📋', title: 'No jobs registered', desc: 'Create a job to get started' },
            triggers: { icon: '⏰', title: 'No triggers found', desc: 'Jobs need triggers to execute on a schedule' },
            history: { icon: '📊', title: 'No execution history yet', desc: 'History will appear after jobs start executing' },
            executing: { icon: '⚡', title: 'No jobs currently executing', desc: 'Jobs will appear here while running' },
            calendars: { icon: '📅', title: 'No calendars configured', desc: 'Quartz calendars can exclude dates from trigger schedules' },
          };
          return messages[page] || { icon: '📂', title: 'No data', desc: '' };
        },

        handleKeydown(e) {
          const tagName = e.target?.tagName;
          const isTypingTarget = tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT' || e.target?.isContentEditable;

          // Command palette / global search: Cmd+K or Ctrl+K
          if ((e.metaKey || e.ctrlKey) && e.key === 'k' && !isTypingTarget) {
            e.preventDefault();
            this.openGlobalSearch();
            return;
          }

          // Escape: close modals
          if (e.key === 'Escape') {
            if (this.globalSearchOpen) { this.closeGlobalSearch(); return; }
            if (this.showCommandPalette) { this.showCommandPalette = false; return; }
            if (this.showShortcutsHelp || this.showShortcutsModal) { this.showShortcutsHelp = false; this.showShortcutsModal = false; return; }
            if (this.showCronBuilder) { this.closeCronBuilder(); return; }
            if (this.showCreateJobModal) { this.showCreateJobModal = false; return; }
            if (this.showCreateTriggerModal) { this.showCreateTriggerModal = false; return; }
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

        toggleFullscreen() {
          if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen().then(() => { this.isFullscreen = true; }).catch(() => {});
          } else {
            document.exitFullscreen().then(() => { this.isFullscreen = false; }).catch(() => {});
          }
        },

        playAlertSound() {
          if (!this.soundAlerts) return;
          try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.frequency.value = 440;
            osc.type = 'sine';
            gain.gain.value = 0.15;
            osc.start();
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
            osc.stop(ctx.currentTime + 0.3);
          } catch(_) {}
        },

        printReport() {
          const w = window.open('', '_blank');
          if (!w) return;
          const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
          const jobs = this.allJobs || [];
          const h = this.history || [];
          const successCount = h.filter(x => x.success).length;
          const failCount = h.filter(x => !x.success).length;
          const perc = this.stats?.percentiles || {};

          let jobRows = jobs.map(j => `<tr><td>${esc(j.group)}.${esc(j.name)}</td><td>${esc(j.status)}</td><td>${esc(j.jobType || '—')}</td><td>${esc((j.triggers || []).length)}</td></tr>`).join('');
          let failRows = h.filter(x => !x.success).slice(0, 10).map(f => `<tr><td>${esc(f.jobKey)}</td><td>${esc(new Date(f.fireTime).toLocaleString())}</td><td>${esc(f.exceptionMessage || f.errorMessage || 'Failed')}</td></tr>`).join('');

          w.document.write(`<!DOCTYPE html><html><head><title>Quartz Dashboard Report</title>
            <style>body{font-family:-apple-system,sans-serif;padding:40px;color:#333}h1{color:#4f46e5}table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border:1px solid #e5e7eb;padding:8px 12px;text-align:left;font-size:13px}th{background:#f9fafb;font-weight:600}.stats{display:flex;gap:24px;margin:16px 0}.stat{padding:16px;border:1px solid #e5e7eb;border-radius:8px;text-align:center;flex:1}.stat .val{font-size:24px;font-weight:700;color:#4f46e5}.stat .lbl{font-size:11px;color:#6b7280;text-transform:uppercase;margin-top:4px}.perc{display:flex;gap:16px;margin:12px 0}.perc>div{flex:1;text-align:center;padding:8px;background:#f9fafb;border-radius:6px}.perc .val{font-size:18px;font-weight:700}.ok{color:#059669}.warn{color:#d97706}.bad{color:#dc2626}@media print{body{padding:20px}}</style></head><body>
            <h1>⚡ Quartz Dashboard Report</h1>
            <p style="color:#6b7280">Generated ${esc(new Date().toLocaleString())} · Scheduler: ${esc(this.scheduler?.schedulerName || this.scheduler?.name || '—')}</p>
            <div class="stats">
              <div class="stat"><div class="val">${jobs.length}</div><div class="lbl">Jobs</div></div>
              <div class="stat"><div class="val">${this.stats?.totalExecutions || 0}</div><div class="lbl">Total Executions</div></div>
              <div class="stat"><div class="val">${h.length ? Math.round(successCount / h.length * 100) : 100}%</div><div class="lbl">Success Rate</div></div>
              <div class="stat"><div class="val">${failCount}</div><div class="lbl">Failures</div></div>
            </div>
            <h2>Latency Percentiles</h2>
            <div class="perc">
              <div><div class="val ok">${perc.p50 || 0}ms</div><div class="lbl">P50</div></div>
              <div><div class="val warn">${perc.p95 || 0}ms</div><div class="lbl">P95</div></div>
              <div><div class="val bad">${perc.p99 || 0}ms</div><div class="lbl">P99</div></div>
            </div>
            <h2>Jobs (${jobs.length})</h2>
            <table><thead><tr><th>Job</th><th>Status</th><th>Type</th><th>Triggers</th></tr></thead><tbody>${jobRows}</tbody></table>
            ${failCount > 0 ? `<h2>Recent Failures</h2><table><thead><tr><th>Job</th><th>Time</th><th>Error</th></tr></thead><tbody>${failRows}</tbody></table>` : '<p style="color:#059669">✓ No recent failures</p>'}
            <hr style="margin:24px 0;border-color:#e5e7eb"><p style="font-size:11px;color:#9ca3af">Dot.QuartzDashboard · n8.lu</p>
            </body></html>`);
          w.document.close();
          w.print();
        },

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
  };
}
