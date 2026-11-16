using System.Collections;
using System.Linq;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The large-bomb skill throws a radius 0.4 bomb; in the air it is the dedicated round art drawn to the size
    // of the bomb held in its throw motion (2026-11-12). Runs in the shipped chapter scene with the approved art.
    public sealed class LargeBombProjectileArtTests
    {
        [UnityTest] public IEnumerator LargeBombFliesAsItsOwnRoundArtAtTheSizeHeldInItsThrow()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            for (int i = 0; i < 600 && (!world || !world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            var arena = world.arena; var player = arena.Player;
            Assert.That(arena.GetRangedAction("bomb_range_special").ToNumericData().bomb.radius, Is.EqualTo(BombProjectile.LargeArtRadius).Within(.0001f), "the large-bomb skill throws that bomb");
            BombProjectile Throw(float radius) => BombProjectile.Spawn(arena, player, player.Position + Vector2.up * 3, Vector2.right, new BombData { radius = radius, blastRadius = 3, gravity = 20, lifetime = 5 }, 1);
            var basic = Throw(.2f).GetComponentInChildren<ApprovedThrownVisual>();
            var large = Throw(BombProjectile.LargeArtRadius).GetComponentInChildren<ApprovedThrownVisual>();
            Assert.That(basic, Is.Not.Null); Assert.That(large, Is.Not.Null);
            Assert.That(basic.name, Is.EqualTo("ApprovedThrown_bomb"), "the basic bomb keeps its art");
            Assert.That(large.name, Is.EqualTo("ApprovedThrown_bomb_large"));
            var sprite = large.GetComponent<SpriteRenderer>().sprite;
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(106.6667f).Within(.001f), "same density as the large-bomb throw motion");
            Assert.That(sprite.bounds.size.x, Is.EqualTo(144 / 106.6667f).Within(.01f));
            Assert.That(large.transform.lossyScale.x, Is.EqualTo(1).Within(.001f), "native size, not enlarged");
            foreach (var bomb in arena.bombs.ToArray()) if (bomb) Object.Destroy(bomb.gameObject);
            yield return new ExitPlayMode();
        }
    }
}
