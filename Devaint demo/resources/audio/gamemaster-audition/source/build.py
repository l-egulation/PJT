"""Build an offline audition for all basic attacks, with current runtime audio references."""
import argparse
import base64
import hashlib
import io
import json
import math
from pathlib import Path
import re
import struct
import wave
import zipfile

HERE = Path(__file__).resolve().parent
ASSET = HERE.parent
RESOURCES = ASSET.parents[1]
MAIN = RESOURCES.parent


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def audio_payload(raw, name):
    with wave.open(io.BytesIO(raw)) as wav:
        rate, channels, width, count = wav.getframerate(), wav.getnchannels(), wav.getsampwidth(), wav.getnframes()
        assert width == 2, 'Expected PCM16'
        samples = struct.unpack('<' + 'h' * count * channels, wav.readframes(count))
    envelope = [max(abs(samples[k * channels + c]) for c in range(channels)) / 32768 for k in range(count)]
    window = max(1, int(rate * .008))
    energies = [sum(v*v for v in envelope[n:n+window]) / len(envelope[n:n+window]) for n in range(0, count, window)]
    peak_time = (max(range(len(energies)), key=energies.__getitem__) + .5) * window / rate
    peak = max(envelope)
    active = [v for v in envelope if v > peak * .03]
    rms = math.sqrt(sum(v*v for v in active) / len(active))
    gain = min(10 ** (-22/20) / rms, .8 / peak)
    waveform = [round(max(envelope[n:min(count, n+max(1, count//140))]), 4) for n in range(0, count, max(1, count//140))]
    return {'file': name, 'sha256': sha(raw), 'duration': count/rate,
            'peakTime': min(peak_time, count/rate), 'comparisonGain': gain, 'waveform': waveform,
            'src': 'data:audio/wav;base64,' + base64.b64encode(raw).decode()}


def without_media(value):
    if isinstance(value, dict):
        return {k: without_media(v) for k, v in value.items() if k not in ['src', 'waveform']}
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', required=True, type=Path)
    args = parser.parse_args()
    unity = MAIN / 'unity/Assets/Deviant/Combat/Resources'
    animation = read_json(unity / 'CharacterV2/AnimationCatalog.json')
    sets = {s['form']: s for s in animation['sets']}
    sets.update({s['form']: s for s in read_json(unity / 'CharacterV2/HeavyWeaponCatalog.json')['sets']})
    forms = read_json(unity / 'MeleeCatalog.json')['forms']
    configs = read_json(HERE / 'weapons.json')
    choices = read_json(HERE / 'selections.json')
    revision_path = HERE / 'revision-02/recipes.json'
    revision = read_json(revision_path) if revision_path.exists() else None
    deployment = {x['target']: x for x in read_json(MAIN / 'docs/assets/manifests/unity-deployment.json')['files']}
    trials = {t['form']: t for t in read_json(MAIN / 'data/combat/playtest_environment.json')['basicSpeedTrials']}
    assert {f['id'] for f in forms} == set(configs), 'Every basic weapon needs a candidate set'
    assert read_json(MAIN / 'data/combat/presentation/base_sword_attack1.json')['mode'] == 'CombatTimeFixed'
    for binding in animation['meleeFrames']['actions']:
        if re.search(r'_basic_\d+$', binding['actionId']):
            assert binding.get('mode', 0) == 0, 'FrameTime needs direct graph projection'
    frames, audio, weapons = {}, {}, []
    from PIL import Image

    def add_frame(ref):
        if ref in frames:
            return
        path = unity / (ref + '.png')
        raw = path.read_bytes()
        meta = path.with_suffix('.png.meta').read_text(encoding='utf-8-sig')
        match = re.search(r'spritePivot: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}', meta)
        assert match, ref
        with Image.open(io.BytesIO(raw)) as im:
            w, h = im.size
            bounds = im.convert('RGBA').getbbox() or (0, 0, w, h)
        ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', meta)[1])
        frames[ref] = {'src': 'data:image/png;base64,' + base64.b64encode(raw).decode(),
                       'pivot': [float(match[1]) * w, (1-float(match[2])) * h],
                       'bounds': bounds, 'ppu': ppu, 'width': w, 'height': h,
                       'sha256': sha(raw), 'source': str(path.relative_to(MAIN)).replace('\\', '/')}
    (HERE / 'audio').mkdir(exist_ok=True)
    with zipfile.ZipFile(args.archive) as archive:
        for config in configs.values():
            assert len(config['candidates']) == 10 and len(set(config['candidates'])) == 10
            for entry in config['candidates']:
                if entry in audio:
                    continue
                name = Path(entry).name
                raw = archive.read(entry)
                (HERE / 'audio' / name).write_bytes(raw)
                audio[entry] = {**audio_payload(raw, name), 'name': name.removesuffix('.wav').removeprefix('whoosh_'), 'archiveEntry': entry}
    if revision:
        for key, value in revision['audio'].items():
            raw = (HERE / key).read_bytes()
            assert sha(raw) == value['sha256'], 'Edited candidate hash mismatch: '+key
            audio[key] = value
    # The selected asset overrides ApprovedCombatAudio's older rendered WAVs.
    audio_root = MAIN / 'unity/Assets/Deviant/Audio'
    selected_asset = (audio_root / 'Resources/SelectedAudioClips.asset').read_text(encoding='utf-8-sig')
    runtime_entries = {e['id']: e for e in read_json(audio_root / 'Resources/ApprovedCombatAudio/Catalog.json')['entries']}
    for entry in read_json(audio_root / 'Resources/WeaponSounds/Timing.json')['entries']:
        runtime_entries.setdefault(entry['id'], entry)
    registry = animation['motionRegistry']
    for form in forms:
        weapon = form['id']
        config = configs[weapon]
        art = sets[weapon]
        clips = {'idle': next(c for c in art['clips'] if c['name'] == 'idle')}
        effects, variants = {}, {}
        for i, basic in enumerate(form['basics']):
            action_id = f'{weapon}_basic_{i}'
            motion = next(a['motion'] for a in registry['actions'] if a['actionId'] == action_id and a['motion'].startswith(weapon+'/'))
            name = motion.split('/')[1]
            key = f'attack{i+1}'
            clip = dict(next(c for c in art['clips'] if c['name'] == name))
            document = next((m for m in registry['motions'] if m['id'] == motion), None)
            if document and document.get('steps'):
                assert document['mode'] == 'CombatTimeFixed', motion
                assert clip['frames'] == [s['frame'] for s in document['steps']], 'Stale catalog frames: '+motion
                assert clip['seconds'] == [s['seconds'] for s in document['steps']], 'Stale catalog seconds: '+motion
            assert 0 <= basic['impact'] <= basic['duration']
            clips[key] = clip
            effect = next((e for e in art.get('effects', []) if e['attack'] == name), None)
            if effect and clip.get('fxStart', -1) >= 0 and weapon != 'shield':
                effects[key] = effect
                for ref in effect['frames']:
                    add_frame(ref)
            filename = f'sfx_weapon_{action_id}_01_v01.wav'
            path = audio_root / 'SFX/Weapons' / filename
            meta = path.with_suffix('.wav.meta').read_text(encoding='utf-8-sig')
            guid = re.search(r'^guid: (\w+)', meta, re.M)[1]
            binding = re.search(r'- id: sfx_weapon_' + action_id + r'\s+clip: \{[^}]*guid: (\w+)', selected_asset)
            assert binding and binding[1] == guid, f'Current runtime binding changed: {action_id}'
            raw = path.read_bytes()
            deployed = deployment[path.relative_to(MAIN).as_posix()]
            original = RESOURCES / deployed['source']['path']
            assert original.read_bytes() == raw, f'Unity/source mismatch: {filename}'
            assert sha(raw) == deployed['sha256'], f'Unity/deployment mismatch: {filename}'
            (HERE / 'audio' / filename).write_bytes(raw)
            entry = runtime_entries[action_id]
            audio[action_id] = {**audio_payload(raw, filename), 'actionId': action_id,
                'source': path.relative_to(MAIN).as_posix(), 'guid': guid,
                'nominalDuration': entry['nominalDuration'], 'runtimeGain': .8}
            variants[key] = action_id
        for clip in clips.values():
            for ref in clip['frames']:
                add_frame(ref)
        trial = trials.get(weapon, {})
        assert trial.get('lastHitExtraRecoverySeconds', 0) == 0, 'Additional recovery requires explicit preview handling'
        weapons.append({'id': weapon, 'label': config['label'], 'eventLabel': config['eventLabel'],
                        'hint': config['hint'], 'clips': clips, 'effects': effects, 'basics': form['basics'],
                        'candidates': config['candidates'], 'currentVariants': variants,
                        'candidateRevision': revision.get('candidateRevisions',{}).get(weapon,2 if weapon in revision['pending'] else 1) if revision else 1,
                        'reviewCandidates': revision['candidates'].get(weapon) if revision else None,
                        'defaultSpeed': trial.get('scale', 1), 'choice': choices.get(weapon)})
    payload = {'schema': 2, 'defaultWeapon': 'hammer' if revision else 'whip', 'weapons': weapons, 'frames': frames, 'audio': audio}
    html = (HERE / 'template.html').read_text(encoding='utf-8')
    html = html.replace('__PAYLOAD__', json.dumps(payload, ensure_ascii=False).replace('</', '<\\/'))
    html = html.replace('__APP__', (HERE / 'app.js').read_text(encoding='utf-8'))
    preview = ASSET / 'preview/index.html'
    preview.parent.mkdir(exist_ok=True)
    preview.write_text(html, encoding='utf-8')
    if revision:
        focus = dict(payload, weapons=[w for w in weapons if w['id'] in revision['pending']])
        focused_html = (HERE / 'template.html').read_text(encoding='utf-8')
        focused_html = focused_html.replace('모든 무기의 기본공격을 한곳에서.', '망치 2타, 휘두름과 충격을 함께.')
        focused_html = focused_html.replace('무기별 후보를 모션과 비교하고, 마음에 드는 소리를 선택하세요.', '충격음을 앞당겨 2타 휘두름과 겹쳤습니다. 발사기는 현재 적용음으로 확정했습니다.')
        focused_html = focused_html.replace('__PAYLOAD__', json.dumps(focus, ensure_ascii=False).replace('</', '<\\/'))
        focused_html = focused_html.replace('__APP__', (HERE / 'app.js').read_text(encoding='utf-8'))
        (ASSET / 'preview/revision-02.html').write_text(focused_html, encoding='utf-8')
    provenance = {'status': 'audition-only; not approved or integrated', 'archive': args.archive.name,
                  'frames': {k: {x: v[x] for x in ['source', 'sha256']} for k, v in frames.items()},
                  'weapons': weapons, 'audio': {k: without_media(s) for k, s in audio.items()},
                  'timing': {name: sha((unity / name).read_bytes()) for name in ['CharacterV2/AnimationCatalog.json', 'MeleeCatalog.json']}}
    write_json(HERE / 'provenance.json', provenance)
    files = sorted(p.relative_to(ASSET).as_posix() for p in ASSET.rglob('*') if p.is_file() and p.name != 'asset.json' and '__pycache__' not in p.parts)
    asset_id, relative = 'audio.gamemaster-audition', ASSET.relative_to(RESOURCES).as_posix()
    write_json(ASSET / 'asset.json', {'format': 'deviant-resource-asset-v1', 'id': asset_id,
               'path': relative, 'status': 'candidate-review', 'gameIntegration': 'none',
               'source': 'User-purchased Gamemaster Audio archive; local audition only', 'files': files})
    catalog = read_json(RESOURCES / 'catalog.json')
    catalog['assets'] = [a for a in catalog['assets'] if a['id'] != asset_id]
    catalog['assets'].append({'id': asset_id, 'path': relative, 'fileCount': len(files)})
    catalog['assets'].sort(key=lambda a: a['id'])
    write_json(RESOURCES / 'catalog.json', catalog)
    generated = read_json(RESOURCES / 'generated-assets.json')
    generated['files'] = [f for f in generated['files'] if not f['path'].startswith(relative + '/')]
    for name in files:
        raw = (ASSET / name).read_bytes()
        generated['files'].append({'path': relative+'/'+name, 'bytes': len(raw), 'sha256': sha(raw)})
    write_json(RESOURCES / 'generated-assets.json', generated)
    lfs = read_json(RESOURCES / '.lfs-payloads.json')
    payload_files = [relative+'/preview/index.html']
    if revision:
        payload_files += [relative+'/preview/revision-02.html',relative+'/source/revision-02/recipes.json']
    write_json(RESOURCES / '.lfs-payloads.json', sorted(set(lfs + payload_files)))
    print(json.dumps({'output': str(preview), 'frames': len(frames), 'weapons': len(weapons),
                      'basicHits': sum(len(w['basics']) for w in weapons), 'audioFiles': len(audio), 'bytes': preview.stat().st_size}))


if __name__ == '__main__':
    main()
