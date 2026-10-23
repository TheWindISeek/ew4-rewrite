using System.Text;
using System.Text.Json;
using Ew4.Platform.Engine;
using Ew4.Platform.Session.Persistence;
using Xunit;

namespace Ew4.Test.Platform.Session;

public sealed class SlotSaveEnvelopeTests
{
    [Fact]
    public void Wrap_unwrap_roundtrip_and_rejects_mismatch()
    {
        var engine = new global::Ew4.Platform.Engine.Engine();
        var binding = new SlotSaveEnvelope.Binding("base", "abc123", engine.Info);
        byte[] slot = Encoding.UTF8.GetBytes("""{"scenario":"campaign1_01"}""");
        byte[] wrapped = SlotSaveEnvelope.Wrap(slot, binding);
        byte[] unwrapped = SlotSaveEnvelope.Unwrap(wrapped, binding);
        Assert.Equal(slot, unwrapped);

        var other = binding with { Fingerprint = "other" };
        Assert.Throws<RuleViolation>(() => SlotSaveEnvelope.Unwrap(wrapped, other));
    }

    [Fact]
    public void Unwrap_accepts_legacy_slot_without_envelope()
    {
        var binding = new SlotSaveEnvelope.Binding("base", "abc", new global::Ew4.Platform.Engine.Engine().Info);
        byte[] legacy = JsonSerializer.SerializeToUtf8Bytes(new { scenario = "x" });
        byte[] unwrapped = SlotSaveEnvelope.Unwrap(legacy, binding);
        Assert.Equal(legacy, unwrapped);
    }
}
