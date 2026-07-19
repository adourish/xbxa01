package com.xbxa01.desktop;

import android.inputmethodservice.InputMethodService;
import android.util.Log;
import android.view.KeyEvent;
import android.view.View;
import android.view.inputmethod.InputConnection;

/**
 * Keyboard path for desktop mode.
 *
 * An IME is the one fully-supported way for a sideloaded app to send keystrokes to
 * *other* apps: when a text field is focused (on any display), Android routes an
 * {@link InputConnection} to whichever IME is selected. We commit text and dispatch
 * key events through that connection, so typing works across the desktop's windows.
 *
 * For the MVP this IME shows no on-screen keyboard of its own — the phone-side Unity
 * controller collects text and forwards it here via {@link DesktopController}, and we
 * commit it. A full soft-keyboard layout is a drop-in upgrade to onCreateInputView().
 */
public class XbxKeyboardService extends InputMethodService {
    private static final String TAG = "XbxIME";

    private static XbxKeyboardService sInstance;
    public static XbxKeyboardService get() { return sInstance; }
    public static boolean isRunning() { return sInstance != null; }

    @Override
    public void onCreate() {
        super.onCreate();
        sInstance = this;
        Log.i(TAG, "IME created");
    }

    @Override
    public void onDestroy() {
        if (sInstance == this) sInstance = null;
        super.onDestroy();
    }

    /** No visible keyboard for the MVP; the controller drives us programmatically. */
    @Override
    public View onCreateInputView() {
        return null;
    }

    /** Commit a run of text into the focused field. */
    public boolean commitText(String text) {
        InputConnection ic = getCurrentInputConnection();
        if (ic == null || text == null) return false;
        return ic.commitText(text, 1);
    }

    /** Delete one character before the cursor (backspace). */
    public boolean backspace() {
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return false;
        return ic.deleteSurroundingText(1, 0);
    }

    /** Send a raw key (Enter, Tab, arrows, Esc, ...) as down+up. */
    public boolean sendKey(int keyCode) {
        InputConnection ic = getCurrentInputConnection();
        if (ic == null) return false;
        long t = android.os.SystemClock.uptimeMillis();
        ic.sendKeyEvent(new KeyEvent(t, t, KeyEvent.ACTION_DOWN, keyCode, 0));
        ic.sendKeyEvent(new KeyEvent(t, t, KeyEvent.ACTION_UP, keyCode, 0));
        return true;
    }
}
