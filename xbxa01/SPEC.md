# XBXA01 AR App — Full Specification

## 1. User Journey (Linear Walkthrough)

### Step 1 — Physical Setup
1. Connect xbxa01 glasses to Pixel 9 via a **DisplayPort Alt Mode USB-C cable**.
2. Put on glasses. The glasses power on immediately (no button, bus-powered).
3. Pixel 9 screen mirrors to glasses (Pixel 9 = mirror-only output).

### Step 2 — Launch
4. Tap **XBXA01 AR** app icon on phone.
5. App opens fullscreen in landscape. Phone screen (and glasses) show a dark background.
6. A subtle loading indicator appears (< 1 second).

### Step 3 — IMU Init
7. App registers the rotation vector sensor.
8. Within ~0.5 seconds, tracking begins. A brief "Tracking Active" toast fades in.

### Step 4 — Panels Appear
9. **Main panel** (large, centred) animates in from scale 0 → full size (0.3s ease-out).
10. **PiP panel** (small, upper-right) animates in 150ms later.
11. Both panels are now **world-locked**: rotating head left/right keeps them stationary.

### Step 5 — Normal Use
12. User turns head — panels remain fixed in world space.
13. User looks away past ~25° from centre — main panel dims slightly (focus hint).
14. User double-taps phone screen — Main and PiP panels swap size/position.
15. User drags one finger on phone — selected panel repositions.
16. User pinch-zooms on phone — selected panel scales.

### Step 6 — Suspend / Resume
17. User receives a phone call → app pauses, sensor unregistered.
18. Call ends → app resumes, sensor re-registered, panels restore last position.

### Step 7 — Exit
19. User presses back button or power-off. App calls `Application.Quit()`.
20. Glasses return to showing phone home screen (mirrored).

---

## 2. Acceptance Criteria

### AC-01 Display
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 1.1 | App launches in landscape | `Screen.orientation == LandscapeLeft` |
| 1.2 | Fills glasses display | Fullscreen, no letterbox, 1920×1080 |
| 1.3 | Target frame rate set | `Application.targetFrameRate == 120` |
| 1.4 | Screen does not sleep | `Screen.sleepTimeout == NeverSleep` |

### AC-02 IMU / Head Tracking
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 2.1 | Sensor registers on device | `HeadTracker.IsTracking == true` within 1s |
| 2.2 | Rotation updates at ≥ 50 Hz | Confirmed via DebugOverlay: q values change every frame |
| 2.3 | No sensor null crash | App stable for 30 min continuous use |
| 2.4 | Sensor unregisters on destroy | No `adb logcat` sensor leak warnings |

### AC-03 World-Locked Panels
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 3.1 | Panels visually stable when head is stationary | Panel edge drift < 0.5° over 10s |
| 3.2 | Panels remain fixed as head rotates ±45° | Panel position does not translate with head motion |
| 3.3 | Main panel centred at neutral head pose | Main panel within ±5% of screen centre |
| 3.4 | PiP panel offset upper-right | PiP visible in upper-right quadrant |

### AC-04 Touch Controls
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 4.1 | Double-tap swaps Main ↔ PiP | Panel sizes and positions exchange |
| 4.2 | Single-finger drag repositions panel | Panel follows drag direction |
| 4.3 | Pinch scales panel | Panel enlarges/shrinks proportionally |

### AC-05 Lifecycle
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 5.1 | App survives pause/resume | Panels restore, tracking resumes |
| 5.2 | Back button exits cleanly | No ANR, no crash in logcat |

### AC-06 Build
| # | Criterion | Pass condition |
|---|-----------|----------------|
| 6.1 | APK builds headless | `deploy.bat debug` exits 0 |
| 6.2 | APK installs on Pixel 9 | `adb install` succeeds |
| 6.3 | APK is ARM64 IL2CPP | Verified in `adb shell pm dump com.xbxa01.glassesvr` |

---

## 3. How It Works

### Architecture

```
Pixel 9 (Android 15)
  └── Unity Android App (ARM64, IL2CPP)
        │
        ├── HeadTracker.cs
        │     └── AndroidJavaProxy → SensorManager
        │           └── TYPE_ROTATION_VECTOR @ SENSOR_DELAY_GAME
        │                 └── → Quaternion (50Hz, thread-safe)
        │
        ├── WorldAnchor.cs
        │     └── transform.rotation = Inverse(HeadTracker.HeadRotation)
        │           └── children appear world-locked
        │
        ├── FloatingPanel × N  (children of WorldAnchor)
        │     └── Canvas (World Space) on Quad mesh
        │           └── RenderTexture or UI content
        │
        ├── Main Camera
        │     └── rotation = HeadTracker.HeadRotation
        │           └── renders WorldAnchor subtree
        │
        └── PhoneController.cs
              └── Touch input → panel reposition/scale/swap
```

### Display Architecture (Mirror Mode)

```
Pixel 9 GPU framebuffer
    │
    ├── Pixel 9 screen (1080×2424 portrait → rotated 1920×1080)
    └── USB-C DisplayPort → xbxa01 glasses (1920×1080 per eye)

Unity renders ONE scene fullscreen.
Both screens show the same image.
Design target: optimise for glasses FOV (50°, ~147" virtual).
```

### Coordinate System

```
Android ENU (right-hand)          Unity (left-hand Y-up)
  X = East  (right)                 X = Right
  Y = North (forward)               Y = Up
  Z = Up                            Z = Forward

Remap:
  unity.x =  android.x
  unity.y =  android.z
  unity.z =  android.y
  unity.w = -android.w   (flip handedness)
```

