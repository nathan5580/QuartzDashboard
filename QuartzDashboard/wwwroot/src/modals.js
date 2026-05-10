export function createModalsSection() {
  return {
        performGlobalSearch() {
          const query = (this.globalSearchQuery || '').trim().toLowerCase();
          if (!query) {
            this.globalSearchResults = { jobs: [], triggers: [], history: [] };
            return this.globalSearchResults;
          }

          const includesQuery = value => String(value || '').toLowerCase().includes(query);
          const jobs = (this.jobs || [])
            .filter(job => includesQuery(job.name) || includesQuery(job.group) || includesQuery(job.jobType) || includesQuery(job.status))
            .slice(0, 5)
            .map(job => ({ key: job.group + '.' + job.name, title: job.name, subtitle: job.group, page: 'jobs', record: job }));

          const triggers = (this.triggers || [])
            .filter(trigger => includesQuery(trigger.name) || includesQuery(trigger.group) || includesQuery(trigger.jobName) || includesQuery(trigger.jobGroup) || includesQuery(trigger.state) || includesQuery(trigger.type))
            .slice(0, 5)
            .map(trigger => ({ key: trigger.group + '.' + trigger.name, title: trigger.name, subtitle: trigger.jobGroup + '.' + trigger.jobName, page: 'triggers', record: trigger }));

          const history = (this.history || [])
            .filter(record => includesQuery(record.jobKey) || includesQuery(record.triggerKey) || includesQuery(record.exceptionMessage) || includesQuery(record.exceptionType))
            .slice(0, 5)
            .map(record => ({ key: record.fireTime + ':' + record.jobKey, title: record.jobKey || 'Execution', subtitle: record.fireTime || '', page: 'history', record }));

          this.globalSearchResults = { jobs, triggers, history };
          return this.globalSearchResults;
        },

        openGlobalSearch() {
          this.globalSearchOpen = true;
          this.performGlobalSearch();
          queueMicrotask(() => {
            const input = document.querySelector('.global-search-input');
            if (input) input.focus();
          });
        },

        closeGlobalSearch() {
          this.globalSearchOpen = false;
          this.globalSearchQuery = '';
          this.globalSearchResults = { jobs: [], triggers: [], history: [] };
        },

        openCronBuilder(existingExpr) {
          const expr = existingExpr || this.newTrigger.cronExpression || this.cronBuilderExpression;
          this.cronBuilderParts = this.parseCronExpression(expr);
          this.showCronBuilder = true;
          this.updateCronFromParts();
        },

        closeCronBuilder() {
          this.showCronBuilder = false;
        },

        updateCronFromParts() {
          const parts = this.cronBuilderParts || {};
          this.cronBuilderExpression = [
            parts.second || '0',
            parts.minute || '*',
            parts.hour || '*',
            parts.dayOfMonth || '*',
            parts.month || '*',
            parts.dayOfWeek || '?'
          ].join(' ');

          if (this.newTrigger?.triggerType === 'cron') this.newTrigger.cronExpression = this.cronBuilderExpression;
          if (this.editTriggerData?.triggerType === 'cron') this.editTriggerData.cronExpression = this.cronBuilderExpression;
          this.validateCron(this.cronBuilderExpression);
        },

        applyCronPreset(preset) {
          if (!preset?.expr) return;
          this.cronBuilderParts = this.parseCronExpression(preset.expr);
          this.updateCronFromParts();
        },

        parseCronExpression(expr) {
          const parts = String(expr || '0 * * * * ?').trim().split(/\s+/);
          return {
            second: parts[0] || '0',
            minute: parts[1] || '*',
            hour: parts[2] || '*',
            dayOfMonth: parts[3] || '*',
            month: parts[4] || '*',
            dayOfWeek: parts[5] || '?',
          };
        },
  };
}
