# Legacy test fixtures

These text fixtures are outside Unity Assets and are not playable scenes. The current game contains only Chapter1Title and Chapter1Procedural.

Tests for retired prototypes still refer to the original asset paths. They open scenes through `LegacySceneFixture.Open` (one copy per test assembly), which skips the test with `Assert.Ignore` when the scene is missing but preserved here, so a normal EditMode run reports them as skipped, not failed. Their `[UnityTearDown]` only exits Play Mode when it is playing.

For an explicit legacy test run, copy only its required `Assets/.../*.unity.txt` and `.unity.meta.txt` pair back under the Unity project at the original path, removing the final `.txt` suffix; the same tests then run normally. Remove those temporary assets after the test. Do not add them to Build Settings or use their builders as current game entry points. Current chapter and monster tests do not need these fixtures.
