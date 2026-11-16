"""Validate the roguelite progression rules and copy them to Unity Resources for the hub/profile runtime."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/roguelite_progression.json'
OUTPUT = ROOT / 'unity/Assets/Deviant/Hub/Resources/RogueliteProgression.json'
INVENTORY = ROOT / 'data/inventory/temporary-inks.json'
INK_PARTS = ROOT / 'data/combat/ink_color_parts_proposal.json'
# ink_part_unlock: a colour ink part that stays out of drops, maintenance draws and the boss altar until bought.
KINDS = {'palette_slot_count', 'first_slot_wildcard', 'starting_gold_bonus', 'destroyed_ink_gold_bonus', 'attack_bonus_percent', 'max_health_bonus',
         'bag_slot_count', 'move_speed_bonus_percent', 'dash_charges', 'ink_part_unlock',
         'shared_reroll_count', 'recovery_extra_uses', 'revive_count', 'weapon_break_gold', 'shop_extra_items',
         'starting_reward_count', 'starting_ink_reward_count', 'kill_gold_bonus_percent', 'ink_bundle_unlock', 'augment_bundle_unlock'}
DECIDED = {'trial-1', 'trial-2', 'trial-3', 'confirmed'}
TARGET_KINDS = KINDS | {'color_change_tickets', 'start_palette_rerolls', 'altar_rerolls', 'death_defiance'}
TARGET_DECIDED = DECIDED
# Ordered open conditions of the runtime list: the first settled run (death or return), then the first chapter-1 and
# chapter-2 boss clears.
GATES = ['first_run_settled', 'chapter1_boss_first_clear', 'chapter2_boss_first_clear']
# Tiered effects whose value is the tier's total; a run takes the highest owned tier, so tiers must rise along the chain.
TIERED = {'palette_slot_count': 'value', 'starting_gold_bonus': 'amount', 'attack_bonus_percent': 'value', 'max_health_bonus': 'value',
          'bag_slot_count': 'value', 'move_speed_bonus_percent': 'value', 'dash_charges': 'value'}
TIERED.update({kind: 'value' for kind in ('shared_reroll_count', 'recovery_extra_uses', 'revive_count',
              'weapon_break_gold', 'shop_extra_items', 'starting_reward_count', 'starting_ink_reward_count', 'kill_gold_bonus_percent')})
# The base dash holds one charge; an extra-dash unlock carries the run's total.
BASE_DASH_CHARGES = 1
# Village colouring of buildings without unlocks; unlock buildings colour by owning any of their unlocks.
COLOR_WHEN = {'chapter1_boss_first_clear', 'first_departure'}


def validate(data):
    assert data['schemaVersion'] == 1
    assert data['progressGates'] == GATES, 'runtime gates are the first settled run then the chapter-1 boss clear'
    load = data['startLoadout']
    assert load['selectedCount'] == 2 and load['paletteCount'] == 2
    assert 3 <= load['basePaletteSlots'] <= load['maxPaletteSlots'] <= 5
    currency = data['currency']
    if currency.get('dropAmounts') is not None:
        assert currency.get('dropAmountsStatus') in DECIDED, 'drop amounts need a decided status'
        assert all(type(v) is int and v >= 0 for v in currency['dropAmounts'].values())
    buildings = {b['id'] for b in data['buildings']}
    ids = [u['id'] for u in data['unlocks']]
    unlock_buildings = {u['building'] for u in data['unlocks']}
    for b in data['buildings']:
        if b['id'] in unlock_buildings:
            assert 'colorWhen' not in b, f"{b['id']} colours by its unlocks"
        elif 'colorWhen' in b:
            assert b['colorWhen'] in COLOR_WHEN, f"{b['id']}: unknown colorWhen {b['colorWhen']}"
    assert len(ids) == len(set(ids)), 'unlock ids must be unique'
    retired = data.get('retiredUnlocks', [])
    assert len({u['id'] for u in retired}) == len(retired), 'retired unlock ids must be unique'
    assert not set(ids) & {u['id'] for u in retired}, 'live and retired unlock ids must be separate'
    assert all(type(u['refund']) is int and u['refund'] >= 0 for u in retired), 'invalid retired refund'
    for u in data['unlocks']:
        assert u['building'] in buildings, u['id']
        assert u['effect']['kind'] in KINDS, u['id']
        assert u['maxPurchases'] == 1, u['id']
        if u['costStatus'] in DECIDED:
            assert type(u['cost']) is int and u['cost'] > 0, f"{u['id']} needs a positive price"
        else:
            assert u['costStatus'] == 'pending' and u['cost'] is None, f"{u['id']} undecided price must stay null"
        assert all(p in ids and p != u['id'] for p in u['prerequisites']), u['id']
        assert u['requiredClear'] in GATES, f"{u['id']}: unknown requiredClear {u.get('requiredClear')}"
        if u['effect']['kind'] in ('attack_bonus_percent', 'max_health_bonus', 'move_speed_bonus_percent'):
            assert type(u['effect']['value']) is int and u['effect']['value'] > 0, u['id']
        validate_bag_and_dash(u)
        validate_perks(u)
    validate_gates_and_tiers(data['unlocks'], GATES)
    validate_branches(data)
    validate_nodes(data)
    # The workshop is a prerequisite tree: every unlock follows its prerequisites, so the list reads from the roots
    # down and buying in list order never meets a missing prerequisite.
    seen = set()
    for u in data['unlocks']:
        assert set(u['prerequisites']) <= seen, f"{u['id']} is listed before its prerequisites {set(u['prerequisites']) - seen}"
        seen.add(u['id'])
    # A card or ink belongs to one bundle at most.
    for kind in ('ink_bundle_unlock', 'augment_bundle_unlock'):
        members = [m for u in data['unlocks'] if u['effect']['kind'] == kind for m in u['effect']['parts']]
        assert len(members) == len(set(members)), f'{kind}: a member appears in two bundles'
    # Village palette pieces (docs/design/progression/village-palette-pieces.md): buildings and unlocks open by piece count.
    pieces = data.get('palettePieces', 0)
    assert type(pieces) is int and pieces >= 0
    for entry in data['buildings'] + data['unlocks']:
        if 'openAtPieces' in entry:
            assert pieces > 0 and type(entry['openAtPieces']) is int and 0 <= entry['openAtPieces'] <= pieces, f"{entry['id']}: openAtPieces within 0..{pieces}"
    # A building may also wait for an open condition (the supply office opens after the first settled run).
    for b in data['buildings']:
        if 'requiredClear' in b:
            assert b['requiredClear'] in GATES, f"{b['id']}: unknown requiredClear {b['requiredClear']}"
    if 'targetRevision' in data:
        validate_target(data['targetRevision'], buildings)


def base_bag_slots():
    return json.loads(INVENTORY.read_text(encoding='utf-8'))['capacity']


def validate_bag_and_dash(u):
    """A bag tier holds more than the base bag (shared by every ink); an extra dash holds more than the one base charge."""
    kind, value = u['effect']['kind'], u['effect'].get('value')
    if kind == 'bag_slot_count':
        assert type(value) is int and value > base_bag_slots(), f"{u['id']}: bag slots must exceed the base bag"
    if kind == 'dash_charges':
        assert type(value) is int and value > BASE_DASH_CHARGES, f"{u['id']}: dash charges must exceed the base dash"
    if kind == 'ink_part_unlock':
        parts = {p['id'] for p in json.loads(INK_PARTS.read_text(encoding='utf-8'))['parts']}
        assert u['effect'].get('part') in parts, f"{u['id']}: unknown colour ink part {u['effect'].get('part')}"


def validate_perks(u):
    effect = u['effect']
    kind = effect['kind']
    if kind in TIERED and kind not in ('palette_slot_count', 'starting_gold_bonus', 'bag_slot_count', 'dash_charges'):
        assert type(effect.get('value')) is int and effect['value'] > 0, f"{u['id']}: positive tier total required"
    if kind == 'kill_gold_bonus_percent':
        assert effect['value'] <= 100, f"{u['id']}: gold bonus cannot exceed 100 percent"
    if kind == 'revive_count':
        assert type(effect.get('healthPercent')) is int and 0 < effect['healthPercent'] <= 100, f"{u['id']}: invalid revival health"
    if kind in ('ink_bundle_unlock', 'augment_bundle_unlock'):
        members = effect.get('parts')
        assert isinstance(members, list) and len(members) >= 2 and all(isinstance(m, str) and m for m in members), f"{u['id']}: bundle needs at least two members"
        assert len(members) == len(set(members)), f"{u['id']}: duplicate bundle member"
        if kind == 'ink_bundle_unlock':
            # Run inks of the sketch-weapon codex (user decision 2026-11-13): basic-attack and skill inks stay open by
            # default, so a bundle only holds inks of the other triggers.
            inks = run_inks()
            assert set(members) <= set(inks), f"{u['id']}: unknown run ink {set(members) - set(inks)}"
            assert not {inks[m] for m in members} & BASE_INK_TRIGGERS, f"{u['id']}: basic-attack and skill inks are open by default"
        else:
            content = json.loads((ROOT / 'data/combat/maintenance_augments_proposal.json').read_text(encoding='utf-8'))
            valid = {p['id'] for p in content['augments'] if p.get('runtime') == 'implemented'}
            assert set(members) <= valid, f"{u['id']}: unknown bundle member {set(members) - valid}"


# Run ink triggers whose inks every profile owns from the start.
BASE_INK_TRIGGERS = {'basic_attack', 'skill_attack'}


def run_inks():
    """Run ink id -> trigger, from the same source and projection as export_run_inks.py."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from export_run_inks import SOURCE as RUN_INK_SOURCE, catalog
    return {ink['id']: ink['trigger'] for ink in catalog(json.loads(RUN_INK_SOURCE.read_text(encoding='utf-8')))['inks']}


