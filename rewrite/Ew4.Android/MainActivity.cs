using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using Ew4.Mods;
using System.Text;

namespace Ew4.Android;

[Activity(Label = "欧陆战争4 重构", MainLauncher = true, Exported = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private WebView? web;
    private Lazy<ModStore>? store;
    private ImportBridge? bridge;
    private string importId = "", importName = "";
    private bool picking;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var assets = Assets!;
        byte[] Read(string path) { using var stream = assets.Open(path); using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray(); }
        IEnumerable<string> Walk(string path)
        {
            foreach (string name in assets.List(path) ?? [])
            {
                string full = path + "/" + name;
                if (assets.List(full)?.Length > 0) { foreach (string child in Walk(full)) yield return child; }
                else yield return full;
            }
        }
        byte[] ContentRead(string path) => Read(path.StartsWith("client/", StringComparison.Ordinal) ? "www/content/" + path[7..]
            : path.StartsWith("legacy/", StringComparison.Ordinal) ? "data/" + path[7..]
            : path.StartsWith("frontend/", StringComparison.Ordinal) ? "www/" + path[9..] : path);
        store = new Lazy<ModStore>(() => new(new ModBase(ContentRead, Walk("original").Concat(Walk("www/content").Select(p => "client/" + p[12..]))
            .Concat(["legacy/catalog.json", "legacy/runtime_rule_tables.json", "legacy/metagame_rule_tables.json", "frontend/conquest-pages.json", "frontend/city-names.json"])),
            Path.Combine(FilesDir!.AbsolutePath, "content-mods"), Path.Combine(FilesDir.AbsolutePath, "saves")));
        web = new WebView(this);
        web.Settings.JavaScriptEnabled = true;
        web.Settings.DomStorageEnabled = true;
        web.Settings.AllowFileAccess = false;
        web.Settings.AllowContentAccess = false;
        web.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
        web.Settings.SetSupportZoom(false);
        bridge = new ImportBridge(this); web.AddJavascriptInterface(bridge, "Ew4ModImport");
        web.SetWebViewClient(new LocalClient(Read, store));
        SetContentView(web);
        web.LoadUrl("https://ew4.local/index.html");
    }
    protected override void OnDestroy()
    {
        web?.RemoveJavascriptInterface("Ew4ModImport"); bridge?.Dispose(); bridge = null;
        web?.Destroy(); web?.Dispose(); web = null;
        base.OnDestroy();
    }
    private void Pick(string id, string name) => RunOnUiThread(() =>
    {
        if (picking || IsFinishing) return;
        picking = true; importId = id; importName = name;
        var intent = new Intent(Intent.ActionOpenDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("*/*");
        StartActivityForResult(intent, 441);
    });
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != 441) return; picking = false;
        if (resultCode != Result.Ok || data?.Data is not { } uri || store == null) return;
        web?.EvaluateJavascript("window.ew4ModFileSelected?.()", null);
        string id = importId, name = importName;
        _ = Task.Run(() =>
        {
            string? temp = Path.Combine(CacheDir!.AbsolutePath, "mod-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("无法读取文件"))
                using (var output = File.Create(temp))
                {
                    byte[] buffer = new byte[65536]; long total = 0; int count;
                    while ((count = input.Read(buffer)) > 0) { total += count; if (total > 1024L * 1024 * 1024) throw new IOException("文件超过1GiB"); output.Write(buffer, 0, count); }
                }
                store.Value.BeginImport(temp, id, name, deleteSource: true); temp = null;
                RunOnUiThread(() => web?.EvaluateJavascript("window.ew4ModFileQueued?.()", null));
            }
            catch (Exception e) { string message = System.Text.Json.JsonSerializer.Serialize(e.Message); RunOnUiThread(() => web?.EvaluateJavascript("window.ew4ModImportError?.(" + message + ")", null)); }
            finally { if (temp != null) File.Delete(temp); }
        });
    }
    private sealed class ImportBridge(MainActivity activity) : Java.Lang.Object
    {
        [JavascriptInterface, Java.Interop.Export("choose")]
        public void Choose(string id, string name) => activity.Pick(id, name);
    }
    private sealed class LocalClient(Func<string, byte[]> read, Lazy<ModStore> store) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => request?.Url?.Host != "ew4.local";
        public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        {
            var uri = request?.Url;
            if (uri?.Host != "ew4.local") return Response("text/plain", "External request blocked", 403);
            string path = Uri.UnescapeDataString(uri.EncodedPath ?? "/");
            if (path.Contains("..", StringComparison.Ordinal) || path.Contains('\\')) return Response("text/plain", "Invalid path", 400);
            if (path.StartsWith("/api/", StringComparison.Ordinal))
                return Response("application/json", store.Value.Dispatch(path, uri.GetQueryParameter("q") ?? "{}"));
            try
            {
                byte[] bytes = path.StartsWith("/content/", StringComparison.Ordinal) ? store.Value.ReadContent(path[9..])
                    : path is "/conquest-pages.json" or "/city-names.json" ? store.Value.Read("frontend" + path) : read("www/" + path.TrimStart('/'));
                if (!path.StartsWith("/content/", StringComparison.Ordinal)) bytes = store.Value.Frontend(bytes, Path.GetExtension(path));
                string mime = Path.GetExtension(path) switch { ".html" => "text/html", ".js" => "text/javascript", ".css" => "text/css", ".json" => "application/json", ".png" => "image/png", ".mp3" => "audio/mpeg", ".wav" => "audio/wav", _ => "application/octet-stream" };
                return new WebResourceResponse(mime, "UTF-8", 200, "OK", new Dictionary<string, string> { ["Cache-Control"] = "no-store" }, new MemoryStream(bytes));
            }
            catch (Exception error) when (error is IOException or Java.IO.IOException) { return Response("text/plain", "Not found", 404); }
        }
        private static WebResourceResponse Response(string mime, string text, int status = 200) => new(mime, "UTF-8", status,
            status == 200 ? "OK" : "Error", new Dictionary<string, string> { ["Cache-Control"] = "no-store" }, new MemoryStream(Encoding.UTF8.GetBytes(text)));
    }
}
