package com.xbxa01.desktop;

import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.GestureDescription;
import android.content.Context;
import android.graphics.Path;
import android.graphics.PixelFormat;
import android.hardware.display.DisplayManager;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Display;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.view.accessibility.AccessibilityEvent;
import android.widget.ImageView;

/**
 * The engine that turns the phone trackpad into desktop control.
 *
 * A sideloaded app cannot move Android's real pointer or inject raw input into other
 * apps, but an enabled AccessibilityService can (a) dispatch synthetic gestures and
 * (b) perform global actions (Home/Back/Recents/...). We combine those with our own
 * cursor sprite drawn as a TYPE_ACCESSIBILITY_OVERLAY (needs no SYSTEM_ALERT_WINDOW):
 *
 *   trackpad delta ─► move our cursor sprite ─► dispatchGesture(tap) at the sprite
 *
 * Cross-display note (DESKTOP_MODE.md caveat C2): to drive the *external* desktop
 * display we build the overlay + gesture context against that display, not the phone
 * display. targetExternalDisplay() picks the first non-default display; if none is
 * present we fall back to the default so the service still works in mirror/dev.
 *
 * Unity never touches this class directly — it calls the static facade in
 * {@link DesktopController}, which forwards to the single live instance held here.
 */
public class XbxAccessibilityService extends AccessibilityService {
    private static final String TAG = "XbxA11y";

    /** The live instance, or null when the service is not enabled. */
    private static XbxAccessibilityService sInstance;

    public static XbxAccessibilityService get() { return sInstance; }
    public static boolean isRunning() { return sInstance != null; }

    private final Handler main = new Handler(Looper.getMainLooper());

    // Soft cursor sprite drawn on the desktop display.
    private WindowManager overlayWm;
    private View cursorView;
    private WindowManager.LayoutParams cursorLp;
    private int displayW = 1920, displayH = 1080;

