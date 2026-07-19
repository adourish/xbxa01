using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires the on-screen button bar and status line to DesktopBridge + WindowLayoutManager.
/// This is the visible, discoverable control surface that replaces blind gestures.
///
/// The scene (built by ControllerSceneBuilder) assigns the button references. Any button
/// left null is simply skipped, so the UI degrades gracefully while it's being authored.
/// </summary>
public class ControllerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DesktopBridge bridge;
    [SerializeField] private WindowLayoutManager layouts;

    [Header("Status")]
    [SerializeField] private Text statusText;
    [Tooltip("Shown when the user still needs to enable the accessibility service / IME.")]
    [SerializeField] private GameObject setupBanner;
    [SerializeField] private Text setupText;

    [Header("Global action buttons")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button recentsButton;
    [SerializeField] private Button keyboardButton;

    [Header("Layout preset buttons (Focus, Duo, Trio, Main+Side)")]
    [SerializeField] private Button[] layoutButtons = new Button[4];

    [Header("Window snap buttons")]
    [SerializeField] private Button snapLeftButton;
    [SerializeField] private Button maximizeButton;
    [SerializeField] private Button snapRightButton;

    [Header("Setup")]
    [SerializeField] private Button openA11ySettingsButton;

    private float _statusTimer;

    void Awake()
    {
        if (bridge == null)  bridge = DesktopBridge.Instance;
        if (layouts == null) layouts = FindObjectOfType<WindowLayoutManager>();
    }

    void Start()
    {
        Wire(homeButton,    () => bridge.Home());
        Wire(backButton,    () => bridge.Back());
        Wire(recentsButton, () => bridge.Recents());
        Wire(keyboardButton, OnKeyboard);

        for (int i = 0; i < layoutButtons.Length; i++)
        {
            int idx = i; // capture
            Wire(layoutButtons[i], () => layouts.ApplyByIndex(idx));
        }

        Wire(snapLeftButton,  () => bridge.SnapWindow(0));
        Wire(maximizeButton,  () => bridge.SnapWindow(2));
        Wire(snapRightButton, () => bridge.SnapWindow(1));

        Wire(openA11ySettingsButton, () => bridge.OpenAccessibilitySettings());
    }

    void Update()
    {
        if (bridge == null) return;

        // Refresh status roughly twice a second — cheap JNI bool reads.
        _statusTimer -= Time.unscaledDeltaTime;
        if (_statusTimer <= 0f)
        {
            _statusTimer = 0.5f;
            RefreshStatus();
        }
    }

    void RefreshStatus()
    {
        bool needsSetup = bridge.NeedsSetup;

        if (setupBanner != null) setupBanner.SetActive(needsSetup);
        if (needsSetup && setupText != null)
            setupText.text = "Enable “XBXA01 Controller” in Accessibility to start controlling the desktop.";

        if (statusText != null)
        {
            string pad = bridge.AccessibilityReady ? "pad ●" : "pad ○";
            string key = bridge.KeyboardReady ? "kbd ●" : "kbd ○";
            statusText.text = $"{pad}   {key}   layout: {layouts?.Current}";
        }
    }

    void OnKeyboard()
    {
        // If our IME isn't selected yet, take the user to input settings; otherwise the
        // IME is live and typing flows through DesktopBridge.Type (wired by a text field
        // the scene can add later). For the MVP the button surfaces the setup path.
        if (!bridge.KeyboardReady)
            bridge.OpenKeyboardSettings();
    }

    static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(action);
    }
}
