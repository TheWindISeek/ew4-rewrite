using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Microsoft.Extensions.FileProviders;

namespace Ew4.Backend.Web;

public static class PlayStaticFiles
{
    public static void UsePlayFrontend(this WebApplication app)
    {
        string wwwroot = app.Services.GetRequiredService<IGameWebPaths>().WwwRootDirectory;
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
                await ServeThroughService(context, path);
                return;
            }

            await next();
        });
    }

    private static async Task ServeThroughService(HttpContext context, string path)
    {
        context.Response.Headers.CacheControl = "no-store";
        IGameOperations game = context.RequestServices.GetRequiredService<IGameOperations>();
        OperationResponse response = game.Invoke(new StaticHttpAssetDescriptor(path), new StaticAssetPayload());
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (response.BinaryContent == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = response.ContentType;
        await context.Response.Body.WriteAsync(response.BinaryContent);
    }
}
