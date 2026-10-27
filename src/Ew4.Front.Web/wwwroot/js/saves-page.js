'use strict';
(function () {
  const proto = Ew4Menus.Menus.prototype;
  proto.saveMenu = async function (saving, mode, tutorial) {
    const slots = await this.api.rpc('saves', {mode});
    const layout = this.manifest.layouts.form_save;
    const body = this.api.detail(saving ? 'title_savegame' : 'title_loadgame', 352, 235);
    body.className = 'save-slots';
    const auto = document.createElement('div');
    auto.className = 'autosave-band';
    auto.append(
      this.lettering(this.text('text_autosave'), 'font_text_2_cn_hd', '#646464', 100, 'center'),
      this.api.uiImage('pattern_stage_intro.png', 36, 12),
    );
    body.append(auto);
    for (const slot of slots) {
      const attrs = layout.children.find(e => e.attributes.id === 'group_save_' + slot.slot).attributes;
      const panel = document.createElement('section');
      panel.className = 'save-slot';
      Object.assign(panel.style, Ew4SaveFormat.panelStyle(attrs));
      const title = document.createElement('div');
      title.className = 'save-name';
      title.append(
        this.lettering(
          slot.corrupt ? ew4t('ui.save_corrupt', '存档损坏') : slot.exists ? slot.name : this.text('text_empty'),
          'font_text_2_cn_hd',
          '#ebe6e1',
          110,
          'center',
        ),
      );
      panel.append(title);
      const date = document.createElement('div');
      date.className = 'save-date';
      if (slot.savedAt) {
        date.append(this.lettering(Ew4SaveFormat.formatSavedAt(slot.savedAt), 'font_char_1_hd', '#404040', 110, 'center'));
      }
      panel.append(date);
      if (slot.country) {
        const flag = this.countryFlag(slot.country, 25.5, 15);
        flag.className = 'save-country';
        panel.append(flag);
      }
      const titleText =
        (saving ? ew4t('ui.save_to', '保存到') : ew4t('ui.load_from', '载入')) +
        (slot.slot === 0 ? ew4t('ui.autosave', '自动存档') : ew4t('ui.save_slot', '存档 {0}', [slot.slot])) +
        (slot.exists
          ? ' ' + ew4t('ui.save_row', '{0} 回合 {1}', [slot.name, slot.round])
          : ew4t('ui.slot_empty', ' 空'));
      const command = this.api.button(
        '',
        async () => {
          if (saving) {
            await this.api.rpc('save', {slot: slot.slot, mode, tutorial});
            document.getElementById('detail-modal').hidden = true;
            this.api.notice(ew4t('ui.saved', '已保存'));
          } else await this.api.load(slot.slot, mode);
        },
        panel,
      );
      command.className = 'save-slot-confirm';
      command.setAttribute('aria-label', titleText);
      command.title = titleText;
      const disabled = Ew4SaveFormat.slotConfirmDisabled(saving, slot);
      command.disabled = disabled;
      command.append(this.api.uiImage(disabled ? 'button_ok_gray_noshadow.png' : 'button_ok_noshadow.png', 30));
      body.append(panel);
    }
  };
})();
