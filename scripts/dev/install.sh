# shellcheck shell=bash
#
# 发布 Web 宿主到安装根（由 scripts/ew4.sh install 调用）。
#
#   dotnet publish Ew4.Backend.Web → install/app/
#
# 依赖：paths.sh（ew4_home_dir）、docker.sh（ew4_docker_dotnet）

cmd_install() {
  local root home web csproj config
  root="$(ew4_repo_root)"
  home="$(ew4_home_dir "$root")"
  web="src/Ew4.Backend.Web/Ew4.Backend.Web.csproj"
  csproj="$root/$web"
  [[ -f "$csproj" ]] || { echo "找不到 $web" >&2; return 1; }
  config="${1:-Release}"
  shift || true

  mkdir -p "$home/app" "$home/mods" "$home/user"

  echo "publish → $home/app ($config)" >&2
  ew4_docker_dotnet publish "$web" -c "$config" -o "$(ew4_container_path "$root" "$home/app")" "$@"

  echo "sync mods/ → $home/mods" >&2
  ew4_sync_repo_mods_to_install "$root"

  echo "安装根: $home（内容包维护在仓库 mods/；web 使用 --home 或 EW4_HOME）" >&2
}
