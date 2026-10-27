package org.ew4;

import android.content.Context;
import android.util.Log;

import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** 解出并启动本机后端（Ew4.Backend.Web，linux-bionic-arm64 自包含发布）。 */
final class ServerProcess {
    static final String TAG = "Ew4";

    interface Status {
        void update(String text);
    }

    private final Context context;
    private final File root;
    private final File appDir;
    private final File homeDir;
    private Process process;
    int port;
    String token;

    ServerProcess(Context context) {
        this.context = context.getApplicationContext();
        this.root = new File(context.getFilesDir(), "ew4");
        this.appDir = new File(root, "app");
        this.homeDir = new File(root, "home");
    }

    String url() {
        return "http://127.0.0.1:" + port + "/";
    }

    boolean alive() {
        return process != null && process.isAlive();
    }

    /** 首次启动或版本戳变化时解压：覆盖 app/ 与 home/mods/，不动 home/user/（存档与设置）。 */
    void prepare(Status status) throws IOException {
        String stamp = readAsset("stamp.txt").trim();
        File stampFile = new File(root, "stamp.txt");
        File exe = new File(appDir, "Ew4.Backend.Web");
        if (stampFile.exists() && stamp.equals(readFile(stampFile).trim()) && exe.exists()) {
            return;
        }
        status.update("正在准备游戏文件（首次启动需要一些时间）…");
        stampFile.delete();
        deleteRecursively(appDir);
        deleteRecursively(new File(homeDir, "mods"));
        unzipAsset("ew4-runtime.zip", appDir, status, "运行时");
        unzipAsset("ew4-mods.zip", new File(homeDir, "mods"), status, "内容包");
        if (!exe.setExecutable(true, false)) {
            throw new IOException("无法设置可执行权限：" + exe);
        }
        new File(homeDir, "user/logs").mkdirs();
        writeFile(stampFile, stamp);
    }

    /** 启动环境的几套候选配置；自检通过的那套会被记住并用于启动后端。 */
    static final String[][] PROFILES = {
            {},
            {"DOTNET_ReadyToRun=0"},
            {"DOTNET_GCHeapHardLimit=0x30000000"},
            {"DOTNET_ReadyToRun=0", "DOTNET_GCHeapHardLimit=0x30000000"},
            {"DOTNET_ReadyToRun=0", "DOTNET_TieredCompilation=0", "DOTNET_GCHeapHardLimit=0x30000000"},
    };

    private File profileFile() {
        return new File(root, "profile.txt");
    }

    int savedProfile() {
        try {
            return Integer.parseInt(readFile(profileFile()).trim());
        } catch (Exception error) {
            return -1;
        }
    }

    void saveProfile(int index) throws IOException {
        writeFile(profileFile(), String.valueOf(index));
    }

    void clearProfile() {
        profileFile().delete();
    }

