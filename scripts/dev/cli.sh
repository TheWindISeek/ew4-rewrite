# shellcheck shell=bash
#
# Ew4.Tool 命令的 shell 封装（由 scripts/ew4.sh 的 import-base / validate / smoke-start 调用）。
#
# 均在 Docker 容器内 dotnet run Ew4.Tool；内容包目录来自 ew4_repo_mods_dir（仓库 mods/）。
# import-base 原料：EW4_IMPORT_SEED 或仓库下 data/import-seed。
#
# 函数：
#   cmd_import_base [cli args…]
#   cmd_validate <smoke|schema|reference|constraint> [--pack id] [--packs dir]
#   cmd_smoke_start [--scenario id] [--packs dir]

readonly EW4_TOOL_PROJECT="src/Ew4.Tool/Ew4.Tool.csproj"

ew4_resolve_import_seed() {
  local root="$1"
  if [[ -n "${EW4_IMPORT_SEED:-}" ]]; then
    printf '%s' "$(cd "$EW4_IMPORT_SEED" && pwd)"
    return 0
  fi
  if [[ -d "$root/data/import-seed" ]]; then
    printf '%s' "$root/data/import-seed"
    return 0
  fi
  echo "找不到 import 原料目录（设置 EW4_IMPORT_SEED，或准备 data/import-seed）" >&2
  return 1
}

cmd_import_base() {
  local root data dest
  root="$(ew4_repo_root)"
  data="$(ew4_resolve_import_seed "$root")" || return 1
  dest="$(ew4_repo_mods_dir "$root")/base"
  data="$(ew4_container_path "$root" "$data")"
  dest="$(ew4_container_path "$root" "$dest")"
  echo "import-base: $data → $dest" >&2
  ew4_docker_dotnet run --project "$EW4_TOOL_PROJECT" -- \
    import-base --data "$data" --dest "$dest" "$@"
}

cmd_smoke_start() {
  local root packs scenario
  root="$(ew4_repo_root)"
  packs="$(ew4_repo_mods_dir "$root")"
  scenario="campaign1_01"
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --scenario)
        scenario="$2"
        shift 2
        ;;
      --packs)
        packs="$2"
        shift 2
        ;;
      *)
        echo "smoke-start: 未知参数 $1" >&2
        return 2
        ;;
    esac
  done
  packs="$(ew4_container_path "$root" "$packs")"
  ew4_docker_dotnet run --project "$EW4_TOOL_PROJECT" -- \
    smoke-start --packs "$packs" --scenario "$scenario"
}

cmd_validate() {
  local root packs level sub pack_id
  root="$(ew4_repo_root)"
  packs="$(ew4_repo_mods_dir "$root")"
  pack_id="base"
  sub="${1:-smoke}"
  shift || true
  case "$sub" in
    smoke|smokestart) level=smoke ;;
    schema) level=schema ;;
    reference) level=reference ;;
    constraint) level=constraint ;;
    *)
      echo "validate: 未知级别 «$sub»（schema | reference | constraint | smoke）" >&2
      return 2
      ;;
  esac
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --pack)
        pack_id="$2"
        shift 2
        ;;
      --packs)
        packs="$2"
        shift 2
        ;;
      *)
        echo "validate: 未知参数 $1" >&2
        return 2
        ;;
    esac
  done
  packs="$(ew4_container_path "$root" "$packs")"
  ew4_docker_dotnet run --project "$EW4_TOOL_PROJECT" -- \
    validate-mod --packs "$packs" --pack "$pack_id" --level "$level"
}

cmd_export_apk() {
  local root
  root="$(ew4_repo_root)"
  ew4_docker_dotnet run --project "$EW4_TOOL_PROJECT" -- export-apk "$@"
}
