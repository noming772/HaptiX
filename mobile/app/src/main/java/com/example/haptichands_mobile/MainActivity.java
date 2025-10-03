package com.example.haptichands_mobile;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.Bundle;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.os.VibratorManager;
import android.util.Log;
import android.view.MotionEvent;
import android.view.View;
import android.widget.EditText;
import android.widget.RadioButton;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.bottomsheet.BottomSheetBehavior;
import com.google.android.material.button.MaterialButtonToggleGroup;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.SocketTimeoutException;

public class MainActivity extends AppCompatActivity {

    private TextView statusText;

    private static final int unityPort = 7777;

    private volatile String unityIP = "";

    private String objectType = "HAND_A";

    private float lastAngle = 0f;
    private boolean angleInitialized = false;

    private volatile boolean running = false;
    private DatagramSocket rxSocket = null;
    private Vibrator vibrator = null;

    private BottomSheetBehavior<View> behavior;
    private boolean lockHalf = true;
    private boolean internalChange = false;

    private MaterialButtonToggleGroup axisToggleGroup;
    private volatile boolean useYAxis = true;

    private SharedPreferences prefs;
    private EditText ipInput;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        statusText = findViewById(R.id.statusText);
        View touchArea = findViewById(R.id.touchArea);
        touchArea.setOnTouchListener(this::handleTouch);

        prefs = getSharedPreferences("haptix", MODE_PRIVATE);
        unityIP = prefs.getString("unityIP", "");
        ipInput = findViewById(R.id.ipInput);
        if (ipInput != null) ipInput.setText(unityIP);

        if (unityIP.isEmpty()) {
            statusText.setText("Set IP first.");
        }

        initVibrator();
        startUdpReceiver();

        View bottomSheet = findViewById(R.id.bottomSheet);
        behavior = BottomSheetBehavior.from(bottomSheet);
        behavior.setFitToContents(false);
        behavior.setHalfExpandedRatio(0.7f);
        behavior.setPeekHeight(dp(24));
        behavior.setHideable(false);
        behavior.setState(BottomSheetBehavior.STATE_HALF_EXPANDED);

        View handle = findViewById(R.id.handleArea);
        handle.setOnClickListener(v -> {
            int s = behavior.getState();
            if (s == BottomSheetBehavior.STATE_HALF_EXPANDED || s == BottomSheetBehavior.STATE_EXPANDED) {
                lockHalf = false;
                behavior.setState(BottomSheetBehavior.STATE_COLLAPSED);
            } else if (s == BottomSheetBehavior.STATE_COLLAPSED) {
                behavior.setState(BottomSheetBehavior.STATE_HALF_EXPANDED);
            }
        });

        RadioButton radioRed = findViewById(R.id.radioRed);
        RadioButton radioBlue = findViewById(R.id.radioBlue);

        if ("HAND_A".equals(objectType)) radioRed.setChecked(true);
        else radioBlue.setChecked(true);

        radioRed.setOnCheckedChangeListener((btn, checked) -> {
            if (internalChange) return;
            if (checked) {
                internalChange = true;
                radioBlue.setChecked(false);
                internalChange = false;
                objectType = "HAND_A";
                statusText.setText(getString(R.string.mode_red));
            }
        });
        radioBlue.setOnCheckedChangeListener((btn, checked) -> {
            if (internalChange) return;
            if (checked) {
                internalChange = true;
                radioRed.setChecked(false);
                internalChange = false;
                objectType = "HAND_B";
                statusText.setText(getString(R.string.mode_blue));
            }
        });

        axisToggleGroup = findViewById(R.id.axisToggleGroup);
        axisToggleGroup.addOnButtonCheckedListener((group, checkedId, isChecked) -> {
            if (!isChecked) return;
            if (checkedId == R.id.btnAxisY) useYAxis = true;
            else if (checkedId == R.id.btnAxisZ) useYAxis = false;
            sendAxisConfigMessage();
        });

