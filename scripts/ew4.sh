#!/usr/bin/env bash
#
# EW4 开发唯一入口（宿主机只需 Docker，不装 .NET SDK）。
#
# 用法：
#   scripts/ew4.sh help
#   scripts/ew4.sh build | test | install | web | …
#
# 模块（scripts/dev/，仅供本脚本 source，勿直接运行）：
#   paths.sh                 仓库根、EW4_HOME、mods/user 路径、宿主机→/src 路径
#   docker.sh                构建 ew4-dev 镜像、在容器内执行 dotnet
#   cli.sh                   调用 Ew4.Tool：import-base、validate、smoke-start、check btl
#   install.sh               publish 到 install/app，同步 mods
#
# 环境变量：
#   EW4_HOME          安装根，默认 <repo>/install
#   EW4_DEV_IMAGE     开发镜像名，默认 ew4-dev:local
#   EW4_IMPORT_SEED   import-base 原料目录，默认 data/import-seed
#   PORT              web 监听端口，默认 5184
#   EW4_WEB_BIND      Kestrel 绑定地址，默认 0.0.0.0
#
# 典型流程：
#   1. scripts/ew4.sh build
#   2. 准备 mods/base（import-base 或手动放入仓库 mods/）；build/install/web 会同步到 install/mods/
#   3. scripts/ew4.sh web   → http://127.0.0.1:5184
#
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=dev/paths.sh
source "$SCRIPT_DIR/dev/paths.sh"
# shellcheck source=dev/docker.sh
source "$SCRIPT_DIR/dev/docker.sh"
# shellcheck source=dev/cli.sh
source "$SCRIPT_DIR/dev/cli.sh"
# shellcheck source=dev/install.sh
source "$SCRIPT_DIR/dev/install.sh"
ew4_usage() {
  cat <<'EOF'
用法: scripts/ew4.sh <命令> [参数…]

  build [args…]       在 Docker 内编译 Ew4.sln（输出到 build/<项目>/）
  install [Release]   publish → install/app
  test [args…]        dotnet test
  web                 开发态启动 Ew4.Backend.Web（--root 仓库 --home install/）
  import-base         Ew4.Tool：data/import-seed → mods/base
  sync-mods           将 mods/ 同步到 install/mods/（build/install/web 也会自动执行）
  validate smoke      Ew4.Tool：对内容包做 SmokeStart（默认 base）
  smoke-start         Ew4.Tool：单剧本开局烟测
  check btl           Ew4.Tool：BTL 往返校验（包内语料）
  check json-btl      G6：嵌入剧本 JSON → BTL 与参考语料逐字节一致
  fmt [--check]       dotnet format
  image dev|web       构建 Docker 镜像（dev 日常 / web 发行）
  clean               删除 src、tests 下散落的 bin/obj
  shell <cmd…>        在 dev 容器内执行任意命令
  dotnet <args…>      在 dev 容器内执行 dotnet

环境变量: EW4_HOME, EW4_DEV_IMAGE, EW4_IMPORT_SEED, PORT, EW4_WEB_BIND
详见脚本头部注释与 scripts/README.md
EOF
}

cmd_web() {
  local root home home_container port host_bind web
  root="$(ew4_repo_root)"
  home="$(ew4_home_dir "$root")"
  home_container="$(ew4_container_path "$root" "$home")"
  web="src/Ew4.Backend.Web/Ew4.Backend.Web.csproj"
  [[ -f "$root/$web" ]] || { echo "找不到 $web" >&2; exit 1; }
  port="${PORT:-5184}"
  host_bind="${EW4_WEB_BIND:-0.0.0.0}"
  ew4_ensure_dev_image
  mkdir -p "$home/mods" "$home/user"
  ew4_sync_repo_mods_to_install "$root"
  echo "Ew4.Backend.Web → http://127.0.0.1:${port}/" >&2
  echo "  EW4_HOME: $home" >&2
  echo "  mods:     $(ew4_install_mods_dir "$root") (来自 $(ew4_repo_mods_dir "$root"))" >&2
  echo "  user:     $(ew4_user_dir "$root")" >&2
  docker run --rm \
    -u "$(id -u):$(id -g)" \
    -e HOME=/tmp \
    -e EW4_HOME="$home_container" \
    -v "$root:/src" \
    -w /src \
    -p "${port}:${port}" \
    "$(ew4_dev_image)" \
    dotnet run --project "$web" -- \
    --root /src \
    --home "$home_container" \
    --urls "http://${host_bind}:${port}"
}

cmd_fmt() {
  ew4_docker_dotnet tool restore
  if [[ "${1:-}" == "--check" ]]; then
    ew4_docker_dotnet format whitespace --verify-no-changes
    ew4_docker_dotnet format style --verify-no-changes
    ew4_docker_dotnet format analyzers --verify-no-changes
  else
    ew4_docker_dotnet format
  fi
}

cmd_clean() {
  local root
  root="$(ew4_repo_root)"
  ew4_ensure_dev_image
  docker run --rm -u root -v "$root:/src" -w /src "$(ew4_dev_image)" \
    sh -c 'find /src/src /src/tests -type d \( -name obj -o -name bin \) -exec rm -rf {} + 2>/dev/null; chown -R "$(stat -c %u:%g /src)" /src/build /src/src /src/tests 2>/dev/null || true'
}

cmd_image_web() {
  local root tag
  root="$(ew4_repo_root)"
  tag="${1:-ew4-web:local}"
  exec docker build -t "$tag" -f "$root/.config/docker/Dockerfile" "$root"
}

main() {
  local cmd="${1:-help}"
  shift || true
  case "$cmd" in
    help|-h|--help) ew4_usage ;;
    build)
      ew4_docker_dotnet build Ew4.sln "$@"
      ew4_sync_repo_mods_to_install "$(ew4_repo_root)"
      ;;
    install) cmd_install "$@" ;;
    sync-mods) ew4_sync_repo_mods_to_install "$(ew4_repo_root)" ;;
    test) ew4_docker_dotnet test Ew4.sln "$@" ;;
    web|run-web) cmd_web "$@" ;;
    import-base) cmd_import_base "$@" ;;
    validate) cmd_validate "$@" ;;
    smoke-start) cmd_smoke_start "$@" ;;
    check) cmd_check "$@" ;;
    fmt|format) cmd_fmt "$@" ;;
    clean) cmd_clean ;;
    shell|run) ew4_docker_dev "$@" ;;
    dotnet) ew4_docker_dotnet "$@" ;;
    image)
      case "${1:-dev}" in
        dev) ew4_ensure_dev_image ;;
        web) shift; cmd_image_web "${1:-}" ;;
        *) echo "image dev|web" >&2; exit 2 ;;
      esac
      ;;
    *)
      echo "未知命令: $cmd" >&2
      ew4_usage >&2
      exit 2
      ;;
  esac
}

main "$@"
