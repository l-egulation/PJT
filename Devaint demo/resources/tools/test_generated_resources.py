"""Generated exports must not impersonate or invalidate imported originals."""
from pathlib import Path
import hashlib
import json
import tempfile
import unittest

from verify_resources import verify


class GeneratedResourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.name = 'ui/example/runtime/icon.png'
        self.payload = b'new reviewed resource bytes'
        target = self.root / self.name
        target.parent.mkdir(parents=True)
        target.write_bytes(self.payload)
        self.item = {'path': self.name, 'bytes': len(self.payload),
                     'sha256': hashlib.sha256(self.payload).hexdigest()}
        self.write('catalog.json', {'format': 'deviant-resource-catalog-v1',
                   'importMapping': 'imports/mapping.json',
                   'generatedManifest': 'generated-assets.json',
                   'assets': [{'id': 'ui.example', 'path': 'ui/example', 'fileCount': 1}]})
        self.write('imports/mapping.json', {'format': 'deviant-resource-import-v1', 'records': []})
        self.write('ui/example/asset.json', {'id': 'ui.example', 'path': 'ui/example',
                                          'files': ['runtime/icon.png']})
        self.write('.lfs-payloads.json', [])
        self.manifest([self.item])

    def write(self, name, data):
        target = self.root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(data), encoding='utf-8')

    def manifest(self, records):
        self.write('generated-assets.json', {'format': 'deviant-generated-resources-v1',
                                            'files': records})

    def test_new_exports_are_checked_without_changing_import_mapping(self):
        mapping = (self.root / 'imports/mapping.json').read_bytes()
        result = verify(self.root, check_links=True)
        self.assertEqual(1, result['generatedFiles'])
        self.assertEqual(mapping, (self.root / 'imports/mapping.json').read_bytes())

    def test_modified_output_is_rejected(self):
        (self.root / self.name).write_bytes(b'unreviewed replacement')
        with self.assertRaisesRegex(ValueError, 'Bytes/hash mismatch'):
            verify(self.root)

    def test_generated_record_cannot_override_imported_original(self):
        self.write('imports/mapping.json', {'format': 'deviant-resource-import-v1', 'records': [
            {'package': None, 'path': 'original.png', 'target': self.name,
             'sha256': self.item['sha256'], 'bytes': self.item['bytes']}]})
        with self.assertRaisesRegex(ValueError, 'conflicts with an imported'):
            verify(self.root)

    def test_duplicate_or_escaping_paths_are_rejected(self):
        for records in ([self.item, self.item], [{**self.item, 'path': '../escape.png'}]):
            with self.subTest(records=records):
                self.manifest(records)
                with self.assertRaises(ValueError):
                    verify(self.root)

    def test_generated_file_requires_an_asset_owner(self):
        self.write('catalog.json', {'format': 'deviant-resource-catalog-v1',
                   'importMapping': 'imports/mapping.json',
                   'generatedManifest': 'generated-assets.json', 'assets': []})
        with self.assertRaisesRegex(ValueError, 'missing from catalog'):
            verify(self.root)


if __name__ == '__main__':
    unittest.main()
