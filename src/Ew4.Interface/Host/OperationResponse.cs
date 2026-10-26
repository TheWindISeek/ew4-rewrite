namespace Ew4.Interface.Host;

/// <summary><see cref="IGameOperations.Invoke"/> 结果；Backend 负责写 HTTP 状态码。</summary>
public sealed record OperationResponse
{
    public static OperationResponse Json(string content) =>
        new(content, "application/json; charset=utf-8", null);

    public static OperationResponse Bytes(byte[] content) =>
        new(null, "application/octet-stream", content);

    public OperationResponse(string? textContent, string contentType, byte[]? binaryContent, int? statusCode = null)
    {
        TextContent = textContent;
        ContentType = contentType;
        BinaryContent = binaryContent;
        StatusCode = statusCode;
    }

    public static OperationResponse NoContent() =>
        new(null, "", null, StatusCodes.Status204NoContent);

    public static OperationResponse NotFound() =>
        new(null, "", null, StatusCodes.Status404NotFound);

    public string? TextContent { get; }

    public string ContentType { get; }

    public byte[]? BinaryContent { get; }

    /// <summary>非 null 时 Backend 使用该 HTTP 状态码（如 201、204）。</summary>
    public int? StatusCode { get; }

    public static OperationResponse CreatedJson(string content) =>
        new(content, "application/json; charset=utf-8", null, StatusCodes.Status201Created);

    public static OperationResponse ClientErrorJson(string content) =>
        new(content, "application/json; charset=utf-8", null, StatusCodes.Status400BadRequest);

    public static OperationResponse ConflictJson(string content) =>
        new(content, "application/json; charset=utf-8", null, StatusCodes.Status409Conflict);

    private static class StatusCodes
    {
        public const int Status204NoContent = 204;
        public const int Status201Created = 201;
        public const int Status400BadRequest = 400;
        public const int Status404NotFound = 404;
        public const int Status409Conflict = 409;
    }
}
