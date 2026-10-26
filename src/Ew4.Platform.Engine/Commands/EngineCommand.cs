using System.Collections.Generic;
namespace Ew4.Platform.Engine;

/// <summary>
/// 局内命令基类；与封存 <c>ClientGame.Action</c> 的操作逐步对齐。
/// </summary>
public abstract record EngineCommand(long ExpectedRevision);

public sealed record MoveCommand(long ExpectedRevision, string UnitId, int DestinationCell)
    : EngineCommand(ExpectedRevision);

public sealed record AttackCommand(long ExpectedRevision, string UnitId, string TargetId)
    : EngineCommand(ExpectedRevision);

public sealed record EndTurnCommand(long ExpectedRevision)
    : EngineCommand(ExpectedRevision);

public sealed record TrainCommand(long ExpectedRevision, string UnitId)
    : EngineCommand(ExpectedRevision);

public sealed record UpgradeBuildingCommand(long ExpectedRevision, int Cell)
    : EngineCommand(ExpectedRevision);

public sealed record BuyTransportCommand(long ExpectedRevision, string UnitId)
    : EngineCommand(ExpectedRevision);

public sealed record BuildInstallationCommand(long ExpectedRevision, string UnitId, string InstallationKind)
    : EngineCommand(ExpectedRevision);

public sealed record RecruitCommand(long ExpectedRevision, int Cell, string OfferId, string UnitIdSeed)
    : EngineCommand(ExpectedRevision);

public sealed record DisbandCommand(long ExpectedRevision, string UnitId)
    : EngineCommand(ExpectedRevision);

/// <summary>确认队首对白。Waiting 为 Runtime 队列中的全部待处理 Flow Id（含队首），按处理顺序。</summary>
public sealed record DialogueAckCommand(long ExpectedRevision, IReadOnlyList<string> Waiting)
    : EngineCommand(ExpectedRevision);

/// <summary>选择剧情选项。Waiting 同上，队首应为该事件的弹窗。</summary>
public sealed record EventChoiceCommand(long ExpectedRevision, string EventId, string ChoiceId, IReadOnlyList<string> Waiting)
    : EngineCommand(ExpectedRevision);

public sealed record HoldCommand(long ExpectedRevision, string UnitId)
    : EngineCommand(ExpectedRevision);

public sealed record DeployCommanderCommand(long ExpectedRevision, string UnitId, string OfferId)
    : EngineCommand(ExpectedRevision);

public sealed record UseSkillCommand(
    long ExpectedRevision,
    string UnitId,
    string SkillInstanceId,
    string? TargetId,
    int TargetCell = -1)
    : EngineCommand(ExpectedRevision);

/// <param name="Cell">地图商店格；&lt; 0 表示指挥部商店（账户库存）。</param>
