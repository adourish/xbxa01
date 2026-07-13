using UnityEngine;

/// <summary>
/// Reads Android TYPE_ROTATION_VECTOR sensor (1000Hz hardware on xbxa01 IMU,
/// sampled at SENSOR_DELAY_GAME ~50Hz) and exposes a Unity Quaternion.
///
/// The Android rotation vector uses the East-North-Up coordinate frame;
/// Unity uses a left-handed Y-up frame. The coordinate remap below converts:
///   Android: X=East, Y=North, Z=Up  (right-hand)
///   Unity:   X=Right, Y=Up, Z=Forward (left-hand)
///
/// Swap: androidY→unityZ, androidZ→unityY, flip Z sign.
/// </summary>
public class HeadTracker : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _sensorManager;
    private AndroidJavaObject _rotationSensor;
    private IMUListener _listener;
#endif

    // Head rotation relative to the neutral pose, updated on the sensor thread.
    private Quaternion _headRotation = Quaternion.identity;

    // Inverse of the raw rotation captured at calibration. The sensor reports an
    // absolute orientation against ENU (i.e. magnetic north), so without this the
    // panels would anchor to north rather than to wherever the user is facing at
    // launch. Recentering makes HeadRotation identity at the neutral pose.
    private Quaternion _neutralInverse = Quaternion.identity;
    private bool _needsRecenter = true;

    private readonly object _lock = new object();

    public Quaternion HeadRotation
    {
        get { lock (_lock) { return _headRotation; } }
    }

    public bool IsTracking { get; private set; }

    /// <summary>
    /// Treat the next sensor sample as the neutral (forward) pose.
    /// Call on startup and whenever the user asks to re-centre.
    /// </summary>
    public void Recenter()
    {
        lock (_lock) { _needsRecenter = true; }
    }

    // Registration lives in OnEnable/OnDisable, not Start/OnDestroy: Unity does not
    // re-run Start() when a component is re-enabled, and OnDestroy does not fire on
    // an app pause. Putting it here is what makes pause/resume actually re-register.
    void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InitializeSensor();
#else
        IsTracking = false;
        Debug.LogWarning("[HeadTracker] IMU only available on Android device.");
#endif
    }

    void OnDisable()
    {
        Shutdown();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void InitializeSensor()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            _sensorManager = activity.Call<AndroidJavaObject>("getSystemService", "sensor");

            if (_sensorManager == null)
            {
                Debug.LogError("[HeadTracker] Could not get SensorManager.");
                return;
            }

            // Sensor.TYPE_ROTATION_VECTOR = 11
            _rotationSensor = _sensorManager.Call<AndroidJavaObject>("getDefaultSensor", 11);

            if (_rotationSensor == null)
            {
                Debug.LogError("[HeadTracker] Rotation vector sensor not available on this device.");
                return;
            }

            _listener = new IMUListener(this);
            // SensorManager.SENSOR_DELAY_GAME = 1  (~20ms poll, 50Hz)
            bool ok = _sensorManager.Call<bool>("registerListener", _listener, _rotationSensor, 1);
            IsTracking = ok;
            Debug.Log($"[HeadTracker] Sensor registered: {ok}");
        }
    }
#endif

    /// <summary>
    /// Called from IMUListener (may be on a non-Unity thread — guarded by lock).
    /// values[] = [x, y, z, w] of rotation vector (unit quaternion subset).
    /// w may be absent on older APIs; compute from x,y,z if needed.
    /// </summary>
    public void UpdateRotation(float[] values)
    {
        if (values == null || values.Length < 3) return;

        float ax = values[0];
        float ay = values[1];
        float az = values[2];
        float aw = values.Length >= 4 ? values[3] : Mathf.Sqrt(Mathf.Max(0f, 1f - ax*ax - ay*ay - az*az));

        // Remap Android ENU → Unity left-hand Y-up
        float ux =  ax;
        float uy =  az;
        float uz =  ay;
        float uw = -aw; // flip w to reverse handedness

        var raw = new Quaternion(ux, uy, uz, uw);

        lock (_lock)
        {
            if (_needsRecenter)
            {
                _neutralInverse = Quaternion.Inverse(raw);
                _needsRecenter = false;
            }

            // Relative to the neutral pose, so identity == "facing forward".
            _headRotation = _neutralInverse * raw;
        }
    }

    void OnDestroy()
    {
        Shutdown();
    }

    void Shutdown()
    {
        IsTracking = false;

        // Re-centre on the next sample, so resuming in a new orientation
        // does not snap the panels to a stale neutral pose.
        lock (_lock) { _needsRecenter = true; }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (_sensorManager != null && _listener != null)
        {
            _sensorManager.Call("unregisterListener", _listener);
            Debug.Log("[HeadTracker] Sensor unregistered.");
        }

        _sensorManager?.Dispose();
        _rotationSensor?.Dispose();
        _sensorManager  = null;
        _rotationSensor = null;
        _listener       = null;
#endif
    }
}
