#!/usr/bin/env bash
# ============================================================
# enable_desktop_mode.sh — one-time setup for the XBXA01 desktop-mode controller
# ============================================================
# Puts the Pixel 9 into Android desktop mode on the external (glasses) display and
# points the user at the two toggles the controller needs. See DESKTOP_MODE.md.
#
# Desktop mode on Pixel shipped as a developer/experimental feature (Android 15 QPR /
# Android 16). These settings enable freeform windows and force desktop output on
# external displays; a reboot is required after the developer-options toggle.
#
# Usage:
#   tools/enable_desktop_mode.sh
# ============================================================
set -euo pipefail

if ! command -v adb >/dev/null 2>&1; then
  echo "[ERROR] adb not found on PATH." >&2
  exit 1
fi

if ! adb get-state >/dev/null 2>&1; then
  echo "[ERROR] No device. Plug in the Pixel 9 with USB debugging on." >&2
  exit 1
fi

echo "[INFO] Enabling freeform / desktop windowing…"
adb shell settings put global force_desktop_mode_on_external_displays 1 || true
adb shell settings put global enable_freeform_support 1 || true
adb shell settings put global development_settings_enabled 1 || true

echo
echo "[INFO] Current values:"
for k in force_desktop_mode_on_external_displays enable_freeform_support; do
  printf '  %-42s = %s\n' "$k" "$(adb shell settings get global "$k" | tr -d '\r')"
done

echo
echo "[NEXT] Finish the one-time setup on the phone:"
echo "  1. Developer options ▸ enable 'Enable freeform windows' (if present) ▸ REBOOT."
echo "  2. Accessibility ▸ enable 'XBXA01 Controller'  (pointer / gestures / window snap)."
echo "  3. Languages & input ▸ enable + select 'XBXA01 Keyboard'  (typing)."
echo
echo "The controller app deep-links to (2) and (3) from its setup banner, so you can"
echo "do those from inside the app too."

# Convenience deep-links (opens the right settings screens on the phone).
open_settings() {
  echo "[INFO] Opening: $1"
  adb shell am start -a "$2" >/dev/null 2>&1 || true
}
read -r -p "Open Accessibility settings now? [y/N] " a
[[ "${a:-N}" =~ ^[Yy]$ ]] && open_settings "Accessibility" "android.settings.ACCESSIBILITY_SETTINGS"

read -r -p "Open Input-method settings now? [y/N] " b
[[ "${b:-N}" =~ ^[Yy]$ ]] && open_settings "Input methods" "android.settings.INPUT_METHOD_SETTINGS"

echo "[INFO] Done."
