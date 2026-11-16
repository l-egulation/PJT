"""The main entry point only runs a clean, pinned resource exporter and media contract."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

import update_assets as launcher


class UpdateAssetsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        (self.repo / 'resources').mkdir()
        (self.repo / 'resources/.git').write_text('gitdir: placeholder')
        self.revision = 'a' * 40
        self.target = 'unity/Assets/Example/icon.png'
        self.meta = self.repo / (self.target + '.meta')
        self.meta.parent.mkdir(parents=True)
        self.meta.write_text('main-owned-guid')
        self.document = {'format': 'deviant-asset-deployment-v1', 'files': [
            {'target': self.target, 'source': {'asset': 'ui.example', 'path': 'ui/example/icon.png'},
             'bytes': 5, 'sha256': 'b' * 64}]}
        self.write_manifest()

    def write_manifest(self):
        path = self.repo / 'docs/assets/manifests/unity-deployment.json'
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(self.document))

    def test_uninitialized_submodule_is_rejected_without_a_folder_dialog(self):
        (self.repo / 'resources/.git').unlink()
        with self.assertRaisesRegex(ValueError, 'not initialized'):
            launcher.resource_revision(self.repo)

    def test_pinned_clean_resource_commit_is_required(self):
        entry = '160000 ' + self.revision + ' 0\tresources\n'
        for head, dirty, passes in ((self.revision, '', True), ('c' * 40, '', False),
                                    (self.revision, ' M tools/assets.py', False)):
            with self.subTest(head=head, dirty=dirty), patch.object(
                    launcher.subprocess, 'check_output', side_effect=[entry, head, dirty]):
                if passes:
                    self.assertEqual(self.revision, launcher.resource_revision(self.repo))
                else:
                    with self.assertRaises(ValueError):
                        launcher.resource_revision(self.repo)

    def test_consumer_cannot_overwrite_code_metadata_or_game_sources(self):
        for target in ('data/combat/actions/base_sword_basic_0.json',
                       'unity/Assets/Example/icon.png.meta', 'tools/arbitrary.png'):
            self.document['files'][0]['target'] = target
            self.write_manifest()
            with self.subTest(target=target), self.assertRaisesRegex(ValueError, 'Not an approved'):
                launcher.contract(self.repo)

    def test_missing_main_owned_guid_is_rejected(self):
        self.meta.unlink()
        with self.assertRaisesRegex(ValueError, 'metadata is missing'):
            launcher.contract(self.repo)

    def test_audio_fonts_and_vendor_images_require_existing_unity_metadata(self):
        for name in ('unity/Assets/Deviant/Audio/SFX/test.wav',
                     'unity/Assets/Deviant/UI/Fonts/test.ttf',
                     'unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/test.otf',
                     'unity/Assets/Deviant/Tutorial/Resources/FirstRunTutorial/Fonts/Galmuri11.ttf',
                     'unity/Assets/Deviant/Tutorial/Resources/FirstRunTutorial/Fonts/Galmuri11-Bold.ttf',
                     'unity/Assets/TextMesh Pro/Fonts/LiberationSans.ttf',
                     'unity/Assets/ThirdParty/Wallcoeur/texture.png'):
            with self.subTest(target=name):
                self.document['files'][0]['target'] = name
                self.write_manifest()
                with self.assertRaisesRegex(ValueError, 'metadata is missing'):
                    launcher.contract(self.repo)
                meta = self.repo / (name + '.meta')
                meta.parent.mkdir(parents=True, exist_ok=True)
                meta.write_bytes(b'preserved GUID\r\n')
                launcher.contract(self.repo)
                self.assertEqual(b'preserved GUID\r\n', meta.read_bytes())

    def test_media_extension_does_not_allow_arbitrary_or_traversal_destinations(self):
        for name in ('tools/report.ttf', 'unity/Assets/Example/code.wav',
                     'unity/Assets/Deviant/Tutorial/Resources/FirstRunTutorial/FontsOther/test.ttf',
                     'unity/Assets/Deviant/Tutorial/Resources/FirstRunTutorial/Fonts/../escape.ttf',
                     'unity/Assets/Deviant/UI/Fonts/../../../escape.ttf',
                     'unity/Assets/Deviant/Audio/../escape.wav',
                     'unity/Assets/Deviant/Audio/script.cs',
                     'unity/Assets/Deviant/UI/Fonts/test.ttf.meta'):
            self.document['files'][0]['target'] = name
            self.write_manifest()
            with self.subTest(target=name), self.assertRaisesRegex(ValueError, 'Not an approved'):
                launcher.contract(self.repo)

    def test_checked_in_deployment_contract_uses_only_approved_destinations(self):
        # Check the whole shared contract even when media is not restored locally.
        manifest = launcher.contract(launcher.ROOT, require_metadata=False)
        self.assertTrue(manifest['files'])

    def test_only_exact_boss_textasset_and_two_editor_bundle_destinations_are_allowed(self):
        for name in (*sorted(launcher.MAP_EDITORS), launcher.BOSS_BYTES,
                     'unity/Assets/Chapter2/Resources/RoseBlue/rose.bytes',
                     'unity/Assets/Chapter2/Resources/RoseBlue/blue.bytes'):
            self.document['files'][0]['target'] = name
            self.write_manifest()
            if name.startswith('unity/'):
                with self.assertRaisesRegex(ValueError, 'metadata is missing'):
                    launcher.contract(self.repo)
                meta = self.repo / (name + '.meta')
                meta.parent.mkdir(parents=True, exist_ok=True)
                meta.write_text('boss GUID retained')
            with self.subTest(target=name):
                launcher.contract(self.repo)
        for name in ('tools/chapter1-map-editor/arbitrary.html', 'tools/script.html',
                     'unity/Assets/ForestCombatV2/Resources/ClockExecutioner/code.bytes',
                     'unity/Assets/Chapter2/Resources/RoseBlue/other.bytes',
                     'tools/chapter1-map-editor/맵 편집기.html/../script.html'):
            self.document['files'][0]['target'] = name
            self.write_manifest()
            with self.subTest(target=name), self.assertRaisesRegex(ValueError, 'Not an approved'):
                launcher.contract(self.repo)

    def test_apply_blocks_an_open_unity_project(self):
        lock = self.repo / 'unity/Temp/UnityLockfile'
        lock.parent.mkdir(parents=True)
        lock.touch()
        with patch.object(launcher, 'resource_revision', return_value=self.revision), self.assertRaisesRegex(ValueError, 'Close Unity'):
            launcher.update(self.repo)

    def test_local_preview_requires_explicit_selection_and_keeps_media_preflight(self):
        with self.assertRaisesRegex(ValueError, 'explicit --asset'):
            launcher.update(self.repo, local_preview=True)
        selected = Mock(); selected.export.return_value = {'verified': 1}
        with patch.object(launcher, 'resource_revision') as revision, patch.object(launcher, 'backend', return_value=selected):
            result = launcher.update(self.repo, check=True, asset='ui.example', local_preview=True)
        revision.assert_not_called()
        selected.export.assert_called_once_with(self.repo/'resources', self.document, self.repo, check=True)
        self.assertTrue(result['localPreview']); self.assertIsNone(result['resourceCommit'])

    def test_local_preview_still_blocks_open_unity_and_unapproved_targets(self):
        lock = self.repo/'unity/Temp/UnityLockfile'; lock.parent.mkdir(parents=True); lock.touch()
        with self.assertRaisesRegex(ValueError, 'Close Unity'):
            launcher.update(self.repo, asset='ui.example', local_preview=True)
        self.document['files'][0]['target'] = 'data/combat/invalid.png'; self.write_manifest()
        with self.assertRaisesRegex(ValueError, 'Not an approved'):
            launcher.update(self.repo, check=True, asset='ui.example', local_preview=True)

    def test_check_delegates_to_resource_exporter_without_mutating_metadata(self):
        backend = Mock()
        backend.export.return_value = {'verified': 1, 'changed': 1, 'checkOnly': True}
        with patch.object(launcher, 'resource_revision', return_value=self.revision), patch.object(
                launcher, 'backend', return_value=backend):
            result = launcher.update(self.repo, check=True)
        backend.export.assert_called_once_with(self.repo / 'resources', self.document, self.repo, check=True)
        self.assertEqual(self.revision, result['resourceCommit'])
        self.assertEqual('main-owned-guid', self.meta.read_text())
        self.assertFalse((self.repo / 'outputs').exists())

    def test_asset_selection_filters_only_deployed_files_of_that_asset(self):
        extra = copy.deepcopy(self.document['files'][0]); extra['source']['asset'] = 'ui.other'
        extra['target'] = 'unity/Assets/Example/other.png'
        # Unselected assets need not be deployed locally. Their GUIDs are untouched.
        self.document['files'].append(extra); self.write_manifest()
        selected = copy.deepcopy(self.document); selected['files'] = selected['files'][:1]
        backend = Mock(); backend.export.return_value = {}
        with patch.object(launcher, 'resource_revision', return_value=self.revision), patch.object(launcher, 'backend', return_value=backend):
            launcher.update(self.repo, check=True, asset='ui.example')
        backend.export.assert_called_once_with(self.repo / 'resources', selected, self.repo, check=True)

    def test_unknown_asset_never_invokes_the_exporter(self):
        with patch.object(launcher, 'resource_revision', return_value=self.revision), patch.object(launcher, 'backend') as backend:
            with self.assertRaisesRegex(ValueError, 'No deployed files'):
                launcher.update(self.repo, asset='ui.missing')
        backend.assert_not_called()

    def test_hub_metadata_compiler_never_copies_images_or_rewrites_the_deployment_contract(self):
        import build_hub_bindings as hub
        source = self.repo / 'data/assets/starting_hub.json'
        source.parent.mkdir(parents=True)
        source.write_text(json.dumps({'layoutProposal': {'stations': []}}))
        index = self.repo / 'unity/Assets/Deviant/Hub/Data/hub-sprites.json'
        layout = self.repo / 'unity/Assets/Deviant/Hub/Resources/HubLayout.json'
        story = self.repo / 'unity/Assets/Deviant/Hub/Resources/HubStory.json'
        layout.parent.mkdir(parents=True)
        row = {'id': 'test', 'unity': 'Assets/Deviant/Hub/Resources/Hub/test.png'}
        deployment = self.repo / 'docs/assets/manifests/unity-deployment.json'
        before = deployment.read_bytes()
        # Every generated binding stays in the temporary fixture. New hub bindings must not read
        # the developer's restored art or write into the real project from this exporter test.
        from contextlib import ExitStack
        outputs = {name: layout.parent / (name.lower() + '.json')
                   for name in ('OPENING', 'BODIES', 'VISITS', 'NOTE', 'GHOSTS', 'SKY')}
        with ExitStack() as mocks:
            mocks.enter_context(patch.multiple(hub, ROOT=self.repo, SOURCE=source, INDEX=index,
                                              LAYOUT=layout, STORY=story, **outputs))
            mocks.enter_context(patch.object(hub, 'plan', return_value=[row]))
            for name in ('story', 'opening', 'bodies', 'visits', 'note', 'ghosts', 'sky'):
                mocks.enter_context(patch.object(hub, name + '_text', return_value='{}\n'))
            mocks.enter_context(patch.object(hub.sys, 'argv', ['build_hub_bindings.py']))
            mocks.enter_context(patch('builtins.print'))
            self.assertEqual(0, hub.main())
        self.assertTrue(index.is_file() and layout.is_file() and story.is_file())
        self.assertTrue(all(path.is_file() for path in outputs.values()))
        self.assertFalse((self.repo / 'unity' / row['unity']).exists())
        self.assertEqual(before, deployment.read_bytes())


if __name__ == '__main__':
    unittest.main()
