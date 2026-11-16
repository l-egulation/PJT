"""Verify final source fidelity, color, atlas import rectangles and portable paths."""
from pathlib import Path
import base64
import hashlib
import io
import json
import unittest
from PIL import Image, ImageChops

HERE = Path(__file__).resolve().parent


class AssetsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((HERE / 'data.json').read_text(encoding='utf8'))
        cls.source_bytes = (HERE / 'source/viewer-data.json').read_bytes()
        cls.source = json.loads(cls.source_bytes)

    def safe_asset(self, relative):
        target = (HERE / relative).resolve()
        self.assertTrue(target.is_relative_to(HERE.resolve()))
        self.assertFalse(Path(relative).is_absolute())
        self.assertTrue(target.is_file(), relative)
        return target

    def test_source_and_only_referenced_frames(self):
        report = json.loads((HERE / 'export-report.json').read_text(encoding='utf8'))
        self.assertEqual(hashlib.sha256(self.source_bytes).hexdigest(), report['sourceSha256'])
        self.assertEqual(len(self.data['weapons']), 7)
        for current, original in zip(self.data['weapons'], self.source['weapons']):
            for key in ('id', 'name', 'pivot', 'sequences', 'hits', 'manualInput'):
                self.assertEqual(current[key], original[key])
            referenced = {s['frame'] for seq in current['sequences'].values() for s in seq}
            self.assertEqual(set(current['frames']), referenced)
            for frame, relative in current['frames'].items():
                self.assertEqual(self.safe_asset(relative).read_bytes(), base64.b64decode(original['frames'][frame].split(',')[1]))
                self.assertEqual(current['pivots'].get(frame), original['pivots'].get(frame))
                self.assertEqual(current['offsets'].get(frame), original['offsets'].get(frame))

    def test_effect_pixels_timings_and_black_palette(self):
        for current, original in zip(self.data['weapons'], self.source['weapons']):
            self.assertEqual(len(current['fx']), len(original['fx']))
            for effect, expected in zip(current['fx'], original['fx']):
                self.assertEqual({k: v for k, v in effect.items() if k != 'png'}, {k: v for k, v in expected.items() if k != 'png'})
                raw = self.safe_asset(effect['png']).read_bytes()
                self.assertEqual(raw, base64.b64decode(expected['png'].split(',')[1]))
                colors = {pixel[:3] for pixel in Image.open(io.BytesIO(raw)).convert('RGBA').get_flattened_data() if pixel[3]}
                self.assertTrue(colors <= {(27, 22, 22)}, (current['id'], colors))

    def test_unity_sheets_render_same_pixels_at_same_ground(self):
        width, height = self.data['canvas']
        for weapon in self.data['weapons']:
            folder = HERE / 'weapons' / weapon['id']
            config = json.loads((folder / 'animations.json').read_text(encoding='utf8'))
            atlas = Image.open(folder / config['sheet']).convert('RGBA')
            self.assertEqual(config['sequences'], weapon['sequences'])
            for frame, sprite in config['sprites'].items():
                x, y, w, h = sprite['rectTopLeft']
                self.assertEqual([w, h], [width, height])
                self.assertEqual(sprite['unityRectBottomLeft'], [x, atlas.height-y-h, w, h])
                cell = atlas.crop((x, y, x+w, y+h))
                image = Image.open(self.safe_asset(weapon['frames'][frame])).convert('RGBA')
                pivot = weapon['pivots'].get(frame, weapon['pivot'])
                offset = weapon['offsets'].get(frame, [0, 0])
                # Compare cropped opaque content, plus its independent anchor position.
                bbox = image.getbbox()
                if bbox:
                    cx, cy = self.data['ground'][0]-pivot[0]+offset[0], self.data['ground'][1]-pivot[1]+offset[1]
                    actual_bbox = cell.getbbox()
                    self.assertEqual(actual_bbox, (cx+bbox[0], cy+bbox[1], cx+bbox[2], cy+bbox[3]))
                    a, b = image.crop(bbox), cell.crop(actual_bbox)
                    self.assertIsNone(ImageChops.difference(a, b).getbbox(alpha_only=False))
                else:
                    self.assertIsNone(cell.getbbox())

    def test_portable_viewer_and_no_embedded_images_in_code(self):
        page = (HERE / 'index.html').read_text(encoding='utf8')
        self.assertIn('<script src="data.js"></script><script src="viewer.js"></script>', page)
        self.assertTrue((HERE / 'assets/무기7종_공격_이펙트.gif').exists())
        for name in ('data.json', 'data.js', 'viewer.js', 'index.html'):
            content = (HERE / name).read_text(encoding='utf8')
            self.assertNotIn('data:image/', content)
            self.assertNotIn('C:/Users/', content)
            self.assertNotIn('C:\\Users\\', content)


if __name__ == '__main__':
    unittest.main()
