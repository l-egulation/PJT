"""The common exporter must be deterministic, offline and protective of local work."""
import base64
import copy
import json
from pathlib import Path
import tempfile
import unittest

from assets import Store, export, json_bytes, sha


class AssetExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'resources'
        self.dest = Path(self.temp.name) / 'main'
        self.root.mkdir()
        self.dest.mkdir()
        self.data = b'original source image bytes'
        self.source = 'ui/example/runtime/icon.png'
        self.write(self.source, self.data)
        item = {'package': None, 'path': 'original.png', 'target': self.source,
                'sha256': sha(self.data), 'bytes': len(self.data)}
        self.write('mapping.json', json_bytes({'records': [item]}))
        self.write('catalog.json', json_bytes({'format': 'deviant-resource-catalog-v1',
                   'importMapping': 'mapping.json', 'assets': [
                       {'id': 'ui.example', 'path': 'ui/example', 'fileCount': 1}]}))
        self.write('ui/example/asset.json', json_bytes({'id': 'ui.example', 'files': ['runtime/icon.png']}))
        self.target = 'unity/Assets/Example/icon.png'
        self.item = {'target': self.target, 'source': {'asset': 'ui.example', 'path': self.source},
                     'sha256': sha(self.data), 'bytes': len(self.data)}
        self.manifest = {'format': 'deviant-asset-deployment-v1', 'files': [self.item]}

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def output(self, data):
        path = self.dest / self.target
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def test_check_is_read_only(self):
        self.assertEqual({'verified': 1, 'changed': 1, 'checkOnly': True},
                         export(self.root, self.manifest, self.dest, check=True))
        self.assertEqual([], list(self.dest.iterdir()))

    def test_native_bytes_and_metadata_are_preserved_and_reapply_is_idempotent(self):
        meta = self.dest / (self.target + '.meta')
        meta.parent.mkdir(parents=True)
        meta.write_bytes(b'game-owned GUID and pivot\r\n')
        self.assertEqual(1, export(self.root, self.manifest, self.dest)['changed'])
        self.assertEqual(self.data, meta.with_suffix('').read_bytes())
        self.assertEqual(b'game-owned GUID and pivot\r\n', meta.read_bytes())
        self.assertEqual(0, export(self.root, self.manifest, self.dest)['changed'])

    def test_local_edit_is_rejected_before_other_files_are_written(self):
        self.output(b'user art edits')
        self.manifest['files'].insert(0, {**self.item, 'target': 'unity/Assets/Example/new.png'})
        with self.assertRaisesRegex(ValueError, 'Local edits preserved'):
            export(self.root, self.manifest, self.dest)
        self.assertFalse((self.dest / 'unity/Assets/Example/new.png').exists())
        self.assertEqual(b'user art edits', (self.dest / self.target).read_bytes())

    def test_user_edit_after_a_successful_apply_is_preserved(self):
        export(self.root, self.manifest, self.dest)
        self.output(b'user art edits')
        with self.assertRaisesRegex(ValueError, 'Local edits preserved'):
            export(self.root, self.manifest, self.dest)

    def test_declared_previous_release_is_backed_up(self):
        old = b'previous approved image'
        self.output(old)
        self.item['replaceHashes'] = [sha(old)]
        export(self.root, self.manifest, self.dest)
        self.assertEqual(old, (self.dest / 'outputs/assets/backups' / sha(old)).read_bytes())

    def test_missing_lfs_and_corrupt_source_fail_without_changes(self):
        for bad in (b'version https://git-lfs.github.com/spec/v1\n', b'corrupt source'):
            with self.subTest(bad=bad):
                self.write(self.source, bad)
                with self.assertRaisesRegex(ValueError, 'LFS object missing|Resource hash mismatch'):
                    export(self.root, self.manifest, self.dest)
                self.assertEqual([], list(self.dest.iterdir()))

    def test_consumer_cannot_select_an_unknown_owner_or_resource(self):
        for source in ({'asset': 'ui.other', 'path': self.source},
                       {'asset': 'ui.example', 'path': 'unknown.png'}):
            self.item['source'] = source
            with self.assertRaisesRegex(ValueError, 'Unindexed resource selection'):
                export(self.root, self.manifest, self.dest)

    def test_consumer_hash_is_a_version_guard(self):
        self.item['sha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'differs from consumer contract'):
            export(self.root, self.manifest, self.dest)

    def test_unsafe_and_duplicate_targets_are_rejected(self):
        for target in ('../outside.png', '/absolute.png', 'unity/../outside.png', 'C:/outside.png'):
            with self.subTest(target=target):
                self.item['target'] = target
                with self.assertRaises(ValueError):
                    export(self.root, self.manifest, self.dest)
        self.item['target'] = self.target
        self.manifest['files'].append({**self.item, 'target': self.target.upper()})
        with self.assertRaisesRegex(ValueError, 'Duplicate asset destination'):
            export(self.root, self.manifest, self.dest)

    def test_source_and_destination_symlinks_are_rejected(self):
        source = self.root / self.source
        source.unlink()
        outside = Path(self.temp.name) / 'outside.png'
        outside.write_bytes(self.data)
        source.symlink_to(outside)
        with self.assertRaises(ValueError):
            export(self.root, self.manifest, self.dest)
        source.unlink()
        source.write_bytes(self.data)
        target = self.dest / self.target
        target.parent.mkdir(parents=True)
        target.symlink_to(outside)
        with self.assertRaises(ValueError):
            export(self.root, self.manifest, self.dest)

    def test_embedded_png_is_extracted_without_reencoding(self):
        payload = json_bytes({'images': {'a/b~': 'data:image/png;base64,' + base64.b64encode(self.data).decode()}})
        self.write(self.source, payload)
        self.write('mapping.json', json_bytes({'records': [{'package': None, 'path': 'original.json',
                   'target': self.source, 'sha256': sha(payload), 'bytes': len(payload)}]}))
        self.item['source']['pointer'] = '/images/a~1b~0'
        export(self.root, self.manifest, self.dest)
        self.assertEqual(self.data, (self.dest / self.target).read_bytes())

    def test_reused_state_from_another_checkout_is_rejected(self):
        state = self.dest / 'outputs/assets/state.json'
        state.parent.mkdir(parents=True)
        state.write_bytes(json_bytes({'destination': '/another-checkout', 'files': {}}))
        with self.assertRaisesRegex(ValueError, 'another checkout'):
            export(self.root, self.manifest, self.dest)

    def test_state_directory_symlink_and_corrupt_backup_are_rejected(self):
        outside = Path(self.temp.name) / 'outside-state'
        outside.mkdir()
        (self.dest / 'outputs').mkdir()
        state = self.dest / 'outputs/assets'
        state.symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError):
            export(self.root, self.manifest, self.dest)
        self.assertEqual([], list(outside.iterdir()))
        state.unlink()
        old = b'previous approved image'
        self.output(old)
        self.item['replaceHashes'] = [sha(old)]
        backup = state / 'backups' / sha(old)
        backup.parent.mkdir(parents=True)
        backup.write_bytes(b'unknown user backup')
        with self.assertRaisesRegex(ValueError, 'backup differs'):
            export(self.root, self.manifest, self.dest)
        self.assertEqual(old, (self.dest / self.target).read_bytes())
        self.assertEqual(b'unknown user backup', backup.read_bytes())


if __name__ == '__main__':
    unittest.main()
