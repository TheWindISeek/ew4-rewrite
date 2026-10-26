using System.Text;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Serialization;
using Xunit;

namespace Ew4.Test.Platform.Persistence;

public sealed class PlaySlotPersistenceTests
{
    [Fact]
    public void Write_read_roundtrip_without_envelope()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ew4-slot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "slot-1.json");
        byte[] inner = Encoding.UTF8.GetBytes("""{"scenario":"x"}""");
        PlaySlotPersistence.WriteInnerJson(path, inner, envelope: null);
        byte[] read = PlaySlotPersistence.ReadInnerJson(path, envelope: null);
        Assert.Equal(inner, read);
    }
}
