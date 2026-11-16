"""Export the user-selected polish metadata to a fresh review directory."""
import hashlib
import json
from authoring_output import ROOT, destination

SELECTION = 'authoring/workbench/source/MonsterReference/approved-polish-v1/manifest.json'


def main():
    out = destination(__doc__)
    raw = (ROOT / SELECTION).read_bytes()
    data = json.loads(raw)
    for entry in data['files']:
        source = ROOT / entry['source']
        content = source.read_bytes()
        assert len(content) == entry['bytes']
        assert hashlib.sha256(content).hexdigest() == entry['sha256']
        target = out / entry['target']
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
    for key, name in [('bodyImport', 'import.json'), ('effectsImport', 'effects-import.json')]:
        value = data[key]
        value['polishManifest'] = SELECTION
        value['polishSHA256'] = hashlib.sha256(raw).hexdigest()
        target = out / 'unity/Assets/Deviant/Stage1Monsters/Data' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Exported {len(data["files"])} selected media files and both import descriptions.')


if __name__ == '__main__':
    main()
