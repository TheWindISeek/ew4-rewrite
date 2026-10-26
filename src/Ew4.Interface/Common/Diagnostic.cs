namespace Ew4.Interface.Common;

public sealed record Diagnostic(
    Severity Severity,
    string Code,
    string Path,
    string Message,
    string? Hint = null);
