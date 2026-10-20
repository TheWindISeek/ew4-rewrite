using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb;
using AssetRipper.TextureDecoder.Rgb.Formats;

namespace Ew4.Mods;

internal sealed record Raster(int Width, int Height, byte[] Pixels)
{
    internal static void Dimensions(int w, int h)
    {
        if (w < 1 || h < 1 || w > 8192 || h > 8192 || (long)w * h > 16 * 1024 * 1024) throw new InvalidDataException("贴图尺寸超过8192边长或1600万像素");
    }
    internal static Raster Decode(byte[] data)
    {
        if (data.Length >= 16 && Encoding.ASCII.GetString(data, 0, 6) == "PKM 10")
        {
            int U(int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
            int ew = U(8), eh = U(10), w = U(12), h = U(14); Dimensions(ew, eh);
            if (U(6) != 0 || ew % 4 != 0 || eh % 4 != 0 || w > ew || h > eh || data.Length != 16 + ew * eh / 2) throw new InvalidDataException("无效的ETC1 PKM");
            EtcDecoder.DecompressETC<ColorRGBA<byte>, byte>(data.AsSpan(16), ew, eh, out byte[] rgba);
            return new Raster(ew, eh, rgba).Crop(0, 0, w, h);
        }
        if (data.Length >= 52 && BitConverter.ToInt32(data, 0) == 52 && BitConverter.ToUInt32(data, 44) == 559044176)
        {
            int h = BitConverter.ToInt32(data, 4), w = BitConverter.ToInt32(data, 8); Dimensions(w, h);
            if (BitConverter.ToInt32(data, 20) != w * h * 2 || BitConverter.ToInt32(data, 24) != 16 || data.Length != 52 + w * h * 2
                || !new[] { 61440, 3840, 240, 15 }.Select((v, i) => BitConverter.ToInt32(data, 28 + i * 4) == v).All(b => b)) throw new InvalidDataException("只支持原版RGBA4444 PVR");
            byte[] rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++) { int p = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(52 + i * 2)); for (int c = 0; c < 4; c++) rgba[i * 4 + c] = (byte)(((p >> (12 - c * 4)) & 15) * 17); }
            return new(w, h, rgba);
        }
        if (data.Length < 24 || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("只支持PNG/PKM/PVR贴图");
        Dimensions(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20)));
        var image = StbImageSharp.ImageResult.FromMemory(data, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return new(image.Width, image.Height, image.Data);
    }
    internal Raster Crop(int x, int y, int w, int h)
    {
        Dimensions(w, h);
        if (x < 0 || y < 0 || (long)x + w > Width || (long)y + h > Height) throw new InvalidDataException("图集裁剪超出贴图边界");
        var result = new Raster(w, h, new byte[w * h * 4]);
        for (int row = 0; row < h; row++) Buffer.BlockCopy(Pixels, ((y + row) * Width + x) * 4, result.Pixels, row * w * 4, w * 4);
        return result;
    }
    internal void Copy(Raster source, int sx, int sy, int dx, int dy, int w, int h)
    {
        for (int y = 0; y < h; y++) Buffer.BlockCopy(source.Pixels, ((sy + y) * source.Width + sx) * 4, Pixels, ((dy + y) * Width + dx) * 4, w * 4);
    }
    internal byte[] Png()
    {
        using var stream = new MemoryStream();
        new StbImageWriteSharp.ImageWriter().WritePng(Pixels, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
        return stream.ToArray();
    }
}

