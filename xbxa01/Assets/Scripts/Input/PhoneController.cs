using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles touch input on the phone screen to control panels.
/// Since Pixel 9 is mirror-only, the phone shows the same image as the glasses.
/// Touch gestures are intercepted here and translated to panel commands.
///
/// Gestures:
///   Single tap          — select / focus panel
///   Double tap          — swap Main ↔ PiP
///   One-finger drag     — reposition selected panel (offset in WorldAnchor space)
///   Two-finger pinch    — resize selected panel
///   Two-finger spread   — change panel depth (dolly in/out)
/// </summary>
public class PhoneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PanelManager panelManager;
    [SerializeField] private Camera mainCamera;

    [Header("Drag Sensitivity")]
    [SerializeField] [Range(0.001f, 0.02f)] private float dragSensitivity = 0.006f;
    [SerializeField] [Range(0.01f, 0.1f)]   private float scaleSensitivity = 0.04f;

    [Header("Double Tap")]
    [SerializeField] private float doubleTapWindow = 0.3f;

    private FloatingPanel _selectedPanel;
    private Vector2 _lastDragPos;
    private float _lastTapTime = -1f;
    private int   _lastTapCount = 0;

    // Pinch state
    private float _initialPinchDist;
    private Vector3 _initialScale;

    void Update()
    {
        if (Input.touchCount == 0) return;

        if (Input.touchCount == 1)
            HandleSingleTouch(Input.GetTouch(0));
        else if (Input.touchCount == 2)
            HandlePinch(Input.GetTouch(0), Input.GetTouch(1));
    }

    void HandleSingleTouch(Touch touch)
    {
        switch (touch.phase)
        {
            case TouchPhase.Began:
                HandleTapBegin(touch);
                break;
            case TouchPhase.Moved:
                if (_selectedPanel != null)
                    DragPanel(touch.deltaPosition);
                break;
            case TouchPhase.Ended:
                if (_selectedPanel != null)
                    _selectedPanel.isDragging = false;
                break;
        }
    }

    void HandleTapBegin(Touch touch)
    {
        // Detect double-tap
        float now = Time.unscaledTime;
        if (now - _lastTapTime < doubleTapWindow)
        {
            panelManager.SwapMainAndPiP();
            _lastTapTime = -1f;
            return;
        }
        _lastTapTime = now;

        // Raycast to select a panel
        Ray ray = mainCamera.ScreenPointToRay(touch.position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            _selectedPanel = hit.collider.GetComponentInParent<FloatingPanel>();
            if (_selectedPanel != null)
            {
                _selectedPanel.isDragging = true;
                _lastDragPos = touch.position;
            }
        }
    }

    void DragPanel(Vector2 delta)
    {
        if (_selectedPanel == null || !_selectedPanel.draggable) return;

        // Map screen delta to WorldAnchor local-space offset
        Vector2 worldDelta = delta * dragSensitivity * _selectedPanel.depth;
        _selectedPanel.SetOffset(_selectedPanel.offset + worldDelta);
    }

    void HandlePinch(Touch t0, Touch t1)
    {
        if (_selectedPanel == null) return;

        float currentDist = Vector2.Distance(t0.position, t1.position);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            _initialPinchDist = currentDist;
            _initialScale = _selectedPanel.transform.localScale;
            return;
        }

        float ratio = currentDist / Mathf.Max(0.01f, _initialPinchDist);
        _selectedPanel.transform.localScale = _initialScale * ratio;
    }
}