def validate_target(target, buildings):
    """The decided-but-unimplemented unlock list; Unity's JsonUtility ignores this field until it is migrated."""
    assert target['status'] == 'decided-not-implemented'
    bag = target['bag']
    assert bag['baseSlots'] == base_bag_slots(), 'the target bag starts from the current shared bag'
    assert bag['baseSlots'] + 5 * bag['slotsPerTier'] == bag['maxSlots']
    ids = [u['id'] for u in target['unlocks']]
    assert len(ids) == len(set(ids)), 'target unlock ids must be unique'
    for u in target['unlocks']:
        assert u['building'] is None or u['building'] in buildings, u['id']
        assert u['effect']['kind'] in TARGET_KINDS, u['id']
        assert u['maxPurchases'] == 1, u['id']
        if u['costStatus'] in TARGET_DECIDED:
            assert type(u['cost']) is int and u['cost'] > 0, f"{u['id']} needs a positive price"
        else:
            assert u['costStatus'] == 'pending' and u['cost'] is None, f"{u['id']} undecided price must stay null"
        assert all(p in ids and p != u['id'] for p in u['prerequisites']), u['id']
        if u['effect']['kind'] == 'bag_slot_count':
            assert bag['baseSlots'] < u['effect']['value'] <= bag['maxSlots'], u['id']
        validate_bag_and_dash(u)
    validate_tree(target['unlockTree'], target['unlocks'])


