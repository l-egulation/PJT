"""Check pixel invariants and exact timeline boundaries in generated review assets."""
import hashlib
import json
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from attack_fx_polish import ROOT,inbetween,decode_webp,key_at


class AttackFxTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec=json.loads((ROOT/'data/presentation/stage2_attack_fx_polish.json').read_text(encoding='utf-8'))
        cls.out=ROOT/cls.spec['output']
        cls.manifest=json.loads((cls.out/'manifest.json').read_text(encoding='utf-8'))

    def test_transition_moves_pixels_instead_of_blending_or_holding(self):
        a=np.zeros((12,12,4),np.uint8);b=a.copy();a[3:6,3:6]=[41,37,47,255];b[2:10,2:10]=[128,88,110,255]
        palette=np.array([[41,37,47],[128,88,110]],np.uint8)
        previous=a
        for u in [.25,.5,.75,1]:
            f=inbetween(a,b,u,palette)
            self.assertLessEqual(set(np.unique(f[:,:,3])),{0,255})
            self.assertEqual(int(np.any(f!=a,axis=2).sum()),round(u*np.any(a!=b,axis=2).sum()))
            self.assertTrue(np.any(f!=previous));previous=f
        np.testing.assert_array_equal(previous,b)

    def test_original_eight_charge_keys_and_timing(self):
        s=self.spec['h3'];m=json.loads((self.out/'h3/timeline.json').read_text(encoding='utf-8'))
        self.assertEqual(len(m['frames']),27);self.assertEqual(sum(m['durationsMs']),1600);self.assertEqual(m['durationsMs'][-1],300)
        starts=np.cumsum([0]+s['originalFormationDurationsMs'][:-1]).tolist()
        for k,t in enumerate(starts):
            p=next(f for f in m['frames'] if f['timeMs']==t)
            old=Image.open(ROOT/s['approved']/'formation/native'/f'orb-{k+1:02}.png').convert('RGBA')
            new=Image.open(self.out/'h3/native'/p['path']).convert('RGBA')
            self.assertEqual(old.tobytes(),new.tobytes())
        self.assertLessEqual(self.manifest['results']['h3']['maxChangedPixelsAfter'],100)

    def test_flight_impact_puddle_and_body_after_release_are_exact(self):
        old,ds=decode_webp(ROOT/self.spec['h3']['latest']/'review-native.webp');new,nds=decode_webp(self.out/'h3/review-native.webp')
        self.assertEqual(sum(ds),6400);self.assertEqual(sum(nds),6400)
        for t in range(1600,6400,50):self.assertEqual(old[key_at(ds,t)].tobytes(),new[key_at(nds,t)].tobytes(),f'{t}ms')

    def test_spray_palette_alpha_size_root_and_unchanged_body(self):
        s=self.spec['k4'];palette=set(map(tuple,s['paletteRgb']))
        for p in sorted((self.out/'k4/native').glob('*.png')):
            im=Image.open(p).convert('RGBA');a=np.array(im);solid=a[:,:,3]>0
            self.assertEqual(im.size,(128,40));self.assertLessEqual(set(np.unique(a[:,:,3])),{0,255})
            self.assertLessEqual(set(map(tuple,np.unique(a[solid,:3],axis=0))),palette)
            self.assertEqual(im.getpixel((127,20))[3],255)
            indexed=Image.open(self.out/'k4/indexed'/p.name).convert('RGBA');self.assertEqual(indexed.tobytes(),im.tobytes())
        for r in self.manifest['results']['k4']['sourceRecords']:
            self.assertEqual(hashlib.sha256((ROOT/r['source']).read_bytes()).hexdigest(),r['sha256'])
            self.assertEqual((ROOT/r['source']).read_bytes(),(self.out/r['copy']).read_bytes())

    def test_spray_has_exactly_650ms_active_and_no_recovery_attack(self):
        rows=json.loads((self.out/'k4/timeline.json').read_text(encoding='utf-8'))
        self.assertEqual(sum(r['durationMs'] for r in rows if r['attackActive']),650)
        for r in rows:self.assertEqual(r['attackActive'],550<=r['timeMs']<1200)
        starts={r['timeMs'] for r in rows}
        self.assertLessEqual(set(range(550,1200,80)),starts)
        for r in rows:
            if r['attackActive']:self.assertEqual(r['effectKey'],((r['timeMs']-550)//80)%8+1)


if __name__=='__main__':unittest.main()
