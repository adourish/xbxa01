#!/usr/bin/env bash
# ============================================================
# launch_on_panel.sh — run an installed app on the XBXA01 AR panel
# ============================================================
# The app owns an *untrusted* VirtualDisplay, so it cannot launch other
# apps onto it itself (Android throws SecurityException — see SPEC §Panel
# Content Sources). The adb shell user is elevated enough to do it, so this
# script finds the app's virtual display id and starts the target app there.
# Its frames are then captured and shown on the main panel.
#
# Prereq: the XBXA01 AR app must already be running (it creates the display).
#
# Usage:
#   tools/launch_on_panel.sh <package>[/<activity>]
#
# Examples:
#   tools/launch_on_panel.sh com.android.settings
#   tools/launch_on_panel.sh com.android.chrome
#   tools/launch_on_panel.sh com.google.android.deskclock/.DeskClock
# ============================================================
set -euo pipefail

TARGET="${1:-}"
if [[ -z "$TARGET" ]]; then
  echo "usage: $0 <package>[/<activity>]" >&2
  exit 2
fi

# Find the app-owned virtual display by its unique name and pull the display id.
# dumpsys prints e.g.:  DisplayInfo{"xbxa01-appwindow", displayId 68, ...
# Isolate the "displayId N" that follows the xbxa01-appwindow name, then take N.
# (Extract 'displayId N' first — the display *name* contains "01", so a bare
# [0-9]+ match would wrongly grab that.)
DID="$(adb shell dumpsys display \
  | grep -oE '"xbxa01-appwindow", displayId [0-9]+' \
  | grep -oE 'displayId [0-9]+' \
  | grep -oE '[0-9]+' \
  | head -1 || true)"

if [[ -z "$DID" ]]; then
  echo "[ERROR] Could not find the 'xbxa01-appwindow' virtual display." >&2
  echo "        Is the XBXA01 AR app running? Launch it first, then retry." >&2
  exit 1
fi

echo "[INFO] Virtual display id: $DID"

if [[ "$TARGET" == */* ]]; then
  echo "[INFO] Launching component $TARGET on display $DID"
  adb shell am start --display "$DID" -n "$TARGET"
else
  echo "[INFO] Launching package $TARGET (main launcher activity) on display $DID"
  adb shell monkey -p "$TARGET" --pct-syskeys 0 --display "$DID" 1 >/dev/null 2>&1 \
    || adb shell am start --display "$DID" \
         -a android.intent.action.MAIN -c android.intent.category.LAUNCHER \
         "$(adb shell cmd package resolve-activity --brief "$TARGET" | tail -1)"
fi

echo "[INFO] Done. The app should now stream onto the main AR panel."
