using Ew4.Interface.Common;
using Ew4.Interface.Editor;
using Ew4.Platform.Content;
using PlatformCommander = Ew4.Platform.Editor.Commander;

namespace Ew4.Platform.Editor.InterfaceAdapters;

internal static class EditorInterfaceMapper
{
    public static CommanderTableView ToContract(PlatformCommander.CommanderTableView view) =>
        new(view.Entries.Select(ToContract).ToList());

    public static CommanderEntry ToContract(PlatformCommander.CommanderEntry entry) =>
        new(
            entry.LegacyId,
            entry.Name,
            entry.Infantry,
            entry.Cavalry,
            entry.Artillery,
            entry.Warship,
            entry.Fort,
            entry.Skill1,
            entry.Skill2,
            entry.Skill3,
            entry.Skill4,
            entry.Rank,
            entry.NobilityRank);

    public static CommanderSkillCatalogView ToContract(PlatformCommander.CommanderSkillCatalogView view) =>
        new(view.Skills.Select(s => new CommanderSkillOption(s.LegacyId, s.Ew4Id, s.DisplayName)).ToList());

    public static PlatformCommander.CommanderEdit ToPlatform(CommanderEdit edit) => edit switch
    {
        CommanderEditSkills s => new PlatformCommander.CommanderEditSkills(s.LegacyId, s.Skill1, s.Skill2, s.Skill3, s.Skill4),
        CommanderEditAptitudes a => new PlatformCommander.CommanderEditAptitudes(
            a.LegacyId, a.Infantry, a.Cavalry, a.Artillery, a.Warship, a.Fort),
        CommanderEditLoadout l => new PlatformCommander.CommanderEditLoadout(
            l.LegacyId,
            l.Skill1,
            l.Skill2,
            l.Skill3,
            l.Skill4,
            l.Infantry,
            l.Cavalry,
            l.Artillery,
            l.Warship,
            l.Fort),
        _ => throw new ArgumentException("Unknown commander edit type.", nameof(edit)),
    };

    public static CommanderEditOutcome ToContract(PlatformCommander.CommanderEditResult result) =>
        new(result.LegacyId, new EditorWriteOutcome(result.Saved.Path, ToContract(result.Saved.Diagnostics)));

    public static IReadOnlyList<Diagnostic> ToContract(IReadOnlyList<Diagnostic> diagnostics) => diagnostics;

}
