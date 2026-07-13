# XBXA01 Project Handoff

**Project:** Unity Android AR App for XREAL xbx a01 glasses
**Goal:** Picture-in-picture (PiP) with 3DoF head tracking
**Status:** Planning / Pre-development
**Last Updated:** 2026-07-12
**Host Device:** Google Pixel 9

---

## Device: XREAL xbx a01 (Model ID: XBXA01)

> **Sub-brand:** xbx (X by XREAL) — budget-tier line launched mid-2026

### Key Specs

| Attribute | Detail |
|---|---|
| **Display** | SeeYa 0.6" Micro OLED |
| **Resolution** | 1920×1080 per eye (3840×1080 side-by-side 3D mode) |
| **Refresh Rate** | Up to 120Hz |
| **Field of View** | 50° |
| **Virtual Screen Size** | ~147" at 4m |
| **Brightness** | 1,600 nits (eye-level) / 6,000 nits (panel) |
| **Contrast** | 2,000,000:1 |
| **Color Gamut** | sRGB 145% |
| **HDR** | HDR10 + AI-HDR (SDR→HDR upscaling) |
| **PWM** | 3840Hz (flicker-free) |
| **Weight** | 62g (56g without front frame) |
| **IPD** | 54.5–74.5mm adjustable |
| **Connection** | USB-C DisplayPort Alt Mode |
| **Power** | 5V/1A from host — no internal battery |
| **Audio** | Built-in stereo speakers, 4 modes |
| **Certifications** | TUV Rheinland Eye Comfort 5-Star |
| **Price** | $299 USD |
| **Released** | July 2026 |

### Sensors

| Sensor | Present? | Notes |
|---|---|---|
| IMU | ✅ Yes | 1,000Hz sampling for stabilization |
| Camera | ❌ No | Passive display only |
| Depth sensor | ❌ No | |
| Hand tracking | ❌ No | |

---

## ⚠️ Critical Architecture Notes

### This is a passive display — not a spatial computer

The xbxa01 is fundamentally different from XREAL Air 2 Ultra or XREAL One:

- **No onboard processor** — the host device (phone, laptop) does all compute
- **No camera** — cannot do plane detection, image anchoring, or 6DoF SLAM
- **No battery** — powered entirely by the USB-C host
- **The glasses = a USB-C monitor** that happens to sit on your face

### XREAL SDK Compatibility

| | |
|---|---|
| Listed in XREAL SDK device matrix? | **No** (as of SDK v3.1.0) |
| Supported XREAL SDK devices | XREAL One Series, Air, Air 2, Air 2 Pro, Air 2 Ultra |
| IMU accessible via SDK? | Unknown — likely not yet, may be added |
| Official dev docs for xbxa01? | Specs/manual only at tutorials.xreal.com |

This means we **cannot use the XREAL SDK** as designed — we need an alternative approach.

---

## What We Can Actually Build

### ✅ Achievable: PiP + 3DoF

| Feature | Approach |
|---|---|
| **Picture-in-picture** | Render multiple panels to the glasses display; Android PiP API or custom Unity multi-panel renderer |
| **3DoF (yaw/pitch/roll)** | IMU data via Android SensorManager — head rotation only, no positional tracking |
| **Virtual floating screens** | Unity scene with panels that rotate relative to head orientation |
| **Screen stabilization** | Use IMU data to counteract head movement (lock a "world-space" panel) |

### ❌ Not Achievable on xbxa01

| Feature | Reason |
|---|---|
| **6DoF** | No camera = no positional SLAM |
| **Plane detection** | No camera |
| **Spatial anchors** | No camera |
| **Hand tracking** | No camera |
| **Full XREAL SDK feature set** | Device not in SDK compatibility matrix |

---

## Recommended Architecture

### Stack

```
Host Device (Android phone)
    └── Unity App (Android APK)
            ├── Android SensorManager → IMU head rotation (3DoF)
            ├── Presentation API or USB-C secondary display → renders to glasses
            └── Unity multi-panel scene → PiP floating screens
```

### Display Approach

The glasses show up as a **secondary display** via Android's `Presentation` API (or `DisplayManager`). Two options:

**Option A — Android Presentation API (Recommended start)**
- Detect the glasses as a secondary display via `DisplayManager`
- Launch a `Presentation` window targeted at the glasses display
- Render PiP panels inside that `Presentation` using standard Android views or a Unity render texture
- Simpler, no XREAL SDK dependency

**Option B — Unity Full Renderer**
- Unity renders directly to the glasses as the primary output
- Use `AndroidJavaClass("android.hardware.SensorManager")` to pull IMU data
- Build virtual floating panel objects in Unity world space, rotated by IMU quaternion
- Better long-term if XREAL adds SDK support for the device

