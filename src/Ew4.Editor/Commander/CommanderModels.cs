namespace Ew4.Editor.Commander;

/// <summary>
/// 将领表一行（<c>def_commander.xml</c>）。适性星与指挥部 UI 一致；
/// <c>skill1</c>–<c>skill4</c> 为技能表 <c>legacy_id</c>（见 <see cref="CommanderSkillCatalog"/>）。
/// </summary>
public sealed record CommanderEntry(
    int LegacyId,
    string Name,
    int Infantry,
    int Cavalry,
    int Artillery,
    int Warship,
    int Fort,
    int Skill1,
    int Skill2,
    int Skill3,
    int Skill4,
    int Rank,
    int NobilityRank);

public sealed record CommanderTableView(IReadOnlyList<CommanderEntry> Entries);

public abstract record CommanderEdit(int LegacyId);

public sealed record CommanderEditSkills(
    int LegacyId,
    int? Skill1 = null,
    int? Skill2 = null,
    int? Skill3 = null,
    int? Skill4 = null) : CommanderEdit(LegacyId);

public sealed record CommanderEditAptitudes(
    int LegacyId,
    int? Infantry = null,
    int? Cavalry = null,
    int? Artillery = null,
    int? Warship = null,
    int? Fort = null) : CommanderEdit(LegacyId);

/// <summary>HTTP / UI：技能槽 + 五维适性（与游戏内将领面板一致）。</summary>
public sealed record CommanderEditLoadout(
    int LegacyId,
    int Skill1,
    int Skill2,
    int Skill3,
    int Skill4,
    int Infantry,
    int Cavalry,
    int Artillery,
    int Warship,
    int Fort) : CommanderEdit(LegacyId);

public sealed record CommanderEditResult(int LegacyId, WriteResult Saved);
