export function createHistorySection() {
  return {
        get sortedHistory() {
          return this.getSortedCollection('history', this.history || []);
        },

        get filteredHistory() {
          let arr = this.sortedHistory;
          const f = this.historyFilterObj;
          if (f.search) {
            const q = f.search.toLowerCase();
            arr = arr.filter(h => (h.jobKey || '').toLowerCase().includes(q) || (h.triggerKey || '').toLowerCase().includes(q));
          }
          if (f.status === 'success') arr = arr.filter(h => h.success);
          if (f.status === 'error') arr = arr.filter(h => !h.success);
          return arr;
        },

        get heatmapGrid() {
          const index = new Map((this.heatmapData || []).map(cell => [cell.day + ':' + cell.hour, cell]));
          return Array.from({ length: 7 }, (_, day) =>
            Array.from({ length: 24 }, (_, hour) => {
              const cell = index.get(day + ':' + hour);
              return cell || { day, hour, count: 0, successRate: 0 };
            })
          );
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
          return this.theme === 'light'
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
            <g transform="translate(${nowX}, 12)">
              <circle class="timeline-now-ring" cx="0" cy="0" r="7" fill="${chartColors.primary}" fill-opacity="0.2"></circle>
              <circle class="timeline-now-dot" cx="0" cy="0" r="4.5" fill="${chartColors.primary}"></circle>
            </g>
            <text x="${nowX - 10}" y="15" text-anchor="end" fill="${chartColors.primary}" font-size="10" font-family="ui-monospace,monospace" font-weight="600" opacity="0.95">NOW</text>`;

          const axisLabels = gridLines.map(gl =>
            `<text x="${labelW + gl.x}" y="${chartH - axisH + 18}" text-anchor="middle" fill="${chartColors.muted}" font-size="9" font-family="ui-monospace,monospace">${gl.label}</text>`
          ).join('');

          // Separator line between label panel and chart area
          const separator = `<line x1="${labelW}" y1="0" x2="${labelW}" y2="${chartH - axisH}" stroke="${chartColors.border}" stroke-width="1"/>`;

          el.innerHTML = `<svg width="${w}" height="${chartH}" viewBox="0 0 ${w} ${chartH}" style="width:100%;display:block;overflow:visible">
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


        openHistoryDetail(record) {
          this.historyDetailData = record || null;
          this.showHistoryDetail = !!record;
        },

        closeHistoryDetail() {
          this.showHistoryDetail = false;
          this.historyDetailData = null;
        },

        loadHeatmap() {
          this.heatmapLoading = true;
          try {
            const counts = new Map();
            for (const record of (this.history || [])) {
              if (!record?.fireTime) continue;
              const fireTime = new Date(record.fireTime);
              if (Number.isNaN(fireTime.getTime())) continue;
              const day = fireTime.getDay();
              const hour = fireTime.getHours();
              const key = day + ':' + hour;
              const cell = counts.get(key) || { day, hour, count: 0, successCount: 0 };
              cell.count += 1;
              if (record.success !== false) cell.successCount += 1;
              counts.set(key, cell);
            }

            this.heatmapData = Array.from(counts.values()).map(cell => ({
              day: cell.day,
              hour: cell.hour,
              count: cell.count,
              successRate: cell.count ? Math.round((cell.successCount / cell.count) * 1000) / 10 : 0,
            }));
          } finally {
            this.heatmapLoading = false;
          }
        },

        applyTimelineAutoFit() {
          if (!this.timelineEvents.length) return;
          const timestamps = this.timelineEvents
            .map(e => new Date(e.fireTime).getTime())
            .filter(t => Number.isFinite(t));
          if (!timestamps.length) return;
          const spanMin = (Date.now() - Math.min(...timestamps)) / 60000;
          if (spanMin <= 5) this.timelineRange = 10;
          else if (spanMin <= 20) this.timelineRange = 30;
          else if (spanMin <= 45) this.timelineRange = 60;
          else this.timelineRange = 180;
        },

        async loadTimeline() {
          this.loading.timeline = true;
          try {
            const data = await this.fetchApi('/timeline');
            this.timelineEvents = data;
            this.errors.timeline = null; this.retryCounts.timeline = 0;
            this.applyTimelineAutoFit();
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
          if (this.timelineEvents.length > 500) this.timelineEvents.length = 500;
        },

        updateGraphSize() {
          const container = this.$refs && this.$refs.graphContainer;
          if (container) {
            this.graphWidth = Math.max(400, container.clientWidth || 800);
          }
          const tlContainer = this.$refs && this.$refs.timelineContainer;
          if (tlContainer) {
            this.timelineWidth = Math.max(600, tlContainer.clientWidth || 800);
            this.timelineHeight = Math.max(200, tlContainer.clientHeight || 400);
          }
        },

        onPageChange(page) {
          if (page === 'history') {
            this.clearFaviconBadge(this.getHistoryFailureCount(this.history));
            this.loadHistory();
          }
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
            requestAnimationFrame(() => requestAnimationFrame(() => { this.updateGraphSize(); this.updateTimelineChart(); }));
          }
        },

        exportHistoryCSV() {
          this.exportHistoryToCsv(this.history || []);
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
  };
}
