using System;
using System.Collections;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ShieldReviewedIntegrationTests
    {
        [Serializable] sealed class SheetClip { public string name; public int count, width, height, columns; }
        [Serializable] sealed class SheetCatalog { public SheetClip[] clips; }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [Test] public void ApprovedSourceAndAlphaSheetsArePresent()
        {
            Assert.That(ShieldRuntimeRules.Current.parryProjectileReach, Is.EqualTo(.75f));
            Assert.That(ShieldRuntimeRules.Current.decoyDamage, Is.EqualTo(20));
            Assert.That(ShieldRuntimeRules.Current.decoyRadius, Is.EqualTo(3));
            var catalog = Resources.Load<TextAsset>("ShieldReviewed/clips"); Assert.That(catalog, Is.Not.Null);
            var clips = JsonUtility.FromJson<SheetCatalog>(catalog.text).clips;
            foreach (var name in new[] { "basic", "parry-empty", "parry-success", "rush", "guard-30-back", "guard-6-front", "guard-break-front", "decoy", "burst" })
            {
                var texture = Resources.Load<Texture2D>("ShieldReviewed/" + name); Assert.That(texture, Is.Not.Null, name);
                var clip = System.Array.Find(clips, c => c.name == name); Assert.That(clip, Is.Not.Null, name);
                // 칸은 원래 화소 크기로 잘라 오므로, 가져올 때 시트를 2의 거듭제곱 등으로 늘리거나 줄이면 장이 어긋난다(2026-11-08).
                Assert.That(texture.width, Is.EqualTo(clip.width * clip.columns), name);
                Assert.That(texture.height, Is.EqualTo(clip.height * ((clip.count + clip.columns - 1) / clip.columns)), name);
                Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0), name);
            }
        }
        [UnityTest] public IEnumerator ActualDamageReflectionPushGuardAndDecoyAgreeWithApproval()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var arena = new GameObject("Shield reviewed integration").AddComponent<CombatArena>(); arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Shield test camera").AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True); arena.loadCharacterArt = true;
            var floor = new GameObject("Shield test floor"); floor.transform.position = new Vector2(0, 99.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>();
            var enemy = new GameObject("Shield test enemy").AddComponent<TestEnemy>(); enemy.Configure(arena, new Vector2(2,100), new Vector2(1,2), Vector2.up, false); enemy.maxHealth = enemy.health = 1000; enemy.aggressive = false; arena.enemies.Add(enemy);
            var player = arena.Player; int shield = Array.FindIndex(arena.catalog.forms, f => f.id == "shield");
            using (var driver = arena.AcquireManualSimulation())
            {
                void Ready(int slot)
                {
                    driver.Reset(); player.UseTestInput = true; player.InputBlocked = false; player.selected = 0;
                    foreach (var w in player.weapons) w.StatSlots.Clear();
                    Assert.That(player.TryReconfigure(0, shield, slot, out var error), Is.True, error);
                    player.Position = new Vector2(0,100); player.velocity = Vector2.zero; player.SyncFacing(1); player.grounded = true;
                    enemy.ResetActor(new Vector2(2,100)); enemy.maxHealth = enemy.health = 1000; enemy.aggressive = false; Physics2D.SyncTransforms();
                }
                CombatCast Start()
                { driver.Step(.02f, new CombatInput(0,CombatButtons.Special)); Assert.That(player.currentCast, Is.Not.Null); return player.currentCast; }
                void Advance(float seconds) { for (int i=0;i<Mathf.CeilToInt(seconds/.01f);i++) { driver.Step(.01f,default); arena.ShieldPresentation.Tick(); } }

                Ready(0);driver.Step(.02f,new CombatInput(0,CombatButtons.Basic));
                var capturedBody = Resources.Load<Sprite>("CharacterApproved11/shield/F047"); Assert.That(capturedBody, Is.Not.Null);
                player.visual.sprite = capturedBody; arena.ShieldPresentation.Tick();
                Assert.That(player.visual.sprite, Is.SameAs(capturedBody), "basic body remains owned by the speed-adjusted attack snapshot");
                Assert.That(arena.ShieldPresentation.ActiveEffects, Is.EqualTo(0), "contact effect waits for impact");
                Advance(.25f);
                var contact = arena.transform.Find("Reviewed shield effects/basic").GetComponent<SpriteRenderer>();
                Assert.That(contact.sprite, Is.Not.Null); Assert.That(contact.enabled, Is.True);
                Assert.That(arena.Presentation.LastEffectHandle, Is.Null);
                Assert.That(player.currentCast.isBasic,Is.True);Assert.That(arena.ShieldPresentation.ActiveEffects,Is.GreaterThan(0),"basic impact uses reviewed contact effect");Assert.That(enemy.ReceivedHitCount,Is.EqualTo(1));
                Ready(0); Start(); Advance(.14f);
                float before=player.health; Assert.That(player.Receive(10,enemy,-1,false,null), Is.False, "0.16-second frontal melee must parry"); Assert.That(player.health, Is.EqualTo(before)); Assert.That(enemy.ReceivedHitCount, Is.EqualTo(1));
                Advance(.10f); Assert.That(player.Receive(10,enemy,-1,false,null), Is.True, "closed window must permit damage");

                Ready(0); var cast=Start();
                Assert.That(cast.TryGetShieldParryBounds(out var expanded), Is.True);
                float px=player.hurtCollider.bounds.max.x+.60f;
                Assert.That(expanded.max.x, Is.EqualTo(player.hurtCollider.bounds.max.x+.75f).Within(.001f));
                var shot=new GameObject("Shield reflected test").AddComponent<ArenaProjectile>();shot.Initialize(arena,enemy,new Vector2(px,player.Center.y),Vector2.left*8,10,arena.Now+3);arena.projectiles.Add(shot);
                shot.Step(.01f);Assert.That(shot.Execution.Reflected, Is.True);Assert.That(shot.velocity.x, Is.EqualTo(8));Assert.That(shot.Execution.Damage, Is.EqualTo(10).Within(.001f));Assert.That(player.health, Is.EqualTo(100));

                Ready(2); var rush=Start(); enemy.Position=new Vector2(2,100);Physics2D.SyncTransforms();Advance(.35f);
                Assert.That(enemy.ReceivedHitCount, Is.EqualTo(1));
                float face=player.Position.x+1.45625f;
                Assert.That(enemy.bodyCollider.bounds.min.x, Is.GreaterThanOrEqualTo(face-.04f), "enemy must stay beyond shield front");
                Advance(.15f);Assert.That(enemy.ReceivedHitCount, Is.EqualTo(1));

                Ready(1); Start();Advance(.12f);Assert.That(player.shield, Is.EqualTo(30));
                for(int i=0;i<5;i++)player.Receive(10,enemy,-1,false,null);
                Assert.That(player.shield, Is.EqualTo(0));Assert.That(player.health, Is.EqualTo(100));
                player.Receive(10,enemy,-1,false,null);Assert.That(player.health, Is.EqualTo(94).Within(.001f));Assert.That(player.guardUntil, Is.GreaterThan(arena.Now));

                Ready(3); Start();Advance(.25f);Assert.That(arena.decoys.Count, Is.EqualTo(1));var decoy=arena.decoys[0];enemy.Position=decoy.Position+Vector2.right;Physics2D.SyncTransforms();
                int hits=enemy.ReceivedHitCount;float enemyHealth=enemy.health;decoy.Receive(9999,enemy,-1,false,null);Assert.That(enemy.ReceivedHitCount, Is.EqualTo(hits+1));Assert.That(enemyHealth-enemy.health, Is.EqualTo(20));decoy.GetComponent<ShieldDecoyRuntime>().Evaluate(true);Assert.That(enemy.ReceivedHitCount, Is.EqualTo(hits+1), "destroy then expiry must not burst twice");
                Ready(3);Start();Advance(.25f);decoy=arena.decoys[0];enemy.Position=decoy.Position+Vector2.right;Physics2D.SyncTransforms();hits=enemy.ReceivedHitCount;Advance(3);Assert.That(enemy.ReceivedHitCount, Is.EqualTo(hits+1), "expiry bursts once");Assert.That(arena.decoys.Count, Is.EqualTo(0));
                Ready(3);Start();Advance(.25f);decoy=arena.decoys[0];enemy.Position=decoy.Position+Vector2.right;Physics2D.SyncTransforms();hits=enemy.ReceivedHitCount;decoy.GetComponent<ShieldDecoyRuntime>().Discard();decoy.GetComponent<ShieldDecoyRuntime>().Evaluate(true);Assert.That(enemy.ReceivedHitCount, Is.EqualTo(hits));
                Ready(0);Start();float rearHealth=player.health;
                var rear=new GameObject("Rear projectile test").AddComponent<ArenaProjectile>();rear.Initialize(arena,enemy,new Vector2(player.hurtCollider.bounds.min.x-.02f,player.Center.y),Vector2.right*8,10,arena.Now+3);arena.projectiles.Add(rear);rear.Step(.01f);
                Assert.That(rear.Execution.Reflected, Is.False);Assert.That(player.health, Is.LessThan(rearHealth), "rear shots are not parried");
                Ready(3);var wall=new GameObject("Blocked decoy test");wall.transform.position=new Vector2(1.40625f,100.75f);wall.AddComponent<BoxCollider2D>().size=new Vector2(.8f,1.5f);wall.AddComponent<ArenaSurface>();Physics2D.SyncTransforms();Start();Advance(.25f);Assert.That(arena.decoys.Count,Is.EqualTo(0),"solid walls still prevent placement");Object.Destroy(wall);
                Assert.That(arena.SimulationFaulted, Is.False);
            }
            Object.Destroy(floor);
        }
    }
}
