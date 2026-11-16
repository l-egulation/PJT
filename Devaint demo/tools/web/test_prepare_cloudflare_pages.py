"""Check deployment URLs and both capability branches without launching gameplay."""
import gzip
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

TOOL = Path(__file__).with_name('prepare_cloudflare_pages.py')


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for variant in ('bc7', 'dxt'):
            source = self.root / variant
            build = source / 'Build'
            build.mkdir(parents=True)
            for extension in ('data.unityweb', 'framework.js.unityweb', 'wasm.unityweb'):
                (build / ('game.' + extension)).write_bytes(gzip.compress(variant.encode(), mtime=0))
            (build / 'game.loader.js').write_text('// fixture')
            (source / 'index.html').write_text('''<script>
var buildUrl = "Build";
var loaderUrl = buildUrl + "/game.loader.js";
var config = {dataUrl: buildUrl + "/game.data.unityweb", frameworkUrl: buildUrl + "/game.framework.js.unityweb", codeUrl: buildUrl + "/game.wasm.unityweb", streamingAssetsUrl: "StreamingAssets"};
var script = document.createElement("script");
</script>''')

    def run_tool(self, dual=False):
        args = ['python3', str(TOOL), '--build', str(self.root / ('bc7' if dual else 'dxt')), '--output', str(self.root / 'staged'),
                '--assets-base-url', 'https://assets.example.test']
        if dual:
            args += ['--dxt-build', str(self.root / 'dxt')]
        return subprocess.run(args, text=True, capture_output=True)

    def test_single_build_preserves_upload_contract_and_excludes_data(self):
        result = self.run_tool()
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = json.loads(result.stdout)
        sha = hashlib.sha256((self.root / 'dxt/Build/game.data.unityweb').read_bytes()).hexdigest()
        self.assertEqual(manifest['object_key'], f'releases/{sha}/game.data.unityweb')
        self.assertNotIn('variants', manifest)
        self.assertFalse((self.root / 'staged/pages/Build/game.data.unityweb').exists())
        self.assertIn(manifest['data_url'], (self.root / 'staged/pages/index.html').read_text())

    def test_dual_build_selects_a_consistent_set_of_files(self):
        result = self.run_tool(dual=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = json.loads(result.stdout)
        self.assertEqual(len(manifest['variants']), 2)
        pages = self.root / 'staged/pages'
        self.assertEqual(list(pages.rglob('*.data.unityweb')), [])
        headers = (pages / '_headers').read_text()
        self.assertIn('/DxtBuild/game.wasm.unityweb', headers)
        source = re.search(r'<script>(.*)</script>', (pages / 'index.html').read_text(), re.S).group(1)
        for supported, variant, platform in [(False, 'dxt', 'Windows'), (True, 'bc7', 'Windows'), (True, 'dxt', 'Macintosh')]:
            js = '''const navigator = {userAgent: PLATFORM}; const document = {createElement: () => ({getContext: () => ({getExtension: name => name === 'EXT_texture_compression_bptc' ? SUPPORTED : {loseContext(){}}})})};
SOURCE
process.stdout.write(JSON.stringify({loaderUrl, config}));'''.replace('SUPPORTED', json.dumps(supported)).replace('PLATFORM', json.dumps(platform)).replace('SOURCE', source)
            checked = subprocess.run(['node', '-e', js], text=True, capture_output=True, check=True)
            state = json.loads(checked.stdout)
            selected = next(item for item in manifest['variants'] if item['variant'] == variant)
            self.assertEqual(state['config']['dataUrl'], selected['data_url'])
            prefix = 'Build' if variant == 'bc7' else 'DxtBuild'
            self.assertEqual(state['loaderUrl'], prefix + '/game.loader.js')
            self.assertEqual(state['config']['codeUrl'], prefix + '/game.wasm.unityweb')
            self.assertEqual(state['config']['frameworkUrl'], prefix + '/game.framework.js.unityweb')
            self.assertEqual(state['config']['streamingAssetsUrl'], 'StreamingAssets')

    def test_bad_secondary_build_is_rejected_before_staging(self):
        (self.root / 'dxt/Build/game.wasm.unityweb').write_bytes(b'not gzip')
        result = self.run_tool(dual=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.root / 'staged').exists())


if __name__ == '__main__':
    unittest.main()
