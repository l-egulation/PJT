"""Copy the maintenance dialogue source into Unity Resources; --check is read-only."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/story/maintenance_introductions.json"
TARGET = ROOT / "unity/Assets/Deviant/Game/Resources/MaintenanceShops/MaintenanceDialogue.json"
FONT_META = ROOT / "unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/Galmuri11-Bold.ttf.meta"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = SOURCE.read_text(encoding="utf-8")
    data = json.loads(content)
    if (data["schema"] != 1 or len(data["first"]) != 10 or len(data.get("firstAfterVillage", [])) != 8
            or len(data["second"]) != 3):
        raise SystemExit("Invalid maintenance dialogue schema")
    font_meta = FONT_META.read_text(encoding="utf-8")
    match = re.search(r'  customCharacters: ("[^\n]*")', font_meta)
    if not match:
        raise SystemExit("Cannot find custom font character set")
    old_chars = json.loads(match[1])
    required = set("".join(data["first"] + data["firstAfterVillage"] + data["second"]) + "마을 장인수로 정비공")
    missing = required - set(old_chars)
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding="utf-8") != content:
            raise SystemExit("Maintenance dialogue is stale; run this script without --check")
        if missing:
            raise SystemExit("Maintenance font is missing: " + "".join(sorted(missing)))
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(content, encoding="utf-8")
        if missing:
            new_chars = "".join(sorted(set(old_chars) | required))
            font_meta = font_meta[:match.start(1)] + json.dumps(new_chars, ensure_ascii=True) + font_meta[match.end(1):]
            FONT_META.write_text(font_meta, encoding="utf-8")
            print("Added maintenance glyphs: " + "".join(sorted(missing)))
    print("Maintenance dialogue synchronized")


if __name__ == "__main__":
    main()