def validate_gates_and_tiers(unlocks, gates):
    """No unlock opens on an earlier gate than a prerequisite, and a tier never lowers the total of the tier before it."""
    by_id = {u['id']: u for u in unlocks}
    for u in unlocks:
        for p in u['prerequisites']:
            parent = by_id[p]
            assert gates.index(parent['requiredClear']) <= gates.index(u['requiredClear']), f"{u['id']} opens before its parent {p}"
            field = TIERED.get(u['effect']['kind'])
            if field and parent['effect']['kind'] == u['effect']['kind']:
                assert u['effect'][field] > parent['effect'][field], f"{u['id']} must raise the total of {p}"


def validate_branches(data):
    """Workshop tree tabs: every unlock names a listed branch, every branch has a root, and the tiers of one effect
    (the line the workshop draws as one row) stay in one branch."""
    branches = data['branches']
    ids = [b['id'] for b in branches]
    assert ids and len(ids) == len(set(ids)), 'branch ids must be unique'
    assert all(b['name'] for b in branches), 'every branch needs a tab name'
    by_id = {u['id']: u for u in data['unlocks']}
    for u in data['unlocks']:
        assert u.get('branch') in ids, f"{u['id']}: unknown branch {u.get('branch')}"
        for p in u['prerequisites']:
            parent = by_id[p]
            if parent['effect']['kind'] == u['effect']['kind']:
                assert parent['branch'] == u['branch'], f"{u['id']} leaves the branch of its lower tier {p}"
    for b in ids:
        assert any(u['branch'] == b and not u['prerequisites'] for u in data['unlocks']), f'branch {b} needs a root'


NODE_LEVELS = 4
ROMAN = ['I', 'II', 'III', 'IV', 'V']


def tier_lines(unlocks):
    """Same-effect chains, lowest tier first, as ProgressionCatalog.FirstTier/NextTier walk them."""
    by_id = {u['id']: u for u in unlocks}
    lower = {u['id']: next((p for p in u['prerequisites'] if by_id[p]['effect']['kind'] == u['effect']['kind']), None) for u in unlocks}
    lines = []
    for u in unlocks:
        if lower[u['id']] is not None:
            continue
        line = [u]
        while True:
            higher = [v for v in unlocks if lower[v['id']] == line[-1]['id']]
            if not higher:
                break
            assert len(higher) == 1, f"{line[-1]['id']} is the lower tier of {[v['id'] for v in higher]}"
            line.append(higher[0])
        lines.append(line)
    return lines


