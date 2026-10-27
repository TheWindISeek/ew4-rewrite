# shellcheck shell=bash
#
# 开发 Web 服务（Docker 后台 + 状态文件，供 ew4.sh web * 调用）。
#
# 状态：仓库根 .run/ew4-web.state（container_name、port）

ew4_web_state_file() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$root/.run/ew4-web.state"
}

ew4_web_container_name() {
  local port="${1:-${PORT:-5184}}"
  printf 'ew4-dev-web-%s' "$port"
}

ew4_web_log_file() {
  local root="${1:-$(ew4_repo_root)}"
  printf '%s' "$root/.run/ew4-web.log"
}

ew4_web_write_state() {
  local root="$1" name="$2" port="$3"
  mkdir -p "$root/.run"
  cat >"$(ew4_web_state_file "$root")" <<EOF
container_name=$name
port=$port
EOF
}

ew4_web_read_state() {
  local root="$1"
  local file
  file="$(ew4_web_state_file "$root")"
  [[ -f "$file" ]] || return 1
  # shellcheck disable=SC1090
  source "$file"
  [[ -n "${container_name:-}" ]] || return 1
}

ew4_web_container_running() {
  local name="$1"
  docker ps -q -f "name=^/${name}$" | grep -q .
}

ew4_web_container_exists() {
  local name="$1"
  docker ps -aq -f "name=^/${name}$" | grep -q .
}

ew4_web_prepare() {
  local root home home_container port host_bind web
  root="$(ew4_repo_root)"
  if [[ -n "${EW4_HOME:-}" ]]; then
    home="$(ew4_home_dir "$root")"
  else
    home="$(ew4_web_home_dir "$root")"
  fi
  home_container="$(ew4_container_path "$root" "$home")"
  web="src/Ew4.Backend.Web/Ew4.Backend.Web.csproj"
  [[ -f "$root/$web" ]] || { echo "找不到 $web" >&2; exit 1; }
  port="${PORT:-5184}"
  host_bind="${EW4_WEB_BIND:-0.0.0.0}"
  ew4_ensure_dev_image
  ew4_migrate_legacy_install_layout "$root"
  ew4_ensure_channel_runtime_dirs "$home"
  ew4_sync_repo_mods_to_install "$root" web
  REPO_ROOT="$root"
  WEB_HOME="$home"
  WEB_HOME_CONTAINER="$home_container"
  WEB_PROJECT="$web"
  WEB_PORT="$port"
  WEB_BIND="$host_bind"
  WEB_CONTAINER="$(ew4_web_container_name "$port")"
  WEB_IMAGE="$(ew4_dev_image)"
}

ew4_web_print_info() {
  echo "Ew4.Backend.Web → http://127.0.0.1:${WEB_PORT}/" >&2
  echo "  EW4_HOME: $WEB_HOME" >&2
  echo "  mods:     $(ew4_install_mods_dir "$REPO_ROOT") (来自 $(ew4_repo_mods_dir "$REPO_ROOT"))" >&2
  echo "  user:     $(ew4_user_dir "$REPO_ROOT")" >&2
}

ew4_web_docker_run_args() {
  printf '%s\n' \
    -u "$(id -u):$(id -g)" \
    -e HOME=/tmp \
    -e "EW4_HOME=$WEB_HOME_CONTAINER" \
    -v "$REPO_ROOT:/src" \
    -w /src \
    -p "${WEB_PORT}:${WEB_PORT}"
}

ew4_web_stop() {
  local root="${1:-$(ew4_repo_root)}" name port
  if ew4_web_read_state "$root"; then
    name="$container_name"
    port="${port:-${PORT:-5184}}"
  else
    port="${PORT:-5184}"
    name="$(ew4_web_container_name "$port")"
  fi
  if ew4_web_container_exists "$name"; then
    echo "停止 Web 容器: $name" >&2
    docker stop "$name" >/dev/null 2>&1 || true
    docker rm "$name" >/dev/null 2>&1 || true
  else
    echo "未发现运行中的 Web 容器 ($name)" >&2
  fi
  rm -f "$(ew4_web_state_file "$root")"
}

ew4_web_status() {
  local root="${1:-$(ew4_repo_root)}" name port
  port="${PORT:-5184}"
  if ew4_web_read_state "$root"; then
    name="$container_name"
    port="${port:-$WEB_PORT}"
  else
    name="$(ew4_web_container_name "$port")"
  fi
  if ew4_web_container_running "$name"; then
    echo "运行中: $name (http://127.0.0.1:${port}/)" >&2
    docker ps -f "name=^/${name}$" --format '  容器 ID {{.ID}}  启动于 {{.RunningFor}}' >&2
    echo "  状态文件: $(ew4_web_state_file "$root")" >&2
    return 0
  fi
  if ew4_web_container_exists "$name"; then
    echo "已停止（容器仍存在）: $name — 可执行 scripts/ew4.sh web restart" >&2
    return 1
  fi
  echo "未运行 (期望容器名 $name)" >&2
  return 1
}

