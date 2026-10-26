using System.Text.Json;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Serialization;

namespace Ew4.Service.Play;

/// <summary>
/// 持久账户（account.json）的内存工作副本。
/// 局内变化只改内存（<see cref="Set"/>），仅在明确的提交点写盘（<see cref="Commit"/>）；
/// 需要放弃时用 <see cref="Discard"/>，下次读取重新从磁盘载入。
/// </summary>
internal sealed class ProfileStore(string path, JsonSerializerOptions json)
{
    /// <summary>当前内存副本；未载入时为 null。</summary>
    public PlayerAccount? Current { get; private set; }

    /// <summary>内存副本是否有尚未写盘的变化。</summary>
    public bool Dirty { get; private set; }

    public bool Exists => PlayerAccountPersistence.Exists(path);

    /// <summary>战役（1）与征服（2）离开时提交账户；教程等其他模式丢弃。</summary>
    public static bool CommitsOnLeave(int mode) => mode is 1 or 2;

    /// <summary>从磁盘读取并替换内存副本（不标记为待提交）。</summary>
    public PlayerAccount Open()
    {
        Current = JsonSerializer.Deserialize<PlayerAccount>(PlayerAccountPersistence.Read(path), json)
            ?? throw new RuleViolation("account.format", "Invalid local account");
        Dirty = false;
        return Current;
    }

    /// <summary>直接替换内存副本，不标记为待提交（用于载入时的默认值/迁移结果）。</summary>
    public void Reset(PlayerAccount? account) => Current = account;

    /// <summary>更新内存副本；修订号变化才标记为待提交。</summary>
    public void Set(PlayerAccount next)
    {
        next.Validate();
        if (Current == null || next.Revision != Current.Revision) Dirty = true;
        Current = next;
    }

    /// <summary>有待提交变化或文件尚不存在时，原子写入 account.json。</summary>
    public void Commit()
    {
        if (Current == null || !Dirty && Exists) return;
        PlayerAccountPersistence.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(Current, json));
        Dirty = false;
    }

    /// <summary>强制在下次 <see cref="Commit"/> 时写盘（迁移结果需要持久化）。</summary>
    public void MarkDirty() => Dirty = true;

    /// <summary>丢弃未提交的变化。</summary>
    public void Discard()
    {
        if (!Dirty) return;
        Current = null;
        Dirty = false;
    }

    /// <summary>离开一局：按模式提交或丢弃。</summary>
    public void Leave(int mode)
    {
        if (CommitsOnLeave(mode)) Commit(); else Discard();
    }
}
