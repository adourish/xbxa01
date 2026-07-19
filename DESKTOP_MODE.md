# XBXA01 — Desktop Mode Redesign

> **Status:** Design + scaffolding (this branch).
> **Supersedes** the world-locked multi-panel renderer described in `SPEC.md` as the
> *primary* experience. The old renderer is retained as the **mirror-mode fallback**
> (see [§9](#9-relationship-to-the-old-renderer)).

## 0. Why this pivot

The world-locked panel renderer (`SPEC.md`) had two usability blockers:

1. **Blind gestures.** All control was invisible finger gestures on a mirrored phone
   screen (double-tap = swap, raycast-drag = move, pinch = scale). In glasses you
   cannot see your finger, so nothing is discoverable and everything is fiddly.
2. **Apps couldn't get onto the screens on their own.** Real apps only appeared when
   an *elevated* context (`adb shell am start --display`) launched them onto our
   untrusted `VirtualDisplay`. A sideloaded app is denied that by Android
   (`SecurityException`), so the self-contained story never worked (`SPEC.md`
   §Panel Content Sources, "Known limits").

**Desktop mode removes both problems at the source.** When the phone drives the
glasses as an *extended* display in **Android desktop mode**, Android itself renders a
real desktop with freeform, resizable windows, and Android itself owns launching apps
onto that display. We stop trying to be a window manager. The app's only job becomes
**input**: the phone screen turns into a **trackpad + buttons + on-screen keyboard**
that drives the desktop — the DeX model.

```
        BEFORE (SPEC.md)                         AFTER (this design)
  ┌───────────────────────────┐          ┌───────────────────────────┐
  │ Glasses (mirror)          │          │ Glasses (EXTENDED)        │
  │  Unity world-locked panels│          │  Android desktop mode:    │
  │  ← we render everything    │          │  real freeform app windows│
  └───────────────────────────┘          │  ← Android renders it     │
  ┌───────────────────────────┐          └───────────────────────────┘
  │ Phone (same mirrored image)│          ┌───────────────────────────┐
  │  invisible gestures        │          │ Phone = TRACKPAD          │
  └───────────────────────────┘          │  cursor pad · buttons · kbd│
                                          └───────────────────────────┘
```

---

## 1. Target experience (user journey)

1. Connect glasses to the Pixel 9 (USB-C DP Alt Mode) with desktop mode enabled
   (one-time setup, [§4](#4-enabling-desktop-mode-on-the-pixel-9)).
2. The glasses show the **Android desktop** — wallpaper, a taskbar, freeform windows.
3. Open **XBXA01 Controller** on the phone. The phone screen becomes a **trackpad**:
   - a large touch area moves an on-screen **cursor** on the desktop;
   - a **button bar** gives Home / Back / Recents / window snapping / layout presets;
   - a **Keyboard** button raises a soft keyboard that types into the focused window.
4. Move real apps between windows, snap them into a layout preset, and type — all
   from the phone, while looking at the desktop in the glasses.

The phone is never mirrored to the glasses in this mode; it is a dedicated control
surface, so what you touch is decoupled from what you see. That is the whole point.

---

## 2. Architecture

```
Pixel 9 (Android 15/16, desktop mode ON)
│
├── EXTERNAL DISPLAY (glasses)  ──►  Android Desktop (freeform windows)
│        ▲                                   ▲
│        │ renders                            │ acts on
│        └── Android WindowManager            │
│                                             │
└── PHONE DISPLAY  ──►  XBXA01 Controller (Unity app, this repo)
         │
         ├── TrackpadController.cs   touch → cursor deltas, taps, scroll, drag
         ├── ControllerUI.cs         button bar → global actions + presets
         ├── WindowLayoutManager.cs  3–4 window arrangement presets
         └── DesktopBridge.cs ──JNI──► DesktopController.java
                                          │
                                          ├── XbxAccessibilityService  (pointer, gestures,
                                          │      global actions, window snap, soft cursor)
                                          └── XbxKeyboardService (IME)   (keystrokes → focused field)
```

The Unity app renders **only** the controller UI on the phone. It never renders the
desktop. Two Android services do the actual driving; both are ordinary components an
end user enables once in Settings (no root, no system signing for the MVP).

---

## 3. Input path — recommendation and the honest limits

**Recommendation (chosen): AccessibilityService for pointer/gestures/actions +
InputMethodService (IME) for the keyboard.** This is the only path a *sideloaded*
(non-system-signed) app can take that controls arbitrary OS windows.

| Need | Mechanism | Confidence |
|------|-----------|-----------|
| Move a cursor | Soft cursor overlay drawn by the a11y service (`TYPE_ACCESSIBILITY_OVERLAY`, no extra permission); trackpad deltas move it | Medium — see caveat C1 |
| Click / long-press / drag / scroll | `AccessibilityService.dispatchGesture(...)` at the cursor point | Medium — see caveat C2 |
| Home / Back / Recents / Notifications / Split | `performGlobalAction(...)` | High |
| Move/snap a window into a layout region | Global split actions + drag gestures on the title bar | Medium |
| Type into a field | **IME**: `InputConnection.commitText()` / `sendKeyEvent()` | High |

### Caveats we are NOT hiding

- **C1 — No public "move the system pointer" API.** A sideloaded app cannot move
  Android's real mouse cursor. We draw our **own** cursor overlay and dispatch
  gestures at *its* coordinates. It looks like a cursor; it is our sprite plus a
  synthetic tap, not the OS pointer.
- **C2 — Cross-display gesture dispatch is the risk area.** `dispatchGesture` does
  not take a `displayId`; historically it targets the display the service is bound
  to. Driving gestures onto the *external* desktop display from an app on the *phone*
  display is the part most likely to need device-specific work (creating the a11y
  overlay + gesture context against the external display via `createDisplayContext`).
  This is called out as the #1 thing to verify on-device.
- **C3 — Fallbacks if C1/C2 don't fully land on this device:**
  1. **Physical BT mouse + keyboard** paired to the phone — desktop mode already
     accepts these natively; the controller app then only adds the layout-preset
     buttons. Zero injection risk.
  2. **adb-tethered** control (`tools/`), same tradeoff as the old renderer.
  3. **System-signed build** with `INJECT_EVENTS` — full fidelity, needs a custom
     ROM / OEM key. Out of scope for sideload.

The design deliberately keeps the **button bar** (global actions + IME) working even
if the free-cursor trackpad (C1/C2) needs tuning — global actions and the IME are
high-confidence, so the app is useful on day one regardless.

---

## 4. Enabling desktop mode on the Pixel 9

Desktop mode on Pixel shipped as a developer/experimental feature (Android 15 QPR /
Android 16). One-time setup, scripted in `tools/enable_desktop_mode.sh`:

```bash
# Freeform windows + force desktop on external displays
adb shell settings put global force_desktop_mode_on_external_displays 1
adb shell settings put global enable_freeform_support 1
# (Android 16) desktop windowing feature flags, if present on the build:
adb shell settings put global development_settings_enabled 1
# Then: Settings ▸ System ▸ Developer options ▸ "Enable freeform windows" ▸ reboot
```

`DisplayDetector` already distinguishes `MirrorToGlasses` from `ExtendedToGlasses`.
When desktop mode is on and the glasses are connected, it reports
`ExtendedToGlasses`; the controller UI activates. If it still reports
`MirrorToGlasses`, desktop mode isn't active and the app shows setup guidance and
offers the legacy renderer ([§9](#9-relationship-to-the-old-renderer)).

> **Target apps must be resizable** to behave in freeform. That is the global
> `enable_freeform_support` setting above; individual apps that hard-set
> `resizeableActivity="false"` will letterbox. Our own controller activity is now
> `resizeableActivity="true"`.

---

## 5. The controller UI (phone screen)

```
┌───────────────────────────────────────────────────────────┐
│  ● tracking   ⌂ Home   ‹ Back   ▚ Recents      [ Keyboard ]│  ← status + global actions
├───────────────────────────────────────────────────────────┤
│                                                           │
│                                                           │
│                   T R A C K P A D                         │  ← 1-finger: move cursor
│              (drag = cursor · tap = click)                │     tap: click
│              (2-finger drag = scroll)                     │     long-press+drag: window drag
│              (long-press = grab window)                   │     2-finger drag: scroll
│                                                           │
├───────────────────────────────────────────────────────────┤
│  Layouts:  [ Focus ]  [ Duo ]  [ Trio ]  [ Main+Side ]    │  ← 3–4 window presets (§6)
│  Window:   ◀ snap-L   ▲ max   ▼ restore   snap-R ▶        │
└───────────────────────────────────────────────────────────┘
```

Gesture map (implemented in `TrackpadController.cs`):

| Touch | Action |
|-------|--------|
| 1-finger drag | move cursor (delta × sensitivity) |
| 1-finger tap | click at cursor |
| 1-finger double-tap | double-click |
| long-press then drag | grab & drag the window under the cursor |
| 2-finger drag | scroll |
| 2-finger pinch | (reserved) zoom in supported apps |

Buttons are real, labelled, on-screen targets — the fix for "blind gestures". You
look at the desktop; your thumb finds a button by position without needing to see the
phone.

---

## 6. Window layout presets (the "3–4 configuration options")

`WindowLayoutManager.cs` defines four presets. Each preset is a set of target regions
on the desktop; applying a preset snaps the current windows into those regions (via
Android split/snap actions + title-bar drag gestures).

| Preset | Regions | Use |
|--------|---------|-----|
| **Focus** | 1 window maximized | single-task, biggest FOV |
| **Duo** | 2 windows, left / right halves | compare / reference + work |
| **Trio** | 3 columns (⅓ each) | chat · editor · browser |
| **Main + Side** | 1 large left (⅔) + 1 stacked right (⅓) | primary app + a helper |

Presets are the successor to the old Main/PiP swap: instead of two fixed panels you
pick an arrangement, and the apps inside are **real Android apps**, moved between
slots with the snap-L / snap-R buttons. This is exactly "move my real apps onto
different screens", expressed as freeform windows Android already knows how to manage.

Regions are defined in normalized display coordinates so they scale to the glasses'
1920×1080. See `WindowLayoutManager.LayoutPreset` for the rects.

---

## 7. Keyboard

The **Keyboard** button raises `XbxKeyboardService`, an `InputMethodService`. Once
enabled and selected (one-time, Settings ▸ System ▸ Languages & input), it types into
whatever text field is focused on the desktop — across displays and apps — because an
IME talks to the field through `InputConnection`, which is display-agnostic. This is
the one fully-supported way for a sideloaded app to send keystrokes to other apps.

For the MVP the controller offers a simple text bar that commits through the IME; a
full soft-keyboard layout is a straightforward follow-up. Special keys (Enter, Tab,
arrows, Esc) are sent as `KeyEvent`s via the IME connection.

---

## 8. Files in this change

| File | Role |
|------|------|
| `Assets/Plugins/Android/XbxAccessibilityService.java` | pointer, gestures, global actions, window snap, soft cursor |
| `Assets/Plugins/Android/XbxKeyboardService.java` | IME: keystrokes → focused field |
| `Assets/Plugins/Android/DesktopController.java` | JNI facade Unity calls; talks to the running services |
| `Assets/Plugins/Android/AndroidManifest.xml` | registers the two services, resizeable activity, permissions |
| `Assets/Plugins/Android/res/xml/xbx_accessibility_config.xml` | accessibility service config |
| `Assets/Plugins/Android/res/xml/xbx_ime_method.xml` | IME subtype config |
| `Assets/Scripts/Desktop/DesktopBridge.cs` | C# wrapper over `DesktopController` |
| `Assets/Scripts/Desktop/WindowLayoutManager.cs` | the 4 layout presets |
| `Assets/Scripts/Desktop/ControllerUI.cs` | button bar → bridge + presets |
| `Assets/Scripts/Input/TrackpadController.cs` | trackpad touch → cursor / click / scroll / drag |
| `Assets/Editor/ControllerSceneBuilder.cs` | generates the phone-side controller scene |
| `tools/enable_desktop_mode.sh` | one-time adb setup for desktop mode + services |

---

## 9. Relationship to the old renderer

Nothing is deleted. The world-locked renderer (`SPEC.md`) stays as the
**mirror-mode fallback**: if `DisplayDetector` reports `MirrorToGlasses` (desktop mode
off, or an OS/device that can't do it), the app can still run the old panels. The
build's default scene becomes the **controller** scene; `SceneBuilder` (the legacy
panel scene) is still there and rebuildable via its menu item.

Decision recorded: on a device where desktop mode is available, the controller is the
product; the renderer is the compatibility path.

---

## 10. Open items to verify on-device

1. **C2** — can the a11y service dispatch gestures onto the *external* desktop
   display from an app running on the phone display? (createDisplayContext route.)
2. Does `force_desktop_mode_on_external_displays` produce freeform windows on this
   Pixel 9 build, or only a scaled desktop?
3. Soft-cursor overlay latency at 1920×1080 / 120 Hz.
4. IME switching UX — can we deep-link the user to enable + select the IME, or does it
   require manual Settings navigation each session?
