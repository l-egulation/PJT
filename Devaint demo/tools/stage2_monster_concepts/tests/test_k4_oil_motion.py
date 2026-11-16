"""Check original art preservation and a complete painted-oil action timeline."""
import json
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image
from scipy.ndimage import binary_fill_holes

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from k4_oil_motion import ROOT,key_at


class OilMotionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.s=json.loads((ROOT/'data/presentation/stage2_k4_oil_motion.json').read_text(encoding='utf-8'));cls.out=ROOT/cls.s['output'];cls.m=json.loads((cls.out/'manifest.json').read_text(encoding='utf-8'))

    def test_three_material_anchors_are_original_pixels_at_same_root(self):
        src=ROOT/self.s['material'];old=json.loads((src/'manifest.json').read_text(encoding='utf-8'))['results'];old={r['id']:r for r in old}
        for index,ident in self.s['anchorKeys'].items():
            r=old[ident];patch=Image.open(src/r['native']).convert('RGBA');canvas=Image.new('RGBA',tuple(self.s['effectCanvas']));canvas.alpha_composite(patch,tuple(np.array(self.s['effectPivotPx'])-r['rootNativePx']))
            target=Image.open(self.out/self.m['effectPaths'][int(index)]).convert('RGBA');self.assertEqual(target.tobytes(),canvas.tobytes())

    def test_palette_alpha_no_holes_and_indexed_equality(self):
        pal=set(map(tuple,self.s['paletteRgb']))
        for p in self.m['effectPaths']:
            f=Image.open(self.out/p).convert('RGBA');a=np.array(f);solid=a[:,:,3]>0
            self.assertLessEqual(set(np.unique(a[:,:,3])),{0,255});self.assertLessEqual(set(map(tuple,np.unique(a[solid,:3],axis=0))),pal)
            self.assertFalse(np.any(binary_fill_holes(solid)&~solid),p)
            indexed=Image.open(self.out/'fx/indexed'/Path(p).name).convert('RGBA');self.assertEqual(f.tobytes(),indexed.tobytes())

    def test_drop_reaches_floor_and_vanishes_before_retracting(self):
        m=self.m;ds=m['effectDurationsMs'];self.assertEqual(len(ds),16);self.assertEqual(sum(ds[:7]),650)
        source=Image.open(self.out/m['effectPaths'][8]).convert('RGBA')
        for i,dy in enumerate(self.s['dropFallOffsetsY'],9):
            f=Image.new('RGBA',source.size);f.alpha_composite(source,(0,dy));self.assertEqual(f.tobytes(),Image.open(self.out/m['effectPaths'][i]).convert('RGBA').tobytes())
        f=Image.open(self.out/m['effectPaths'][13]);origin_y=self.s['bodyOffsetWorld'][1]+self.s['nozzleNativePx'][1]-self.s['effectPivotPx'][1]
        self.assertEqual(f.getbbox()[3]+origin_y,self.s['landingGroundYWorld'])
        self.assertIsNone(Image.open(self.out/m['effectPaths'][-1]).getbbox());self.assertEqual(ds[-1],100)
        self.assertEqual(sum(m['bodyDurationsMs'][:10]),m['retractStartMs']);self.assertEqual(m['retractStartMs'],1930)
        for r in m['timeline']:
            if 550<=r['timeMs']<m['retractStartMs']:self.assertEqual(r['bodyKey'],10)
            if r['timeMs']>=m['retractStartMs']:self.assertIsNone(r['effectKey'])

    def test_body_files_and_original_nonhold_times_and_h3_unchanged(self):
        m=self.m;src=ROOT/self.s['body'];old=json.loads((src/'manifest.json').read_text(encoding='utf-8'))['durationsMs']
        self.assertEqual(m['bodyDurationsMs'][:9],old[:9]);self.assertEqual(m['bodyDurationsMs'][10:],old[10:])
        for r in m['bodySourceRecords']:self.assertEqual((ROOT/r['source']).read_bytes(),(self.out/r['path']).read_bytes())
        self.assertEqual((ROOT/self.s['approvedH3']/'review-native.webp').read_bytes(),(self.out/'approved-h3/review-native.webp').read_bytes())


if __name__=='__main__':unittest.main()