        findViewById(R.id.btnSave).setOnClickListener(v -> {
            if (ipInput != null) {
                String ip = ipInput.getText().toString().trim();
                if (isValidIPv4(ip)) {
                    unityIP = ip;
                    prefs.edit().putString("unityIP", unityIP).apply();
                    statusText.setText("Saved IP: " + unityIP + ":" + unityPort);

                    lockHalf = false;
                    behavior.setState(BottomSheetBehavior.STATE_COLLAPSED);
                } else {

                    Toast.makeText(MainActivity.this,
                            "Invalid IP.",
                            Toast.LENGTH_SHORT).show();
                    if (ipInput.requestFocus()) {
                        ipInput.setSelection(ip.length());
                    }
                }
            }
        });

        behavior.addBottomSheetCallback(new BottomSheetBehavior.BottomSheetCallback() {
            @Override
            public void onStateChanged(@NonNull View bs, int newState) {
                if (lockHalf && (newState == BottomSheetBehavior.STATE_EXPANDED || newState == BottomSheetBehavior.STATE_COLLAPSED)) {
                    behavior.setState(BottomSheetBehavior.STATE_HALF_EXPANDED);
                }
                if (!lockHalf && newState == BottomSheetBehavior.STATE_COLLAPSED) {
                    lockHalf = true;
                }
            }
            @Override public void onSlide(@NonNull View bs, float slideOffset) { }
        });
    }

    private int dp(int v) {
        return Math.round(getResources().getDisplayMetrics().density * v);
    }

    private void initVibrator() {
        if (Build.VERSION.SDK_INT >= 31) {
            VibratorManager vm = (VibratorManager) getSystemService(Context.VIBRATOR_MANAGER_SERVICE);
            vibrator = vm != null ? vm.getDefaultVibrator() : null;
        } else {
            vibrator = (Vibrator) getSystemService(Context.VIBRATOR_SERVICE);
        }
    }

    private void vibrateOnce(int ms) {
        if (vibrator == null) return;
        if (Build.VERSION.SDK_INT >= 26) {
            vibrator.vibrate(VibrationEffect.createOneShot(ms, VibrationEffect.DEFAULT_AMPLITUDE));
        } else {
            vibrator.vibrate(ms);
        }
    }

    private void cancelVibration() {
        if (vibrator != null) vibrator.cancel();
    }

    private void startUdpReceiver() {
        running = true;
        new Thread(() -> {
            try {
                rxSocket = new DatagramSocket(7777);
                rxSocket.setReuseAddress(true);
                rxSocket.setSoTimeout(1000);
                byte[] buf = new byte[1024];
                DatagramPacket packet = new DatagramPacket(buf, buf.length);
                while (running) {
                    try {
                        rxSocket.receive(packet);
                        String msg = new String(packet.getData(), 0, packet.getLength()).trim();
                        runOnUiThread(() -> statusText.setText(prettyStatus("RX: " + msg)));
                        if (msg.startsWith("VIBRATE")) {
                            vibrateOnce(80);
                        } else if (msg.startsWith("STOP")) {
                            cancelVibration();
                        }
                    } catch (SocketTimeoutException ignored) { }
                }
            } catch (Exception e) {
                Log.e("TouchController", "UDP rx failed", e);
            } finally {
                if (rxSocket != null && !rxSocket.isClosed()) rxSocket.close();
            }
        }).start();
    }

    private String prettyStatus(String raw) {
        if (raw == null) return "";
        return raw.replace("HAND_A", "Left Hand")
                .replace("HAND_B", "Right Hand");
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        running = false;
        if (rxSocket != null) rxSocket.close();
    }

    private boolean handleTouch(View v, MotionEvent event) {
        int pointerCount = event.getPointerCount();
        String message = "";

        float x1, y1, x2, y2, midX = 0f, midY = 0f, distance = 0f;

        if (pointerCount >= 2) {
            x1 = event.getX(0);
            y1 = event.getY(0);
            x2 = event.getX(1);
            y2 = event.getY(1);
            midX = (x1 + x2) / 2f;
            midY = (y1 + y2) / 2f;
            float dx = x2 - x1;
            float dy = y2 - y1;
            distance = (float) Math.sqrt(dx * dx + dy * dy);
        }

        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                angleInitialized = false;
                break;

            case MotionEvent.ACTION_POINTER_DOWN:
                if (pointerCount >= 2) {
                    float angle = (float) Math.toDegrees(Math.atan2(
                            event.getY(1) - event.getY(0),
                            event.getX(1) - event.getX(0)
                    ));
                    lastAngle = angle;
                    angleInitialized = true;

                    int screenWidth = v.getWidth();
                    int screenHeight = v.getHeight();
                    String axis = useYAxis ? "Y" : "Z";
                    message = objectType + ":START:" + midX + "," + midY + "," + distance + "," + 0f + "," + 1 + "," + screenWidth + "," + screenHeight + "," + axis;
                }
                break;

            case MotionEvent.ACTION_MOVE:
                if (pointerCount >= 2 && angleInitialized) {
                    float angle = (float) Math.toDegrees(Math.atan2(
                            event.getY(1) - event.getY(0),
                            event.getX(1) - event.getX(0)
                    ));
                    float rawDelta = angle - lastAngle;
                    while (rawDelta > 180f) rawDelta -= 360f;
                    while (rawDelta < -180f) rawDelta += 360f;
                    lastAngle = angle;

                    int screenWidth = v.getWidth();
                    int screenHeight = v.getHeight();
                    String axis = useYAxis ? "Y" : "Z";
                    message = objectType + ":MOVE:" + midX + "," + midY + "," + distance + "," + rawDelta + "," + 1 + "," + screenWidth + "," + screenHeight + "," + axis;
                }
                break;

            case MotionEvent.ACTION_POINTER_UP:
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                angleInitialized = false;
                float sx = event.getX(event.getActionIndex());
                float sy = event.getY(event.getActionIndex());
                String axis = useYAxis ? "Y" : "Z";
                message = objectType + ":STOP:" + sx + "," + sy + "," + axis;
                break;
        }

        if (!message.isEmpty()) {
            statusText.setText(prettyStatus(message));
            sendUDPMessage(message);
        }
        return true;
    }

    private void sendAxisConfigMessage() {
        String axis = useYAxis ? "Y" : "Z";
        String msg = objectType + ":AXIS:" + axis;
        sendUDPMessage(msg);
    }

    private void sendUDPMessage(String message) {
        if (!isValidIPv4(unityIP)) {
            runOnUiThread(() -> Toast.makeText(
                    MainActivity.this,
                    "Please set a valid IP first.",
                    Toast.LENGTH_SHORT
            ).show());
            return;
        }

        Log.d("TouchController", "SEND to " + unityIP + ":" + unityPort + " -> " + message);
        new Thread(() -> {
            try {
                InetAddress serverAddress = InetAddress.getByName(unityIP);
                byte[] buffer = message.getBytes();
                DatagramPacket packet = new DatagramPacket(buffer, buffer.length, serverAddress, unityPort);
                synchronized (MainActivity.this) {
                    if (rxSocket != null && !rxSocket.isClosed()) {
                        rxSocket.send(packet);
                        return;
                    }
                }
            } catch (Exception e) {
                Log.e("TouchController", "UDP send failed", e);
            }
        }).start();
    }

    private boolean isValidIPv4(String ip) {
        if (ip == null || ip.isEmpty()) return false;
        String[] parts = ip.split("\\.");
        if (parts.length != 4) return false;
        for (String p : parts) {
            try {
                int n = Integer.parseInt(p);
                if (n < 0 || n > 255) return false;
            } catch (NumberFormatException e) {
                return false;
            }
        }
        return true;
    }
}
