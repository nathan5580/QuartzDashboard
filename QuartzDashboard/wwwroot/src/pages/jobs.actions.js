export function createJobsActionsSection() {
  return {
    actionKey(type, group, name) {
      return [type || '', group || '', name || ''].join(':');
    },

    isActionPending(type, group, name) {
      return !!this.actionPending[this.actionKey(type, group, name)];
    },

    async withActionPending(type, group, name, callback) {
      const key = this.actionKey(type, group, name);
      if (this.actionPending[key]) return;
      this.actionPending = { ...this.actionPending, [key]: true };
      try {
        return await callback();
      } finally {
        const next = { ...this.actionPending };
        delete next[key];
        this.actionPending = next;
      }
    },

    duplicateJob(job) {
      this.newJob = {
        name: (job?.name || '') + '-copy',
        group: job?.group || 'DEFAULT',
        description: job?.description || '',
        jobType: job?.jobType || '',
        isDurable: job?.isDurable ?? job?.durable ?? false,
        durable: job?.durable || false,
        requestsRecovery: job?.requestsRecovery || false,
        disallowConcurrentExecution: job?.disallowConcurrentExecution || false,
        persistJobDataAfterExecution: job?.persistJobDataAfterExecution || false,
      };
      this.createJobErrors = {};
      this.createJobSubmitted = false;
      this.showCreateJobModal = true;
    },

    deletePendingActionKey() {
      return this.deletePending?.type === 'trigger' ? 'delete-trigger' : 'delete-job';
    },

    isDeletePending() {
      return this.isActionPending(this.deletePendingActionKey(), this.deletePending?.group, this.deletePending?.name);
    },

    deletePendingButtonLabel() {
      return this.isDeletePending() ? 'Deleting...' : 'Delete';
    },

    async submitCreateJob() {
      this.createJobSubmitted = true;
      this.createJobErrors = {};
      if (!String(this.newJob.name || '').trim()) {
        this.createJobErrors = { name: 'Job name is required.' };
        this.showToast('Add a job name before creating the job', 'error');
        this.$nextTick?.(() => this.$refs?.newJobName?.focus?.());
        return;
      }
      this.loading.global = true;
      try {
        const body = {};
        if (this.newJob.name) body.name = String(this.newJob.name).trim();
        if (this.newJob.group && this.newJob.group !== 'DEFAULT') body.group = this.newJob.group;
        if (this.newJob.description) body.description = this.newJob.description;
        if (this.newJob.jobType) body.jobType = this.newJob.jobType;
        if (this.newJob.isDurable) body.isDurable = true;
        if (this.newJob.persistJobDataAfterExecution) body.persistJobDataAfterExecution = true;
        if (this.newJob.disallowConcurrentExecution) body.disallowConcurrentExecution = true;

        const res = await fetch(this._api('/jobs'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        });
        if (!res.ok) throw new Error(await this.apiErrorMessage(res));

        this.showToast('Job ' + this.newJob.name + ' created', 'success');
        this.showCreateJobModal = false;
        this.createJobErrors = {};
        this.createJobSubmitted = false;
        this.newJob = { name: '', group: 'DEFAULT', description: '', jobType: '', isDurable: false, disallowConcurrentExecution: false, persistJobDataAfterExecution: false };
        await this.loadJobs();
      } catch (e) {
        this.showToast('Failed to create job: ' + e.message, 'error');
      }
      this.loading.global = false;
    },

    deleteJob(group, name) {
      this.deleteConfirmMessage = 'Are you sure you want to delete job ' + group + '.' + name + '?';
      this.deletePending = { type: 'job', group: group, name: name };
      this.showDeleteConfirm = true;
    },

    // Delete trigger from job inline details
    deleteJobTrigger(jobGroup, jobName, triggerGroup, triggerName) {
      this.deleteConfirmMessage = 'Are you sure you want to delete trigger ' + triggerGroup + '.' + triggerName + '?';
      this.deletePending = { type: 'trigger', group: triggerGroup, name: triggerName };
      this.showDeleteConfirm = true;
    },

    async executeDelete() {
      if (!this.deletePending) return;
      const { type, group, name } = this.deletePending;
      await this.withActionPending(type === 'job' ? 'delete-job' : 'delete-trigger', group, name, async () => {
        this.loading.global = true;
        try {
          const endpoint = type === 'job'
            ? '/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name)
            : '/triggers/' + encodeURIComponent(group) + '/' + encodeURIComponent(name);

          const res = await fetch(this._api(endpoint), { method: 'DELETE' });
          if (!res.ok) throw new Error(await this.apiErrorMessage(res));

          this.showToast((type === 'job' ? 'Job' : 'Trigger') + ' ' + group + '.' + name + ' deleted', 'success');
          this.showDeleteConfirm = false;
          this.deletePending = null;

          if (type === 'job') await this.loadJobs();
          else await this.loadTriggers();
        } catch (e) {
          this.showToast('Failed to delete: ' + e.message, 'error');
        }
        this.loading.global = false;
      });
    },

    openTriggerJobModal(group, name) {
      this.closeRowActionsMenu();
      this.triggerJobTarget = { group: group, name: name };
      this.triggerJobDataMap = [{ key: '', value: '' }];
      this.showTriggerJobModal = true;
    },

    closeTriggerJobModal() {
      this.showTriggerJobModal = false;
      this.triggerJobTarget = null;
      this.triggerJobDataMap = [];
    },

    addTriggerJobParam() {
      this.triggerJobDataMap.push({ key: '', value: '' });
    },

    removeTriggerJobParam(index) {
      this.triggerJobDataMap.splice(index, 1);
      if (!this.triggerJobDataMap.length) this.triggerJobDataMap = [{ key: '', value: '' }];
    },

    async triggerJob(group, name) {
      await this.withActionPending('trigger', group, name, async () => {
        try {
          const payload = {
            dataMap: Object.fromEntries((this.triggerJobDataMap || []).filter(e => e.key).map(e => [e.key, e.value ?? '']))
          };
          await this.postApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/trigger', payload);
          this.pendingTriggers[group + '.' + name] = Date.now();
          this.showToast('Triggered ' + group + '.' + name, 'success');
          this.closeTriggerJobModal();
        } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
      });
    },

    async pauseJob(group, name) {
      await this.withActionPending('pause', group, name, async () => {
        try {
          await this.postApi('/jobs/' + group + '/' + name + '/pause');
          await this.loadJobs();
          this.showToast('Paused ' + group + '.' + name, 'info');
        } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
      });
    },

    async resumeJob(group, name) {
      await this.withActionPending('resume', group, name, async () => {
        try {
          await this.postApi('/jobs/' + group + '/' + name + '/resume');
          await this.loadJobs();
          this.showToast('Resumed ' + group + '.' + name, 'success');
        } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
      });
    },

    async pauseJobGroup(group) {
      try {
        await this.postApi('/jobs/group/' + encodeURIComponent(group) + '/pause');
        await this.loadJobs();
        this.showToast('Paused all jobs in group "' + group + '"', 'info');
      } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
    },

    async resumeJobGroup(group) {
      try {
        await this.postApi('/jobs/group/' + encodeURIComponent(group) + '/resume');
        await this.loadJobs();
        this.showToast('Resumed all jobs in group "' + group + '"', 'success');
      } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
    },

    async interruptJob(group, name) {
      try {
        const res = await this.postApi('/jobs/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/interrupt');
        if (res?.interrupted) {
          this.showToast(group + '.' + name + ' interrupted', 'success');
        } else {
          this.showToast(group + '.' + name + ' does not implement IInterruptableJob', 'warning');
        }
      } catch (e) { this.showToast('Failed to interrupt: ' + e.message, 'error'); }
    },

    async exportJobs() {
      try {
        const data = await this.fetchApi('/export');
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'quartz-jobs-' + new Date().toISOString().slice(0, 10) + '.json';
        a.click();
        URL.revokeObjectURL(url);
        this.showToast('Exported ' + (data.jobs?.length || 0) + ' jobs', 'success');
      } catch (e) { this.showToast('Export failed: ' + e.message, 'error'); }
    },

    async importJobs(event) {
      const file = event.target.files[0];
      if (!file) return;
      try {
        const text = await file.text();
        const payload = JSON.parse(text);
        const result = await this.postApi('/import', payload);
        this.showToast('Imported ' + result.jobsCreated + ' jobs, ' + result.triggersCreated + ' triggers' + (result.errors > 0 ? ' (' + result.errors + ' errors)' : ''), result.errors > 0 ? 'error' : 'success');
        await this.loadJobs();
        await this.loadTriggers();
        event.target.value = '';
      } catch (e) { this.showToast('Import failed: ' + e.message, 'error'); event.target.value = ''; }
    },
  };
}
