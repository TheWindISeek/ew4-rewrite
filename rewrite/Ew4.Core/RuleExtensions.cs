using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record SkillSlot(string Id, string InstanceId = "", int Level = 1, int Order = 0, float? X = null, float? Y = null)
{
    public static void Validate(IEnumerable<SkillSlot> slots)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
            if (string.IsNullOrWhiteSpace(slot.Id) || string.IsNullOrWhiteSpace(slot.InstanceId) || slot.InstanceId.Length > 64
                || slot.InstanceId.Contains('/') || slot.Level < 1 || !ids.Add(slot.InstanceId) || slot.X.HasValue != slot.Y.HasValue
                || float.IsNaN(slot.X ?? 0) || float.IsInfinity(slot.X ?? 0) || float.IsNaN(slot.Y ?? 0) || float.IsInfinity(slot.Y ?? 0))
                throw new RuleViolation("extension.slot", "Invalid or duplicate skill instance");
    }
}
public sealed record ExtensionEffect
{
    public string Event { get; init; } = "combat.modify_damage";
    public string Operation { get; init; } = "damage_multiplier";
    public double Value { get; init; } = 1;
    public string? TargetTag { get; init; }
    public string? Script { get; init; }
    public string? Function { get; init; }
}
public sealed record ExtensionSkill
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";
    public string? Icon { get; init; }
    public string Kind { get; init; } = "passive";
    public string Target { get; init; } = "self";
    public int Range { get; init; } = 1;
    public int Cooldown { get; init; }
    public bool EndsAction { get; init; } = true;
    public Resources Cost { get; init; } = new(0, 0, 0);
    public IReadOnlyList<ExtensionEffect> Effects { get; init; } = Array.Empty<ExtensionEffect>();
}
public sealed record RuleScriptContext(string Event, Combatant? Source, Combatant? Target, int Level,
    int Round, double Value, IReadOnlyDictionary<string, long> State);
public sealed record RuleScriptResult(double Value, IReadOnlyDictionary<string, long>? State = null);
public interface IRuleScriptRuntime
{
    RuleScriptResult Run(string file, string function, RuleScriptContext context, Ew4Random random);
}

public sealed class RuleEventBus
{
    public IReadOnlyDictionary<string, ExtensionSkill> Skills { get; }
    public IRuleScriptRuntime? Scripts { get; }
    public RuleEventBus(IReadOnlyDictionary<string, ExtensionSkill> skills, IRuleScriptRuntime? scripts = null)
    { Skills = skills; Scripts = scripts; }

    public static IReadOnlyList<SkillSlot> Slots(Combatant unit) => unit.Profile.SkillSlots.Concat(unit.SkillSlots.Count > 0
        ? unit.SkillSlots : unit.Skills.Select((id, i) => new SkillSlot(id, "legacy." + i))).ToArray();

    public IEnumerable<(ExtensionSkill Skill, SkillSlot Slot)> Bound(Combatant unit) => Slots(unit)
        .Concat(unit.Profile.Traits.Select(id => new SkillSlot(id, "trait." + id)))
        .Where(s => Skills.ContainsKey(s.Id)).Select(s => (Skills[s.Id], s));

