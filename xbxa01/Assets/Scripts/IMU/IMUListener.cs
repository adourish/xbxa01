using UnityEngine;

/// <summary>
/// Android SensorEventListener proxy — bridges Java callbacks to HeadTracker.
/// Lives on the Android sensor thread; UpdateRotation is thread-safe.
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class IMUListener : AndroidJavaProxy
{
    private readonly HeadTracker _tracker;

    public IMUListener(HeadTracker tracker)
        : base("android.hardware.SensorEventListener")
    {
        _tracker = tracker;
    }

    // Called by Android runtime when a new sensor sample arrives.
    void onSensorChanged(AndroidJavaObject sensorEvent)
    {
        // Retrieve the float[] values field via JNI directly to avoid
        // boxing overhead on the hot path (~50 calls/sec).
        IntPtr eventPtr = sensorEvent.GetRawObject();
        IntPtr classPtr  = AndroidJNI.GetObjectClass(eventPtr);
        IntPtr fieldId   = AndroidJNI.GetFieldID(classPtr, "values", "[F");
        IntPtr arrayPtr  = AndroidJNI.GetObjectField(eventPtr, fieldId);

        float[] values = AndroidJNI.FromFloatArray(arrayPtr);
        _tracker.UpdateRotation(values);
    }

    void onAccuracyChanged(AndroidJavaObject sensor, int accuracy)
    {
        Debug.Log($"[IMUListener] Sensor accuracy changed: {accuracy}");
    }
}
#else
// Stub so the project compiles in the Editor.
public class IMUListener
{
    public IMUListener(HeadTracker tracker) { }
}
#endif
