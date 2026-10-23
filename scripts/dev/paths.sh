# shellcheck shell=bash
#
# 路径解析（仅被 scripts/ew4.sh 通过 source 加载，不要直接执行）。
#
# 约定：
#   - 仓库根：含 Ew4.sln、src/ 的目录。
#   - 渠道安装根 EW4_HOME：默认 <仓库>/install/web 或 install/apk（各自含 app/、mods/、user/）。
#   - 内容包源码树：仓库根 mods/；运行时从各渠道 install/<channel>/mods/ 读（由 sync 拷贝）。
#   - 容器内仓库挂载为 /src；传给 dotnet 的路径须用 ew4_container_path 转成 /src/...。
#
# 函数：
#   ew4_repo_root                    → 仓库根绝对路径
#   ew4_install_channel              → web | apk（EW4_CHANNEL，默认 web）
#   ew4_channel_home_dir root ch      → install/<channel>
#   ew4_home_dir [root]              → EW4_HOME 或当前渠道安装根
#   ew4_web_home_dir / ew4_apk_home_dir
#   ew4_repo_mods_dir                → 仓库 mods/
#   ew4_install_mods_dir             → 当前渠道 EW4_HOME/mods
#   ew4_sync_repo_mods_to_install    → mods/ → install/web/mods 与 install/apk/mods
#   ew4_migrate_legacy_install_layout → 旧 install/{mods,user,app} → install/web/

ew4_repo_root() {
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd
}

ew4_install_channel() {
  local ch="${EW4_CHANNEL:-web}"
  case "$ch" in
    web|apk) printf '%s' "$ch" ;;
    *)
      echo "EW4_CHANNEL 须为 web 或 apk，当前: $ch" >&2
      return 1
      ;;
  esac
}

ew4_channel_home_dir() {
  local root="${1:?}" channel="${2:?}"
  printf '%s' "$root/install/$channel"
}

ew4_web_home_dir() {
  ew4_channel_home_dir "${1:-$(ew4_repo_root)}" web
}

ew4_apk_home_dir() {
  ew4_channel_home_dir "${1:-$(ew4_repo_root)}" apk
}

ew4_home_dir() {
  local root="${1:-$(ew4_repo_root)}"
  if [[ -n "${EW4_HOME:-}" ]]; then
    printf '%s' "$(cd "$EW4_HOME" && pwd)"
    return 0
  fi
  ew4_channel_home_dir "$root" "$(ew4_install_channel)"
}

ew4_repo_mods_dir() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$root/mods"
}

ew4_install_mods_dir() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$(ew4_home_dir "$root")/mods"
}

ew4_mods_dir() {
  ew4_install_mods_dir "${1:-}"
}

ew4_migrate_legacy_install_layout() {
  local root="${1:-$(ew4_repo_root)}"
  local web_home
  web_home="$(ew4_web_home_dir "$root")"
  mkdir -p "$web_home"

  if [[ -d "$root/install/mods" && ! -e "$web_home/mods" ]]; then
    echo "迁移 install/mods → install/web/mods" >&2
    mv "$root/install/mods" "$web_home/mods"
  fi
  if [[ -d "$root/install/user" && ! -e "$web_home/user" ]]; then
    echo "迁移 install/user → install/web/user" >&2
    mv "$root/install/user" "$web_home/user"
  fi
  if [[ -d "$root/install/app" && ! -e "$web_home/app" ]]; then
    local app_empty=1
    if [[ -n "$(ls -A "$root/install/app" 2>/dev/null)" ]]; then
      app_empty=0
    fi
    if [[ "$app_empty" -eq 0 ]]; then
      echo "迁移 install/app → install/web/app" >&2
      mv "$root/install/app" "$web_home/app"
    else
      rmdir "$root/install/app" 2>/dev/null || true
    fi
  fi
}

ew4_ensure_channel_runtime_dirs() {
  local home="${1:?}"
  mkdir -p "$home/app" "$home/mods" "$home/user"
}

ew4_sync_repo_mods_to_home() {
  local root="${1:?}" home="${2:?}"
  local src dst
  src="$(ew4_repo_mods_dir "$root")"
  dst="$home/mods"
  if [[ ! -d "$src" ]]; then
    return 0
  fi
  ew4_ensure_channel_runtime_dirs "$home"
  mkdir -p "$dst"
  cp -a "$src/." "$dst/"
}

# 参数：web | apk | all（默认 all）
ew4_sync_repo_mods_to_install() {
  local root="${1:-$(ew4_repo_root)}"
  local target="${2:-all}"
  ew4_migrate_legacy_install_layout "$root"

  case "$target" in
    web)
      ew4_sync_repo_mods_to_home "$root" "$(ew4_web_home_dir "$root")"
      ;;
    apk)
      ew4_sync_repo_mods_to_home "$root" "$(ew4_apk_home_dir "$root")"
      ;;
    all)
      ew4_sync_repo_mods_to_home "$root" "$(ew4_web_home_dir "$root")"
      ew4_sync_repo_mods_to_home "$root" "$(ew4_apk_home_dir "$root")"
      ;;
    *)
      echo "sync 目标须为 web、apk 或 all，当前: $target" >&2
      return 1
      ;;
  esac
}

ew4_user_dir() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$(ew4_home_dir "$root")/user"
}

ew4_state_dir() {
  ew4_user_dir "${1:-}"
}

ew4_container_path() {
  local root host
  root="$(cd "${1:?}" && pwd)"
  host="$(readlink -f "${2:?}" 2>/dev/null || realpath "${2:?}" 2>/dev/null || echo "${2:?}")"
  if [[ "$host" == "$root" ]]; then
    printf '/src'
    return 0
  fi
  if [[ "$host" == "$root"/* ]]; then
    printf '/src/%s' "${host#$root/}"
    return 0
  fi
  printf '%s' "$host"
}
