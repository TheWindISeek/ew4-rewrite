using Ew4.Platform.Content;

namespace Ew4.Backend.Web;

internal static class AuthorHttpJson
{
    internal static IResult Problem(ContentError error) =>
        Results.Json(
            new { code = error.Code, message = error.Message },
            statusCode: StatusCodes.Status400BadRequest);

    internal sealed record DiagnosticDto(string Severity, string Code, string Path, string Message, string? Hint)
    {
        public static DiagnosticDto From(Diagnostic diagnostic) => new(
            diagnostic.Severity.ToString(),
            diagnostic.Code,
            diagnostic.Path,
            diagnostic.Message,
            diagnostic.Hint);
    }
}
