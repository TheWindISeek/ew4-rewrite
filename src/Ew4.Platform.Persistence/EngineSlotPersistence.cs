using System;
using System.IO;
using System.Text.Json;
using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Serialization;

namespace Ew4.Platform.Persistence;

public static class EngineSlotPersistence
{
    private sealed record Payload(string ScenarioId, byte[] Session);

    public static void Write(string path, GameSession session, SlotSaveEnvelope.Binding envelope)
    {
        session.Validate();
        byte[] inner = JsonSerializer.SerializeToUtf8Bytes(
            new Payload(session.World.ScenarioId, SessionSave.Encode(session)),
            SessionJson.Options);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            PhysicalFiles.EnsureDirectory(parent);
        }

        SlotSaveEnvelope.WriteAtomic(path, inner, envelope);
    }

    public static string ReadScenarioId(byte[] inner)
    {
        using var document = JsonDocument.Parse(inner);
        return document.RootElement.GetProperty("scenarioId").GetString()
            ?? throw new RuleViolation("save.format", "Invalid engine slot");
    }

    public static GameSession ReadInner(byte[] inner, SessionContentBinding contentBinding, SaveReadLimits? limits = null)
    {
        Payload payload = JsonSerializer.Deserialize<Payload>(inner, SessionJson.Options)
            ?? throw new RuleViolation("save.format", "Invalid engine slot");
        return SessionSave.Decode(payload.Session, contentBinding, limits);
    }

    internal static class SessionJson
    {
        internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    }
}
