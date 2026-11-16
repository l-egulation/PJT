"""Register the approved native-size book frames and main-owned Unity import settings."""
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    source = ROOT / 'resources/ui/workshop-frames-20261109'
    frames = json.loads((source / 'runtime/frames.json').read_text(encoding='utf-8'))['frames']
    template = (ROOT / 'unity/Assets/Deviant/TemporaryGUI/Resources/JournalGUI/primary.png.meta').read_text(encoding='utf-8')
    for frame in frames:
        raw = (source / frame['file']).read_bytes()
        target = 'unity/Assets/Deviant/TemporaryGUI/Resources/JournalGUI/WorkshopFrames/' + Path(frame['file']).name
        meta = ROOT / (target + '.meta')
        meta.parent.mkdir(parents=True, exist_ok=True)
        if not meta.exists():
            lines = template.splitlines()
            lines[1] = 'guid: ' + uuid.uuid4().hex
            text = '\n'.join(lines) + '\n'
            text = text.replace('enableMipMap: 0', 'enableMipMap: 1').replace('maxTextureSize: 2048', 'maxTextureSize: 4096')
            text = text.replace('filterMode: 1', 'filterMode: 2').replace('textureCompression: 1', 'textureCompression: 0')
            meta.write_text(text, encoding='utf-8')
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
               'source': {'asset': 'ui.workshop-frames-20261109', 'path': 'ui/workshop-frames-20261109/' + frame['file']}}
        manifest['files'] = [item for item in manifest['files'] if item['target'] != target]
        manifest['files'].append(row)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    register()
