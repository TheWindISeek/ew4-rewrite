'use strict';
(function () {
  const proto = Ew4Menus.Menus.prototype;
  document.getElementById('campaign-back').onclick = () => {
    document.getElementById('campaign-screen').hidden = true;
  };
  proto.openCampaignChapterSelect = async function () {
    const list = document.getElementById('scenarios');
    list.replaceChildren();
    list.className = 'chapter-list';
    const layout = this.manifest.layouts.form_selcampaign;
    const custom = this.catalog.chapters.filter(
      chapter => !layout.children.some(e => e.attributes.id === 'btn_selcamp_' + chapter.id),
    );
    if (custom.length) list.classList.add('extended-chapters');
    for (const chapter of this.catalog.chapters) {
      const placement = layout.children.find(e => e.attributes.id === 'btn_selcamp_' + chapter.id)?.attributes;
      const b = this.api.button('', () => this.previewChapter(chapter, b), list);
      b.className = 'chapter-entry';
      b.setAttribute('aria-label', chapter.name);
      if (placement) {
        Object.assign(b.style, {
          left: placement.x + 'px',
          top: placement.y + 'px',
          width: placement.w + 'px',
          height: placement.h + 'px',
        });
        b.append(this.api.uiImage(placement.frm1, Number(placement.w), Number(placement.h)));
      } else {
        b.classList.add('custom-chapter');
        Object.assign(b.style, {
          left: '356px',
          top: 14 + custom.indexOf(chapter) * 48 + 'px',
          width: '185px',
          height: '40px',
        });
        b.append(this.lettering(chapter.name, 'font_text_2_cn_hd', '#404040', 170, 'center'));
      }
      b.title = chapter.name + ' ' + chapter.start + '–' + chapter.end;
    }
    document.getElementById('scenario-menu').onclick = e => {
      if (!e.target.closest('.chapter-entry,.chapter-preview')) document.querySelector('.chapter-preview')?.remove();
    };
    return true;
  };
  proto.previewChapter = function (chapter, anchor) {
    document.querySelector('.chapter-preview')?.remove();
    const popup = document.createElement('section');
    popup.className = 'chapter-preview';
    popup.setAttribute('aria-label', chapter.name + ' ' + chapter.start + '-' + chapter.end);
    const title = document.createElement('div');
    title.className = 'chapter-preview-title';
    title.append(this.lettering(chapter.name, 'font_text_2_cn_hd', '#ebe6e1', 139, 'center', {singleLine: true}));
    const years = document.createElement('div');
    years.className = 'chapter-preview-years';
    years.append(this.lettering(chapter.start + '-' + chapter.end, 'font_char_2_hd', '#404040'));
    const flags = document.createElement('div');
    flags.className = 'chapter-preview-flags';
    for (const country of chapter.countries) flags.append(this.countryFlag(country, 26, 15));
    popup.append(title, years, flags);
    const confirm = this.api.button(
      '',
      () => {
        popup.remove();
        return this.openChapter(chapter);
      },
      popup,
    );
    confirm.className = 'chapter-preview-confirm';
    confirm.setAttribute('aria-label', ew4t('ui.enter', '进入 ') + chapter.name);
    confirm.title = ew4t('ui.enter', '进入 ') + chapter.name;
    confirm.append(this.api.uiImage('button_confrim.png', 31, 31.5));
    const targetX = parseFloat(anchor.style.left) + 25;
    const targetY = parseFloat(anchor.style.top) + 54;
    const left = Math.max(4, Math.min(425, targetX - 69.5));
    popup.style.left = left + 'px';
    popup.style.top = Math.max(24, targetY - 69.5) + 'px';
    popup.style.setProperty('--chapter-arrow-left', targetX - left - 16.5 + 'px');
    document.getElementById('scenarios').append(popup);
    confirm.focus({preventScroll: true});
  };
  proto.presentationDescription = function (presentation) {
    const text = presentation.description || '';
    if (!text) return '';
    if (text.startsWith('desc_') || text.startsWith('title_') || text.startsWith('name_')) return this.text(text) || text;
    return text;
  };
  proto.battleStrategicDefinition = function (presentation) {
    const manifestDef = this.manifest.strategic[presentation.id] || {};
    const definition = {...manifestDef};
    if (presentation.centerX != null) definition.centerx = String(presentation.centerX);
    if (presentation.centerY != null) definition.centery = String(presentation.centerY);
    if (presentation.strategicName) definition.name = presentation.strategicName;
    return definition;
  };
  proto.openChapter = async function (chapter) {
    await this.api.refreshCatalog();
    await this.api.loadLibrary('strategicmap');
    this.chapter = chapter;
    document.getElementById('campaign-title').replaceChildren(this.lettering(chapter.name, 'font_text_2_cn_hd', '#ebe6e1'));
    const list = document.getElementById('campaign-battles');
    list.replaceChildren();
    const chapterArt = ['france', 'coalition', 'holyroman', 'east', 'usa', 'uk'][chapter.id - 1] || 'france';
    list.style.setProperty('--chapter-entry-image', 'url("/content/ui/image_menu_hd/button_choosestage_' + chapterArt + '.png")');
    for (const presentation of Object.values(this.catalog.presentations).filter(s => s.chapter === chapter.id)) {
      if (presentation.hidden && !this.catalog.unlockedCampaigns?.includes(presentation.id)) continue;
      const scenario = this.catalog.scenarios.find(s => s.id === presentation.id);
      if (!scenario) continue;
      const b = this.api.button('', () => this.chooseBattle(presentation, b), list);
      b.className = 'country-entry';
      const starMap = this.catalog.campaignStars || {};
      const stars = Number(starMap[presentation.id] ?? (presentation.alternate ? starMap[presentation.alternate] : 0) ?? 0);
      b.setAttribute('aria-label', scenario.name + (stars > 0 ? ' ' + stars + '/5' : ''));
      b.setAttribute('aria-pressed', 'false');
      const label = document.createElement('div');
      label.className = 'campaign-stage-label';
      label.append(this.lettering(scenario.name, 'font_text_2_cn_hd', '#404040', 132, 'center', {singleLine: true}));
      if (stars > 0) {
        const row = document.createElement('div');
        row.className = 'campaign-stage-stars';
        row.setAttribute('aria-hidden', 'true');
        for (let i = 0; i < Math.min(5, stars); i++) row.append(this.api.uiImage('star_middle.png', 10, 9));
        label.append(row);
      }
      b.append(label);
    }
    document.getElementById('campaign-screen').hidden = false;
    list.scrollTop = 0;
    list.firstElementChild?.click();
    this.slideInSidebar(list);
  };
  proto.chooseBattle = function (presentation, button) {
    this.battle = presentation;
    for (const b of document.getElementById('campaign-battles').children) b.setAttribute('aria-pressed', String(b === button));
    document.getElementById('campaign-description').replaceChildren(
      this.lettering(this.presentationDescription(presentation), 'font_text_1_cn_hd', '#404040', 267),
    );
    document.getElementById('campaign-description').scrollTop = 0;
    const commander = document.getElementById('campaign-commander');
    commander.replaceChildren();
    if (presentation.commander) {
      commander.append(this.portrait(presentation.commander + '.png'));
      const name = document.createElement('span');
      name.append(this.lettering(this.text('name_' + presentation.commander), 'font_text_2_cn_hd', '#ebe6e1', 70, 'center'));
      commander.append(name);
    }
    document.getElementById('campaign-confirm').onclick = () => this.confirmBattle(presentation).catch(e => this.api.notice(e.message));
  };
  proto.confirmBattle = async function (presentation) {
    if (presentation.alternate && presentation.countries.length) {
      let chosen = 0;
      const body = this.api.detail('country_label', 234, 150, () => this.api.start(chosen ? presentation.alternate : presentation.id, null));
      body.className = 'scenario-countries';
      presentation.countries.forEach((country, index) => {
        const name = this.api.catalog?.countries?.[country]?.name || this.text('name_' + country);
        const b = this.api.button(
          '',
          () => {
            chosen = index;
            for (const c of body.children) c.setAttribute('aria-pressed', String(c === b));
          },
          body,
        );
        b.setAttribute('aria-label', name);
        b.append(this.countryFlag(country, 51, 30), this.api.labels('country_' + country, name));
        if (!index) b.click();
      });
    } else await this.api.start(presentation.id, null);
  };
  proto.drawCampaignStrategic = function (time) {
    if (document.getElementById('campaign-screen').hidden || !this.battle) return;
    const surface = document.getElementById('campaign-map');
    const draw = surface.getContext('2d');
    const lib = this.api.libraries.get('strategicmap');
    if (!lib) return;
    const rect = surface.getBoundingClientRect();
    const ratio = Math.min(devicePixelRatio || 1, 3);
    const w = Math.round(rect.width * ratio);
    const h = Math.round(rect.height * ratio);
    if (surface.width !== w || surface.height !== h) {
      surface.width = w;
      surface.height = h;
    }
    draw.setTransform(w / 568, 0, 0, h / 320, 0, 0);
    draw.clearRect(0, 0, 568, 320);
    const definition = this.battleStrategicDefinition(this.battle);
    const cx = Number(definition.centerx || 410);
    const cy = Number(definition.centery || 288);
    const scale = Number(definition.scale || 1);
    draw.save();
    this.api.menuArt.drawStrategic(draw, definition, scale);
    draw.translate(205, 128);
    draw.scale(scale, scale);
    if (lib.byName.has(definition.name)) this.api.animation(lib, lib.byName.get(definition.name), time / 180, 0, draw);
    draw.restore();
  };
  proto.tutorialMenu = function () {
    const body = this.api.detail('tutorial_label', 250, 200);
    body.className = 'tutorial-menu';
    for (const [title, action] of [
      ['btn_basic', () => this.beginTutorial('tutorials1')],
      ['btn_classic', () => this.beginTutorial('tutorials2')],
      ['btn_notice', () => this.playNotice()],
    ]) {
      const b = this.api.button(this.text(title), action, body);
      b.className = 'native-command';
    }
    return true;
  };
  proto.beginTutorial = async function (id) {
    document.getElementById('detail-modal').hidden = true;
    await this.api.start(id, null);
  };
  proto.playNotice = function () {
    const body = this.api.detail('tutorial_label', 400, 225);
    body.className = 'play-notice';
    const p = document.createElement('p');
    p.textContent = this.text('html_notice').replace(/<[^>]*>/g, '').replace(/\\n/g, '\n');
    body.append(p);
  };
  proto.draw = function (time) {
    this.drawCampaignStrategic(time);
  };
})();
