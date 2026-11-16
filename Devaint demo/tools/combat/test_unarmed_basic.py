"""Approved starter balance, authored contact and selected import contract must agree."""
import copy
import json
from pathlib import Path
import unittest
import export_attack_motion as motion

ROOT = Path(__file__).resolve().parents[2]


class UnarmedBasicTests(unittest.TestCase):
    def test_exported_balance_is_independent_and_has_two_separate_contacts(self):
        source = json.loads((ROOT/'data/combat/unarmed_basic.json').read_text(encoding='utf-8'))
        catalog = json.loads((ROOT/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json').read_text(encoding='utf-8'))
        form = next(f for f in catalog['forms'] if f['id']=='unarmed')
        self.assertEqual(form, source['form'])
        self.assertEqual([b['damage'] for b in form['basics']], [40,50])  # weapons/rules.md
        self.assertAlmostEqual(sum(b['duration'] for b in form['basics']), .68)
        self.assertTrue(form['specials'][0]['unavailable'])
        for basic, clip in zip(form['basics'],source['presentation']):
            self.assertAlmostEqual(sum(clip['seconds']),basic['duration'])
            self.assertAlmostEqual(sum(clip['seconds'][:clip['contact']]), basic['impact'])
            self.assertEqual(basic['active'], .06)
            self.assertEqual(basic['advance'],dict(distance=.2,start=.08,duration=.06))

    def test_registry_exports_both_attacks_and_preserves_other_forms(self):
        catalog=json.loads((ROOT/motion.CATALOG).read_text(encoding='utf-8'))
        baseline=copy.deepcopy(catalog)
        result=motion.apply_registry(motion.merge_unarmed_media(catalog),motion.load_registry())
        self.assertEqual(result,baseline)
        clips=next(s for s in result['sets'] if s['form']=='unarmed')['clips']
        self.assertEqual([len(c['frames']) for c in clips if c['name'] in ('attack1','attack2')],[6,5])
        effects=next(s for s in result['sets'] if s['form']=='unarmed')['effects']
        self.assertEqual([e['attack'] for e in effects],['attack1','attack2'])
        for clip in (c for c in clips if c['name'] in ('attack1','attack2')):
            self.assertEqual(clip['fxStart'],clip['sourceImpact'],'the crescent starts at the impact')
        actions={a['actionId']:a for a in result['motionRegistry']['actions']}
        for i in range(2):
            self.assertEqual(actions[f'unarmed_basic_{i}']['motion'],f'unarmed/attack{i+1}')

    def test_import_preserves_canvas_scale_and_existing_foot_origin(self):
        manifest=json.loads((ROOT/'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
        entries=[e for e in manifest['files'] if e['source']['asset']=='characters.ink.weapons.unarmed']
        self.assertEqual(len(entries),21)
        body=[e for e in entries if '/jab-combo/' in e['target']]
        self.assertEqual(len(body),11)
        for entry in body:
            meta=(ROOT/(entry['target']+'.meta')).read_text(encoding='utf-8')
            self.assertIn('spritePixelsToUnits: 106.6667',meta)
            # Unity's own float32 text, so opening the project does not rewrite the meta.
            self.assertIn('spritePivot: {x: 0.34375, y: 0.09166667}',meta)
        for entry in entries[len(body):]:
            self.assertIn('spritePixelsToUnits: 32',(ROOT/(entry['target']+'.meta')).read_text(encoding='utf-8'))
        import sys; sys.path.insert(0, str(ROOT/'tools/assets'))
        import register_unarmed_art as art
        self.assertEqual([art._unity_float(v) for v in (0.34375, 1-327/360, .5)], ['0.34375','0.09166667','0.5'])
        # Body frames are drawn at 1 unit = 106.6667 px, the same scale as the idle body (128 px at PPU32 scaled by 10/3).
        self.assertAlmostEqual(106.6667/32,10/3,places=4)


if __name__=='__main__': unittest.main()
