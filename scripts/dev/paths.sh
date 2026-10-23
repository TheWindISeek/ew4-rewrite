# shellcheck shell=bash
#
# 路径解析（仅被 scripts/ew4.sh 通过 source 加载，不要直接执行）。
#
# 约定：
#   - 仓库根：含 Ew4.sln、src/ 的目录。
#   - 安装根 EW4_HOME：默认 <仓库>/install（app/、mods/、user/）。
#   - 内容包源码树：仓库根 mods/（维护、import-base、APK 导入落盘）；运行时从 install/mods/ 读（由 sync 拷贝）。
#   - 容器内仓库挂载为 /src；传给 dotnet 的路径须用 ew4_container_path 转成 /src/...。
#
# 函数：
#   ew4_repo_root              → 仓库根绝对路径
#   ew4_home_dir [root]        → 安装根（EW4_HOME 或 root/install）
#   ew4_repo_mods_dir [root]   → 仓库 mods/（内容包维护目录）
#   ew4_mods_dir [root]        → 同 ew4_install_mods_dir（运行时内容包）
#   ew4_install_mods_dir     → install/mods（EW4_HOME 下）
#   ew4_user_dir [root]        → 用户数据（install/user）
#   ew4_sync_repo_mods_to_install → cp -a mods/ → install/mods/
#   ew4_container_path root p  → 宿主机路径 → 容器内 /src/... 路径

ew4_repo_root() {
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd
}

ew4_home_dir() {
  local root="${1:-$(ew4_repo_root)}"
  if [[ -n "${EW4_HOME:-}" ]]; then
    printf '%s' "$(cd "$EW4_HOME" && pwd)"
    return 0
  fi
  printf '%s' "$root/install"
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

ew4_sync_repo_mods_to_install() {
  local root="${1:-$(ew4_repo_root)}"
  local src dst
  src="$(ew4_repo_mods_dir "$root")"
  dst="$(ew4_install_mods_dir "$root")"
  if [[ ! -d "$src" ]]; then
    return 0
  fi
  mkdir -p "$dst"
  cp -a "$src/." "$dst/"
}

ew4_user_dir() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$(ew4_home_dir "$root")/user"
}

# 兼容旧名（ew4.sh 与文档逐步改用 ew4_user_dir）
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
