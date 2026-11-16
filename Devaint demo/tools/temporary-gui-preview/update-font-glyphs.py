"""Extend the existing static Galmuri glyph set; preserve font files and GUIDs."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
fonts = root / "unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts"
glyph_file = fonts / "Glyphs.txt"
chars = set(glyph_file.read_text(encoding="utf-8-sig").strip("\r\n"))
for directory in ("Deviant/Game/Runtime", "ForestCombatV2/Runtime", "Deviant/Run/Runtime", "Deviant/Stage1Monsters/Game", "Deviant/Inventory/Runtime", "Deviant/Inventory/Unity", "Deviant/Integration/Unity"):
    for source in (root / "unity/Assets" / directory).rglob("*.cs"):
        chars.update(re.findall("[가-힣ㄱ-ㅎㅏ-ㅣ]", source.read_text(encoding="utf-8-sig")))
for content in (root / "unity/Assets/Deviant/Stage1Monsters/Data/import.json", root / "data/inventory/temporary-inks.json", root / "data/story/maintenance_introductions.json", root / "unity/Assets/Deviant/Combat/Resources/WeaponGrowthMvp.json", root / "unity/Assets/Deviant/Hub/Resources/HubStory.json", root / "unity/Assets/Deviant/Hub/Resources/HubOpening.json", root / "unity/Assets/Deviant/Hub/Resources/HubSkyParkour.json", root / "unity/Assets/Deviant/Hub/Resources/RogueliteProgression.json", root / "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json"):
    # Inventory labels, growth part names and descriptions, and the bag's skill names and lines are drawn with the same static font.
    chars.update(re.findall("[가-힣ㄱ-ㅎㅏ-ㅣ]", content.read_text(encoding="utf-8-sig")))
glyphs = "".join(sorted(chars))
glyph_file.write_text(glyphs, encoding="utf-8")
for name in ("Galmuri11.ttf.meta", "Galmuri11-Bold.ttf.meta"):
    path = fonts / name
    source = path.read_text(encoding="utf-8-sig")
    updated, count = re.subn(r"^  customCharacters:.*$", lambda _: "  customCharacters: " + json.dumps(glyphs, ensure_ascii=True), source, flags=re.M)
    if count != 1:
        raise RuntimeError(f"Expected one customCharacters entry: {path}")
    path.write_text(updated, encoding="utf-8")
print(f"Galmuri static glyphs: {len(glyphs)}")
