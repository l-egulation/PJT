"""Publish combat damage bounds from their editing source."""
import argparse
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / "data/combat/damage_variance.json"
target = root / "unity/Assets/Deviant/Combat/Resources/CombatDamageVariance.json"
data = json.loads(source.read_text(encoding="utf-8"))
assert data["schema"] == 1
assert isinstance(data["varyPlayerReceivedDamage"], bool)
assert all(math.isfinite(data[key]) for key in ("minimum", "maximum"))
assert 0 < data["minimum"] <= 1 <= data["maximum"]
content = json.dumps(data, indent=2) + "\n"
parser = argparse.ArgumentParser()
parser.add_argument("--check", action="store_true")
if parser.parse_args().check:
    assert target.read_text(encoding="utf-8") == content, "Run export_damage_variance.py"
else:
    target.write_text(content, encoding="utf-8")
print("Combat damage variance: source matches runtime")
