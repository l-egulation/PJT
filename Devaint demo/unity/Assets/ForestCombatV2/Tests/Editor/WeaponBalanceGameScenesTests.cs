using System;
using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public class WeaponBalanceGameScenesTests
    {
        IDisposable profile;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            profile?.Dispose(); profile = null;
        }
        [UnityTest] public IEnumerator BothActualChapterScenesLoadTheReleaseDamageAndCollisionRules()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            profile = Chapter1ProfileHost.UseTemporaryStore(new MemoryProfileStore());
            foreach (string path in new[] { Chapter1MapSelector.ProceduralScene, "Assets/Chapter2/Scenes/Chapter2Procedural.unity" }) {
                EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                CombatArena arena = null;
                bool ready = false;
                for (int frame = 0; frame < 1200; frame++) {
                    yield return null;
                    arena = Object.FindAnyObjectByType<CombatArena>();
                    var first = Object.FindAnyObjectByType<ForestCombatWorld>();
                    var second = Object.FindAnyObjectByType<Deviant.Chapter2.Chapter2World>();
                    ready = arena && arena.Player && (first && first.Ready || second && second.Ready);
                    if (ready) break;
                }
                Assert.That(arena && arena.Player, Is.True, path);
                Assert.That(ready, Is.True, path + " world ready");
                Assert.That(arena.CompositionProfile, Is.EqualTo(CombatCompositionProfile.Game), path);
                var expected = new[] { ("base_sword", 26f), ("whip", 29f), ("dagger", 40f), ("rapier", 33f),
                    ("hammer", 87f), ("greatsword", 111f), ("shield", 17f), ("launcher", 13f), ("shuriken", 23f), ("bomb", 24f) };
                foreach (var item in expected)
                    Assert.That(arena.catalog.forms.Single(f => f.id == item.Item1).basics.Sum(b => b.damage), Is.EqualTo(item.Item2), path + " " + item.Item1);
                var great = arena.catalog.forms.Single(f => f.id == "greatsword");
                Assert.That(great.basics.All(b => b.box.x - b.box.width / 2 >= .65624f), Is.True);
                Assert.That(arena.catalog.forms.Single(f => f.id == "launcher").specials[2].ranged.pierce, Is.True);
                Assert.That(arena.catalog.forms.Single(f => f.id == "shuriken").specials[2].ranged.pierce, Is.True);
                Assert.That(arena.GetDirectMeleeSpecial("greatsword_range"), Is.Not.Null);
                Assert.That(arena.catalog.forms.Single(f => f.id == "shield").specials[0].effect.parryDamage, Is.EqualTo(26));
                Assert.That(arena.SimulationFaulted, Is.False);
                Debug.Log("BALANCE_GAME_SCENE " + path + " all ten forms and special collision policies loaded");
            }
        }
    }
}