    public double Modify(string ev, string operation, double value, Combatant source, Combatant target, Ew4Random random)
    {
        foreach (var (skill, slot) in Bound(source).Where(p => p.Skill.Kind == "passive"))
            foreach (var effect in skill.Effects.Where(e => e.Event == ev && e.Operation == operation
                && (e.TargetTag == null || target.Profile.Traits.Contains(e.TargetTag))))
            {
                double amount = effect.Script == null ? effect.Value : Scripts!.Run(effect.Script, effect.Function!,
                    new(ev, source, target, slot.Level, 0, value, new Dictionary<string, long>()), random).Value;
                value = operation.EndsWith("multiplier", StringComparison.Ordinal) ? value * amount
                    : operation == "counterattack" ? Math.Min(value, amount) : value + amount;
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > int.MaxValue)
                    throw new RuleViolation("extension.value", "Invalid rule result: " + skill.Id);
            }
        return value;
    }

    public WorldState Dispatch(WorldState world, string ev, string unitId, Ew4Random random, string? onlyInstance = null, string? targetId = null)
    {
        var source = world.Units.SingleOrDefault(u => u.Id == unitId);
        if (source == null) return world;
        var target = targetId == null ? source : world.Units.Single(u => u.Id == targetId);
        var counters = world.ExtensionState.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var (skill, slot) in Bound(source.Combatant).Where(p => onlyInstance != null
            ? p.Slot.InstanceId == onlyInstance : p.Skill.Kind == "passive"))
            foreach (var effect in skill.Effects.Where(e => e.Event == ev
                && (e.TargetTag == null || target.Combatant.Profile.Traits.Contains(e.TargetTag))))
            {
                string prefix = unitId + "/" + slot.InstanceId + "/";
                var local = counters.Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal))
                    .ToDictionary(p => p.Key[prefix.Length..], p => p.Value, StringComparer.Ordinal);
                var result = effect.Script == null ? new RuleScriptResult(effect.Value) : Scripts!.Run(effect.Script, effect.Function!,
                    new(ev, source.Combatant, target.Combatant, slot.Level, world.Round, effect.Value, local), random);
                if (result.State != null) foreach (var pair in result.State)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || pair.Key.Contains('/'))
                        throw new RuleViolation("extension.state", "Invalid script state key");
                    counters[prefix + pair.Key] = pair.Value;
                }
                if (double.IsNaN(result.Value) || double.IsInfinity(result.Value) || result.Value < 0 || result.Value > int.MaxValue)
                    throw new RuleViolation("extension.value", "Invalid event result: " + skill.Id);
                int amount = checked((int)result.Value);
                if (effect.Operation == "heal")
                    world = world with { Units = world.Units.Select(u => u.Id != target.Id ? u : u with {
                        Combatant = u.Combatant with { Health = (int)Math.Min(u.Combatant.Profile.MaxHealth, (long)u.Combatant.Health + amount) } }).ToArray() };
                else if (effect.Operation == "money" || effect.Operation == "industry" || effect.Operation == "food")
                    world = world with { Factions = world.Factions.Select(f => f.Id != source.Owner ? f : f with {
                        Resources = new Resources(checked(f.Resources.Money + (effect.Operation == "money" ? amount : 0)),
                            checked(f.Resources.Industry + (effect.Operation == "industry" ? amount : 0)),
                            checked(f.Resources.Food + (effect.Operation == "food" ? amount : 0))) }).ToArray() };
            }
        return world with { ExtensionState = counters, RandomState = random.State };
    }
}

public static class WorldSkillActions
{
    public static WorldChange Execute(WorldState state, long revision, string unitId, string instanceId, string? targetId,
        RuleEventBus bus, WorldMoveRules movement)
    {
        state.Validate();
        var unit = state.Units.SingleOrDefault(u => u.Id == unitId);
        if (state.Revision != revision || state.Phase != TurnPhase.Active || state.PendingDialogue != null
            || unit == null || unit.Owner != state.ActiveFaction || unit.Acted || unit.Morale.Effective == -3)
            throw new RuleViolation("skill.unavailable", "Unit cannot use a skill now");
        var binding = bus.Bound(unit.Combatant).SingleOrDefault(p => p.Slot.InstanceId == instanceId);
        var skill = binding.Skill ?? throw new RuleViolation("skill.unknown", "Unknown skill instance");
        var target = targetId == null ? unit : state.Units.SingleOrDefault(u => u.Id == targetId);
        string key = unitId + "/" + instanceId + "/ready";
        if (skill.Kind != "active" || target == null || state.ExtensionState.TryGetValue(key, out long ready) && ready > state.Round)
            throw new RuleViolation("skill.unavailable", "Skill unavailable or cooling down");
        bool hostile = WorldDiplomacy.Hostile(state, unit.Owner, target.Owner, movement.NeutralCamps);
        bool allied = state.Factions.Single(f => f.Id == unit.Owner).Camp == state.Factions.Single(f => f.Id == target.Owner).Camp;
        if (skill.Target == "self" && target.Id != unit.Id || skill.Target == "ally" && !allied || skill.Target == "enemy" && !hostile
            || new HexMap(state.Width, state.Height).Distance(unit.Cell, target.Cell) > skill.Range)
            throw new RuleViolation("skill.target", "Invalid skill target");
        var faction = state.Factions.Single(f => f.Id == unit.Owner);
        if (faction.Resources.Money < skill.Cost.Money || faction.Resources.Industry < skill.Cost.Industry || faction.Resources.Food < skill.Cost.Food)
            throw new RuleViolation("skill.resources", "Insufficient resources");
        var next = state.Copy() with { Factions = state.Factions.Select(f => f.Id != unit.Owner ? f : f with { Resources = new(
            checked(f.Resources.Money - skill.Cost.Money), checked(f.Resources.Industry - skill.Cost.Industry), checked(f.Resources.Food - skill.Cost.Food)) }).ToArray() };
        next = bus.Dispatch(next, "skill.activate", unitId, new Ew4Random(state.RandomState), instanceId, target.Id);
        var counters = next.ExtensionState.ToDictionary(p => p.Key, p => p.Value);
        counters[key] = checked(state.Round + Math.Max(1, skill.Cooldown));
        next = next with { ExtensionState = counters, Revision = checked(revision + 1), Units = next.Units.Select(u => u.Id == unitId && skill.EndsAction
            ? u with { Acted = true, RemainingMovement = 0 } : u).ToArray() };
        next.Validate();
        return new(next, new[] { new WorldNotice("skill.used", unitId) });
    }
}
