namespace Ew4.Interface.Editor;

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

public sealed record CommanderSkillOption(int LegacyId, string Ew4Id, string DisplayName);

public sealed record CommanderSkillCatalogView(IReadOnlyList<CommanderSkillOption> Skills);

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

public sealed record CommanderEditOutcome(int LegacyId, EditorWriteOutcome Written);
