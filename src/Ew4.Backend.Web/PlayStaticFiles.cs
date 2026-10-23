using Ew4.Platform.Content;
using Ew4.Platform.Runtime;
using Ew4.Backend.Web.Layout;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Ew4.Backend.Web;

public static class PlayStaticFiles
{
    public static void UsePlayFrontend(this WebApplication app, AuthorHostOptions options)
    {
        string wwwroot = WebPaths.ResolveWwwRoot(options.Install, options.RepositoryRoot);
        if (Directory.Exists(wwwroot))
        {
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot),
                RequestPath = "",
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot),
                RequestPath = "",
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-store",
            });
        }

        app.Use(async (context, next) =>
        {
            string path = context.Request.Path.Value ?? "/";
            if (path.StartsWith("/content/", StringComparison.Ordinal)
                || path is "/conquest-pages.json" or "/city-names.json")
            {
                await ServePackOrFrontend(context, options, wwwroot, path);
                return;
            }

            await next();
        });
    }

    private static async Task ServePackOrFrontend(
        HttpContext context,
        AuthorHostOptions options,
        string wwwroot,
        string path)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            HostApp host = context.RequestServices.GetRequiredService<HostApp>();
            ResolvedContent? content = host.ActiveContent;
            if (content == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (path is "/conquest-pages.json" or "/city-names.json")
            {
                string file = Path.Combine(wwwroot, path.TrimStart('/'));
                if (!File.Exists(file))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.SendFileAsync(file);
                return;
            }

            string runtimePath = MapContentPath(path["/content/".Length..]);
            if (!PackAssetReader.TryResolve(content.Source, runtimePath, out string packPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = Mime(packPath);
            byte[] bytes = content.Source.Read(packPath);
            await context.Response.Body.WriteAsync(bytes);
        }
        catch (IOException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
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
