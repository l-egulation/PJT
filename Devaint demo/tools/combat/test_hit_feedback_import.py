"""Synthetic-package tests; never bundles or downloads the licensed artwork."""
import hashlib
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("hit_feedback_import", Path(__file__).with_name("import_hit_feedback.py"))
IMPORTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORTER)


class RestoreTests(unittest.TestCase):
    def setUp(self):
        output = Path(__file__).resolve().parents[2] / "outputs/hit-feedback"
        output.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=output, prefix="import-test-")
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def package(self, rows):
        archive = self.root / "fixture.unitypackage"
        with tarfile.open(archive, "w:gz") as tar:
            for index, (name, content) in enumerate(rows):
                for entry, data in [("pathname", name.encode() + b"\n00"), ("asset", content), ("asset.meta", b"guid: fixture\n")]:
                    item = tarfile.TarInfo(f"{index:032x}/{entry}")
                    item.size = len(data)
                    tar.addfile(item, io.BytesIO(data))
        return archive

    def restore(self, archive):
        with patch.object(IMPORTER, "PACKAGE_SHA256", hashlib.sha256(archive.read_bytes()).hexdigest()):
            return IMPORTER.restore(archive, self.root / "project")

    def test_restore_is_exact_repeatable_and_preserves_local_edits(self):
        archive = self.package([("Assets/CartoonVFX by Wallcoeur/FX.prefab", b"original\n")])
        self.assertEqual(self.restore(archive), 2)
        self.assertEqual(self.restore(archive), 2)
        target = self.root / "project/Assets/ThirdParty/Wallcoeur/FX.prefab"
        self.assertEqual(target.read_bytes(), b"original\n")
        target.write_bytes(b"user edit")
        with self.assertRaisesRegex(ValueError, "differs"):
            self.restore(archive)
        self.assertEqual(target.read_bytes(), b"user edit")

    def test_foreign_and_traversal_paths_are_rejected_before_writes(self):
        for path in ["Assets/../outside.txt", "Assets/Other/FX.prefab", "/absolute/FX.prefab"]:
            with self.subTest(path=path):
                archive = self.package([("Assets/CartoonVFX by Wallcoeur/valid.prefab", b"valid"), (path, b"bad")])
                with self.assertRaises(ValueError):
                    self.restore(archive)
                self.assertFalse((self.root / "project").exists())

    def test_wrong_archive_version_is_rejected(self):
        archive = self.package([("Assets/CartoonVFX by Wallcoeur/FX.prefab", b"original")])
        with self.assertRaisesRegex(ValueError, "hash differs"):
            IMPORTER.restore(archive, self.root / "project")


if __name__ == "__main__":
    unittest.main()