    private void applyEnvironment(ProcessBuilder builder, String[] profile) {
        File tmp = context.getCacheDir();
        builder.environment().put("HOME", context.getFilesDir().getAbsolutePath());
        builder.environment().put("TMPDIR", tmp.getAbsolutePath());
        builder.environment().put("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1");
        builder.environment().put("DOTNET_gcServer", "0");
        builder.environment().put("DOTNET_TieredPGO", "0");
        builder.environment().put("DOTNET_EnableDiagnostics", "0");
        builder.environment().put("DOTNET_EnableWriteXorExecute", "0");
        builder.environment().put("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        builder.environment().put("ASPNETCORE_ENVIRONMENT", "Production");
        builder.environment().put("EW4_HOME", homeDir.getAbsolutePath());
        builder.environment().put("EW4_CHANNEL", "apk");
        // Android 没有 OpenSSL：系统 SHA256 会让 Mono 直接 abort（捕获不了），所以强制托管实现
        builder.environment().put("EW4_FORCE_MANAGED_SHA256", "1");
        for (String entry : profile) {
            int eq = entry.indexOf('=');
            builder.environment().put(entry.substring(0, eq), entry.substring(eq + 1));
        }
    }

    /** 运行 --probe 自检，返回「退出码 + 输出」。后端崩溃又拿不到日志时，靠它定位出事的阶段。 */
    String probe(String[] profile) throws IOException, InterruptedException {
        ProcessBuilder builder = new ProcessBuilder(new File(appDir, "Ew4.Backend.Web").getAbsolutePath(), "--probe");
        builder.directory(appDir);
        builder.redirectErrorStream(true);
        applyEnvironment(builder, profile);
        Process child = builder.start();
        StringBuilder output = new StringBuilder();
        Thread reader = new Thread(() -> {
            try (InputStream stream = child.getInputStream()) {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.read(buffer)) > 0) {
                    synchronized (output) {
                        output.append(new String(buffer, 0, read, StandardCharsets.UTF_8));
                    }
                }
            } catch (IOException ignored) {
                // 进程结束
            }
        });
        reader.start();
        boolean finished = child.waitFor(90, java.util.concurrent.TimeUnit.SECONDS);
        if (!finished) {
            child.destroyForcibly();
        }
        reader.join(2000);
        String text;
        synchronized (output) {
            text = output.toString().trim();
        }
        return (finished ? "exit " + child.exitValue() : "超时") + "\n" + text;
    }

    /** 关键文件的大小，用于排查解压是否完整。 */
    String describeFiles() {
        StringBuilder text = new StringBuilder("[文件]\n");
        String[] names = {"Ew4.Backend.Web", "libhostfxr.so", "libhostpolicy.so", "libcoreclr.so",
                "libclrjit.so", "libSystem.Native.so", "System.Private.CoreLib.dll", "Ew4.Backend.Web.dll",
                "Ew4.Backend.Web.runtimeconfig.json", "Ew4.Backend.Web.deps.json"};
        for (String name : names) {
            File file = new File(appDir, name);
            text.append(name).append(' ').append(file.exists() ? file.length() + (file.canExecute() ? " x" : "") : "缺失").append('\n');
        }
        text.append("目录项数 ").append(appDir.list() == null ? -1 : appDir.list().length);
        return text.toString();
    }

    static boolean probePassed(String result) {
        return result.startsWith("exit 0") && result.contains("probe: done");
    }

    void start(Status status, int profileIndex) throws IOException {
        stop();
        status.update("正在启动游戏服务…");
        try (ServerSocket socket = new ServerSocket(0, 0, InetAddress.getByName("127.0.0.1"))) {
            port = socket.getLocalPort();
        }
        token = UUID.randomUUID().toString();
        File logs = new File(homeDir, "user/logs");
        logs.mkdirs();
        File log = new File(logs, "backend.log");

        ProcessBuilder builder = new ProcessBuilder(
                new File(appDir, "Ew4.Backend.Web").getAbsolutePath(),
                "--home", homeDir.getAbsolutePath(),
                "--urls", "http://127.0.0.1:" + port);
        builder.directory(appDir);
        builder.redirectErrorStream(true);
        builder.redirectOutput(ProcessBuilder.Redirect.appendTo(log));
        applyEnvironment(builder, PROFILES[profileIndex]);
        builder.environment().put("EW4_ACCESS_TOKEN", token);
        process = builder.start();
        Log.i(TAG, "backend started on port " + port);
    }

    /** 轮询直到后端能响应；后端提前退出就报错（日志在 user/logs/backend.log）。 */
    void waitReady(long timeoutMs) throws IOException, InterruptedException {
        long deadline = System.currentTimeMillis() + timeoutMs;
        while (System.currentTimeMillis() < deadline) {
            if (!alive()) {
                throw new IOException("后端进程已退出（exit " + process.exitValue() + "）。日志：" + new File(homeDir, "user/logs/backend.log"));
            }
            try {
                HttpURLConnection connection = (HttpURLConnection) new URL(url() + "api/language").openConnection();
                connection.setRequestProperty("Cookie", "ew4_token=" + token);
                connection.setConnectTimeout(1000);
                connection.setReadTimeout(5000);
                int code = connection.getResponseCode();
                connection.disconnect();
                if (code > 0) {
                    return;
                }
            } catch (IOException notReady) {
                // 还没起来
            }
            Thread.sleep(300);
        }
        throw new IOException("等待后端启动超时。日志：" + new File(homeDir, "user/logs/backend.log"));
    }

    void stop() {
        if (process != null) {
            process.destroy();
            process = null;
        }
    }

    String tailLog() {
        try {
            File log = new File(homeDir, "user/logs/backend.log");
            if (!log.exists()) {
                return "";
            }
            String text = readFile(log);
            return text.length() > 1500 ? text.substring(text.length() - 1500) : text;
        } catch (IOException error) {
            return "";
        }
    }

    // —— 文件工具

    private void unzipAsset(String name, File target, Status status, String label) throws IOException {
        target.mkdirs();
        String targetPath = target.getCanonicalPath() + File.separator;
        int count = 0;
        try (ZipInputStream zip = new ZipInputStream(new BufferedInputStream(context.getAssets().open(name), 1 << 16))) {
            byte[] buffer = new byte[1 << 16];
            for (ZipEntry entry = zip.getNextEntry(); entry != null; entry = zip.getNextEntry()) {
                File out = new File(target, entry.getName());
                if (!out.getCanonicalPath().startsWith(targetPath)) {
                    throw new IOException("非法的压缩包路径：" + entry.getName());
                }
                if (entry.isDirectory()) {
                    out.mkdirs();
                    continue;
                }
                File parent = out.getParentFile();
                if (parent != null) {
                    parent.mkdirs();
                }
                try (OutputStream stream = new FileOutputStream(out)) {
                    int read;
                    while ((read = zip.read(buffer)) > 0) {
                        stream.write(buffer, 0, read);
                    }
                }
                if (++count % 200 == 0) {
                    status.update("正在解压" + label + "…（" + count + " 个文件）");
                }
            }
        }
    }

    private String readAsset(String name) throws IOException {
        try (InputStream stream = context.getAssets().open(name)) {
            return readAll(stream);
        }
    }

    private static String readFile(File file) throws IOException {
        try (InputStream stream = new java.io.FileInputStream(file)) {
            return readAll(stream);
        }
    }

    private static String readAll(InputStream stream) throws IOException {
        java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = stream.read(buffer)) > 0) {
            bytes.write(buffer, 0, read);
        }
        return new String(bytes.toByteArray(), StandardCharsets.UTF_8);
    }

    private static void writeFile(File file, String text) throws IOException {
        file.getParentFile().mkdirs();
        try (OutputStream stream = new FileOutputStream(file)) {
            stream.write(text.getBytes(StandardCharsets.UTF_8));
        }
    }

    private static void deleteRecursively(File file) {
        File[] children = file.listFiles();
        if (children != null) {
            for (File child : children) {
                deleteRecursively(child);
            }
        }
        file.delete();
    }
}
