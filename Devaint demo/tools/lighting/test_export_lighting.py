import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('export_lighting', Path(__file__).with_name('export_lighting.py'))
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


class LightingExportTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        root = Path(self.directory.name)
        self.source = root / 'source.json'
        self.target = root / 'unity' / 'Lab.json'

    def run_export(self, check=False):
        with patch.object(exporter, 'SOURCE', self.source), patch.object(exporter, 'TARGET', self.target), \
             patch('sys.argv', ['export_lighting.py'] + (['--check'] if check else [])), contextlib.redirect_stdout(io.StringIO()):
            exporter.main()

    def test_export_preserves_the_authoring_bytes(self):
        payload = b'{\n  "schema": 1, "profiles": []\n}\n'
        self.source.write_bytes(payload)
        self.run_export()
        self.assertEqual(self.target.read_bytes(), payload)
        self.run_export(check=True)

    def test_check_rejects_missing_or_changed_output_without_writing(self):
        self.source.write_text('{"schema": 1}\n')
        with self.assertRaises(SystemExit):
            self.run_export(check=True)
        self.assertFalse(self.target.exists())
        self.target.parent.mkdir()
        self.target.write_text('user content')
        with self.assertRaises(SystemExit):
            self.run_export(check=True)
        self.assertEqual(self.target.read_text(), 'user content')

    def test_invalid_json_does_not_overwrite_an_existing_target(self):
        self.source.write_text('{broken')
        self.target.parent.mkdir()
        self.target.write_text('previous valid version')
        with self.assertRaises(ValueError):
            self.run_export()
        self.assertEqual(self.target.read_text(), 'previous valid version')


if __name__ == '__main__':
    unittest.main()
