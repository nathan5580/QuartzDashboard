import { mergeSections } from '../utils.js';
import { createSettingsCommandsSection } from './settings.commands.js';
import { createSettingsKeyboardSection } from './settings.keyboard.js';
import { createSettingsRefreshSection } from './settings.refresh.js';

function createSettingsCoreSection() {
  return {
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
        setTimeout(() => ctx.close(), 350);
      } catch (_) {}
    },

    printReport() {
      const w = window.open('', '_blank');
      if (!w) return;
      const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
      const jobs = this.jobs || [];
      const h = this.history || [];
      const successCount = h.filter(x => x.success).length;
      const failCount = h.filter(x => !x.success).length;
      const perc = this.stats?.percentiles || {};

      const jobRows = jobs.map(j => `<tr><td>${esc(j.group)}.${esc(j.name)}</td><td>${esc(j.status)}</td><td>${esc(j.jobType || '—')}</td><td>${esc((j.triggers || []).length)}</td></tr>`).join('');
      const failRows = h.filter(x => !x.success).slice(0, 10).map(f => `<tr><td>${esc(f.jobKey)}</td><td>${esc(new Date(f.fireTime).toLocaleString())}</td><td>${esc(f.exceptionMessage || f.errorMessage || 'Failed')}</td></tr>`).join('');

      w.document.write(`<!DOCTYPE html><html><head><title>Quartz Dashboard Report</title>
        <style>body{font-family:-apple-system,sans-serif;padding:40px;color:#333}h1{color:#4f46e5}table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border:1px solid #e5e7eb;padding:8px 12px;text-align:left;font-size:13px}th{background:#f9fafb;font-weight:600}.stats{display:flex;gap:24px;margin:16px 0}.stat{padding:16px;border:1px solid #e5e7eb;border-radius:8px;text-align:center;flex:1}.stat .val{font-size:24px;font-weight:700;color:#4f46e5}.stat .lbl{font-size:11px;color:#6b7280;text-transform:uppercase;margin-top:4px}.perc{display:flex;gap:16px;margin:12px 0}.perc>div{flex:1;text-align:center;padding:8px;background:#f9fafb;border-radius:6px}.perc .val{font-size:18px;font-weight:700}.ok{color:#059669}.warn{color:#d97706}.bad{color:#dc2626}@page{margin:1.5cm}@media print{body{padding:0}thead{display:table-header-group}tr{page-break-inside:avoid}h2{page-break-before:auto;page-break-after:avoid}}</style></head><body>
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

    // ========================= PERSISTENT SETTINGS =========================
    saveSettings() {
      try {
        localStorage.setItem('quartz-settings', JSON.stringify({
          sidebarOpen: this.sidebarOpen,
          graphChartMode: this.graphChartMode,
          refreshInterval: this.settings.refreshInterval,
          historyPageSize: this.historyPageSize,
          collapsedGroups: this.collapsedGroups,
          rowDensity: this.rowDensity,
          soundAlerts: this.soundAlerts,
          desktopNotificationsEnabled: this.desktopNotificationsEnabled,
          historyFilterObj: JSON.parse(JSON.stringify(this.historyFilterObj)),
        }));
      } catch (_) {}
    },

    // ========================= COUNTDOWN & LIVE DURATION =========================
    formatCountdown(isoString) {
      if (!isoString) return '—';
      const diff = new Date(isoString).getTime() - this.nowTick;
      if (diff < -5000) return 'overdue';
      if (diff <= 0) return 'due now';
      if (diff < 60000) return `in ${Math.floor(diff / 1000)}s`;
      if (diff < 3600000) return `in ${Math.floor(diff / 60000)}m ${Math.floor((diff % 60000) / 1000)}s`;
      if (diff < 86400000) return `in ${Math.floor(diff / 3600000)}h ${Math.floor((diff % 3600000) / 60000)}m`;
      return `in ${Math.floor(diff / 86400000)}d`;
    },

    triggerCountdown(nextFireTime) {
      if (!nextFireTime) return '';
      const diff = new Date(nextFireTime).getTime() - this.nowTick;
      if (diff < -5000) return 'overdue';
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
      if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
      return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    },

    // ========================= STATS TREND =========================
    statsTrend(key) {
      if (!this.statsPrev || this.stats[key] === undefined || this.statsPrev[key] === undefined) return null;
      return (this.stats[key] || 0) - (this.statsPrev[key] || 0);
    },

    // ========================= DESKTOP NOTIFICATIONS =========================
    async requestDesktopNotifications() {
      if (typeof Notification === 'undefined') {
        this.showToast('Notifications not supported in this browser', 'warning');
        return;
      }
      if (Notification.permission === 'granted') {
        this.desktopNotificationsEnabled = true;
        localStorage.setItem('quartz-desktop-notifications', 'true');
        this.showToast('Desktop notifications enabled', 'success');
        return;
      }
      const result = await Notification.requestPermission();
      this.desktopNotificationsPermission = result;
      if (result === 'granted') {
        this.desktopNotificationsEnabled = true;
        localStorage.setItem('quartz-desktop-notifications', 'true');
        this.showToast('Desktop notifications enabled', 'success');
      } else {
        this.desktopNotificationsEnabled = false;
        localStorage.setItem('quartz-desktop-notifications', 'false');
        this.showToast('Notification permission denied', 'warning');
      }
    },

    disableDesktopNotifications() {
      this.desktopNotificationsEnabled = false;
      localStorage.setItem('quartz-desktop-notifications', 'false');
    },

    sendDesktopNotification(title, body, icon) {
      if (!this.desktopNotificationsEnabled || typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
      try {
        const n = new Notification(title, { body, icon: icon || '/favicon.ico', tag: 'quartz-' + Date.now() });
        setTimeout(() => n.close(), 5000);
      } catch (_) {}
    },

    // ========================= ROW DENSITY =========================
    setRowDensity(density) {
      this.rowDensity = density;
      localStorage.setItem('quartz-row-density', density);
      document.body.setAttribute('data-density', density);
    },
  };
}

export function createSettingsSection() {
  return mergeSections(
    createSettingsCommandsSection(),
    createSettingsKeyboardSection(),
    createSettingsRefreshSection(),
    createSettingsCoreSection(),
  );
}
