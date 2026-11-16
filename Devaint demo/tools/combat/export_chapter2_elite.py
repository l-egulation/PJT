"""Export the dedicated elite tuning without touching chapter layouts."""
import json
import math
import argparse
from pathlib import Path
root = Path(__file__).resolve().parents[2]
source = root / "data/combat/chapter2_elite.json"
data = json.loads(source.read_text(encoding="utf-8"))
assert data["schema"] == 1 and math.isfinite(data["hp"]) and data["hp"] > 0
assert len(data["comboTimes"]) == len(data["comboDamage"]) == 3
assert data["activeSeconds"] > 0 and data["comboStepSeconds"] > 0
assert data["fragmentColliderSize"] > 0 and data["knockSeconds"] > 0
out = root / "unity/Assets/Chapter2/Resources/Chapter2Elite.json"
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check',action='store_true')
args=parser.parse_args()
expected=json.dumps(data, indent=2) + "\n"
if args.check:
    if not out.exists() or out.read_text(encoding='utf-8')!=expected:
        raise SystemExit('Chapter2Elite.json is out of date; run export_chapter2_elite.py')
else:
    out.write_text(expected, encoding="utf-8")
print(out)
