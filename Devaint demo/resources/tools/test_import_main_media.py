"""Import immutable committed media without moving GUIDs or losing local edits."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from assets import export, json_bytes, sha
from import_main_media import import_media, MAPPING


class MainMediaImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name) / 'main'
        self.root = Path(self.temp.name) / 'resources'
        self.repo.mkdir()
        self.root.mkdir()
        self.git('init', '-q')
        self.names = [
            'unity/Assets/Deviant/Audio/SFX/example.wav',
            'unity/Assets/Deviant/UI/Fonts/example.ttf',
            'unity/Assets/ThirdParty/Wallcoeur/Textures/example.png']
        for i, name in enumerate(self.names):
            path = self.repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'original media\x00' + bytes([i]))
            Path(str(path) + '.meta').write_bytes(b'GUID and importer\r\n' + bytes([i]))
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                 'commit', '-qm', 'source media')
        (self.root / 'empty.json').write_bytes(json_bytes({'records': []}))
        (self.root / 'catalog.json').write_bytes(json_bytes({
            'format': 'deviant-resource-catalog-v1', 'assets': [], 'importMapping': 'empty.json'}))

    def git(self, *args):
        return subprocess.check_output(['git', '-C', str(self.repo), *args])

    def test_dry_run_and_repeated_import_preserve_source_and_metadata(self):
        before = {p: p.read_bytes() for p in (self.repo / 'unity').rglob('*') if p.is_file()}
        self.assertEqual(3, import_media(self.repo, root=self.root, check=True)['mediaFiles'])
        self.assertFalse((self.root / MAPPING).exists())
        import_media(self.repo, root=self.root)
        import_media(self.repo, root=self.root)
        self.assertEqual(before, {p: p.read_bytes() for p in before})
        records = json.loads((self.root / MAPPING).read_text())['records']
        self.assertEqual(3, len(records))
        for record in records:
            self.assertEqual(before[self.repo / record['path']], (self.root / record['target']).read_bytes())
            self.assertEqual(sha(before[self.repo / (record['path'] + '.meta')]), record['unityMetaSha256'])
        self.assertFalse(list(self.root.rglob('*.meta')))

    def test_modified_source_or_metadata_is_rejected_before_any_write(self):
        for suffix in ('', '.meta'):
            path = self.repo / (self.names[0] + suffix)
            old = path.read_bytes()
            path.write_bytes(b'local user edits')
            with self.subTest(suffix=suffix), self.assertRaisesRegex(ValueError, 'edits preserved'):
                import_media(self.repo, root=self.root)
            self.assertFalse((self.root / MAPPING).exists())
            self.assertEqual(b'local user edits', path.read_bytes())
            path.write_bytes(old)

    def test_resource_conflict_is_rejected_before_any_write(self):
        target = self.root / 'fonts/game/runtime/Deviant/UI/Fonts/example.ttf'
        target.parent.mkdir(parents=True)
        target.write_bytes(b'local resource edits')
        with self.assertRaisesRegex(ValueError, 'Refusing to overwrite'):
            import_media(self.repo, root=self.root)
        self.assertEqual(b'local resource edits', target.read_bytes())
        self.assertFalse((self.root / MAPPING).exists())
        self.assertFalse((self.root / 'audio').exists())

    def test_imported_media_restores_into_empty_consumer_without_touching_metadata(self):
        import_media(self.repo, root=self.root)
        mapping = json.loads((self.root / MAPPING).read_text())
        catalog = json.loads((self.root / 'catalog.json').read_text())
        owners = {a['path']: a['id'] for a in catalog['assets']}
        manifest = {'format': 'deviant-asset-deployment-v1', 'files': []}
        for r in mapping['records']:
            folder = r['target'].split('/runtime/')[0]
            manifest['files'].append({'target': r['path'], 'bytes': r['bytes'], 'sha256': r['sha256'],
                                     'source': {'asset': owners[folder], 'path': r['target']}})
        dest = Path(self.temp.name) / 'consumer'
        dest.mkdir()
        for name in self.names:
            meta = dest / (name + '.meta')
            meta.parent.mkdir(parents=True, exist_ok=True)
            meta.write_bytes((self.repo / (name + '.meta')).read_bytes())
        self.assertEqual(3, export(self.root, manifest, dest)['changed'])
        self.assertEqual(0, export(self.root, manifest, dest)['changed'])
        for name in self.names:
            self.assertEqual((self.repo / name).read_bytes(), (dest / name).read_bytes())
            self.assertEqual((self.repo / (name + '.meta')).read_bytes(), (dest / (name + '.meta')).read_bytes())


if __name__ == '__main__':
    unittest.main()
