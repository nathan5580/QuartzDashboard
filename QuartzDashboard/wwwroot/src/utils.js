export function mergeSections(...sources) {
  const target = {};
  for (const source of sources) {
    if (!source) continue;
    Object.defineProperties(target, Object.getOwnPropertyDescriptors(source));
  }
  return target;
}

export function createUtilsSection() {
  return {
        debounce(fn, key, ms) {
          if (this._debounceTimers[key]) clearTimeout(this._debounceTimers[key]);
          this._debounceTimers[key] = setTimeout(() => { delete this._debounceTimers[key]; fn(); }, ms);
        },

        truncateText(value, max = 120) {
          if (!value) return '';
          const text = String(value);
          return text.length > max ? text.slice(0, Math.max(0, max - 1)) + '…' : text;
        },
        formatDurationAxis(ms) {
          if (!Number.isFinite(ms) || ms <= 0) return '0ms';
          if (ms >= 10000) return Math.round(ms / 1000) + 's';
          if (ms >= 1000) return (ms / 1000).toFixed(1) + 's';
          return Math.round(ms) + 'ms';
        },

        sortTable(table, col) {
          const colKey = table + 'SortCol';
          const dirKey = table + 'SortDir';
          if (!(colKey in this) || !(dirKey in this)) return;
          if (this[colKey] === col) {
            this[dirKey] = this[dirKey] === 'asc' ? 'desc' : 'asc';
          } else {
            this[colKey] = col;
            this[dirKey] = 'asc';
          }
        },

        getSortedCollection(table, items) {
          const sortCol = this[table + 'SortCol'];
          const sortDir = this[table + 'SortDir'];
          return [...(items || [])].sort((a, b) => this.compareSortValues(this.getSortValue(table, a, sortCol), this.getSortValue(table, b, sortCol), sortDir));
        },

        getSortValue(table, item, col) {
          if (table === 'jobs') {
            switch (col) {
              case 'group': return item.group || '';
              case 'status': return item.status || item.state || '';
              case 'jobType': return item.jobType || '';
              case 'nextFireTime': return this.getJobNextFireTime(item);
              case 'name':
              default: return item.name || '';
            }
          }

          if (table === 'triggers') {
            switch (col) {
              case 'group': return item.group || '';
              case 'state': return item.state || '';
              case 'type': return item.type || '';
              case 'nextFireTime': return item.nextFireTime || '';
              case 'lastFireTime': return item.lastFireTime || '';
              case 'name':
              default: return item.name || '';
            }
          }

          switch (col) {
            case 'jobKey': return item.jobKey || '';
            case 'duration': return item.durationMs ?? item.duration ?? 0;
            case 'success': return item.success ? 1 : 0;
            case 'fireTime':
            default: return item.fireTime || '';
          }
        },

        compareSortValues(left, right, dir) {
          const leftDate = this.toSortTimestamp(left);
          const rightDate = this.toSortTimestamp(right);
          let result = 0;

          if (leftDate !== null && rightDate !== null) {
            result = leftDate - rightDate;
          } else if (typeof left === 'number' || typeof right === 'number') {
            result = (Number(left) || 0) - (Number(right) || 0);
          } else {
            result = String(left ?? '').localeCompare(String(right ?? ''), undefined, { numeric: true, sensitivity: 'base' });
          }

          return dir === 'desc' ? -result : result;
        },

        toSortTimestamp(value) {
          if (value === null || value === undefined || value === '') return null;
          if (value instanceof Date) return value.getTime();
          if (typeof value === 'string') {
            const parsed = Date.parse(value);
            return Number.isNaN(parsed) ? null : parsed;
          }
          return null;
        },

        getJobNextFireTime(job) {
          const times = (job?.triggers || []).map(trigger => trigger.nextFireTime).filter(Boolean);
          if (!times.length) return job?.nextFireTime || '';
          return times.sort((a, b) => new Date(a).getTime() - new Date(b).getTime())[0];
        },


        isFailureHistoryEntry(entry) {
          if (!entry) return false;
          if (entry.success === false) return true;
          const status = String(entry.status || entry.outcome || '').toLowerCase();
          return status === 'error' || status === 'failed';
        },

        getHistoryFailureCount(items) {
          return (items || []).filter(item => this.isFailureHistoryEntry(item)).length;
        },

        updateFaviconBadge(count) {
          const safeCount = Math.max(0, Number(count) || 0);
          this.faviconFailureCount = safeCount;
          if (typeof document === 'undefined') return;

          const canvas = document.createElement('canvas');
          canvas.width = 32;
          canvas.height = 32;
          const ctx = canvas.getContext('2d');
          if (!ctx) return;

          ctx.clearRect(0, 0, 32, 32);
          ctx.fillStyle = '#6366f1';
          if (typeof ctx.roundRect === 'function') {
            ctx.beginPath();
            ctx.roundRect(0, 0, 32, 32, 6);
            ctx.fill();
          } else {
            ctx.fillRect(0, 0, 32, 32);
          }

          ctx.fillStyle = '#ffffff';
          ctx.font = 'bold 20px system-ui';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText('Q', 16, 17);

          if (safeCount > 0) {
            const badgeText = safeCount > 99 ? '99+' : String(safeCount);
            ctx.fillStyle = '#ef4444';
            ctx.beginPath();
            ctx.arc(25, 8, 8, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#ffffff';
            ctx.font = badgeText.length > 2 ? 'bold 7px system-ui' : 'bold 10px system-ui';
            ctx.fillText(badgeText, 25, 9);
          }

          let link = document.querySelector("link[rel='icon'], link[rel='shortcut icon'], link[rel*='icon']");
          if (!link) {
            link = document.createElement('link');
            link.rel = 'icon';
            document.head.appendChild(link);
          }
          link.type = 'image/png';
          link.href = canvas.toDataURL('image/png');
        },

        syncFaviconBadgeFromHistory(items) {
          const failureCount = this.getHistoryFailureCount(items ?? this.history);
          if (this.currentPage === 'history') {
            this.acknowledgedFailureCount = failureCount;
            this.updateFaviconBadge(0);
            return 0;
          }
          const unseenCount = Math.max(0, failureCount - (this.acknowledgedFailureCount || 0));
          this.updateFaviconBadge(unseenCount);
          return unseenCount;
        },

        clearFaviconBadge(acknowledgedCount) {
          const resolvedCount = Number.isFinite(acknowledgedCount)
            ? acknowledgedCount
            : this.getHistoryFailureCount(this.history);
          this.acknowledgedFailureCount = Math.max(this.acknowledgedFailureCount || 0, resolvedCount);
          this.updateFaviconBadge(0);
        },

        showToast(msg, type = 'info') {
          const id = ++this.toastIdCounter;
          this.toastQueue.push({ id: id, message: msg, type: type });
          if (this.toastQueue.length > 10) this.toastQueue.shift();
          // Also keep the legacy toast for backward compatibility
          this.toast = { show: true, message: msg, type };
          setTimeout(() => {
            this.toastQueue = this.toastQueue.filter(t => t.id !== id);
            this.toast.show = false;
          }, 3000);
        },


        relativeTime(dateStr) {
          if (!dateStr) return '\u2014';
          const now = new Date();
          const target = new Date(dateStr);
          const diffMs = Math.abs(target - now);
          if (diffMs < 1000) return 'now';
          const secs = Math.floor(diffMs / 1000);
          if (secs < 60) return secs + 's';
          const mins = Math.floor(secs / 60);
          const remSecs = secs % 60;
          if (mins < 60) return mins + 'm ' + remSecs + 's';
          const hours = Math.floor(mins / 60);
          const remMins = mins % 60;
          return hours + 'h ' + remMins + 'm';
        },

        relativeTimePhrase(dateStr, futurePrefix = 'in ', pastSuffix = ' ago') {
          if (!dateStr) return '\u2014';
          const target = new Date(dateStr);
          if (isNaN(target.getTime())) return '\u2014';
          const diffMs = target.getTime() - Date.now();
          const value = this.relativeTime(dateStr);
          if (value === 'now') return 'now';
          return diffMs >= 0 ? futurePrefix + value : value + pastSuffix;
        },

        formatDuration(d) {
          if (!d) return '';
          if (typeof d === 'string') {
            return d.replace('PT', '').replace('H', 'h ').replace('M', 'm ').replace('S', 's');
          }
          if (typeof d === 'object') {
            const secs = (d.hours || 0) * 3600 + (d.minutes || 0) * 60 + (d.seconds || 0) + (d.milliseconds || 0) / 1000;
            if (secs < 1) return Math.round(secs * 1000) + 'ms';
            if (secs < 60) return secs.toFixed(1) + 's';
            return Math.floor(secs / 60) + 'm ' + Math.round(secs % 60) + 's';
          }
          return d;
        },

        formatDate(d) {
          if (!d) return '';
          const dt = new Date(d);
          if (isNaN(dt.getTime())) return '';
          return dt.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
        },

        formatTimeShort(d) {
          if (!d) return '';
          const dt = new Date(d);
          if (isNaN(dt.getTime())) return '';
          return dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        },

        exportHistoryToCsv(history) {
          const headers = ['Fire Time', 'Job Name', 'Job Group', 'Trigger Name', 'Trigger Group', 'Status', 'Duration (ms)', 'Error'];
          const splitKey = (key) => {
            const value = String(key || '');
            const idx = value.indexOf('.');
            return idx === -1 ? ['', value] : [value.slice(0, idx), value.slice(idx + 1)];
          };
          const toStatus = (record) => record.status || (record.success === false ? 'Error' : 'Success');
          const escapeCell = (value) => String(value ?? '').replace(/"/g, '""');
          const rows = (history || []).map(h => {
            const [jobGroup, jobName] = h.jobName || h.jobGroup
              ? [h.jobGroup || '', h.jobName || '']
              : splitKey(h.jobKey);
            const [triggerGroup, triggerName] = h.triggerName || h.triggerGroup
              ? [h.triggerGroup || '', h.triggerName || '']
              : splitKey(h.triggerKey);

            return [
              h.fireTime || '',
              jobName || '',
              jobGroup || '',
              triggerName || '',
              triggerGroup || '',
              toStatus(h),
              h.durationMs ?? h.duration ?? '',
              h.errorMessage || h.exceptionMessage || ''
            ];
          });

          let csv = headers.join(',') + '\n';
          rows.forEach(row => {
            csv += row.map(cell => `"${escapeCell(cell)}"`).join(',') + '\n';
          });

          const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `quartz-history-${new Date().toISOString().slice(0, 10)}.csv`;
          a.click();
          URL.revokeObjectURL(url);
        },
  };
}
