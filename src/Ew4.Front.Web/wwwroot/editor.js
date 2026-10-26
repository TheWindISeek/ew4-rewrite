'use strict';

const $ = id => document.getElementById(id);
let currentWorkspace = null;
let currentScenario = null;
let scenarioDoc = null;
let selection = null;

const FIELD_DEFS = {
  army: [
    ['cell', '格子', 'number'],
    ['unit', '兵种 unit', 'number'],
    ['grade', 'grade', 'number'],
    ['health', '生命', 'number'],
    ['maximumHealth', '最大生命', 'number'],
    ['commander', '将领 id', 'number'],
    ['movement', '移动力', 'number'],
    ['morale', '士气', 'number'],
    ['moraleDuration', '士气持续', 'number'],
  ],
  site: [
    ['cell', '格子', 'number'],
    ['cityId', '城市 id', 'number'],
    ['buildingKind', '建筑类型', 'number'],
    ['buildingLevel', '等级', 'number'],
    ['installationKind', '设施', 'number'],
  ],
  faction: [
    ['money', '金', 'number'],
    ['industry', '工业', 'number'],
    ['food', '粮', 'number'],
    ['camp', '阵营', 'number'],
    ['countryId', '国家 id', 'text'],
    ['variant', '变体', 'text'],
    ['ai', '电脑', 'checkbox'],
  ],
  event: [
    ['id', 'id', 'number'],
    ['condition', '条件', 'number'],
    ['effect', '效果', 'number'],
    ['round', '回合', 'number'],
    ['dialogue', '对白', 'number'],
    ['cell', '格子', 'number'],
    ['country', '国家', 'text'],
  ],
};

function notice(text, isError = true) {
  const el = $('notice');
  el.textContent = text;
  el.hidden = !text;
  el.style.background = isError ? '#fff3cd' : '#d4edda';
}

async function api(path, options = {}) {
  const response = await fetch(path, { cache: 'no-store', ...options });
  const contentType = response.headers.get('content-type') || '';
  if (!response.ok) {
    let message = response.statusText;
    if (contentType.includes('json')) {
      const body = await response.json();
      message = body.message || body.code || message;
    }
    throw new Error(message);
  }
  if (contentType.includes('json')) return response.json();
  return response.arrayBuffer();
}

// 场景文档为 schema 3：armies / sites / factions / events 在顶层，地图在 map。
function layers() {
  return {
    armies: scenarioDoc?.armies || [],
    sites: scenarioDoc?.sites || [],
  };
}

function factions() {
  return scenarioDoc?.factions || [];
}

function events() {
  return scenarioDoc?.events || [];
}

function factionResources(f) {
  return { money: f.money ?? 0, industry: f.industry ?? 0, food: f.food ?? 0 };
}

function mapSize() {
  const crop = scenarioDoc?.map?.crop || {};
  return { width: crop.w, height: crop.h };
}

async function loadPreviewStatus() {
  try {
    const state = await api('/api/editor/preview');
    $('preview-badge').hidden = !state.active;
    $('btn-preview-exit').hidden = !state.active;
  } catch (_) {
    $('preview-badge').hidden = true;
  }
}

async function loadWorkspaces() {
  const list = await api('/api/editor/workspaces');
  const ul = $('workspace-list');
  ul.replaceChildren();
  for (const ws of list) {
    const li = document.createElement('li');
    const label = document.createElement('span');
    label.textContent = `${ws.name} (${ws.id}) · parent=${ws.parentPackId}`;
    const open = document.createElement('button');
    open.textContent = '打开';
    open.onclick = () => openWorkspace(ws);
    li.append(label, open);
    ul.append(li);
  }
  const packs = await api('/api/mods?q=' + encodeURIComponent('{}')).catch(() => null);
  const sel = $('ws-from');
  if (packs?.mods) {
    for (const m of packs.mods) {
      if (!sel.querySelector(`option[value="${m.id}"]`)) {
        const o = document.createElement('option');
        o.value = m.id;
        o.textContent = m.id;
        sel.append(o);
      }
    }
  }
}

