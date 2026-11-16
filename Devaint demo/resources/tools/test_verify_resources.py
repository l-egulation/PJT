"""Integrity checks for corrupted assets, missing provenance and unsafe paths."""
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from verify_resources import digest, verify


class ResourceVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.asset = 'effects/example'
        self.target = self.asset + '/runtime/frame.png'
        self.payload = b'example-image-bytes'
        self.archive = self.root / 'input.zip'
        with zipfile.ZipFile(self.archive, 'w') as archive:
            archive.writestr('frame.png', self.payload)
        self.mapping = {
            'format': 'deviant-resource-import-v1',
            'archive': {'bytes': self.archive.stat().st_size, 'sha256': digest(self.archive.read_bytes())},
            'records': [{'package': None, 'path': 'frame.png', 'target': self.target,
                         'bytes': len(self.payload), 'sha256': digest(self.payload)}],
        }
        self.write(self.target, self.payload)
        self.write_json('mapping.json', self.mapping)
        self.write_json('.lfs-payloads.json', [])
        self.write_json('catalog.json', {
            'format': 'deviant-resource-catalog-v1', 'importMapping': 'mapping.json',
            'assets': [{'id': 'effects.example', 'path': self.asset, 'fileCount': 1}],
        })
        self.write_json(self.asset + '/asset.json', {
            'id': 'effects.example', 'path': self.asset, 'files': ['runtime/frame.png'],
        })

    def write(self, path, value):
        file = self.root / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_bytes(value)

    def write_json(self, path, value):
        self.write(path, json.dumps(value).encode('utf-8'))

    def test_complete_source_round_trip(self):
        result = verify(self.root, self.archive)
        self.assertTrue(result['originalArchiveVerified'])
        self.assertEqual(result['verifiedFiles'], 1)

    def test_same_size_corruption_is_rejected(self):
        self.write(self.target, b'X' * len(self.payload))
        with self.assertRaisesRegex(ValueError, 'Bytes/hash mismatch'):
            verify(self.root)

    def test_unhydrated_lfs_pointer_is_rejected(self):
        self.write(self.target, b'version https://git-lfs.github.com/spec/v1\noid sha256:' + b'0' * 64 + b'\nsize 19\n')
        with self.assertRaisesRegex(ValueError, 'LFS object not downloaded'):
            verify(self.root)

    def test_missing_asset_is_rejected(self):
        (self.root / self.target).unlink()
        with self.assertRaises(FileNotFoundError):
            verify(self.root)

    def test_path_escape_is_rejected(self):
        self.mapping['records'][0]['target'] = '../outside.png'
        self.write_json('mapping.json', self.mapping)
        with self.assertRaisesRegex(ValueError, 'Invalid repository path'):
            verify(self.root)

    def test_unmapped_archive_entry_is_rejected(self):
        with zipfile.ZipFile(self.archive, 'a') as archive:
            archive.writestr('omitted.png', b'preserve-this-too')
        self.mapping['archive'] = {'bytes': self.archive.stat().st_size,
                                   'sha256': digest(self.archive.read_bytes())}
        self.write_json('mapping.json', self.mapping)
        with self.assertRaisesRegex(ValueError, 'Unmapped/duplicate ZIP entry'):
            verify(self.root, self.archive)

    def test_missing_catalog_ownership_is_rejected(self):
        self.write_json('catalog.json', {
            'format': 'deviant-resource-catalog-v1', 'importMapping': 'mapping.json', 'assets': [],
        })
        with self.assertRaisesRegex(ValueError, 'Asset files missing from catalog'):
            verify(self.root)

    def add_selected_import(self):
        self.extra_archive = self.root / 'additional.zip'
        nested = io.BytesIO()
        with zipfile.ZipFile(nested, 'w') as archive:
            archive.writestr('icon.png', b'new icon')
            archive.writestr('icon.png.meta', b'original GUID bytes\r\n')
        sources = [(None, 'frame.png', 'second.png', b'new sword frame'),
                   (None, 'gui/package.zip', 'package.zip', nested.getvalue()),
                   ('gui/package.zip', 'icon.png', 'icon.png', b'new icon'),
                   ('gui/package.zip', 'icon.png.meta', 'icon.png.meta', b'original GUID bytes\r\n')]
        with zipfile.ZipFile(self.extra_archive, 'w') as archive:
            archive.writestr('unselected.png', b'unrelated existing Drive resource')
            for package, path, _, data in sources:
                if package is None:
                    archive.writestr(path, data)
        self.extra = {'format': 'deviant-resource-import-v1',
                      'archive': {'bytes': self.extra_archive.stat().st_size,
                                  'sha256': digest(self.extra_archive.read_bytes())},
                      'selection': {'files': ['frame.png'], 'directories': ['gui/']},
                      'records': []}
        asset = json.loads((self.root / self.asset / 'asset.json').read_text())
        for package, path, name, data in sources:
            target = self.asset + '/runtime/' + name
            self.write(target, data)
            self.extra['records'].append({'package': package, 'path': path, 'target': target,
                                          'bytes': len(data), 'sha256': digest(data)})
            asset['files'].append('runtime/' + name)
        self.write_json(self.asset + '/asset.json', asset)
        catalog = json.loads((self.root / 'catalog.json').read_text())
        catalog['assets'][0]['fileCount'] = len(asset['files'])
        catalog['additionalImportMappings'] = ['additional-mapping.json']
        self.write_json('catalog.json', catalog)
        self.write_json('additional-mapping.json', self.extra)

    def test_selected_import_preserves_primary_mapping_and_checks_nested_sources(self):
        original = (self.root / 'mapping.json').read_bytes()
        self.add_selected_import()
        result = verify(self.root, self.extra_archive)
        self.assertFalse(result['originalArchiveVerified'])
        self.assertEqual(5, result['inputRecords'])
        self.assertEqual('selected-inputs', result['importMappings'][1]['scope'])
        self.assertTrue(result['importMappings'][1]['archiveVerified'])
        self.assertEqual(original, (self.root / 'mapping.json').read_bytes())

    def test_primary_and_additional_archives_can_be_checked_together(self):
        self.add_selected_import()
        result = verify(self.root, self.archive, additional_archives=[self.extra_archive])
        self.assertTrue(result['originalArchiveVerified'])
        self.assertTrue(all(item['archiveVerified'] for item in result['importMappings']))

    def test_additional_resource_corruption_is_rejected(self):
        self.add_selected_import()
        self.write(self.extra['records'][0]['target'], b'X' * len(b'new sword frame'))
        with self.assertRaisesRegex(ValueError, 'Bytes/hash mismatch'):
            verify(self.root)

    def test_additional_import_cannot_replace_original_bytes(self):
        self.add_selected_import()
        self.extra['records'][0]['target'] = self.target
        self.write_json('additional-mapping.json', self.extra)
        with self.assertRaisesRegex(ValueError, 'Conflicting target record'):
            verify(self.root)

    def test_missing_selected_package_child_is_rejected(self):
        self.add_selected_import()
        missing = self.extra['records'].pop()
        asset = json.loads((self.root / self.asset / 'asset.json').read_text())
        asset['files'].remove(missing['target'][len(self.asset) + 1:])
        self.write_json(self.asset + '/asset.json', asset)
        catalog = json.loads((self.root / 'catalog.json').read_text())
        catalog['assets'][0]['fileCount'] -= 1
        self.write_json('catalog.json', catalog)
        self.write_json('additional-mapping.json', self.extra)
        with self.assertRaisesRegex(ValueError, 'Unmapped/duplicate ZIP entry'):
            verify(self.root, self.extra_archive)

    def test_nonexistent_selected_source_is_rejected(self):
        self.add_selected_import()
        self.extra['records'][0]['path'] = 'gui/nonexistent.png'
        self.write_json('additional-mapping.json', self.extra)
        with self.assertRaisesRegex(ValueError, 'Unmapped/duplicate ZIP entry|nonexistent ZIP entries'):
            verify(self.root, self.extra_archive)

    def test_record_outside_explicit_selection_is_rejected(self):
        self.add_selected_import()
        self.extra['records'][0]['path'] = 'unselected.png'
        self.write_json('additional-mapping.json', self.extra)
        with self.assertRaisesRegex(ValueError, 'outside import selection'):
            verify(self.root)

    def test_unsafe_or_empty_selection_is_rejected(self):
        self.add_selected_import()
        for selection in ({}, {'directories': ['../escape/']}, {'directories': ['gui']},
                          {'files': ['frame.png', 'frame.png']}):
            with self.subTest(selection=selection):
                self.extra['selection'] = selection
                self.write_json('additional-mapping.json', self.extra)
                with self.assertRaises(ValueError):
                    verify(self.root)

    def test_duplicate_additional_mapping_or_source_is_rejected(self):
        self.add_selected_import()
        catalog = json.loads((self.root / 'catalog.json').read_text())
        catalog['additionalImportMappings'].append('additional-mapping.json')
        self.write_json('catalog.json', catalog)
        with self.assertRaisesRegex(ValueError, 'Duplicate import mapping'):
            verify(self.root)
        catalog['additionalImportMappings'].pop()
        self.write_json('catalog.json', catalog)
        self.extra['records'].append(self.extra['records'][0])
        self.write_json('additional-mapping.json', self.extra)
        with self.assertRaisesRegex(ValueError, 'Duplicate input record'):
            verify(self.root)


if __name__ == '__main__':
    unittest.main()
