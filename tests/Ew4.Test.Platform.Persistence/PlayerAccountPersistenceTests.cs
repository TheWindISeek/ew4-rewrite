using System.Text;
using Ew4.Platform.Persistence.Serialization;
using Xunit;

namespace Ew4.Test.Platform.Persistence;

public sealed class PlayerAccountPersistenceTests
{
    [Fact]
    public void Write_read_roundtrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ew4-account-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "account.json");
        byte[] json = Encoding.UTF8.GetBytes("""{"id":"local","revision":1}""");
        PlayerAccountPersistence.WriteAtomic(path, json);
        Assert.True(PlayerAccountPersistence.Exists(path));
        Assert.Equal(json, PlayerAccountPersistence.Read(path));
    }
}
