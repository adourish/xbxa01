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
| 3.5 | Main panel dims past ~25° off-centre | `FloatingPanel.dimOnLookAway` on Main lerps `CanvasGroup.alpha` 1→0.35 between 25°–55° off world-forward |

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
        │     └── static (identity rotation) — see World-Lock Mechanism
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
  headRotation = IMU quaternion, relative to the neutral pose captured at startup
                 (e.g., user turned 30° right)

  camera.rotation      = identity              (camera does NOT move)
  worldAnchor.rotation = Inverse(headRotation)

  Panel position in camera space = Inverse(headRotation) × fixed localPos
  ∴ Panel appears stationary as head rotates.
```

Counter-rotating the anchor and rotating the camera are two ways to express the
same thing. Doing BOTH yields Inverse(headRotation)² — panels counter-rotate at
twice head rate. Exactly one must be applied.

HeadRotation is recentred: the sensor reports absolute orientation against ENU
(magnetic north), so `HeadTracker` captures the first sample as the neutral pose
and reports rotation relative to it. Without that, panels would anchor to north
rather than to wherever the user happens to be facing at launch.

### Panel Content Sources

A `FloatingPanel` is just a world-space quad with a `Content` RawImage. What fills
that RawImage is deliberately pluggable — `FloatingPanel.SetContentTexture(tex, flipV)`
binds any `Texture` to the panel. The default fill is a flat colour; two real sources
exist — a live Android app (`AppWindow`, tethered launch) and a whole-screen mirror
(`MediaProjectionWindow`, untethered but coarser) — described below.

**App-in-a-window (`AppWindow` + `VirtualAppWindow.java`):**

```
AppWindow.cs (Unity, main thread)
  └── new VirtualAppWindow(activity, w, h)         ← Assets/Plugins/Android/*.java
        ├── ImageReader(w, h, RGBA_8888)           ← CPU-readable frames
        └── DisplayManager.createVirtualDisplay(    ← PUBLIC|PRESENTATION|OWN_CONTENT_ONLY
              surface = imageReader.getSurface())
  └── launch(packageName)
        └── startActivity(intent,
              ActivityOptions.setLaunchDisplayId(virtualDisplayId))
  └── Update(): byte[] = acquireFrame()             ← newest RGBA, else null
        └── Texture2D.LoadRawTextureData → Apply
        └── FloatingPanel.SetContentTexture(tex, flipV:true)
```

The app runs on an off-screen `VirtualDisplay` we own; its frames are read back via
`ImageReader` and uploaded into the panel texture, one copy per frame. Frames are
top-down in memory, so the panel flips them in UV space (`flipV`) rather than paying a
per-frame CPU row-flip.

**Why ImageReader, not a SurfaceTexture/OES zero-copy bridge:** ImageReader needs no
shared GL context, no native `.so`, and no external-OES shader, so it builds from a
clean clone through Unity's normal Gradle step — consistent with the rest of this
project (see §Dependencies). The cost is one `w*h*4` copy per frame: negligible at PiP
size, fine for a main panel at ~30fps. The zero-copy `SurfaceTexture` path is the
optimisation if that copy ever dominates.

**Known limits (verified on a Pixel 9, Android 15):**
- **A non-system app cannot launch *other* apps onto its own virtual display.** The
  display we create is *untrusted* (no `FLAG_TRUSTED`; setting it needs the
  `ADD_TRUSTED_DISPLAY` signature permission a sideloaded app can't hold). Android's
  `SafeActivityOptions.checkPermissions` then denies any cross-app `setLaunchDisplayId`
  with a `SecurityException` — confirmed against Settings *and* Chrome. `launch()`
  catches and logs it; the panel keeps its placeholder. So `AppWindow.launchOnStart`
  defaults **off**: the app only creates the display and streams whatever renders on it.
- **The launch must come from an elevated context.** Two options that work:
  1. *Tethered:* `adb shell` (uid 2000) has the permission the app lacks. Run the app,
     then `tools/launch_on_panel.sh <package>` — it finds the `xbxa01-appwindow` display
     id via `dumpsys display` and `am start --display <id>`. Verified: Settings and
     Chrome both render on the main panel.
  2. *Self-contained:* build the app **system-signed** (platform key / privileged
     install), grant `ADD_TRUSTED_DISPLAY`, create the display with `FLAG_TRUSTED`, and
     `launch()` works in-app with no tether. Requires a custom ROM or OEM signing.
- For fully untethered use without system signing, the alternative is **MediaProjection**
  (whole-screen capture with a one-time consent dialog) shown on a panel — one mirrored
  screen rather than independent app windows. Implemented; see below.
- Package visibility (Android 11+): the target app's launcher intent is only resolvable
  because of the `<queries>` block in `AndroidManifest.xml`.

**Screen mirror fallback (`MediaProjectionWindow` + `ScreenCaptureBridge.java`):**

```
MediaProjectionWindow.cs (Unity, main thread)
  └── RequestCapture()
        └── ScreenCaptureBridge.requestPermission(activity, w, h)   ← static bridge
              └── starts MediaProjectionRequestActivity (translucent)
                    └── MediaProjectionManager.createScreenCaptureIntent()
                          └── system consent dialog (one-time per launch)
                    └── onActivityResult → ScreenCaptureBridge.onPermissionResult
                          └── starts ScreenCaptureService (foreground, type=mediaProjection)
                                └── MediaProjectionManager.getMediaProjection(code, data)
                                └── ScreenCaptureBridge.start(context, projection)
                                      ├── ImageReader(w, h, RGBA_8888)
                                      └── projection.createVirtualDisplay(..., imageReader.getSurface())
  └── Update(): byte[] = ScreenCaptureBridge.acquireFrame()         ← newest RGBA, else null
        └── Texture2D.LoadRawTextureData → Apply
        └── FloatingPanel.SetContentTexture(tex, flipV:true)
```

Unlike `AppWindow`/`VirtualAppWindow` (one object, one `VirtualDisplay`, owned by a
single Activity call), `ScreenCaptureBridge` is a **static** Java class. MediaProjection's
consent grant is an `Intent` result that only a real `Activity.onActivityResult` can
receive — `UnityPlayerActivity` doesn't forward that to plugin code — so a translucent
helper activity (`MediaProjectionRequestActivity`) exists solely to request it, and a
foreground service (`ScreenCaptureService`) exists solely to hold it, since Android
10+ requires `createVirtualDisplay()` to be called from a running foreground service,
and Android 14+ additionally requires `foregroundServiceType="mediaProjection"` plus
the `FOREGROUND_SERVICE_MEDIA_PROJECTION` permission. No single Unity-owned object
spans all three components, so the frame buffer lives in the static bridge instead —
`MediaProjectionWindow` polls it exactly like `AppWindow` polls `VirtualAppWindow`.

Call `MediaProjectionWindow.RequestCapture()` (e.g. from a UI button, or set
`requestOnStart = true` to prompt automatically) to show the one-time consent dialog;
if the user denies or cancels, the panel keeps its checkerboard placeholder and
`consumeDenied()` surfaces a warning in logcat.

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

**The scene is generated, not hand-authored.** `Assets/Editor/SceneBuilder.cs` builds
it (and the panel prefabs) from code, so it can be produced headlessly and reviewed
as source. `BuildScript` calls it automatically if the scene is missing, so a fresh
clone builds with no manual Editor work.

To (re)generate it explicitly:

```bat
Unity.exe -batchmode -quit -projectPath . -executeMethod SceneBuilder.BuildMainScene
```
or in the Editor: **Tools ▸ XBXA01 ▸ Rebuild Main Scene**.

Note the panel prefabs carry a **BoxCollider**. `PhoneController` selects panels with
`Physics.Raycast`, which does not hit a world-space Canvas on its own — without the
collider, drag and pinch silently never fire.

The generated hierarchy:

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

**Camera rotation** — do NOT rotate the Main Camera.

World-lock is achieved by exactly one mechanism: `WorldAnchor` counter-rotates by
`Inverse(HeadRotation)` while the camera stays fixed. Rotating the camera by
`HeadRotation` *as well* applies the inverse twice — panels then swing backwards at
2× head rate, which fails AC-3.2. Pick one; this project uses the anchor.

The Main Camera is a plain static camera. Leave its rotation at identity.

### Dependencies

**None.** FloatingPanel originally used LeanTween; its scale animations are now plain
coroutines with equivalent easeOutBack/easeInBack curves, so the project has no Asset
Store dependencies and builds from a clean clone.

---

## 5. How We Deploy and Test

### Quick Deploy (Pixel 9 plugged in)

```bat
cd C:\projects\xbxa01
tools\deploy.bat debug
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
adb install -r build\xbxa01.apk   :: written by tools\deploy.bat

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
| 4 | ~~LeanTween dependency~~ | Resolved | Replaced with coroutines; no external deps |
| 5 | 120Hz stable on Pixel 9? | Low | Tensor G4 should handle it; monitor via DebugOverlay FPS |
| 6 | XREAL SDK adds xbxa01 support? | Opportunity | Monitor developer.xreal.com; migration path is straightforward |