function showWorkspacePanel(show) {
  $('panel-workspaces').hidden = show;
  $('panel-workspace').hidden = !show;
  $('panel-scenario').hidden = true;
}

async function openWorkspace(ws) {
  currentWorkspace = ws;
  currentScenario = null;
  $('ws-title').textContent = `${ws.name} (${ws.id})`;
  showWorkspacePanel(true);
  await refreshScenarios();
  notice('');
}

function flattenScenarioGroups(data) {
  if (Array.isArray(data)) {
    return data.filter(id => !String(id).includes('.ew4scenario'));
  }
  return ['campaign', 'conquest', 'tutorial', 'other'].flatMap(k => data[k] || []);
}

async function refreshScenarios() {
  const data = await api(`/api/editor/workspaces/${currentWorkspace.id}/campaigns`);
  const ul = $('scenario-list');
  ul.replaceChildren();
  const sections = Array.isArray(data)
    ? [{ title: '剧本', ids: flattenScenarioGroups(data) }]
    : [
        { title: '战役', ids: data.campaign || [] },
        { title: '征服', ids: data.conquest || [] },
        { title: '教程', ids: data.tutorial || [] },
        { title: '其他', ids: data.other || [] },
      ];
  for (const section of sections) {
    if (!section.ids.length) continue;
    const head = document.createElement('li');
    head.className = 'scenario-group-title';
    head.textContent = section.title;
    ul.append(head);
    for (const id of section.ids) {
      const li = document.createElement('li');
      li.textContent = id;
      const edit = document.createElement('button');
      edit.textContent = '编辑';
      edit.onclick = () => openScenario(id);
      li.append(edit);
      ul.append(li);
    }
  }
}

async function openScenario(id) {
  currentScenario = id;
  selection = null;
  $('sc-title').textContent = `剧本 · ${id}`;
  $('panel-scenario').hidden = false;
  const buf = await api(`/api/editor/workspaces/${currentWorkspace.id}/campaign/${id}`);
  scenarioDoc = JSON.parse(new TextDecoder().decode(buf));
  $('meta-mapId').value = scenarioDoc.map?.mapId ?? '';
  $('meta-width').value = mapSize().width ?? '';
  $('meta-height').value = mapSize().height ?? '';
  renderEditor();
  notice('');
}

async function reloadScenario() {
  await openScenario(currentScenario);
}

