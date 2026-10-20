"""Original transport attack penalty, immunity and coastal-gun target matrix."""
from dataclasses import asdict
import json
from validate_native_combat import Combatant, CombatProbe, reference, ROOT


def main():
    probe = CombatProbe()
    multipliers = json.loads((ROOT / 'reverse/native/rules_inventory.json').read_text(encoding='utf-8'))['tables']['weapon_category_multipliers']
    fixtures = []
    for category in range(3):
        for sea in (False, True):
            for skills, equipment in (((), ()), ((17,), ()), ((), ((12, 1, -1),))):
                a = Combatant(category=category, health=100, minimum_attack=20, maximum_attack=20, sea=sea, skills=skills, equipment=equipment)
                for target_sea in (False, True):
                    d = Combatant(health=100, minimum_attack=10, maximum_attack=10, sea=target_sea)
                    result = probe.run(a, d, 1, 7)
                    assert result == reference(a, d, 1, 7, multipliers)
                    fixtures.append(dict(attacker=asdict(a), defender=asdict(d), distance=1, seed=7, expected=result))
    coastal = Combatant(unit_id=21, category=4, health=100, minimum_attack=20, maximum_attack=20, maximum_range=3)
    for category in range(4):
        for sea in (False, True):
            d = Combatant(category=category, health=100, minimum_attack=10, maximum_attack=10, sea=sea)
            result = probe.run(coastal, d, 1, 7)
            assert result == reference(coastal, d, 1, 7, multipliers)
            fixtures.append(dict(attacker=asdict(coastal), defender=asdict(d), distance=1, seed=7, expected=result))
    output = dict(checks=len(fixtures), errors=0, fixtures=fixtures,
        scope='Original ARM64 81634 and original skill/item helpers; resolved terrain defense is supplied. Transport is a land-category unit at sea, not a naval category.')
    (ROOT / 'reverse/reports/transport_combat_vectors.json').write_text(json.dumps(output, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(checks=len(fixtures), errors=0)))


if __name__ == '__main__':
    main()
