using UnityEngine;

/// <summary>
/// A single floating screen panel in the glasses display.
/// Panels are children of WorldAnchor so they appear world-locked.
///
/// Panel layout (default, in WorldAnchor local space):
///   Main panel   — position (0,   0,   5)  scale (3.2, 1.8, 1)  → ~147" equiv
///   PiP panel    — position (3.2, 1.6, 5)  scale (1.1, 0.6, 1)  → upper-right corner
///   Debug panel  — position (-4,  -2,  5)  scale (0.8, 0.5, 1)  → lower-left, dev only
///
/// The Canvas is World Space, rendered by the main camera.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class FloatingPanel : MonoBehaviour
{
    public enum PanelType { Main, PiP, Debug }

    [Header("Identity")]
    public PanelType panelType = PanelType.Main;
    public string panelId;

    [Header("Layout (WorldAnchor local space)")]
    [Tooltip("Distance from origin (meters). 5m gives comfortable angular size.")]
    public float depth = 5f;

    [Tooltip("Offset from centre (metres, X=right, Y=up).")]
    public Vector2 offset = Vector2.zero;

    [Header("Interaction")]
    [Tooltip("Allows PhoneController to drag this panel.")]
    public bool draggable = true;

    // Set by PhoneController during drag
    [HideInInspector] public bool isDragging;

    private Canvas _canvas;
    private RectTransform _rectTransform;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _rectTransform = GetComponent<RectTransform>();

        ApplyLayout();
    }

    public void ApplyLayout()
    {
        transform.localPosition = new Vector3(offset.x, offset.y, depth);
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>Move panel to a new offset (called by PhoneController on drag).</summary>
    public void SetOffset(Vector2 newOffset)
    {
        offset = newOffset;
        transform.localPosition = new Vector3(offset.x, offset.y, depth);
    }

    public void SetDepth(float newDepth)
    {
        depth = newDepth;
        transform.localPosition = new Vector3(offset.x, offset.y, depth);
    }

    /// <summary>Animate panel into view from scale 0.</summary>
    public void AnimateIn(float duration = 0.25f)
    {
        transform.localScale = Vector3.zero;
        LeanTween.scale(gameObject, Vector3.one, duration).setEaseOutBack();
    }

    /// <summary>Animate panel out before destroying.</summary>
    public void AnimateOut(float duration = 0.2f, System.Action onDone = null)
    {
        LeanTween.scale(gameObject, Vector3.zero, duration)
            .setEaseInBack()
            .setOnComplete(() => {
                onDone?.Invoke();
                Destroy(gameObject);
            });
    }
}
