"""Preserve exact boss/editor payloads and generate review HTML without overwriting originals."""
import base64
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from assets import export, json_bytes, sha
from build_map_editor import build
from import_boss_editor_media import import_media, MAPPING, SELECTIONS


class BossEditorImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name) / 'main'
        self.root = Path(self.temp.name) / 'resources'
        self.repo.mkdir()
        self.root.mkdir()
        self.git('init', '-q')
        for name, _, _, _ in SELECTIONS:
            path = self.repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'\x89PNG\r\n\x1a\nsource' if name.endswith('.bytes') else b'<!doctype html><p>inline media source</p>\r\n')
            if name.startswith('unity/'):
                Path(str(path) + '.meta').write_bytes(b'guid: preserved\r\n')
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'originals')
        (self.root / 'catalog.json').write_bytes(json_bytes({
            'format': 'deviant-resource-catalog-v1', 'assets': [], 'importMapping': 'empty.json'}))
        (self.root / 'empty.json').write_bytes(json_bytes({'records': []}))
        (self.root / '.lfs-payloads.json').write_bytes(json_bytes([]))

    def git(self, *args):
        return subprocess.check_output(['git', '-C', str(self.repo), *args])

    def test_dry_run_repeated_import_and_empty_consumer_restore_preserve_exact_bytes(self):
        self.assertEqual(3, import_media(self.repo, root=self.root, check=True)['mediaFiles'])
        self.assertFalse((self.root / MAPPING).exists())
        import_media(self.repo, root=self.root)
        import_media(self.repo, root=self.root)
        mapping = json.loads((self.root / MAPPING).read_text())
        by_source = {name: asset_id for name, asset_id, _, _ in SELECTIONS}
        manifest = {'format': 'deviant-asset-deployment-v1', 'files': [
            {'target': r['path'], 'bytes': r['bytes'], 'sha256': r['sha256'],
             'source': {'asset': by_source[r['path']], 'path': r['target']}} for r in mapping['records']]}
        dest = Path(self.temp.name) / 'consumer'
        dest.mkdir()
        self.assertEqual(3, export(self.root, manifest, dest)['changed'])
        self.assertEqual(0, export(self.root, manifest, dest)['changed'])
        for r in mapping['records']:
            self.assertEqual((self.repo / r['path']).read_bytes(), (dest / r['path']).read_bytes())
        self.assertEqual(2, len(json.loads((self.root / '.lfs-payloads.json').read_text())))
        self.assertFalse(list(self.root.rglob('*.meta')))

    def test_user_source_metadata_and_resource_edits_are_never_overwritten(self):
        source = self.repo / SELECTIONS[0][0]
        for path in [source, Path(str(source) + '.meta')]:
            before = path.read_bytes()
            path.write_bytes(b'local edits')
            with self.assertRaisesRegex(ValueError, 'edits preserved'):
                import_media(self.repo, root=self.root)
            self.assertFalse((self.root / MAPPING).exists())
            self.assertEqual(b'local edits', path.read_bytes())
            path.write_bytes(before)
        _, _, folder, local = SELECTIONS[-1]
        target = self.root / folder / local
        target.parent.mkdir(parents=True)
        target.write_bytes(b'local resource edits')
        with self.assertRaisesRegex(ValueError, 'Refusing to overwrite'):
            import_media(self.repo, root=self.root)
        self.assertEqual(b'local resource edits', target.read_bytes())
        self.assertFalse((self.root / MAPPING).exists())


class MapEditorBuildTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        (self.source / 'data').mkdir(parents=True)
        (self.source / 'index.html').write_text('<script src="data/map.js"></script><a href="data/placements.json" download>map</a><a href="README.md" target="_blank">help</a><footer></footer>')
        (self.source / 'data/map.js').write_text('window.example="</script>";')
        (self.source / 'data/placements.json').write_text('{"markers":[]}')
        names = {'Terrain32.png': (256, 256), 'SolidPlatform.png': (512, 64),
                 'DropPlatform.png': (512, 64), 'forest_depth.png': (480, 270),
                 'forest_near.png': (480, 270), **{f'hero{i}.png': (16, 16) for i in range(4)}}
        self.inline = {f'assets/{n}': 'data:image/png;base64,' + base64.b64encode(
            b'\x89PNG\r\n\x1a\n' + b'\x00' * 8 + w.to_bytes(4, 'big') + h.to_bytes(4, 'big')).decode()
                       for n, (w, h) in names.items()}
        self.bundle = ('<script>window.INLINE_ASSETS=' + json.dumps(self.inline) + ';</script>').encode()

    def test_build_keeps_embedded_png_bytes_and_inlines_code_and_saved_json(self):
        out = self.root / 'review/editor.html'
        result = build(self.source, self.bundle, out)
        self.assertEqual(9, result['embeddedImages'])
        html = out.read_text()
        self.assertNotIn('<script src=', html)
        self.assertIn('<\\/script>', html)
        self.assertIn('data:application/json;base64,', html)
        for value in self.inline.values():
            self.assertIn(value, html)
        self.assertEqual('{"markers":[]}', (self.source / 'data/placements.json').read_text())

    def test_existing_reviews_source_directory_and_invalid_inputs_are_rejected(self):
        out = self.root / 'review/editor.html'
        out.parent.mkdir()
        out.write_text('user review')
        with self.assertRaisesRegex(ValueError, 'Preserve existing review'):
            build(self.source, self.bundle, out)
        self.assertEqual('user review', out.read_text())
        with self.assertRaisesRegex(ValueError, 'outside the editor source'):
            build(self.source, self.bundle, self.source / 'editor.html')
        with self.assertRaises(ValueError):
            build(self.source, b'no media', self.root / 'invalid/editor.html')
        self.assertFalse((self.root / 'invalid').exists())


if __name__ == '__main__':
    unittest.main()
