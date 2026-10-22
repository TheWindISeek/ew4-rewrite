using Ew4.Core;

namespace Ew4.Legacy;

public sealed record ExtensionItemBinding(string Target, SkillSlot[]? Skills = null, SkillSlot? UseSkill = null,
    bool ReplaceLegacyEffect = false, int? Value = null, string? TargetCategory = null, bool? RequiresFlagSkill = null);

public sealed partial class ExtensionLoader
{
    public IReadOnlyList<ExtensionItemBinding> ItemBindings { get; private init; } = [];

    private static void ValidateItems(ExtensionItemBinding[] items, IReadOnlyDictionary<string, ExtensionSkill> skills)
    {
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in items)
        {
            if (binding == null || binding.Target == null || !binding.Target.StartsWith("ew4.item.", StringComparison.Ordinal)
                || !int.TryParse(binding.Target[9..], out int id) || id < 0 || binding.Target != "ew4.item." + id
                || !targets.Add(binding.Target) || binding.Value < 0 || binding.TargetCategory != null && string.IsNullOrWhiteSpace(binding.TargetCategory))
                throw new RuleViolation("extension.item_binding", "Invalid or duplicate item binding");
            var slots = (binding.Skills ?? []).Concat(binding.UseSkill == null ? [] : new[] { binding.UseSkill }).ToArray();
            SkillSlot.Validate(slots);
            foreach (var slot in slots)
            {
                if (slot == null || !skills.TryGetValue(slot.Id, out var skill) || ItemSlot(binding.Target, slot).InstanceId.Length > 64)
                    throw new RuleViolation("extension.item_binding", "Item skills must reference extension definitions: " + binding.Target);
                if (slot == binding.UseSkill && (skill.Kind != "active" || skill.Target != "self" || skill.Cooldown != 0
                    || skill.EndsAction || skill.Cost != new Resources(0, 0, 0)))
                    throw new RuleViolation("extension.item_binding", "useSkill requires active/self, endsAction:false, cooldown:0 and zero cost");
            }
        }
    }

    private static SkillSlot ItemSlot(string item, SkillSlot slot) => slot with { InstanceId = "item." + item[9..] + "." + slot.InstanceId };

    public Equipment ConfigureItem(Equipment item)
    {
        var binding = ItemBindings.SingleOrDefault(b => b.Target == item.Id);
        if (binding == null) return item;
        return item with
        {
            Effect = binding.ReplaceLegacyEffect ? "" : item.Effect,
            Value = binding.Value ?? item.Value,
            TargetCategory = binding.TargetCategory == null ? item.TargetCategory
                : binding.TargetCategory == "all" ? null : binding.TargetCategory,
            RequiresFlagSkill = binding.RequiresFlagSkill ?? item.RequiresFlagSkill,
            SkillSlots = binding.Skills?.Select(s => ItemSlot(item.Id, s)).ToArray(),
            UseSkill = binding.UseSkill == null ? null : ItemSlot(item.Id, binding.UseSkill)
        };
    }
}