async function postEdit(payload) {
  await api(`/api/editor/workspaces/${currentWorkspace.id}/campaign/${currentScenario}/edit`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  await reloadScenario();
  notice('已保存', false);
}

function selectEntity(kind, index) {
  selection = { kind, index };
  renderInspector();
  renderMap();
  document.querySelectorAll('.data-table tr').forEach(tr => tr.classList.remove('selected'));
  const row = document.querySelector(`tr[data-kind="${kind}"][data-index="${index}"]`);
  if (row) row.classList.add('selected');
}

function entityValue(kind, index) {
  if (kind === 'army') return layers().armies[index];
  if (kind === 'site') return layers().sites[index];
  if (kind === 'faction') {
    const f = factions()[index];
    const res = factionResources(f);
    return {
      ...f,
      money: res.money,
      industry: res.industry,
      food: res.food,
      countryId: f.country ?? '',
      variant: f.variant ?? '',
      ai: f.ai ?? false,
      camp: f.camp ?? 0,
    };
  }
  if (kind === 'event') return events()[index];
  return null;
}

function renderInspector() {
  const form = $('inspector-form');
  form.replaceChildren();
  $('btn-inspector-save').disabled = !selection;
  if (!selection) {
    $('inspector-title').textContent = '属性（点击表格或地图）';
    return;
  }
  const { kind, index } = selection;
  $('inspector-title').textContent = `${kind} #${index}`;
  const value = entityValue(kind, index);
  for (const [key, label, type] of FIELD_DEFS[kind] || []) {
    const wrap = document.createElement('label');
    wrap.textContent = label;
    const input = document.createElement('input');
    input.name = key;
    input.dataset.key = key;
    if (type === 'checkbox') {
      input.type = 'checkbox';
      input.checked = !!value[key];
    } else {
      input.type = type === 'number' ? 'number' : 'text';
      input.value = value[key] ?? '';
    }
    wrap.append(input);
    form.append(wrap);
  }
}

$('btn-inspector-save').onclick = async () => {
  if (!selection) return;
  const { kind, index } = selection;
  const fields = {};
  for (const input of $('inspector-form').querySelectorAll('input')) {
    const key = input.dataset.key;
    if (input.type === 'checkbox') fields[key] = input.checked;
    else if (input.type === 'number') fields[key] = input.value === '' ? null : Number(input.value);
    else fields[key] = input.value;
  }
  try {
    await postEdit({ type: kind, index, fields });
  } catch (err) {
    notice(err.message);
  }
};

function renderMap() {
  const grid = $('map-grid');
  grid.replaceChildren();
  const w = Number(mapSize().width) || 0;
  const h = Number(mapSize().height) || 0;
  if (!w || !h || w * h > 4000) {
    grid.textContent = w && h ? '地图过大，请用表格编辑' : '缺少 width/height';
    return;
  }
  grid.style.gridTemplateColumns = `repeat(${w}, 14px)`;
  const armyCells = new Set((layers().armies || []).map(a => a.cell));
  const siteCells = new Set((layers().sites || []).map(s => s.cell));
  for (let cell = 0; cell < w * h; cell++) {
    const div = document.createElement('div');
    div.className = 'map-cell';
    div.title = `(${cell % w},${Math.floor(cell / w)}) #${cell}`;
    if (armyCells.has(cell)) div.classList.add('has-army');
    if (siteCells.has(cell)) div.classList.add('has-site');
    if (selection && selection.kind === 'army' && layers().armies[selection.index]?.cell === cell) div.classList.add('selected');
    if (selection && selection.kind === 'site' && layers().sites[selection.index]?.cell === cell) div.classList.add('selected');
    div.onclick = () => {
      const ai = (layers().armies || []).findIndex(a => a.cell === cell);
      const si = (layers().sites || []).findIndex(s => s.cell === cell);
      if (ai >= 0) selectEntity('army', ai);
      else if (si >= 0) selectEntity('site', si);
      else notice(`格子 ${cell} 无部队/建筑`);
    };
    grid.append(div);
  }
}

function renderTable(containerId, kind, columns, rows) {
  const host = $(containerId);
  host.replaceChildren();
  const table = document.createElement('table');
  table.className = 'data-table';
  const thead = document.createElement('thead');
  thead.innerHTML = '<tr>' + columns.map(c => `<th>${c}</th>`).join('') + '</tr>';
  table.append(thead);
  const tbody = document.createElement('tbody');
  rows.forEach((row, index) => {
    const tr = document.createElement('tr');
    tr.dataset.kind = kind;
    tr.dataset.index = String(index);
    tr.innerHTML = row.map(c => `<td>${c}</td>`).join('');
    tr.onclick = () => selectEntity(kind, index);
    if (selection?.kind === kind && selection.index === index) tr.classList.add('selected');
    tbody.append(tr);
  });
  table.append(tbody);
  host.append(table);
}

function renderEditor() {
  renderMap();
  const armies = layers().armies || [];
  renderTable('tab-armies', 'army',
    ['#', 'cell', 'unit', 'grade', 'hp', 'cmd'],
    armies.map((a, i) => [i, a.cell, a.unit, a.grade, `${a.health}/${a.maximumHealth}`, a.commander]));
  const sites = layers().sites || [];
  renderTable('tab-sites', 'site',
    ['#', 'cell', 'city', '建筑', '等级'],
    sites.map((s, i) => [i, s.cell, s.cityId, s.buildingKind, s.buildingLevel]));
  const facs = factions();
  renderTable('tab-factions', 'faction',
    ['#', '国家', '金', '工', '粮', 'camp', 'AI'],
    facs.map((f, i) => {
      const r = factionResources(f);
      return [i, f.country, r.money, r.industry, r.food, f.camp, f.ai ? 'Y' : ''];
    }));
  const evs = events();
  renderTable('tab-events', 'event',
    ['#', 'id', 'round', 'cell', 'country'],
    evs.map((e, i) => [i, e.id, e.round, e.cell, e.country]));
  renderInspector();
}

document.querySelectorAll('#editor-tabs button').forEach(btn => {
  btn.onclick = () => {
    document.querySelectorAll('#editor-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    const tab = btn.dataset.tab;
    for (const id of ['map', 'armies', 'sites', 'factions', 'events']) {
      $(`tab-${id}`).hidden = tab !== id;
    }
  };
});

$('map-form').onsubmit = async e => {
  e.preventDefault();
  try {
    await postEdit({
      type: 'map',
      index: 0,
      fields: {
        mapId: Number($('meta-mapId').value),
        width: Number($('meta-width').value),
        height: Number($('meta-height').value),
      },
    });
  } catch (err) {
    notice(err.message);
  }
};

$('create-form').onsubmit = async e => {
  e.preventDefault();
  try {
    await api('/api/editor/workspaces', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: $('ws-id').value.trim(),
        name: $('ws-name').value.trim(),
        fromPackId: $('ws-from').value,
      }),
    });
    $('ws-id').value = '';
    await loadWorkspaces();
    notice('草稿已创建', false);
  } catch (err) {
    notice(err.message);
  }
};

