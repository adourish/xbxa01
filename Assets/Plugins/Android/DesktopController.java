package com.xbxa01.desktop;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.provider.Settings;
import android.text.TextUtils;
import android.util.Log;
import android.view.KeyEvent;

/**
 * The single seam between Unity (C#) and the two Android services.
 *
 * Unity's {@code AndroidJavaClass}/{@code AndroidJavaObject} calls land here; this
 * class forwards to the live {@link XbxAccessibilityService} / {@link XbxKeyboardService}
 * instances (or reports they're not enabled yet). Keeping the JNI surface in one small
 * facade means the service classes stay plain Android and Unity has exactly one thing
 * to bind to.
 */
public class DesktopController {
    private static final String TAG = "DesktopController";

    private final Activity activity;

    public DesktopController(Activity activity) {
        this.activity = activity;
    }

    // ---- readiness ----------------------------------------------------------

    public boolean isAccessibilityReady() { return XbxAccessibilityService.isRunning(); }
    public boolean isKeyboardReady()      { return XbxKeyboardService.isRunning(); }

    /** True if our a11y service is enabled in Secure settings (even before it binds). */
    public boolean isAccessibilityEnabled() {
        String flat = Settings.Secure.getString(
                activity.getContentResolver(),
                Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES);
        if (TextUtils.isEmpty(flat)) return false;
        String me = new ComponentName(activity, XbxAccessibilityService.class).flattenToString();
        return flat.contains(me);
    }

    /** Deep-link the user to the Accessibility settings to enable our service. */
    public void openAccessibilitySettings() {
        launch(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS));
    }

    /** Deep-link to input-method settings to enable + select our IME. */
    public void openKeyboardSettings() {
        launch(new Intent(Settings.ACTION_INPUT_METHOD_SETTINGS));
    }

    private void launch(Intent i) {
        i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        try { activity.startActivity(i); }
        catch (Exception e) { Log.e(TAG, "launch settings failed", e); }
    }

    // ---- pointer / gestures (forward to a11y service) -----------------------

    public void moveCursor(float dx, float dy) { with(s -> s.moveCursor(dx, dy)); }
    public void tap()                          { with(s -> s.tap(40)); }
    public void doubleTap()                    { with(XbxAccessibilityService::doubleTap); }
    public void drag(float dx, float dy)       { with(s -> s.drag(dx, dy, 200)); }
    public void scroll(float dx, float dy)     { with(s -> s.scroll(dx, dy)); }
    public void snapWindow(int region)         { with(s -> s.snapWindow(region)); }

    // ---- global actions -----------------------------------------------------

    public void home()          { with(XbxAccessibilityService::home); }
    public void back()          { with(XbxAccessibilityService::back); }
    public void recents()       { with(XbxAccessibilityService::recents); }
    public void notifications() { with(XbxAccessibilityService::notifications); }
    public void split()         { with(XbxAccessibilityService::split); }

    // ---- keyboard (forward to IME) ------------------------------------------
    //
    // These are the Unity-facing surface, so they return void: Unity's AndroidJavaObject
    // resolves a plain Call(...) as a void JNI method, and a boolean return type would
    // make GetMethodID miss and throw. The IME calls themselves still report success to
    // logcat for debugging.

    public void type(String text) {
        XbxKeyboardService ime = XbxKeyboardService.get();
        if (ime == null || !ime.commitText(text)) Log.w(TAG, "type dropped — IME not ready");
    }

    public void backspace() {
        XbxKeyboardService ime = XbxKeyboardService.get();
        if (ime == null || !ime.backspace()) Log.w(TAG, "backspace dropped — IME not ready");
    }

    public void enter()  { sendKey(KeyEvent.KEYCODE_ENTER); }
    public void tabKey() { sendKey(KeyEvent.KEYCODE_TAB); }

    public void sendKey(int keyCode) {
        XbxKeyboardService ime = XbxKeyboardService.get();
        if (ime == null || !ime.sendKey(keyCode)) Log.w(TAG, "sendKey dropped — IME not ready");
    }

    // ---- helper -------------------------------------------------------------

    private interface A11yAction { void run(XbxAccessibilityService s); }

    private void with(A11yAction action) {
        XbxAccessibilityService s = XbxAccessibilityService.get();
        if (s == null) { Log.w(TAG, "a11y service not enabled — action dropped"); return; }
        action.run(s);
    }
}
