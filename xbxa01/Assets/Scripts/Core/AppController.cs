using UnityEngine;

/// <summary>
/// Top-level bootstrap controller.
/// Initialises subsystems in order, manages app state, handles
/// Android lifecycle events (pause/resume for sensor de/re-registration).
///
/// State machine:
///   Initialising → WaitingForIMU → Running → Paused
/// </summary>
public class AppController : MonoBehaviour
{
    public enum AppState { Initialising, WaitingForIMU, Running, Paused }

    [Header("Subsystems")]
    [SerializeField] private HeadTracker headTracker;
    [SerializeField] private DisplayDetector displayDetector;
    [SerializeField] private PanelManager panelManager;
    [SerializeField] private DebugOverlay debugOverlay;

    [Header("Settings")]
    [SerializeField] [Range(60, 120)] private int targetFrameRate = 120;
    [Tooltip("Keep screen on while running")]
    [SerializeField] private bool keepScreenOn = true;

    public AppState State { get; private set; } = AppState.Initialising;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        Screen.sleepTimeout = keepScreenOn ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

        // Force landscape for glasses 1920×1080
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        Debug.Log($"[AppController] Init. Target FPS={targetFrameRate}");
    }

    void Start()
    {
        Transition(AppState.WaitingForIMU);
    }

    void Update()
    {
        if (State == AppState.WaitingForIMU)
        {
            if (headTracker.IsTracking || Application.isEditor)
                Transition(AppState.Running);
        }

        // Back button → exit
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
    }

    void Transition(AppState next)
    {
        State = next;
        Debug.Log($"[AppController] → {next}");

        switch (next)
        {
            case AppState.Running:
                if (debugOverlay != null)
                    debugOverlay.SetVisible(Application.isEditor || Debug.isDebugBuild);
                break;
        }
    }

    void OnApplicationPause(bool paused)
    {
        // Android lifecycle — sensor is unregistered in HeadTracker.OnDestroy;
        // re-registration on resume requires re-creating the listener.
        // Simplest approach: restart the HeadTracker component.
        if (!paused && State == AppState.Running)
        {
            headTracker.enabled = false;
            headTracker.enabled = true;
        }

        State = paused ? AppState.Paused : AppState.Running;
        Debug.Log($"[AppController] OnApplicationPause({paused}) → {State}");
    }
}
