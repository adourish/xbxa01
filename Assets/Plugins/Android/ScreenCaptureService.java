package com.xbxa01.appwindow;

import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.IBinder;
import android.util.Log;

/**
 * Foreground service that holds the MediaProjection grant while capture is active.
 *
 * Android 10+ requires MediaProjection.createVirtualDisplay() to be called from a
 * running foreground service, and Android 14 additionally requires that service to
 * declare foregroundServiceType="mediaProjection" (AndroidManifest.xml) and hold
 * FOREGROUND_SERVICE_MEDIA_PROJECTION — without both, getMediaProjection()/
 * createVirtualDisplay() throws SecurityException once the consenting Activity
 * finishes. minSdk is 31 here, so the 3-arg startForeground overload (API 29+) is
 * always available; no version gate needed.
 */
public class ScreenCaptureService extends Service {
    private static final String TAG = "ScreenCaptureService";
    private static final String CHANNEL_ID = "xbxa01-screencapture";
    private static final int NOTIFICATION_ID = 4201;

    public static final String EXTRA_RESULT_CODE = "resultCode";
    public static final String EXTRA_RESULT_DATA = "resultData";

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        startForeground(NOTIFICATION_ID, buildNotification(),
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);

        int resultCode = intent.getIntExtra(EXTRA_RESULT_CODE, Activity.RESULT_CANCELED);
        Intent resultData = intent.getParcelableExtra(EXTRA_RESULT_DATA);

        if (resultData == null || resultCode != Activity.RESULT_OK) {
            Log.e(TAG, "Missing/invalid projection result; stopping.");
            stopSelf();
            return START_NOT_STICKY;
        }

        MediaProjectionManager mpm =
                (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        MediaProjection projection = mpm.getMediaProjection(resultCode, resultData);
        if (projection == null) {
            Log.e(TAG, "getMediaProjection returned null; stopping.");
            stopSelf();
            return START_NOT_STICKY;
        }

        ScreenCaptureBridge.start(getApplicationContext(), projection);
        return START_NOT_STICKY;
    }

    @Override
    public void onDestroy() {
        ScreenCaptureBridge.stop();
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) { return null; }

    private Notification buildNotification() {
        NotificationManager nm = getSystemService(NotificationManager.class);
        nm.createNotificationChannel(
                new NotificationChannel(CHANNEL_ID, "Screen capture", NotificationManager.IMPORTANCE_LOW));

        return new Notification.Builder(this, CHANNEL_ID)
                .setContentTitle("XBXA01 AR")
                .setContentText("Mirroring screen to glasses panel")
                .setSmallIcon(android.R.drawable.stat_sys_download)
                .build();
    }
}
