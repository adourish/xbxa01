#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

/// <summary>
/// Editor-only build automation script.
/// Called from deploy.bat via Unity's -executeMethod flag.
///
/// Usage (headless):
///   Unity.exe -batchmode -quit -projectPath . -executeMethod BuildScript.BuildAndroiddebug
///   Unity.exe -batchmode -quit -projectPath . -executeMethod BuildScript.BuildAndroidrelease
/// </summary>
public static class BuildScript
{
    private const string AppIdentifier = "com.xbxa01.glassesvr";
    private const string AppName       = "XBXA01 AR";

    // Scene 0 is the desktop-mode controller (the product); Main.unity is the
    // world-locked renderer, kept as the mirror-mode fallback. See DESKTOP_MODE.md.
    private const string ControllerScene = "Assets/Scenes/Controller.unity";
    private const string FallbackScene   = "Assets/Scenes/Main.unity";

    private static readonly string[] Scenes = new[]
    {
        ControllerScene,
        FallbackScene,
    };

    public static void BuildAndroiddebug()  => Build(BuildOptions.Development | BuildOptions.AllowDebugging, "debug");
    public static void BuildAndroidrelease() => Build(BuildOptions.None, "release");

    static void Build(BuildOptions options, string variant)
    {
        // Read output path from command line args
        string outputPath = GetArg("-outputPath") ?? $"build/xbxa01_{variant}.apk";

        Debug.Log($"[BuildScript] Building {variant} → {outputPath}");

        // Scenes are generated, not committed. Build any that a fresh clone is missing.
        if (!File.Exists(FallbackScene))
        {
            Debug.Log($"[BuildScript] {FallbackScene} missing — generating via SceneBuilder.");
            SceneBuilder.BuildMainScene();
            AssetDatabase.Refresh();
        }
        if (!File.Exists(ControllerScene))
        {
            Debug.Log($"[BuildScript] {ControllerScene} missing — generating via ControllerSceneBuilder.");
            ControllerSceneBuilder.BuildControllerScene();
            AssetDatabase.Refresh();
        }

        // The scene and prefabs are built from code, so no material asset references
        // the uGUI shaders. Without a reference, Unity strips them from the build and
        // every Text/RawImage renders magenta (missing-shader colour). Force-include
        // them so panels and the debug HUD actually render.
        EnsureAlwaysIncludedShaders("UI/Default", "UI/Default Font", "Sprites/Default");

        // Player settings
        PlayerSettings.applicationIdentifier = AppIdentifier;
        PlayerSettings.productName           = AppName;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        // Unity 6 defaults the Android app entry to GameActivity. Our custom
        // AndroidManifest.xml and tools\deploy.bat both target the classic
        // com.unity3d.player.UnityPlayerActivity, so pin the entry to Activity —
        // otherwise the manifest merger conflicts and `am start ...UnityPlayerActivity`
        // has nothing to launch.
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel31;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

        // 120Hz target
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

        // Screen orientation
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // Ensure output dir exists
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = Scenes,
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            options          = options,
        });

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] Build FAILED: {report.summary.result}");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log($"[BuildScript] Build SUCCEEDED: {outputPath} " +
                      $"({report.summary.totalSize / 1024 / 1024} MB)");
            EditorApplication.Exit(0);
        }
    }

    static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    /// <summary>
    /// Add shaders to GraphicsSettings' "Always Included Shaders" so they survive a
    /// headless build even when no material asset references them (our scene is
    /// generated from code). Idempotent.
    /// </summary>
    static void EnsureAlwaysIncludedShaders(params string[] names)
    {
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var list = so.FindProperty("m_AlwaysIncludedShaders");

        foreach (var name in names)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[BuildScript] Shader not found, cannot include: {name}");
                continue;
            }

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    present = true;
                    break;
                }
            }
            if (present) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            Debug.Log($"[BuildScript] Always-included shader added: {name}");
        }

        so.ApplyModifiedProperties();
    }
}
#endif
