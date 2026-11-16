"""Export picker metadata and the explicitly implemented shared combat experiment."""
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
source = json.loads((ROOT / "data/combat/ink_parts_proposal.json").read_text(encoding="utf-8-sig"))
keys = ("id", "name", "color", "description", "limit", "cooldown", "duplicate", "unityImplementation", "requiresStamp", "unityNote")
output = ROOT / "unity/Assets/ForestCombatV2/Resources/TrainingInkParts.json"
implemented = source["unityExperiment"]["implementedParts"]
assert set(implemented) == {p["id"] for p in source["parts"] if p["unityImplementation"] == "experimental_shared_combat_v1"}
runtime = []
ops = {"dot", "damage", "area", "blood", "range", "low_health", "buff", "next_recovery", "refund", "movement_wave", "charge_refund", "charge_move", "recovery", "windup", "distance_windup", "shield", "counter_heal", "guard", "seed", "charge_shield", "heal", "leech", "overheal", "shield_damage", "slow", "slow_area", "root", "frost", "conditional_damage", "death_area", "detonate", "mark", "spread", "timed_detonate", "distance_frost", "distance_mark"}
for key in ("controlRecoverySeconds", "maxControlSeconds", "markDuration", "markCap", "shieldCap", "maxSlow", "maxDamageReduction", "chargeStoreCap"):
    assert source["unityExperiment"]["tuning"][key] == source["rules"][key], f"Conflicting trial rule: {key}"
assert len({p["form"] for p in source["unityExperiment"]["chargeProfiles"]}) == len(source["unityExperiment"]["chargeProfiles"])
assert all(math.isfinite(v) and v > 0 for v in source["unityExperiment"]["tuning"].values())
for p in source["parts"]:
    if p["id"] not in implemented:
        continue
    assert p["trigger"] in ("hit", "start", "cross", "kill", "move", "damage", "dodge")
    assert p["id"] != "g_armor", "Armor part is explicitly deferred"
    assert all(e["op"] in ops for e in p["effects"])
    assert all(math.isfinite(v) and v >= 0 for e in p["effects"] for v in e.values() if isinstance(v, (int, float)))
    runtime.append({**p, "oncePerAction": p["id"] in source["rules"]["oncePerActionParts"]})
documents = {
    output: {"parts": [{k: p[k] for k in keys if k in p} for p in source["parts"]]},
    ROOT / "unity/Assets/Deviant/Combat/Resources/RuntimeInkParts.json": {"status": "experimental_not_product_approval", "parts": runtime, "tuning": source["unityExperiment"]["tuning"], "chargeProfiles": source["unityExperiment"]["chargeProfiles"]},
}
for path, document in documents.items():
    content = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    if "--check" in sys.argv:
        if not path.exists() or path.read_text(encoding="utf-8") != content:
            raise SystemExit(f"Ink catalogue is stale: {path}; run export_training_parts.py")
    else:
        path.write_text(content, encoding="utf-8")
print(f"PASS: {len(source['parts'])} picker entries, {len(runtime)} shared combat experimental parts")
