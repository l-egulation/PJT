"""Every migrated media payload must remain reconstructible at its original Unity GUID."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class MediaDeliveryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mapping = json.loads((ROOT / 'resources/imports/main-media/mapping.json').read_text())
        contract = json.loads((ROOT / 'docs/assets/manifests/unity-deployment.json').read_text())
        cls.delivery = {item['target']: item for item in contract['files']}

    def test_imported_paths_have_exact_delivery_versions_and_unchanged_metadata(self):
        counts = Counter()
        for record in self.mapping['records']:
            name = record['originalProjectPath']
            with self.subTest(path=name):
                counts[Path(name).suffix] += 1
                item = self.delivery[name]
                self.assertEqual(record['target'], item['source']['path'])
                self.assertEqual(record['sha256'], item['sha256'])
                self.assertEqual(record['bytes'], item['bytes'])
                self.assertEqual(record['sha256'], digest(ROOT / 'resources' / record['target']))
                self.assertEqual(record['sha256'], digest(ROOT / name))
                self.assertEqual(record['unityMetaSha256'], digest(ROOT / (name + '.meta')))
        self.assertEqual({'.wav': 170, '.ttf': 6, '.png': 43}, dict(counts))

    def test_runtime_audio_catalog_and_editor_source_are_restorable(self):
        folder = 'unity/Assets/Deviant/Audio/Resources/'
        catalog = json.loads((ROOT / folder / 'ApprovedCombatAudio/Catalog.json').read_text())
        for entry in catalog['entries']:
            name = folder + entry['clip'] + '.wav'
            with self.subTest(clip=entry['id']):
                self.assertEqual(entry['sha256'], self.delivery[name]['sha256'])
        name = folder + 'ApprovedCombatAudio/Source/basic_sword_selected_07_v01.wav'
        recipe = json.loads((ROOT / 'data/audio/approved-weapon-project.json').read_text())
        self.assertEqual(recipe['source_hash'], self.delivery[name]['sha256'])

    def test_cleanup_inventory_records_preservation_without_authorizing_deletion(self):
        plan = json.loads((ROOT / 'data/maintenance/git-object-cleanup.json').read_text())
        proofs = {record['sha256']: record for record in self.mapping['records']}
        matched = 0
        for item in plan['objects']:
            if item['family'] not in {'current-audio', 'current-fonts', 'licensed-vfx'}:
                continue
            with self.subTest(oid=item['oid']):
                proof = proofs[item['sha256']]
                self.assertEqual(proof['bytes'], item['originalBytes'])
                self.assertEqual(proof['sha256'], item['resourceProof']['sha256'])
                self.assertIn(item['resourceProof']['path'],
                              [record['target'] for record in self.mapping['records']
                               if record['sha256'] == item['sha256']])
                self.assertFalse(item['objectRemovalAllowedNow'])
                matched += 1
        self.assertEqual(204, matched)


class BossEditorDeliveryTests(unittest.TestCase):
    def test_boss_and_both_embedded_editors_keep_exact_versions_and_original_metadata(self):
        mapping = json.loads((ROOT / 'resources/imports/main-boss-map-editor/mapping.json').read_text())
        manifest = json.loads((ROOT / 'docs/assets/manifests/unity-deployment.json').read_text())
        delivery = {record['target']: record for record in manifest['files']}
        payloads = json.loads((ROOT / 'resources/.lfs-payloads.json').read_text())
        self.assertEqual(3, len(mapping['records']))
        for record in mapping['records']:
            name = record['originalProjectPath']
            with self.subTest(path=name):
                item = delivery[name]
                self.assertEqual(record['sha256'], item['sha256'])
                self.assertEqual(record['target'], item['source']['path'])
                self.assertEqual(record['sha256'], digest(ROOT / 'resources' / record['target']))
                self.assertEqual(record['sha256'], digest(ROOT / name))
                if name.startswith('unity/'):
                    self.assertEqual(record['unityMetaSha256'], digest(ROOT / (name + '.meta')))
                    self.assertTrue((ROOT / name).read_bytes().startswith(b'\x89PNG\r\n\x1a\n'))
                else:
                    self.assertIn(record['target'], payloads)
                    html = (ROOT / name).read_text(encoding='utf-8')
                    inline = json.loads(re.search(r'window\.INLINE_ASSETS=(\{[^<]+\});</script>', html)[1])
                    self.assertEqual(9, len(inline))
                    self.assertTrue(all(value.startswith('data:image/png;base64,') for value in inline.values()))
                    self.assertNotIn('<script src=', html)

    def test_all_207_current_media_blobs_have_preservation_proofs_without_removal_approval(self):
        mapping = json.loads((ROOT / 'resources/imports/main-boss-map-editor/mapping.json').read_text())
        records = {record['sha256']: record for record in mapping['records']}
        plan = json.loads((ROOT / 'data/maintenance/git-object-cleanup.json').read_text())
        matched = 0
        for obj in plan['objects']:
            if obj['family'] not in {'current-native-media', 'current-embedded-tool'}:
                continue
            with self.subTest(oid=obj['oid']):
                record = records[obj['sha256']]
                self.assertEqual(record['target'], obj['resourceProof']['path'])
                self.assertEqual(record['bytes'], obj['resourceProof']['bytes'])
                self.assertEqual('registered-pushed-remotely-restored', obj['resourceDestinationStatus'])
                self.assertFalse(obj['objectRemovalAllowedNow'])
                matched += 1
        self.assertEqual(3, matched)
        self.assertEqual(207, plan['currentBossEditorPreservation']['allCurrentMediaUniquePayloads'])
        self.assertFalse(plan['executionGates']['automaticDeletionAllowed'])


if __name__ == '__main__':
    unittest.main()
