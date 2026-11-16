using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorReturnIdentityTests
    {
        [UnityTest] public IEnumerator GameSameWeaponViewsShareOneReturnAdmission() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSameWeaponViewsShareOneReturnAdmission() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var floor = new GameObject("ReturnIdentityFloor"); floor.transform.position = new Vector2(0, 999.5f); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            var a = new GameObject("ReturnIdentityFirst").AddComponent<CombatActor>(); var b = new GameObject("ReturnIdentitySecond").AddComponent<CombatActor>(); a.Configure(arena, new Vector2(4, 1000), Vector2.one * 2, Vector2.up, false); b.Configure(arena, new Vector2(8, 1000), Vector2.one * 2, Vector2.up, false);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); var p = arena.Player; p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                    var owner = Deviant.Equipment.WeaponResources.CreateWeapon(); var first = new WeaponState(owner); var alias = new WeaponState(owner); var shot = arena.catalog.forms[System.Array.FindIndex(arena.catalog.forms, f => f.id == "shuriken")].specials[2].ranged; Physics2D.SyncTransforms();
                    Assert.That(p.TryShurikenVisit(a, first, shot.returnDelay, shot.behindGap), Is.True); var before = p.Position; Assert.That(p.TryShurikenVisit(b, alias, shot.returnDelay, shot.behindGap), Is.False, "Two views of the same Equipment instance cannot bypass its pending return."); Assert.That(p.Position, Is.EqualTo(before)); Assert.That(p.ShurikenReturnPending(alias), Is.True); Assert.That(p.ShurikenReturnRemaining(alias), Is.EqualTo(p.ShurikenReturnRemaining(first)));
                }
            }
            finally { Object.Destroy(a.gameObject); Object.Destroy(b.gameObject); Object.Destroy(floor); }
        }
    }
}
