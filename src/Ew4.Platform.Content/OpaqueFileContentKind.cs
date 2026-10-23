using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// 不透明文件（图片、BTL、XML 等）：仅参与大小限制，不按 JSON 解析。
/// </summary>
public sealed class OpaqueFileContentKind : IContentKind
{
    public OpaqueFileContentKind(
        string name,
        string pathPattern,
        int currentSchema,
        bool affectsFingerprint,
        ContentLimits limits)
    {
        Name = name;
        PathPattern = pathPattern;
        CurrentSchema = currentSchema;
        AffectsFingerprint = affectsFingerprint;
        Limits = limits;
    }

    public string Name { get; }

    public string PathPattern { get; }

    public int CurrentSchema { get; }

    public bool AffectsFingerprint { get; }

    public ContentLimits Limits { get; }

    public object Load(byte[] data, int schema) => data;

    public IEnumerable<Diagnostic> Validate(object model, ValidationContext context) => [];
}
