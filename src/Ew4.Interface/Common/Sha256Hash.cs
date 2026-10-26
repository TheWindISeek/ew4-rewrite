using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ew4.Interface.Common;

/// <summary>
/// SHA-256。优先用系统实现；系统没有可用的加密库时（Android 的 bionic 运行时没有 OpenSSL）退回托管实现。
/// 结果与 <see cref="SHA256"/> 完全一致，所以存档/指纹在各平台间通用。
/// </summary>
public static class Sha256Hash
{
    private static readonly bool NativeAvailable = Probe();

    /// <summary>true 表示用系统实现；false 表示用托管实现（给测试和诊断看）。</summary>
    public static bool UsesSystemImplementation => NativeAvailable;

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        if (NativeAvailable)
        {
            using SHA256 sha = SHA256.Create();
            byte[] result = new byte[32];
            return sha.TryComputeHash(data, result, out _) ? result : throw new CryptographicException("SHA-256 failed.");
        }

        return ManagedSha256.Hash(data);
    }

    public static byte[] HashData(byte[] data) => HashData(new ReadOnlySpan<byte>(data));

    /// <summary>增量哈希。<see cref="GetHashAndReset"/> 后可重新使用。</summary>
    public static IncrementalSha256 CreateIncremental() => new(NativeAvailable);

    private static bool Probe()
    {
        // 测试/诊断：强制使用托管实现，验证它在真实流程里与系统实现一致。
        if (Environment.GetEnvironmentVariable("EW4_FORCE_MANAGED_SHA256") == "1")
        {
            return false;
        }

        try
        {
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(new byte[] { 1 }).Length == 32;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public sealed class IncrementalSha256 : IDisposable
    {
        private readonly IncrementalHash? _native;
        private readonly ManagedSha256 _managed = new();

        internal IncrementalSha256(bool native) =>
            _native = native ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;

        public void AppendData(ReadOnlySpan<byte> data)
        {
            if (_native != null)
            {
                _native.AppendData(data.ToArray());
            }
            else
            {
                _managed.Append(data);
            }
        }

        public byte[] GetHashAndReset() => _native != null ? _native.GetHashAndReset() : _managed.FinishAndReset();

        public void Dispose() => _native?.Dispose();
    }

    public sealed class ManagedSha256
    {
        private static readonly uint[] K =
        [
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
        ];

        private readonly uint[] _state = new uint[8];
        private readonly byte[] _block = new byte[64];
        private readonly uint[] _w = new uint[64];
        private int _blockLength;
        private ulong _totalLength;

        public ManagedSha256() => Reset();

        public static byte[] Hash(ReadOnlySpan<byte> data)
        {
            var sha = new ManagedSha256();
            sha.Append(data);
            return sha.FinishAndReset();
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            _totalLength += (ulong)data.Length;
            while (!data.IsEmpty)
            {
                int take = Math.Min(64 - _blockLength, data.Length);
                data.Slice(0, take).CopyTo(_block.AsSpan(_blockLength));
                _blockLength += take;
                data = data.Slice(take);
                if (_blockLength == 64)
                {
                    Compress(_block);
                    _blockLength = 0;
                }
            }
        }

        public byte[] FinishAndReset()
        {
            ulong bits = _totalLength * 8;
            _block[_blockLength++] = 0x80;
            if (_blockLength > 56)
            {
                Array.Clear(_block, _blockLength, 64 - _blockLength);
                Compress(_block);
                _blockLength = 0;
            }

            Array.Clear(_block, _blockLength, 64 - _blockLength);
            BinaryPrimitives.WriteUInt64BigEndian(_block.AsSpan(56), bits);
            Compress(_block);

            byte[] result = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(i * 4), _state[i]);
            }

            Reset();
            return result;
        }

        private void Reset()
        {
            uint[] initial = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
            initial.CopyTo(_state, 0);
            _blockLength = 0;
            _totalLength = 0;
        }

        private static uint Rotr(uint x, int n) => (x >> n) | (x << (32 - n));

        private void Compress(byte[] block)
        {
            uint[] w = _w;
            for (int i = 0; i < 16; i++)
            {
                w[i] = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(i * 4));
            }

            for (int i = 16; i < 64; i++)
            {
                uint s0 = Rotr(w[i - 15], 7) ^ Rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
                uint s1 = Rotr(w[i - 2], 17) ^ Rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
            }

            uint a = _state[0], b = _state[1], c = _state[2], d = _state[3];
            uint e = _state[4], f = _state[5], g = _state[6], h = _state[7];
            unchecked
            {
                for (int i = 0; i < 64; i++)
                {
                    uint s1 = Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25);
                    uint ch = (e & f) ^ (~e & g);
                    uint t1 = h + s1 + ch + K[i] + w[i];
                    uint s0 = Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22);
                    uint maj = (a & b) ^ (a & c) ^ (b & c);
                    uint t2 = s0 + maj;
                    h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
                }

                _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
                _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
            }
        }
    }
}
