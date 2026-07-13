#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
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

    private static readonly string[] Scenes = new[]
    {
        "Assets/Scenes/Main.unity",
    };

    public static void BuildAndroiddebug()  => Build(BuildOptions.Development | BuildOptions.AllowDebugging, "debug");
    public static void BuildAndroidrelease() => Build(BuildOptions.None, "release");

    static void Build(BuildOptions options, string variant)
    {
        // Read output path from command line args
        string outputPath = GetArg("-outputPath") ?? $"build/xbxa01_{variant}.apk";

        Debug.Log($"[BuildScript] Building {variant} → {outputPath}");

        // Player settings
        PlayerSettings.applicationIdentifier = AppIdentifier;
        PlayerSettings.productName           = AppName;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
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
}
#endif
