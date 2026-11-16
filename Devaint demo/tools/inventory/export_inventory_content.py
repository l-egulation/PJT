"""Export temporary inventory data to its editable Unity content asset (no image payloads)."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/inventory/temporary-inks.json"
DESTINATION = ROOT / "unity/Assets/Deviant/Inventory/Resources/TemporaryInventoryContent.asset"
SCRIPT = ROOT / "unity/Assets/Deviant/Inventory/Unity/InventoryContent.cs.meta"


def render():
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    ids = [item["id"] for item in data["items"]]
    assert len(ids) == len(set(ids)) and data["capacity"] == 5 and "perKindCapacity" not in data
    guid = next(line.split(": ", 1)[1] for line in SCRIPT.read_text().splitlines() if line.startswith("guid: "))
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}", "  m_Name: TemporaryInventoryContent",
             "  m_EditorClassIdentifier: Deviant.Inventory.Unity::Deviant.Inventory.Unity.InventoryContent",
             f"  capacity: {data['capacity']}", "  items:"]
    quote = lambda text: json.dumps(text, ensure_ascii=False)
    for item in data["items"]:
        assert item["kind"] in (0, 1, 2)
        lines += [f"  - id: {quote(item['id'])}", f"    displayName: {quote(item['name'])}",
                  f"    description: {quote(item['description'])}", f"    formId: {quote(item.get('formId', ''))}",
                  f"    weaponCategory: {quote(item.get('category', ''))}", f"    kind: {item['kind']}",
                  f"    value: {item.get('value', 0)}", "    icon: {fileID: 0}",
                  "    tint: {" + ", ".join(f"{key}: {value}" for key, value in zip("rgba", item["tint"])) + "}"]
    lines += ["  tables:"]
    for table in data["tables"]:
        assert table["deathSeconds"] >= 0 and sum(e["amount"] for e in table["entries"]) <= 32
        lines += [f"  - key: {table['key']}", f"    deathSeconds: {table['deathSeconds']}", "    entries:"]
        for entry in table["entries"]:
            assert entry["itemId"] in ids and 0 < entry["amount"] <= 32
            lines += [f"    - itemId: {quote(entry['itemId'])}", f"      amount: {entry['amount']}"]
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = render()
    if args.check:
        assert DESTINATION.read_text(encoding="utf-8") == content, "Regenerate inventory content."
        print("PASS: inventory source/content asset match")
    else:
        DESTINATION.parent.mkdir(parents=True, exist_ok=True)
        DESTINATION.write_text(content, encoding="utf-8")
        print(DESTINATION.relative_to(ROOT))