ew4_web_start() {
  local root log_file
  ew4_web_prepare
  root="$REPO_ROOT"
  log_file="$(ew4_web_log_file "$root")"
  mkdir -p "$root/.run"

  if ew4_web_container_running "$WEB_CONTAINER"; then
    ew4_web_write_state "$root" "$WEB_CONTAINER" "$WEB_PORT"
    echo "Web 已在运行: $WEB_CONTAINER" >&2
    ew4_web_print_info
    echo "  查看日志: scripts/ew4.sh web logs" >&2
    echo "  重启:     scripts/ew4.sh web restart" >&2
    return 0
  fi

  if ew4_web_container_exists "$WEB_CONTAINER"; then
    docker rm "$WEB_CONTAINER" >/dev/null 2>&1 || true
  fi

  ew4_web_print_info
  echo "  容器:     $WEB_CONTAINER" >&2
  echo "  日志:     $log_file" >&2

  if ! docker run -d --name "$WEB_CONTAINER" \
    $(ew4_web_docker_run_args) \
    "$WEB_IMAGE" \
    dotnet run --project "$WEB_PROJECT" -- \
    --root /src \
    --home "$WEB_HOME_CONTAINER" \
    --urls "http://${WEB_BIND}:${WEB_PORT}"; then
    echo "启动失败，见: scripts/ew4.sh web logs" >&2
    return 1
  fi

  ew4_web_write_state "$root" "$WEB_CONTAINER" "$WEB_PORT"
  echo "等待 Kestrel 就绪（首次 dotnet run 可能需 30–90 秒）…" >&2
  local i=0
  while ((i < 120)); do
    if curl -fsS -o /dev/null --connect-timeout 1 "http://127.0.0.1:${WEB_PORT}/" 2>/dev/null; then
      echo "已就绪: http://127.0.0.1:${WEB_PORT}/" >&2
      echo "日志: scripts/ew4.sh web logs -f | 重启: scripts/ew4.sh web restart" >&2
      return 0
    fi
    sleep 1
    i=$((i + 1))
  done
  echo "容器已启动但端口未响应，请执行: scripts/ew4.sh web logs -f" >&2
  return 1
}

ew4_web_restart() {
  ew4_web_stop "$(ew4_repo_root)"
  ew4_web_start
}

ew4_web_logs() {
  local root name follow="${1:-}"
  root="$(ew4_repo_root)"
  if ew4_web_read_state "$root"; then
    name="$container_name"
  else
    name="$(ew4_web_container_name "${PORT:-5184}")"
  fi
  if ew4_web_container_running "$name"; then
    if [[ "$follow" == "-f" || "$follow" == "follow" ]]; then
      exec docker logs -f "$name"
    fi
    docker logs --tail 80 "$name" 2>&1
    if curl -fsS -o /dev/null --connect-timeout 1 "http://127.0.0.1:${PORT:-5184}/" 2>/dev/null; then
      echo "" >&2
      echo "HTTP 正常: http://127.0.0.1:${PORT:-5184}/" >&2
    else
      echo "" >&2
      echo "容器在跑但端口未响应，可能仍在 dotnet 编译；请: scripts/ew4.sh web logs -f" >&2
    fi
    return 0
  fi
  local log_file
  log_file="$(ew4_web_log_file "$root")"
  if [[ -f "$log_file" ]]; then
    tail -n 80 "$log_file"
    return 0
  fi
  echo "无运行中容器且找不到日志: $log_file" >&2
  return 1
}

ew4_web_fg() {
  ew4_web_prepare
  ew4_web_print_info
  echo "前台运行（Ctrl+C 结束）。后台请用: scripts/ew4.sh web start" >&2
  # shellcheck disable=SC2046
  exec docker run --rm \
    $(ew4_web_docker_run_args) \
    "$WEB_IMAGE" \
    dotnet run --project "$WEB_PROJECT" -- \
    --root /src \
    --home "$WEB_HOME_CONTAINER" \
    --urls "http://${WEB_BIND}:${WEB_PORT}"
}

cmd_web() {
  local sub="${1:-start}"
  shift || true
  case "$sub" in
    start) ew4_web_start ;;
    stop) ew4_web_stop ;;
    restart) ew4_web_restart ;;
    status) ew4_web_status ;;
    logs) ew4_web_logs "$@" ;;
    fg|run|foreground) ew4_web_fg ;;
    -h|--help|help)
      cat <<'EOF'
用法: scripts/ew4.sh web [子命令]

  start（默认）  后台启动并写入 .run/ew4-web.state
  stop           停止并删除记录中的容器
  restart        stop + start（改代码后常用）
  status         是否运行、容器名与端口
  logs [-f]      查看 docker 日志（-f 跟随）
  fg             前台运行（旧行为，不写状态文件）

环境变量: PORT, EW4_HOME, EW4_WEB_BIND
EOF
      ;;
    *)
      echo "未知 web 子命令: $sub（可用 start|stop|restart|status|logs|fg）" >&2
      return 2
      ;;
  esac
}
