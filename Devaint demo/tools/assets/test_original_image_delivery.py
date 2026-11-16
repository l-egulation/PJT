"""The production contract must deliver authored PNG bytes before GPU compression."""
import hashlib
import json
from pathlib import Path
import struct
import unittest

from update_assets import backend

ROOT = Path(__file__).resolve().parents[2]


class OriginalImageDeliveryTests(unittest.TestCase):
    def test_all_gpu_images_use_the_selected_original_source_bytes_and_canvas(self):
        settings = json.loads((ROOT / 'data/assets/media-compression.json').read_text())
        self.assertEqual({'source': 'original'}, settings['images'])
        delivery = {r['target']: r for r in json.loads((ROOT / 'docs/assets/manifests/unity-deployment.json').read_text())['files']}
        images = json.loads((ROOT / 'unity/Assets/Deviant/Media/Editor/CompressionSelection.json').read_text())['images']
        self.assertGreater(len(images), 7000)
        store = backend(ROOT).Store(ROOT / 'resources')
        for image in images:
            target = 'unity/' + image['path']
            with self.subTest(target=target):
                item = delivery[target]
                self.assertFalse(item['source']['asset'].startswith('authoring.compressed-game-media'))
                original = store.read(item['source'])
                self.assertEqual(item['sha256'], hashlib.sha256(original).hexdigest())
                self.assertEqual(original, (ROOT / target).read_bytes())
                self.assertTrue(original.startswith(b'\x89PNG\r\n\x1a\n'))
                self.assertEqual((image['width'], image['height']), struct.unpack('>II', original[16:24]))
