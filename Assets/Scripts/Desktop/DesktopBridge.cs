using UnityEngine;

/// <summary>
/// C# side of the desktop-mode controller. Wraps the Java DesktopController facade
/// (Assets/Plugins/Android/DesktopController.java) so the rest of the app can drive the
/// glasses desktop with plain method calls.
///
/// In the Editor there is no Android desktop, so calls are logged no-ops — the
/// controller UI still lays out and responds, which is enough to iterate on layout.
///
/// Enable-once setup (surfaced by ControllerUI when NeedsSetup is true):
///   • Accessibility ▸ XBXA01 Controller           (pointer, gestures, window snap)
///   • Languages & input ▸ XBXA01 Keyboard         (typing)
/// </summary>
public class DesktopBridge : MonoBehaviour
{
    public static DesktopBridge Instance { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _java;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            _java = new AndroidJavaObject("com.xbxa01.desktop.DesktopController", activity);
        }
#endif
    }

    // ---- readiness ----------------------------------------------------------

    /// <summary>The a11y service is bound and ready to dispatch gestures.</summary>
    public bool AccessibilityReady => CallBool("isAccessibilityReady");

    /// <summary>The a11y service is enabled in Settings (may not have bound yet).</summary>
    public bool AccessibilityEnabled => CallBool("isAccessibilityEnabled");

    /// <summary>Our IME is running (selected + a field focused).</summary>
    public bool KeyboardReady => CallBool("isKeyboardReady");

    /// <summary>True while the user still has a one-time enable step to do.</summary>
    public bool NeedsSetup => !AccessibilityEnabled;

    public void OpenAccessibilitySettings() => Call("openAccessibilitySettings");
    public void OpenKeyboardSettings()      => Call("openKeyboardSettings");

    // ---- pointer / gestures -------------------------------------------------

    public void MoveCursor(float dx, float dy) => Call("moveCursor", dx, dy);
    public void Tap()                          => Call("tap");
    public void DoubleTap()                    => Call("doubleTap");
    public void Drag(float dx, float dy)       => Call("drag", dx, dy);
    public void Scroll(float dx, float dy)     => Call("scroll", dx, dy);

    /// <summary>0 = snap left half, 1 = snap right half, 2 = maximize.</summary>
    public void SnapWindow(int region) => Call("snapWindow", region);

    // ---- global actions -----------------------------------------------------

    public void Home()          => Call("home");
    public void Back()          => Call("back");
    public void Recents()       => Call("recents");
    public void Notifications() => Call("notifications");
    public void Split()         => Call("split");

    // ---- keyboard -----------------------------------------------------------

    public void Type(string text) => Call("type", text);
    public void Backspace()       => Call("backspace");
    public void Enter()           => Call("enter");

    // ---- JNI plumbing -------------------------------------------------------

    void Call(string method, params object[] args)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_java == null) return;
        _java.Call(method, args);
#else
        Debug.Log($"[DesktopBridge] (editor no-op) {method}({string.Join(", ", args)})");
#endif
    }

    bool CallBool(string method)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return _java != null && _java.Call<bool>(method);
#else
        return false;
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _java?.Dispose();
        _java = null;
#endif
        if (Instance == this) Instance = null;
    }
}
