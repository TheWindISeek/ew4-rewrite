# shellcheck shell=bash
#
# 发布 Web 宿主到 install/web/app/（由 scripts/ew4.sh install 调用）。
#
#   dotnet publish Ew4.Backend.Web → install/web/app/
#
# 依赖：paths.sh（ew4_web_home_dir）、docker.sh（ew4_docker_dotnet）

cmd_install() {
  local root home web csproj config
  root="$(ew4_repo_root)"
  home="$(ew4_web_home_dir "$root")"
  web="src/Ew4.Backend.Web/Ew4.Backend.Web.csproj"
  csproj="$root/$web"
  [[ -f "$csproj" ]] || { echo "找不到 $web" >&2; return 1; }
  config="${1:-Release}"
  shift || true

  ew4_migrate_legacy_install_layout "$root"
  ew4_ensure_channel_runtime_dirs "$home"

  echo "publish → $home/app ($config)" >&2
  ew4_docker_dotnet publish "$web" -c "$config" -o "$(ew4_container_path "$root" "$home/app")" "$@"

  echo "sync mods/ → install/web/mods 与 install/apk/mods" >&2
  ew4_sync_repo_mods_to_install "$root" all

  ew4_write_web_launcher "$home"

  echo "Web 安装根: $home（EW4_HOME 或 EW4_CHANNEL=web）" >&2
  echo "APK 资产根: $(ew4_apk_home_dir "$root")（EW4_CHANNEL=apk）" >&2
  echo "仅 install/ 发行：运行 $home/run.sh" >&2
}

ew4_write_web_launcher() {
  local home="${1:?}"
  cat >"$home/run.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export EW4_HOME="$ROOT"
cd "$ROOT/app"
exec dotnet Ew4.Backend.Web.dll --home "$EW4_HOME" --urls "${EW4_URLS:-http://127.0.0.1:5184}" "$@"
EOF
  chmod +x "$home/run.sh"
}
