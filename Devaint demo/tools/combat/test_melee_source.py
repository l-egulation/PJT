"""Checks source ownership and resolution using isolated copies of canonical data."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from melee_source import DEFAULT_ROOT, load_melee


class MeleeSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'combat'
        shutil.copytree(DEFAULT_ROOT, self.root)

    def update(self, name, change):
        path = self.root / name
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        change(data)
        path.write_text(json.dumps(data), encoding='utf-8')

    def test_first_hit_edit_changes_only_its_resolved_values(self):
        before = load_melee(self.root)
        def edit(action):
            action['hit'].update(damage=24, duration=.6, impact=.35, active=.1)
            action['hit']['box'].update(width=8, x=4.1875)
        self.update('actions/base_sword_basic_0.json', edit)
        after = load_melee(self.root)
        basic = after['forms'][0]['basic']
        self.assertEqual(basic['damages'], [24, before['forms'][0]['basic']['damages'][1]])
        self.assertEqual(basic['motion_durations'], [.6, .37])
        self.assertEqual(basic['local_hit_times'], [.35, .13])
        self.assertEqual(basic['active_durations'], [.1, .08])
        self.assertAlmostEqual(basic['fastest_cycle'], .97)
        self.assertEqual(basic['shapes'][0]['size'], [8, 2])
        self.assertEqual(basic['shapes'][0]['left_offset'], [-4.1875, 1])
        self.assertEqual(basic['shapes'][1], before['forms'][0]['basic']['shapes'][1])
        self.assertEqual(after['forms'][0]['specials'], before['forms'][0]['specials'])
        self.assertEqual(after['forms'][1:], before['forms'][1:])

    def test_duplicate_numeric_source_is_rejected(self):
        self.update('melee_weapon_design.json', lambda data: data['forms'][0]['basic']['damages'].__setitem__(0, 10))
        with self.assertRaisesRegex(ValueError, 'duplicate hit source'):
            load_melee(self.root)

    def test_derived_cycle_cannot_be_authored_twice(self):
        self.update('melee_weapon_design.json', lambda data: data['forms'][0]['basic'].update(fastest_cycle=1))
        with self.assertRaisesRegex(ValueError, 'cycle must be derived'):
            load_melee(self.root)

    def test_wrong_id_and_schema_are_rejected(self):
        for edit in [dict(id='another_action'), dict(id='base_sword_basic_0', schemaVersion=2)]:
            self.update('actions/base_sword_basic_0.json', lambda action: action.update(edit))
            with self.assertRaisesRegex(ValueError, 'invalid schema, identity'):
                load_melee(self.root)

    def test_source_cannot_escape_combat_directory(self):
        self.update('melee_weapon_design.json', lambda data: data['forms'][0]['basic']['hit_sources'].__setitem__(0, '../outside.json'))
        with self.assertRaisesRegex(ValueError, 'inside data/combat'):
            load_melee(self.root)

    def test_finite_numeric_generation_preserves_inactive_timing_and_distance(self):
        original_wave = next(s for f in load_melee(self.root)['forms'] for s in f['specials'] if s['id'] == 'base_sword_range')['independent_effect']
        def edit(data):
            actions = {value['id']: value for form in data['forms'] for value in form['specials']}
            actions['base_sword_movement']['hits'][0]['damage'] = 13
            actions['base_sword_movement']['movement'].update(speed=12, wall_recovery=.4)
            actions['base_sword_follow_up']['hits'][0]['damage'] = 9
            actions['base_sword_follow_up']['hits'][1]['damage'] = 17
            actions['base_sword_range']['hits'][0]['damage'] = 53
            actions['base_sword_range']['independent_effect'].update(speed=8, distance=2, right_spawn=[2, 1.25], left_spawn=[-2, 1.25])
            actions['shield_range']['independent_effect'].update(shield_hp=50, damage_reduction=.25)
        self.update('melee_weapon_design.json', edit)
        repository = Path(self.temp.name) / 'finite-repository'
        shutil.copytree(self.root, repository / 'data/combat')
        scripts = repository / 'tools/combat'
        scripts.mkdir(parents=True)
        for name in ['build_melee_design.py', 'export_unity_catalog.py', 'melee_source.py']:
            shutil.copy(Path(__file__).parent / name, scripts / name)
        (repository / 'docs/design/equipment/weapons/forms').mkdir(parents=True)
        source_before = (repository / 'data/combat/melee_weapon_design.json').read_bytes()
        for name in ['build_melee_design.py', 'export_unity_catalog.py']:
            result = subprocess.run([sys.executable, str(scripts / name)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((repository / 'data/combat/melee_weapon_design.json').read_bytes(), source_before)
        generated = json.loads((repository / 'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json').read_text(encoding='utf-8'))
        actions = {value['id']: value for form in generated['forms'] for value in form['specials']}
        movement = actions['base_sword_movement']
        self.assertEqual(movement['hits'][0]['damage'], 13)
        self.assertEqual(movement['movement']['speed'], 12)
        self.assertEqual(movement['movement']['distance'], 6)
        self.assertEqual(movement['movement']['duration'], .2)
        self.assertEqual(movement['movement']['wallRecovery'], .4)
        self.assertEqual([hit['damage'] for hit in actions['base_sword_follow_up']['hits']], [9, 17])
        wave = actions['base_sword_range']['effect']
        self.assertEqual([wave[key] for key in ['speed', 'distance', 'x', 'y', 'duration']], [8, 2, 2, 1.25, original_wave['lifetime']])
        guard = actions['shield_range']['effect']
        self.assertEqual([guard[key] for key in ['hp', 'reduction', 'duration']], [50, .25, 3])
        canonical = repository / 'data/combat/melee_weapon_design.json'
        valid = json.loads(canonical.read_text(encoding='utf-8'))
        catalog_path = repository / 'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json'
        catalog_before = catalog_path.read_bytes()
        for action_id, group, field, value in [
            ('base_sword_movement', 'movement', 'speed', 0),
            ('base_sword_range', 'independent_effect', 'distance', float('inf')),
            ('shield_range', 'independent_effect', 'damage_reduction', 1.1),
        ]:
            invalid = json.loads(json.dumps(valid))
            selected = next(action for form in invalid['forms'] for action in form['specials'] if action['id']==action_id)
            selected[group][field] = value
            canonical.write_text(json.dumps(invalid), encoding='utf-8')
            result = subprocess.run([sys.executable, str(scripts / 'build_melee_design.py')], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0, action_id + '/' + field)
            self.assertEqual(catalog_path.read_bytes(), catalog_before)


if __name__ == '__main__':
    unittest.main()