internal static class LegacyBinary
{
    internal static JsonObject Font(byte[] data)
    {
        if (data.Length < 8) throw new InvalidDataException("FNT字体头截断");
        int count = BitConverter.ToInt32(data, 0), height = BitConverter.ToInt32(data, 4);
        if (count < 0 || count > 65536 || data.Length < 8L + count * 12 || height is < 1 or > 255) throw new InvalidDataException("无效的FNT字体");
        var glyphs = new JsonObject();
        for (int i = 0; i < count; i++)
        {
            int o = 8 + i * 12; int U(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(o + offset));
            glyphs[U(0).ToString()] = new JsonArray(U(2), U(4), data[o + 6], data[o + 7], (sbyte)data[o + 8], (sbyte)data[o + 9], U(10));
        }
        return new() { ["height"] = height, ["glyphs"] = glyphs };
    }
    internal static JsonObject Animation(byte[] bytes)
    {
        int I(int offset) => BitConverter.ToInt32(bytes, offset);
        int U(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        float F(int offset) { float v = BitConverter.ToSingle(bytes, offset); if (!float.IsFinite(v)) throw new InvalidDataException("动画含非有限数字"); return v; }
        if (bytes.Length < 20 || Encoding.ASCII.GetString(bytes, 0, 4) != "BILE" || I(4) != 3 || I(8) != bytes.Length - 20 || U(12) != 20 || F(16) <= 0) throw new InvalidDataException("不支持的BILE动画");
        var chunks = new Dictionary<string, (int Offset, int Count)>(); int offset = 20, strings = -1, stringsEnd = -1;
        for (int n = 0; n < U(14); n++)
        {
            if (offset + 8 > bytes.Length) throw new InvalidDataException("动画块截断");
            string tag = Encoding.ASCII.GetString(bytes, offset, 4); int size = I(offset + 4);
            if (size < 12 || (long)offset + size > bytes.Length || chunks.ContainsKey(tag)) throw new InvalidDataException("动画块尺寸/重复错误");
            int stride = tag switch { "BELE" => 44, "BXDI" or "BMRF" or "BYAL" => 8, "BMTI" => 56, "BRTS" => 0, _ => throw new InvalidDataException("未知动画块: " + tag) };
            if (stride > 0 && (size < 16 || I(offset + 8) < 0 || size != 16L + I(offset + 8) * (long)stride)) throw new InvalidDataException("动画数组长度不符");
            chunks.Add(tag, (offset + 16, stride == 0 ? 0 : I(offset + 8)));
            if (tag == "BRTS") { strings = offset + 12; stringsEnd = offset + size; }
            offset += size;
        }
        if (offset != bytes.Length || strings < 0 || new[] { "BELE", "BXDI", "BMRF", "BYAL", "BMTI" }.Any(t => !chunks.ContainsKey(t))) throw new InvalidDataException("动画缺少数据块");
        int At(string tag, int index, int stride) => index >= 0 && index < chunks[tag].Count ? chunks[tag].Offset + index * stride : throw new InvalidDataException("动画引用越界");
        var elements = new JsonArray();
        for (int i = 0; i < chunks["BELE"].Count; i++)
        {
            int o = At("BELE", i, 44);
            elements.Add(new JsonObject { ["transform"] = new JsonArray(Enumerable.Range(0, 6).Select(c => (JsonNode)JsonValue.Create(F(o + c * 4))!).ToArray()),
                ["alpha"] = F(o + 24), ["start_frame"] = I(o + 28), ["playback_mode"] = I(o + 32), ["unknown_tail"] = Convert.ToHexStringLower(bytes.AsSpan(o + 36, 8)) });
        }
        var items = new JsonArray(); int li = 0, fi = 0, xi = 0;
        for (int i = 0; i < chunks["BMTI"].Count; i++)
        {
            int o = At("BMTI", i, 56), start = checked(strings + I(o + 4));
            if (start < strings || start >= stringsEnd) throw new InvalidDataException("动画名称越界");
            int end = Array.IndexOf(bytes, (byte)0, start, stringsEnd - start); if (end < 0) throw new InvalidDataException("动画名称未终止");
            var layers = new JsonArray(); int layerCount = I(o + 32);
            if (layerCount < 0 || layerCount > chunks["BYAL"].Count) throw new InvalidDataException("动画层数无效");
            for (int l = 0; l < layerCount; l++)
            {
                int framesCount = I(At("BYAL", li++, 8)); var frames = new JsonArray();
                if (framesCount < 0 || framesCount > chunks["BMRF"].Count) throw new InvalidDataException("动画帧数无效");
                for (int f = 0; f < framesCount; f++)
                {
                    int fo = At("BMRF", fi++, 8); var indices = new JsonArray();
                    for (int x = 0; x < U(fo + 2); x++) { int io = At("BXDI", xi++, 8); indices.Add(new JsonArray(I(io), I(io + 4))); }
                    frames.Add(new JsonObject { ["key"] = U(fo), ["indices"] = indices });
                }
                layers.Add(frames);
            }
            items.Add(new JsonObject { ["id"] = I(o), ["name"] = Encoding.UTF8.GetString(bytes, start, end - start), ["kind"] = I(o + 24),
                ["duration"] = I(o + 28), ["pivot"] = new JsonArray(F(o + 8), F(o + 12)), ["layers"] = layers });
        }
        if (li != chunks["BYAL"].Count || fi != chunks["BMRF"].Count || xi != chunks["BXDI"].Count) throw new InvalidDataException("动画有未引用记录");
        return new() { ["schema_version"] = 1, ["fps"] = F(16), ["items"] = items, ["elements"] = elements };
    }
}
