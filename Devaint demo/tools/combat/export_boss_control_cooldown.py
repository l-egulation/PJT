"""Publish the boss stun/root internal cooldowns from their editing source."""
import argparse
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / "data/combat/boss_status_design.json"
target = root / "unity/Assets/Deviant/Combat/Resources/BossControlCooldown.json"
data = json.loads(source.read_text(encoding="utf-8"))
assert data["schema"] == 1
cooldown = data["controlInternalCooldown"]
assert cooldown["startsWhen"] == "applied" and cooldown["duringCooldown"] == "application_fails"
assert cooldown["independentPerControl"] is True
for key in ("stunSeconds", "rootSeconds"):
    assert isinstance(cooldown[key], (int, float)) and math.isfinite(cooldown[key]) and cooldown[key] >= 0, key
content = json.dumps({"schema": 1, "stunSeconds": cooldown["stunSeconds"], "rootSeconds": cooldown["rootSeconds"]}, indent=2) + "\n"
parser = argparse.ArgumentParser()
parser.add_argument("--check", action="store_true")
if parser.parse_args().check:
    assert target.read_text(encoding="utf-8") == content, "Run export_boss_control_cooldown.py"
else:
    target.write_text(content, encoding="utf-8")
print("Boss control cooldown: source matches runtime")
