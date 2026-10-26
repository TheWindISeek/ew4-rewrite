package org.ew4;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import java.io.IOException;
import java.util.Arrays;
import android.widget.FrameLayout;
import android.widget.TextView;

/** 全屏 WebView，加载本机后端提供的游戏页面。 */
public final class MainActivity extends Activity {
    private static final int FILE_CHOOSER = 7;

    private final Handler main = new Handler(Looper.getMainLooper());
    private ServerProcess server;
    private WebView web;
    private TextView message;
    private ValueCallback<Uri[]> fileCallback;
    private volatile boolean starting;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        FrameLayout layout = new FrameLayout(this);
        layout.setBackgroundColor(Color.BLACK);

        web = new WebView(this);
        web.setBackgroundColor(Color.BLACK);
        web.setVisibility(View.INVISIBLE);
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        web.setWebViewClient(new WebViewClient());
        web.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onShowFileChooser(WebView view, ValueCallback<Uri[]> callback, FileChooserParams params) {
                if (fileCallback != null) {
                    fileCallback.onReceiveValue(null);
                }
                fileCallback = callback;
                try {
                    startActivityForResult(params.createIntent(), FILE_CHOOSER);
                } catch (RuntimeException error) {
                    fileCallback = null;
                    return false;
                }
                return true;
            }
        });
        layout.addView(web, new FrameLayout.LayoutParams(-1, -1));

        message = new TextView(this);
        message.setTextColor(Color.WHITE);
        message.setTextSize(12);
        message.setGravity(Gravity.CENTER);
        message.setTextIsSelectable(true);
        message.setPadding(48, 48, 48, 48);
        message.setText("欧陆战争4");
        layout.addView(message, new FrameLayout.LayoutParams(-1, -1));
        setContentView(layout);
        hideSystemUi();

        server = new ServerProcess(this);
        launch();
    }

    private void launch() {
        if (starting) {
            return;
        }
        starting = true;
        new Thread(() -> {
            try {
                server.prepare(this::show);
                int profile = server.savedProfile();
                if (profile < 0) {
                    profile = selectProfile();
                }
                server.start(this::show, profile);
                try {
                    server.waitReady(180_000);
                } catch (IOException failed) {
                    server.clearProfile(); // 下次重新自检
                    throw failed;
                }
                main.post(() -> {
                    CookieManager cookies = CookieManager.getInstance();
                    cookies.setAcceptCookie(true);
                    cookies.setCookie("http://127.0.0.1:" + server.port + "/", "ew4_token=" + server.token + "; Path=/");
                    cookies.flush();
                    web.loadUrl(server.url());
                    web.setVisibility(View.VISIBLE);
                    message.setVisibility(View.GONE);
                });
            } catch (Exception error) {
                Log.e(ServerProcess.TAG, "launch failed", error);
                String log = server.tailLog();
                show("启动失败：" + error.getMessage() + (log.isEmpty() ? "" : "\n\n" + log));
            } finally {
                starting = false;
            }
        }, "ew4-launch").start();
    }

    /** 依次用候选环境跑 --probe，选出第一套能完整通过的；都不行就把每套的输出显示在屏幕上。 */
    private int selectProfile() throws Exception {
        StringBuilder report = new StringBuilder("设备：" + Build.MODEL + " / Android " + Build.VERSION.RELEASE
                + " (API " + Build.VERSION.SDK_INT + ") / " + Arrays.toString(Build.SUPPORTED_ABIS) + "\n");
        for (int i = 0; i < ServerProcess.PROFILES.length; i++) {
            show("正在自检运行环境（" + (i + 1) + "/" + ServerProcess.PROFILES.length + "）…");
            String result = server.probe(ServerProcess.PROFILES[i]);
            if (ServerProcess.probePassed(result)) {
                server.saveProfile(i);
                return i;
            }
            report.append("\n[配置 ").append(i).append(' ').append(Arrays.toString(ServerProcess.PROFILES[i])).append("]\n")
                    .append(result.length() > 700 ? "…" + result.substring(result.length() - 700) : result).append('\n');
        }
        // 全部失败：再带上 host 跟踪跑一次，看 hostfxr/hostpolicy/coreclr 走到哪一步
        show("正在收集诊断信息…");
        String trace = server.probe(new String[] {"COREHOST_TRACE=1", "COREHOST_TRACE_VERBOSITY=4"});
        report.append("\n[host 跟踪]\n").append(trace.length() > 2500 ? "…" + trace.substring(trace.length() - 2500) : trace).append('\n');
        report.append('\n').append(server.describeFiles());
        try {
            java.io.File dir = getExternalFilesDir(null);
            if (dir != null) {
                java.io.FileOutputStream out = new java.io.FileOutputStream(new java.io.File(dir, "diagnose.txt"));
                out.write(report.toString().getBytes("UTF-8"));
                out.close();
            }
        } catch (Exception ignored) {
            // 仅尽力而为
        }
        throw new IOException("所有自检配置都失败。\n" + report);
    }

    private void show(String text) {
        main.post(() -> {
            message.setVisibility(View.VISIBLE);
            message.setText(text);
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        hideSystemUi();
        // 后台时进程可能被系统回收：回来时重启并重载。
        if (server != null && !starting && !server.alive()) {
            web.setVisibility(View.INVISIBLE);
            launch();
        }
    }

    @Override
    protected void onDestroy() {
        if (server != null) {
            server.stop();
        }
        super.onDestroy();
    }

    @Override
    public void onBackPressed() {
        moveTaskToBack(true);
    }

    @Override
    protected void onActivityResult(int request, int result, Intent data) {
        if (request == FILE_CHOOSER && fileCallback != null) {
            fileCallback.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(result, data));
            fileCallback = null;
            return;
        }
        super.onActivityResult(request, result, data);
    }

    @Override
    public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        if (focused) {
            hideSystemUi();
        }
    }

    private void hideSystemUi() {
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }
}
