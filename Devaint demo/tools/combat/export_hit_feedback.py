#!/usr/bin/env python3
"""Export both feedback layers; C# validates their runtime meaning."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[2]
FILES = [
    ("data/presentation/hit_feedback.json", "unity/Assets/Deviant/Presentation/Resources/HitFeedback/HitFeedback.json"),
    ("data/combat/hit_feedback_time.json", "unity/Assets/Deviant/Presentation/Resources/HitFeedback/HitFeedbackTime.json"),
    ("data/combat/presentation/hit_feedback.json", "unity/Assets/Deviant/Combat/Resources/CombatFeelFeedback.json"),
    # Gameplay side of the hit-feel test: per-action enemy push and player basic step.
    ("data/combat/attack_feel_motion.json", "unity/Assets/Deviant/Combat/Resources/CombatFeelMotion.json"),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    for source, destination in FILES:
        content = json.dumps(json.loads((ROOT / source).read_text(encoding="utf-8")), ensure_ascii=False, indent=2) + "\n"
        target = ROOT / destination
        if args.check:
            if not target.is_file() or target.read_text(encoding="utf-8") != content:
                raise SystemExit(f"Stale generated feedback settings: {target}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")
    print("Both feedback layers match canonical sources." if args.check else "Exported both feedback layers.")


if __name__ == "__main__":
    main()
