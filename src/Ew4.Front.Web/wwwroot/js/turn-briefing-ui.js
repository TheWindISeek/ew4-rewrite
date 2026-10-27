'use strict';
(function (root) {
  function signed(value) {
    return value > 0 ? '+' + value : String(value);
  }

  function fillGeneralSlots(layout, api, generals, slotCount) {
    const box = layout.get('lbox_general');
    const height = Math.min(98, parseFloat(box.style.height) || 59);
    const width = height * 78 / 98;
    const count = Math.max(slotCount || 6, generals?.length || 0);
    box.classList.add('result-generals');
    Object.assign(box.style, {
      transform: 'none',
      display: 'flex',
      gap: '7px',
      justifyContent: count * width + (count - 1) * 7 <= parseFloat(box.style.width) ? 'space-between' : 'flex-start',
    });
    box.style.setProperty('--result-card-width', width + 'px');
    box.style.setProperty('--result-card-height', height + 'px');
    const slots = Array.from({length: count}, (_, i) => {
      const general = generals[i];
      const slot = document.createElement('div');
      slot.className = 'result-general';
      if (general) {
        slot.setAttribute('aria-label', general.name + (general.withdrawn ? ew4t('ui.withdrawn', ' 已退场') : ''));
        slot.title = general.name;
        slot.append(api.portrait(general.image));
        for (const [value, prefix] of [[general.rank, 'rank_'], [general.title, 'class_']]) {
          if (value > 0) {
            const badge = api.uiImage(prefix + Math.min(value, prefix === 'rank_' ? 14 : 9) + '.png', 21, 24);
            badge.className = prefix === 'rank_' ? 'result-rank' : 'result-title';
            slot.append(badge);
          }
        }
        if (general.withdrawn) slot.classList.add('withdrawn');
      } else {
        slot.append(api.uiImage('button_add_generals.png', 78, 78));
        slot.setAttribute('aria-label', ew4t('ui.empty_slot', '空将领位'));
      }
      return slot;
    });
    layout.fill('lbox_general', ...slots);
  }

  function showLayout(api, layoutId, setup) {
    return new Promise(resolve => {
      const layout = new Ew4NativeUI.Layout(api, layoutId, () => {
        api.closeDetail();
        resolve();
      });
      setup(layout);
      $('detail-confirm').hidden = true;
    });
  }

  function decorateVictoryGroup(layout, api, groupId, valueId, roundValue) {
    const box = layout.get(groupId);
    const titleKey = box.nativeAttributes?.name;
    const strings = api.manifest?.strings || {};
    const width = parseFloat(box.style.width);
    if (titleKey && strings[titleKey]) {
      const title = document.createElement('div');
      title.className = 'stage-intro-cond-title';
      title.append(api.text(strings[titleKey], 'font_text_2_cn_hd', '#646464', width, 'center', {singleLine: true}));
      box.insertBefore(title, box.firstChild);
    }
    layout.text(
      groupId + '/text_round',
      strings.text_round_word || ew4t('ui.round_word', '回合'),
      {font: 'font_text_2_cn_hd', color: '64,64,64,255', align: 'center'},
    );
    const roundEl = layout.get(groupId + '/text_round');
    Object.assign(roundEl.style, {display: 'flex', alignItems: 'center', justifyContent: 'center'});
    layout.text(groupId + '/' + valueId, String(roundValue), {
      font: 'font_char_2_hd',
      color: '64,64,64,255',
      align: 'center',
    });
    const valueEl = layout.get(groupId + '/' + valueId);
    Object.assign(valueEl.style, {display: 'flex', alignItems: 'center', justifyContent: 'center'});
  }

  function showBattleBriefing(api, briefing) {
    return showLayout(api, 'form_stageintro', layout => {
      $('detail-title').replaceChildren(api.titleLabel(briefing.title));
      layout.text('group_desc/text_desc', briefing.description || '', {breakwords: 'true'});
      if (briefing.commanderImage) {
        const card = document.createElement('div');
        card.append(api.portrait(briefing.commanderImage));
        if (briefing.commanderName) {
          const name = document.createElement('span');
          name.append(
            api.text(briefing.commanderName, 'font_text_2_cn_hd', '#ebe6e1', 70, 'center', {singleLine: true}),
          );
          card.append(name);
        }
        layout.fill('tcmder', card);
      }
      decorateVictoryGroup(layout, api, 'group_condwin', 'text_victory', briefing.minimumRound);
      decorateVictoryGroup(layout, api, 'group_condlost', 'text_bestvic', briefing.ratingRound);
    });
  }

  function showRoundTurn(api, summary) {
    return showLayout(api, 'form_roundturn', layout => {
      layout.text('text_roundnum', String(summary.round), {font: 'font_char_2_hd', align: 'center'});
      layout.text('text_money', signed(summary.income.money), {font: 'font_char_2_hd'});
      layout.text('text_indus', signed(summary.income.industry), {font: 'font_char_2_hd'});
      layout.text('text_addfood', signed(summary.income.food), {font: 'font_char_2_hd'});
      layout.text('text_delfood', summary.upkeep.food ? '-' + summary.upkeep.food : '0', {font: 'font_char_2_hd'});
      layout.text('text_roundmin', String(summary.minimumRound), {font: 'font_char_2_hd', color: '255,255,255,255'});
      layout.text('text_roundmax', String(summary.ratingRound), {font: 'font_char_2_hd', color: '255,255,255,255'});
      fillGeneralSlots(layout, api, summary.generals || [], summary.generalSlots || 6);
    });
  }

  root.Ew4TurnBriefing = {showBattleBriefing, showRoundTurn};
})(globalThis);
