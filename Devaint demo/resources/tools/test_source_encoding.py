"""Cross-platform source identities must not be changed to hide a hash mismatch."""
import json
import unittest

from assets import ROOT, sha
from restore_source_encodings import SOURCE, TARGET, EXPECTED


class SourceEncodingTests(unittest.TestCase):
    def test_boss_binding_uses_exact_original_crlf_bytes_with_identical_json_meaning(self):
        raw = (ROOT / SOURCE).read_bytes()
        restored = (ROOT / TARGET).read_bytes()
        self.assertEqual(EXPECTED, sha(restored))
        self.assertEqual(raw.replace(b'\n', b'\r\n'), restored)
        self.assertEqual(json.loads(raw), json.loads(restored))


if __name__ == '__main__':
    unittest.main()
