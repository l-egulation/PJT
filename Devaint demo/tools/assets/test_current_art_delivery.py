"""Keep currently loaded character/effect art restorable after asset cleanup."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / 'unity/Assets/Deviant/Combat/Resources'
CATALOGS = (
    'AnimationCatalog.json', 'HeavyWeaponCatalog.json', 'ApprovedRangedEffects.json',
    'ApprovedThrownProjectiles.json', 'ApprovedDashPresentation.json',
)
MEDIA_PREFIXES = (
    'CharacterApproved11/', 'CharacterApproved13/', 'EffectsApproved11/',
    'EffectsApproved13/', 'CharacterConfirmed/', 'CharacterHeavyV7/',
    'EffectsConfirmed/', 'EffectsHeavyV7/', 'CharacterV2/Sword/', 'CharacterV2/Whip/',
    'ApprovedTraversal/',
)


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for item in value.values():
            yield from strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)


class CurrentArtDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.delivered = set()
        data = json.loads((ROOT / 'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
        self.delivered.update(record['target'] for record in data['files'])

    def assert_delivered(self, path):
        name = path.relative_to(ROOT).as_posix()
        self.assertTrue(Path(str(path) + '.meta').is_file(), 'Missing Unity import metadata: ' + name)
        self.assertIn(name, self.delivered, 'Current runtime art has no delivery record: ' + name)

    def test_active_character_and_effect_catalogs_remain_restorable(self):
        for catalog in CATALOGS:
            data = json.loads((RESOURCES / 'CharacterV2' / catalog).read_text(encoding='utf-8-sig'))
            for value in set(strings(data)):
                if value.startswith(MEDIA_PREFIXES):
                    with self.subTest(catalog=catalog, resource=value):
                        self.assert_delivered(RESOURCES / (value + '.png'))

    def test_common_build_art_sentinels_remain_restorable(self):
        for name in ('unity/Assets/Deviant/UI/Sprites/hp_fill.png',
                     'unity/Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png'):
            with self.subTest(path=name):
                self.assert_delivered(ROOT / name)

    def test_deployment_has_unique_explicit_versions_and_no_gameplay_outputs(self):
        data = json.loads((ROOT / 'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
        names = [item['target'].casefold() for item in data['files']]
        self.assertEqual(len(names), len(set(names)))
        for item in data['files']:
            with self.subTest(target=item['target']):
                editors = {'tools/chapter1-map-editor/맵 편집기.html',
                           'tools/chapter1-map-editor/이전_지형용_편집기.html'}
                self.assertTrue(item['target'] in editors or item['target'].endswith(
                    ('.png', '.bytes', '.wav', '.ogg', '.mp3', '.flac', '.ttf', '.otf')))
                self.assertRegex(item['sha256'], r'^[0-9a-f]{64}$')
                self.assertTrue(item['source']['asset'])
                self.assertFalse(item['source']['path'].startswith(('/', '../')))

    def test_hub_bindings_are_covered_by_the_same_contract(self):
        path = ROOT / 'unity/Assets/Deviant/Hub/Data/hub-sprites.json'
        if not path.is_file():
            self.skipTest('Hub bindings are not present in this source revision')
        data = json.loads(path.read_text(encoding='utf-8'))
        source = json.loads((ROOT / data['source']).read_text(encoding='utf-8'))
        self.assertCountEqual([item['id'] for item in source['sprites']],
                              [item['id'] for item in data['sprites']])
        for item in data['sprites']:
            with self.subTest(sprite=item['id']):
                self.assertIn('unity/' + item['unity'], self.delivered)


if __name__ == '__main__':
    unittest.main(verbosity=2)
