using System.Collections.Generic;
using System;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Play.Match;

/// <summary>
/// 单槽撤销：只记住"上一次移动之前"的完整局面。只要之后发生过任何其他操作（版本号变化），撤销即失效，
/// 因此不会回滚购买、训练、战斗或随机数。
/// </summary>
public sealed class UndoSlot
{
    private GameSession? _before;
    private string _unit = "";
    private IReadOnlyList<int> _path = Array.Empty<int>();
    private long _afterRevision = -1;

    public void Clear() => _before = null;

    public void Record(GameSession before, GameSession after, string unitId, IReadOnlyList<int> path)
    {
        _before = before.Copy();
        _unit = unitId;
        _path = path.ToArray();
        _afterRevision = after.Revision;
    }

    /// <summary>当前可撤销的单位（局面未变时）。</summary>
    public string? UnitFor(GameSession current) =>
        _before != null && current.Revision == _afterRevision && current.World.Phase == TurnPhase.Active ? _unit : null;

    public bool Available(GameSession current, string? unit = null) =>
        UnitFor(current) is string owner && (unit == null || unit == owner);

    public (GameSession Restored, string Unit, int[] Path) Take(GameSession current, string? unit = null)
    {
        if (!Available(current, unit)) throw new RuleViolation("client.undo_move", "This unit has no available movement to undo");
        GameSession before = _before!;
        GameSession restored = before.Copy() with
        {
            Revision = checked(current.Revision + 1),
            World = before.World with { Revision = checked(current.World.Revision + 1) },
        };
        string moved = _unit;
        int[] path = _path.Reverse().ToArray();
        _before = null;
        return (restored, moved, path);
    }
}
