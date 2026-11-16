#!/usr/bin/env python3
"""Copy the loot drop toss numbers to Unity Resources; C# LootTossSettings validates their meaning."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/presentation/loot_drop_toss.json"
TARGET = ROOT / "unity/Assets/Deviant/Inventory/Resources/LootDropToss.json"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = json.dumps(json.loads(SOURCE.read_text(encoding="utf-8")), ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not TARGET.is_file() or TARGET.read_text(encoding="utf-8") != content:
            raise SystemExit(f"Stale loot drop toss settings: {TARGET}")
        print("Loot drop toss settings match the canonical source.")
        return
    TARGET.write_text(content, encoding="utf-8")
    print("Exported loot drop toss settings.")


if __name__ == "__main__":
    main()
