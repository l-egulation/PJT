"""Compile main-owned hub sprite bindings, layout and story; image deployment is separate.

Source of truth: data/assets/starting_hub.json (paths relative to resources/, pivot/PPU/size).
Output: Hub/Data/hub-sprites.json, HubLayout.json, HubStory.json, HubOpening.json, HubBodies.json, HubVisits.json, HubRecordNote.json, HubRecordStories.json, HubOpeningGhosts.json and HubSkyParkour.json. The common pinned-resource
exporter owns PNG transport. This command never copies images or changes the deployment selection.
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path
from update_assets import resource_revision, backend

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/assets/starting_hub.json'
RESOURCES = ROOT / 'resources'
INDEX = ROOT / 'unity/Assets/Deviant/Hub/Data/hub-sprites.json'
# Station/building x positions (world units) read at runtime by StartingVillageWorld.
LAYOUT = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubLayout.json'
# First-visit narration (source data/story) copied for the village screens.
STORY_SOURCE = ROOT / 'data/story/starting_village_opening.json'
STORY = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubStory.json'
# Playable opening beats (docs/design/story/opening/opening-playable.md); the game validates every field on load.
OPENING_SOURCE = ROOT / 'data/story/opening_playable.json'
OPENING = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubOpening.json'
# The opening's guide ghosts: each lesson's demonstration recorded from the HTML prototype at 60 fps.
GHOSTS_SOURCE = ROOT / 'data/story/opening_ghosts.json'
GHOSTS = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubOpeningGhosts.json'
# Hero animation sets drawn in the village (bare-handed) and in the opening (the human before the ink).
BODIES_SOURCE = ROOT / 'data/story/hero_bodies.json'
BODIES = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubBodies.json'
# The village artisan's visits after each village palette piece (docs/design/progression/village-palette-pieces.md).
VISITS_SOURCE = ROOT / 'data/story/village_visits.json'
VISITS = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubVisits.json'
# The investigator's note the record desk shows before the first departure.
NOTE_SOURCE = ROOT / 'data/story/record_note.json'
NOTE = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubRecordNote.json'
RECORD_STORIES_SOURCE = ROOT / 'data/story/record_stories.json'
RECORD_STORIES = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubRecordStories.json'
# The sky path above the village (docs/design/world/village/starting-village.md 「하늘길」): course, launchers and the keeper's lines.
SKY_SOURCE = ROOT / 'data/hub/sky_parkour.json'
SKY = ROOT / 'unity/Assets/Deviant/Hub/Resources/HubSkyParkour.json'
RESOURCE_ROOTS = (ROOT / 'unity/Assets/Deviant/Hub/Resources', ROOT / 'unity/Assets/Deviant/Combat/Resources')


def record_stories_text():
    data = json.loads(RECORD_STORIES_SOURCE.read_text(encoding='utf-8'))
    assert data['schemaVersion'] == 1 and data['stories'], 'record stories need entries'
    ids, reads = set(), set()
    for story in data['stories']:
        assert story['id'] not in ids and story['readKey'] not in reads, 'record IDs/read keys must be unique'
        ids.add(story['id']); reads.add(story['readKey'])
        assert story['title'] and story['unlock'] in {'arrival', 'first-run', 'chapter1-clear', 'first-ink'}, story
        assert story['pages'] and all(p.get('text') or p.get('note') or p.get('paragraphs') for p in story['pages']), story
    return json.dumps(data, ensure_ascii=False, indent=2) + '\n'


def sky_text():
    sky = json.loads(SKY_SOURCE.read_text(encoding='utf-8'))
    assert sky['schemaVersion'] == 1 and 0 < sky['goalY'] and sky['worldWidth'] == 84, 'sky path needs a goal inside the 84-unit village'
    kinds = {'step', 'start', 'stone', 'crumble', 'goal'}
    for p in sky['platforms']:
        assert p['kind'] in kinds and p['w'] >= 1 and 0 <= p['x'] and p['x'] + p['w'] <= sky['worldWidth'], p
    for k in ('start', 'goal'):
        assert sum(p['kind'] == k for p in sky['platforms']) == 1, 'exactly one ' + k
    for w in sky['walls']:
        assert w['w'] > 0 and w['h'] > 0 and 0 <= w['x'] and w['x'] + w['w'] <= sky['worldWidth'], w
    for m in sky['movers']:
        assert m['speed'] > 0 and m['w'] >= 1, m
    for l in sky['launchers']:
        assert l['dir'] in (-1, 1) and l['period'] > sky['arrow']['warnSeconds'], l
    assert sky['zones'][0]['y'] == 0 and [z['y'] for z in sky['zones']] == sorted(z['y'] for z in sky['zones'])
    assert sky['keeper']['lines'] and sky['keeper']['welcome'], 'the keeper needs lines and a welcome'
    return json.dumps(sky, ensure_ascii=False, separators=(',', ':')) + '\n'


def ghosts_text():
    ghosts = json.loads(GHOSTS_SOURCE.read_text(encoding='utf-8'))
    assert ghosts['schemaVersion'] == 1 and ghosts['ghosts'], 'opening ghosts need tracks'
    for g in ghosts['ghosts']:
        n = len(g['x'])
        assert n and all(len(g[k]) == n for k in ('y', 'face', 'clip', 'clipTime', 'held', 'tapped', 'punch')), g['id'] + ': track lengths differ'
    return json.dumps(ghosts, ensure_ascii=False, separators=(',', ':')) + '\n'


def story_text():
    story = json.loads(STORY_SOURCE.read_text(encoding='utf-8'))
    assert story['schemaVersion'] == 1 and story['id'] and story['scenes'], 'story needs an id and scenes'
    steps = [step for scene in story['scenes'] for step in scene['steps']]
    assert steps == sorted(steps) and len(set(steps)) == len(steps), 'scenes must follow opening.md order'
    for scene in story['scenes']:
        assert scene['lines'] and scene['backdrop'].startswith('Hub/story/'), scene['backdrop']
        # Optional still characters per narration line (same count as lines); sprites live under Hub/story/characters.
        cast = scene.get('cast')
        if cast is not None:
            assert len(cast) == len(scene['lines']), 'cast needs one entry per line'
            for actor in (a for line in cast for a in line['actors']):
                for key in ('sprite', 'clip'):
                    if key in actor:
                        assert actor[key].startswith('Hub/story/characters/'), actor[key]
                        image = ROOT / 'unity/Assets/Deviant/Hub/Resources' / (actor[key] + ('.png' if key == 'sprite' else '/01.png'))
                        assert image.is_file(), 'missing character art: ' + str(image)
                assert 0 <= actor['x'] <= 960 and 0 < actor['baseline'] <= 540 and actor['scale'] in (1, 2), actor
    return json.dumps(story, ensure_ascii=False, indent=2) + '\n'


def opening_text():
    opening = json.loads(OPENING_SOURCE.read_text(encoding='utf-8'))
    ids = [beat['id'] for beat in opening['beats']]
    assert opening['schemaVersion'] == 1 and opening['id'] and ids, 'opening needs an id and beats'
    assert len(set(ids)) == len(ids), 'beat ids must be unique'
    return json.dumps(opening, ensure_ascii=False, indent=2) + '\n'


def bodies_text():
    bodies = json.loads(BODIES_SOURCE.read_text(encoding='utf-8'))
    ids = [body['id'] for body in bodies['bodies']]
    assert bodies['schemaVersion'] == 1 and len(set(ids)) == len(ids), 'bodies need unique ids'
    assert bodies['village'] in ids and bodies['opening'] in ids, 'village and opening name a body'
    for body in bodies['bodies']:
        # dashPoses: the five frames of a longer dash clip that the approved dash recipe plays (the weapons' five poses).
        if 'dashPoses' in body:
            dash = next(clip for clip in body['clips'] if clip['name'] == 'dash')
            poses = body['dashPoses']
            assert len(poses) == 5 and all(0 <= pose < len(dash['frames']) for pose in poses), body['id'] + ': dashPoses'
        for clip in body['clips']:
            assert clip['frames'] and len(clip['frames']) == len(clip['seconds']), body['id'] + '/' + clip['name']
            for frame in clip['frames']:
                rel = body['root'] + '/' + frame + '.png'
                assert any((root / rel).is_file() for root in RESOURCE_ROOTS), 'missing body frame: ' + rel
    return json.dumps(bodies, ensure_ascii=False, indent=2) + '\n'


def visits_text():
    visits = json.loads(VISITS_SOURCE.read_text(encoding='utf-8'))
    ids = [beat['id'] for beat in visits['beats']]
    assert visits['schemaVersion'] == 1 and len(set(ids)) == len(ids), 'visit beats need unique ids'
    parts = sorted({beat['part'] for beat in visits['beats']})
    # Part k plays after the k-th piece; a piece may have none (piece 3: visit 2 also opens the reclaim workshop).
    assert parts and parts[0] == 1 and all(type(p) is int for p in parts), 'visit parts are pieces, from 1'
    for part in parts:
        first = next(beat for beat in visits['beats'] if beat['part'] == part)
        assert first['trigger']['type'] == 'start', f'visit {part} starts with its first beat'
    lines = visits.get('firstRunDialogue', [])
    anchors = {a.get('id') for b in visits['beats'] if b['part'] == 1 for a in b['actions'] if a['type'] == 'bubble'}
    assert lines and all(l.get('id') in anchors and l.get('text') for l in lines), 'first-clear dialogue overrides need existing anchors and text'
    lesson = visits.get('firstRunSupplyLesson')
    assert isinstance(lesson, list) and lesson and all(a.get('type') for a in lesson), 'first-clear supply conversation needs actions'
    assert sum(a['type'] == 'pigment' for a in lesson) == 1, 'the combined lesson reveals pigment once'
    assert all(a.get('mode') in ('advance', 'auto') for a in lesson if a['type'] == 'bubble'), 'lesson bubbles need an advance mode'
    return json.dumps(visits, ensure_ascii=False, indent=2) + '\n'


def note_text():
    note = json.loads(NOTE_SOURCE.read_text(encoding='utf-8'))
    assert note['schemaVersion'] == 1 and note['lines'] and all(isinstance(l, str) and l for l in note['lines']), 'record note needs lines'
    for key in ('afterFirstRun', 'afterFirstInk'):
        assert isinstance(note.get(key), list) and note[key] and all(isinstance(l, str) and l for l in note[key]), f'{key} needs non-empty record entries'
    return json.dumps(note, ensure_ascii=False, indent=2) + '\n'


def target(path):
    if '/approved-hub-20261101/runtime/' in path:
        rel=path.split('/runtime/',1)[1]
        if rel.startswith('effects/'): return Path('effects/color-spread')/rel[8:]
        return Path('approved-background/'+rel.split('/',1)[1]) if rel.startswith('background/') else Path(rel)
    parts = Path(path).parts
    name = parts[-1]
    if parts[0] == 'environments':  # .../runtime/{buildings,props}/starting-hub/{color,sketch}/x.png
        return Path(parts[3]) / parts[5] / name
    if parts[0] == 'ui':
        return Path('icons') / name
    if parts[0] == 'effects':
        return Path('effects') / parts[3] / name
    if parts[0] == 'sequences':
        return Path('story') / name
    raise ValueError('unknown hub sprite group: ' + path)


def plan():
    resource_revision(ROOT)
    store = backend(ROOT).Store(RESOURCES)
    hub = json.loads(SOURCE.read_text(encoding='utf-8'))
    rows = []
    for sprite in hub['sprites']:
        data = store.read({'asset': store.owners[sprite['path']], 'path': sprite['path']})
        if data[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Hub source is not PNG data: ' + sprite['path'])
        size = list(struct.unpack('>II', data[16:24]))
        if size != sprite['size']:
            raise ValueError(f"{sprite['id']}: size {size} != {sprite['size']}")
        rel = target(sprite['path'])
        rows.append(dict(id=sprite['id'], source=sprite['path'], unity=('Assets/Deviant/Hub/Resources/Hub/' + rel.as_posix()),
                         resource=('Hub/' + rel.with_suffix('').as_posix()), size=size, ppu=sprite.get('ppu', 32),
                         pivot=sprite.get('pivot', [0.5, 0.5]), sha256=hashlib.sha256(data).hexdigest()))
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Verify the Unity copies and index without writing')
    parser.add_argument('--record-stories-only', action='store_true', help='Generate only the text-owned record story catalog; does not inspect or deploy media')
    parser.add_argument('--visits-only', action='store_true', help='Compile only the village dialogue, without inspecting media')
    args = parser.parse_args()
    if args.record_stories_only:
        text = record_stories_text()
        if args.check:
            return 0 if RECORD_STORIES.is_file() and RECORD_STORIES.read_text(encoding='utf-8') == text else 1
        RECORD_STORIES.write_text(text, encoding='utf-8', newline='\n')
        print('Generated record story text catalog; media deployment unchanged')
        return 0
    if args.visits_only:
        text = visits_text()
        if args.check:
            if not VISITS.is_file() or VISITS.read_text(encoding='utf-8') != text:
                print('HubVisits is out of date', file=sys.stderr)
                return 1
            print('HubVisits is up to date')
        else:
            VISITS.write_text(text, encoding='utf-8', newline='\n')
            print('compiled village visits')
        return 0
    rows = plan()
    layout = json.dumps(json.loads(SOURCE.read_text(encoding='utf-8'))['layoutProposal'], ensure_ascii=False, indent=2) + '\n'
    text = json.dumps(dict(format='deviant-hub-unity-sprites-v1', source='data/assets/starting_hub.json', sprites=rows), ensure_ascii=False, indent=2) + '\n'
    bad = []
    for row in rows:
        out = ROOT / 'unity' / row['unity']
        if args.check:
            if not out.is_file() or hashlib.sha256(out.read_bytes()).hexdigest() != row['sha256']:
                bad.append(row['unity'])
            continue
    if args.check:
        if not INDEX.is_file() or INDEX.read_text(encoding='utf-8') != text:
            bad.append(str(INDEX.relative_to(ROOT)))
        if not LAYOUT.is_file() or LAYOUT.read_text(encoding='utf-8') != layout:
            bad.append(str(LAYOUT.relative_to(ROOT)))
        if not STORY.is_file() or STORY.read_text(encoding='utf-8') != story_text():
            bad.append(str(STORY.relative_to(ROOT)))
        if not OPENING.is_file() or OPENING.read_text(encoding='utf-8') != opening_text():
            bad.append(str(OPENING.relative_to(ROOT)))
        if not BODIES.is_file() or BODIES.read_text(encoding='utf-8') != bodies_text():
            bad.append(str(BODIES.relative_to(ROOT)))
        if not VISITS.is_file() or VISITS.read_text(encoding='utf-8') != visits_text():
            bad.append(str(VISITS.relative_to(ROOT)))
        if not NOTE.is_file() or NOTE.read_text(encoding='utf-8') != note_text():
            bad.append(str(NOTE.relative_to(ROOT)))
        if not RECORD_STORIES.is_file() or RECORD_STORIES.read_text(encoding='utf-8') != record_stories_text():
            bad.append(str(RECORD_STORIES.relative_to(ROOT)))
        if not GHOSTS.is_file() or GHOSTS.read_text(encoding='utf-8') != ghosts_text():
            bad.append(str(GHOSTS.relative_to(ROOT)))
        if not SKY.is_file() or SKY.read_text(encoding='utf-8') != sky_text():
            bad.append(str(SKY.relative_to(ROOT)))
        if bad:
            print('out of date:', *bad, sep='\n  ', file=sys.stderr)
            return 1
        print(f'{len(rows)} hub sprites up to date')
        return 0
    INDEX.parent.mkdir(parents=True, exist_ok=True)
    INDEX.write_text(text, encoding='utf-8', newline='\n')
    LAYOUT.write_text(layout, encoding='utf-8', newline='\n')
    STORY.write_text(story_text(), encoding='utf-8', newline='\n')
    OPENING.write_text(opening_text(), encoding='utf-8', newline='\n')
    BODIES.write_text(bodies_text(), encoding='utf-8', newline='\n')
    VISITS.write_text(visits_text(), encoding='utf-8', newline='\n')
    NOTE.write_text(note_text(), encoding='utf-8', newline='\n')
    RECORD_STORIES.write_text(record_stories_text(), encoding='utf-8', newline='\n')
    GHOSTS.write_text(ghosts_text(), encoding='utf-8', newline='\n')
    SKY.write_text(sky_text(), encoding='utf-8', newline='\n')
    print(f'compiled bindings for {len(rows)} hub sprites; PNG deployment is unchanged')
    return 0


if __name__ == '__main__':
    sys.exit(main())
