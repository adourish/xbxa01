using UnityEngine;

/// <summary>
/// Turns the phone touchscreen into a trackpad that drives the glasses desktop through
/// DesktopBridge. This replaces PhoneController (invisible world-panel gestures) with a
/// discoverable pad-plus-buttons surface — the fix for the old design's usability.
///
/// Only touches inside <see cref="trackpadZone"/> are treated as pad input, so the
/// button bar (handled by Unity UI on the same screen) keeps working normally.
///
/// Gesture map (see DESKTOP_MODE.md §5):
///   1-finger drag        → move cursor
///   1-finger tap         → click
///   1-finger double-tap  → double-click
///   long-press + drag    → grab & drag the window under the cursor
///   2-finger drag        → scroll
/// </summary>
public class TrackpadController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DesktopBridge bridge;
    [Tooltip("The touch area that acts as the trackpad. Touches outside it are ignored " +
             "(they belong to the button bar).")]
    [SerializeField] private RectTransform trackpadZone;

    [Header("Sensitivity")]
    [SerializeField] [Range(0.5f, 6f)] private float cursorSpeed = 2.2f;
    [SerializeField] [Range(0.5f, 6f)] private float scrollSpeed = 2.0f;

    [Header("Tap / hold tuning")]
    [SerializeField] private float tapMaxSeconds = 0.2f;
    [SerializeField] private float tapMaxMovePixels = 18f;
    [SerializeField] private float doubleTapWindow = 0.3f;
    [SerializeField] private float longPressSeconds = 0.35f;

    // Single-touch tracking
    private bool _tracking;
    private int _activeFingerId = -1;
    private Vector2 _touchStart;
    private float _touchStartTime;
    private float _movedDistance;
    private bool _longPressArmed;   // held long enough → subsequent moves are window drag
    private float _lastTapTime = -1f;

    void Awake()
    {
        if (bridge == null) bridge = DesktopBridge.Instance;
    }

    void Update()
    {
        if (bridge == null) bridge = DesktopBridge.Instance;
        if (bridge == null) return;

        int count = Input.touchCount;
        if (count == 0) { _tracking = false; _activeFingerId = -1; return; }

        if (count >= 2) { HandleTwoFinger(Input.GetTouch(0), Input.GetTouch(1)); return; }

        HandleOneFinger(Input.GetTouch(0));
    }

    void HandleOneFinger(Touch t)
    {
        switch (t.phase)
        {
            case TouchPhase.Began:
                if (!InZone(t.position)) { _tracking = false; return; }
                _tracking = true;
                _activeFingerId = t.fingerId;
                _touchStart = t.position;
                _touchStartTime = Time.unscaledTime;
                _movedDistance = 0f;
                _longPressArmed = false;
                break;

            case TouchPhase.Stationary:
            case TouchPhase.Moved:
                if (!_tracking || t.fingerId != _activeFingerId) return;
                _movedDistance += t.deltaPosition.magnitude;

                // Held in place past the long-press threshold → arm window-drag mode.
                if (!_longPressArmed &&
                    _movedDistance < tapMaxMovePixels &&
                    Time.unscaledTime - _touchStartTime >= longPressSeconds)
                {
                    _longPressArmed = true;
                }

                if (t.deltaPosition.sqrMagnitude > 0f)
                {
                    Vector2 d = t.deltaPosition;
                    if (_longPressArmed)
                        bridge.Drag(d.x * cursorSpeed, -d.y * cursorSpeed);
                    else
                        bridge.MoveCursor(d.x * cursorSpeed, -d.y * cursorSpeed);
                }
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (!_tracking || t.fingerId != _activeFingerId) return;
                _tracking = false;
                _activeFingerId = -1;

                bool wasTap = !_longPressArmed &&
                              _movedDistance < tapMaxMovePixels &&
                              Time.unscaledTime - _touchStartTime < tapMaxSeconds;
                if (wasTap) RegisterTap();
                break;
        }
    }

    void RegisterTap()
    {
        float now = Time.unscaledTime;
        if (now - _lastTapTime < doubleTapWindow)
        {
            bridge.DoubleTap();
            _lastTapTime = -1f;
        }
        else
        {
            bridge.Tap();
            _lastTapTime = now;
        }
    }

    void HandleTwoFinger(Touch a, Touch b)
    {
        // Only scroll if the gesture originated on the pad.
        if (!InZone(a.position) && !InZone(b.position)) return;

        Vector2 avg = (a.deltaPosition + b.deltaPosition) * 0.5f;
        if (avg.sqrMagnitude > 0f)
            bridge.Scroll(avg.x * scrollSpeed, -avg.y * scrollSpeed);
    }

    bool InZone(Vector2 screenPos)
    {
        if (trackpadZone == null) return true; // whole screen is the pad
        return RectTransformUtility.RectangleContainsScreenPoint(trackpadZone, screenPos);
    }
}