### World-Lock Mechanism

```
Frame N:
  headRotation = IMU quaternion (e.g., user turned 30° right)
  camera.rotation = headRotation       (camera follows head)
  worldAnchor.rotation = Inverse(headRotation)

  Panel world position = worldAnchor.localPos × worldAnchor.rotation
                       = fixed localPos × Inverse(headRotation)
  ∴ Panel appears stationary as head rotates.
```

---

## 4. How We Build It

### One-Time Setup (do once)

1. **Install Unity Hub** → https://unity.com/download
2. **Add Unity 2022.3 LTS** (or Unity 6000.x)
   - Module: Android Build Support
   - Module: Android SDK & NDK Tools
   - Module: OpenJDK
3. **Android SDK Platform Tools** → add to PATH:
   ```
   %LOCALAPPDATA%\Android\Sdk\platform-tools
   ```
4. **Enable USB Debugging on Pixel 9**:
   Settings → About Phone → tap Build Number 7× → Developer Options → USB Debugging ON

5. **Verify ADB**:
   ```
   adb devices
   # Should show: <serial>    device
   ```

### Creating the Unity Project

1. Unity Hub → New Project → **3D (URP)** → name: `xbxa01`
2. Copy `Assets/` folder from this repo into the project
3. File → Build Settings → switch to **Android**
4. Player Settings:
   - Company: xbxa01
   - Product: XBXA01 AR
   - Package: `com.xbxa01.glassesvr`
   - Scripting Backend: **IL2CPP**
   - Target Architectures: **ARM64** only
   - Orientation: **Landscape Left**

### Scene Setup (Main.unity)

Create the following hierarchy:

```
Scene: Main
├── [AppController]          ← AppController.cs, HeadTracker.cs, DisplayDetector.cs
├── Main Camera              ← Standard camera, Clear Flags: Solid Color (black)
│   └── (rotation driven by HeadTracker in AppController)
├── WorldAnchor              ← WorldAnchor.cs
│   ├── MainPanel Prefab     ← FloatingPanel.cs, Canvas (World Space)
│   │   └── RawImage (1920×1080 RenderTexture or placeholder)
│   └── PiPPanel Prefab      ← FloatingPanel.cs, Canvas (World Space)
│       └── RawImage (480×270)
└── PhoneController          ← PhoneController.cs
```

**Camera rotation** — add this to AppController.Update():
```csharp
if (mainCamera != null)
    mainCamera.transform.rotation = headTracker.HeadRotation;
```

### LeanTween Dependency

FloatingPanel uses LeanTween for animations. Import via:
- Unity Asset Store: search "LeanTween" (free)
- OR replace `LeanTween.scale(...)` calls with a simple coroutine if preferred.

---

## 5. How We Deploy and Test

### Quick Deploy (Pixel 9 plugged in)

```bat
cd C:\projects\xbxa01\xbxa01
build\deploy.bat debug
```

This: builds APK → installs on Pixel 9 → launches app.

### Manual Deploy Steps

```bat
:: 1. Check device
adb devices

:: 2. Check display state (glasses connected?)
adb shell dumpsys display | findstr -i "display\|mDisplayId\|width\|height"

:: 3. Check sensors available
adb shell dumpsys sensorservice | findstr -i "rotation"

:: 4. Install APK
adb install -r build\xbxa01.apk

:: 5. Launch
adb shell am start -n com.xbxa01.glassesvr/com.unity3d.player.UnityPlayerActivity

:: 6. Watch Unity logs
adb logcat -s Unity:V

:: 7. Watch IMU data (should see sensor events)
adb logcat -s HeadTracker:V IMUListener:V
```

### First Boot Verification Checklist

- [ ] App launches fullscreen landscape
- [ ] `[HeadTracker] Sensor registered: True` in logcat
- [ ] `[DisplayDetector] Mode=MirrorToGlasses` in logcat
- [ ] WorldAnchor counter-rotation visible (tilt phone, panels stay)
- [ ] Both panels visible in glasses
- [ ] Double-tap swaps panels
- [ ] DebugOverlay shows live quaternion values

### Debugging Display State

```bat
:: What displays are connected?
adb shell dumpsys display

:: Is USB-C DisplayPort active?
adb shell dumpsys window displays | findstr -i "display\|flag"

:: Check if glasses are Extended vs Mirror
:: Look for: FLAG_PRESENTATION (extended) vs mirroring references
adb shell dumpsys SurfaceFlinger | findstr -i "display\|mirror"
```

### IMU Validation Test

1. Hold phone still for 10 seconds
2. Open DebugOverlay — quaternion values should be nearly constant
3. Rotate phone 90° left — `euler.Y` should change by ~90°
4. Panels should NOT move in glasses view (world-locked)

---

## 6. Open Questions / Risks

| # | Question | Risk | Mitigation |
|---|----------|------|------------|
| 1 | Does Pixel 9 DP Alt Mode work with xbxa01 cable? | High | Test `adb shell dumpsys display` with glasses connected |
| 2 | Is mirror-mode acceptable UX? | Medium | Phone shows same view as glasses; design for glasses-primary |
| 3 | IMU coordinate remap correct? | Medium | Validate with known rotations in first test session |
| 4 | LeanTween available / acceptable dependency? | Low | Replace with coroutine if needed |
| 5 | 120Hz stable on Pixel 9? | Low | Tensor G4 should handle it; monitor via DebugOverlay FPS |
| 6 | XREAL SDK adds xbxa01 support? | Opportunity | Monitor developer.xreal.com; migration path is straightforward |
