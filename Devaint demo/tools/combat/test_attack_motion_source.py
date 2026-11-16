import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import export_attack_motion as motion


class AttackMotionSourceTests(unittest.TestCase):
    def setUp(self):
        self.document = motion.load()
        self.catalog = json.loads((motion.ROOT / motion.CATALOG).read_text(encoding='utf-8'))

    def test_effect_placement_survives_art_rebuild_without_changing_other_hits(self):
        original = copy.deepcopy(self.catalog)
        first = original['sets'][0]['effects'][0]
        first.update(scale=1, x=[0]*len(first['frames']), y=[0]*len(first['frames']))
        result = motion.apply_effects(original)
        self.assertEqual(result, self.catalog)
        self.assertEqual(first['scale'], 1, 'Projection must not mutate the input binding')

    def test_invalid_effect_placement_cannot_publish_a_partial_catalog(self):
        source = json.loads((motion.ROOT / motion.EFFECTS).read_text(encoding='utf-8'))
        for mutation in (lambda p: p.update(scale=0), lambda p: p.update(scale=float('nan')),
                         lambda p: p.update(x=[]), lambda p: p.update(attack='missing')):
            document = copy.deepcopy(source); mutation(document['effects'][0])
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / motion.EFFECTS; path.parent.mkdir(parents=True)
                path.write_text(json.dumps(document), encoding='utf-8')
                baseline = copy.deepcopy(self.catalog)
                with self.assertRaises(ValueError): motion.apply_effects(self.catalog, directory)
                self.assertEqual(self.catalog, baseline)

    def test_initial_migration_preserves_existing_frame_times_and_art(self):
        generated = motion.apply(self.catalog, self.document)
        self.assertEqual(generated, self.catalog)
        self.assertEqual(len({s['id'] for s in self.document['steps']}), 5)
        self.assertEqual(self.document['steps'][-1]['frame'], 'CharacterApproved11/base_sword/F104')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for path in (motion.SOURCE, motion.CATALOG, motion.RAPIER_CHARGE, motion.HEAVY_CATALOG, motion.UNARMED, motion.HUB_BODIES, motion.REGISTRY, motion.MELEE_FRAMES, motion.EFFECTS, motion.special_frame_transport.SOURCE):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((motion.ROOT / path).read_bytes())
            motion.export(root)
            once = (root / motion.CATALOG).read_bytes()
            motion.export(root)
            self.assertEqual((root / motion.CATALOG).read_bytes(), once)

    def test_art_reimport_and_export_use_game_timing_without_mutating_package_input(self):
        original = copy.deepcopy(self.catalog)
        changed = copy.deepcopy(self.document)
        changed['steps'][0]['seconds'] = .12
        changed['sourceImpact'] = .17
        changed['fxStart'] = .13
        # Simulate the verified art importer rebuilding the original package timings.
        packaged = copy.deepcopy(self.catalog)
        first = next(c for s in packaged['sets'] if s['form'] == 'base_sword' for c in s['clips'] if c['name'] == 'attack1')
        first.pop('stepIds')
        result = motion.apply(packaged, changed)
        self.assertEqual(packaged['sets'][0]['clips'][0], self.catalog['sets'][0]['clips'][0])
        target = next(c for s in result['sets'] if s['form'] == 'base_sword' for c in s['clips'] if c['name'] == 'attack1')
        self.assertEqual(target['seconds'][0], .12)
        self.assertEqual(target['sourceImpact'], .17)
        first_original = next(c for s in original['sets'] if s['form'] == 'base_sword' for c in s['clips'] if c['name'] == 'attack1')
        target.clear(); target.update(first_original)
        self.assertEqual(result, original)
        # The image transport no longer rebuilds gameplay catalogs or timing sources.
        deployment = json.loads((motion.ROOT / 'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
        self.assertTrue(all(item['target'].endswith(('.png', '.bytes', '.wav', '.ogg', '.mp3', '.flac', '.ttf', '.otf', '.html')) for item in deployment['files']))
        self.assertFalse(any(item['target'] == motion.CATALOG for item in deployment['files']))

    def test_invalid_schema_steps_numbers_and_markers_are_rejected(self):
        mutations = [
            lambda d: d.update(schemaVersion=2), lambda d: d.update(mode='FrameTime'), lambda d: d.update(actionId='other'), lambda d: d.update(id='other'),
            lambda d: d.update(steps=[]), lambda d: d['steps'][0].update(id=d['steps'][1]['id']), lambda d: d['steps'][0].update(frame=' '),
            lambda d: d['steps'][0].update(seconds=0), lambda d: d['steps'][0].update(seconds=float('nan')), lambda d: d['steps'][0].update(seconds=float('inf')),
            lambda d: d['steps'][0].update(seconds=1e100), lambda d: d.update(sourceImpact=0), lambda d: d.update(sourceImpact=-1), lambda d: d.update(sourceImpact=10),
            lambda d: d.update(fxStart=-.5), lambda d: d.update(fxStart=10), lambda d: d.update(fxStart=float('nan'))
        ]
        for mutate in mutations:
            document = copy.deepcopy(self.document); mutate(document)
            with self.subTest(document=document), self.assertRaises(ValueError):
                motion.apply(self.catalog, document)

    def test_binding_failure_and_concurrent_catalog_change_do_not_overwrite(self):
        document = copy.deepcopy(self.document); document['steps'][0]['frame'] = 'missing'
        baseline = copy.deepcopy(self.catalog)
        with self.assertRaises(ValueError): motion.apply(self.catalog, document)
        self.assertEqual(self.catalog, baseline)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for path in (motion.SOURCE, motion.CATALOG, motion.RAPIER_CHARGE, motion.HEAVY_CATALOG, motion.UNARMED, motion.HUB_BODIES, motion.REGISTRY, motion.MELEE_FRAMES, motion.EFFECTS, motion.special_frame_transport.SOURCE):
                target = root / path; target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((motion.ROOT / path).read_bytes())
            catalog_path = root / motion.CATALOG
            before = catalog_path.read_bytes()
            (root / motion.SOURCE).write_text(json.dumps(document), encoding='utf-8')
            with self.assertRaises(ValueError): motion.export(root)
            self.assertEqual(catalog_path.read_bytes(), before)
            (root / motion.SOURCE).write_text(json.dumps(self.document), encoding='utf-8')
            original_apply = motion.apply
            def change_during_compile(catalog, data):
                result = original_apply(catalog, data); catalog_path.write_bytes(b'external edit'); return result
            with patch.object(motion, 'apply', change_during_compile), self.assertRaises(OSError): motion.export(root)
            self.assertEqual(catalog_path.read_bytes(), b'external edit')
            self.assertEqual(list(catalog_path.parent.glob('*.tmp')), [])

    def frame_document(self):
        # Transport fixture, not another implementation of frame projection.
        return dict(schemaVersion=2, id=motion.MOTION_ID, actionId=motion.ACTION_ID, mode=motion.FRAME_MODE,
                    fixedBinding=copy.deepcopy(self.document), timeline=dict(
                        frames=[dict(id=step['id'], seconds=step['seconds']) for step in self.document['steps']],
                        markers=[dict(id='contact', anchor=1, reference='sword-first-contact', offset=0.0),
                                 dict(id='hit-end', anchor=3, reference='contact', offset=0.08),
                                 dict(id='action-end', anchor=2, reference='sword-first-return', offset=0.0),
                                 dict(id='cue-start', anchor=0, reference='', offset=0.125)]))

    def test_frame_source_transports_one_timeline_without_changing_media_or_other_bindings(self):
        document = self.frame_document()
        original = copy.deepcopy(document)
        result = motion.apply(self.catalog, document)
        transported = result.pop('motionSources')
        self.assertEqual(result, self.catalog)
        self.assertEqual(len(transported), 1)
        self.assertEqual(transported[0]['fixedBinding'], self.document)
        self.assertEqual(transported[0]['timeline']['frames'], document['timeline']['frames'])
        self.assertEqual(document, original)
        self.assertEqual(self.catalog, motion.apply(result, self.document))
        document['timeline']['markers'].reverse()
        self.assertEqual(motion.apply(self.catalog, document)['motionSources'], transported)

    def test_frame_export_is_deterministic_and_fixed_cutover_removes_obsolete_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for path in (motion.SOURCE, motion.CATALOG, motion.RAPIER_CHARGE, motion.HEAVY_CATALOG, motion.UNARMED, motion.HUB_BODIES, motion.REGISTRY, motion.MELEE_FRAMES, motion.EFFECTS, motion.special_frame_transport.SOURCE):
                target = root / path; target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((motion.ROOT / path).read_bytes())
            (root / motion.SOURCE).write_text(json.dumps(self.frame_document()), encoding='utf-8')
            motion.export(root)
            once = (root / motion.CATALOG).read_bytes()
            motion.export(root)
            self.assertEqual((root / motion.CATALOG).read_bytes(), once)
            generated = json.loads(once.decode('utf-8'))
            self.assertEqual(generated['motionSources'][0]['mode'], motion.FRAME_MODE)
            self.assertEqual(motion.apply(generated, self.document), self.catalog)

    def test_frame_transport_rejects_dual_authorities_and_malformed_payloads_not_semantics(self):
        mutations = [
            lambda d: d.update(steps=copy.deepcopy(self.document['steps'])), lambda d: d.update(sourceImpact=.2),
            lambda d: d.update(fxStart=.1), lambda d: d.update(fixedBinding=None), lambda d: d.update(timeline=None),
            lambda d: d['timeline'].update(frames=[]), lambda d: d['timeline'].update(markers=[]),
            lambda d: d['timeline']['frames'][0].update(seconds=float('nan')),
            lambda d: d['timeline']['markers'][0].update(anchor=99), lambda d: d['timeline']['markers'][0].update(anchor=True),
            lambda d: d['timeline']['markers'][0].update(offset=float('inf')),
            lambda d: d['timeline']['markers'][0].update(reference=None)
        ]
        for mutate in mutations:
            document = self.frame_document(); mutate(document)
            with self.subTest(document=document), self.assertRaises(ValueError):
                motion.apply(self.catalog, document)
        document = self.frame_document()
        document['timeline']['markers'][0].update(anchor=3, reference='contact')
        # Python preserves even a semantically invalid graph: only the C#
        # compiler owns cycle detection and resolved gameplay marker rules.
        generated = motion.apply(self.catalog, document)
        self.assertEqual(next(m for m in generated['motionSources'][0]['timeline']['markers'] if m['id'] == 'contact')['reference'], 'contact')

    def test_frame_transport_rejects_unrelated_generated_source_slots_and_oversized_input(self):
        for previous in (None, {}, [dict(id='other')], [dict(id=motion.MOTION_ID), dict(id=motion.MOTION_ID)]):
            catalog = copy.deepcopy(self.catalog); catalog['motionSources'] = previous
            with self.subTest(previous=previous), self.assertRaises(ValueError):
                motion.apply(catalog, self.frame_document())
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / motion.SOURCE; source.parent.mkdir(parents=True)
            source.write_bytes(b' ' * (motion.MAXIMUM_SOURCE_BYTES + 1))
            with self.assertRaises(ValueError): motion.load(root)

    def test_registry_transport_and_art_reimport_keep_all_owned_body_weights(self):
        registry = motion.load_registry()
        changed = copy.deepcopy(registry)
        target = next(item for item in changed['motions'] if item['id'] == 'rapier/attack1')
        target['steps'][0]['seconds'] += .03
        target['steps'].insert(1, dict(id='rapier-added-known', frame=target['steps'][0]['frame'], seconds=.04))
        baseline = copy.deepcopy(self.catalog)
        generated = motion.apply_registry(self.catalog, changed)
        self.assertEqual(self.catalog, baseline)
        self.assertEqual(generated['motionRegistry'], changed)
        clip = next(clip for group in generated['sets'] if group['form'] == 'rapier' for clip in group['clips'] if clip['name'] == 'attack1')
        self.assertEqual(clip['frames'], [step['frame'] for step in target['steps']])
        self.assertEqual(clip['seconds'], [step['seconds'] for step in target['steps']])
        first_before = next(clip for group in baseline['sets'] if group['form'] == 'base_sword' for clip in group['clips'] if clip['name'] == 'attack1')
        first_after = next(clip for group in generated['sets'] if group['form'] == 'base_sword' for clip in group['clips'] if clip['name'] == 'attack1')
        self.assertEqual(first_after, first_before)
        self.assertEqual([group['effects'] for group in generated['sets']], [group['effects'] for group in baseline['sets']])
        self.assertEqual(motion.apply_registry(generated, changed), generated)

    def test_approved_heavy_media_replaces_fallbacks_before_registry_projection(self):
        heavy = json.loads((motion.ROOT / motion.HEAVY_CATALOG).read_text(encoding='utf-8'))
        forms = {group['form'] for group in heavy['sets']}
        self.assertEqual(forms, {'hammer', 'shield', 'greatsword'})
        supplied = copy.deepcopy(self.catalog)
        supplied['sets'] = [group for group in supplied['sets'] if group['form'] not in forms]
        baseline = copy.deepcopy(supplied)
        merged = motion.merge_heavy_media(supplied)
        self.assertEqual(supplied, baseline)
        self.assertEqual(motion.merge_heavy_media(merged), merged)
        self.assertEqual([group for group in merged['sets'] if group['form'] not in forms], supplied['sets'])
        registry = motion.load_registry()
        result = motion.apply_registry(merged, registry)
        self.assertEqual({group['form'] for group in result['sets']}, {'base_sword', 'whip', 'rapier', 'dagger', 'hammer', 'shield', 'greatsword', 'launcher', 'launcher_laser', 'launcher_shotgun', 'launcher_grenade', 'shuriken', 'bomb', 'unarmed'})
        melee = motion.load_melee_frames()
        special = motion.special_frame_transport.load(motion.ROOT)
        for group in heavy['sets']:
            actual = next(value for value in result['sets'] if value['form'] == group['form'])
            self.assertEqual(actual['iconUvRect'], group['iconUvRect'])
            self.assertEqual(actual['effects'], group['effects'])
            for clip in group['clips']:
                projected = next(value for value in actual['clips'] if value['name'] == clip['name'])
                if not clip['name'].startswith('attack'):
                    self.assertEqual(projected, clip)
                    continue
                identity = group['form'] + '/' + clip['name']
                palette = next(value for value in registry['palettes'] if value['id'] == identity)
                self.assertEqual(palette['frames'], list(dict.fromkeys(clip['frames'])))
                self.assertEqual(projected['frames'], clip['frames'])
                self.assertEqual(projected['seconds'], clip['seconds'])
                self.assertEqual(projected['sourceImpact'], clip['sourceImpact'])
                self.assertEqual(projected['fxStart'], clip['fxStart'])
                self.assertEqual(len(set(projected['stepIds'])), len(clip['frames']))
                for frame in clip['frames']:
                    self.assertTrue((motion.ROOT / motion.CATALOG.parent.parent / (frame + '.png')).is_file(), frame)
            for binding in (value for value in registry['actions'] if value['form'] == group['form']):
                self.assertFalse(binding['fallback'])
                self.assertEqual(binding['motion'], binding['form'] + '/' + binding['clip'])
                records = melee['actions'] if binding.get('frameTimingSource') == motion.MELEE_FRAMES.as_posix() else special['actions']
                if binding.get('frameTimingSource'):
                    record = next(value for value in records if value['actionId'] == binding['actionId'])
                    self.assertEqual(record['motionId'], binding['motion'])

    def test_registry_transport_is_not_a_second_semantic_compiler(self):
        registry = motion.load_registry()
        registry['actions'][0]['actionId'] = 'unknown-action'
        # C# MotionSourceRegistry alone rejects unknown coverage. Python does
        # not infer a second inventory or resolve family-specific timing.
        transported = motion.apply_registry(self.catalog, registry)
        self.assertEqual(transported['motionRegistry']['actions'][0]['actionId'], 'unknown-action')
        registry['motions'][1]['steps'][0]['seconds'] = float('nan')
        with self.assertRaises(ValueError): motion.apply_registry(self.catalog, registry)

    def test_registry_optional_source_reference_preserves_serialized_absence(self):
        registry = motion.load_registry()
        binding = next(action for action in registry['actions'] if not action.get('frameTimingSource'))
        for absent in (None, ''):
            binding['frameTimingSource'] = absent
            result = motion.apply_registry(self.catalog, registry)
            actual = next(action for action in result['motionRegistry']['actions'] if action['actionId'] == binding['actionId'] and action['kind'] == binding['kind'] and action['variant'] == binding['variant'])
            self.assertEqual(actual['frameTimingSource'], absent)
        binding['frameTimingSource'] = 7
        with self.assertRaises(ValueError):
            motion.apply_registry(self.catalog, registry)

    def test_first_sword_known_media_step_insertion_and_references_survive_export(self):
        document = copy.deepcopy(self.document)
        document['steps'].insert(1, dict(id='first-added-known', frame=document['steps'][-1]['frame'], seconds=.04))
        result = motion.apply(self.catalog, document)
        clip = next(clip for group in result['sets'] if group['form'] == 'base_sword' for clip in group['clips'] if clip['name'] == 'attack1')
        self.assertEqual(clip['stepIds'], [step['id'] for step in document['steps']])
        self.assertEqual(clip['frames'], [step['frame'] for step in document['steps']])
        self.assertEqual(clip['seconds'], [step['seconds'] for step in document['steps']])
        self.assertEqual(self.catalog['motionRegistry'], result['motionRegistry'])
        imported = motion.apply(motion.apply_registry(self.catalog, motion.load_registry()), document)
        self.assertEqual(next(clip for group in imported['sets'] if group['form'] == 'base_sword' for clip in group['clips'] if clip['name'] == 'attack1'), clip)
        frame_document = self.frame_document()
        frame_document['fixedBinding'] = copy.deepcopy(document)
        frame_document['timeline']['frames'] = [dict(id=step['id'], seconds=step['seconds']) for step in document['steps']]
        for source in (document, frame_document):
            with self.subTest(mode=source['mode']), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for path in (motion.SOURCE, motion.CATALOG, motion.RAPIER_CHARGE, motion.HEAVY_CATALOG, motion.UNARMED, motion.HUB_BODIES, motion.REGISTRY, motion.MELEE_FRAMES, motion.EFFECTS, motion.special_frame_transport.SOURCE):
                    target = root / path; target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes((motion.ROOT / path).read_bytes())
                (root / motion.SOURCE).write_text(json.dumps(source), encoding='utf-8')
                motion.export(root)
                generated = json.loads((root / motion.CATALOG).read_text(encoding='utf-8'))
                published = next(clip for group in generated['sets'] if group['form'] == 'base_sword' for clip in group['clips'] if clip['name'] == 'attack1')
                self.assertEqual(published, clip)
                once = (root / motion.CATALOG).read_bytes()
                motion.export(root)
                self.assertEqual((root / motion.CATALOG).read_bytes(), once)
                self.assertEqual(generated['motionRegistry'], self.catalog['motionRegistry'])


if __name__ == '__main__':
    unittest.main()
