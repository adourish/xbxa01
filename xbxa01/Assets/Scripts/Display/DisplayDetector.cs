using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Queries Android DisplayManager to detect connected displays and logs
/// whether the xbxa01 glasses are visible as a secondary display.
///
/// On Pixel 9, USB-C output is mirror-only — the glasses will NOT appear
/// as a separate display in DisplayManager; Unity renders to the single
/// mirrored framebuffer. This class documents that reality and exposes
/// DisplayMode so the app can adapt UI layout accordingly.
/// </summary>
public class DisplayDetector : MonoBehaviour
{
    public enum DisplayMode
    {
        Unknown,
        PhoneOnly,          // No external display detected
        MirrorToGlasses,    // Pixel 9 mirror mode — one logical display
        ExtendedToGlasses,  // True secondary display (future Android XR / hub)
    }

    public DisplayMode CurrentMode { get; private set; } = DisplayMode.Unknown;
    public int DisplayCount { get; private set; } = 1;

    void Start()
    {
        DetectDisplays();
    }

    void DetectDisplays()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        DetectAndroid();
#else
        CurrentMode = DisplayMode.PhoneOnly;
        Debug.Log("[DisplayDetector] Running in Editor — display detection skipped.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void DetectAndroid()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var displayManager = activity.Call<AndroidJavaObject>("getSystemService", "display"))
        {
            if (displayManager == null)
            {
                Debug.LogError("[DisplayDetector] Could not get DisplayManager.");
                CurrentMode = DisplayMode.Unknown;
                return;
            }

            // DisplayManager.DISPLAY_CATEGORY_PRESENTATION = "android.hardware.display.category.PRESENTATION"
            AndroidJavaObject[] displays = displayManager.Call<AndroidJavaObject[]>(
                "getDisplays", "android.hardware.display.category.PRESENTATION");

            DisplayCount = (displays != null ? displays.Length : 0) + 1; // +1 for primary

            if (displays == null || displays.Length == 0)
            {
                // No presentation display found — Pixel 9 mirror mode expected
                CurrentMode = DisplayMode.MirrorToGlasses;
                Debug.Log("[DisplayDetector] No secondary display — assuming mirror mode (Pixel 9).");
            }
            else
            {
                CurrentMode = DisplayMode.ExtendedToGlasses;
                foreach (var d in displays)
                {
                    int id    = d.Call<int>("getDisplayId");
                    string name = d.Call<string>("getName");
                    Debug.Log($"[DisplayDetector] Secondary display #{id}: {name}");
                }
            }

            Debug.Log($"[DisplayDetector] Mode={CurrentMode}, displays={DisplayCount}");
        }
    }
#endif
}
