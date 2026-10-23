using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Importers.Original;

/// <summary>
/// 原版 BTL v2 读取（自封存解析器迁入，仅供 Importer/往返测试）。
/// </summary>
public static class BtlReader
{
    public static BtlDocument Read(byte[] raw)
    {
        ReadOnlySpan<byte> data = raw;
        if (data.Length < 72 || BinaryPrimitives.ReadInt32LittleEndian(data) != 2)
        {
            throw new ContentError("original.btl", "Only BTL version 2 is supported.");
        }

        var header = new int[18];
        for (int i = 0; i < header.Length; i++)
        {
            header[i] = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(i * 4, 4));
        }

        return new BtlDocument(
            MapId: header[1],
            Width: header[4],
            Height: header[5],
            FactionCount: header[6],
            SiteCount: header[7],
            ArmyCount: header[8],
            EventCount: header[9],
            OriginalLength: data.Length);
    }

    /// <summary>
    /// 读取头摘要并与完整 <see cref="OriginalBattle"/> 解析结果对照（往返/导入验收用）。
    /// </summary>
    public static BtlDocument ReadAligned(byte[] raw)
    {
        BtlDocument summary = Read(raw);
        OriginalBattle battle = OriginalBattle.Read(raw);
        if (summary.MapId != battle.MapId
            || summary.FactionCount != battle.Factions.Count
            || summary.ArmyCount != battle.Armies.Count
            || summary.SiteCount != battle.Sites.Count
            || summary.EventCount != battle.Events.Count)
        {
            throw new ContentError(
                "original.btl",
                "BTL header counts do not match OriginalBattle parse for map " + battle.MapId + ".");
        }

        return summary;
    }

    public static string ReadCountryId(ReadOnlySpan<byte> factionRecord)
    {
        if (factionRecord.Length != 180)
        {
            throw new ContentError("original.btl", "Invalid faction record length.");
        }

        return ReadFixedString(factionRecord.Slice(24, 16));
    }

    private static string ReadFixedString(ReadOnlySpan<byte> data)
    {
        int end = data.IndexOf((byte)0);
        if (end < 0)
        {
            throw new ContentError("original.btl", "Unterminated string in BTL.");
        }

        return Encoding.UTF8.GetString(data[..end]);
    }
}

public sealed record BtlDocument(
    int MapId,
    int Width,
    int Height,
    int FactionCount,
    int SiteCount,
    int ArmyCount,
    int EventCount,
    int OriginalLength);
