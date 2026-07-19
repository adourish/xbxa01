#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/Main.unity from scratch, plus the panel prefabs it needs.
///
/// The scene is generated rather than committed as YAML so it can be rebuilt
/// headlessly and reviewed as code. BuildScript expects Assets/Scenes/Main.unity
/// to exist; run this before the first build.
///
/// Usage:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod SceneBuilder.BuildMainScene
///   or in the Editor: Tools > XBXA01 > Rebuild Main Scene
/// </summary>
public static class SceneBuilder
{
    const string ScenesDir  = "Assets/Scenes";
    const string PrefabsDir = "Assets/Prefabs";
    const string ScenePath  = ScenesDir + "/Main.unity";

    // Panels are unit-sized RectTransforms; localScale carries the real dimensions,
    // so FloatingPanel.BaseScale (and the Main<->PiP swap) works in scale space.
    static readonly Vector3 MainScale = new Vector3(3.2f, 1.8f, 1f);
    static readonly Vector3 PipScale  = new Vector3(1.1f, 0.6f, 1f);

    // The app streamed into the main panel via VirtualDisplay (see SPEC §Panel Content
    // Sources). Settings is present on every device and generally launches on a
    // secondary display; change this to whatever app you want floating in the glasses.
    // If it refuses the secondary display, the panel falls back to its fill and logcat
    // says why — the app does not crash.
    const string MainAppPackage = "com.android.settings";

