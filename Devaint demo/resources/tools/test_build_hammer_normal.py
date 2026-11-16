"""Weapon replacement reuses the approved body, ground ink and lifecycle."""
import unittest
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi
import build_hammer_normal as hammer

ASSET = Path(__file__).resolve().parents[1]/'characters/ink/weapons/hammer'
SOURCE = ASSET/'source/user-hammer-normal-20261031'


class HammerNormalTests(unittest.TestCase):
    def tearDown(self):
        hammer.REFINE = False
        hammer.RIGID = False

    def test_grip_passes_through_the_palm_center(self):
        icon=Image.open(SOURCE/'hammer-native32.png').convert('RGBA')
        hammer.REFINE=True
        mask=np.array(hammer.weapon_layer(icon,[58,102],-4).getchannel('A'))>0
        ys=np.where(mask[:,90])[0]
        self.assertLessEqual(abs(float(ys.mean())-132),1.5)

    def test_native_rotation_has_no_single_pixel_teeth(self):
        icon=Image.open(SOURCE/'hammer-native32.png').convert('RGBA')
        hammer.REFINE=True
        mask=np.array(hammer.weapon_layer(icon,[58,102],-4).getchannel('A'))>0
        neighbors=ndi.convolve(mask.astype(int),np.ones((3,3),int),mode='constant')
        self.assertEqual(int(np.count_nonzero(mask & (neighbors<=3))),0)

    def test_all_visible_body_pixels_including_ground_ink_and_shadow_are_untouched(self):
        for path in (SOURCE/'bodies').glob('*.png'):
            a=np.array(Image.open(path).convert('RGBA'))
            b=np.array(hammer.prepare_body(Image.open(path)))
            np.testing.assert_array_equal(a[a[:,:,3]>0],b[a[:,:,3]>0],err_msg=path.name)
            np.testing.assert_array_equal(a[:,:,3],b[:,:,3],err_msg=path.name)

    def test_approved_last_five_frames_are_exactly_the_original_body_and_ink(self):
        for frame in range(8,13):
            body=hammer.body_canvas(Image.open(SOURCE/'bodies'/f'death_{frame:02}.png'))
            approved=Image.open(ASSET/'runtime/approved-eleven/character'/f'F{33+frame:03}.png').convert('RGBA')
            np.testing.assert_array_equal(np.array(body),np.array(approved),err_msg=str(frame))
            self.assertEqual(int(hammer.approved_weapon_mask(approved,body).sum()),0)

    def test_no_independent_weapon_survives_when_the_original_has_none(self):
        layer=hammer.rigid_weapon_layer([58,102],-9)
        body=hammer.body_canvas(Image.open(SOURCE/'bodies/death_08.png'))
        result=hammer.reuse_approved_death_weapon(layer,body,0,996,1000)
        self.assertIsNone(result.getbbox())

    def test_replacement_uses_original_visibility_progression(self):
        layer=hammer.rigid_weapon_layer([58,102],-9)
        first=hammer.body_canvas(Image.open(SOURCE/'bodies/death_01.png'))
        first_new=int(hammer.approved_weapon_mask(layer,first).sum())
        counts=[]
        for frame in range(4,9):
            body=hammer.body_canvas(Image.open(SOURCE/'bodies'/f'death_{frame:02}.png'))
            approved=Image.open(ASSET/'runtime/approved-eleven/character'/f'F{33+frame:03}.png').convert('RGBA')
            visible=int(hammer.approved_weapon_mask(approved,body).sum())
            result=hammer.reuse_approved_death_weapon(layer,body,visible,996,first_new)
            actual=int(hammer.approved_weapon_mask(result,body).sum())
            self.assertLessEqual(abs(actual-round(first_new*visible/996)),35)
            counts.append(actual)
        self.assertEqual(counts,sorted(counts,reverse=True))
        self.assertEqual(counts[-1],0)


if __name__=='__main__':
    unittest.main()
