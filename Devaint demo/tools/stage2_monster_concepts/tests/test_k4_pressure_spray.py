"""Check the physical force contracts and the source-pixel/retraction boundary."""
import json
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from k4_pressure_spray import ROOT,pressure,pair_forces


class PressureSprayTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec=json.loads((ROOT/'data/presentation/stage2_k4_pressure_spray.json').read_text(encoding='utf-8'))
        cls.s=cls.spec['simulation'];cls.out=ROOT/cls.spec['output'];cls.info=json.loads((cls.out/'manifest.json').read_text(encoding='utf-8'))

    def test_pressure_decreases_and_stops_before_retraction(self):
        p=[pressure(t/1000,self.s) for t in range(0,651,5)]
        self.assertAlmostEqual(p[0],2.2);self.assertEqual(p[-1],0)
        self.assertTrue(all(a>=b for a,b in zip(p,p[1:])))
        trace=json.loads((self.out/'fx/particle-trace.json').read_text(encoding='utf-8'))['samples']
        for r in trace:self.assertAlmostEqual(r['exitSpeedPxPerSecond']**2,self.s['baseExitSpeedPxPerSecond']**2*r['pressure'],places=6)

    def test_internal_forces_conserve_momentum_and_viscosity_dissipates(self):
        pos=np.array([[0.,0.],[1.7,0.],[3.4,.3]]);vel=np.array([[8.,1.],[-5.,4.],[0.,-3.]]);mass=np.array([2.,5.,1.])
        force=pair_forces(pos,vel,mass,self.s);np.testing.assert_allclose(force.sum(0),[0,0],atol=1e-12)
        viscous=pair_forces(pos,vel,mass,{**self.s,'cohesionPerSecondSquared':0.})
        self.assertLess(float((viscous*vel).sum()),0.)

    def test_emitted_area_matches_integrated_mass_flow(self):
        s=self.s;dt=s['stepSeconds'];steps=round(s['maxSeconds']/dt)
        flux=sum(s['apertureWidthPx']*s['baseExitSpeedPxPerSecond']*np.sqrt(pressure(i*dt,s))*dt for i in range(steps))
        result=self.info['simulationResult']
        self.assertAlmostEqual(flux,result['emittedArea']+result['unemittedRemainderArea'],places=7)

    def test_body_hold_continues_until_the_last_visible_ink(self):
        m=self.info;self.assertGreater(m['retractStartMs'],1200)
        self.assertEqual(sum(m['bodyDurationsMs'][:9]),550)
        self.assertEqual(sum(m['bodyDurationsMs'][:10]),m['retractStartMs'])
        self.assertEqual(m['bodyDurationsMs'][:9],m['originalBodyDurationsMs'][:9]);self.assertEqual(m['bodyDurationsMs'][10:],m['originalBodyDurationsMs'][10:])
        self.assertIsNone(Image.open(self.out/m['effectPaths'][-1]).getbbox())
        for r in m['timeline']:
            if r['visibleDrain']:self.assertEqual(r['bodyKey'],10)
            if r['retracting']:self.assertIsNone(r['effectKey'])

    def test_masks_and_colors_and_preserved_source_files(self):
        m=self.info;palette=set(map(tuple,self.spec['paletteRgb']))
        for i,p in enumerate(m['effectPaths']):
            f=Image.open(self.out/p).convert('RGBA');a=np.array(f);solid=a[:,:,3]>0
            self.assertLessEqual(set(np.unique(a[:,:,3])),{0,255})
            self.assertLessEqual(set(map(tuple,np.unique(a[solid,:3],axis=0))),palette)
            np.testing.assert_array_equal(a[:,:,3],np.array(Image.open(self.out/'fx/coverage'/Path(p).name)))
            b=f.getbbox();self.assertEqual(list(b) if b else None,m['effectCoverage'][i]['bboxLocalPx'])
        for r in m['bodySources']:self.assertEqual((ROOT/r['source']).read_bytes(),(self.out/r['copy']).read_bytes())
        for r in m['h3Copies']:self.assertEqual((ROOT/self.spec['approvedH3']/r['path']).read_bytes(),(self.out/'approved-h3'/r['path']).read_bytes())


if __name__=='__main__':unittest.main()
