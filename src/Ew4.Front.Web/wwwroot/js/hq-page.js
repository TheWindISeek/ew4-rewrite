'use strict';
(function () {
  const proto = Ew4Menus.Menus.prototype;
  proto.headquarters = async function (deployment = null) {
    this.data = await this.api.rpc('headquarters');
    this.deployment = deployment;
    this.recruiting = false;
    this.tagDraft = null;
    if (deployment) document.getElementById('play').hidden = true;
    for (const id of ['main-menu', 'scenario-menu', 'countries', 'campaign-screen', 'mod-screen', 'mod-packages-screen', 'editor-screen']) document.getElementById(id).hidden = true;
    document.getElementById('hq-screen').hidden = false;
    document.getElementById('menu').hidden = false;
    if (deployment) document.getElementById('hq-roster').scrollLeft = 0;
    this.drawRoster();
  };
  proto.deployGeneral = async function (unit, revision) {
    const available = await this.api.rpc('deployment', {unit, revision});
    this.deploymentQuota = available;
    await this.headquarters(unit);
    this.data.roster = this.data.roster.map(c => ({...c, locked: !available.commanders.includes(c.id)}));
    this.drawRoster();
  };
  proto.closeRoster = function () {
    document.getElementById('hq-screen').hidden = true;
    if (this.deployment) {
      document.getElementById('menu').hidden = true;
      document.getElementById('play').hidden = false;
    }
    this.deployment = null;
  };
  proto.card = function (definition, held, action) {
    const card = document.createElement('div');
    card.className = 'commander-card';
    card.setAttribute('role', 'group');
    card.setAttribute('aria-label', definition.name);
    card.append(this.portrait(definition.image));
    const name = document.createElement('span');
    name.className = 'commander-name';
    name.append(this.lettering(definition.name, 'font_text_2_cn_hd', '#ebe6e1', 74, 'center', {singleLine: true}));
    card.append(name);
    const rank = held?.learning.rank.level ?? definition.rank;
    const title = held?.learning.title.level ?? definition.title;
    if (rank > 0) {
      const img = this.api.uiImage('rank_' + Math.min(rank, 14) + '.png', 21, 24);
      img.className = 'commander-rank';
      card.append(img);
    }
    if (title > 0) {
      const img = this.api.uiImage('class_' + Math.min(title, 9) + '.png', 21, 24);
      img.className = 'commander-title';
      card.append(img);
    }
    const open = document.createElement('button');
    open.className = 'commander-open';
    open.setAttribute('aria-label', definition.name);
    open.onclick = action;
    card.append(open);
    const info = document.createElement('button');
    info.className = 'commander-info';
    info.setAttribute('aria-label', definition.name + '信息');
    info.title = ew4t('ui.commander_info', '将领信息');
    info.onclick = e => {
      e.stopPropagation();
      this.commanderInfo(definition, held);
    };
    info.append(this.api.uiImage('button_generalinfo_blue.png', 20, 20));
    card.append(info);
    if (held?.tag) {
      const tag = document.createElement('span');
      tag.className = 'commander-tag';
      card.title = ew4t('ui.tagged', '已标记');
      tag.setAttribute('aria-label', ew4t('ui.tagged', '已标记'));
      card.append(tag);
    }
    if (held?.locked) {
      card.dataset.locked = 'true';
      card.title = (card.title ? card.title + ' · ' : '') + ew4t('ui.dispatched', '已派遣');
    }
    return card;
  };
  proto.drawRoster = function () {
    const data = this.data;
    const screen = document.getElementById('hq-screen');
    screen.classList.toggle('recruiting', this.recruiting);
    screen.classList.toggle('tag-editing', !!this.tagDraft);
    screen.classList.toggle('deploying', !!this.deployment);
    const heading = screen.querySelector('h1');
    const headingKey = this.deployment ? 'title_deploygeneral' : 'title_headquarters';
    heading.dataset.label = headingKey;
    heading.setAttribute('aria-label', this.text(headingKey));
    heading.replaceChildren(this.api.labels(headingKey, this.text(headingKey), heading));
    document.getElementById('hq-medals').replaceChildren(
      this.api.uiImage('medals.png', 13, 17),
      this.lettering(data.medals, 'font_char_2_hd', '#ebe6e1'),
      this.api.uiImage('badges.png', 13, 17),
      this.lettering(data.badges, 'font_char_2_hd', '#ebe6e1'),
    );
    const recruit = document.getElementById('hq-recruit');
    recruit.replaceChildren(this.lettering(this.text('btn_college'), 'font_text_3_cn_hd', '#ebe6e1'));
    recruit.setAttribute('aria-label', this.text('btn_college'));
    recruit.hidden = !!this.deployment;
    recruit.disabled = !!this.tagDraft;
    document.getElementById('hq-back').disabled = this.tagSaving;
    const definitions = data.definitions;
    const byId = new Map(definitions.map(d => [d.id, d]));
    const owned = new Map(data.roster.map(c => [c.definitionId, c]));
    const section = d => Math.max(0, Math.min(2, Math.floor((d.rating - 1) / 3)));
    const entries = this.recruiting
      ? (data.offers[this.rating] || []).map(id => byId.get(id)).filter(Boolean)
      : data.roster
          .map(c => byId.get(c.definitionId))
          .filter(
            d =>
              d &&
              (this.deployment || this.rosterRating === null || section(d) === this.rosterRating),
          )
          .sort(
            (a, b) =>
              Number(!!owned.get(b.id)?.tag) - Number(!!owned.get(a.id)?.tag) || section(b) - section(a),
          );
    const filters = document.getElementById('hq-filters');
    filters.replaceChildren();
    if (this.recruiting)
      for (const [rating, count] of [6, 4, 2].entries()) {
        const b = this.api.button(
          '',
          () => {
            this.rating = rating;
            this.offer = null;
            this.drawRoster();
          },
          filters,
        );
        b.setAttribute('role', 'tab');
        b.setAttribute(
          'aria-label',
          [ew4t('ui.star1_zone', '一星招募区'), ew4t('ui.star2_zone', '二星招募区'), ew4t('ui.star3_zone', '三星招募区')][rating],
        );
        b.setAttribute('aria-selected', String(rating === this.rating));
        b.append(this.api.uiImage('button_general_' + count + '.png', 152, 55));
      }
    else if (!this.deployment)
      for (const rating of [null, 0, 1, 2]) {
        const label =
          rating === null
            ? ew4t('ui.all', '全选')
            : [ew4t('ui.star1', '一星区'), ew4t('ui.star2', '二星区'), ew4t('ui.star3', '三星区')][rating];
        const b = this.api.button(
          '',
          () => {
            this.rosterRating = rating;
            document.getElementById('hq-roster').scrollLeft = 0;
            this.drawRoster();
          },
          filters,
        );
        b.setAttribute('role', 'tab');
        b.setAttribute('aria-label', label);
        b.title = label;
        b.setAttribute('aria-selected', String(rating === this.rosterRating));
        if (rating === null) {
          b.className = 'native-command blue';
          b.append(this.lettering(label, 'font_text_3_cn_hd', '#ebe6e1'));
        } else b.append(this.api.uiImage('button_general_' + [6, 4, 2][rating] + '.png', 110, 40));
      }
    const roster = document.getElementById('hq-roster');
    const scroll = roster.scrollLeft;
    roster.replaceChildren();
    roster.dataset.count = this.recruiting ? [6, 4, 2][this.rating] : entries.length;
    for (const definition of entries) {
      const held = owned.get(definition.id);
      const shown =
        this.tagDraft && held
          ? {...held, tag: this.tagDraft.has(held.id) ? ew4t('ui.pinned', '置顶') : ''}
          : held;
      const b = this.card(definition, shown, () => {
        if (this.tagDraft) {
          if (!this.tagSaving) {
            if (this.tagDraft.has(held.id)) this.tagDraft.delete(held.id);
            else this.tagDraft.add(held.id);
            this.drawRoster();
          }
          return;
        }
        if (this.recruiting) {
          this.offer = definition;
          this.drawPurchases();
          for (const c of roster.children) c.setAttribute('aria-pressed', String(c === b));
        } else {
          this.rosterSelected = definition.id;
          for (const c of roster.children) c.setAttribute('aria-pressed', String(c === b));
          if (!this.deployment) this.commanderInfo(definition, held);
        }
      });
      roster.append(b);
      if (held) b.dataset.commander = held.id;
      if (this.deployment) {
        b.querySelector('.commander-info').onclick = e => {
          e.stopPropagation();
          this.commanderInfo(definition, held, () => this.drawRoster());
        };
        const deploy = this.api.button(
          '',
          async () => {
            await this.api.deploy(this.deployment, held.id);
            this.closeRoster();
          },
          b,
        );
        deploy.className = 'deploy-card-confirm';
        deploy.setAttribute('aria-label', ew4t('ui.appoint', '任命 ') + definition.name);
        deploy.disabled = held.locked;
        deploy.append(this.lettering(this.text('btn_deploy'), 'font_text_3_cn_hd', '#ebe6e1'));
      }
      if (this.tagDraft) b.querySelector('.commander-open').setAttribute('aria-pressed', String(this.tagDraft.has(held.id)));
      if (!this.recruiting && !this.deployment) this.promotionShortcut(b, held, () => this.drawRoster());
      b.setAttribute('aria-pressed', String(definition.id === (this.recruiting ? this.offer?.id : this.rosterSelected)));
    }
    const capacity = data.sectionCapacity ?? 20;
    const slots = this.recruiting
      ? [6, 4, 2][this.rating]
      : this.deployment
        ? Math.max(12, entries.length)
        : Math.max(this.rosterRating === null ? capacity * 3 : capacity, entries.length);
    for (let i = entries.length; i < slots; i++) {
      const b = this.api.button(
        '',
        () => {
          if (!this.deployment) {
            this.recruiting = true;
            this.rating = this.rosterRating ?? 0;
            this.drawRoster();
          }
        },
        roster,
      );
      b.className = 'commander-card empty-commander';
      b.setAttribute('aria-label', ew4t('ui.empty_slot', '空将领位'));
      b.append(this.api.uiImage('button_add_generals.png', 78, 78));
      const name = document.createElement('span');
      name.className = 'commander-name';
      b.append(name);
      b.disabled = this.recruiting || !!this.deployment || !!this.tagDraft;
    }
    if (!this.recruiting)
      for (const [i, card] of [...roster.children].entries()) {
        card.style.gridRow = i % 2 + 1;
        card.style.gridColumn = Math.floor(i / 2) + 1;
      }
    for (const id of ['hq-recruit-band', 'hq-refresh', 'hq-buy-buttons']) document.getElementById(id)?.remove();
    if (this.recruiting) {
      const band = document.createElement('div');
      band.id = 'hq-recruit-band';
      screen.insertBefore(band, roster);
      const refresh = this.api.button(
        '',
        async () => {
          this.offer = null;
          await this.transact({op: 'refresh', group: this.rating});
        },
        screen,
      );
      refresh.id = 'hq-refresh';
      refresh.className = 'native-command blue';
      refresh.setAttribute('aria-label', ew4t('ui.refresh_commanders', '刷新将领'));
      for (let i = 0; i < 3; i++) refresh.append(this.api.uiImage('button_refresh.png', 13, 16));
      this.drawPurchases();
    }
    roster.scrollLeft = scroll;
    this.headquartersControls();
    const count = document.getElementById('hq-count');
    count.hidden = this.recruiting;
    count.replaceChildren(this.lettering(entries.length + ' / ' + slots, 'font_char_2_hd', '#404040'));
    document.getElementById('hq-deployment-quota')?.remove();
    if (this.deployment) {
      const quota = document.createElement('div');
      quota.id = 'hq-deployment-quota';
      quota.append(
        this.api.uiImage('generalnumber.png', 26, 19),
        this.lettering(
          (this.deploymentQuota?.used ?? 0) + ' / ' + (this.deploymentQuota?.limit ?? '∞'),
          'font_char_4_hd',
          '#404040',
        ),
      );
      screen.append(quota);
    }
  };
  proto.drawPurchases = function () {
    document.getElementById('hq-buy-buttons')?.remove();
    if (!this.offer) return;
    const row = document.createElement('div');
    row.id = 'hq-buy-buttons';
    document.getElementById('hq-screen').append(row);
    for (const [currency, op, price, balance] of [
      ['medals', 'recruit', this.offer.price, this.data.medals],
      ['badges', 'recruit_badge', this.offer.badgePrice, this.data.badges],
    ]) {
      if (price <= 0) continue;
      const b = this.api.button(
        '',
        async () => {
          const definition = this.offer;
          this.offer = null;
          await this.transact({op, commander: definition.id, group: this.rating});
        },
        row,
      );
      b.className = 'native-command' + (currency === 'badges' ? ' red' : '');
      b.setAttribute(
        'aria-label',
        (currency === 'badges' ? ew4t('ui.badge', '徽章') : ew4t('ui.medal', '勋章')) + ew4t('ui.recruit_for', '招募 ') + price,
      );
      b.disabled = balance < price;
      b.append(this.api.uiImage(currency + '.png', 13, 17), this.lettering(price, 'font_text_3_cn_hd', '#ebe6e1'));
    }
    this.api.popup(row);
  };
  proto.transact = async function (args) {
    this.data = await this.api.rpc('commander', {revision: this.data.revision, ...args});
    this.drawRoster();
  };
  proto.commanderInfo = function (definition, held, onClose) {
    this.commanderReturn = onClose;
    const body = this.api.detail('title_unitinfo', 440, 217, null, onClose || (() => this.drawRoster()));
    body.className = 'commander-detail';
    const card = document.createElement('div');
    card.className = 'commander-detail-card';
    const portrait = this.card(definition, held, () => {});
    portrait.querySelector('.commander-info').remove();
    portrait.querySelector('.commander-tag')?.remove();
    delete portrait.dataset.locked;
    card.append(portrait);
    body.append(card);
    const equipment = document.createElement('div');
    equipment.className = 'commander-equipment';
    const title = document.createElement('div');
    title.className = 'commander-equipment-title';
    title.append(this.lettering(this.text('text_equipitem')));
    equipment.append(title);
    const items = document.createElement('div');
    items.className = 'commander-equipment-slots';
    for (let i = 0; i < (this.data.equipmentCapacity ?? 2); i++) {
      const marker = this.data.items.find(m => m.id === held?.items[i]);
      items.append(this.api.uiImage(marker?.image || 'item_blank.png', 45));
    }
    equipment.append(items);
    body.append(equipment);
    const skills = document.createElement('div');
    skills.className = 'commander-skills';
    for (let i = 0; i < Math.max(this.data.skillLayout ? 0 : 4, definition.skills.length); i++) {
      const row = document.createElement('div');
      row.className = 'skill-row';
      const skill = definition.skills[i];
      if (skill) {
        row.append(Ew4SkillUI.icon(skill, 19, this.api.uiImage), this.lettering(skill.name));
        const info = this.api.button('', () => this.skillTip(row, skill, true), row);
        info.className = 'skill-info';
        info.setAttribute('aria-label', skill.name + '说明');
        info.append(this.api.uiImage('button_generalinfo_blue.png', 18));
        row.onpointerenter = () => this.skillTip(row, skill);
        row.onpointerleave = () => {
          if (!this.skillPinned) this.dismissSkillTip();
        };
        info.onfocus = () => this.skillTip(row, skill);
        info.onblur = () => {
          if (!this.skillPinned) this.dismissSkillTip();
        };
      }
      skills.append(row);
    }
    Ew4SkillUI.layout(skills, this.data.skillLayout, definition.skills);
    body.append(skills);
    const ranks = document.createElement('div');
    ranks.className = 'commander-ranks';
    for (const [track, effect, value] of [
      ['rank', 'hp', held?.healthBonus ?? definition.healthBonus],
      ['title', 'recover', held?.healing ?? definition.healing],
    ]) {
      const row = document.createElement('div');
      row.append(this.progressionBadge(track, held?.learning[track] ?? {level: definition[track], experience: 0}));
      const icon = this.api.uiImage('marker_' + effect + '_dark.png', 17);
      icon.className = 'rank-effect';
      row.append(icon, this.lettering(value, 'font_char_2_hd'));
      ranks.append(row);
    }
    body.append(ranks);
    const abilities = document.createElement('div');
    abilities.className = 'commander-abilities';
    const values = held?.learning.abilities || definition.abilities;
    for (const [key, icon] of [
      ['infantry', 'upgrade_tag_infantry'],
      ['cavalry', 'upgrade_tag_cavalry'],
      ['artillery', 'upgrade_tag_artillery'],
      ['naval', 'upgrade_tag_ship'],
      ['fort', 'upgrade_tag_fortress'],
      ['commerce', 'generalsinfo_trade'],
      ['movement', 'generalsinfo_move'],
      ['training', 'generalsinfo_training'],
    ]) {
      const row = document.createElement('div');
      row.className = 'commander-ability';
      row.setAttribute(
        'aria-label',
        key + ' ' + (key === 'training' ? held?.training ?? definition.training : values[key]),
      );
      row.append(this.api.uiImage(icon + '.png', 22, 21.5));
      const stars = document.createElement('div');
      stars.className = 'ability-stars';
      for (let i = 0; i < (key === 'training' ? held?.training ?? definition.training : values[key]); i++)
        stars.append(this.api.uiImage('star_middle.png', 13.5, 12.5));
      row.append(stars);
      abilities.append(row);
    }
    body.append(abilities);
    if (held && !this.deployment && !held.battle) {
      const regroup = this.api.button('', () => this.regroup(held.id), equipment);
      regroup.className = 'native-command orange regroup';
      regroup.disabled = held.locked;
      regroup.setAttribute('aria-label', this.text('btn_regroup'));
      regroup.append(this.lettering(this.text('btn_regroup'), 'font_text_3_cn_hd', '#ebe6e1'));
    }
    const item = this.api.button(
      '',
      () =>
        held?.battle
          ? this.battleEquipment(held, () => this.commanderInfo(definition, held, onClose))
          : this.equipment(held.id),
      equipment,
    );
    item.className = 'native-command blue equip';
    item.disabled = !held || !!this.deployment || (!!held.locked && !held.battle);
    item.setAttribute('aria-label', this.text('btn_item'));
    item.append(this.lettering(this.text('btn_item'), 'font_text_3_cn_hd', '#ebe6e1'));
  };
})();
