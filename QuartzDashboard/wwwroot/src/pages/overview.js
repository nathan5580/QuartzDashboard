export function createOverviewSection() {
  return {
        // Health computed
        get failedCount() { return this.history ? this.history.filter(h => h.success === false).length : 0; },
        get misfiredCount() { return Array.isArray(this.triggers) ? this.triggers.filter(t => t.state === 'Error').length : 0; },
        get successRate() {
          if (!this.history || !this.history.length) return 100;
          const successes = this.history.filter(h => h.success !== false).length;
          return Math.round(successes / this.history.length * 100);
        },
        get failedHistory() { return this.history ? this.history.filter(h => h.success === false) : []; },
        get lastErrorEntry() {
          const failed = this.failedHistory;
          if (!failed.length) return null;
          return failed.reduce((latest, entry) => {
            if (!latest) return entry;
            return new Date(entry.fireTime).getTime() > new Date(latest.fireTime).getTime() ? entry : latest;
          }, null);
        },

        get failuresByHour() {
          const now = new Date();
          now.setMinutes(0, 0, 0);
          const failed = this.history ? this.history.filter(h => h.success === false && h.fireTime) : [];
          const buckets = [];
          for (let i = 23; i >= 0; i--) {
            const start = new Date(now.getTime() - i * 60 * 60 * 1000);
            const end = new Date(start.getTime() + 60 * 60 * 1000);
            let count = 0;
            for (const item of failed) {
              const time = new Date(item.fireTime).getTime();
              if (time >= start.getTime() && time < end.getTime()) count++;
            }
            buckets.push({ hour: 23 - i, count: count, label: start.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) });
          }
          return buckets;
        },
        get poolUtilization() {
          const poolSize = this.scheduler.threadPoolSize || 10;
          const active = this.executingJobs.length;
          return poolSize > 0 ? (active / poolSize) * 100 : 0;
        },
        get recentFailures() {
          return (this.history || []).filter(h => !h.success).slice(0, 5);
        },
        get uptimePercent() {
          const h = this.history || [];
          if (!h.length) return 100;
          const success = h.filter(x => x.success).length;
          return Math.round((success / h.length) * 1000) / 10;
        },

        // ========================= COMPUTED =========================
        get allJobs() {
          return this.jobs || [];
        },

        get breadcrumb() {
          const names = {
            overview: 'Overview', jobs: 'Jobs', triggers: 'Triggers', executing: 'Executing',
            history: 'History', graph: 'Graph', timeline: 'Timeline', health: 'Health',
            calendars: 'Calendars', settings: 'Settings', heatmap: 'Heatmap'
          };
          return names[this.currentPage] || this.currentPage;
        },

        get healthAlertCount() {
          return this.effectiveHealthStatus && this.effectiveHealthStatus !== 'healthy' ? 1 : 0;
        },

        get effectiveHealthStatus() {
          if (!this.scheduler.isStarted || this.scheduler.isStandbyMode) return 'degraded';
          if (this.successRate < 80) return 'failing';
          if (this.successRate < 95 || this.failedCount > 0) return 'degraded';
          return this.healthData?.status || 'healthy';
        },

        get effectiveHealthLabel() {
          return { healthy: 'Healthy', degraded: 'Degraded', failing: 'Failing' }[this.effectiveHealthStatus] || this.schedulerStatusLabel();
        },

        schedulerStatusLabel() {
          return this.scheduler.isStarted ? (this.scheduler.isStandbyMode ? 'Standby' : 'Running') : 'Stopped';
        },
        schedulerStaleMessage() {
          return !this.scheduler.isStarted
            ? 'Scheduler is stopped — data may be stale'
            : this.scheduler.isStandbyMode
              ? 'Scheduler is in standby mode — jobs are paused'
              : 'Real-time connection lost — data may be stale';
        },
        formatShortDuration(ms, empty = '—') {
          if (!Number.isFinite(ms) || ms < 0) return empty;
          if (ms < 1000) return ms.toFixed(1) + 'ms';
          return (ms / 1000).toFixed(2) + 's';
        },
        renderFailureRateBars() {
          const data = (this.executionBuckets || []).slice(-24);
          if (!data.length) return '';
          const width = 668;
          const count = data.length;
          return data.map((bucket, idx) => {
            if (!bucket) return '';
            const errorRate = bucket.errorRate || 0;
            const barWidth = Math.min(22, Math.max(6, (width / count) * 0.6));
            const centerX = 36 + (idx + 0.5) * (width / count);
            const barHeight = Math.max(4, (errorRate / 100) * 120);
            const color = errorRate > 0 ? '#ef4444' : '#22c55e';
            const opacity = errorRate > 0 ? 0.78 : 0.35;
            return '<rect x="' + (centerX - barWidth / 2) + '" y="' + (148 - barHeight) + '" width="' + barWidth + '" height="' + barHeight + '" rx="2" fill="' + color + '" fill-opacity="' + opacity + '"/>' +
              '<text x="' + centerX + '" y="165" text-anchor="middle" style="font-size:9px;fill:#374151">' + (bucket.label || '') + '</text>';
          }).join('');
        },

        get schedulePreview() {
          const now = Date.now();
          const end = now + 86400000;
          const events = [];
          for (const job of (this.allJobs || [])) {
            for (const trig of (job.triggers || [])) {
              if (trig.nextFireTime) {
                const t = new Date(trig.nextFireTime).getTime();
                if (t >= now && t <= end) {
                  events.push({ time: t, jobKey: job.group + '.' + job.name, triggerName: trig.name });
                }
              }
            }
          }
          return events.sort((a, b) => a.time - b.time).slice(0, 20);
        },

        get statsLoading() {
          return this.loading.stats;
        },

        get connectionStatus() {
          if (this.signalRConnected) return 'Live';
          if (this.signalRPolling) {
            const secs = Math.floor((Date.now() - (this.lastPollingTime || Date.now())) / 1000);
            return 'Polling (' + secs + 's)';
          }
          return 'Connecting...';
        },

        buildJobDependencyData() {
          const executionCounts = (this.history || []).reduce((acc, record) => {
            acc[record.jobKey] = (acc[record.jobKey] || 0) + 1;
            return acc;
          }, {});

          const nodes = [];
          const edges = [];
          for (const job of (this.jobs || [])) {
            const jobKey = job.group + '.' + job.name;
            nodes.push({ id: jobKey, label: job.name, group: job.group, type: 'job', executionCount: executionCounts[jobKey] || 0 });
          }

          for (const trigger of (this.triggers || [])) {
            const jobKey = trigger.jobGroup + '.' + trigger.jobName;
            const triggerKey = trigger.group + '.' + trigger.name;
            nodes.push({ id: triggerKey, label: trigger.name, group: trigger.group, type: 'trigger', state: trigger.state || '', triggerType: trigger.type || '' });
            edges.push({ from: jobKey, to: triggerKey, relationship: 'fires', nextFireTime: trigger.nextFireTime || null, lastFireTime: trigger.lastFireTime || null });
          }

          return { nodes, edges };
        },

        async loadHealth() {
          this.loading.health = true;
          try {
            this.healthData = await this.fetchApi('/health');
          } catch (e) {
            console.error('loadHealth:', e);
          }
          this.loading.health = false;
        },

        getGraphData() {
          const buckets = this.executionBuckets || [];
          let data;
          if (this.graphView === 'live') {
            data = buckets.slice(-Math.max(this.graphTimeRange, 1));
          } else {
            return this.graphHistoryData || [];
          }
          // Pad with zero-buckets so line chart always has enough points to draw
          if (data.length < this.graphTimeRange) {
            const now = Date.now();
            const pad = [];
            for (let i = this.graphTimeRange - data.length; i > 0; i--) {
              const d = new Date(now - i * 60000);
              pad.push({ minute: d.toISOString(), label: d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }), count: 0, avgDurationMs: 0, errorRate: 0 });
            }
            data = [...pad, ...data];
          }
          return data;
        },

        async loadGraphHistoryData() {
          try {
            this.graphHistoryData = await this.fetchApi('/stats/history');
            this.graphData = this.getGraphData();
          } catch (e) { this.showToast('Failed to load history graph: ' + e.message, 'error'); }
        },

        async startScheduler() {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          this.loading.global = true;
          try {
            await this.postApi('/scheduler/start');
            this.showToast('Scheduler started', 'success');
            await this.refreshAll();
          } catch (e) { this.showToast('Failed to start: ' + e.message, 'error'); }
          this.loading.global = false;
        },

        async standbyScheduler() {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          this.loading.global = true;
          try {
            await this.postApi('/scheduler/standby');
            this.showToast('Scheduler on standby', 'info');
            await this.refreshAll();
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
          this.loading.global = false;
        },

        graphX(idx) {
          const margin = this.graphMargin;
          const data = this.graphData;
          const w = this.graphWidth - margin.left - margin.right;
          return margin.left + (idx / Math.max(data.length - 1, 1)) * w;
        },

        graphY(val, maxVal) {
          const margin = this.graphMargin;
          const h = this.graphHeight - margin.top - margin.bottom;
          const max = maxVal || 1;
          return margin.top + h - (val / max) * h;
        },

        // Render entire graph chart via innerHTML (bypasses Alpine SVG namespace issues)
        // Uses incremental update (only replaces data group) to eliminate flicker on refresh.
        updateGraphChart() {
          const el = this.$refs && this.$refs.graphChartWrap;
          if (!el) return;
          const data = this.graphData;
          if (!data || !data.length) { el.innerHTML = ''; return; }

          const w = this.graphWidth;
          const h = this.graphHeight;
          const margin = this.graphMargin;
          const mode = this.graphChartMode;
          const maxVal = Math.max(...data.map(b => b.count || 0), 1);
          const maxValAxis = Math.max(maxVal, 10);
          const yTicks = ChartEngine.yAxisTicks(maxValAxis, h, margin, 5);

          let xLabels = [];
          try { xLabels = ChartEngine.xAxisTimeLabels(data, 'minute', w, margin, 8); } catch(_){}

          const xScale = ChartEngine.scaleLinear(margin.left, w - margin.right, 0, data.length - 1);
          const chartColors = this.getChartColors();

          const gridLines = yTicks.map(t =>
            `<line x1="${margin.left}" y1="${t.y.toFixed(1)}" x2="${w - margin.right}" y2="${t.y.toFixed(1)}" stroke="${chartColors.grid}" stroke-width="0.5" stroke-dasharray="3,3"/>`
          ).join('');

          const yLabels = yTicks.map(t =>
            `<text x="${margin.left - 8}" y="${(t.y + 4).toFixed(1)}" text-anchor="end" fill="${chartColors.muted}" font-size="9" font-family="ui-monospace,monospace">${t.label}</text>`
          ).join('');

          const maxDur = Math.max(...data.map(b => b.avgDurationMs || 0), 1);
          const durTicks = ChartEngine.yAxisTicks(maxDur, h, margin, 5);
          const durLabels = durTicks.map(t =>
            `<text x="${w - margin.right + 8}" y="${(t.y + 4).toFixed(1)}" text-anchor="start" fill="#34d399" font-size="9" font-family="ui-monospace,monospace">${this.formatDurationAxis(t.value)}</text>`
          ).join('');

          const xLabelsSvg = xLabels.map(l => {
            let label = l.label || '';
            try {
              const dt = new Date(label);
              if (!isNaN(dt.getTime())) label = dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } catch(_) {}
            return `<text x="${l.x.toFixed(1)}" y="${h - margin.bottom + 14}" text-anchor="middle" fill="${chartColors.muted}" font-size="8" font-family="ui-monospace,monospace">${label}</text>`;
          }).join('');

          const dataSeries = this._buildGraphSeries(data, mode, w, h, margin, maxVal, maxValAxis, xScale, chartColors);

          const legendY = h - margin.bottom + 34;
          const legendTextY = h - margin.bottom + 37;

          // Incremental update: only replace data group when SVG structure is compatible
          const existingSvg = el.querySelector('svg.gc-svg');
          if (existingSvg &&
              existingSvg.dataset.dataLen === String(data.length) &&
              existingSvg.dataset.mode === mode) {
            const seriesG = existingSvg.querySelector('.gc-series');
            const yAxisG  = existingSvg.querySelector('.gc-yaxis');
            const yAxisRightG = existingSvg.querySelector('.gc-yaxis-right');
            const xAxisG  = existingSvg.querySelector('.gc-xaxis');
            const gridG   = existingSvg.querySelector('.gc-grid');
            if (seriesG) seriesG.innerHTML = dataSeries;
            if (yAxisG)  yAxisG.innerHTML  = yLabels;
            if (yAxisRightG) yAxisRightG.innerHTML = durLabels;
            if (xAxisG)  xAxisG.innerHTML  = xLabelsSvg;
            if (gridG)   gridG.innerHTML   = gridLines;
            return;
          }

          // Full rebuild (first render or structure change)
          el.innerHTML = `<svg class="gc-svg" width="${w}" height="${h + 50}" viewBox="0 0 ${w} ${h + 50}"
            style="width:100%;display:block;overflow:visible"
            data-data-len="${data.length}" data-mode="${mode}">
            <defs>
              <linearGradient id="gcCountGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="${chartColors.primary}" stop-opacity="0.18"/>
                <stop offset="100%" stop-color="${chartColors.primary}" stop-opacity="0"/>
              </linearGradient>
              <filter id="gcGlow">
                <feGaussianBlur stdDeviation="2" result="blur"/>
                <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
              </filter>
            </defs>
            <g class="gc-grid">${gridLines}</g>
            <g class="gc-yaxis">${yLabels}</g>
            <g class="gc-yaxis-right">${durLabels}</g>
            <g class="gc-xaxis">${xLabelsSvg}</g>
            <line x1="${margin.left}" y1="${h - margin.bottom}" x2="${w - margin.right}" y2="${h - margin.bottom}" stroke="${chartColors.border}" stroke-width="1"/>
            <g class="gc-series">${dataSeries}</g>
            <g>
              <line x1="16" y1="${legendY}" x2="36" y2="${legendY}" stroke="${chartColors.primary}" stroke-width="2"/>
              <text x="40" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Count</text>
              <line x1="100" y1="${legendY}" x2="120" y2="${legendY}" stroke="#34d399" stroke-width="2" stroke-dasharray="6,3"/>
              <text x="124" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Avg Dur</text>
              <line x1="190" y1="${legendY}" x2="210" y2="${legendY}" stroke="#ef4444" stroke-width="1.5" stroke-dasharray="3,2"/>
              <text x="214" y="${legendTextY}" fill="${chartColors.text}" font-size="9">Errors</text>
            </g>
          </svg>`;
        },

        _buildGraphSeries(data, mode, w, h, margin, maxVal, maxValAxis, xScale, chartColors) {
          if (mode === 'line' || mode === 'area') {
            if (data.length < 2) return '';
            const yScaleCount = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxVal > 0 ? maxVal : 1);
            const countPath = ChartEngine.smoothPath(data, null, 'count', xScale, yScaleCount);
            const countArea = ChartEngine.areaPath(data, null, 'count', xScale, yScaleCount, h - margin.bottom);
            const maxDur = Math.max(...data.map(b => b.avgDurationMs || 0), 1);
            const yScaleDur = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxDur);
            const durPath = ChartEngine.smoothPath(data, null, 'avgDurationMs', xScale, yScaleDur);
            const maxErr = Math.max(...data.map(b => b.errorRate || 0), 0.001);
            const yScaleErr = ChartEngine.scaleLinear(h - margin.bottom, margin.top, 0, maxErr);
            const errPath = ChartEngine.smoothPath(data, null, 'errorRate', xScale, yScaleErr);
            return `
              <path d="${countArea}" fill="url(#gcCountGrad)"/>
              <path d="${countPath}" fill="none" stroke="${chartColors.primary}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" filter="url(#gcGlow)"/>
              <path d="${durPath}" fill="none" stroke="#34d399" stroke-width="1.5" stroke-dasharray="6,3" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="${errPath}" fill="none" stroke="#ef4444" stroke-width="1.5" stroke-dasharray="3,2" stroke-linecap="round" stroke-linejoin="round" opacity="0.8"/>`;
          } else if (mode === 'bar') {
            const barRects = ChartEngine.barRects(data, 'count', w, h, margin);
            return barRects.map(r =>
              `<rect x="${r.x}" y="${r.y}" width="${r.width}" height="${r.height}" rx="2" fill="${chartColors.primary}" fill-opacity="0.7"/>`
            ).join('');
          } else if (mode === 'heatmap') {
            const cells = ChartEngine.heatmapCells(data, 'count', w, h, margin, 8, Math.min(data.length, 60));
            return cells.map(c =>
              `<rect x="${c.x}" y="${c.y}" width="${c.width}" height="${c.height}" fill="${c.fill}" rx="1"/>`
            ).join('');
          }
          return '';
        },

        get graphTooltipTime() {
          if (!this.graphCrosshair || !this.graphCrosshair.data) return '';
          return this.formatTimeShort(this.graphCrosshair.data.minute);
        },

        // ========================= GRAPH EVENT HANDLERS =========================
        onGraphMouseMove(event) {
          const container = this.$refs && this.$refs.graphContainer;
          if (!container) return;
          const rect = container.getBoundingClientRect();
          const svgX = event.clientX - rect.left;
          const margin = this.graphMargin;
          const data = this.graphData;
          if (!data || data.length < 2) return;
          const chartW = this.graphWidth - margin.left - margin.right;
          const normalizedX = (svgX - margin.left) / chartW;
          const idx = Math.round(normalizedX * (data.length - 1));
          const clampedIdx = Math.max(0, Math.min(data.length - 1, idx));
          this.graphCrosshair = {
            show: true,
            x: margin.left + (clampedIdx / Math.max(data.length - 1, 1)) * chartW,
            data: Object.assign({}, data[clampedIdx])
          };
        },

        onTimelineMouseMove(event) {
          const container = this.$refs && this.$refs.timelineContainer;
          if (!container) return;
          const rect = container.getBoundingClientRect();
          const mouseX = event.clientX - rect.left;
          const mouseY = event.clientY - rect.top;

          const labelW = this.timelineLabelWidth;
          const chartWidth = Math.max(1, this.timelineWidth - labelW);
          const rowH = this.timelineRowHeight;
          const labels = this.timelineVisibleLabels;
          const now = Date.now();
          const rangeMs = this.timelineRangeMs;
          const leftTime = now - rangeMs;
          const chartX = mouseX - labelW;

          if (chartX < 0 || chartX > chartWidth || !labels.length) {
            this.timelineCursor.show = false;
            return;
          }

          const timeMs = leftTime + (chartX / chartWidth) * rangeMs;
          const rowIdx = Math.floor((mouseY - 8) / rowH);

          // Find events whose bars overlap the cursor X (with a small pixel tolerance)
          const pxPerMs = chartWidth / rangeMs;
          const TOL_PX = 6;
          const nearEvents = this.timelineVisibleEvents.filter(e => {
            const t = new Date(e.fireTime).getTime();
            const barStart = (t - leftTime) * pxPerMs;
            const barEnd = barStart + Math.max(TOL_PX, (e.duration || 0) * pxPerMs);
            return chartX >= barStart - TOL_PX && chartX <= barEnd + TOL_PX;
          }).sort((a, b) => new Date(a.fireTime) - new Date(b.fireTime));

          const barEl = event.target && event.target.closest ? event.target.closest('.tl-bar') : null;
          if (!barEl && !nearEvents.length) {
            this.timelineCursor.show = false;
            return;
          }

          this.timelineCursor = {
            show: true,
            x: mouseX,
            timeMs,
            rowIdx: (rowIdx >= 0 && rowIdx < labels.length) ? rowIdx : -1,
            events: nearEvents,
            bar: barEl ? {
              jobKey: barEl.dataset.key,
              triggerKey: barEl.dataset.trigger || '',
              fireTime: barEl.dataset.time,
              duration: parseFloat(barEl.dataset.dur) || 0,
              success: barEl.dataset.success !== 'false',
              errorMessage: barEl.dataset.error || null,
            } : null,
          };
        },

        loadGraphData() {
          this.loadStats();
        },
  };
}