    // Cursor position in the target display's pixels.
    private float cursorX = 960f, cursorY = 540f;

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        sInstance = this;
        main.post(this::attachCursor);
        Log.i(TAG, "connected");
    }

    @Override
    public void onDestroy() {
        detachCursor();
        if (sInstance == this) sInstance = null;
        super.onDestroy();
    }

    @Override public void onAccessibilityEvent(AccessibilityEvent event) { /* unused */ }
    @Override public void onInterrupt() { }

    // ---- cursor overlay -----------------------------------------------------

    /** Pick the external desktop display; fall back to default when none. */
    private Context targetDisplayContext() {
        DisplayManager dm = (DisplayManager) getSystemService(Context.DISPLAY_SERVICE);
        Display target = null;
        for (Display d : dm.getDisplays()) {
            if (d.getDisplayId() != Display.DEFAULT_DISPLAY) { target = d; break; }
        }
        if (target == null) target = dm.getDisplay(Display.DEFAULT_DISPLAY);

        android.graphics.Point size = new android.graphics.Point();
        target.getRealSize(size);
        if (size.x > 0 && size.y > 0) { displayW = size.x; displayH = size.y; }

        return createDisplayContext(target);
    }

    private void attachCursor() {
        try {
            Context ctx = targetDisplayContext();
            overlayWm = (WindowManager) ctx.getSystemService(Context.WINDOW_SERVICE);

            ImageView v = new ImageView(ctx);
            v.setImageDrawable(buildCursorDrawable());
            cursorView = v;

            cursorLp = new WindowManager.LayoutParams(
                    48, 48,
                    WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
                    WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                        | WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE
                        | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
                    PixelFormat.TRANSLUCENT);
            cursorLp.gravity = Gravity.TOP | Gravity.START;
            cursorX = displayW / 2f;
            cursorY = displayH / 2f;
            positionCursor();

            overlayWm.addView(cursorView, cursorLp);
            Log.i(TAG, "cursor attached on " + displayW + "x" + displayH);
        } catch (Exception e) {
            Log.e(TAG, "attachCursor failed (see caveat C2)", e);
        }
    }

    private void detachCursor() {
        try {
            if (overlayWm != null && cursorView != null) overlayWm.removeView(cursorView);
        } catch (Exception ignored) {
        } finally {
            cursorView = null;
            overlayWm = null;
        }
    }

    private android.graphics.drawable.Drawable buildCursorDrawable() {
        android.graphics.drawable.GradientDrawable g = new android.graphics.drawable.GradientDrawable();
        g.setShape(android.graphics.drawable.GradientDrawable.OVAL);
        g.setColor(0xCCFFFFFF);
        g.setStroke(3, 0xFF1E88E5);
        return g;
    }

    private void positionCursor() {
        if (cursorLp == null) return;
        cursorLp.x = Math.round(cursorX - cursorLp.width / 2f);
        cursorLp.y = Math.round(cursorY - cursorLp.height / 2f);
    }

    // ---- API surface called from DesktopController --------------------------

    /** Move the soft cursor by a delta (trackpad drag). */
    public void moveCursor(final float dx, final float dy) {
        main.post(() -> {
            cursorX = clamp(cursorX + dx, 0, displayW);
            cursorY = clamp(cursorY + dy, 0, displayH);
            positionCursor();
            if (overlayWm != null && cursorView != null) {
                try { overlayWm.updateViewLayout(cursorView, cursorLp); } catch (Exception ignored) {}
            }
        });
    }

    /** Tap at the current cursor point. durationMs ~40 for a click. */
    public void tap(int durationMs) {
        dispatchAt(cursorX, cursorY, cursorX, cursorY, Math.max(1, durationMs), 0);
    }

    public void doubleTap() {
        tap(40);
        main.postDelayed(() -> tap(40), 120);
    }

    /** Drag from the cursor by (dx,dy) — used to grab & move a window. */
    public void drag(float dx, float dy, int durationMs) {
        float x2 = clamp(cursorX + dx, 0, displayW);
        float y2 = clamp(cursorY + dy, 0, displayH);
        dispatchAt(cursorX, cursorY, x2, y2, Math.max(60, durationMs), 120);
        cursorX = x2; cursorY = y2;
        moveCursor(0, 0);
    }

    /** Two-finger-style scroll rendered as a swipe at the cursor. */
    public void scroll(float dx, float dy) {
        float x2 = clamp(cursorX - dx, 0, displayW);
        float y2 = clamp(cursorY - dy, 0, displayH);
        dispatchAt(cursorX, cursorY, x2, y2, 120, 0);
    }

    /**
     * Snap-drag: grab the window under the cursor and fling it toward an edge region so
     * the OS snaps it. region: 0=left half, 1=right half, 2=maximize (drag to top).
     */
    public void snapWindow(int region) {
        float toX, toY;
        switch (region) {
            case 0:  toX = 4;             toY = displayH / 2f; break; // left edge
            case 1:  toX = displayW - 4;  toY = displayH / 2f; break; // right edge
            default: toX = displayW / 2f; toY = 4;             break; // top → maximize
        }
        // long-press to grab the title bar, then drag to the edge.
        dispatchAt(cursorX, cursorY, toX, toY, 350, 400);
    }

    private void dispatchAt(float x1, float y1, float x2, float y2, int durationMs, int startDelayMs) {
        try {
            Path p = new Path();
            p.moveTo(x1, y1);
            if (x1 != x2 || y1 != y2) p.lineTo(x2, y2);
            GestureDescription.StrokeDescription stroke =
                    new GestureDescription.StrokeDescription(p, startDelayMs, durationMs);
            GestureDescription gesture = new GestureDescription.Builder().addStroke(stroke).build();
            boolean ok = dispatchGesture(gesture, null, main);
            if (!ok) Log.w(TAG, "dispatchGesture rejected (caveat C2 — cross-display)");
        } catch (Exception e) {
            Log.e(TAG, "dispatchAt failed", e);
        }
    }

    // ---- global actions -----------------------------------------------------

    public void home()          { performGlobalAction(GLOBAL_ACTION_HOME); }
    public void back()          { performGlobalAction(GLOBAL_ACTION_BACK); }
    public void recents()       { performGlobalAction(GLOBAL_ACTION_RECENTS); }
    public void notifications() { performGlobalAction(GLOBAL_ACTION_NOTIFICATIONS); }

    /** Enter split-screen (API 32+); older builds ignore the unknown constant. */
    public void split() {
        try { performGlobalAction(GLOBAL_ACTION_TOGGLE_SPLIT_SCREEN); }
        catch (Throwable t) { Log.w(TAG, "split unsupported on this API"); }
    }

    private static float clamp(float v, float lo, float hi) {
        return v < lo ? lo : (v > hi ? hi : v);
    }
}
