# shellcheck shell=bash
#
# Android 壳构建（仅被 scripts/ew4.sh source）。设计见 docs/task/plan/android-shell.md。
#
#   ew4_ensure_android_image   → 构建 ew4-android:local（JDK + Android 命令行工具）
#   ew4_build_apk              → publish linux-bionic-arm64 → 打包 → install/apk/ew4.apk
#
# 环境变量：EW4_APK_MODS  内置哪些 mod（空格分隔，默认 mods/ 下全部）

ew4_android_image() {
  printf '%s' "${EW4_ANDROID_IMAGE:-ew4-android:local}"
}

ew4_ensure_android_image() {
  local root image
  root="$(ew4_repo_root)"
  image="$(ew4_android_image)"
  if docker image inspect "$image" >/dev/null 2>&1; then
    return 0
  fi
  docker build -t "$image" -f "$root/.config/docker/Dockerfile.android" "$root"
}

# 官方 linux-bionic 运行时包（Mono）在真机上于 libcoreclr 初始化时 SIGSEGV；
# 改用 Termux 在 Android 上实际跑通的同版本（10.0.13）Mono 运行时，覆盖 Microsoft.NETCore.App 部分。
EW4_TERMUX_RUNTIME_URL="${EW4_TERMUX_RUNTIME_URL:-https://packages.termux.dev/apt/termux-main/pool/main/d/dotnet-runtime-10.0/dotnet-runtime-10.0_10.0.13_aarch64.deb}"

ew4_android_overlay_runtime() {
  local root cache deb dir
  root="$1"
  cache="$root/build/android/cache"
  deb="$cache/$(basename "$EW4_TERMUX_RUNTIME_URL")"
  dir="$cache/termux-runtime"
  mkdir -p "$cache"
  if [[ ! -f "$deb" ]]; then
    echo "[apk] 下载 Termux 运行时" >&2
    curl -sSfL -o "$deb" "$EW4_TERMUX_RUNTIME_URL"
  fi
  rm -rf "$dir" && mkdir -p "$dir"
  (cd "$dir" && ar x "$deb" && tar xf data.tar.*)
  local src
  src="$(find "$dir" -type d -path '*shared/Microsoft.NETCore.App/*' -maxdepth 14 | head -1)"
  [[ -n "$src" ]] || { echo "Termux 运行时目录未找到" >&2; return 1; }
  echo "[apk] 覆盖运行时：$src" >&2
  local f name
  for f in "$src"/*.so "$src"/*.dll; do
    name="$(basename "$f")"
    cp -f "$f" "$root/build/android/app/$name"
  done
  # 官方包里的 Mono 静态库对运行没用，删掉以减小体积
  rm -f "$root"/build/android/app/*.a
}

ew4_build_apk() {
  local root
  root="$(ew4_repo_root)"
  ew4_ensure_dev_image
  ew4_ensure_android_image
  ew4_sync_repo_mods_to_install "$root" apk

  echo "[apk] dotnet publish (linux-bionic-arm64)" >&2
  rm -rf "$root/build/android/app"
  ew4_docker_dotnet publish src/Ew4.Backend.Web/Ew4.Backend.Web.csproj \
    -c Release -r linux-bionic-arm64 -p:Ew4Android=true -o build/android/app

  ew4_android_overlay_runtime "$root"

  docker run --rm \
    -u "$(id -u):$(id -g)" \
    -e HOME=/tmp \
    -e EW4_APK_MODS="${EW4_APK_MODS:-}" \
    -v "$root:/src" \
    -w /src \
    "$(ew4_android_image)" \
    bash src/Ew4.Android/build.sh
  echo "APK: $root/install/apk/ew4.apk" >&2
}
