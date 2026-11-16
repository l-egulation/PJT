using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The human hero of the opening (before the ink) climbs walls in its own frames and leaves dash afterimages in its
    // own colours, not the ink body's black (playtest 2026-11-03).
    public sealed class StartingVillageHumanBodyTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test] public void HumanWallKickKeepsTheStandingBodySize()
        {
            const string root = "Hub/opening/human-hero/";
            var standing = Resources.Load<Sprite>(root + "idle/F01");
            Assert.That(standing, Is.Not.Null);
            float standingHeight = OpaqueHeight(standing);
            for (int frame = 24; frame <= 29; frame++)
            {
                var sprite = Resources.Load<Sprite>(root + "wall/F" + frame);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(OpaqueHeight(sprite) / standingHeight, Is.InRange(.85f, 1.25f),
                    sprite.name + " must not shrink when leaving the wall");
            }
        }

        static float OpaqueHeight(Sprite sprite)
        {
            Assert.That(sprite.texture.isReadable, Is.True, sprite.name + " keeps its authored importer");
            var rect = sprite.rect; var pixels = sprite.texture.GetPixels32();
            int bottom = (int)rect.yMax, top = (int)rect.y;
            for (int y = (int)rect.y; y < rect.yMax; y++)
                for (int x = (int)rect.x; x < rect.xMax; x++)
                    if (pixels[y * sprite.texture.width + x].a >= 128)
                    { bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
            return (top - bottom + 1) / sprite.pixelsPerUnit;
        }

        [UnityTest] public IEnumerator TheHumanHeroClimbsAndDashesWithoutInk()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            try
            {
                StartingVillageWorld village = null;
                for (int f = 0; f < 600 && !(village && village.Ready); f++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                gui.GetType().GetField("storyChecked", Hidden).SetValue(gui, false);
                yield return null; yield return null;
                gui.GetType().GetMethod("SkipOpening", Hidden).Invoke(gui, null);
                yield return null;
                var player = village.World.arena.Player;
                player.SetMovementAbilities(Deviant.Combat.Contracts.MovementAbilities.All);
                village.SetHeroBody(village.OpeningBody);

                // Wall frames: the human body has its own, the ink body falls back to the approved ink art.
                var traversal = Object.FindAnyObjectByType<Chapter1TraversalPresentation>();
                Assert.That(traversal, Is.Not.Null);
                var body = (CharacterAnimationSet)typeof(Chapter1TraversalPresentation).GetMethod("BodySet", Hidden).Invoke(traversal, null);
                foreach (var (name, count) in new[] { ("wall_contact", 16), ("wall_kick", 6) })
                {
                    var clip = body.Find(name);
                    Assert.That(clip, Is.Not.Null, name);
                    Assert.That(clip.sprites.Length, Is.EqualTo(count), name);
                    Assert.That(clip.sprites.All(s => s && AssetDatabase.GetAssetPath(s).Contains("opening/human-hero/wall/")), Is.True, name + " draws the human wall frames");
                    var ink = name == "wall_contact" ? traversal.Art.wallContact : traversal.Art.wallKick;
                    Assert.That(clip.Duration, Is.EqualTo(ink.Duration).Within(1e-4f), name + " keeps the approved timing");
                }
                // A jump reads the same as in a fight: each jump phase lasts as long as the combat forms' approved clip.
                var combat = System.Array.Find(traversal.Art.jumps, j => j.form == "base_sword");
                foreach (var phase in new[] { "jump_start", "jump_rise", "jump_apex", "fall_start", "fall_loop", "land" })
                    foreach (var id in new[] { village.OpeningBody, village.VillageBody })
                    {
                        var set = System.Array.Find(village.World.arena.characterAnimations.sets, x => x.form == id);
                        Assert.That(set.Find(phase).Duration, Is.EqualTo(combat.Find(phase).Duration).Within(1e-3f), id + " " + phase);
                    }
                village.SetHeroBody(village.VillageBody);
                var inkBody = (CharacterAnimationSet)typeof(Chapter1TraversalPresentation).GetMethod("BodySet", Hidden).Invoke(traversal, null);
                Assert.That(inkBody?.Find("wall_contact"), Is.Null, "the ink body keeps the approved ink wall art");
                village.SetHeroBody(village.OpeningBody);

                var savedPosition = player.Position;
                var wall = new GameObject("Human wall contact regression");
                wall.transform.position = new Vector3(204, 200, 0);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(2, 80);
                wall.AddComponent<ArenaSurface>();
                try
                {
                    using (var driver = village.World.arena.AcquireManualSimulation())
                    {
                      foreach (string bodyId in new[] { village.OpeningBody, village.VillageBody })
                        foreach (int side in new[] { 1, -1 })
                        {
                            driver.Reset(); village.SetHeroBody(bodyId);
                            player.Position = new Vector2(side > 0 ? 202.34f : 205.66f, 200);
                            player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(side);
                            Physics2D.SyncTransforms();
                            for (int frame = 0; frame < 22; frame++)
                            {
                                driver.Step(.025f, new Deviant.Combat.Contracts.CombatInput(side, climbAxis: 1));
                                village.World.arena.Presentation.Draw(.025f); traversal.RefreshPresentation();
                                Assert.That(player.WallClimbing, Is.True);
                                var sprite = player.visual.sprite;
                                Assert.That(AssetDatabase.GetAssetPath(sprite), Does.Contain(bodyId == village.OpeningBody ? "climb-v4/human/" : "climb-v4/ink/"));
                                float hand = player.visual.transform.localPosition.x * side + OpaqueRight(sprite) * player.visual.transform.localScale.x;
                                Assert.That(hand, Is.EqualTo(player.offset.x + player.size.x / 2).Within(.04f),
                                    sprite.name + " keeps the hand against the physical wall in either facing");
                                Assert.That(OpaqueHeight(sprite) * player.visual.transform.localScale.y, Is.InRange(1.9f, 2.2f),
                                    "Climbing stays close to the two-unit standing body height");
                            }
                        }
                      foreach (string bodyId in new[] { village.OpeningBody, village.VillageBody })
                      {
                          driver.Reset(); village.SetHeroBody(bodyId);
                          player.Position = savedPosition; player.grounded = true; player.velocity = Vector2.zero;
                          var visuals = JsonUtility.FromJson<WallClimbVisuals>(Resources.Load<TextAsset>("ApprovedTraversal/ClimbV4").text);
                          visuals.Load();
                          var clip = bodyId == village.OpeningBody ? visuals.humanLedge : visuals.inkLedge;
                          // Sample the final grounded recovery using the real simulation clock and native presenter.
                          typeof(CombatPlayer).GetProperty("WallLedgeStartedAt").SetValue(player, village.World.arena.Now - clip.Duration + .02f);
                          village.World.arena.Presentation.Draw(.01f); traversal.RefreshPresentation();
                          Assert.That(traversal.CurrentMotion, Is.EqualTo("wall_ledge"));
                          var sprite = player.visual.sprite;
                          Assert.That(OpaqueHeight(sprite) * player.visual.transform.localScale.y, Is.InRange(1.95f, 2.1f),
                              bodyId + " finishes at the standing body height");
                          float foot = visuals.FootOffset(sprite) * player.visual.transform.localScale.y + player.visual.transform.localPosition.y;
                          Assert.That(foot, Is.EqualTo(0).Within(.005f), "Grounded recovery plants the feet");
                          var at = player.visual.transform.localPosition; var scale = player.visual.transform.localScale;
                          traversal.RefreshPresentation();
                          Assert.That(player.visual.transform.localPosition, Is.EqualTo(at), "Repeated draws do not accumulate offsets");
                          Assert.That(player.visual.transform.localScale, Is.EqualTo(scale));
                          driver.Step(.04f); village.World.arena.Presentation.Draw(.04f); traversal.RefreshPresentation();
                          Assert.That(player.visual.transform.localScale, Is.EqualTo(Vector3.one), "Native frames regain their size after the ledge");
                      }
                    }
                }
                finally
                {
                    Object.Destroy(wall);
                    player.Position = savedPosition; player.velocity = Vector2.zero;
                    Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
                    traversal.RefreshPresentation();
                }
                village.SetHeroBody(village.OpeningBody);

                // Dash afterimages: untinted copies of the human frames, not ink silhouettes.
                player.UseTestInput = true;
                for (int f = 0; f < 5; f++) yield return null;
                player.QueueTestInput(dash: true);
                SpriteRenderer echo = null;
                for (int f = 0; f < 60 && !echo; f++)
                {
                    yield return null;
                    // Afterimages are DontSave objects, which FindObjectsByType skips.
                    echo = Resources.FindObjectsOfTypeAll<SpriteRenderer>().FirstOrDefault(r => r && r.name == "Approved dash afterimage" && r.gameObject.scene.IsValid());
                }
                Assert.That(echo, Is.Not.Null, "the dash leaves afterimages");
                Assert.That(AssetDatabase.GetAssetPath(echo.sprite), Does.Contain("opening/human-hero/"), "the afterimage is the human frame");
                Assert.That(new Vector3(echo.color.r, echo.color.g, echo.color.b), Is.EqualTo(Vector3.one), "no ink tint on the human body");

                // Trapped at the well: the hero glances away and back in turn without trembling, and ends facing as it began.
                player.UseTestInput = false; player.facing = 1;
                village.StartHeroFear(.7f, .2f);
                var seen = new System.Collections.Generic.HashSet<int>(); bool shook = false;
                for (float t = Time.unscaledTime; Time.unscaledTime - t < .6f;)
                { yield return null; seen.Add(player.facing); shook |= Mathf.Abs(player.visual.transform.localPosition.x) > 0; }
                Assert.That(seen, Is.EquivalentTo(new[] { -1, 1 }), "both ways");
                Assert.That(shook, Is.False, "no trembling");
                for (float t = Time.unscaledTime; Time.unscaledTime - t < .4f;) yield return null;
                Assert.That(village.HeroFearing, Is.False); Assert.That(player.facing, Is.EqualTo(1));
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator VillageJumpFeetStayWithThePhysicalBodyAndLandingFinishesStanding()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            try
            {
                StartingVillageWorld village = null;
                for (int f = 0; f < 600 && !(village && village.Ready); f++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
                Assert.That(village && village.Ready, Is.True);
                var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
                yield return null; yield return null;
                gui.GetType().GetMethod("SkipOpening", Hidden).Invoke(gui, null);
                var active = gui.GetType().GetField("storyActive", Hidden);
                for (int i = 0; i < 30 && (bool)active.GetValue(gui); i++) gui.GetType().GetMethod("AdvanceStory", Hidden).Invoke(gui, null);
                yield return null;
                var arena = village.World.arena; var player = arena.Player;
                var floor = new GameObject("Village jump test floor"); floor.transform.position = new Vector3(-500, 99.5f, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
                using (var driver = arena.AcquireManualSimulation())
                    foreach (var id in new[] { village.OpeningBody, village.VillageBody })
                    {
                        driver.Reset(); village.SetHeroBody(id);
                        player.Position = new Vector2(-500, 100.003f); player.velocity = Vector2.zero; player.grounded = true;
                        var set = System.Array.Find(arena.characterAnimations.sets, s => s.form == id);
                        float idleFoot = BootOffsetPixels(set.Find("idle").sprites[0]);
                        // Check every imported airborne drawing, including frames skipped by a short physical fall.
                        foreach (var phase in new[] { "jump_start", "jump_rise", "jump_apex", "fall_start", "fall_loop" })
                            foreach (var sprite in set.Find(phase).sprites)
                                Assert.That(BootOffsetPixels(sprite) - idleFoot, Is.InRange(-.01f, 3.01f), id + "/" + phase + "/" + sprite.name + " must not add a second jump");
                        Assert.That(set.Find("land").sprites.Last().name, Is.EqualTo("F12"), "Recover to standing before idle");
                        bool airborne = false, landed = false;
                        var seen = new System.Collections.Generic.HashSet<string>();
                        for (int tick = 0; tick < 160; tick++)
                        {
                            driver.Step(1f / 120, new Deviant.Combat.Contracts.CombatInput(0, tick == 2 ? Deviant.Combat.Contracts.CombatButtons.Jump : Deviant.Combat.Contracts.CombatButtons.None));
                            arena.Presentation.Draw(1f / 120); seen.Add(arena.Presentation.CurrentClip);
                            airborne |= !player.grounded;
                            if (!player.grounded)
                                Assert.That(BootOffsetPixels(player.visual.sprite) - idleFoot, Is.InRange(-.01f, 3.01f), id + " runtime foot alignment");
                            if (airborne && player.grounded) { landed = true; Assert.That(player.visual.transform.localScale, Is.EqualTo(Vector3.one)); }
                        }
                        Assert.That(airborne && landed, Is.True, id + " jumps and lands in StartingVillage");
                        Assert.That(seen, Does.Contain("jump_rise")); Assert.That(seen, Does.Contain("land"));
                    }
                Object.Destroy(floor);
            }
            finally { Chapter1ProfileHost.UseStore(null); }
            yield return new ExitPlayMode();
        }

        static float BootOffsetPixels(Sprite sprite)
        {
            var texture = sprite.texture; var pixels = texture.GetPixels32(); var rect = sprite.rect;
            for (int y = (int)rect.y; y < rect.yMax; y++)
                for (int x = (int)rect.x; x < rect.xMax; x++)
                {
                    var c = pixels[y * texture.width + x];
                    if (c.a > 200 && c.r > 190 && c.g > 110 && c.g < 215 && c.b < 90)
                        return y - rect.y - sprite.pivot.y;
                }
            Assert.Fail("Missing boots in " + sprite.name); return 0;
        }

        static float OpaqueRight(Sprite sprite)
        {
            var pixels = sprite.texture.GetPixels32(); var rect = sprite.rect;
            for (int x = (int)rect.xMax - 1; x >= rect.x; x--)
                for (int y = (int)rect.y; y < rect.yMax; y++)
                    if (pixels[y * sprite.texture.width + x].a >= 128)
                        return (x + 1 - rect.x - sprite.pivot.x) / sprite.pixelsPerUnit;
            Assert.Fail("Empty wall sprite " + sprite.name); return 0;
        }

    }
}
