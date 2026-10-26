using System.Text;
using Ew4.Interface.Front;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Microsoft.AspNetCore.Http;

namespace Ew4.Backend.Web;

public static class PlayEndpoints
{
    public static void MapPlayApi(this WebApplication app)
    {
        app.MapMethods("/api/play/{route}", ["GET", "POST"], HandlePlayRoute);
        app.MapMethods("/api/{route}", ["GET", "POST"], HandlePlayRoute);
    }

    private static async Task<IResult> HandlePlayRoute(HttpRequest request, string route, IGameOperations game)
    {
        if (route is PlayApiRoutes.Mods or PlayApiRoutes.ModImport or PlayApiRoutes.ModPrepare)
        {
            return Results.NotFound();
        }

        if (IsForbiddenCrossSite(request))
        {
            return Results.StatusCode(403);
        }

        if (game.ActiveContentRevision == null)
        {
            return PlayError("client.no_pack", "No content pack is active.");
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string body = request.Query["q"].FirstOrDefault() ?? await reader.ReadToEndAsync();
        try
        {
            OperationDescriptor descriptor = PlayDescriptors.Describe(route);
            OperationResponse response = game.Invoke(descriptor, new PlayBodyPayload(body));
            return ToPlayResult(response);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
        {
            return PlayError("client.failure", error.Message);
        }
    }

    private static IResult ToPlayResult(OperationResponse response)
    {
        if (response.StatusCode == StatusCodes.Status400BadRequest)
        {
            return Results.Text(response.TextContent ?? "", response.ContentType, statusCode: StatusCodes.Status400BadRequest);
        }

        return response.BinaryContent != null
            ? Results.Bytes(response.BinaryContent, response.ContentType)
            : Results.Text(response.TextContent ?? "", response.ContentType);
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
