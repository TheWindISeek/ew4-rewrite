using System.Text;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Service;
using Ew4.Interface.Front;
using Microsoft.AspNetCore.Http;

namespace Ew4.Backend.Web;

public static class PlayEndpoints
{
    public static void MapPlayApi(this WebApplication app)
    {
        app.MapMethods("/api/{route}", ["GET", "POST"], async (HttpRequest request, string route, HostApp host, PlaySessionHost play) =>
        {
            if (route is PlayApiRoutes.Mods or PlayApiRoutes.ModImport or PlayApiRoutes.ModPrepare)
            {
                return Results.NotFound();
            }

            if (IsForbiddenCrossSite(request))
            {
                return Results.StatusCode(403);
            }

            IModSnapshot? snapshot = host.ActiveSnapshot;
            if (snapshot == null)
            {
                return PlayError("client.no_pack", "No content pack is active.");
            }

            using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            string body = request.Query["q"].FirstOrDefault() ?? await reader.ReadToEndAsync();
            string path = "/api/" + route;
            try
            {
                string json = play.Dispatch(snapshot, path, body);
                return Results.Text(json, "application/json; charset=utf-8");
            }
            catch (Exception error) when (error is RuleViolation or ContentError or IOException or InvalidOperationException)
            {
                string code = error is RuleViolation rule ? rule.Code : error is ContentError contentError ? contentError.Code : "client.failure";
                return PlayError(code, error.Message);
            }
        });
    }

    internal static bool IsForbiddenCrossSite(HttpRequest request)
    {
        if (request.Headers["Sec-Fetch-Site"] == "cross-site")
        {
            return true;
        }

        if (request.Headers.Origin.Count > 0
            && request.Headers.Origin != request.Scheme + "://" + request.Host)
        {
            return true;
        }

        return false;
    }

    private static IResult PlayError(string code, string message) =>
        Results.Text(
            System.Text.Json.JsonSerializer.Serialize(ApiFailure.Create(code, message)),
            "application/json; charset=utf-8");
}
