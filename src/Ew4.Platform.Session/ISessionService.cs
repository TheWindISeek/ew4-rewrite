using System.Collections.Generic;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Session;

public sealed record StartRequest(string ScenarioId, long ExpectedRevision = 0);

public sealed record SaveSlotId(int Mode, int Index);

public sealed record SaveResult(bool Ok, string? Code, string? Message);

/// <summary>
/// 面向 API 的会话视图；P3 实现时替换为强类型 DTO。
/// </summary>
public sealed record SessionView(long Revision, object Payload);

public sealed record SaveSlotInfo(SaveSlotId Slot, string Label, long Revision);

/// <summary>
/// 局内：当前一局与存读档。
/// </summary>
public interface ISessionService
{
    SessionView Catalog();

    SessionView Start(StartRequest request);

    SessionView Apply(EngineCommand command);

    SessionView Leave(long expectedRevision);

    SaveResult Save(SaveSlotId slot);

    SessionView Load(SaveSlotId slot);

    IReadOnlyList<SaveSlotInfo> Slots(int mode);
}

public sealed record AccountView(long Revision, object Payload);

/// <summary>
/// 局外：名册、商店、培养等。
/// </summary>
public interface IAccountService
{
    AccountView Get();

    AccountView Apply(EngineCommand command);
}
