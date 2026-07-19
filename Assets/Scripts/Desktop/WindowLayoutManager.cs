using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "3–4 configuration options" for the glasses desktop, recast for desktop mode.
///
/// Instead of two fixed Unity panels (the old Main/PiP), a preset is an arrangement of
/// *regions* on the real desktop. Applying a preset snaps the current windows into
/// those regions via the DesktopBridge (Android snap/split + title-bar drag gestures).
/// The apps inside the regions are real Android apps, moved between slots with the
/// snap-L / snap-R buttons — i.e. "move my real apps onto different screens".
///
/// Regions are normalized (0..1) in display space so they scale to the glasses'
/// 1920×1080. Because a sideloaded app can only *snap* (not pixel-place) windows, the
/// preset chooses the closest built-in snap for each region; the exact rect is design
/// intent and the tuning target once cross-display gestures are verified (caveat C2).
/// </summary>
public class WindowLayoutManager : MonoBehaviour
{
    public enum Preset { Focus, Duo, Trio, MainWithSide }

    [System.Serializable]
    public struct Slot
    {
        public string label;
        public Rect region;    // normalized 0..1 (x, y, width, height)
        public int snapRegion; // DesktopBridge.SnapWindow arg: 0=left,1=right,2=maximize
    }

    /// <summary>The four presets, in button order.</summary>
    public static readonly Dictionary<Preset, Slot[]> Presets = new Dictionary<Preset, Slot[]>
    {
        // One window, full desktop.
        [Preset.Focus] = new[]
        {
            new Slot { label = "Focus",  region = new Rect(0f,   0f, 1f,    1f), snapRegion = 2 },
        },
        // Two halves, side by side.
        [Preset.Duo] = new[]
        {
            new Slot { label = "Left",   region = new Rect(0f,   0f, 0.5f,  1f), snapRegion = 0 },
            new Slot { label = "Right",  region = new Rect(0.5f, 0f, 0.5f,  1f), snapRegion = 1 },
        },
        // Three columns (thirds). The middle column falls back to left-snap; exact
        // thirds need title-bar drag placement once C2 is verified.
        [Preset.Trio] = new[]
        {
            new Slot { label = "Left",   region = new Rect(0f,     0f, 0.333f, 1f), snapRegion = 0 },
            new Slot { label = "Center", region = new Rect(0.333f, 0f, 0.334f, 1f), snapRegion = 0 },
            new Slot { label = "Right",  region = new Rect(0.667f, 0f, 0.333f, 1f), snapRegion = 1 },
        },
        // Large primary (⅔) left + a helper (⅓) right.
        [Preset.MainWithSide] = new[]
        {
            new Slot { label = "Main",   region = new Rect(0f,     0f, 0.667f, 1f), snapRegion = 0 },
            new Slot { label = "Side",   region = new Rect(0.667f, 0f, 0.333f, 1f), snapRegion = 1 },
        },
    };

    public Preset Current { get; private set; } = Preset.Focus;

    [Tooltip("Delay between per-window snap gestures so the OS settles each one.")]
    [SerializeField] private float snapStepSeconds = 0.5f;

    private DesktopBridge Bridge => DesktopBridge.Instance;

    /// <summary>
    /// Apply a preset. For the multi-slot presets we cycle the visible windows with
    /// Recents between snaps, so successive foreground windows land in successive slots.
    /// </summary>
    public void Apply(Preset preset)
    {
        Current = preset;
        StopAllCoroutines();
        StartCoroutine(ApplyRoutine(Presets[preset]));
        Debug.Log($"[WindowLayout] Apply {preset} ({Presets[preset].Length} slot(s))");
    }

    public void ApplyByIndex(int i) => Apply((Preset)Mathf.Clamp(i, 0, 3));

    IEnumerator ApplyRoutine(Slot[] slots)
    {
        if (Bridge == null) yield break;

        for (int i = 0; i < slots.Length; i++)
        {
            Bridge.SnapWindow(slots[i].snapRegion);
            yield return new WaitForSeconds(snapStepSeconds);

            // Bring the next window forward to snap it into the next slot.
            if (i < slots.Length - 1)
            {
                Bridge.Recents();
                yield return new WaitForSeconds(snapStepSeconds);
            }
        }
    }

    /// <summary>Cycle to the next preset (a single button can rotate through all four).</summary>
    public void Next()
    {
        Apply((Preset)(((int)Current + 1) % 4));
    }
}
