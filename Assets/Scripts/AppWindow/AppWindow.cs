using UnityEngine;

/// <summary>
/// Launches an installed Android app onto an off-screen VirtualDisplay and streams
/// its frames into a FloatingPanel — the "panel content source" the SPEC left open.
///
/// Pairs with Assets/Plugins/Android/VirtualAppWindow.java: that class owns the
/// VirtualDisplay + ImageReader and hands us tightly-packed RGBA each frame; we upload
/// it into a Texture2D and bind it to the panel.
///
/// Attach to the same GameObject as (or wire a reference to) the target FloatingPanel.
/// In the Editor there is no Android app to run, so a checkerboard stands in.
/// </summary>
public class AppWindow : MonoBehaviour
{
    [Header("Source app")]
    [Tooltip("Package name of the app to run in this window, e.g. com.google.android.youtube.")]
    public string packageName = "";

    [Tooltip("Launch the app automatically on Start. Otherwise call Launch() yourself.")]
    public bool launchOnStart = true;

    [Header("Render target")]
    [Tooltip("Panel to display the app on. Defaults to a FloatingPanel on this GameObject.")]
    public FloatingPanel targetPanel;

    [Header("Resolution")]
    [Tooltip("Virtual display size. Match the panel aspect (16:9) to avoid distortion.")]
    public int width  = 1280;
    public int height = 720;

    private Texture2D _texture;
    private bool _boundLiveFrames;   // switched to flipV once real ImageReader frames arrive

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _java;
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
        // flip; live ImageReader frames are top-down and get flipV on first arrival.
        FillCheckerboard();
        _texture.Apply(false);
        if (targetPanel != null) targetPanel.SetContentTexture(_texture, flipV: false);

#if UNITY_ANDROID && !UNITY_EDITOR
        CreateJavaWindow();
        // A non-system app cannot launch another app onto its own (untrusted) virtual
        // display — Android throws SecurityException. The launch is expected to come
        // from an elevated context: `adb shell am start --display <id> -n <pkg>/<act>`
        // (tethered), or the app must be system-signed to self-launch. launchOnStart is
        // left off by default to avoid a guaranteed SecurityException in the log.
        if (launchOnStart) Launch();
#else
        Debug.Log("[AppWindow] Editor: showing checkerboard (no Android app to launch).");
#endif
    }

    /// <summary>Launch (or relaunch) <see cref="packageName"/> onto this window.</summary>
    public void Launch()
    {
        if (string.IsNullOrEmpty(packageName))
        {
            Debug.LogWarning("[AppWindow] No packageName set; nothing to launch.");
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        bool ok = _java != null && _java.Call<bool>("launch", packageName);
        Debug.Log($"[AppWindow] Launch {packageName}: {ok}");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void CreateJavaWindow()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            _java = new AndroidJavaObject(
                "com.xbxa01.appwindow.VirtualAppWindow", activity, width, height);
        }
    }

    void Update()
    {
        if (_java == null) return;

        // Only the newest frame; null when nothing new has arrived since last frame.
        byte[] frame = _java.Call<byte[]>("acquireFrame");
        if (frame == null || frame.Length != width * height * 4) return;

        _texture.LoadRawTextureData(frame);
        _texture.Apply(false);

        // First live frame: rebind with flipV — ImageReader frames are top-down,
        // unlike the bottom-up checkerboard placeholder bound at startup.
        if (!_boundLiveFrames && targetPanel != null)
        {
            targetPanel.SetContentTexture(_texture, flipV: true);
            _boundLiveFrames = true;
            Debug.Log("[AppWindow] Live frames arriving; bound app content to panel.");
        }
    }

    void OnDestroy()
    {
        if (_java != null)
        {
            _java.Call("release");
            _java.Dispose();
            _java = null;
        }
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
            pixels[y * width + x] = on ? new Color32(40, 40, 48, 255)
                                       : new Color32(70, 70, 84, 255);
        }
        _texture.SetPixels32(pixels);
    }
}
