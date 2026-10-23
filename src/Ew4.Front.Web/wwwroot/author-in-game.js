'use strict';
(() => {
  const el = id => document.getElementById(id);
  let workspace = null;
  let scenarioId = null;
  let scenarioDoc = null;
  let scenarioGroups = { campaign: [], conquest: [], tutorial: [], other: [] };
  let scenarioCategory = 'campaign';

  function say(text, ok) {
    const box = el('author-notice');
    box.textContent = text || '';
    box.hidden = !text;
    box.style.color = ok ? '#2d5a2d' : '#5a3d28';
  }

  async function api(path, options = {}) {
    const response = await fetch(path, { cache: 'no-store', ...options });
    const type = response.headers.get('content-type') || '';
    if (!response.ok) {
      let message = response.statusText;
      if (type.includes('json')) {
        const body = await response.json();
        message = body.message || body.code || message;
      }
      throw new Error(message);
    }
    if (type.includes('json')) return response.json();
    return response.arrayBuffer();
  }

  function show(view) {
    el('author-home').hidden = view !== 'home';
    el('author-workspace').hidden = view !== 'workspace';
    el('author-scenario').hidden = view !== 'scenario';
    el('author-heading').textContent =
      view === 'home' ? 'Mod 创作' : view === 'workspace' ? workspace.name : `剧本 · ${scenarioId}`;
  }

  function workspaceRow(ws) {
    const box = document.createElement('div');
    box.className = 'mod-row';
    const info = document.createElement('div');
    info.className = 'mod-row-info';
    const title = document.createElement('div');
    title.className = 'mod-row-name';
    title.textContent = ws.name;
    const meta = document.createElement('div');
    meta.className = 'mod-row-meta';
    meta.textContent = `${ws.id} · parent=${ws.parentPackId}`;
    info.append(title, meta);
    const actions = document.createElement('div');
    actions.className = 'mod-row-actions';
    const open = document.createElement('button');
    open.className = 'native-command';
    open.textContent = '打开';
    open.onclick = () => openWorkspace(ws);
    actions.append(open);
    box.append(info, actions);
    return box;
  }

  async function loadWorkspaces() {
    const list = await api('/api/author/workspaces');
    const host = el('author-workspace-list');
    host.replaceChildren();
    for (const ws of list) host.append(workspaceRow(ws));
  }

  function normalizeScenarioGroups(data) {
    if (Array.isArray(data)) {
      const all = data.filter(id => !String(id).includes('.ew4scenario'));
      return {
        campaign: all.filter(id => String(id).startsWith('campaign')),
        conquest: all.filter(id => String(id).startsWith('conquest')),
        tutorial: all.filter(id => String(id).startsWith('tutorial')),
        other: all.filter(id => {
          const s = String(id);
          return !s.startsWith('campaign') && !s.startsWith('conquest') && !s.startsWith('tutorial');
        }),
      };
    }
    return {
      campaign: data.campaign || [],
      conquest: data.conquest || [],
      tutorial: data.tutorial || [],
      other: data.other || [],
    };
  }

  function setScenarioCategory(cat) {
    scenarioCategory = cat;
    for (const tab of document.querySelectorAll('.author-cat-tab')) {
      tab.setAttribute('aria-pressed', tab.dataset.cat === cat ? 'true' : 'false');
    }
    renderScenarioList();
  }

  function scenarioLabel(id) {
    if (scenarioCategory === 'campaign' && /^campaign(\d+)_/.test(id)) {
      const chapter = id.match(/^campaign(\d+)_/)[1];
      return `第 ${chapter} 章 · ${id}`;
    }
    if (scenarioCategory === 'conquest' && /^conquest(\d+)$/.test(id)) {
      return `征服 ${id.replace('conquest', '')} · ${id}`;
    }
    return id;
  }

  function renderScenarioList() {
    const ids = scenarioGroups[scenarioCategory] || [];
    const host = el('author-scenario-list');
    const empty = el('author-scenario-empty');
    host.replaceChildren();
    empty.hidden = ids.length > 0;
    host.hidden = ids.length === 0;
    for (const id of ids) {
      const box = document.createElement('div');
      box.className = 'mod-row';
      const info = document.createElement('div');
      info.className = 'mod-row-info';
      const title = document.createElement('div');
      title.className = 'mod-row-name';
      title.textContent = scenarioLabel(id);
      const meta = document.createElement('div');
      meta.className = 'mod-row-meta';
      meta.textContent = id;
      info.append(title, meta);
      const actions = document.createElement('div');
      actions.className = 'mod-row-actions';
      const edit = document.createElement('button');
      edit.type = 'button';
      edit.className = 'native-command blue';
      edit.textContent = '编辑';
      edit.onclick = () => openScenario(id);
      actions.append(edit);
      box.append(info, actions);
      host.append(box);
    }
  }

  async function openWorkspace(ws) {
    workspace = ws;
    show('workspace');
    say('');
    scenarioGroups = normalizeScenarioGroups(await api(`/api/author/workspaces/${ws.id}/scenarios`));
    if (!scenarioGroups[scenarioCategory]?.length) {
      const order = ['campaign', 'conquest', 'tutorial', 'other'];
      scenarioCategory = order.find(c => scenarioGroups[c]?.length) || 'campaign';
    }
    setScenarioCategory(scenarioCategory);
  }

  function renderArmies() {
    const armies = scenarioDoc?.layers?.armies || [];
    const host = el('author-army-table');
    host.replaceChildren();
    armies.forEach((a, index) => {
      const box = document.createElement('div');
      box.className = 'mod-row';
      box.setAttribute('aria-selected', 'false');
      const info = document.createElement('div');
      info.className = 'mod-row-info';
      const title = document.createElement('div');
      title.className = 'mod-row-name';
      title.textContent = `#${index} · 格子 ${a.cell}`;
      const meta = document.createElement('div');
      meta.className = 'mod-row-meta';
      meta.textContent = `兵种 ${a.unit} · 将领 ${a.commander} · HP ${a.health}/${a.maximumHealth}`;
      info.append(title, meta);
      box.append(info);
      box.onclick = () => {
        for (const row of host.children) row.setAttribute('aria-selected', 'false');
        box.setAttribute('aria-selected', 'true');
        el('author-army-index').value = index;
        el('author-army-cell').value = a.cell;
      };
      host.append(box);
    });
  }

  async function openScenario(id) {
    scenarioId = id;
    show('scenario');
    el('author-scenario-title').textContent = id;
    const buf = await api(`/api/author/workspaces/${workspace.id}/scenario/${id}`);
    scenarioDoc = JSON.parse(new TextDecoder().decode(buf));
    renderArmies();
    say('');
  }

  async function postEdit(body) {
    await api(`/api/author/workspaces/${workspace.id}/scenario/${scenarioId}/edit`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    await openScenario(scenarioId);
    say('已保存', true);
  }

  function authorGoBack() {
    if (!el('author-scenario').hidden) {
      openWorkspace(workspace).catch(err => say(err.message));
      return;
    }
    if (!el('author-workspace').hidden) {
      workspace = null;
      scenarioId = null;
      el('author-diag').hidden = true;
      show('home');
      loadWorkspaces();
      say('');
      return;
    }
    window.Ew4Author.close();
    el('mod-hub').hidden = false;
    if (typeof popupOpen === 'function') popupOpen(el('mod-hub').firstElementChild);
  }

  window.Ew4Author = {
    open() {
      el('author-manager').hidden = false;
      if (typeof popupOpen === 'function') popupOpen(el('author-manager').firstElementChild);
      workspace = null;
      scenarioId = null;
      show('home');
      loadWorkspaces().catch(e => say(e.message));
    },
    close() {
      el('author-manager').hidden = true;
    },
    goBack: authorGoBack,
  };

  for (const tab of document.querySelectorAll('.author-cat-tab')) {
    tab.onclick = () => setScenarioCategory(tab.dataset.cat);
  }

  el('author-back').onclick = () => authorGoBack();

  el('author-create').onsubmit = async e => {
    e.preventDefault();
    try {
      await api('/api/author/workspaces', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          id: el('author-ws-id').value.trim(),
          name: el('author-ws-name').value.trim(),
          fromPackId: 'base',
        }),
      });
      el('author-ws-id').value = '';
      await loadWorkspaces();
      say('草稿已创建', true);
    } catch (err) {
      say(err.message);
    }
  };

  el('author-army-apply').onclick = async () => {
    try {
      await postEdit({
        type: 'army',
        index: Number(el('author-army-index').value),
        fields: { cell: Number(el('author-army-cell').value) },
      });
    } catch (err) {
      say(err.message);
    }
  };

  el('author-validate').onclick = async () => {
    try {
      const diag = await api(`/api/author/workspaces/${workspace.id}/validate?level=SmokeStart`);
      el('author-diag').hidden = false;
      el('author-diag').textContent = JSON.stringify(diag, null, 2);
      const errors = diag.filter(d => d.severity === 'Error');
      say(errors.length ? `校验失败：${errors.length} 项` : '校验通过', !errors.length);
    } catch (err) {
      say(err.message);
    }
  };

  el('author-export').onclick = async () => {
    try {
      const result = await api(`/api/author/workspaces/${workspace.id}/export?level=SmokeStart`);
      el('author-diag').hidden = false;
      el('author-diag').textContent = JSON.stringify(result, null, 2);
      if (result.success && result.archivePath) {
        const name = result.archivePath.split(/[/\\]/).pop();
        window.open(`/api/author/workspaces/${workspace.id}/files/exports/${encodeURIComponent(name)}`, '_blank');
        say('导出成功：' + name, true);
      } else say('导出失败，见下方诊断');
    } catch (err) {
      say(err.message);
    }
  };

  el('author-preview').onclick = async () => {
    try {
      await api(`/api/author/workspaces/${workspace.id}/preview/enter`, { method: 'POST' });
      say('已进入预览，请关闭本窗口后从主菜单试玩', true);
    } catch (err) {
      say(err.message);
    }
  };
})();
