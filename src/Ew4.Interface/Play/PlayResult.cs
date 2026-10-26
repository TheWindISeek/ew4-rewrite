namespace Ew4.Interface.Play;

public readonly record struct PlayResult<T>(bool Success, T? Value, PlayError? Error)
{
    public static PlayResult<T> Ok(T value) => new(true, value, null);

    public static PlayResult<T> Fail(string code, string message) => new(false, default, new PlayError(code, message));
}
