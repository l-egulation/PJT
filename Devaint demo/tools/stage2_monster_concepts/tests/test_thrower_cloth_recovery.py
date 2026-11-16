"""Regression checks for gray/purple trousers becoming transparent at open edges."""
import json
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from thrower_walk_candidates import recover_cloth,ROOT

PALETTE=np.array([[37,36,43],[97,76,105],[140,129,121],[180,167,151],[196,180,153]],np.uint8)
CONFIG={'region':[1,0,5,4],'sourceRgbMinMax':210,'minCoverage':0.5,'grid':4,
        'grayPalette':[[140,129,121],[180,167,151],[196,180,153]],'purpleRgb':[97,76,105],
        'purpleMinBlueOverGreen':8,'purpleMinBlueOverRed':2,'neutralMaxChroma':50,'neutralMinPeak':115}


class ClothRecoveryTests(unittest.TestCase):
    def fixture(self):
        before=np.zeros((4,6,4),np.uint8)
        source=np.full((4,6,4),255,np.uint8)
        return before,source

    def test_open_gray_edge_is_solid_material(self):
        before,source=self.fixture();source[1,2]=[161,151,141,255]
        after,mask=recover_cloth(before,Image.fromarray(source),PALETTE,1,[0,0],CONFIG)
        self.assertEqual(int(after[1,2,3]),255,'Gray trousers touching the outside must not become alpha=0.')
        self.assertTrue(mask[1,2])
        self.assertIn(tuple(after[1,2,:3]),map(tuple,CONFIG['grayPalette']))

    def test_muted_purple_uses_existing_purple(self):
        before,source=self.fixture();source[1,3]=[156,150,160,255]
        after,_=recover_cloth(before,Image.fromarray(source),PALETTE,1,[0,0],CONFIG)
        self.assertEqual(after[1,3].tolist(),[97,76,105,255])

    def test_background_original_opaque_and_outside_region_stay_exact(self):
        before,source=self.fixture();before[1,2]=[37,36,43,255];source[1,2]=[161,151,141,255]
        source[1,0]=[161,151,141,255]
        after,mask=recover_cloth(before,Image.fromarray(source),PALETTE,1,[0,0],CONFIG)
        np.testing.assert_array_equal(after,before)
        self.assertFalse(mask.any())

    def test_near_white_fringe_is_not_new_cloth(self):
        before,source=self.fixture();source[1,2]=[235,234,233,255]
        after,mask=recover_cloth(before,Image.fromarray(source),PALETTE,1,[0,0],CONFIG)
        np.testing.assert_array_equal(after,before)
        self.assertFalse(mask.any())

    def test_returned_C7_reproduces_real_open_gap(self):
        folder=ROOT/'outputs/waterway/thrower-walk-candidates-20261101'
        if not (folder/'manifest.json').exists():self.skipTest('Requires locally preserved returned artwork.')
        info=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
        before=np.array(Image.open(folder/'C/native/walk-07.png').convert('RGBA'))
        source=Image.open(folder/'C/source/walk-07.png').convert('RGBA')
        config={**CONFIG,'region':[145,158,225,181]}
        self.assertEqual(int(before[165,160,3]),0,'Original C fixture must still reproduce the missing trouser pixel.')
        after,mask=recover_cloth(before,source,np.array(info['paletteRgb'],np.uint8),info['scale'],info['offset'],config)
        self.assertEqual(int(after[165,160,3]),255)
        self.assertGreater(int(mask.sum()),10)
        np.testing.assert_array_equal(after[:158],before[:158])
        np.testing.assert_array_equal(after[181:],before[181:])
        np.testing.assert_array_equal(after[before[:,:,3]>0],before[before[:,:,3]>0])


if __name__=='__main__':unittest.main()
