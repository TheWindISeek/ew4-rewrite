'use strict';
(function (root) {
  class Menus {
    constructor(api) {
      this.api = api;
      this.chapter = null;
      this.rating = null;
      this.rosterRating = null;
      this.recruiting = false;
      this.tagDraft = null;
      this.tagSaving = false;
      document.getElementById('hq-back').onclick = () => {
        if (this.tagSaving) return;
        if (this.tagDraft) {
          this.tagDraft = null;
          this.drawRoster();
          return;
        }
        if (this.deployment) {
          this.closeRoster();
          this.api.tutorialUi('winbtn_back');
          return;
        }
        if (this.recruiting) {
          this.recruiting = false;
          this.drawRoster();
          return;
        }
        this.api.main();
      };
      document.getElementById('hq-recruit').onclick = () => {
        this.recruiting = true;
        this.rating = 0;
        this.offer = null;
        this.drawRoster();
      };
      const roster = document.getElementById('hq-roster');
      let drag = null;
      let dragged = false;
      roster.onpointerdown = e => {
        if (this.recruiting || e.button !== 0) return;
        dragged = false;
        drag = {id: e.pointerId, x: e.clientX, scroll: roster.scrollLeft};
      };
      roster.onpointermove = e => {
        if (!drag || e.pointerId !== drag.id) return;
        const scale = roster.getBoundingClientRect().width / roster.offsetWidth;
        const dx = (e.clientX - drag.x) / scale;
        if (!dragged && Math.abs(e.clientX - drag.x) > 6) {
          dragged = true;
          roster.setPointerCapture(e.pointerId);
          roster.classList.add('dragging');
        }
        if (dragged) {
          e.preventDefault();
          roster.scrollLeft = drag.scroll - dx;
        }
      };
      const endDrag = () => {
        drag = null;
        roster.classList.remove('dragging');
      };
      roster.onpointerup = endDrag;
      roster.onpointercancel = endDrag;
      roster.onlostpointercapture = e => {
        if (e.target === roster) endDrag();
      };
      roster.addEventListener(
        'click',
        e => {
          if (dragged) {
            e.preventDefault();
            e.stopImmediatePropagation();
            dragged = false;
          }
        },
        true,
      );
      roster.ondragstart = e => e.preventDefault();
    }
    get manifest() {
      return this.api.manifest;
    }
    get catalog() {
      return this.api.catalog;
    }
    countryFlag(id, width, height) {
      return this.api.countryImage?.(id, width, height) || this.api.uiImage(id + '1.png', width, height);
    }
    text(key) {
      return this.manifest.strings[key] || key;
    }
    lettering(text, font = 'font_text_2_cn_hd', color = '#404040', width, align, options) {
      return this.api.text(text, font, color, width, align, options);
    }
    slideInSidebar(list) {
      const rows = [...list.children];
      for (const node of [list, ...rows]) for (const animation of node.getAnimations?.() || []) animation.cancel();
      if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
      const easing = 'cubic-bezier(.2,.7,.25,1)';
      list.animate([{translate: '180px 0'}, {translate: '0 0'}], {duration: 220, easing});
      rows.forEach((row, index) =>
        row.animate([{translate: '160px 0'}, {translate: '0 0'}], {
          duration: 300,
          delay: Math.min(index, 5) * 60,
          easing,
          fill: 'backwards',
        }),
      );
    }
    async open(mode) {
      if (mode === 4) return this.tutorialMenu();
      if (mode !== 1) return false;
      return this.openCampaignChapterSelect();
    }
    portrait(name) {
      const img = document.createElement('img');
      img.alt = '';
      img.width = 78;
      img.height = 78;
      const atlas = ['princesses_hd', 'commanders1_hd', 'commanders2_hd', 'commanders3_hd'].find(a => this.manifest.atlases[a]?.[name]);
      if (atlas) img.src = '/content/ui/' + atlas + '/' + name;
      else {
        img.src = '/content/ui/image_ui_hd/button_add_generals.png';
        img.title = name;
      }
      return img;
    }
  }
  root.Ew4Menus = {Menus};
})(globalThis);
