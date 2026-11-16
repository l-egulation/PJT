"""Resolve migrated melee hits without introducing a second editable numeric source."""
import json
from pathlib import Path

DEFAULT_ROOT = Path(__file__).resolve().parents[2] / 'data/combat'


def load_melee(root=DEFAULT_ROOT):
    root = Path(root).resolve()
    data = json.loads((root / 'melee_weapon_design.json').read_text(encoding='utf-8-sig'))
    seen = set()
    for form in data['forms']:
        basic = form['basic']
        count = len(basic['damages'])
        basic['active_durations'] = [basic['active_seconds']] * count
        sources = basic.get('hit_sources', [None] * count)
        if len(sources) != count:
            raise ValueError(f"{form['id']}: hit_sources length mismatch")
        for index, source in enumerate(sources):
            if source is None:
                continue
            if any(basic[key][index] is not None for key in ['damages', 'motion_durations', 'local_hit_times', 'shapes']):
                raise ValueError(f"{form['id']}[{index}]: duplicate hit source")
            path = (root / source).resolve()
            if not path.is_relative_to(root):
                raise ValueError('Hit source must remain inside data/combat')
            action = json.loads(path.read_text(encoding='utf-8-sig'))
            expected = f"{form['id']}_basic_{index}"
            if action['schemaVersion'] != 1 or action['id'] != expected or action['id'] in seen:
                raise ValueError(f'{source}: invalid schema, identity, or duplicate id')
            seen.add(action['id'])
            hit = action['hit']
            if hit.get('kind'):
                raise ValueError(f'{source}: only legacy melee hits are supported')
            box = hit['box']
            basic['damages'][index] = hit['damage']
            basic['motion_durations'][index] = hit['duration']
            basic['local_hit_times'][index] = hit['impact']
            basic['active_durations'][index] = hit['active']
            basic['shapes'][index] = dict(kind='box', size=[box['width'], box['height']],
                right_offset=[box['x'], box['y']], left_offset=[-box['x'], box['y']],
                reach_from_body_front=box['x'] + box['width'] / 2 - .65625,
                bottom=box['y'] - box['height'] / 2)
        if any(sources):
            if 'fastest_cycle' in basic:
                raise ValueError(f"{form['id']}: migrated cycle must be derived, not independently authored")
            basic['fastest_cycle'] = sum(basic['motion_durations'])
    return data
