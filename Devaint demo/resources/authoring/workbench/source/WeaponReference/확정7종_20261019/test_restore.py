from pathlib import Path
import hashlib
import json
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
import restore

ARCHIVE = Path(sys.argv.pop(1)) if len(sys.argv) > 1 else None
TEST_TMP = (restore.ROOT / 'local-verification/weapon-restore').resolve()
assert TEST_TMP.is_relative_to(restore.ROOT.resolve())
TEST_TMP.mkdir(parents=True, exist_ok=True)


@unittest.skipUnless(ARCHIVE and ARCHIVE.exists(), 'Pass the matching asset ZIP path')
class RestoreTests(unittest.TestCase):
    def test_fresh_restore_and_idempotence(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            root = Path(tmp)
            self.assertEqual(restore.restore(ARCHIVE, root), len(json.loads((restore.HERE / '에셋목록.json').read_text(encoding='utf8'))['files']))
            self.assertEqual(restore.restore(ARCHIVE, root), 0)

    def test_changed_existing_file_is_not_overwritten(self):
        manifest = json.loads((restore.HERE / '에셋목록.json').read_text(encoding='utf8'))
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            root = Path(tmp)
            target = root / manifest['files'][0]['path']
            target.parent.mkdir(parents=True)
            target.write_bytes(b'keep my work')
            with self.assertRaisesRegex(ValueError, 'Existing file differs'):
                restore.restore(ARCHIVE, root)
            self.assertEqual(target.read_bytes(), b'keep my work')
            self.assertEqual(len(list(root.rglob('*.*'))), 1)

    def test_changed_archive_is_rejected(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            archive = Path(tmp) / 'bad.zip'
            archive.write_bytes(b'not the matching archive')
            with self.assertRaisesRegex(ValueError, 'Archive SHA256 mismatch'):
                restore.restore(archive, Path(tmp) / 'output')

    def test_traversal_entry_is_rejected(self):
        with tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
            archive = Path(tmp) / 'bad.zip'
            with zipfile.ZipFile(archive, 'w') as z:
                z.writestr('../outside.txt', b'no')
            manifest = {'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                        'files': [{'path': '../outside.txt', 'bytes': 2,
                                   'sha256': hashlib.sha256(b'no').hexdigest()}]}
            with patch.object(restore.json, 'loads', return_value=manifest):
                with self.assertRaisesRegex(ValueError, 'Unsafe asset path'):
                    restore.restore(archive, Path(tmp) / 'output')
            self.assertFalse((Path(tmp) / 'outside.txt').exists())

    def test_windows_separators_cannot_escape_package(self):
        for name in (restore.PREFIX + 'assets/..\\..\\..\\outside.txt',
                     restore.PREFIX + 'assets/unsafe:stream'):
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=TEST_TMP) as tmp:
                archive = Path(tmp) / 'bad.zip'
                with zipfile.ZipFile(archive, 'w') as z:
                    z.writestr(name, b'no')
                manifest = {'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                            'files': [{'path': name, 'bytes': 2,
                                       'sha256': hashlib.sha256(b'no').hexdigest()}]}
                with patch.object(restore.json, 'loads', return_value=manifest):
                    # Windows zipfile may normalize backslashes first; both
                    # the exact-entry guard and path guard must reject it.
                    with self.assertRaisesRegex(ValueError, 'Unsafe asset path|Archive file list mismatch'):
                        restore.restore(archive, Path(tmp) / 'output')
                self.assertEqual(list((Path(tmp) / 'output').rglob('*')), [])


if __name__ == '__main__':
    unittest.main()
