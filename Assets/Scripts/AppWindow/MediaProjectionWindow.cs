using UnityEngine;

/// <summary>
/// Whole-screen mirror fallback for panel content, via Android's MediaProjection API
/// (SPEC §Known limits: the untethered alternative to AppWindow's VirtualDisplay path,
/// since it needs no cross-app setLaunchDisplayId call an unsigned app can't make).
///
/// Trade-off vs AppWindow: one mirrored screen (whatever's on the device's default
/// display), not an independently addressable app window — and it needs a one-time
/// user consent dialog (RequestCapture) rather than running silently.
///
/// Pairs with Assets/Plugins/Android/ScreenCaptureBridge.java (and the
/// MediaProjectionRequestActivity/ScreenCaptureService it drives). Unlike AppWindow's
/// per-instance VirtualAppWindow, that Java side is a static bridge: the consent grant
/// has to survive a hop through a separate Activity and a foreground Service, so no
/// single object owns it end-to-end the way VirtualAppWindow owns its VirtualDisplay.
///
/// Attach to the same GameObject as (or wire a reference to) the target FloatingPanel.
/// In the Editor there is no MediaProjection API, so a checkerboard stands in.
/// </summary>
public class MediaProjectionWindow : MonoBehaviour
{
    [Header("Render target")]
    [Tooltip("Panel to display the mirrored screen on. Defaults to a FloatingPanel on this GameObject.")]
    public FloatingPanel targetPanel;

    [Header("Resolution")]
    [Tooltip("Capture size. Match the panel aspect (16:9) to avoid distortion.")]
    public int width  = 1280;
    public int height = 720;

    [Tooltip("Show the system consent dialog automatically on Start. Otherwise call RequestCapture() yourself (e.g. from a button).")]
    public bool requestOnStart = false;

    private Texture2D _texture;
    private bool _boundLiveFrames;   // switched to flipV once real captured frames arrive

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass _bridge;
#endif

    void Start()
    {
        if (targetPanel == null) targetPanel = GetComponent<FloatingPanel>();

        _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        // Visible placeholder until real frames arrive, so the panel is never a blank
        // or undefined-texture surface. The checkerboard is authored bottom-up, so no
        // flip; live captured frames are top-down and get flipV on first arrival.
        FillCheckerboard();
        _texture.Apply(false);
        if (targetPanel != null) targetPanel.SetContentTexture(_texture, flipV: false);

#if UNITY_ANDROID && !UNITY_EDITOR
        _bridge = new AndroidJavaClass("com.xbxa01.appwindow.ScreenCaptureBridge");
        if (requestOnStart) RequestCapture();
#else
        Debug.Log("[MediaProjectionWindow] Editor: showing checkerboard (no MediaProjection API).");
#endif
    }

    /// <summary>
    /// Show the one-time system screen-capture consent dialog. If granted, frames
    /// start arriving within a frame or two of the foreground service starting; if
    /// denied or cancelled, the panel keeps its checkerboard placeholder.
    /// </summary>
    public void RequestCapture()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            _bridge.CallStatic("requestPermission", activity, width, height);
        }
        Debug.Log("[MediaProjectionWindow] Consent dialog requested.");
#else
        Debug.Log("[MediaProjectionWindow] Editor: RequestCapture is a no-op.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void Update()
    {
        if (_bridge == null) return;

        if (_bridge.CallStatic<bool>("consumeDenied"))
            Debug.LogWarning("[MediaProjectionWindow] User denied/cancelled screen capture consent.");

        // Only the newest frame; null when nothing new has arrived since last frame.
        byte[] frame = _bridge.CallStatic<byte[]>("acquireFrame");
        if (frame == null || frame.Length != width * height * 4) return;

        _texture.LoadRawTextureData(frame);
        _texture.Apply(false);

        // First live frame: rebind with flipV — captured frames are top-down, unlike
        // the bottom-up checkerboard placeholder bound at startup.
        if (!_boundLiveFrames && targetPanel != null)
        {
            targetPanel.SetContentTexture(_texture, flipV: true);
            _boundLiveFrames = true;
            Debug.Log("[MediaProjectionWindow] Live frames arriving; bound screen mirror to panel.");
        }
    }

    void OnDestroy()
    {
        _bridge?.CallStatic("stop");
    }
#endif

    void FillCheckerboard()
    {
        var pixels = new Color32[width * height];
        const int cell = 64;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool on = ((x / cell) + (y / cell)) % 2 == 0;
            pixels[y * width + x] = on ? new Color32(48, 32, 32, 255)
                                       : new Color32(84, 56, 56, 255);
        }
        _texture.SetPixels32(pixels);
    }
}
