# shellcheck shell=bash
#
# Docker 开发镜像与在容器内执行命令（仅被 scripts/ew4.sh source）。
#
# 宿主机不需要安装 .NET；build / test / dotnet run 都经此进容器。
# 镜像：EW4_DEV_IMAGE，默认 ew4-dev:local（见 .config/docker/Dockerfile.dev）。
#
# 函数：
#   ew4_dev_image           → 镜像名
#   ew4_ensure_dev_image    → 不存在则 docker compose build dev
#   ew4_docker_dev cmd…     → 在容器内执行任意命令（仓库挂载 /src）
#   ew4_docker_dotnet args… → 在容器内执行 dotnet …

ew4_dev_image() {
  printf '%s' "${EW4_DEV_IMAGE:-ew4-dev:local}"
}

ew4_ensure_dev_image() {
  local root image
  root="$(ew4_repo_root)"
  image="$(ew4_dev_image)"
  if docker image inspect "$image" >/dev/null 2>&1; then
    return 0
  fi
  if docker compose version >/dev/null 2>&1; then
    docker compose -f "$root/.config/docker/compose.yaml" build dev
    return
  fi
  if command -v docker-compose >/dev/null 2>&1; then
    docker-compose -f "$root/.config/docker/compose.yaml" build dev
    return
  fi
  docker build -t "$image" -f "$root/.config/docker/Dockerfile.dev" "$root"
}

ew4_docker_dev() {
  local root image
  root="$(ew4_repo_root)"
  ew4_ensure_dev_image
  image="$(ew4_dev_image)"
  docker run --rm \
    -u "$(id -u):$(id -g)" \
    -e HOME=/tmp \
    -v "$root:/src" \
    -w /src \
    "$image" \
    "$@"
}

ew4_docker_dotnet() {
  ew4_docker_dev dotnet "$@"
}
