"""Build an offline sound editor from approved art and game-owned timing.

python tools/audio/weapon_sfx_studio/build.py --package approved.zip --thrown-package thrown.zip \
    --sound selected-swing.wav --output weapon-sfx-studio.html
Image payloads and the generated HTML stay in the external asset workspace.
"""
import argparse
import base64
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RES = ROOT / 'unity/Assets/Deviant/Combat/Resources'
FORMS = {'sword': 'base_sword', 'cannon': 'launcher', 'laser': 'launcher_laser',
         'shotgun': 'launcher_shotgun', 'grenade': 'launcher_grenade'}


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def decode(uri):
    return Image.open(io.BytesIO(base64.b64decode(uri.split(',', 1)[1]))).convert('RGBA')


def png(image):
    stream = io.BytesIO()
    image.save(stream, 'PNG')
    return 'data:image/png;base64,' + base64.b64encode(stream.getvalue()).decode()


def make_payload(package, thrown_package, sound):
    with zipfile.ZipFile(package) as z:
        name = next(n for n in z.namelist() if n.endswith('/viewer-data.json'))
        raw = z.read(name)
    manifest = load(ROOT / 'resources/authoring/workbench/source/WeaponReference/확정11종_20261021/manifest.json')
    if hashlib.sha256(raw).hexdigest() != manifest['sourceHashes']['viewer-data.json']:
        raise ValueError('The approved eleven-weapon source hash does not match this checkout.')
    art = json.loads(raw)
    catalog = load(RES / 'CharacterV2/AnimationCatalog.json')
    if any(a.get('mode', 0) != 0 for key in ['meleeFrames', 'specialFrames']
           for a in catalog[key]['actions']):
        raise ValueError('FrameTime bindings require a direct frame graph projection; fixed-time approximation is not allowed.')
    sets = {s['form']: s for s in catalog['sets']}
    sets.update({s['form']: s for s in load(RES / 'CharacterV2/HeavyWeaponCatalog.json')['sets']})
    expected = {s['path'].split('/Resources/')[1][:-4]: s['sha256'] for s in manifest['sprites']}
    frames = {}
    for weapon in art['weapons']:
        form = FORMS.get(weapon['id'], weapon['id'])
        for i, (key, uri) in enumerate(weapon['frames'].items()):
            ref = f'CharacterApproved11/{form}/F{i:03}'
            if hashlib.sha256(base64.b64decode(uri.split(',')[1])).hexdigest() != expected[ref]:
                raise ValueError('Character pixels differ from the approved manifest: ' + ref)
            p = weapon['pivots'].get(key, weapon['pivot'])
            o = weapon['offsets'].get(key, [0, 0])
            frames[ref] = {'src': uri, 'pivot': [p[0]-o[0], p[1]-o[1]]}
        for hit in weapon['hits']:
            events = [e for e in weapon['fx'] if e['hit'] == hit['number'] and decode(e['png']).getbbox()]
            if not events:
                continue
            start, end = min(e['start'] for e in events), max(e['end'] for e in events)
            left, top = min(e['x'] for e in events), min(e['y'] for e in events)
            right = max(e['x']+e['width'] for e in events)
            bottom = max(e['y']+e['height'] for e in events)
            previous = None
            index = 0
            for t in range(start, end, 10):
                canvas = Image.new('RGBA', (right-left, bottom-top))
                for e in events:
                    if not e['start'] <= t < e['end']:
                        continue
                    image = decode(e['png'])
                    if e.get('flip'):
                        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    if image.size != (e['width'], e['height']):
                        image = image.resize((e['width'], e['height']), Image.Resampling.NEAREST)
                    alpha = min(1, (t-e['start'])/e['fadeIn'] if e.get('fadeIn') else 1,
                                (e['end']-t)/e['fadeOut'] if e.get('fadeOut') else 1)
                    if alpha < 1:
                        image.putalpha(image.getchannel('A').point(lambda a: round(a*alpha)))
                    canvas.alpha_composite(image, (e['x']-left, e['y']-top))
                pixels = canvas.tobytes()
                if pixels == previous:
                    continue
                previous = pixels
                ref = f'EffectsApproved11/{form}/attack{hit["number"]}/F{index:03}'
                frames[ref] = {'src': png(canvas), 'pivot': [0, 0]}
                index += 1

    thrown_manifest = load(ROOT / 'resources/authoring/workbench/source/WeaponReference/확정표창폭탄_20261023/manifest.json')
    flight = {}
    with zipfile.ZipFile(thrown_package) as z:
        for form in ['shuriken', 'bomb']:
            raw = z.read(form+'.json')
            if hashlib.sha256(raw).hexdigest() != thrown_manifest['sourceHashes'][form+'.json']:
                raise ValueError('Approved thrown source hash differs: '+form)
            source = json.loads(raw)
            for i, (fid, uri) in enumerate(source['frames'].items()):
                corrected = form == 'shuriken' and fid in {'idle_01', 'idle_02', 'idle_03', 'idle_04', 'hurt_01', 'hurt_05', '01_H1_1', '15_idle'}
                if corrected:
                    clean = decode(uri)
                    for point in [(73, 92), (73, 93), (74, 94), (75, 94), (75, 97)]:
                        if clean.getpixel(point) == (27, 22, 22, 255):
                            clean.putpixel(point, (253, 238, 218, 255))
                    uri = png(clean)
                ref = f'CharacterApproved13/{form}/F{i:03}'
                record = next(s for s in thrown_manifest['sprites'] if s['path'].endswith('/'+ref+'.png'))
                # Corrected PNG compression bytes vary across Pillow versions;
                # their source JSON hash and explicit pixel correction are checked above.
                if not corrected and hashlib.sha256(base64.b64decode(uri.split(',')[1])).hexdigest() != record['sha256']:
                    raise ValueError('Thrown character pixels differ from approved manifest: '+ref)
                frames[ref] = {'src': uri, 'pivot': source['pivot']}
            flight[form] = source['flightSprites'] if form == 'shuriken' else [source['projectile']]

    def clip(form, name):
        s = sets[form]
        c = next(c for c in s['clips'] if c['name'] == name)
        fx = next((e for e in s.get('effects', []) if e['attack'] == name), None)
        for ref in c['frames'] + (fx['frames'] if fx else []):
            if ref not in frames:
                raise ValueError('Required approved frame missing: ' + ref)
        return {'frames': c['frames'], 'seconds': c['seconds'], 'impact': c.get('sourceImpact', 0),
                'fxStart': c.get('fxStart', -1), 'effect': fx}

    actions = []
    melee = load(RES / 'MeleeCatalog.json')
    registry = catalog['motionRegistry']['actions']
    names = {s['id']: s['name'] for s in melee['forms']}

    def action(action_id, name, weapon, segments, markers, kind, note=''):
        duration = max(s['start']+s['duration'] for s in segments)
        actions.append({'id': action_id, 'name': name, 'weapon': weapon, 'weaponName': names.get(weapon, weapon),
                        'duration': duration, 'segments': segments, 'markers': markers, 'kind': kind, 'note': note})

    for form in melee['forms']:
        f = form['id']
        if f not in sets:
            continue
        segments, markers, at = [], [], 0
        for i, h in enumerate(form['basics']):
            name = 'attack_single' if f == 'launcher' else f'attack{i+1}'
            c = clip(f, name)
            segment = {'start': at, 'duration': h['duration'], 'impact': h['impact'], 'clip': c}
            segments.append(segment)
            markers.append(at+h['impact'])
            single = dict(segment, start=0)
            action(f'{f}/basic/{i+1}', f'기본공격 {i+1}타', f, [single], [h['impact']], 'basic')
            at += h['duration']
        action(f'{f}/combo', '기본공격 전체 연계', f, segments, markers, 'combo', '후속 입력을 미리 예약한 최단 연계')
        for spec in form['specials']:
            if spec['end'] <= 0:
                continue
            binding = next((a for a in registry if a['actionId'] == spec['id'] and a.get('kind') == 1), None)
            motion = binding['motion'] if binding else f'{f}/attack1'
            mf, mc = motion.split('/')
            marks = sorted(set([h['time'] for h in spec['hits']])) or [spec['activation']]
            segment = {'start': 0, 'duration': spec['end'], 'impact': min(spec['activation'], spec['end']), 'clip': clip(mf, mc)}
            action(spec['id'], spec['name'], f, [segment], marks, 'special',
                   '현재 게임이 연결한 공용 본체 모션. 비행·착탄·지형 충돌과 전용 특수 이펙트는 재현하지 않음.')

    with wave.open(str(sound)) as w:
        if (w.getframerate(), w.getnchannels(), w.getsampwidth()) != (48000, 1, 2):
            raise ValueError('Approved source must be 48kHz mono PCM16.')
        pcm = w.readframes(w.getnframes())
    return {'schema': 1, 'frames': frames, 'actions': actions, 'flight': flight,
            'source': {'name': 'Minifantasy 검 3 · 검 확정 단발', 'rate': 48000,
                       'pcm': base64.b64encode(pcm).decode(), 'sha256': hashlib.sha256(sound.read_bytes()).hexdigest()},
            'artSource': '무기11종_확정_20261021', 'timingSource': 'MeleeCatalog + AnimationCatalog + HeavyWeaponCatalog'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--thrown-package', type=Path, required=True)
    parser.add_argument('--sound', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    payload = make_payload(args.package, args.thrown_package, args.sound)
    html = (HERE/'studio.html').read_text(encoding='utf-8')
    for key, value in {'__DATA__': json.dumps(payload, ensure_ascii=False),
                       '__DSP__': (HERE/'dsp.js').read_text(encoding='utf-8'),
                       '__APP__': (HERE/'app.js').read_text(encoding='utf-8')}.items():
        html = html.replace(key, value)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(html, encoding='utf-8')
    print(json.dumps({'output': str(args.output), 'actions': len(payload['actions']), 'frames': len(payload['frames']),
                      'bytes': args.output.stat().st_size}, ensure_ascii=False))


if __name__ == '__main__':
    main()
