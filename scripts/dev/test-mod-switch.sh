#!/usr/bin/env bash
# Quick HTTP smoke: activate each installed mod and verify catalog / HQ / start.
set -euo pipefail
BASE_URL="${EW4_TEST_URL:-http://127.0.0.1:5184}"

api() {
  local route="$1"
  local body="$2"
  if [ -z "$body" ]; then body='{}'; fi
  local q
  q=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))" "$body")
  curl -sf -X POST "${BASE_URL}/api/${route}?q=${q}"
}

activate_mod() {
  local key="$1"
  if [ -z "$key" ]; then
    api mods '{"op":"activate","key":""}'
  else
    api mods "{\"op\":\"activate\",\"key\":\"${key}\"}"
  fi
}

json_ok() {
  python3 -c "import json,sys; d=json.load(sys.stdin); ok=d.get('ok',d.get('Ok')); sys.exit(0 if ok else 1)"
}

echo "== mod switch smoke (${BASE_URL}) =="
for label in ww1 huangkai great-front; do
  echo "-- ${label}"
  activate_mod "$label" >/dev/null
  catalog=$(api catalog '{}')
  echo "$catalog" | json_ok || { echo "$catalog" | head -c 500; echo; exit 1; }
  echo "$catalog" | python3 -c "
import json, sys
d = json.load(sys.stdin)
label = sys.argv[1]
data = d['data']
sc = data['scenarios']
ch = data['chapters']
conq = [s for s in sc if s.get('mode') == 2]
camp = [s for s in sc if s.get('mode') == 1]
print(f'  scenarios={len(sc)} campaign={len(camp)} conquest={len(conq)} chapters={len(ch)}')
if label == 'ww1':
    assert any('1914' in s.get('name','') for s in conq), 'ww1 conquest names'
    assert len(sc) >= 70, 'ww1 scenario count'
if label == 'huangkai':
    assert len(sc) >= 40, 'huangkai scenarios'
    assert len(ch) >= 1, 'huangkai chapters'
if label == 'great-front':
    assert len(sc) >= 40, 'great-front scenarios'
" "$label"
  hq=$(api headquarters '{}')
  echo "$hq" | json_ok || { echo "$hq" | head -c 500; echo; exit 1; }
  echo "$hq" | python3 -c "
import json, sys
d = json.load(sys.stdin)['data']
label = sys.argv[1]
defs = d['definitions']
assert len(defs) > 100, 'HQ definitions'
sample = next(x for x in defs if x.get('skills'))
assert sample['skills'], 'commander skills'
print(f\"  HQ definitions={len(defs)} sample={sample['name']} skills={len(sample['skills'])}\")
" "$label"
  case "$label" in
    ww1) start_body='{"scenario":"conquest2","country":"fra"}' ;;
    huangkai|great-front) start_body='{"scenario":"campaign1_01"}' ;;
  esac
  start=$(api start "$start_body")
  echo "$start" | json_ok || { echo "$start" | head -c 500; echo; exit 1; }
  echo "$start" | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print(f\"  start ok scenario={d.get('scenario')} round={d.get('round')}\")"
done
echo "== all mod switches passed =="
