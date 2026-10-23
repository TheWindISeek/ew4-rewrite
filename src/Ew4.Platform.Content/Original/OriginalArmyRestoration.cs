using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public sealed record OriginalRestoredUnit(int Health, int MaximumHealth, int Movement, int MaximumMovement, int Morale);

public static class OriginalArmyRestoration
{
    public static OriginalRestoredUnit Calculate(OriginalArmy source, int baseHealth, int baseMovement, int healthBonus,
        int movementBonus, int shortage, bool attached, bool leadership, bool logistics, bool legacy = false, bool immobile = false)
    {
        if (source.Health < 1 || source.MaximumHealth < 1 || baseHealth < 1 || baseMovement < 0 || healthBonus < 0 || movementBonus < 0
            || source.Morale < -3 || source.MoraleDuration < 0 || shortage < 0)
            throw new RuleViolation("legacy.army_state", $"Invalid army restoration: cell={source.Cell}, unit={source.Unit}, grade={source.Grade}, health={source.Health}/{source.MaximumHealth}, baseHealth={baseHealth}, movement={baseMovement}+{movementBonus}, healthBonus={healthBonus}, morale={source.Morale}/{source.MoraleDuration}, shortage={shortage}");
        int maximum = checked(baseHealth + (attached ? healthBonus : 0));
        int health = source.Health > source.MaximumHealth ? maximum
            : CommanderAttachment.ResizeHealth(source.Health, source.MaximumHealth, baseHealth, maximum, legacy);
        int budget = baseMovement == 0 || immobile && !legacy ? 0 : checked(baseMovement + (attached ? movementBonus : 0));
        int movement = Math.Max(0, (int)source.Movement);
        movement = legacy ? movement == 0 ? 0 : budget : Math.Min(movement, budget);
        int morale = TurnEconomy.Morale(source.Morale, source.MoraleDuration, 0, shortage, attached && logistics, attached && leadership);
        return new(health, maximum, movement, budget, morale);
    }
}

public sealed record OriginalUnitBinding(UnitState Unit, CommanderRuntime? Commander, Commander? Definition,
    OriginalArmy Source, IReadOnlyList<string> UnmappedGameplay);
public sealed record OriginalOwnedAssignment(int OriginalId, string Owner, OwnedCommander Commander, RosterDeploymentRules Rules);