def validate_nodes(data):
    """Workshop nodes (user decision 2026-11-15): the tiers of one tiered effect form nodes of four named '<line> I',
    '<line> II', ... and a remainder starts the next numeral; a single unlock or a bundle is its own node without a
    numeral. Inside a node a tier only follows the tier before it; a link from another node enters at a node's first
    tier and needs that other node's last tier, so a node opens what it leads to only once it is complete."""
    unlocks = data['unlocks']
    assert all(isinstance(u.get('node'), str) and u['node'] for u in unlocks), 'every unlock names its node'
    node_of = {u['id']: u['node'] for u in unlocks}
    members = {}
    for line in tier_lines(unlocks):
        if line[0]['effect']['kind'] in TIERED and len(line) > 1:
            name = line[0]['node']
            assert name.endswith(' I'), f"{line[0]['id']}: a tiered line starts at node I, got {name}"
            stem = name[:-2]
            for i, u in enumerate(line):
                want = f'{stem} {ROMAN[i // NODE_LEVELS]}'
                assert u['node'] == want, f"{u['id']}: tier {i + 1} belongs to node {want}, got {u['node']}"
        else:
            for u in line:
                assert u['node'].split(' ')[-1] not in ROMAN, f"{u['id']}: a single unlock has no numeral"
        for u in line:
            members.setdefault(u['node'], []).append(u)
    lines_of = {}
    for line in tier_lines(unlocks):
        for u in line:
            lines_of.setdefault(u['node'], set()).add(line[0]['id'])
    assert all(len(v) == 1 for v in lines_of.values()), f'a node name is shared by two lines: {[k for k, v in lines_of.items() if len(v) > 1]}'
    for name, levels in members.items():
        for i, u in enumerate(levels):
            if i > 0:
                assert u['prerequisites'] == [levels[i - 1]['id']], f"{u['id']}: inside {name} a tier only follows {levels[i - 1]['id']}"
                continue
            for p in u['prerequisites']:
                assert node_of[p] != name, f"{u['id']}: {p} is in the same node"
                last = members[node_of[p]][-1]['id'] if node_of[p] in members else p
                assert p == last, f"{u['id']}: link from {node_of[p]} needs its last tier {last}, not {p}"


def validate_tree(tree, unlocks):
    """Independent pre-boss lines that open after the first settled run, plus one tree rooted at the chapter-1 gate.
    Every node has one parent and none opens on an earlier gate than its parent."""
    assert tree['shape'] == 'pre-boss-lines-and-boss-tree'
    gates = tree['progressGates']
    assert len(gates) == len(set(gates)) and set(tree['unimplementedGates']) <= set(gates)
    assert gates[:len(GATES)] == GATES, 'the target keeps the runtime gate order'
    by_id = {u['id']: u for u in unlocks}
    roots = [u['id'] for u in unlocks if not u['prerequisites']]
    lines = tree['preBossLineRoots']
    assert sorted(roots) == sorted([tree['root']] + lines), f'roots must be the boss tree and the pre-boss lines, got {roots}'
    assert by_id[tree['root']]['requiredClear'] == gates[1], 'the boss tree opens on the chapter-1 boss clear'
    assert all(by_id[r]['requiredClear'] == gates[0] for r in lines), 'pre-boss lines open after the first settled run'
    for u in unlocks:
        assert u['requiredClear'] in gates, u['id']
        assert len(u['prerequisites']) <= 1, f"{u['id']} must have a single parent"
    validate_gates_and_tiers(unlocks, gates)
    reached, frontier = set(roots), list(roots)
    while frontier:
        parent = frontier.pop()
        children = [u['id'] for u in unlocks if parent in u['prerequisites']]
        reached.update(children)
        frontier.extend(children)
    assert reached == set(by_id), f'unreachable nodes: {set(by_id) - reached}'


def render():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    validate(data)
    return json.dumps(data, ensure_ascii=False, indent=2) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Fail when the Unity copy differs from the source')
    args = parser.parse_args()
    text = render()
    if args.check:
        if not OUTPUT.is_file() or OUTPUT.read_text(encoding='utf-8') != text:
            print(f'{OUTPUT.relative_to(ROOT)} is out of date; run without --check', file=sys.stderr)
            return 1
        print('roguelite progression is up to date')
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(text, encoding='utf-8', newline='\n')
    print(f'wrote {OUTPUT.relative_to(ROOT)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
