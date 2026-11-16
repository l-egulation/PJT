#!/usr/bin/env python3
"""Export clock executioner timing; the runtime validates its meaning."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/combat/clock_executioner_timing.json"
TARGET = ROOT / "unity/Assets/ForestCombatV2/Resources/ClockExecutioner/Timing.json"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    text = json.dumps(json.loads(SOURCE.read_text(encoding="utf-8")), ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not TARGET.is_file() or TARGET.read_text(encoding="utf-8") != text:
            raise SystemExit("Stale clock boss timing: " + str(TARGET))
        print("Clock boss timing matches canonical source.")
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(text, encoding="utf-8")
        print("Exported clock boss timing.")


if __name__ == "__main__":
    main()
