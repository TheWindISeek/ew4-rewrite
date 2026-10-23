using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Session.Persistence;

/// <summary>
/// 槽位文件外层信封（<c>ew4.session</c>），校验 pack / fingerprint。
/// </summary>
public static class SlotSaveEnvelope
{
    public const string Format = "ew4.session";
    public const int SaveSchema = 1;

    public sealed record Binding(string ModId, string Fingerprint, EngineInfo Engine);

    public static byte[] Wrap(byte[] slotJson, Binding binding)
    {
        byte[] payload = slotJson;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("saveSchema", SaveSchema);
            writer.WriteString("engineVersion", binding.Engine.EngineVersion);
            writer.WriteNumber("behaviorVersion", binding.Engine.BehaviorVersion);
            writer.WriteString("modId", binding.ModId);
            writer.WriteString("fingerprint", binding.Fingerprint);
            writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(payload)));
            writer.WritePropertyName("payload");
            writer.WriteRawValue(payload);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static byte[] Unwrap(byte[] bytes, Binding binding, SaveReadLimits? limits = null)
    {
        limits ??= new();
        if (bytes.Length > limits.MaximumBytes)
        {
            throw new RuleViolation("save.size", "Save exceeds configured read budget");
        }

        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = limits.MaximumDepth });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new RuleViolation("save.format", "Unknown save format");
        }

        if (!root.TryGetProperty("format", out JsonElement formatEl) || formatEl.GetString() != Format)
        {
            return bytes;
        }

        if (!root.TryGetProperty("saveSchema", out JsonElement schemaEl) || schemaEl.GetInt32() != SaveSchema)
        {
            throw new RuleViolation("save.version", "Save requires an explicit migration");
        }

        string modId = root.GetProperty("modId").GetString()
            ?? throw new RuleViolation("save.format", "Missing pack id");
        string fingerprint = root.GetProperty("fingerprint").GetString()
            ?? throw new RuleViolation("save.format", "Missing content fingerprint");
        if (!string.Equals(modId, binding.ModId, StringComparison.Ordinal)
            || !string.Equals(fingerprint, binding.Fingerprint, StringComparison.Ordinal))
        {
            throw new RuleViolation("save.content", "此存档属于另一内容或版本");
        }

        JsonElement payload = root.GetProperty("payload");
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload.GetRawText());
        string expectedHash = root.GetProperty("sha256").GetString()
            ?? throw new RuleViolation("save.checksum", "Missing checksum");
        if (!string.Equals(expectedHash, Convert.ToHexStringLower(SHA256.HashData(payloadBytes)), StringComparison.OrdinalIgnoreCase))
        {
            throw new RuleViolation("save.checksum", "Save payload checksum mismatch");
        }

        return payloadBytes;
    }

    public static void WriteAtomic(string path, byte[] slotJson, Binding binding) =>
        WorldSave.WriteAtomicBytes(path, Wrap(slotJson, binding));

    public static byte[] ReadSlotJson(string path, Binding binding, SaveReadLimits? limits = null)
    {
        limits ??= new();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > limits.MaximumBytes)
        {
            throw new RuleViolation("save.size", "Save exceeds configured read budget");
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return Unwrap(bytes, binding, limits);
    }
}
