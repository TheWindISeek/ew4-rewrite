using System.IO;
using System.Text.Json;
using Ew4.Interface.Play;
using Ew4.Platform.Engine;

namespace Ew4.Service.Play;

internal static class PlayJsonEnvelope
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Success(object data) =>
        JsonSerializer.Serialize(new { ok = true, data }, Json);

    public static string Failure(Exception error)
    {
        string code = error is RuleViolation rule ? rule.Code : "client.failure";
        return JsonSerializer.Serialize(new { ok = false, code, message = error.Message }, Json);
    }

    public static string Failure(PlayError error) =>
        JsonSerializer.Serialize(new { ok = false, code = error.Code, message = error.Message }, Json);
}
