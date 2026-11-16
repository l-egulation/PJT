import copy
import json
from pathlib import Path
import tempfile
import unittest

import special_frame_transport as transport

ROOT = Path(__file__).resolve().parents[2]


class SpecialFrameTransportTests(unittest.TestCase):
    def test_default_source_carries_all_declared_records_without_compiling_policy(self):
        source = transport.load(ROOT)
        self.assertEqual(len(source['actions']), 23)
        self.assertEqual({record['mode'] for record in source['actions']}, {0})
        self.assertTrue(all(not record.get('timeline') for record in source['actions']))
        catalog = {'sets': [{'form': 'fixture', 'clips': ['unchanged']}], 'motionRegistry': {'keep': True}, 'meleeFrames': {'keep': True}}
        original = copy.deepcopy(catalog)
        result = transport.apply(catalog, source)
        self.assertEqual(catalog, original)
        self.assertEqual(result['specialFrames'], source)
        self.assertEqual(result['sets'], original['sets'])
        self.assertEqual(result['meleeFrames'], original['meleeFrames'])
        self.assertEqual(result['motionRegistry'], original['motionRegistry'])
        result['specialFrames']['actions'][0]['actionId'] = 'changed'
        self.assertNotEqual(source['actions'][0]['actionId'], 'changed')

    def test_transport_keeps_exact_user_timeline_including_semantic_invalidity_for_csharp(self):
        source = transport.load(ROOT)
        record = source['actions'][0]
        record.update(mode=1, policyVersion='policy', mediaBindingVersion='media', timeline={
            'frames': [{'id': 'known-step', 'seconds': .125}],
            'markers': [{'id': 'cycle', 'anchor': 3, 'reference': 'cycle', 'offset': 0}]})
        original = copy.deepcopy(source)
        result = transport.apply({}, source)
        self.assertEqual(source, original)
        self.assertEqual(result['specialFrames']['actions'][0]['timeline'], record['timeline'])
        self.assertEqual(result['specialFrames']['actions'][0]['timeline']['markers'][0]['reference'], 'cycle')
        # Only the shared C# FrameTimeline/ActionSpecialFrameCompiler reject this
        # cycle or a wrong policy ID. Python must not invent a second resolver.

    def test_source_transport_rejects_shape_type_and_size_without_overwriting(self):
        source = transport.load(ROOT)
        for mutation in (lambda value: value.update(schemaVersion=2), lambda value: value.update(actions=[]),
                         lambda value: value['actions'][0].update(kind=True), lambda value: value['actions'][0].update(timeline=[]),
                         lambda value: value['actions'][0].update(new_clock='forbidden')):
            invalid = copy.deepcopy(source)
            mutation(invalid)
            with self.assertRaises(ValueError): transport.apply({}, invalid)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / transport.SOURCE
            path.parent.mkdir(parents=True)
            path.write_bytes(b' ' * (transport.MAXIMUM_SOURCE_BYTES + 1))
            with self.assertRaises(ValueError): transport.load(root)
            path.write_text(json.dumps(source), encoding='utf-8')
            self.assertEqual(transport.load(root), source)


if __name__ == '__main__':
    unittest.main()
