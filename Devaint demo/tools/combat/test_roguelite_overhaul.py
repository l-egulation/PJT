"""Behavioral validation of the shared roguelite unlock catalog."""
import json
import unittest

import export_roguelite_progression as exporter


class RogueliteOverhaulTests(unittest.TestCase):
    def source(self):
        return json.loads(exporter.SOURCE.read_text(encoding='utf-8'))

    def with_unlock(self, effect):
        data = self.source()
        data.pop('targetRevision', None)
        # A root of the ink research tab, so dropping the live ink bundles still leaves that tab a root.
        data['unlocks'].append(dict(id='test_unlock', name='검사', building='supply_office', branch='ink_research', node='검사',
                                   cost=100, costStatus='trial-3', requiredClear='first_run_settled',
                                   prerequisites=[], effect=effect, maxPurchases=1))
        return data

    def test_shared_reroll_effect_is_accepted(self):
        exporter.validate(self.with_unlock(dict(kind='shared_reroll_count', value=3)))

    def without_ink_bundles(self, effect):
        data = self.with_unlock(effect)
        removed = {u['id'] for u in data['unlocks'] if u['effect']['kind'] == 'ink_bundle_unlock' and u['id'] != 'test_unlock'}
        # Also drop what depends on the removed bundles (the departure ink reward follows the first one).
        data['unlocks'] = [u for u in data['unlocks'] if u['id'] not in removed and not set(u['prerequisites']) & removed]
        return data

    def test_ink_bundle_validates_every_member_against_the_run_inks(self):
        data = self.without_ink_bundles(dict(kind='ink_bundle_unlock', parts=['yellow_dash_use_movement_speed_increase', 'green_dash_use_shield']))
        exporter.validate(data)
        data['unlocks'][-1]['effect']['parts'].append('ink3_red_02')  # an old colour part is not a run ink
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_basic_attack_and_skill_inks_stay_open_by_default(self):
        data = self.without_ink_bundles(dict(kind='ink_bundle_unlock', parts=['green_dash_use_shield', 'green_basic_attack_shield']))
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_an_ink_belongs_to_one_bundle_at_most(self):
        data = self.with_unlock(dict(kind='ink_bundle_unlock', parts=['green_dash_use_shield', 'green_dash_use_heal']))
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_duplicate_bundle_member_is_rejected(self):
        data = self.without_ink_bundles(dict(kind='ink_bundle_unlock', parts=['green_dash_use_shield', 'green_dash_use_shield']))
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_invalid_gold_bonus_and_revive_fraction_are_rejected(self):
        for effect in [dict(kind='kill_gold_bonus_percent', value=-1),
                       dict(kind='revive_count', value=1, healthPercent=0),
                       dict(kind='revive_count', value=1, healthPercent=101)]:
            with self.subTest(effect=effect), self.assertRaises(AssertionError):
                exporter.validate(self.with_unlock(effect))


    def test_every_unlock_names_a_listed_branch(self):
        data = self.source()
        data['unlocks'][0]['branch'] = 'nowhere'
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_a_tier_line_stays_in_one_branch(self):
        data = self.source()
        next(u for u in data['unlocks'] if u['id'] == 'attack_bonus_2')['branch'] = 'mobility'
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_every_branch_has_a_root(self):
        data = self.source()
        data['branches'].append(dict(id='empty', name='빈 줄기'))
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_tiers_form_nodes_of_four(self):
        data = self.source()
        next(u for u in data['unlocks'] if u['id'] == 'attack_bonus_5')['node'] = '공격력 I'
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_a_single_unlock_has_no_numeral(self):
        data = self.source()
        next(u for u in data['unlocks'] if u['id'] == 'revive_1')['node'] = '부활 I'
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_a_link_needs_the_whole_prerequisite_node(self):
        data = self.source()
        next(u for u in data['unlocks'] if u['id'] == 'recovery_uses_1')['prerequisites'] = ['max_health_bonus_2']
        with self.assertRaises(AssertionError):
            exporter.validate(data)

    def test_a_link_enters_at_the_first_tier_of_a_node(self):
        data = self.source()
        next(u for u in data['unlocks'] if u['id'] == 'attack_bonus_7')['prerequisites'] = ['attack_bonus_6', 'max_health_bonus_8']
        with self.assertRaises(AssertionError):
            exporter.validate(data)

if __name__ == '__main__':
    unittest.main()