$('btn-validate').onclick = async () => {
  try {
    const diag = await api(`/api/editor/workspaces/${currentWorkspace.id}/validate?level=SmokeStart`);
    $('diag').hidden = false;
    $('diag').textContent = JSON.stringify(diag, null, 2);
    const errors = diag.filter(d => d.severity === 'Error');
    notice(errors.length ? `校验失败：${errors.length} 个错误` : '校验通过', errors.length > 0);
  } catch (err) {
    notice(err.message);
  }
};

$('btn-export').onclick = async () => {
  try {
    const result = await api(`/api/editor/workspaces/${currentWorkspace.id}/export?level=SmokeStart`);
    $('diag').hidden = false;
    $('diag').textContent = JSON.stringify(result, null, 2);
    if (result.success && result.archivePath) {
      const name = result.archivePath.split(/[/\\]/).pop();
      const url = `/api/editor/workspaces/${currentWorkspace.id}/files/exports/${encodeURIComponent(name)}`;
      notice(`导出成功：${name}`, false);
      const a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.textContent = '下载 ' + name;
      $('diag').append('\n');
      $('diag').appendChild(a);
    } else notice('导出未成功，请查看诊断');
  } catch (err) {
    notice(err.message);
  }
};

$('btn-preview').onclick = async () => {
  try {
    await api(`/api/editor/workspaces/${currentWorkspace.id}/preview/enter`, { method: 'POST' });
    await loadPreviewStatus();
    notice('已进入预览：请打开游戏页试玩', false);
    window.open('index.html', '_blank');
  } catch (err) {
    notice(err.message);
  }
};

$('btn-preview-exit').onclick = async () => {
  try {
    await api('/api/editor/preview/exit', { method: 'POST' });
    await loadPreviewStatus();
    notice('已退出预览', false);
  } catch (err) {
    notice(err.message);
  }
};

$('btn-delete').onclick = async () => {
  if (!confirm(`删除草稿 ${currentWorkspace.id}？`)) return;
  try {
    await api(`/api/editor/workspaces/${currentWorkspace.id}`, { method: 'DELETE' });
    currentWorkspace = null;
    showWorkspacePanel(false);
    await loadWorkspaces();
    notice('已删除', false);
  } catch (err) {
    notice(err.message);
  }
};

$('btn-sc-back').onclick = () => { $('panel-scenario').hidden = true; };

void loadWorkspaces();
void loadPreviewStatus();
