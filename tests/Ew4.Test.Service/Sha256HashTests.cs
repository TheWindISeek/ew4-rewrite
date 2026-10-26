using System.Security.Cryptography;
using Ew4.Interface.Common;
using Xunit;

namespace Ew4.Test.Service;

public sealed class Sha256HashTests
{
    [Fact]
    public void Managed_implementation_matches_the_system_one_at_every_block_boundary()
    {
        var random = new Random(7);
        foreach (int length in Enumerable.Range(0, 200).Concat([1000, 4096, 100_000]))
        {
            byte[] data = new byte[length];
            random.NextBytes(data);
            Assert.Equal(SHA256.HashData(data), Sha256Hash.ManagedSha256.Hash(data));
        }
    }

    [Fact]
    public void Managed_incremental_hash_is_independent_of_chunking_and_reusable()
    {
        byte[] data = new byte[777];
        new Random(3).NextBytes(data);
        var sha = new Sha256Hash.ManagedSha256();
        foreach (int chunk in new[] { 1, 7, 64, 100 })
        {
            for (int i = 0; i < data.Length; i += chunk)
            {
                sha.Append(data.AsSpan(i, Math.Min(chunk, data.Length - i)));
            }

            Assert.Equal(SHA256.HashData(data), sha.FinishAndReset());
        }
    }

    [Fact]
    public void Known_vector_and_public_api_agree()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            Convert.ToHexStringLower(Sha256Hash.ManagedSha256.Hash("abc"u8)));
        Assert.Equal(SHA256.HashData("abc"u8), Sha256Hash.HashData("abc"u8.ToArray()));
        using var incremental = Sha256Hash.CreateIncremental();
        incremental.AppendData("a"u8);
        incremental.AppendData("bc"u8);
        Assert.Equal(SHA256.HashData("abc"u8), incremental.GetHashAndReset());
    }
}
