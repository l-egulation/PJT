"""Compile the selected HTML handoff into the actual procedural combat candidates."""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/chapter1_selected_rooms.json'


def export():
    proposal = json.loads(SOURCE.read_text(encoding='utf-8'))
    collider = json.loads((ROOT / 'data/combat/player_hurtbox.json').read_text(encoding='utf-8'))['terrain_collider_design']
    half_width, half_height = (size / 2 for size in collider['size'])
    offsets = [collider['offset_facing_right'], collider['offset_facing_left']]
    left = min(offset[0] - half_width for offset in offsets)
    right = max(offset[0] + half_width for offset in offsets)
    top = max(offset[1] + half_height for offset in offsets)
    bottom = min(offset[1] - half_height for offset in offsets)
    target = ROOT / 'unity/Assets/ForestCombatV2/Resources/Chapter1Rooms.json'
    previous = json.loads(target.read_text(encoding='utf-8'))
    # Reference cap/middle cells are actual native authored platform cells.
    first = previous['rooms'][0]
    strip = next(t for t in first['terrain'] if t['kind'] == 'wood' and t['w'] >= 3)
    cells = [next(c['id'] for c in first['renderPlatforms']
                  if c['x'] == strip['x']+i and c['y'] == int(strip['y'])) for i in range(3)]
    aliases = {'01_thorn-root': '05_boar', '02_broom-stalker': '07_scarecrow', '03_gutter-spitter': '10_water_bucket'}
    result = []
    for r in proposal['rooms']:
        if r['kind'] != 'combat':
            continue
        assert len(r['monsters']) >= 10
        route = r.get('traversalRoute')
        if not route or len(route) < 2:
            raise ValueError(f"{r['id']}: authored traversalRoute is required; terrain order is not a route")
        for point in route:
            x, y = point['x'], point['y']
            if not (0.7 <= x <= r['width']-0.7 and 2 <= y < r['height']):
                raise ValueError(f"{r['id']}: traversal point is outside the safe room interior: {point}")
            if not any(abs(t['y']-y)<.001 and t['x']<=x<t['x']+t['w'] for t in r['terrain']):
                raise ValueError(f"{r['id']}: traversal point has no supporting surface: {point}")
            # Check both facings using the authored player collider geometry.
            if any(t['kind']!='wood' and x+left<t['x']+t['w'] and x+right>t['x'] and
                   y-top<t['y']+t['h'] and y-bottom>t['y'] for t in r['terrain']):
                raise ValueError(f"{r['id']}: traversal point overlaps solid terrain: {point}")
        for point, port in zip((route[0],route[-1]),r['ports']):
            expected_x=1 if port['side']=='left' else r['width']-1
            if point['x']!=expected_x or point['y']!=port['y']:
                raise ValueError(f"{r['id']}: traversal endpoints must match the authored doors")
        item = {k: copy.deepcopy(r[k]) for k in ['id','name','width','height','mirrorable','ports','terrain','monsters','decorations','traversalRoute']}
        item['renderGround'] = []
        item['renderPlatforms'] = []
        for t in r['terrain']:
            assert 0 <= t['x'] < r['width'] and t['x']+t['w'] <= r['width']
            assert 0 <= t['y'] < r['height'] and t['y']+t['h'] <= r['height']
            if t['kind'] == 'wood':
                for i in range(int(t['w'])):
                    item['renderPlatforms'].append(dict(x=int(t['x'])+i,y=int(t['y']),offsetY=t['y']%1,
                                                        id=cells[0 if i == 0 else 2 if i == t['w']-1 else 1]))
        for m in item['monsters']:
            m['species'] = aliases.get(m['species'],m['species'])
        result.append(item)
    assert len(result) == 12 and not any(r['id']=='sam_12' for r in result)
    target.write_text(json.dumps(dict(schema='chapter1-authored-rooms-v1',rooms=result),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (target.parent/'Chapter1SelectedRooms.json').write_text(json.dumps(proposal,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Exported 12 selected combat templates, exact positions and 17 room decoration layouts.')


if __name__ == '__main__':
    export()
