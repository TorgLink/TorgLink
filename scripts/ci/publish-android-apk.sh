#!/usr/bin/env bash
# Publish a signed TorgLink Android APK for GitHub Actions.
# Usage: publish-android-apk.sh <runtime-identifier> <configuration>
#
# Signing (always; unsigned APKs will not install on Android / BlueStacks):
#   All of ANDROID_KEYSTORE_BASE64, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD,
#   ANDROID_KEYSTORE_PASSWORD → release keystore from secrets.
#   Otherwise → scripts/android/ci-debug.keystore
#     alias androiddebugkey / password android (public CI debug key, not for Play Store).
#
# .NET 10 MAUI on CI often emits com.*.apk without applying AndroidKeyStore props
# (no *-Signed.apk). We always verify the APK and fall back to apksigner when needed.

set -euo pipefail

rid="${1:?usage: publish-android-apk.sh <runtime-identifier> <configuration>}"
configuration="${2:?usage: publish-android-apk.sh <runtime-identifier> <configuration>}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp="${RUNNER_TEMP:-/tmp}"
extra=()

if [[ -n "${ANDROID_KEYSTORE_BASE64:-}" && -n "${ANDROID_KEY_ALIAS:-}" && -n "${ANDROID_KEY_PASSWORD:-}" && -n "${ANDROID_KEYSTORE_PASSWORD:-}" ]]; then
  keystore="$tmp/torglink.keystore"
  echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > "$keystore"
  key_alias="$ANDROID_KEY_ALIAS"
  key_pass="$ANDROID_KEY_PASSWORD"
  store_pass="$ANDROID_KEYSTORE_PASSWORD"
  echo "Signing with release keystore from GitHub secrets."
else
  keystore="$repo_root/scripts/android/ci-debug.keystore"
  if [[ ! -f "$keystore" ]]; then
    echo "Missing CI debug keystore: $keystore" >&2
    exit 1
  fi
  key_alias=androiddebugkey
  key_pass=android
  store_pass=android
  echo "Release signing secrets incomplete; signing with scripts/android/ci-debug.keystore (installable, not for Play Store)."
fi

# env: prefix keeps passwords out of MSBuild command-line logs when honored.
export TORGLINK_ANDROID_KEY_PASS="$key_pass"
export TORGLINK_ANDROID_STORE_PASS="$store_pass"
extra+=(
  -p:AndroidKeyStore=true
  -p:AndroidSigningKeyStore="$keystore"
  -p:AndroidSigningKeyAlias="$key_alias"
  -p:AndroidSigningKeyPass=env:TORGLINK_ANDROID_KEY_PASS
  -p:AndroidSigningStorePass=env:TORGLINK_ANDROID_STORE_PASS
)

apk_has_signature() {
  local path="$1"
  python3 - "$path" <<'PY'
import sys
import zipfile
from pathlib import Path

path = Path(sys.argv[1])
data = path.read_bytes()
v2 = b"APK Sig Block 42" in data
with zipfile.ZipFile(path) as z:
    v1 = any(
        name.startswith("META-INF/") and name.endswith((".RSA", ".DSA", ".EC"))
        for name in z.namelist()
    )
sys.exit(0 if (v1 or v2) else 1)
PY
}

find_apksigner() {
  local sdk root bt
  for root in "${ANDROID_HOME:-}" "${ANDROID_SDK_ROOT:-}"; do
    [[ -n "$root" ]] || continue
    if [[ -x "$root/build-tools/35.0.0/apksigner" ]]; then
      echo "$root/build-tools/35.0.0/apksigner"
      return 0
    fi
    bt="$(ls -1d "$root"/build-tools/*/apksigner 2>/dev/null | sort -V | tail -n1 || true)"
    if [[ -n "$bt" && -x "$bt" ]]; then
      echo "$bt"
      return 0
    fi
  done
  if command -v apksigner >/dev/null 2>&1; then
    command -v apksigner
    return 0
  fi
  return 1
}

sign_apk_with_apksigner() {
  local apk="$1"
  local signer
  signer="$(find_apksigner)" || {
    echo "apksigner not found under ANDROID_HOME/ANDROID_SDK_ROOT; cannot sign APK." >&2
    exit 1
  }

  local signed="$tmp/torglink-signed.apk"
  rm -f "$signed"
  echo "MSBuild left an unsigned APK; signing with $signer"
  "$signer" sign \
    --ks "$keystore" \
    --ks-key-alias "$key_alias" \
    --ks-pass "pass:$store_pass" \
    --key-pass "pass:$key_pass" \
    --out "$signed" \
    "$apk"
  "$signer" verify --verbose "$signed"
  cp -f "$signed" "$apk"
}

# IncludeAndroid=true → ShortP2PBuildAndroid via src/Directory.Build.props.
# Windows Bluetooth is skipped: TorgLink.Maui omits the Windows TFM on non-Windows.
dotnet publish src/TorgLink.Maui/TorgLink.Maui.csproj \
  -c "$configuration" \
  -f net10.0-android \
  -p:IncludeAndroid=true \
  -p:RuntimeIdentifier="$rid" \
  -p:AndroidPackageFormat=apk \
  -p:AndroidPackageFormats=apk \
  "${extra[@]}"

mkdir -p dist/apk

# Prefer MSBuild's *-Signed.apk when present; otherwise any non-unsigned APK.
mapfile -t apks < <(
  {
    find src/TorgLink.Maui/bin -type f -name '*-Signed.apk'
    find src/TorgLink.Maui/bin -type f -name '*.apk' ! -name '*-Unsigned.apk' ! -name '*-unsigned.apk' ! -name '*-Signed.apk'
  } | awk 'NF && !seen[$0]++'
)

if [[ ${#apks[@]} -eq 0 ]]; then
  echo "No APK produced. Remaining APKs:" >&2
  find src/TorgLink.Maui/bin -type f -name '*.apk' -print >&2 || true
  exit 1
fi

dest="dist/apk/TorgLink-${rid}.apk"
cp "${apks[0]}" "$dest"
echo "Copied ${apks[0]} -> $dest"

if ! apk_has_signature "$dest"; then
  sign_apk_with_apksigner "$dest"
fi

if ! apk_has_signature "$dest"; then
  echo "ERROR: $dest has no APK signature (v1/v2/v3) after apksigner. Android will refuse install." >&2
  exit 1
fi

python3 - "$dest" <<'PY'
import sys
import zipfile
from pathlib import Path

path = Path(sys.argv[1])
data = path.read_bytes()
v2 = b"APK Sig Block 42" in data
with zipfile.ZipFile(path) as z:
    v1 = any(
        name.startswith("META-INF/") and name.endswith((".RSA", ".DSA", ".EC"))
        for name in z.namelist()
    )
print(f"APK signature OK: v1={v1} v2/v3={v2} ({path})")
PY
