using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Heads-up debug panel visible in development builds.
/// Displays: FPS, IMU quaternion, head euler angles, display mode.
/// Attach to the Debug FloatingPanel prefab's root.
/// Requires: Text components on child objects (wired in Inspector).
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeadTracker headTracker;
    [SerializeField] private DisplayDetector displayDetector;
    [SerializeField] private AppController appController;

    [Header("UI Text Elements")]
    [SerializeField] private Text fpsText;
    [SerializeField] private Text imuText;
    [SerializeField] private Text displayText;
    [SerializeField] private Text stateText;

    private float _fpsAccum;
    private int   _fpsFrames;
    private float _fpsCurrent;
    private float _fpsTimer;

    void Update()
    {
        UpdateFPS();
        UpdateLabels();
    }

    void UpdateFPS()
    {
        _fpsAccum  += Time.unscaledDeltaTime;
        _fpsFrames += 1;
        _fpsTimer  += Time.unscaledDeltaTime;

        if (_fpsTimer >= 0.5f)
        {
            _fpsCurrent = _fpsFrames / _fpsAccum;
            _fpsAccum   = 0f;
            _fpsFrames  = 0;
            _fpsTimer   = 0f;
        }
    }

    void UpdateLabels()
    {
        if (fpsText != null)
            fpsText.text = $"FPS: {_fpsCurrent:F0}";

        if (imuText != null && headTracker != null)
        {
            Quaternion q      = headTracker.HeadRotation;
            Vector3    euler  = q.eulerAngles;
            string     track  = headTracker.IsTracking ? "OK" : "NO SENSOR";
            imuText.text = $"IMU: {track}\n" +
                           $"Q ({q.x:F3}, {q.y:F3}, {q.z:F3}, {q.w:F3})\n" +
                           $"YPR ({euler.y:F1}°, {euler.x:F1}°, {euler.z:F1}°)";
        }

        if (displayText != null && displayDetector != null)
            displayText.text = $"Display: {displayDetector.CurrentMode}\n" +
                               $"Screens: {displayDetector.DisplayCount}";

        if (stateText != null && appController != null)
            stateText.text = $"State: {appController.State}";
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
