#!/usr/bin/env bash
#
# 在 ew4-dev:local 容器内运行（由 scripts/dev/android.sh 调用，仓库挂载为 /src）。
# 输入：build/android/app（dotnet publish 结果）、install/apk/mods（要内置的 mod 由 EW4_APK_MODS 指定）
# 输出：install/apk/ew4.apk（debug 密钥签名，可直接侧载）
set -euo pipefail
cd /src
OUT=build/android
WORK=$OUT/work
ASSETS=$OUT/assets
ANDROID_JAR="$ANDROID_HOME/platforms/android-34/android.jar"
# 默认内置 install/apk/mods 下的全部 mod（即仓库 mods/ 的内容）；EW4_APK_MODS 可显式指定子集
MODS="${EW4_APK_MODS:-$(cd install/apk/mods && ls -d */ | tr -d / | tr '\n' ' ')}"
echo "[apk] 内置 mod：$MODS"

rm -rf "$WORK" "$ASSETS"
mkdir -p "$WORK/classes" "$WORK/dex" "$ASSETS" install/apk

echo "[apk] 打包运行时与内容包"
(cd "$OUT/app" && zip -q -0 -r "/src/$ASSETS/ew4-runtime.zip" .)
(cd install/apk/mods && zip -q -0 -r "/src/$ASSETS/ew4-mods.zip" $MODS)
date +%s > "$ASSETS/stamp.txt"

echo "[apk] aapt2 link"
aapt2 link -o "$WORK/base.apk" \
  --manifest src/Ew4.Android/AndroidManifest.xml \
  -I "$ANDROID_JAR" \
  --min-sdk-version 24 --target-sdk-version 28 \
  -A "$ASSETS"

echo "[apk] javac + d8"
javac -encoding UTF-8 --release 8 -Xlint:-options -classpath "$ANDROID_JAR" -d "$WORK/classes" $(find src/Ew4.Android/java -name '*.java')
d8 --lib "$ANDROID_JAR" --min-api 24 --output "$WORK/dex" $(find "$WORK/classes" -name '*.class')
(cd "$WORK/dex" && zip -q ../base.apk classes.dex)

echo "[apk] zipalign + sign"
zipalign -f -p 4 "$WORK/base.apk" "$WORK/aligned.apk"
KEYSTORE=$OUT/ew4-debug.keystore
if [[ ! -f "$KEYSTORE" ]]; then
  keytool -genkeypair -keystore "$KEYSTORE" -alias ew4 -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass android -keypass android -dname "CN=EW4 Debug" >/dev/null 2>&1
fi
apksigner sign --ks "$KEYSTORE" --ks-pass pass:android --key-pass pass:android --out install/apk/ew4.apk "$WORK/aligned.apk"
apksigner verify install/apk/ew4.apk
ls -lh install/apk/ew4.apk