public static class OriginalUnitImporter
{
    public static OriginalUnitBinding Import(OriginalArmy source, string unitId, string owner, string variant,
        CellState cell, OriginalRuntimeCatalog catalog, int shortage = 0, string? commanderId = null, OriginalOwnedAssignment? ownedAssignment = null)
    {
        if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(owner) || source.Cell < 0 || !cell.Terrain.Active || cell.Owner != owner
            || source.Flag58 > 1 || source.Flag58 == 1 && source.Commander >= 0 && ownedAssignment == null
            || source.Flag58 == 0 && ownedAssignment != null || source.ConstructionTurns < 0 || source.Veterancy < 0 || source.Burning < 0 || source.Burning > 3
            || source.Behavior > 3 || source.RestoreBehavior > 3 && source.BehaviorExpiry != 0 || source.BehaviorExpiry < 0
            || source.Transport > 1 || source.Acted > 1)
            throw new RuleViolation("legacy.army_state", $"Unsupported army state: cell={source.Cell}, unit={source.Unit}, grade={source.Grade}, owner={owner}, active={cell.Terrain.Active}, cellOwner={cell.Owner}, commander={source.Commander}, flag58={source.Flag58}, construction={source.ConstructionTurns}, veterancy={source.Veterancy}, burning={source.Burning}, behavior={source.Behavior}/{source.RestoreBehavior}/{source.BehaviorExpiry}, transport={source.Transport}, acted={source.Acted}, recovery={source.ForceRecovery}");
        var unassigned = catalog.SerializedUnit(variant, source.Unit, source.Grade);
        var profile = unassigned.Profile;
        var healthSource = profile.HasTrait("consumed_on_attack") && (source.Health == -999 || source.MaximumHealth == -999)
            ? source with { Health = 1, MaximumHealth = 1 } : source;
        OriginalCommanderDefinition? imported = null;
        if (source.Commander >= 0 && !catalog.Commanders.TryGetValue(source.Commander, out imported))
            throw new RuleViolation("legacy.commander_definition", "Unknown scenario commander");
        Commander? definition = imported == null ? null : imported.Definition with { Id = commanderId ?? imported.Definition.Id };
        CommanderRuntime? commander = definition == null ? null : new() { Id = definition.Id, Owner = owner,
            Rank = new(imported!.Rank, 0), Title = new(imported.Title, 0), RankHealthBonus = imported.HealthBonus, MaximumTrainingLevel = imported.Training,
            RankExperiencePercent = imported.RankExperiencePercent, TitleExperiencePercent = imported.TitleExperiencePercent };
        int titleHealing = imported?.TitleHealing ?? 0;
        if (ownedAssignment != null)
        {
            if (source.Flag58 != 1 || imported == null || source.Commander != ownedAssignment.OriginalId || ownedAssignment.Owner != owner
                || ownedAssignment.Commander.DefinitionId != imported.Definition.Id
                || commanderId != null && commanderId != ownedAssignment.Commander.Id)
                throw new RuleViolation("legacy.roster_assignment", "Saved roster assignment does not match its commander, owner or instance");
            var resolvedOwned = ownedAssignment.Rules.Resolve(ownedAssignment.Commander, owner);
            definition = resolvedOwned.Definition; commander = resolvedOwned.Runtime;
            titleHealing = ownedAssignment.Rules.Assignment.TitleHealing[commander.Title.Level];
        }
        if (definition != null && string.IsNullOrWhiteSpace(definition.Id)) throw new RuleViolation("legacy.commander_identity", "Missing assigned commander identity");
        var effects = definition == null ? null : Ew4Commander.Resolve(definition, profile.Category, catalog.SkillParameters);
        var restored = OriginalArmyRestoration.Calculate(healthSource, profile.MaxHealth, profile.Movement, commander?.RankHealthBonus ?? 0,
            effects?.MovementBonus ?? 0, shortage, definition != null, definition?.HasSkill(3) ?? false,
            effects?.FoodExempt ?? false, immobile: profile.Category == "fort");
        var basis = new Combatant { Profile = profile, Health = profile.MaxHealth, AtSea = cell.Terrain.Sea, BuildingKind = cell.BuildingKind };
        var support = new UnitSupport { FoodConsumption = unassigned.FoodConsumption };
        var resolved = definition == null ? basis : Ew4Commander.Attach(basis, definition, catalog.SkillParameters);
        var unit = new UnitState { Id = unitId, Owner = owner, Cell = source.Cell,
            Combatant = resolved with { Profile = resolved.Profile with { MaxHealth = restored.MaximumHealth, Movement = restored.MaximumMovement },
                Health = restored.Health, Morale = restored.Morale, ConstructionTurns = source.ConstructionTurns },
            Support = effects == null ? support : support with { FieldHealing = effects.FieldHealing, AdjacentHealing = effects.AdjacentHealing,
                AdjacentAttack = effects.AdjacentAttack, AdjacentDefense = effects.AdjacentDefense, TitleHealing = titleHealing,
                Logistics = effects.FoodExempt, IncomeMultiplier = effects.EconomicMultiplier },
            CommanderId = definition?.Id, CommanderBase = definition == null ? null : new(basis, support),
            Morale = new(source.Morale, source.MoraleDuration, restored.Morale), RemainingMovement = restored.Movement,
            Acted = source.Acted != 0, Objective = source.Objective == 1, Experience = source.Experience,
            Veterancy = source.Veterancy.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Transport = source.Transport != 0, Burning = source.Burning, ForceRecovery = source.ForceRecovery != 0,
            Behavior = source.Behavior.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RestoreBehavior = source.RestoreBehavior.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BehaviorExpiry = source.BehaviorExpiry == 0 ? null : source.BehaviorExpiry };
        var unmapped = new List<string>();
        if (source.Flag5c != 1) unmapped.Add("flag5c");
        if (source.State70 != -1) unmapped.Add("death_event26");
        unit.Combatant.Validate(); unit.Morale.Validate();
        return new(unit, commander, definition, source, unmapped.AsReadOnly());
    }
}
