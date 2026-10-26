using System.Text.Json;
using Ew4.Platform.Engine;
using Ew4.Platform.Play.Match;

namespace Ew4.Service.Play;

public sealed record PlayCommand(
    string Op = "",
    long Revision = -1,
    string? Scenario = null,
    string? Country = null,
    string? Unit = null,
    string? Target = null,
    int Cell = -1,
    string? Offer = null,
    string? Commander = null,
    string? Item = null,
    JsonElement? Tutorial = null,
    int Group = 0,
    int Slot = 0,
    int Mode = 0,
    IReadOnlyList<string>? LocalPlayers = null,
    string? Tag = null,
    IReadOnlyList<string>? TaggedCommanders = null);

public sealed record PlaySlot(
    string Scenario,
    string? Country,
    byte[] Session,
    IReadOnlyList<FlowItem>? Flow = null,
    JsonElement? Tutorial = null,
    int Round = 0,
    DateTimeOffset? SavedAt = null,
    IReadOnlyList<string>? LocalPlayers = null,
    ulong? AiRandom = null);

internal static class PlayCommandJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PlayCommand Parse(string body) =>
        JsonSerializer.Deserialize<PlayCommand>(body, Options)
        ?? throw new RuleViolation("client.command", "Missing command payload");

    public static PlayCommand ParseOrEmpty(string body) =>
        JsonSerializer.Deserialize<PlayCommand>(body, Options) ?? new PlayCommand();
}
