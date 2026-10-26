using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Ew4.Platform.Content;

namespace Ew4.Service.Host;

public sealed class PlayStaticAssetService
{
    private readonly IRuntimeHost _runtime;
    private readonly IGameWebPaths _webPaths;

    public PlayStaticAssetService(IRuntimeHost runtime, IGameWebPaths webPaths)
    {
        _runtime = runtime;
        _webPaths = webPaths;
    }

    public OperationResponse Serve(string requestPath)
    {
        requestPath = requestPath.Split('?', 2)[0];
        if (requestPath is "/conquest-pages.json" or "/city-names.json")
        {
            return ServeBundledJson(requestPath);
        }

        if (!requestPath.StartsWith("/content/", StringComparison.Ordinal))
        {
            return OperationResponse.NotFound();
        }

        return ServeModAsset(requestPath);
    }

    private OperationResponse ServeBundledJson(string requestPath)
    {
        string file = Path.Combine(_webPaths.WwwRootDirectory, requestPath.TrimStart('/'));
        if (!File.Exists(file))
        {
            return OperationResponse.NotFound();
        }

        byte[] bytes = File.ReadAllBytes(file);
        return new OperationResponse(null, "application/json; charset=utf-8", bytes);
    }

    private OperationResponse ServeModAsset(string requestPath)
    {
        IModSnapshot? snapshot = _runtime.ActiveSnapshot;
        if (snapshot == null)
        {
            return OperationResponse.NotFound();
        }

        try
        {
            string relative = requestPath["/content/".Length..];
            string runtimePath = MapContentPath(relative);
            if (!snapshot.TryReadRuntimeAsset(runtimePath, out byte[] bytes))
            {
                return OperationResponse.NotFound();
            }

            string mimePath = ModAssetReader.TryResolve(snapshot.AsResolvedContent().Source, runtimePath, out string packPath)
                ? packPath
                : runtimePath;
            return new OperationResponse(null, Mime(mimePath), bytes);
        }
        catch (IOException)
        {
            return OperationResponse.NotFound();
        }
    }

    private static string MapContentPath(string relative)
    {
        relative = relative.Replace('\\', '/');
        if (relative.StartsWith("r-", StringComparison.Ordinal))
        {
            int slash = relative.IndexOf('/');
            if (slash > 0)
            {
                relative = relative[(slash + 1)..];
            }
        }

        if (relative.StartsWith("extensions/", StringComparison.Ordinal))
        {
            if (!relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Only extension images are public.");
            }

            return "rewrite/" + relative["extensions/".Length..];
        }

        return "client/" + relative;
    }

    private static string Mime(string path) => Path.GetExtension(path) switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".png" => "image/png",
        ".json" => "application/json; charset=utf-8",
        ".ttf" => "font/ttf",
        ".woff2" => "font/woff2",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        _ => "application/octet-stream",
    };
}