### PiP Implementation

For floating video windows in the glasses:

```
Unity Scene
├── Head Transform (driven by IMU quaternion)
├── Panel A — Main content (e.g., game/app stream)
├── Panel B — Floating PiP overlay (smaller, offset)
└── Panel C — Optional third panel (notifications, etc.)
```

Each panel is a RenderTexture on a world-space Canvas or Quad mesh. IMU keeps panels "pinned" to world space even as head moves.

---

## Development Setup

### Requirements

- **Unity** 2022.3 LTS or 6000.x (Unity 6)
- **Android Build Support** module installed in Unity Hub
- **Android SDK** API Level 31+ (Android 12 minimum)
- **Target ABI:** ARM64
- **Scripting Backend:** IL2CPP
- Android phone with USB-C DisplayPort output to pair with glasses

### Companion Device: Google Pixel 9

**Confirmed host device for this project.**

| Attribute | Detail |
|---|---|
| USB-C spec | USB 3.2 Gen 2 (10 Gbps) |
| DisplayPort Alt Mode | ✅ Yes — hardware supported out of the box |
| Chipset | Google Tensor G4 |
| Android version | Android 15+ |
| XREAL official compatibility list | Not listed (Pixel 9 not in XREAL's verified device list) |
| XREAL Nebula app | ❌ Reported incompatible on Pixel 9 Pro XL — irrelevant since Nebula is deprecated |
| Display output mode | **Mirror only** — Pixel 9 currently does not support Android extended/desktop display mode |

#### ⚠️ Mirror-Only Display — Critical Implication

The Pixel 9's USB-C display output is **screen mirroring**, not extended display. This affects the architecture:

- Android's `Presentation` API requires the phone to report the glasses as a **secondary/extended display** — this may not work on Pixel 9 with mirror mode
- **Workaround:** Target the glasses as the *primary* display (run app full-screen on glasses, use phone only as a controller/input)
- **Alternative:** Use a USB-C hub or adapter that forces extended mode, or wait for Android 16 / Android XR desktop mode (Google has signaled this is coming)
- **Unity approach is unaffected** — Unity can render directly to whatever display it's given

#### Cable Requirement

Must use a USB-C cable with explicit DisplayPort Alt Mode support — not all USB-C cables carry DP signal.

### Key Android APIs

```java
// Detect glasses as secondary display
DisplayManager dm = getSystemService(DisplayManager.class);
Display[] displays = dm.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION);

// Head rotation from IMU
SensorManager sm = getSystemService(SensorManager.class);
sm.registerListener(listener, sm.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR),
                    SensorManager.SENSOR_DELAY_GAME);

// Android PiP (if running app in PiP mode on phone side)
enterPictureInPictureMode(new PictureInPictureParams.Builder().build());
```

---

## Open Questions / Next Steps

- [ ] Confirm Pixel 9 + xbxa01 USB-C connection — verify if glasses appear as extended or mirror-only display (run `adb shell dumpsys display` with glasses connected)
- [ ] If mirror-only: prototype Unity full-screen on glasses as primary output with phone as controller
- [ ] Test IMU access via Android SensorManager through Unity's AndroidJavaClass bridge
- [ ] Decide: Unity-native renderer vs. Android Presentation API hybrid
- [ ] Investigate if XREAL plans SDK support for xbxa01 (check developer.xreal.com changelog)
- [ ] Define PiP content sources: video file, screen mirror, web view, camera feed?
- [ ] Prototype: minimal Unity scene rendering to secondary display with IMU head lock

---

## Resources

| Resource | URL |
|---|---|
| XREAL xbxa01 product page | https://www.xreal.com/xbxa01 |
| xbxa01 specs & manual | https://tutorials.xreal.com/docs/glasses/xbxa01/ |
| XREAL SDK docs | https://docs.xreal.com/ |
| XREAL Developer Portal | https://developer.xreal.com/ |
| XREAL SDK device compatibility | https://docs.xreal.com/XREALDevices/Compatibility |
| Android DisplayManager API | https://developer.android.com/reference/android/hardware/display/DisplayManager |
| Android Presentation API | https://developer.android.com/reference/android/app/Presentation |
| Unity Android docs | https://docs.unity3d.com/Manual/android.html |
| GitHub repo | https://github.com/adourish/xbxa01 |
| Pixel 9 specs | https://store.google.com/product/pixel_9_specs |
| Android DisplayManager (extended display) | https://developer.android.com/reference/android/hardware/display/DisplayManager |
