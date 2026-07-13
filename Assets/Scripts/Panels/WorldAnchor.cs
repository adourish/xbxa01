using UnityEngine;

/// <summary>
/// Root transform for all world-locked content.
///
/// Strategy: the Main Camera's rotation tracks head orientation (IMU quaternion).
/// This GameObject counter-rotates by the same quaternion, so its children
/// appear stationary in world space even as the camera rotates.
///
/// In practice: WorldAnchor.rotation = Quaternion.Inverse(HeadTracker.HeadRotation)
/// Children of WorldAnchor (FloatingPanel, etc.) are placed at offsets in
/// "neutral head" space and remain visually fixed as the user turns their head.
///
/// Attach to: the WorldAnchor GameObject in the Main scene.
/// Requires: HeadTracker in the scene.
/// </summary>
public class WorldAnchor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeadTracker headTracker;

    [Header("Smoothing")]
    [Tooltip("Slerp speed for rotation smoothing. Higher = more responsive, lower = smoother.")]
    [SerializeField] [Range(1f, 60f)] private float smoothingSpeed = 20f;

    [Tooltip("Apply smoothing. Disable for lowest-latency mode.")]
    [SerializeField] private bool smoothing = true;

    private Quaternion _targetRotation = Quaternion.identity;

    void LateUpdate()
    {
        if (headTracker == null) return;

        _targetRotation = Quaternion.Inverse(headTracker.HeadRotation);

        if (smoothing)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetRotation,
                smoothingSpeed * Time.deltaTime
            );
        }
        else
        {
            transform.rotation = _targetRotation;
        }
    }

    void Reset()
    {
        // Auto-wire in Editor
        headTracker = FindObjectOfType<HeadTracker>();
    }
}
