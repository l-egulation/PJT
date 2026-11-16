"""Exercise the tuner's fixed destinations, conflict handling and shared schema."""
import copy
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest import mock
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import export_gamepad_feedback as catalog
import gamepad_feedback_tuner as tuner


class GamepadFeedbackTunerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.source, self.target = self.root / "source.json", self.root / "unity/profile.json"
        self.original = json.loads(catalog.SOURCE.read_text(encoding="utf-8"))
        # Keep legacy fixtures valid after the real authoring profile adopts schema 2.
        self.original["schemaVersion"] = 1
        for name in catalog.PULSES:
            if "segments" in self.original[name]:
                first = self.original[name]["segments"][0]
                self.original[name] = {key: first[key] for key in ("low", "high", "duration")}
        self.source.write_text(json.dumps(self.original), encoding="utf-8")
        self.store = tuner.ProfileStore(self.source, self.target)

    def test_save_publishes_the_source_and_generated_catalog_without_touching_metadata(self):
        before = self.store.read()
        self.assertFalse(before["catalogMatches"])
        metadata = self.target.with_suffix(".json.meta")
        metadata.parent.mkdir(parents=True)
        metadata.write_text("preserve-guid", encoding="utf-8")
        edited = copy.deepcopy(self.original)
        edited["hit"]["low"] = .73
        result = self.store.save(edited, before["revision"])
        self.assertTrue(result["catalogMatches"])
        self.assertEqual(result["profile"], edited)
        self.assertEqual(self.source.read_bytes(), self.target.read_bytes())
        self.assertNotEqual(result["revision"], before["revision"])
        self.assertEqual(metadata.read_text(), "preserve-guid")
        self.assertEqual(list(self.root.rglob("*.tmp")), [])

    def test_catalog_match_accepts_platform_line_endings(self):
        self.target.parent.mkdir(parents=True)
        self.target.write_bytes(catalog.serialize(self.original).replace("\n", "\r\n").encode("utf-8"))
        self.assertTrue(self.store.read()["catalogMatches"])

    def test_external_edit_rejects_stale_save_and_preserves_both_files(self):
        before = self.store.read()
        self.source.write_text(json.dumps({**self.original, "kickFrequency": 40}), encoding="utf-8")
        external = self.source.read_bytes()
        with self.assertRaises(tuner.ConflictError):
            self.store.save(self.original, before["revision"])
        self.assertEqual(self.source.read_bytes(), external)
        self.assertFalse(self.target.exists())

    def test_external_edit_during_staging_is_not_replaced(self):
        before = self.store.read()
        original_stage = self.store._stage
        external = json.dumps({**self.original, "kickFrequency": 52}).encode()

        def stage(target, raw):
            result = original_stage(target, raw)
            if target == self.target:
                self.source.write_bytes(external)
            return result

        with mock.patch.object(self.store, "_stage", side_effect=stage):
            with self.assertRaises(tuner.ConflictError):
                self.store.save(self.original, before["revision"])
        self.assertEqual(self.source.read_bytes(), external)
        self.assertFalse(self.target.exists())
        self.assertEqual(list(self.root.rglob("*.tmp")), [])

    def test_failed_catalog_publish_restores_the_source_and_cleans_staging_files(self):
        before = self.store.read()
        original_bytes = self.source.read_bytes()
        real_replace = tuner.os.replace

        def replace(source, target):
            if target == self.target:
                raise OSError("synthetic catalog failure")
            return real_replace(source, target)

        edited = {**self.original, "kickFrequency": 80}
        with mock.patch.object(tuner.os, "replace", side_effect=replace):
            with self.assertRaises(OSError):
                self.store.save(edited, before["revision"])
        self.assertEqual(self.source.read_bytes(), original_bytes)
        self.assertFalse(self.target.exists())
        self.assertEqual(list(self.root.rglob("*.tmp")), [])

    def test_pattern_schema_round_trips_without_replacing_other_event_values(self):
        edited = copy.deepcopy(self.original)
        edited["schemaVersion"] = 2
        edited["hit"] = {"segments": [
            {"low": .7, "high": .2, "duration": .04, "curve": "hold"},
            {"low": 0, "high": 0, "duration": .03, "curve": "hold"},
            {"low": .2, "high": .6, "duration": .08, "curve": "rise"},
        ]}
        before = self.store.read()
        result = self.store.save(edited, before["revision"])
        self.assertEqual(result["profile"], edited)
        self.assertEqual(result["profile"]["basic"], self.original["basic"])
        self.assertTrue(result["catalogMatches"])
        self.assertEqual(json.loads(self.target.read_text()), edited)
        self.assertEqual(len(result["profile"]["hit"]["segments"]), 3)

    def test_pattern_limits_curves_and_old_schema_reject_invalid_segments(self):
        good = {"low": .3, "high": .5, "duration": .1, "curve": "decay"}
        invalid_segments = [[], [good] * 17, [{**good, "curve": "unknown"}],
                            [{**good, "duration": 0}], [{**good, "high": True}],
                            [{**good, "low": float("nan")}], [{**good, "duration": 1}] * 3,
                            [{**good, "extra": 1}], [{"duration": .1, "curve": "hold"}]]
        for segments in invalid_segments:
            document = {**self.original, "schemaVersion": 2, "hit": {"segments": segments}}
            with self.subTest(segments=segments), self.assertRaises(ValueError):
                catalog.validate(document)
        with self.assertRaises(ValueError):
            catalog.validate({**self.original, "hit": {"segments": [good]}})
        exact = {**self.original, "schemaVersion": 2, "hit": {"segments": [{**good, "duration": 1}] * 2}}
        self.assertIs(catalog.validate(exact), exact)
        mixed = {**self.original, "schemaVersion": 2, "hit": {**self.original["hit"], "segments": [good]}}
        with self.assertRaises(ValueError):
            catalog.validate(mixed)

    def test_zero_resistance_round_trips_without_changing_the_kick(self):
        profile = {**self.original, "readyStrength": 0, "chargingStrength": 0,
                   "kickStrength": 5, "kickFrequency": 29, "kickDuration": .15}
        result = self.store.save(profile, self.store.read()["revision"])
        self.assertEqual(result["profile"], profile)
        self.assertTrue(result["catalogMatches"])
        for key in ("readyStrength", "chargingStrength"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                catalog.validate({**profile, key: -1})
        with self.assertRaises(ValueError):
            catalog.validate({**profile, "kickStrength": 0})

    def test_shared_validation_rejects_nonfinite_boolean_zero_and_unknown_values(self):
        invalid = []
        for value in (float("nan"), float("inf"), True, -1, 1.01, 10 ** 350):
            data = copy.deepcopy(self.original)
            data["basic"]["low"] = value
            invalid.append(data)
        for path, value in (("schemaVersion", True), ("triggerStart", .5), ("kickDuration", 0), ("maximumEventAge", "0.2")):
            invalid.append({**self.original, path: value})
        invalid.append({**self.original, "typo": 1})
        invalid.append([])
        before = self.source.read_bytes()
        for document in invalid:
            with self.subTest(document=document), self.assertRaises(ValueError):
                self.store.save(document, self.store.read()["revision"])
        self.assertEqual(self.source.read_bytes(), before)
        self.assertFalse(self.target.exists())

    def test_http_rejects_cross_origin_and_invalid_profile_and_reports_conflicts(self):
        server = ThreadingHTTPServer(("127.0.0.1", 0), tuner.handler_for(self.store))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 3)
        self.addCleanup(server.shutdown)
        origin = f"http://127.0.0.1:{server.server_port}"
        with urlopen(origin + "/api/profile") as response:
            initial = json.load(response)
        with urlopen(origin + "/") as response:
            self.assertIn("피드백 스튜디오", response.read().decode())
        request = {"profile": self.original, "revision": initial["revision"]}

        def post(data, origin_header):
            return urlopen(Request(origin + "/api/profile", json.dumps(data).encode(),
                                   headers={"Content-Type": "application/json", "Origin": origin_header}))

        with self.assertRaises(HTTPError) as error:
            post(request, "https://example.com")
        self.assertEqual(error.exception.code, 403)
        with self.assertRaises(HTTPError) as error:
            post({**request, "profile": {**self.original, "kickDuration": 0}}, origin)
        self.assertEqual(error.exception.code, 400)
        with post(request, origin) as response:
            self.assertTrue(json.load(response)["catalogMatches"])
        with self.assertRaises(HTTPError) as error:
            post(request, origin)
        self.assertEqual(error.exception.code, 409)


if __name__ == "__main__":
    unittest.main()
