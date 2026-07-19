package com.xbxa01.appwindow;

import android.app.Activity;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.os.Bundle;
import android.util.Log;

/**
 * Translucent activity whose only job is to show the one-time MediaProjection
 * consent dialog and hand the result to ScreenCaptureBridge.
 *
 * Unity's UnityPlayerActivity doesn't forward onActivityResult to plugin code, so
 * there is no way to call startActivityForResult from Unity directly — this activity
 * exists purely to own that call and immediately finish once the dialog is answered.
 */
public class MediaProjectionRequestActivity extends Activity {
    private static final String TAG = "MediaProjectionRequest";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        MediaProjectionManager mpm =
                (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        startActivityForResult(mpm.createScreenCaptureIntent(), ScreenCaptureBridge.REQUEST_CODE);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == ScreenCaptureBridge.REQUEST_CODE) {
            Intent result = (resultCode == RESULT_OK) ? data : null;
            ScreenCaptureBridge.onPermissionResult(getApplicationContext(), resultCode, result);
        } else {
            Log.w(TAG, "Unexpected requestCode " + requestCode);
        }
        finish();
    }
}
