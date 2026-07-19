package com.xbxa01.appwindow;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.PixelFormat;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.media.projection.MediaProjection;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;

import java.nio.ByteBuffer;

/**
 * Whole-screen capture fallback for panel content, via Android's MediaProjection API
 * (SPEC §Known limits: "the alternative is MediaProjection ... shown on a panel").
 *
 * Unlike VirtualAppWindow (a display we own, with an app launched onto it), this
 * mirrors whatever is already on the device's default display — no cross-app
 * setLaunchDisplayId call, so it works from an unsigned, untethered app. The
 * trade-off is coarser: one mirrored screen, not an independently addressable app
 * window, and it needs a one-time user consent dialog rather than running silently.
 *
 * Static, not an instance class like VirtualAppWindow: the consent grant has to
 * survive a hop through a separate Activity (the only thing that can call
 * startActivityForResult) and a foreground Service (required to hold the projection
 * on Android 10+), so no single object owns this end-to-end the way VirtualAppWindow
 * owns its VirtualDisplay. Unity polls this bridge the same way AppWindow polls
 * VirtualAppWindow.acquireFrame().
 */
public final class ScreenCaptureBridge {
    private static final String TAG = "ScreenCaptureBridge";

    // DisplayManager.VIRTUAL_DISPLAY_FLAG_PUBLIC — inlined to match VirtualAppWindow's
    // style. This is the flag the platform's own screen-capture samples use with
    // MediaProjection.createVirtualDisplay.
    private static final int FLAG_PUBLIC = 1;

    static final int REQUEST_CODE = 9001;

    private static int requestedWidth  = 1280;
    private static int requestedHeight = 720;

    private static HandlerThread readerThread;
    private static Handler readerHandler;
    private static ImageReader imageReader;
    private static VirtualDisplay virtualDisplay;
    private static MediaProjection mediaProjection;

    private static final Object lock = new Object();
    private static byte[] latestFrame;
    private static boolean frameDirty;

    private static volatile boolean capturing;
    private static volatile boolean lastRequestDenied;

    private ScreenCaptureBridge() {}

    /** Called from Unity's main thread. Shows the one-time system consent dialog. */
    public static void requestPermission(Activity activity, int width, int height) {
        requestedWidth  = width;
        requestedHeight = height;
        lastRequestDenied = false;
        activity.startActivity(new Intent(activity, MediaProjectionRequestActivity.class));
    }

    public static boolean isCapturing() { return capturing; }

    /** True once, right after a denied/cancelled consent dialog; clears itself on read. */
    public static boolean consumeDenied() {
        boolean d = lastRequestDenied;
        lastRequestDenied = false;
        return d;
    }

    // Called by MediaProjectionRequestActivity.onActivityResult on the UI thread.
    static void onPermissionResult(Context context, int resultCode, Intent data) {
        if (data == null) {
            lastRequestDenied = true;
            Log.w(TAG, "MediaProjection consent denied or cancelled.");
            return;
        }
        Intent serviceIntent = new Intent(context, ScreenCaptureService.class);
        serviceIntent.putExtra(ScreenCaptureService.EXTRA_RESULT_CODE, resultCode);
        serviceIntent.putExtra(ScreenCaptureService.EXTRA_RESULT_DATA, data);
        context.startForegroundService(serviceIntent);
    }

    // Called by ScreenCaptureService once it holds the foreground-service grant.
    static void start(Context context, MediaProjection projection) {
        stop(); // idempotent; replaces any prior capture

        mediaProjection = projection;
        int width  = requestedWidth;
        int height = requestedHeight;
        int densityDpi = context.getResources().getDisplayMetrics().densityDpi;

        readerThread = new HandlerThread("ScreenCaptureBridge-reader");
        readerThread.start();
        readerHandler = new Handler(readerThread.getLooper());

        imageReader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 2);
        imageReader.setOnImageAvailableListener(ScreenCaptureBridge::onImageAvailable, readerHandler);

        virtualDisplay = mediaProjection.createVirtualDisplay(
                "xbxa01-screencapture", width, height, densityDpi,
                FLAG_PUBLIC, imageReader.getSurface(), null, readerHandler);

        capturing = true;
        Log.i(TAG, "MediaProjection capture started " + width + "x" + height);
    }

    // Runs on the reader thread. Grab the newest image, pack it tight, drop the rest.
    // Mirrors VirtualAppWindow.onImageAvailable — same ImageReader row-stride handling.
    private static void onImageAvailable(ImageReader reader) {
        Image image = null;
        try {
            image = reader.acquireLatestImage();
            if (image == null) return;

            Image.Plane plane = image.getPlanes()[0];
            ByteBuffer buffer = plane.getBuffer();
            int pixelStride = plane.getPixelStride();   // 4 for RGBA_8888
            int rowStride   = plane.getRowStride();     // may be > width*4 (row padding)
            int width  = requestedWidth;
            int height = requestedHeight;
            int rowBytes = width * pixelStride;

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
    public static byte[] acquireFrame() {
        synchronized (lock) {
            if (!frameDirty) return null;
            frameDirty = false;
            return latestFrame;
        }
    }

    /** Called from Unity's main thread (OnDestroy) or ScreenCaptureService.onDestroy. */
    public static void stop() {
        capturing = false;
        if (virtualDisplay != null)  { virtualDisplay.release();  virtualDisplay  = null; }
        if (imageReader != null)     { imageReader.close();       imageReader     = null; }
        if (mediaProjection != null) { mediaProjection.stop();    mediaProjection = null; }
        if (readerThread != null)    { readerThread.quitSafely(); readerThread    = null; }
        synchronized (lock) { latestFrame = null; frameDirty = false; }
        Log.i(TAG, "capture stopped");
    }
}
