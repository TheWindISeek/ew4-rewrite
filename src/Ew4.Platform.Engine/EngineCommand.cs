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

public sealed record DeployCommand(long ExpectedRevision, string UnitId, string CommanderId)
    : EngineCommand(ExpectedRevision);

public sealed record DialogueAckCommand(long ExpectedRevision)
    : EngineCommand(ExpectedRevision);

public sealed record EventChoiceCommand(long ExpectedRevision, string EventId, string ChoiceId)
    : EngineCommand(ExpectedRevision);

public sealed record AiStepCommand(long ExpectedRevision)
    : EngineCommand(ExpectedRevision);