    [MenuItem("Tools/XBXA01/Rebuild Main Scene")]
    public static void BuildMainScene()
    {
        Directory.CreateDirectory(ScenesDir);
        Directory.CreateDirectory(PrefabsDir);

        var mainPrefab = CreatePanelPrefab("MainPanel", FloatingPanel.PanelType.Main, MainScale, Color.black, dimOnLookAway: true, appPackage: MainAppPackage);
        var pipPrefab  = CreatePanelPrefab("PiPPanel",  FloatingPanel.PanelType.PiP,  PipScale,  new Color(0.1f, 0.1f, 0.12f));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Camera. Static by design: WorldAnchor counter-rotates instead.
        // Rotating this as well would apply Inverse(head) twice (see SPEC).
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 100f;
        camGo.tag = "MainCamera";
        camGo.transform.position = Vector3.zero;
        camGo.transform.rotation = Quaternion.identity;
        camGo.AddComponent<AudioListener>();

        // --- Subsystems
        var appGo = new GameObject("AppController");
        var headTracker     = appGo.AddComponent<HeadTracker>();
        var displayDetector = appGo.AddComponent<DisplayDetector>();
        var panelManager    = appGo.AddComponent<PanelManager>();
        var appController   = appGo.AddComponent<AppController>();

        var anchorGo = new GameObject("WorldAnchor");
        var worldAnchor = anchorGo.AddComponent<WorldAnchor>();

        var phoneGo = new GameObject("PhoneController");
        var phoneController = phoneGo.AddComponent<PhoneController>();

        var debugOverlay = CreateDebugOverlay(headTracker, displayDetector, appController);

        // --- Wiring (fields are [SerializeField] private, so go through SerializedObject)
        SetRef(worldAnchor, "headTracker", headTracker);

        SetRef(panelManager, "worldAnchor",     worldAnchor);
        // Reload the prefab assets fresh from disk: the mainPrefab/pipPrefab handles
        // were captured before NewScene(..., Single), which invalidates them, so
        // assigning them directly serialized a null reference (the runtime NRE in
        // PanelManager.SpawnDefaultPanels). LoadAssetAtPath gives a live asset ref.
        var mainPrefabAsset = AssetDatabase.LoadAssetAtPath<FloatingPanel>(PrefabsDir + "/MainPanel.prefab");
        var pipPrefabAsset  = AssetDatabase.LoadAssetAtPath<FloatingPanel>(PrefabsDir + "/PiPPanel.prefab");
        SetRef(panelManager, "mainPanelPrefab", mainPrefabAsset);
        SetRef(panelManager, "pipPanelPrefab",  pipPrefabAsset);

        SetRef(phoneController, "panelManager", panelManager);
        SetRef(phoneController, "mainCamera",   cam);

        SetRef(appController, "headTracker",     headTracker);
        SetRef(appController, "displayDetector", displayDetector);
        SetRef(appController, "panelManager",    panelManager);
        SetRef(appController, "debugOverlay",    debugOverlay);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Make sure the scene is the one the build actually ships.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneBuilder] Wrote {ScenePath}");
    }

    /// <summary>
    /// A panel is a world-space Canvas on a unit RectTransform, with a BoxCollider
    /// so PhoneController's Physics.Raycast can actually select it — a world-space
    /// Canvas alone is not hit by Physics.Raycast.
    /// </summary>
    static FloatingPanel CreatePanelPrefab(string name, FloatingPanel.PanelType type, Vector3 scale, Color fill, bool dimOnLookAway = false, string appPackage = null)
    {
        var go = new GameObject(name, typeof(RectTransform));

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one;   // 1x1; localScale supplies real size
        go.transform.localScale = scale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<CanvasGroup>();

        var panel = go.AddComponent<FloatingPanel>();
        panel.panelType = type;
        panel.dimOnLookAway = dimOnLookAway;

        // Stream a live Android app into this panel via VirtualDisplay. Self-targets
        // this FloatingPanel; falls back to the flat fill below if the launch fails.
        if (!string.IsNullOrEmpty(appPackage))
        {
            var appWindow = go.AddComponent<AppWindow>();
            appWindow.packageName   = appPackage;
            appWindow.targetPanel   = panel;
            // Self-launch onto an untrusted virtual display is blocked by Android for a
            // non-system app; the launch comes from `adb shell am start --display <id>`
            // (tethered) or requires system signing. Don't attempt a doomed self-launch.
            appWindow.launchOnStart = false;
        }

        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 1f, 0.01f);

        // Content surface — swap for a RenderTexture later.
        var imgGo = new GameObject("Content", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;
        imgGo.AddComponent<RawImage>().color = fill;

        string path = $"{PrefabsDir}/{name}.prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        return saved.GetComponent<FloatingPanel>();
    }

    /// <summary>
    /// Screen-space debug HUD. SPEC put this on a world FloatingPanel, but a plain
    /// overlay canvas is far less fragile and mirrors to the glasses identically.
    /// </summary>
    static DebugOverlay CreateDebugOverlay(HeadTracker head, DisplayDetector display, AppController app)
    {
        var canvasGo = new GameObject("DebugOverlay", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var overlay = canvasGo.AddComponent<DebugOverlay>();

        var fps     = CreateLabel(canvasGo.transform, "FPS",     new Vector2(20f, -20f));
        var imu     = CreateLabel(canvasGo.transform, "IMU",     new Vector2(20f, -60f));
        var disp    = CreateLabel(canvasGo.transform, "Display", new Vector2(20f, -160f));
        var state   = CreateLabel(canvasGo.transform, "State",   new Vector2(20f, -220f));

        SetRef(overlay, "headTracker",     head);
        SetRef(overlay, "displayDetector", display);
        SetRef(overlay, "appController",   app);
        SetRef(overlay, "fpsText",         fps);
        SetRef(overlay, "imuText",         imu);
        SetRef(overlay, "displayText",     disp);
        SetRef(overlay, "stateText",       state);

        return overlay;
    }

    static Text CreateLabel(Transform parent, string name, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(0f, 1f);
        rect.pivot            = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(600f, 100f);

        var text = go.AddComponent<Text>();
        text.font      = BuiltinFont();
        text.fontSize  = 24;
        text.color     = Color.green;
        text.alignment = TextAnchor.UpperLeft;
        text.text      = name;
        return text;
    }

    static Font BuiltinFont()
    {
        // Renamed in 2022.x; fall back for older editors.
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[SceneBuilder] {target.GetType().Name} has no serialized field '{field}'");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
