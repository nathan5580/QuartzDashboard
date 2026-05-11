export function createTriggersSection() {
  return {
        get sortedTriggers() {
          return this.getSortedCollection('triggers', this.triggers || []);
        },

        get groupedTriggers() {
          const groups = {};
          const list = this.sortedTriggers;
          for (const t of list) {
            const key = t.jobGroup + '.' + t.jobName;
            if (!groups[key]) groups[key] = { jobName: key, jobGroup: t.jobGroup, jobNameOnly: t.jobName, triggers: [] };
            groups[key].triggers.push(t);
          }
          return Object.values(groups);
        },

        get filteredGroupedTriggers() {
          const q = (this.triggersFilter || '').toLowerCase().trim();
          if (!q) return this.groupedTriggers;
          return this.groupedTriggers
            .map(g => ({
              ...g,
              triggers: g.triggers.filter(t =>
                t.name.toLowerCase().includes(q) ||
                t.group.toLowerCase().includes(q) ||
                t.jobName.toLowerCase().includes(q) ||
                t.jobGroup.toLowerCase().includes(q) ||
                (t.state || '').toLowerCase().includes(q) ||
                (t.type || '').toLowerCase().includes(q) ||
                (t.scheduleDescription || '').toLowerCase().includes(q)
              )
            }))
            .filter(g => g.triggers.length > 0 || g.jobName.toLowerCase().includes(q));
        },

        get triggersTotalPages() {
          return Math.max(1, Math.ceil((this.triggersTotal || 0) / this.triggersPageSize));
        },

        async validateCron(expr) {
          if (!expr || expr.length < 5) { this.cronNextFires = []; this.cronValid = null; return; }
          try {
            const resp = await this.postApi('/cron/describe', { expression: expr });
            this.cronValid = resp.valid;
            this.cronNextFires = (resp.nextFireTimes || []).map(t => new Date(t).toLocaleString());
          } catch { this.cronValid = false; this.cronNextFires = []; }
        },

        async submitCreateTrigger() {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          if (!this.newTrigger.name || !this.newTrigger.jobName) return;
          if (this.newTrigger.triggerType === 'cron' && !this.newTrigger.cronExpression) return;
          if (this.newTrigger.triggerType === 'simple' && !this.newTrigger.intervalSeconds) return;

          this.loading.global = true;
          try {
            const body = { name: this.newTrigger.name };
            if (this.newTrigger.group && this.newTrigger.group !== 'DEFAULT') body.group = this.newTrigger.group;
            body.jobName = this.newTrigger.jobName;
            if (this.newTrigger.jobGroup && this.newTrigger.jobGroup !== 'DEFAULT') body.jobGroup = this.newTrigger.jobGroup;
            if (this.newTrigger.description) body.description = this.newTrigger.description;

            if (this.newTrigger.triggerType === 'cron') {
              body.cronExpression = this.newTrigger.cronExpression;
            } else {
              body.intervalSeconds = this.newTrigger.intervalSeconds;
              if (this.newTrigger.repeatCount !== null && this.newTrigger.repeatCount !== undefined) {
                body.repeatCount = this.newTrigger.repeatCount;
              }
            }

            if (this.newTrigger.priority !== null && this.newTrigger.priority !== undefined) {
              body.priority = this.newTrigger.priority;
            }
            if (this.newTrigger.startTimeUtc) {
              const d = new Date(this.newTrigger.startTimeUtc);
              body.startTimeUtc = d.toISOString();
            }
            if (this.newTrigger.endTimeUtc) {
              const d = new Date(this.newTrigger.endTimeUtc);
              body.endTimeUtc = d.toISOString();
            }

            const res = await fetch(this._api('/triggers'), {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body)
            });
            if (!res.ok) throw new Error(await this.apiErrorMessage(res));

            this.showToast('Trigger ' + this.newTrigger.name + ' created', 'success');
            this.showCreateTriggerModal = false;
            this.newTrigger = { name: '', group: 'DEFAULT', jobName: '', jobGroup: 'DEFAULT', description: '', triggerType: 'cron', cronExpression: '', intervalSeconds: null, repeatCount: -1, priority: 5, startTimeUtc: '', endTimeUtc: '' };
            this.cronNextFires = [];
            this.cronValid = null;
            await this.loadTriggers();
          } catch (e) {
            this.showToast('Failed to create trigger: ' + e.message, 'error');
          }
          this.loading.global = false;
        },

        // ========================= DELETE TRIGGER =========================
        deleteTrigger(group, name) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          this.deleteConfirmMessage = 'Are you sure you want to delete trigger ' + group + '.' + name + '?';
          this.deletePending = { type: 'trigger', group: group, name: name };
          this.showDeleteConfirm = true;
        },

        async pauseTrigger(group, name) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            await this.postApi('/triggers/' + group + '/' + name + '/pause');
            await this.loadTriggers();
            this.showToast('Paused trigger ' + group + '.' + name, 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeTrigger(group, name) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            await this.postApi('/triggers/' + group + '/' + name + '/resume');
            await this.loadTriggers();
            this.showToast('Resumed trigger ' + group + '.' + name, 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async pauseTriggerGroup(group) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            await this.postApi('/triggers/group/' + encodeURIComponent(group) + '/pause');
            await this.loadTriggers();
            this.showToast('Paused all triggers in group "' + group + '"', 'info');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async resumeTriggerGroup(group) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            await this.postApi('/triggers/group/' + encodeURIComponent(group) + '/resume');
            await this.loadTriggers();
            this.showToast('Resumed all triggers in group "' + group + '"', 'success');
          } catch (e) { this.showToast('Failed: ' + e.message, 'error'); }
        },

        async openTriggerDetail(trigger) {
          if (!trigger) return;
          this.triggerDetailData = trigger;
          this.nextFires = [];
          this.showTriggerDetailModal = true;
          document.body.style.overflow = 'hidden';
          try {
            const group = encodeURIComponent(trigger.group);
            const name = encodeURIComponent(trigger.name);
            const [detail] = await Promise.all([
              this.fetchApi('/triggers/' + group + '/' + name),
              this.loadNextFires(trigger.group, trigger.name)
            ]);
            this.triggerDetailData = detail || trigger;
          } catch (e) {
            this.showToast('Failed to load trigger details: ' + e.message, 'error');
          }
        },

        closeTriggerDetail() {
          this.showTriggerDetailModal = false;
          this.triggerDetailData = null;
          this.nextFires = [];
          this.nextFiresLoading = false;
          document.body.style.overflow = '';
        },

        async loadNextFires(group, name) {
          this.nextFiresLoading = true;
          try {
            this.nextFires = await this.fetchApi('/triggers/' + encodeURIComponent(group) + '/' + encodeURIComponent(name) + '/next-fires?count=10');
            return this.nextFires;
          } catch (e) {
            this.nextFires = [];
            throw e;
          } finally {
            this.nextFiresLoading = false;
          }
        },

        openEditTrigger(trigger) {
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          this.editTriggerData = {
            group: trigger.group,
            name: trigger.name,
            triggerType: (trigger.type || '').toLowerCase().includes('cron') ? 'cron' : 'simple',
            cronExpression: trigger.cronExpression || '',
            intervalSeconds: trigger.intervalSeconds || null,
            misfireInstruction: trigger.misfireInstructionValue || 'smartPolicy'
          };
          this.showEditTriggerModal = true;
        },

        async saveEditTrigger() {
          if (!this.editTriggerData) return;
          if (this.config.readOnly) {
            this.showToast('Dashboard is in read-only mode.', 'warning');
            return;
          }
          try {
            const body = {
              cronExpression: this.editTriggerData.triggerType === 'cron' ? this.editTriggerData.cronExpression : null,
              intervalSeconds: this.editTriggerData.triggerType === 'simple' ? this.editTriggerData.intervalSeconds : null,
              misfireInstruction: this.editTriggerData.misfireInstruction
            };
            await this.putApi('/triggers/' + encodeURIComponent(this.editTriggerData.group) + '/' + encodeURIComponent(this.editTriggerData.name), body);
            this.showEditTriggerModal = false;
            this.editTriggerData = null;
            await this.loadTriggers();
            this.showToast('Trigger updated', 'success');
          } catch (e) { this.showToast('Failed to update trigger: ' + e.message, 'error'); }
        },

        getMisfireOptions(triggerType) {
          if (triggerType === 'cron') {
            return [
              { value: 'smartPolicy', label: 'SmartPolicy' },
              { value: 'fireOnceNow', label: 'FireOnceNow' },
              { value: 'doNothing', label: 'DoNothing' },
              { value: 'ignoreMisfirePolicy', label: 'IgnoreMisfirePolicy' }
            ];
          }
          return [
            { value: 'smartPolicy', label: 'SmartPolicy' },
            { value: 'fireNow', label: 'FireNow' },
            { value: 'rescheduleNowWithExistingCount', label: 'RescheduleNowWithExistingCount' },
            { value: 'rescheduleNowWithRemainingCount', label: 'RescheduleNowWithRemainingCount' },
            { value: 'rescheduleNextWithRemainingCount', label: 'RescheduleNextWithRemainingCount' },
            { value: 'rescheduleNextWithExistingCount', label: 'RescheduleNextWithExistingCount' },
            { value: 'ignoreMisfirePolicy', label: 'IgnoreMisfirePolicy' }
          ];
        },

        toggleTriggerGroup(key) {
          this.expandedTriggerGroups[key] = !this.expandedTriggerGroups[key];
        },
  };
}
