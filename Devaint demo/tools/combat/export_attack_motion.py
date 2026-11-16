"""Transport game-owned body motion sources without importing or changing art."""
import copy
import hashlib
import json
import math
import os
from pathlib import Path
import struct
import tempfile
import special_frame_transport

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path('data/combat/presentation/base_sword_attack1.json')
CATALOG = Path('unity/Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json')
RAPIER_CHARGE = Path('data/combat/presentation/rapier_charge_presentation.json')
HEAVY_CATALOG = Path('unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json')
UNARMED = Path('data/combat/unarmed_basic.json')
HUB_BODIES = Path('data/story/hero_bodies.json')
REGISTRY = Path('data/combat/presentation/action_motion_registry.json')
MELEE_FRAMES = Path('data/combat/presentation/melee_frame_bindings.json')
EFFECTS = Path('data/combat/presentation/basic_attack_effects.json')
MOTION_ID = 'base_sword/attack1'
ACTION_ID = 'base_sword_basic_0'
MAXIMUM_SOURCE_BYTES = 1024 * 1024
FRAME_MODE = 'FrameTime'


def single(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError('Motion times must be finite numbers')
    try:
        value = struct.unpack('<f', struct.pack('<f', value))[0]
    except (OverflowError, struct.error) as error:
        raise ValueError('Motion time exceeds float range') from error
    if not math.isfinite(value):
        raise ValueError('Motion times must be finite numbers')
    return value


def validate_fixed(document):
    if not isinstance(document, dict) or type(document.get('schemaVersion')) is not int or document['schemaVersion'] != 1 or document.get('id') != MOTION_ID or document.get('actionId') != ACTION_ID or document.get('mode') != 'CombatTimeFixed' or document.get('fixedBinding') is not None or document.get('timeline') is not None:
        raise ValueError('Expected schemaVersion 1, first sword motion/action IDs and CombatTimeFixed mode without a frame timeline')
    steps = document.get('steps')
    if not isinstance(steps, list) or not 1 <= len(steps) <= 256:
        raise ValueError('Motion requires 1..256 steps')
    ids = set()
    duration = 0.0
    for step in steps:
        if not isinstance(step, dict) or any(not isinstance(step.get(key), str) or not step[key].strip() for key in ('id', 'frame')) or step['id'] in ids:
            raise ValueError('Motion needs unique stable step IDs and frame references')
        ids.add(step['id'])
        seconds = single(step.get('seconds'))
        next_time = single(duration + seconds)
        if seconds <= 0 or next_time <= duration:
            raise ValueError('Frame durations must advance source time')
        duration = next_time
    impact = single(document.get('sourceImpact'))
    cue = single(document.get('fxStart'))
    if not 0 < impact < duration or not (cue == -1 or 0 <= cue <= duration):
        raise ValueError('Fixed motion needs an interior source contact and Cue start in [0,duration] or -1')
    return document


def validate(document):
    if isinstance(document, dict) and document.get('mode') == 'CombatTimeFixed':
        return validate_fixed(document)
    if not isinstance(document, dict) or type(document.get('schemaVersion')) is not int or document['schemaVersion'] != 2 or document.get('id') != MOTION_ID or document.get('actionId') != ACTION_ID or document.get('mode') != FRAME_MODE:
        raise ValueError('Expected first sword schemaVersion 1 CombatTimeFixed or schemaVersion 2 FrameTime source')
    if set(document) != {'schemaVersion', 'id', 'actionId', 'mode', 'fixedBinding', 'timeline'}:
        raise ValueError('FrameTime gameplay times belong only to timeline; duplicate root timing fields are not allowed')
    validate_fixed(document.get('fixedBinding'))
    timeline = document.get('timeline')
    if not isinstance(timeline, dict) or set(timeline) != {'frames', 'markers'}:
        raise ValueError('FrameTime requires one frame timeline')
    frames, markers = timeline['frames'], timeline['markers']
    if not isinstance(frames, list) or not 1 <= len(frames) <= 256 or not isinstance(markers, list) or not 1 <= len(markers) <= 256:
        raise ValueError('FrameTime requires bounded frame and marker lists')
    for frame in frames:
        if not isinstance(frame, dict) or set(frame) != {'id', 'seconds'} or not isinstance(frame['id'], str) or not frame['id'].strip():
            raise ValueError('A frame needs its stable ID and duration')
        single(frame['seconds'])
    for marker in markers:
        if not isinstance(marker, dict) or set(marker) != {'id', 'anchor', 'reference', 'offset'} or not isinstance(marker['id'], str) or not marker['id'].strip() or type(marker['anchor']) is not int or marker['anchor'] not in range(4) or not isinstance(marker['reference'], str):
            raise ValueError('A marker needs its stable ID, supported anchor, reference and offset')
        single(marker['offset'])
    # Transport only. The C# frame compiler alone resolves graph/timing semantics.
    return document


def load(root=ROOT):
    path = Path(root) / SOURCE
    with path.open('rb') as file:
        raw = file.read(MAXIMUM_SOURCE_BYTES + 1)
    if len(raw) > MAXIMUM_SOURCE_BYTES:
        raise ValueError('Motion source exceeds the allowed file size')
    return validate(json.loads(raw.decode('utf-8-sig')))


def load_registry(root=ROOT):
    path = Path(root) / REGISTRY
    with path.open('rb') as file:
        raw = file.read(MAXIMUM_SOURCE_BYTES + 1)
    if len(raw) > MAXIMUM_SOURCE_BYTES:
        raise ValueError('Motion registry exceeds the allowed file size')
    return registry_transport(json.loads(raw.decode('utf-8-sig')))


def registry_transport(document):
    # Shape/finite-number transport only. MotionSourceRegistry in C# alone
    # compiles known media, complete action/variant coverage and body timing.
    if not isinstance(document, dict) or set(document) != {'schemaVersion', 'palettes', 'motions', 'actions'} or type(document['schemaVersion']) is not int or document['schemaVersion'] != 1:
        raise ValueError('Expected complete motion registry schemaVersion 1')
    for field in ('palettes', 'motions', 'actions'):
        if not isinstance(document[field], list) or not 1 <= len(document[field]) <= 256:
            raise ValueError('Motion registry lists require 1..256 entries')
    def text(value):
        if not isinstance(value, str) or not value.strip():
            raise ValueError('Stable motion identities and frame references are required')
    for palette in document['palettes']:
        if not isinstance(palette, dict) or set(palette) != {'id', 'form', 'clip', 'frames'} or not isinstance(palette['frames'], list) or not 1 <= len(palette['frames']) <= 256:
            raise ValueError('A known media palette needs its ID, form/clip and frame references')
        for key in ('id', 'form', 'clip'): text(palette[key])
        for frame in palette['frames']: text(frame)
    for motion in document['motions']:
        if not isinstance(motion, dict): raise ValueError('A motion source entry is required')
        for key in ('id', 'palette'): text(motion.get(key))
        if motion.get('delegatedSource'):
            if set(motion) - {'id', 'palette', 'delegatedSource', 'mode', 'steps', 'sourceImpact', 'fxStart'} or motion.get('mode') or motion.get('steps') or motion.get('sourceImpact', 0) != 0 or motion.get('fxStart', 0) != 0:
                raise ValueError('Delegated sources must not carry duplicated body timing')
            text(motion['delegatedSource'])
        else:
            if set(motion) - {'id', 'palette', 'delegatedSource', 'mode', 'steps', 'sourceImpact', 'fxStart'} or not isinstance(motion.get('steps'), list) or not 1 <= len(motion['steps']) <= 256:
                raise ValueError('A body motion needs bounded stable frame steps')
            text(motion.get('mode'))
            single(motion.get('sourceImpact')); single(motion.get('fxStart'))
            for step in motion['steps']:
                if not isinstance(step, dict) or set(step) != {'id', 'frame', 'seconds'}: raise ValueError('A body step needs its stable ID, known frame and weight')
                text(step['id']); text(step['frame']); single(step['seconds'])
    for action in document['actions']:
        if not isinstance(action, dict) or set(action) - {'actionId', 'kind', 'variant', 'form', 'clip', 'motion', 'fallback', 'frameTimingSource'} or not {'actionId', 'kind', 'variant', 'form', 'clip', 'motion', 'fallback'} <= set(action) or type(action['kind']) is not int or action['kind'] not in range(3) or type(action['variant']) is not int or type(action['fallback']) is not bool:
            raise ValueError('An explicit typed action/variant body binding is required')
        for key in ('actionId', 'form', 'clip', 'motion'): text(action[key])
        if action.get('frameTimingSource') not in (None, ''): text(action['frameTimingSource'])
    return document


def load_melee_frames(root=ROOT):
    with (Path(root) / MELEE_FRAMES).open('rb') as file:
        raw = file.read(MAXIMUM_SOURCE_BYTES + 1)
    if len(raw) > MAXIMUM_SOURCE_BYTES:
        raise ValueError('Melee frame source exceeds the allowed file size')
    document = json.loads(raw.decode('utf-8-sig'))
    if not isinstance(document, dict) or set(document) != {'schemaVersion', 'actions'} or document.get('schemaVersion') != 1 or not isinstance(document['actions'], list):
        raise ValueError('Expected complete melee frame source transport schema1')
    # No action/marker/interval interpretation here; C# owns those semantics.
    return document


def merge_heavy_media(catalog, root=ROOT):
    """Compose approved media before the one game-owned registry projection."""
    with (Path(root) / HEAVY_CATALOG).open('rb') as file:
        raw = file.read(MAXIMUM_SOURCE_BYTES + 1)
    if len(raw) > MAXIMUM_SOURCE_BYTES:
        raise ValueError('Heavy weapon media catalog exceeds the allowed file size')
    heavy = json.loads(raw.decode('utf-8-sig'))
    sets = heavy.get('sets')
    forms = {'hammer', 'shield', 'greatsword'}
    if not isinstance(sets, list) or len(sets) != len(forms) or any(not isinstance(group, dict) for group in sets) or {group.get('form') for group in sets} != forms:
        raise ValueError('The three approved heavy weapon media sets are required')
    result = copy.deepcopy(catalog)
    result['sets'] = [group for group in result.get('sets', []) if group.get('form') not in forms] + copy.deepcopy(sets)
    return result


def merge_unarmed_media(catalog, root=ROOT):
    result = copy.deepcopy(catalog)
    # The unarmed run shares village locomotion and owns its approved two-hit body clips.
    bodies = json.loads((Path(root)/HUB_BODIES).read_text(encoding='utf-8'))
    body = next(value for value in bodies['bodies'] if value['id'] == bodies['village'])
    unarmed = json.loads((Path(root)/UNARMED).read_text(encoding='utf-8'))
    clips = copy.deepcopy(body['clips'])
    for clip in clips:
        clip['frames'] = [body['root']+'/'+frame for frame in clip['frames']]
    clips.extend(copy.deepcopy(unarmed['presentation']))
    result['sets'] = [group for group in result['sets'] if group.get('form') != 'unarmed']
    # Its basic impact effect (white crescent, 2026-11-12) is authored with the clips in the same source.
    result['sets'].append(dict(form='unarmed', gameplayForm='unarmed', plainAfterimage=body.get('plainAfterimage',False), dashPoses=body.get('dashPoses'), clips=clips, effects=copy.deepcopy(unarmed.get('effects', []))))
    return result


def apply_registry(catalog, document):
    result = copy.deepcopy(catalog)
    result['motionRegistry'] = copy.deepcopy(registry_transport(document))
    for motion in document['motions']:
        if motion.get('delegatedSource'): continue
        targets = [clip for group in result.get('sets', []) for clip in group.get('clips', []) if group.get('form') + '/' + clip.get('name') == motion['id']]
        if len(targets) != 1: raise ValueError('One existing body media clip is required for each motion source')
        targets[0].update(stepIds=[step['id'] for step in motion['steps']], frames=[step['frame'] for step in motion['steps']],
                          seconds=[step['seconds'] for step in motion['steps']], contact=-1, sourceImpact=motion['sourceImpact'], fxStart=motion['fxStart'])
    return result


def merge_rapier_charge_media(catalog, root=ROOT):
    """Held body clips are presentation only; charge policy stays in combat data."""
    document = json.loads((Path(root) / RAPIER_CHARGE).read_text(encoding='utf-8'))
    clips = document['clips']
    if document['form'] != 'rapier' or {clip['name'] for clip in clips} != {'charge_prepare', 'charge_hold'} or len(clips) != 2:
        raise ValueError('The rapier charge preparation and hold clips are required')
    for clip in clips:
        if not clip['frames'] or len(clip['frames']) != len(clip['seconds']) or any(not math.isfinite(s) or s <= 0 for s in clip['seconds']):
            raise ValueError('Invalid rapier charge pose timing')
        if any(not frame.startswith('CharacterRapierChargeHold/') for frame in clip['frames']):
            raise ValueError('Unknown rapier charge pose media')
    result = copy.deepcopy(catalog)
    group = next(group for group in result['sets'] if group['form'] == 'rapier')
    group['clips'] = [clip for clip in group['clips'] if clip['name'] not in {'charge_prepare', 'charge_hold'}] + copy.deepcopy(clips)
    return result


def apply(catalog, document):
    validate(document)
    binding = document['fixedBinding'] if document['mode'] == FRAME_MODE else document
    sets = [s for s in catalog.get('sets', []) if s.get('form') == 'base_sword']
    if len(sets) != 1:
        raise ValueError('One basic sword art binding is required')
    clips = [c for c in sets[0].get('clips', []) if c.get('name') == 'attack1']
    if len(clips) != 1:
        raise ValueError('One first sword motion binding is required')
    clip = clips[0]
    palette = next((p for p in catalog.get('motionRegistry', {}).get('palettes', []) if p.get('id') == MOTION_ID), None)
    if palette is None:
        if clip.get('frames') != [s['frame'] for s in binding['steps']]: raise ValueError('Motion frame bindings differ; rebind art separately')
    elif any(step['frame'] not in palette['frames'] for step in binding['steps']):
        raise ValueError('First-sword steps must use only known supplied media frames')
    result = copy.deepcopy(catalog)
    target = next(c for s in result['sets'] if s['form'] == 'base_sword' for c in s['clips'] if c['name'] == 'attack1')
    target.update(stepIds=[s['id'] for s in binding['steps']], frames=[s['frame'] for s in binding['steps']],
                  seconds=[s['seconds'] for s in binding['steps']], contact=-1, sourceImpact=binding['sourceImpact'], fxStart=binding['fxStart'])
    previous = result.get('motionSources', [])
    if not isinstance(previous, list) or len(previous) > 1 or any(not isinstance(source, dict) or source.get('id') != MOTION_ID for source in previous):
        raise ValueError('Generated first-sword source slot contains unrelated or duplicate sources')
    if document['mode'] == FRAME_MODE:
        transported = copy.deepcopy(document)
        transported['timeline']['markers'].sort(key=lambda marker: marker['id'])
        result['motionSources'] = [transported]
    else:
        result.pop('motionSources', None)
    return result


def apply_effects(catalog, root=ROOT):
    """Game-owned placement of existing basic effects; art/timing stay intact."""
    document = json.loads((Path(root) / EFFECTS).read_text(encoding='utf-8-sig'))
    if document.get('schemaVersion') != 1 or not isinstance(document.get('effects'), list):
        raise ValueError('Expected basic effect placement schema1')
    result = copy.deepcopy(catalog)
    seen = set()
    for placement in document['effects']:
        key = (placement['form'], placement['attack'])
        if key in seen: raise ValueError('Duplicate basic effect placement')
        seen.add(key)
        targets = [effect for group in result['sets'] if group['form'] == key[0]
                   for effect in group.get('effects', []) if effect['attack'] == key[1]]
        if len(targets) != 1: raise ValueError('One existing basic effect is required')
        effect = targets[0]
        scale = single(placement['scale'])
        if scale <= 0: raise ValueError('Effect scale must be positive')
        for axis in ('x', 'y'):
            values = placement[axis]
            if not isinstance(values, list) or len(values) != len(effect['frames']):
                raise ValueError('Effect offsets must match its existing frames')
            effect[axis] = [single(value) for value in values]
        effect['scale'] = scale
        if 'rotation' in placement:
            effect['rotation'] = single(placement['rotation'])
    return result


def export(root=ROOT):
    root = Path(root)
    document = load(root)
    path = root / CATALOG
    original = path.read_bytes()
    media = merge_rapier_charge_media(merge_unarmed_media(merge_heavy_media(json.loads(original.decode('utf-8-sig')), root), root), root)
    result = apply(apply_registry(media, load_registry(root)), document)
    result['meleeFrames'] = copy.deepcopy(load_melee_frames(root))
    result = special_frame_transport.apply(result, special_frame_transport.load(root))
    result = apply_effects(result, root)
    raw = (json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode('utf-8')
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=path.name + '.', suffix='.tmp', delete=False) as file:
            temporary = Path(file.name)
            file.write(raw)
        if hashlib.sha256(path.read_bytes()).digest() != hashlib.sha256(original).digest():
            raise OSError('Animation catalog changed during generation; retry from its current binding')
        os.replace(temporary, path)
    finally:
        if temporary and temporary.exists():
            temporary.unlink()
    print('Published complete body motion registry, melee frame bindings and delegated first sword source; other clips, effects and image bindings preserved')


if __name__ == '__main__':
    export()
