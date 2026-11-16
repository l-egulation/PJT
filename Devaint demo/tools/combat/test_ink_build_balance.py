"""Check nominal hit targets and preserve per-species reward supply; no playtime claim."""
import json
import math
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(name):
    return json.loads((ROOT / "data/combat" / name).read_text())


class InkBuildBalanceTests(unittest.TestCase):
    def test_stage_one_sword_hits_match_unbuffed_and_focused_targets(self):
        damage = read("actions/base_sword_basic_0.json")["hit"]["damage"]
        ink = read("weapon_ink_run_design.json")["inks"]["attackGranting"]["buff"]["attackPowerIncrease"]["base"]
        village = read("stage1_village_monsters.json")["monsters"]
        new = read("chapter1_new_monsters.json")["monsters"]
        small = [m["maxHP"] for m in village if m["id"] != "04_well-warden"] + [new[0]["behavior"]["hp"]]
        for hp in small:
            self.assertEqual(math.ceil(hp / damage), 4)
            self.assertIn(math.ceil(hp / (damage * (1 + 3 * ink))), (1, 2))
        large = new[1]["behavior"]["hp"]
        self.assertEqual(math.ceil(large / damage), 6)
        self.assertEqual(math.ceil(large / (damage * (1 + 3 * ink))), 4)

    def test_stage_two_focused_sword_keeps_two_and_four_hit_targets(self):
        damage = read("actions/base_sword_basic_0.json")["hit"]["damage"]
        ink = read("weapon_ink_run_design.json")["inks"]["attackGranting"]["buff"]["attackPowerIncrease"]["base"]
        for species, monster in read("stage2_monster_stats.json")["monsters"].items():
            self.assertEqual(math.ceil(monster["maxHP"] / (damage * (1 + 4 * ink))), 4 if species == "B3" else 2)

    def test_current_species_keep_their_previous_gauge_contributions(self):
        rows = read("weapon_ink_run_design.json")["rewards"]["gauge"]["stageTiers"]
        def gain(stage, hp):
            tiers = rows[str(stage)]
            return tiers["small" if hp < tiers["small"]["maxHpBelow"] else "large" if hp >= tiers["large"]["maxHpFrom"] else "medium"]["gain"]
        expected = {"01_thorn-root": 5, "02_broom-stalker": 4, "03_gutter-spitter": 5}
        for monster in read("stage1_village_monsters.json")["monsters"]:
            if monster["id"] in expected:
                self.assertEqual(gain(1, monster["maxHP"]), expected[monster["id"]])
        for monster in read("chapter1_new_monsters.json")["monsters"]:
            self.assertEqual(gain(1, monster["behavior"]["hp"]), 8 if "charger" in monster["id"] else 5)
        for species, monster in read("stage2_monster_stats.json")["monsters"].items():
            self.assertEqual(gain(2, monster["maxHP"]), 12 if species == "B3" else 7.5)

    def test_elite_health_matches_user_decision_20261116(self):
        # 2026-11-16 user decision: elite HP is set directly, no longer 3x the large normal.
        village = read("stage1_village_monsters.json")["monsters"]
        self.assertEqual(next(m["maxHP"] for m in village if m["id"] == "04_well-warden"), 3000)
        self.assertEqual(read("chapter2_elite.json")["hp"], 4500)

if __name__ == "__main__":
    unittest.main()
