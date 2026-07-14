using System.Collections;
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
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
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

    [Header("Focus Dimming (SPEC step 13)")]
    [Tooltip("Dim this panel as the head turns away from it. The camera never " +
             "rotates (WorldAnchor counter-rotates instead), so world-forward is " +
             "always the gaze direction and no camera reference is needed.")]
    public bool dimOnLookAway = false;

    [Tooltip("Degrees off-centre where dimming begins.")]
    public float dimStartAngle = 25f;

    [Tooltip("Degrees off-centre where the panel reaches its minimum alpha.")]
    public float dimEndAngle = 55f;

    [Range(0f, 1f)] public float dimmedAlpha = 0.35f;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    // The panel's authored size (Main ~3.2x1.8, PiP ~1.1x0.6). Captured before any
    // animation runs; animations must return to this, not to Vector3.one.
    private Vector3 _baseScale = Vector3.one;

    private Coroutine _scaleRoutine;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvasGroup = GetComponent<CanvasGroup>();

        _baseScale = transform.localScale;

        ApplyLayout();
    }

    void Update()
    {
        if (!dimOnLookAway) return;

        float angle = Vector3.Angle(Vector3.forward, transform.position);
        float t = Mathf.InverseLerp(dimStartAngle, dimEndAngle, angle);
        _canvasGroup.alpha = Mathf.Lerp(1f, dimmedAlpha, t);
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

    /// <summary>
    /// The panel's authored (full) scale. Swapping panels exchanges these.
    /// </summary>
    public Vector3 BaseScale
    {
        get => _baseScale;
        set => _baseScale = value;
    }

    /// <summary>Animate panel into view from scale 0 up to its authored scale.</summary>
    public void AnimateIn(float duration = 0.25f, float delay = 0f)
    {
        // Zero synchronously so a zero-delay call (Main panel) actually starts
        // from invisible instead of tweening from-baseScale to baseScale.
        transform.localScale = Vector3.zero;
        Restart(ScaleTo(_baseScale, duration, delay, EaseOutBack, null));
    }

    /// <summary>Animate panel out, then destroy it.</summary>
    public void AnimateOut(float duration = 0.2f, System.Action onDone = null)
    {
        Restart(ScaleTo(Vector3.zero, duration, 0f, EaseInBack, () =>
        {
            onDone?.Invoke();
            Destroy(gameObject);
        }));
    }

    void Restart(IEnumerator routine)
    {
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(routine);
    }

    IEnumerator ScaleTo(Vector3 target, float duration, float delay,
                        System.Func<float, float> ease, System.Action onDone)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 from = transform.localScale;

        if (duration <= 0f)
        {
            transform.localScale = target;
        }
        else
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.localScale = Vector3.LerpUnclamped(from, target, ease(t / duration));
                yield return null;
            }
            transform.localScale = target;
        }

        _scaleRoutine = null;
        onDone?.Invoke();
    }

    // Overshoot easings, equivalent to LeanTween's easeOutBack / easeInBack.
    const float BackOvershoot = 1.70158f;

    static float EaseOutBack(float t)
    {
        float s = BackOvershoot;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    static float EaseInBack(float t)
    {
        float s = BackOvershoot;
        return t * t * ((s + 1f) * t - s);
    }
}
