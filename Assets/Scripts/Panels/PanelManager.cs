using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the lifecycle of floating panels in the scene.
/// Spawns the default Main + PiP panels on startup, provides API to
/// add/remove panels at runtime.
///
/// Attach to: a persistent manager GameObject (e.g., "AppController").
/// Requires: WorldAnchor in the scene, panel prefabs assigned.
/// </summary>
public class PanelManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FloatingPanel mainPanelPrefab;
    [SerializeField] private FloatingPanel pipPanelPrefab;
    [SerializeField] private FloatingPanel debugPanelPrefab;

    [Header("Scene References")]
    [SerializeField] private WorldAnchor worldAnchor;

    [Header("Default Layout")]
    [SerializeField] private float panelDepth = 5f;

    private readonly List<FloatingPanel> _panels = new List<FloatingPanel>();

    public IReadOnlyList<FloatingPanel> Panels => _panels;

    void Start()
    {
        if (worldAnchor == null)
            worldAnchor = FindObjectOfType<WorldAnchor>();

        SpawnDefaultPanels();
    }

    void SpawnDefaultPanels()
    {
        // Main panel — centred
        var main = SpawnPanel(mainPanelPrefab, Vector2.zero, panelDepth);
        main.panelId = "main";
        main.AnimateIn(0.3f);

        // PiP panel — upper right
        var pip = SpawnPanel(pipPanelPrefab, new Vector2(3.2f, 1.6f), panelDepth);
        pip.panelId = "pip";
        pip.AnimateIn(0.45f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugPanelPrefab != null)
        {
            var dbg = SpawnPanel(debugPanelPrefab, new Vector2(-4f, -2f), panelDepth);
            dbg.panelId = "debug";
        }
#endif
    }

    public FloatingPanel SpawnPanel(FloatingPanel prefab, Vector2 offset, float depth)
    {
        if (prefab == null)
        {
            Debug.LogError("[PanelManager] Prefab is null.");
            return null;
        }

        FloatingPanel panel = Instantiate(prefab, worldAnchor.transform);
        panel.offset = offset;
        panel.depth  = depth;
        panel.ApplyLayout();
        _panels.Add(panel);
        return panel;
    }

    public void RemovePanel(string id)
    {
        var panel = _panels.Find(p => p.panelId == id);
        if (panel == null) return;

        _panels.Remove(panel);
        panel.AnimateOut();
    }

    public FloatingPanel GetPanel(string id) => _panels.Find(p => p.panelId == id);

    /// <summary>Swap main and PiP panels (double-tap gesture).</summary>
    public void SwapMainAndPiP()
    {
        var main = GetPanel("main");
        var pip  = GetPanel("pip");
        if (main == null || pip == null) return;

        Vector2 mainOffset = main.offset;
        float   mainDepth  = main.depth;
        Vector3 mainScale  = main.transform.localScale;

        main.SetOffset(pip.offset);
        main.SetDepth(pip.depth);
        main.transform.localScale = pip.transform.localScale;

        pip.SetOffset(mainOffset);
        pip.SetDepth(mainDepth);
        pip.transform.localScale = mainScale;
    }
}
