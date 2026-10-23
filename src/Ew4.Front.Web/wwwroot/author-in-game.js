'use strict';
(() => {
  const el = id => document.getElementById(id);
  let workspace = null;
  let scenarioId = null;
  let scenarioDoc = null;
  let scenarioGroups = { campaign: [], conquest: [], tutorial: [], other: [] };
  let scenarioCategory = 'campaign';
  let workspaceMode = 'commander';
  let commanders = [];
  let skillCatalog = [];
  let commanderId = null;
  let commanderEntry = null;

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
    el('author-commander').hidden = view !== 'commander';
    if (view === 'home') {
      el('author-heading').textContent = 'Mod 创作';
    } else if (view === 'workspace') {
      el('author-heading').textContent = workspace.name;
    } else if (view === 'scenario') {
      el('author-heading').textContent = `战役 · ${scenarioId}`;
    } else if (view === 'commander') {
      el('author-heading').textContent = `将领 · ${commanderEntry?.name || commanderId}`;
    }
  }

  function setWorkspaceMode(mode) {
    workspaceMode = mode;
    for (const tab of document.querySelectorAll('.author-mode-tab')) {
      tab.setAttribute('aria-pressed', tab.dataset.mode === mode ? 'true' : 'false');
    }
    el('author-ws-panel-commander').hidden = mode !== 'commander';
    el('author-ws-panel-campaign').hidden = mode !== 'campaign';
    el('author-ws-panel-conquest').hidden = mode !== 'conquest';
    if (mode === 'commander') renderCommanderList();
    if (mode === 'campaign') renderScenarioList();
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

  function commanderFilterText() {
    return (el('author-commander-filter')?.value || '').trim().toLowerCase();
  }

  function renderCommanderList() {
    const host = el('author-commander-list');
    if (!host) return;
    const q = commanderFilterText();
    const rows = commanders.filter(c => {
      if (!q) return true;
      const id = String(c.legacyId);
      return id.includes(q) || (c.name || '').toLowerCase().includes(q);
    });
    host.replaceChildren();
    const cap = 200;
    for (const c of rows.slice(0, cap)) {
      const box = document.createElement('div');
      box.className = 'mod-row';
      const info = document.createElement('div');
      info.className = 'mod-row-info';
      const title = document.createElement('div');
      title.className = 'mod-row-name';
      title.textContent = c.name || `将领 ${c.legacyId}`;
      const meta = document.createElement('div');
      meta.className = 'mod-row-meta';
      meta.textContent = `id=${c.legacyId} · 步/骑/炮/海/塞 ${c.infantry}/${c.cavalry}/${c.artillery}/${c.warship}/${c.fort} · 技 ${c.skill1}/${c.skill2}/${c.skill3}/${c.skill4}`;
      info.append(title, meta);
      const actions = document.createElement('div');
      actions.className = 'mod-row-actions';
      const edit = document.createElement('button');
      edit.type = 'button';
      edit.className = 'native-command blue';
      edit.textContent = '编辑';
      edit.onclick = () => openCommander(c.legacyId);
      actions.append(edit);
      box.append(info, actions);
      host.append(box);
    }
    if (rows.length > cap) {
      say(`仅显示前 ${cap} 条，请用筛选缩小范围`, true);
    }
  }

  function skillDisplayName(legacyId) {
    const hit = skillCatalog.find(s => s.legacyId === legacyId);
    return hit ? `${hit.displayName} (#${legacyId})` : legacyId === 0 ? '无' : `ID ${legacyId}`;
  }

  function updateSkillCaptions() {
    for (let i = 1; i <= 4; i++) {
      const cap = el(`author-commander-skill${i}-name`);
      if (cap) cap.textContent = skillDisplayName(Number(el(`author-commander-skill${i}`).value));
    }
  }

  async function loadCommanders() {
    const [view, skills] = await Promise.all([
      api(`/api/author/workspaces/${workspace.id}/commanders`),
      api(`/api/author/workspaces/${workspace.id}/commanders/skills`),
    ]);
    commanders = view.entries || [];
    skillCatalog = skills.skills || [];
    renderCommanderList();
  }

  async function openWorkspace(ws) {
    workspace = ws;
    commanderId = null;
    commanderEntry = null;
    show('workspace');
    say('');
    setWorkspaceMode(workspaceMode);
    scenarioGroups = normalizeScenarioGroups(await api(`/api/author/workspaces/${ws.id}/scenarios`));
    if (!scenarioGroups[scenarioCategory]?.length) {
      const order = ['campaign', 'conquest', 'tutorial', 'other'];
      scenarioCategory = order.find(c => scenarioGroups[c]?.length) || 'campaign';
    }
    setScenarioCategory(scenarioCategory);
    try {
      await loadCommanders();
    } catch (err) {
      say(err.message);
    }
  }

  function fillCommanderForm(entry) {
    el('author-commander-skill1').value = entry.skill1;
    el('author-commander-skill2').value = entry.skill2;
    el('author-commander-skill3').value = entry.skill3;
    el('author-commander-skill4').value = entry.skill4;
    el('author-commander-infantry').value = entry.infantry;
    el('author-commander-cavalry').value = entry.cavalry;
    el('author-commander-artillery').value = entry.artillery;
    el('author-commander-warship').value = entry.warship;
    el('author-commander-fort').value = entry.fort;
    el('author-commander-title').textContent = `${entry.name || '将领'} (表 id=${entry.legacyId} · 军衔 ${entry.rank} · 爵位 ${entry.nobilityRank})`;
    updateSkillCaptions();
  }

  async function openCommander(legacyId) {
    commanderId = legacyId;
    commanderEntry = await api(`/api/author/workspaces/${workspace.id}/commanders/${legacyId}`);
    fillCommanderForm(commanderEntry);
    show('commander');
    say('');
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
    if (!el('author-commander').hidden) {
      commanderId = null;
      commanderEntry = null;
      show('workspace');
      setWorkspaceMode('commander');
      loadCommanders().catch(err => say(err.message));
      return;
    }
    if (!el('author-scenario').hidden) {
      openWorkspace(workspace).catch(err => say(err.message));
      return;
    }
    if (!el('author-workspace').hidden) {
      workspace = null;
      scenarioId = null;
      commanderId = null;
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
      workspaceMode = 'commander';
      show('home');
      loadWorkspaces().catch(e => say(e.message));
    },
    close() {
      el('author-manager').hidden = true;
    },
    goBack: authorGoBack,
  };

  for (const tab of document.querySelectorAll('.author-mode-tab')) {
    tab.onclick = () => setWorkspaceMode(tab.dataset.mode);
  }

  for (const tab of document.querySelectorAll('.author-cat-tab')) {
    tab.onclick = () => setScenarioCategory(tab.dataset.cat);
  }

  el('author-back').onclick = () => authorGoBack();

  el('author-commander-filter')?.addEventListener('input', () => renderCommanderList());
  for (let i = 1; i <= 4; i++) {
    el(`author-commander-skill${i}`)?.addEventListener('input', updateSkillCaptions);
  }

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

  el('author-commander-save').onclick = async () => {
    if (!workspace || commanderId == null) return;
    try {
      const body = {
        skill1: Number(el('author-commander-skill1').value),
        skill2: Number(el('author-commander-skill2').value),
        skill3: Number(el('author-commander-skill3').value),
        skill4: Number(el('author-commander-skill4').value),
        infantry: Number(el('author-commander-infantry').value),
        cavalry: Number(el('author-commander-cavalry').value),
        artillery: Number(el('author-commander-artillery').value),
        warship: Number(el('author-commander-warship').value),
        fort: Number(el('author-commander-fort').value),
      };
      await api(`/api/author/workspaces/${workspace.id}/commanders/${commanderId}/edit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      commanderEntry = await api(`/api/author/workspaces/${workspace.id}/commanders/${commanderId}`);
      fillCommanderForm(commanderEntry);
      await loadCommanders();
      say('将领已保存', true);
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
