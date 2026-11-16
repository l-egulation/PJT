"""Compile explicitly provisional village attacks; approved artwork/timing is read-only."""
import hashlib
import json
import math
from pathlib import Path
from export_chapter1_enemies import compile_definitions

ROOT = Path(__file__).resolve().parents[2]


def compile_content():
    raw = (ROOT / 'data/combat/stage1_village_monsters.json').read_bytes()
    result = json.loads(raw)
    templates = {d['id']: d for d in compile_definitions(
        (ROOT / 'data/monsters/chapter1_approved.json').read_bytes())['monsters']}
    cue = result['windupCue']
    assert cue['shakeStepSeconds'] > 0 and 0 <= cue['shakeWidenFrom'] <= 1 and cue['flashSeconds'] > 0
    assert len(cue['flashColor']) == 3 and all(0 <= c <= 255 for c in cue['flashColor'])
    # Melee kinds whose body reddens over the wind-up up to tintMax alpha before the flash.
    assert all(isinstance(k, str) for k in cue['tintKinds']) and 0 < cue['tintMax'] <= 1
    assert len(cue['tintColor']) == 3 and all(0 <= c <= 255 for c in cue['tintColor'])
    for entry in result['monsters']:
        definition = templates[entry['template']].copy()
        if 'hitStaggerImmune' in entry:
            assert isinstance(entry['hitStaggerImmune'], bool)
            definition['hitStaggerImmune'] = entry['hitStaggerImmune']
        # The player takes this monster's damage without hit stagger (input lock) or knockback.
        if 'noPlayerHitReaction' in entry:
            assert isinstance(entry['noPlayerHitReaction'], bool)
            definition['noPlayerHitReaction'] = entry['noPlayerHitReaction']
        if 'maxHP' in entry:
            assert entry['maxHP'] > 0
            definition['maxHP'] = entry['maxHP']
        if 'damage' in entry:
            assert math.isfinite(entry['damage']) and entry['damage'] >= 0
            definition['damage'] = entry['damage']
        for key in ('id', 'bodySize', 'bodyOffset', 'hurtSize', 'hurtOffset', 'attackSize', 'attackOffset', 'walkSpeed',
                    'startGap', 'minimumStartDistance', 'prepare', 'chargeDistance', 'chargeSpeed', 'interval'):
            definition[key] = entry[key]
        # Village normals may widen the shared horizontal detection range; others keep the approved value.
        if 'senseX' in entry:
            definition['senseX'] = entry['senseX']
        folder = ROOT / 'resources/authoring/workbench/source/MonsterReference/approved' / entry['id']
        motion = lambda name: json.loads((folder / name / 'editor.json').read_text(encoding='utf-8-sig'))['ms']
        duration = sum(motion('attack')) / 1000
        if entry['attackKind'] == 'hook-claw':
            assert entry['hookSpeed'] > 0
            assert 0 <= entry['clawStart'] < entry['damageStart'] < entry['damageEnd'] <= duration
        definition.update(sourceSha256=hashlib.sha256(raw).hexdigest(),
                          active=entry['damageEnd'] - entry['prepare'], brake=0,
                          recovery=round(duration-entry['damageEnd'], 6),
                          moveCycle=sum(motion('walk')) / 1000,
                          deathDuration=sum(motion('death')) / 1000,
                          attackFrameDurations=[ms/1000 for ms in motion('attack')])
        # Thorn travel ends with the slice, rather than continuing during recovery.
        if entry['attackKind'] == 'dash':
            definition['active'] = entry['chargeDistance'] / entry['chargeSpeed']
            definition['recovery'] = round(duration-entry['prepare']-definition['active'], 6)
        assert definition['recovery'] >= 0 and entry['damageEnd'] <= duration
        # Wind-up telegraph: the runtime holds this approved frame prepareHoldExtra seconds longer and moves
        # prepare and every later authored time by the same amount; the frame must end before the damage window.
        if 'prepareHoldFrame' in entry:
            frames = motion('attack')
            assert 0 <= entry['prepareHoldFrame'] < len(frames) and entry['prepareHoldExtra'] >= 0
            assert sum(frames[:entry['prepareHoldFrame'] + 1]) / 1000 <= entry['prepare'] + 1e-6
        # Wind-up cue (shake, then a flash just before the hit) starts at this approved frame and must begin
        # before the damage window.
        if 'windupCueFrame' in entry:
            frames = motion('attack')
            assert 0 <= entry['windupCueFrame'] < len(frames)
            assert sum(frames[:entry['windupCueFrame']]) / 1000 < entry['damageStart']
        # Game-side size of the approved attack stroke; the delivered frames stay untouched.
        if 'attackEffectScale' in entry:
            assert 0 < entry['attackEffectScale'] <= 4
        if 'projectileSize' in entry:
            assert len(entry['projectileSize']) == 2 and all(math.isfinite(v) and v > 0 for v in entry['projectileSize'])
        if 'attackEffectStart' in entry:
            assert 0 <= entry['attackEffectStart'] <= entry['damageStart']
        if entry.get('burrow', {}).get('enabled'):
            b = entry['burrow']
            assert 0 < b['meltEnd'] < b['warningStart'] < b['eruptionStart'] < b['damageEnd'] <= entry['damageEnd']
            assert b['cooldown'] >= duration and b['maxTravel'] > 0 and b['groundSearch'] > 0
            assert len(b['attackSize']) == 2 and all(x > 0 for x in b['attackSize'])
        entry['definition'] = definition
    return result


if __name__ == '__main__':
    out = ROOT / 'unity/Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(compile_content(), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Exported four provisional village enemies; boss excluded.')
