"""Copy the lighting authoring source to Unity; C# owns semantic validation."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/presentation/lighting/lab.json'
TARGET = ROOT / 'unity/Assets/Deviant/Presentation/Resources/Lighting/Lab.json'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--chapter2', action='store_true', help='Export the limited Chapter2 prop trial instead of the lab')
    args = parser.parse_args()
    source = ROOT / 'data/presentation/lighting/chapter2-props.json' if args.chapter2 else SOURCE
    target = ROOT / 'unity/Assets/Chapter2/Resources/Chapter2Lighting/Settings.json' if args.chapter2 else TARGET
    payload = source.read_bytes()
    json.loads(payload)
    if args.check:
        if not target.is_file() or target.read_bytes() != payload:
            raise SystemExit('Lighting catalog differs: run python3 tools/lighting/export_lighting.py' + (' --chapter2' if args.chapter2 else ''))
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
    print('PASS: lighting source and Unity catalog match')


if __name__ == '__main__':
    main()
