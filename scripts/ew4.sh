#!/usr/bin/env bash
#
# EW4 开发唯一入口（宿主机只需 Docker，不装 .NET SDK）。
#
# 用法：
#   scripts/ew4.sh help
#   scripts/ew4.sh build | test | install | web | …
#
# 模块（scripts/dev/，仅供本脚本 source，勿直接运行）：
#   paths.sh                 仓库根、渠道 install/web|apk、mods/user、宿主机→/src 路径
#   docker.sh                构建 ew4-dev 镜像、在容器内执行 dotnet
#   cli.sh                   调用 Ew4.Tool：import-base、validate、smoke-start
#   install.sh               publish → install/web/app，同步 mods 到 web/apk
#   android.sh               Android 壳（同 ew4-dev 镜像）：publish bionic → install/apk/ew4.apk
#
# 环境变量：
#   EW4_HOME          安装根（覆盖默认）；未设时 EW4_CHANNEL 决定 install/web 或 install/apk
#   EW4_CHANNEL       web | apk，默认 web
#   EW4_DEV_IMAGE     开发镜像名，默认 ew4-dev:local
#   EW4_IMPORT_SEED   import-base 原料目录，默认 data/import-seed
#   PORT              web 监听端口，默认 5184
#   EW4_WEB_BIND      Kestrel 绑定地址，默认 0.0.0.0
#
# 典型流程：
#   1. scripts/ew4.sh build
#   2. 准备 mods/base；build/install/web 会同步到 install/web/mods 与 install/apk/mods
#   3. scripts/ew4.sh web start | restart | stop | logs   → :5184（状态 .run/ew4-web.state）
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
# shellcheck source=dev/android.sh
source "$SCRIPT_DIR/dev/android.sh"
# shellcheck source=dev/web.sh
source "$SCRIPT_DIR/dev/web.sh"
ew4_usage() {
  cat <<'EOF'
用法: scripts/ew4.sh <命令> [参数…]

  build [args…]       在 Docker 内编译 Ew4.sln（输出到 build/<项目>/）
  install [Release]   publish → install/web/app
  apk                 Android 壳（内置后端 + WebView）→ install/apk/ew4.apk
  test [args…]        dotnet test
  front-test          在 Docker 内运行 Ew4.Front.Web Vitest 单测
  web [start|stop|restart|status|logs|fg]  开发 Web（默认 start 后台；状态 .run/ew4-web.state）
  import-base         Ew4.Tool：data/import-seed → mods/base
  sync-mods [web|apk|all]  mods/ → install/<channel>/mods（默认 all）
  validate smoke      Ew4.Tool：对内容包做 SmokeStart（默认 base）
  smoke-start         Ew4.Tool：单剧本开局烟测
  export-apk          Ew4.Tool：合并 Mod → 原版壳 APK（见 docs/task/plan/apk-export.md）
  fmt [--check]       dotnet format
  image dev|web       构建 Docker 镜像（dev 日常 / web 发行）
  clean               删除 src、tests 下散落的 bin/obj
  shell <cmd…>        在 dev 容器内执行任意命令
  dotnet <args…>      在 dev 容器内执行 dotnet

环境变量: EW4_HOME, EW4_CHANNEL, EW4_DEV_IMAGE, EW4_IMPORT_SEED, PORT, EW4_WEB_BIND
详见脚本头部注释与 scripts/README.md
EOF
}

cmd_front_test() {
  ew4_ensure_dev_image
  ew4_docker_dev bash -lc 'cd tests/Ew4.Test.Front && npm ci && npm test'
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
    apk) ew4_build_apk ;;
    sync-mods) ew4_sync_repo_mods_to_install "$(ew4_repo_root)" "${1:-all}" ;;
    test) ew4_docker_dotnet test Ew4.sln "$@" ;;
    front-test) cmd_front_test ;;
    web|run-web) cmd_web "$@" ;;
    import-base) cmd_import_base "$@" ;;
    validate) cmd_validate "$@" ;;
    smoke-start) cmd_smoke_start "$@" ;;
    export-apk) cmd_export_apk "$@" ;;
    fmt|format) cmd_fmt "$@" ;;
    clean) cmd_clean ;;
    shell|run) ew4_docker_dev "$@" ;;
    dotnet) ew4_docker_dotnet "$@" ;;
    image)
      case "${1:-dev}" in
        dev) ew4_build_dev_image ;;
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
