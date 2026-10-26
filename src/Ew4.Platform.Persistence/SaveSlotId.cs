namespace Ew4.Platform.Persistence;

/// <summary>槽位坐标：<paramref name="Mode"/> 为游戏模式；<paramref name="Index"/> 为 0–6。</summary>
public sealed record SaveSlotId(int Mode, int Index);
