export function createSettingsCommandsSection() {
  return {
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

    openCommandPalette() {
      this.closeGlobalSearch();
      this.showCommandPalette = true;
      this.commandPaletteQuery = '';
      this.commandPaletteIndex = 0;
      this.$nextTick(() => this.focusCommandPaletteInput());
    },

    focusCommandPaletteInput(attempt = 0) {
      if (!this.showCommandPalette) return;
      const input = this.$refs && this.$refs.commandPaletteInput;
      if (input && document.activeElement !== input) {
        input.focus({ preventScroll: true });
        input.select?.();
      }
      if (input && document.activeElement !== input && attempt < 6) {
        requestAnimationFrame(() => this.focusCommandPaletteInput(attempt + 1));
      }
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
        this.navigateTo(cmd.page);
      } else if (cmd.action === 'triggerJob') {
        this.openTriggerJobModal(cmd.group, cmd.name);
      }
    },
  };
}
