"""Select the pinned compressed derivatives and generate main-owned Unity bindings."""
import hashlib
import json
from pathlib import Path
import re
import struct
import uuid

from update_assets import ROOT, backend


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def metadata(path):
    name = Path(str(path) + '.meta')
    if not name.exists():
        name.parent.mkdir(parents=True, exist_ok=True)
        if path.suffix == '.wav':
            template = (ROOT / 'unity/Assets/Deviant/Audio/SFX/UI/sfx_ui_confirm_01_v01.wav.meta').read_text()
            name.write_text(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, template))
        elif path.is_dir():
            name.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n')
        else:
            name.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n')


def main():
    settings = json.loads((ROOT / 'data/assets/media-compression.json').read_text())
    asset = settings['asset']
    if not re.fullmatch(r'authoring\.[a-z0-9-]+', asset):
        raise ValueError('Expected an explicit authoring compressed media asset ID')
    group = asset.replace('authoring.', 'authoring/', 1)
    store = backend(ROOT).Store(ROOT / 'resources')
    media = json.loads(store.read({'asset': asset, 'path': group + '/runtime/manifest.json'}))
    if settings['images'] != {'source': 'original'}:
        raise ValueError('Game images must use original resource bytes')
    if any(settings[key] != media['settings'][key] for key in ('schema', 'asset', 'audio')):
        raise ValueError('Compressed derivatives do not match the main media-compression settings')
    # This is a main-owned runtime binding, paired with the preserved generated WAV inputs.
    combat_catalog = ROOT / 'unity/Assets/Deviant/Audio/Resources/ApprovedCombatAudio/Catalog.json'
    combat_catalog.parent.mkdir(parents=True, exist_ok=True)
    combat_catalog.write_bytes(store.read({'asset': asset, 'path': group + '/source/generated-audio/Catalog.json'}))
    metadata(combat_catalog)
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text())
    by_target = {item['target']: item for item in manifest['files']}
    images, audio_sources = [], []
    for record in media['files']:
        target = record['target']
        if record['kind'] == 'image':
            previous = by_target.get(target)
            # The current deployment contract excludes archived frames that are no longer selected.
            if previous is None:
                continue
            # Restore known compressed derivatives, but preserve newer authored art selected by main.
            if previous['source']['asset'].startswith('authoring.compressed-game-media'):
                if previous['sha256'] != record['unitySha256'] and record['sha256'] not in previous.get('replaceHashes', []):
                    raise ValueError('Unrecognized image version: ' + target)
                replace_hashes = set(previous.get('replaceHashes', [])) | {record['sha256'], previous['sha256']}
                by_target[target] = {'target': target, 'bytes': record['bytes'], 'sha256': record['sha256'],
                                     'replaceHashes': sorted(replace_hashes), 'source': record['source']}
        else:
            if target not in by_target:
                by_target[target] = {k: record[k] for k in ('target', 'bytes', 'sha256', 'source')}
                metadata(ROOT / target)
            audio_sources.append(target[len('unity/'):])
    original_opacity = {record['sha256']: record['opaque'] for record in media['files'] if record['kind'] == 'image'}
    # Include newly authored PNGs from the current contract without reverting their selected versions.
    for item in by_target.values():
        target = item['target']
        if not (target.startswith('unity/Assets/') and target.endswith(('.png', '.bytes'))):
            continue
        if target.endswith('.bytes') and '/CompressedAudio/' in target:
            continue
        data = store.read(item['source'])
        if not data.startswith(b'\x89PNG\r\n\x1a\n'):
            continue
        if hashlib.sha256(data).hexdigest() != item['sha256']:
            raise ValueError('Original image source does not match contract: ' + target)
        width, height = struct.unpack('>II', data[16:24])
        # Without decoding pixels, only RGB/grayscale PNGs can be proven opaque; others safely use alpha-capable GPU formats.
        opaque = original_opacity.get(item['sha256'], data[25] in (0, 2) and b'tRNS' not in data)
        images.append({'path': target[len('unity/'):], 'width': width, 'height': height, 'opaque': opaque})
    # Both GUID-bound catalogs keep the authoring clips in the Editor; build scopes clear those references only while packaging.
    ids_by_guid = {}
    for path, prefix in [('unity/Assets/Deviant/Audio/Resources/SelectedAudioClips.asset', ''),
                         ('unity/Assets/ForestCombatV2/Resources/PresentationSfx.asset', 'presentation:')]:
        text = (ROOT / path).read_text()
        for name, guid in re.findall(r'^  - id: ([^\n]+)\n    clip: \{fileID: 8300000, guid: ([0-9a-f]+)', text, re.M):
            ids_by_guid.setdefault(guid, []).append(prefix + name)
    entries = []
    bound_ids = set()
    for record in media['files']:
        if record['kind'] != 'audio':
            continue
        target = record['target']
        guid = re.search(r'guid: ([0-9a-f]+)', (ROOT / (target + '.meta')).read_text()).group(1)
        ids = ids_by_guid.get(guid, [])
        resource = target.split('/Resources/', 1)[1].rsplit('.', 1)[0] if '/Resources/' in target else ''
        if not ids and not resource:
            continue
        payload_target = 'unity/Assets/Deviant/Media/Resources/CompressedAudio/Payloads/' + record['opusSha256'] + '.bytes'
        by_target[payload_target] = {'target': payload_target, 'bytes': record['opusBytes'], 'sha256': record['opusSha256'],
                                     'source': {'asset': asset, 'path': group + '/' + record['opus']}}
        metadata(ROOT / payload_target)
        for id_value in ids or ['']:
            bound_ids.add(id_value)
            entries.append({'id': id_value, 'resource': resource,
                            'payload': 'CompressedAudio/Payloads/' + record['opusSha256'], 'name': Path(target).stem})
    expected_ids = {value for values in ids_by_guid.values() for value in values}
    if expected_ids - bound_ids:
        raise ValueError('Unbound audio catalog IDs: ' + str(sorted(expected_ids - bound_ids)))
    catalog = ROOT / 'unity/Assets/Deviant/Media/Resources/CompressedAudio/Catalog.json'
    write_json(catalog, {'schema': 1, 'entries': entries})
    metadata(catalog)
    selection = ROOT / 'unity/Assets/Deviant/Media/Editor/CompressionSelection.json'
    write_json(selection, {'schema': 1, 'images': images, 'audioSources': audio_sources})
    metadata(selection)
    for item in by_target.values():
        path = ROOT / item['target']
        if not item['target'].startswith('unity/Assets/'):
            continue
        parent = path.parent
        while parent != ROOT / 'unity/Assets':
            if not parent.exists(): parent.mkdir(parents=True)
            metadata(parent); parent = parent.parent
    # Keep the existing contract order and append the new payload/source selections.
    original_targets = [item['target'] for item in manifest['files']]
    manifest['files'] = [by_target.pop(name) for name in original_targets] + [by_target[name] for name in sorted(by_target)]
    write_json(manifest_path, manifest)
    print(json.dumps({'images': len(images), 'audioSources': len(audio_sources), 'audioBindings': len(entries), 'selectedIds': len(expected_ids)}))


if __name__ == '__main__':
    main()
