namespace Ew4.Interface.Editor;

public readonly record struct EditorOutcome<T>(bool Success, T? Value, EditorError? Error)
{
    public static EditorOutcome<T> Ok(T value) => new(true, value, null);

    public static EditorOutcome<T> Fail(string code, string message) =>
        new(false, default, new EditorError(code, message));
}
