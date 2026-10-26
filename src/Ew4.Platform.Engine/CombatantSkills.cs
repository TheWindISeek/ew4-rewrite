using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record SkillSlot(string Id, string InstanceId = "", int Level = 1, int Order = 0, float? X = null, float? Y = null)
{
    public static void Validate(IEnumerable<SkillSlot> slots)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.Id) || string.IsNullOrWhiteSpace(slot.InstanceId) || slot.InstanceId.Length > 64
                || slot.InstanceId.Contains('/') || slot.Level < 1 || !ids.Add(slot.InstanceId) || slot.X.HasValue != slot.Y.HasValue
                || float.IsNaN(slot.X ?? 0) || float.IsInfinity(slot.X ?? 0) || float.IsNaN(slot.Y ?? 0) || float.IsInfinity(slot.Y ?? 0))
            {
                throw new RuleViolation("combatant.skill_slot", "Invalid or duplicate skill instance");
            }
        }
    }
}

public sealed record SkillStatus(string Id, int RemainingTurns, int Level = 1, int Stacks = 1,
    string? SourceId = null, string? SourceOwner = null);

public static class CombatantSkills
{
    public static IReadOnlyList<SkillSlot> Slots(Combatant unit) => unit.Profile.SkillSlots.Concat(unit.SkillSlots.Count > 0
        ? unit.SkillSlots
        : unit.Skills.Select((id, i) => new SkillSlot(id, "legacy." + i))).ToArray();
}
