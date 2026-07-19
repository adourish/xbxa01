#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/Controller.unity — the phone-side trackpad-and-buttons surface
/// for desktop mode (see DESKTOP_MODE.md). Like SceneBuilder, the scene is generated
/// from code so it can be produced headlessly and reviewed as source.
///
/// This becomes the app's default scene when desktop mode is the product; SceneBuilder's
/// world-locked panel scene (Main.unity) remains the mirror-mode fallback and is still
/// rebuildable from its own menu item.
///
/// Usage:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod ControllerSceneBuilder.BuildControllerScene
///   or in the Editor: Tools ▸ XBXA01 ▸ Build Controller Scene (Desktop Mode)
/// </summary>
public static class ControllerSceneBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string ScenePath = ScenesDir + "/Controller.unity";

    static readonly Color Bg        = new Color(0.06f, 0.07f, 0.09f);
    static readonly Color Pad       = new Color(0.12f, 0.13f, 0.17f);
    static readonly Color BtnColor  = new Color(0.18f, 0.20f, 0.26f);
    static readonly Color Accent    = new Color(0.12f, 0.53f, 0.90f);

    [MenuItem("Tools/XBXA01/Build Controller Scene (Desktop Mode)")]
    public static void BuildControllerScene()
    {
        Directory.CreateDirectory(ScenesDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Camera (UI only; solid background)
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // --- Controller subsystems
        var ctrlGo = new GameObject("Controller");
        var bridge  = ctrlGo.AddComponent<DesktopBridge>();
        var layouts = ctrlGo.AddComponent<WindowLayoutManager>();
        var trackpad = ctrlGo.AddComponent<TrackpadController>();
        var ui = ctrlGo.AddComponent<ControllerUI>();

        // --- Canvas
        var canvasGo = new GameObject("UI", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        // --- Trackpad zone (fills the centre)
        var padGo = CreatePanel(canvasGo.transform, "Trackpad", Pad);
        var padRect = padGo.GetComponent<RectTransform>();
        Stretch(padRect, new Vector2(40f, 140f), new Vector2(-40f, -140f));
        AddLabel(padGo.transform, "TrackpadHint",
            "T R A C K P A D\n\ndrag = move cursor   ·   tap = click\n2-finger drag = scroll   ·   hold+drag = move window",
            28, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter);

        // --- Top bar: status + global actions
        var topBar = CreateRow(canvasGo.transform, "TopBar", top: true);
        var statusText = AddLabel(topBar.transform, "Status", "pad ○   kbd ○   layout: Focus",
            26, Color.white, TextAnchor.MiddleLeft, width: 520f);
        var homeBtn    = AddButton(topBar.transform, "Home",    "⌂ Home");
        var backBtn    = AddButton(topBar.transform, "Back",    "‹ Back");
        var recentsBtn = AddButton(topBar.transform, "Recents", "▚ Recents");
        var kbdBtn     = AddButton(topBar.transform, "Keyboard", "⌨ Keyboard", Accent);

        // --- Bottom bar: layout presets + window snapping
        var bottomBar = CreateRow(canvasGo.transform, "BottomBar", top: false);
        var focusBtn = AddButton(bottomBar.transform, "Focus", "Focus", Accent);
        var duoBtn   = AddButton(bottomBar.transform, "Duo",   "Duo");
        var trioBtn  = AddButton(bottomBar.transform, "Trio",  "Trio");
        var mainBtn  = AddButton(bottomBar.transform, "MainSide", "Main+Side");
        var snapL    = AddButton(bottomBar.transform, "SnapLeft",  "◀ Snap");
        var maxBtn   = AddButton(bottomBar.transform, "Max",       "▲ Max");
        var snapR    = AddButton(bottomBar.transform, "SnapRight", "Snap ▶");

        // --- Setup banner (hidden unless the a11y service isn't enabled)
        var banner = CreatePanel(canvasGo.transform, "SetupBanner", new Color(0.15f, 0.09f, 0.05f, 0.96f));
        var bannerRect = banner.GetComponent<RectTransform>();
        Stretch(bannerRect, new Vector2(120f, 400f), new Vector2(-120f, -400f));
        var setupText = AddLabel(banner.transform, "SetupText",
            "Enable “XBXA01 Controller” in Accessibility to start controlling the desktop.",
            30, Color.white, TextAnchor.UpperCenter);
        var openSettingsBtn = AddButton(banner.transform, "OpenSettings", "Open Accessibility Settings", Accent);
        var osRect = openSettingsBtn.GetComponent<RectTransform>();
        osRect.anchorMin = osRect.anchorMax = new Vector2(0.5f, 0f);
        osRect.pivot = new Vector2(0.5f, 0f);
        osRect.anchoredPosition = new Vector2(0f, 40f);
        osRect.sizeDelta = new Vector2(520f, 90f);

        // --- Wiring
        SetRef(trackpad, "bridge", bridge);
        SetRef(trackpad, "trackpadZone", padRect);

        SetRef(ui, "bridge", bridge);
        SetRef(ui, "layouts", layouts);
        SetRef(ui, "statusText", statusText);
        SetRef(ui, "setupBanner", banner);
        SetRef(ui, "setupText", setupText);
        SetRef(ui, "homeButton", homeBtn);
        SetRef(ui, "backButton", backBtn);
        SetRef(ui, "recentsButton", recentsBtn);
        SetRef(ui, "keyboardButton", kbdBtn);
        SetRefArray(ui, "layoutButtons", new[] { focusBtn, duoBtn, trioBtn, mainBtn });
        SetRef(ui, "snapLeftButton", snapL);
        SetRef(ui, "maximizeButton", maxBtn);
        SetRef(ui, "snapRightButton", snapR);
        SetRef(ui, "openA11ySettingsButton", openSettingsBtn);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Ship the controller scene as scene 0; keep Main.unity available after it.
        var main = ScenesDir + "/Main.unity";
        var list = File.Exists(main)
            ? new[] { new EditorBuildSettingsScene(ScenePath, true),
                      new EditorBuildSettingsScene(main, true) }
            : new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = list;

        AssetDatabase.SaveAssets();
        Debug.Log($"[ControllerSceneBuilder] Wrote {ScenePath}");
    }

    // ---- UI helpers ---------------------------------------------------------

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static GameObject CreateRow(Transform parent, string name, bool top)
    {
        var go = CreatePanel(parent, name, new Color(0f, 0f, 0f, 0.25f));
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot     = new Vector2(0.5f, top ? 1f : 0f);
        rect.sizeDelta = new Vector2(0f, 120f);
        rect.anchoredPosition = Vector2.zero;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 16f;
        layout.padding = new RectOffset(24, 24, 16, 16);
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return go;
    }

    static Button AddButton(Transform parent, string name, string label, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 84f);

        var img = go.AddComponent<Image>();
        img.color = color ?? BtnColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 150f;
        le.preferredWidth = 200f;

        AddLabel(go.transform, "Label", label, 28, Color.white, TextAnchor.MiddleCenter);
        return btn;
    }

    static Text AddLabel(Transform parent, string name, string text, int size, Color color,
                         TextAnchor anchor, float width = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        if (width > 0f)
        {
            rect.sizeDelta = new Vector2(width, 84f);
            go.AddComponent<LayoutElement>().preferredWidth = width;
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        var t = go.AddComponent<Text>();
        t.font = BuiltinFont();
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.text = text;
        return t;
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static Font BuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[ControllerSceneBuilder] {target.GetType().Name} has no field '{field}'"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRefArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[ControllerSceneBuilder] {target.GetType().Name} has no field '{field}'"); return; }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
