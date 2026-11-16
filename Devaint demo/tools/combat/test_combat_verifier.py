"""Guard tests with a synthetic editor process, not a substitute for actual Unity compilation."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
PWSH = os.environ.get('PWSH') or shutil.which('pwsh')


@unittest.skipUnless(PWSH and os.name == 'posix', 'Set PWSH to run PowerShell/editor-launch guard tests')
class CombatVerifierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix='combat-verifier-')
        cls.addClassCleanup(cls.temp.cleanup)
        cls.folder = Path(cls.temp.name)
        cls.project = cls.folder / 'isolated project'
        for name in ('ProjectSettings/ProjectVersion.txt', 'Packages/manifest.json', 'Packages/packages-lock.json'):
            dest = cls.project / name
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / 'unity' / name, dest)
        for source in (ROOT / 'unity/Assets').rglob('*'):
            if source.is_file() and source.suffix in {'.cs', '.asmdef'}:
                dest = cls.project / source.relative_to(ROOT / 'unity')
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, dest)
        cls.editor = cls.folder / 'synthetic-editor'
        cls.editor.write_text('''#!/usr/bin/env python3
import os,sys
from pathlib import Path
if '-version' in sys.argv:
    print('6000.6.5f1')
else:
    log=Path(sys.argv[sys.argv.index('-logFile')+1])
    targets=['StandaloneLinux64','StandaloneWindows64'] if os.environ.get('SYNTHETIC_EDITOR_MODE')=='both' else ['StandaloneLinux64']
    log.write_text(''.join('HIT_FEEDBACK_PLAYER_SCRIPTS_OK target='+t+' unity=6000.6.5f1 assemblies=1\\n' for t in targets))
''')
        cls.editor.chmod(0o755)

    def invoke(self, *args, mode='both'):
        return subprocess.run([PWSH, '-NoProfile', '-File', str(ROOT / 'tools/combat/verify_combat_v2.ps1'),
                               *map(str, args)], text=True, capture_output=True,
                              env={**os.environ, 'SYNTHETIC_EDITOR_MODE': mode}, timeout=60)

    def test_portable_rules_do_not_claim_unity_compilation(self):
        result = self.invoke('-RulesOnly')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('PASS: 30 portable', result.stdout)
        self.assertIn('SKIP: Unity', result.stdout)
        self.assertNotIn('PASS: actual Unity', result.stdout)

    def test_main_checkout_and_changed_packages_are_rejected(self):
        result = self.invoke('-UnityEditor', self.editor, '-UnityProject', ROOT / 'unity')
        self.assertNotEqual(0, result.returncode)
        self.assertIn('isolated copy', result.stderr)
        manifest = self.project / 'Packages/manifest.json'
        old = manifest.read_bytes()
        try:
            manifest.write_bytes(b'{}')
            result = self.invoke('-UnityEditor', self.editor, '-UnityProject', self.project)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('version/packages differ', result.stderr)
        finally:
            manifest.write_bytes(old)

    def test_both_compiler_markers_are_required_and_existing_logs_are_preserved(self):
        log = self.folder / 'synthetic-failed.log'
        result = self.invoke('-UnityEditor', self.editor, '-UnityProject', self.project, '-LogFile', log, mode='linux-only')
        self.assertNotEqual(0, result.returncode)
        self.assertIn('did not confirm StandaloneWindows64', result.stderr)
        original = log.read_bytes()
        result = self.invoke('-UnityEditor', self.editor, '-UnityProject', self.project, '-LogFile', log)
        self.assertNotEqual(0, result.returncode)
        self.assertIn('Preserve the existing validation log', result.stderr)
        self.assertEqual(original, log.read_bytes())

    def test_complete_synthetic_editor_contract_accepts_paths_with_spaces(self):
        result = self.invoke('-UnityEditor', self.editor, '-UnityProject', self.project,
                             '-LogFile', self.folder / 'synthetic complete.log')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('PASS: actual Unity', result.stdout)
        self.assertIn('NOT RUN: scene/PlayMode', result.stdout)


if __name__ == '__main__':
    unittest.main()
