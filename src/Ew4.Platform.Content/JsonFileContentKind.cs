using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// 将一类 JSON 文件作为 <see cref="JsonDocument"/> 加载；Schema 阶段校验 JSON 语法。
/// </summary>
public sealed class JsonFileContentKind : IContentKind
{
    public JsonFileContentKind(
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

    public object Load(byte[] data, int schema)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException ex)
        {
            throw new ContentError("content.json", "Invalid JSON: " + ex.Message);
        }
    }

    public IEnumerable<Diagnostic> Validate(object model, ValidationContext context)
    {
        if (model is not JsonDocument document)
        {
            yield return new Diagnostic(Severity.Error, "content.model", Name, "Expected JSON document.");
            yield break;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object && document.RootElement.ValueKind != JsonValueKind.Array)
        {
            yield return new Diagnostic(Severity.Error, "content.schema", Name, "JSON root must be an object or array.");
        }
    }
}
