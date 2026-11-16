"""Selective Drive imports preserve originals and fail before overwriting files."""
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import import_sword_gui as importer


def png(width, height):
    return b'\x89PNG\r\n\x1a\n' + b'\0\0\0\rIHDR' + struct.pack('>II', width, height)


class SwordGuiImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'resources'
        self.root.mkdir()
        self.archive = self.base / 'drive.zip'
        self.original = self.root / importer.SWORD / 'runtime/approved-eleven/character/F046.png'
        self.original.parent.mkdir(parents=True)
        self.original.write_bytes(b'previous approved frame')
        self.write_json(importer.SWORD + '/asset.json', {
            'id': 'characters.ink.weapons.sword', 'path': importer.SWORD,
            'sources': ['previous ZIP'], 'files': ['runtime/approved-eleven/character/F046.png']})
        self.write_json('catalog.json', {'format': 'deviant-resource-catalog-v1',
            'importMapping': 'original-mapping.json', 'assets': [
                {'id': 'characters.ink.weapons.sword', 'path': importer.SWORD, 'fileCount': 1}]})
        self.write_json('original-mapping.json', {'original': 'keep this byte-for-byte'})
        self.write_json('generated-assets.json', {'generated': 'keep this byte-for-byte too'})
        self.children = {'00_적용안내.txt': b'provided instructions\r\n'}
        self.children['원본/단순도트_문양20종_원본.png'] = b'original atlas bytes'
        for name in ('bag', 'growth', 'hud', 'shop'):
            self.children['실제화면/' + name + '.png'] = (name + ' actual capture').encode()
        self.children[importer.GUI_UNITY + 'Pixel.meta'] = b'original folder GUID\r\n'
        for i in range(20):
            name = importer.GUI_UNITY + f'Pixel/ink-test-{i:02}.png'
            self.children[name] = png(32, 32)
            self.children[name + '.meta'] = f'original GUID {i}\r\n'.encode()
        self.extra_outer = {}
        self.make_archive()

    def write_json(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(importer.encoded(value))

    def make_archive(self, loose_growth=None):
        nested = io.BytesIO()
        with zipfile.ZipFile(nested, 'w') as archive:
            for name, data in self.children.items():
                archive.writestr(importer.PACKAGE_NAME[:-4] + '/' + name, data)
        with zipfile.ZipFile(self.archive, 'w') as archive:
            for frame in range(100, 110):
                archive.writestr(importer.PREFIX + importer.SWORD_UNITY + f'F{frame}.png', png(256, 144))
            archive.writestr(importer.PACKAGE, nested.getvalue())
            archive.writestr(importer.SHARE + '00_적용안내.txt', self.children['00_적용안내.txt'])
            archive.writestr(importer.SHARE + '단순도트_문양20종_원본.png', self.children['원본/단순도트_문양20종_원본.png'])
            archive.writestr(importer.SHARE + '미리보기_팔레트관리.png',
                             loose_growth if loose_growth is not None else self.children['실제화면/growth.png'])
            archive.writestr('unselected-existing-resource.png', b'not in scope')
            for name, data in self.extra_outer.items():
                archive.writestr(name, data)

    def run_import(self):
        with patch.object(importer, 'ARCHIVE_SHA256', importer.digest(self.archive.read_bytes())):
            return importer.import_archive(self.archive, self.root)

    def snapshot(self):
        return {path.relative_to(self.root).as_posix(): path.read_bytes()
                for path in self.root.rglob('*') if path.is_file()}

    def test_preserves_all_selected_bytes_and_does_not_rewrite_old_sources(self):
        before = self.snapshot()
        result = self.run_import()
        self.assertEqual(61, result['inputRecords'])
        self.assertEqual(58, result['preservedFiles'])
        for name in ('original-mapping.json', 'generated-assets.json',
                     self.original.relative_to(self.root).as_posix()):
            self.assertEqual(before[name], (self.root / name).read_bytes())
        mapping = json.loads((self.root / importer.MAPPING).read_text())
        for record in mapping['records']:
            target = self.root / record['target']
            self.assertEqual(record['sha256'], importer.digest(target.read_bytes()))
        meta = self.root / importer.GUI / 'runtime/paper-gui/Pixel/ink-test-00.png.meta'
        self.assertEqual(b'original GUID 0\r\n', meta.read_bytes())

    def test_reimport_is_byte_for_byte_idempotent(self):
        self.run_import()
        before = self.snapshot()
        self.run_import()
        self.assertEqual(before, self.snapshot())

    def test_unknown_archive_is_rejected_before_writes(self):
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, 'Unexpected Drive ZIP'):
            importer.import_archive(self.archive, self.root)
        self.assertEqual(before, self.snapshot())

    def test_different_existing_target_is_not_overwritten(self):
        target = self.root / importer.SWORD / 'runtime/basic-two-hit-256/character/F105.png'
        target.parent.mkdir(parents=True)
        target.write_bytes(b'local art edits')
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, 'Refusing to overwrite'):
            self.run_import()
        self.assertEqual(before, self.snapshot())

    def test_divergent_loose_and_packaged_preview_is_rejected(self):
        self.make_archive(loose_growth=b'different capture')
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, 'Conflicting source bytes'):
            self.run_import()
        self.assertEqual(before, self.snapshot())

    def test_unknown_shared_file_is_not_silently_omitted(self):
        self.extra_outer[importer.SHARE + 'new-source.png'] = b'preserve me too'
        self.make_archive()
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, 'GUI share contents changed'):
            self.run_import()
        self.assertEqual(before, self.snapshot())

    def test_unsafe_nested_path_is_rejected(self):
        name = importer.GUI_UNITY + 'Pixel/ink-test-00.png'
        self.children[importer.GUI_UNITY + 'Pixel/../../outside.png'] = self.children.pop(name)
        self.make_archive()
        with self.assertRaisesRegex(ValueError, 'Unsafe GUI package path'):
            self.run_import()

    def test_wrong_icon_dimensions_are_rejected(self):
        self.children[importer.GUI_UNITY + 'Pixel/ink-test-00.png'] = png(64, 64)
        self.make_archive()
        with self.assertRaisesRegex(ValueError, 'Unexpected GUI icon PNG'):
            self.run_import()


if __name__ == '__main__':
    unittest.main()
