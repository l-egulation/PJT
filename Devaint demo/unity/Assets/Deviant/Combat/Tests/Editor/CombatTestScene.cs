using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    // Gameplay fixtures deliberately run without the separately distributed PNGs.
    // Art fixtures opt in; missing art must still fail their assertions.
    internal static class CombatTestScene
    {
        public static void Open(bool requireArt = false)
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena, Is.Not.Null);
            arena.loadCharacterArt = requireArt ||
                TestContext.CurrentContext.Test.Properties["Category"].Contains("RequiresArt");
        }
    }
}
