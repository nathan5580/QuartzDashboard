export function createCalendarsSection() {
  return {
        async loadCalendars() {
          this.loading.calendars = true;
          try {
            const resp = await this.fetchApi('/calendars');
            this.calendars = Array.isArray(resp) ? resp : (resp.data || []);
            this.errors.calendars = null;
            this.retryCounts.calendars = 0;
          } catch (e) {
            console.error('loadCalendars:', e);
            this.errors.calendars = e.message;
            this.showToast('Failed to load calendars: ' + e.message, 'error');
          }
          this.loading.calendars = false;
        },

        async createCalendar() {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            const body = {
              name: this.newCalendar.name,
              type: this.newCalendar.type,
              description: this.newCalendar.description,
              cronExpression: this.newCalendar.type === 'cron' ? this.newCalendar.cronExpression : null
            };
            await this.postApi('/calendars', body);
            this.showCreateCalendarModal = false;
            this.newCalendar = { name: '', type: 'holiday', cronExpression: '', description: '' };
            await this.loadCalendars();
            this.showToast('Calendar created', 'success');
          } catch (e) { this.showToast('Failed to create calendar: ' + e.message, 'error'); }
        },

        async deleteCalendar(name) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            const res = await fetch(this._api('/calendars/' + encodeURIComponent(name)), { method: 'DELETE', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!res.ok) throw new Error(await this.apiErrorMessage(res));
            await this.loadCalendars();
            this.showToast('Calendar deleted', 'success');
          } catch (e) { this.showToast('Failed to delete calendar: ' + e.message, 'error'); }
        },
  };
}
