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
            self.assertEqual(restore.restore(ARCHIVE, root), sum(r['path'] not in restore.RETIRED for r in json.loads((restore.HERE / '에셋목록.json').read_text(encoding='utf8'))['files']))
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


class CommonRuntimeRestoreTests(unittest.TestCase):
    def test_retired_game_is_verified_but_not_restored(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            archive = root / 'assets.zip'
            old_game = next(iter(restore.RETIRED))
            art = restore.PREFIX + 'test-art.png'
            entries = {old_game: b'old embedded combat', art: b'art'}
            with zipfile.ZipFile(archive, 'w') as bundle:
                for name, data in entries.items():
                    bundle.writestr(name, data)
            manifest = {
                'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                'files': [{'path': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
                          for name, data in entries.items()]}
            with patch.object(restore.json, 'loads', return_value=manifest):
                self.assertEqual(restore.restore(archive, root), 1)
                self.assertFalse((root / old_game).exists())
                self.assertEqual((root / art).read_bytes(), b'art')
                manifest['files'][0]['sha256'] = 'invalid'
                with self.assertRaisesRegex(ValueError, 'Asset SHA256 mismatch'):
                    restore.restore(archive, root)


if __name__ == '__main__':
    unittest.main()
