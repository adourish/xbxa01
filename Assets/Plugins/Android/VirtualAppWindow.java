package com.xbxa01.appwindow;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.graphics.PixelFormat;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.view.Display;

import java.nio.ByteBuffer;

/**
 * Runs another Android app inside an off-screen VirtualDisplay and hands its frames
 * back to Unity as tightly-packed RGBA bytes, one texture upload per frame.
 *
 * This is the "panel content source" the design left open (README / SPEC): the app
 * is launched onto a private display we own, and each frame is copied out via
 * ImageReader so Unity can drop it onto a FloatingPanel.
 *
 * Why ImageReader and not a SurfaceTexture/OES bridge:
 *   ImageReader gives CPU-readable RGBA with no GL context sharing, no native .so, and
 *   no external-OES shader — so the whole thing compiles from a clean clone through
 *   Unity's normal Gradle build, matching the rest of this project. The cost is one
 *   ~w*h*4 copy per frame; fine at PiP sizes, acceptable for a main panel at 30fps.
 *   If that copy ever dominates, the zero-copy SurfaceTexture path is the upgrade.
 *
 * Threading: frames arrive on a private HandlerThread and are copied under `lock`.
 * Unity's main thread calls acquireFrame() to pull the newest one.
 */
public class VirtualAppWindow {
    private static final String TAG = "VirtualAppWindow";

    // DisplayManager.VIRTUAL_DISPLAY_FLAG_* — inlined to avoid the constant lookup.
    private static final int FLAG_PUBLIC           = 1;   // visible to other apps' windows
    private static final int FLAG_PRESENTATION     = 2;   // hosts app UI, not just mirrored pixels
    private static final int FLAG_OWN_CONTENT_ONLY = 8;   // never mirror the default display here

    private final Activity activity;
    private final int width;
    private final int height;

    private final HandlerThread readerThread;
    private final Handler readerHandler;
    private ImageReader imageReader;
    private VirtualDisplay virtualDisplay;

    private final Object lock = new Object();
    private byte[] latestFrame;      // tightly packed RGBA, width*height*4
    private boolean frameDirty;      // a new frame is waiting since the last acquire

    public VirtualAppWindow(Activity activity, int width, int height) {
        this.activity = activity;
        this.width  = width;
        this.height = height;

        readerThread = new HandlerThread("VirtualAppWindow-reader");
        readerThread.start();
        readerHandler = new Handler(readerThread.getLooper());

        int densityDpi = activity.getResources().getDisplayMetrics().densityDpi;

        imageReader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 2);
        imageReader.setOnImageAvailableListener(this::onImageAvailable, readerHandler);

        DisplayManager dm = (DisplayManager) activity.getSystemService(Activity.DISPLAY_SERVICE);
        virtualDisplay = dm.createVirtualDisplay(
                "xbxa01-appwindow",
                width, height, densityDpi,
                imageReader.getSurface(),
                FLAG_PUBLIC | FLAG_PRESENTATION | FLAG_OWN_CONTENT_ONLY);

        Log.i(TAG, "VirtualDisplay created " + width + "x" + height
                + " displayId=" + displayId());
    }

    public int getWidth()  { return width; }
    public int getHeight() { return height; }

    private int displayId() {
        Display d = virtualDisplay != null ? virtualDisplay.getDisplay() : null;
        return d != null ? d.getDisplayId() : Display.INVALID_DISPLAY;
    }

    /**
     * Launch an installed app by package name onto this window's display.
     *
     * Caveat worth knowing: from Android 10 on, only the display owner may start
     * activities on a display it created, and some apps refuse to run on a secondary
     * display at all (they throw SecurityException, or bounce back to the default
     * display). Your own activities and most standard apps work; a hardened app may
     * not. We log the failure rather than crash so the panel just stays blank.
     *
     * @return true if startActivity was dispatched without throwing.
     */
    public boolean launch(String packageName) {
        if (virtualDisplay == null) return false;

        Intent intent = activity.getPackageManager().getLaunchIntentForPackage(packageName);
        if (intent == null) {
            Log.e(TAG, "No launch intent for package: " + packageName
                    + " (is it installed, and is it listed in <queries>?)");
            return false;
        }
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_MULTIPLE_TASK);

        ActivityOptions options = ActivityOptions.makeBasic();
        options.setLaunchDisplayId(displayId());

        try {
            activity.startActivity(intent, options.toBundle());
            Log.i(TAG, "Launched " + packageName + " on display " + displayId());
            return true;
        } catch (Exception e) {
            Log.e(TAG, "Failed to launch " + packageName + " on secondary display", e);
            return false;
        }
    }

    // Runs on the reader thread. Grab the newest image, pack it tight, drop the rest.
    private void onImageAvailable(ImageReader reader) {
        Image image = null;
        try {
            image = reader.acquireLatestImage();
            if (image == null) return;

            Image.Plane plane = image.getPlanes()[0];
            ByteBuffer buffer = plane.getBuffer();
            int pixelStride = plane.getPixelStride();   // 4 for RGBA_8888
            int rowStride   = plane.getRowStride();     // may be > width*4 (row padding)
            int rowBytes    = width * pixelStride;

            byte[] out = new byte[rowBytes * height];
            if (rowStride == rowBytes) {
                buffer.get(out);                        // no padding — one bulk copy
            } else {
                for (int row = 0; row < height; row++) {
                    buffer.position(row * rowStride);
                    buffer.get(out, row * rowBytes, rowBytes);
                }
            }

            synchronized (lock) {
                latestFrame = out;
                frameDirty  = true;
            }
        } catch (Exception e) {
            Log.e(TAG, "onImageAvailable failed", e);
        } finally {
            if (image != null) image.close();
        }
    }

    /**
     * Return the newest frame as tightly-packed RGBA (width*height*4), or null if no
     * new frame has arrived since the last call. Called from Unity's main thread.
     */
    public byte[] acquireFrame() {
        synchronized (lock) {
            if (!frameDirty) return null;
            frameDirty = false;
            return latestFrame;
        }
    }

    public void release() {
        if (virtualDisplay != null) { virtualDisplay.release(); virtualDisplay = null; }
        if (imageReader != null)    { imageReader.close();      imageReader    = null; }
        readerThread.quitSafely();
        synchronized (lock) { latestFrame = null; frameDirty = false; }
        Log.i(TAG, "released");
    }
}
