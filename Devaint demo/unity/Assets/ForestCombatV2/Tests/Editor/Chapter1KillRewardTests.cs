using System.Collections;
using System.Linq;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Trial table: normal 6-10G only; elite 200-280G, 3 random inks + 1 special ink, 1 stamp, palette 50%;
    // boss 600-720G, 5 random inks + 1 special ink, 1 stamp, 1 palette. Form inks follow the chosen blueprints.
    public sealed class Chapter1KillRewardTests
    {
        [TestCase(GroundLootArt.Gold, 12)]
        [TestCase(GroundLootArt.Pigment, 16)]
        [TestCase(GroundLootArt.WeaponStamp, 16)]
        [TestCase(GroundLootArt.SkillStamp, 16)]
        public void ApprovedPickupArtLoadsAtNativePixelScale(string id, int width)
        {
            var sprite = GroundLootArt.Load(id);
            Assert.That(sprite, Is.Not.Null, id + " must load instead of the square fallback");
            Assert.That(sprite.rect.width, Is.EqualTo(width), id);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16), id);
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), id);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), id);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), id);
        }

        [Test]
        public void EveryInkPartGradeLoadsItsOwnApprovedSprite()
        {
            foreach (var color in new[] { "red", "yellow", "green", "blue", "purple" })
            for (int grade = 1; grade <= 4; grade++)
            {
                var id = GroundLootArt.GradedInk(color, grade);
                var sprite = GroundLootArt.Load(id);
                Assert.That(sprite, Is.Not.Null, id + " must not fall back to ungraded ink");
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16), id);
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), id);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), id);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), id);
            }
        }

        static Chapter1Rewards Rewards() => Object.FindAnyObjectByType<Chapter1Rewards>();
        [UnityTearDown] public IEnumerator Cleanup()
        { if(Application.isPlaying) yield return new ExitPlayMode(); }

        // EnterPlayMode must be yielded by the test body itself, so setup is inlined in each test.
        static ForestCombatWorld ReadyWorld()
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            return world && world.Ready && world.arena.GetComponent<InventoryRuntimeHost>() && Rewards() ? world : null;
        }

        [UnityTest] public IEnumerator NormalKillsCanDropColoredInkAndElitesNeverDropPalettes()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            yield return null; yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            var rewards = Rewards(); var run = rewards.Run;
            var bag = world.arena.GetComponent<InventoryRuntimeHost>();
            foreach (var enemy in world.arena.enemies) if (enemy) enemy.aggressive = false;
            Assert.That(world.arena.enemies.All(e => !e || e.GetComponent<InkDropSource>()), Is.True, "fixed per-enemy ink tables are disabled in chapter 1");

            var normal = world.arena.enemies.First(e => e && !e.Dead && Chapter1EnemyRole.Of(e) == Chapter1EnemyRole.Normal);
            normal.health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 1; frame++) yield return null;
            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(bag.Drops.Count, Is.Zero, "colored ink uses the palette inventory pickup route");
            var normalGold = rewards.Pickups.Single(p => p.Kind == Chapter1RewardKind.Gold);
            Assert.That(normalGold.GoldAmount, Is.InRange(6, 10));
            Assert.That(rewards.Pickups.Count(p=>p.Kind==Chapter1RewardKind.ColoredInk),Is.InRange(0,1));
            int normalAmount = normalGold.GoldAmount;
            var goldArt = normalGold.GetComponent<SpriteRenderer>();
            Assert.That(goldArt.sprite.name, Is.EqualTo("gold-flat-small"));
            Assert.That(goldArt.sprite.rect.size, Is.EqualTo(new Vector2(12, 12)));
            Assert.That(goldArt.sprite.pixelsPerUnit, Is.EqualTo(16));
            Assert.That(normalGold.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1), "only one coin is drawn before the burst");
            Assert.That(goldArt.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(normalGold.Collected, Is.True, "gold is credited immediately without contact");
            Assert.That(normalGold.TryCollect(), Is.False, "already credited gold cannot pay twice");
            Assert.That(run.Economy.Gold, Is.EqualTo(normalAmount));
            // Render-frame counts do not guarantee a second of combat time in fast batch runs.
            using(var simulation=world.arena.AcquireManualSimulation())
            { simulation.Step(1.5f); yield return null; yield return null; }
            Assert.That(normalGold == null, Is.True, "the floor visual disappears after its powder has bounced and settled");

            var elite = world.arena.enemies.Single(e => e && Chapter1EnemyRole.Of(e) == Chapter1EnemyRole.Elite);
            elite.health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 2; frame++) yield return null;
            var dropped = rewards.Pickups.ToArray();
            Assert.That(dropped.Single(p => p.Kind == Chapter1RewardKind.Gold).GoldAmount, Is.InRange(200, 280));
            Assert.That(dropped.Count(p => p.Kind == Chapter1RewardKind.InkPart), Is.Zero, "elites no longer drop colour ink parts");
            Assert.That(dropped.Count(p=>p.Kind==Chapter1RewardKind.ColoredInk),Is.EqualTo(3));
            // No blueprint is chosen yet, so a weapon stamp has no fitting kind; a skill stamp still drops.
            Assert.That(dropped.Count(p => p.Kind == Chapter1RewardKind.SkillStamp || p.Kind == Chapter1RewardKind.WeaponStamp), Is.InRange(0, 1));
            // Chapter 1 has no bag inks: weapon and skill stamps replace the form and special-attack inks.
            Assert.That(bag.Drops.Count, Is.Zero, "elites drop no bag inks");
            // Locals captured by lambdas do not survive EnterPlayMode's domain reload, so check with a loop.
            foreach (var stamp in dropped)
                if (stamp.Kind == Chapter1RewardKind.SkillStamp)
                {
                    Assert.That(stamp.Stamp, Is.Not.Null);
                    Assert.That(stamp.Stamp.Skill, Is.InRange(1, 3));
                    Assert.That(Vector2.Distance(stamp.transform.position, elite.Position), Is.LessThan(4), "stamps lie near the elite");
                }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator GoldCreditsOnceThenPopsIntoLocalPowderAndPausesWithCombat()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            yield return new EnterPlayMode();
            for (int frame=0; frame<600 && !ReadyWorld(); frame++) yield return null;
            yield return null;yield return null;
            var world=ReadyWorld();Assert.That(world,Is.Not.Null);
            var arena=world.arena;var rewards=Rewards();var run=rewards.Run;
            foreach(var enemy in arena.enemies)if(enemy)enemy.aggressive=false;
            using(var sim=arena.AcquireManualSimulation())
            {
                int before=run.Economy.Gold;
                var drop=rewards.Grant(Chapter1EnemyRole.Normal,arena.Player.Position+Vector2.right*30);
                var coin=rewards.Pickups.Single(p=>p.Kind==Chapter1RewardKind.Gold);
                Assert.That(Vector2.Distance(arena.Player.Center,coin.transform.position),Is.GreaterThan(10));
                Assert.That(run.Economy.Gold,Is.EqualTo(before+drop.Gold));
                Assert.That(coin.Collected,Is.True);Assert.That(coin.Ready,Is.False,"settling cannot delay gold credit");
                Assert.That(coin.TryCollect(),Is.False);
                coin.SetGold(run,drop.Gold);
                Assert.That(run.Economy.Gold,Is.EqualTo(before+drop.Gold),"repeated initialization cannot credit twice");
                var body=coin.GetComponent<SpriteRenderer>();Assert.That(body.enabled,Is.True);
                var powder=coin.CurrencyEffect;
                sim.Step(.15f);yield return null;
                Assert.That(powder.HasBurst,Is.False);
                var previousPosition=coin.transform.position;
                using(var pause=arena.Controls.OpenScope("gold powder pause test"))
                {
                    pause.PauseSimulation();float now=arena.Now;
                    Assert.Throws<System.InvalidOperationException>(()=>sim.Step(2));
                    yield return null;yield return null;
                    Assert.That(arena.Now,Is.EqualTo(now));
                    Assert.That(coin.transform.position,Is.EqualTo(previousPosition));
                    Assert.That(powder.HasBurst,Is.False);
                }
                sim.Step(.22f);yield return null;yield return null;
                Assert.That(powder.HasBurst,Is.True);Assert.That(powder.ActiveGrains,Is.GreaterThan(0));
                Assert.That(powder.BurstNotifications,Is.EqualTo(1));
                Assert.That(Vector2.Distance(arena.Player.Center,coin.transform.position),Is.GreaterThan(10),"the effect never homes toward the player");
                sim.Step(.95f);yield return null;yield return null;
                Assert.That(coin==null,Is.True);
                Assert.That(run.Economy.Gold,Is.EqualTo(before+drop.Gold));
                var other=new GameObject("non-gold reward control").AddComponent<Chapter1RewardPickup>();
                other.Initialize(world,run.Stash,Chapter1RewardKind.SkillStamp,null,arena.Player.Position+Vector2.right*30);
                int stamps=run.Stash.Stamps.Count;sim.Step(2);yield return null;
                Assert.That(other.Collected,Is.False);Assert.That(other.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1));
                Assert.That(run.Stash.Stamps.Count,Is.EqualTo(stamps));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator BossRewardsAreGuaranteedAndWeaponStampsFollowTheChosenBlueprints()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>().sketchWeapons = false; // legacy loot/bag path
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !ReadyWorld(); frame++) yield return null;
            yield return null; yield return null;
            var world = ReadyWorld();
            Assert.That(world, Is.Not.Null);
            var rewards = Rewards();
            var bag = world.arena.GetComponent<InventoryRuntimeHost>();
            var player = world.arena.Player;
            var offer = world.GetComponent<Chapter1StartingBlueprints>();
            player.Position = offer.OfferPosition(1);
            Assert.That(offer.TryChoose(1), Is.True);
            CollectionAssert.AreEqual(new[] { "blunt" }, rewards.ChosenBlueprintKinds());
            var pool = rewards.InkPool();
            CollectionAssert.IsSubsetOf(new[] { "form.shield", "form.greatsword" }, pool);
            Assert.That(pool.Any(id => id == "form.base_sword" || id == "form.hammer" || id == "form.launcher" || id == "form.whip"), Is.False);
            Assert.That(rewards.InkPool(true).All(id => id.StartsWith("special.")), Is.True);

            // Stage stamp limit (2026-11-06): elites and bosses always drop a stamp until the stage has dropped 3.
            var economy = rewards.Run.Economy;
            Assert.That(economy.StageStampLimit, Is.EqualTo(3));
            Assert.That(economy.StageStampsDropped(Deviant.Run.StampStages.Chapter1), Is.Zero, "nothing has dropped a stamp yet");
            var elite = rewards.Grant(Chapter1EnemyRole.Elite, player.Position + Vector2.right * 6);
            Assert.That(elite.WeaponStamps + elite.SkillStamps, Is.EqualTo(1), "the elite stamp is guaranteed");
            int stamps = 1;
            for (int i = 0; i < 30; i++)
            {
                var boss = rewards.Grant(Chapter1EnemyRole.Boss, player.Position + Vector2.right * 6);
                Assert.That(boss.Gold, Is.InRange(600, 720), "gold still pays after the stamp limit");
                Assert.That(boss.Inks + boss.SpecialInks, Is.Zero, "no bag inks in chapter 1");
                Assert.That(boss.Parts, Is.Zero);
                Assert.That(boss.WeaponStamps + boss.SkillStamps, Is.EqualTo(i < 2 ? 1 : 0), "guaranteed until the third stamp, then none");
                stamps += boss.WeaponStamps + boss.SkillStamps;
            }
            Assert.That(stamps, Is.EqualTo(3));
            Assert.That(economy.StageStampsDropped(Deviant.Run.StampStages.Chapter1), Is.EqualTo(3));
            Assert.That(economy.StageStampsLeft(Deviant.Run.StampStages.Chapter2), Is.EqualTo(3), "chapter 2 counts its own stage");
            Assert.That(rewards.Pickups.Count(p => p.Kind == Chapter1RewardKind.WeaponStamp || p.Kind == Chapter1RewardKind.SkillStamp), Is.EqualTo(3));
            Assert.That(bag.Drops.Count, Is.Zero);
            var stampKinds = rewards.Pickups.Where(p => p.Kind == Chapter1RewardKind.WeaponStamp).Select(p => p.BlueprintKind).Distinct().ToArray();
            CollectionAssert.IsSubsetOf(stampKinds, new[] { "blunt" }, "weapon stamps fit only the chosen hammer blueprint");
            var stampForms = rewards.Pickups.Where(p => p.Kind == Chapter1RewardKind.WeaponStamp).Select(p => p.Stamp.FormId).Distinct().ToArray();
            CollectionAssert.IsSubsetOf(stampForms, new[] { "shield", "greatsword" });
            yield return new ExitPlayMode();
        }

        // A weapon stamp names one of the carried blueprint's other forms; skill stamps spread over the three variants;
        // a guaranteed weapon stamp without a carried blueprint becomes a skill stamp instead of vanishing.
        [Test] public void GuaranteedStampsFollowTheBlueprintOrFallBackToSkill()
        {
            var random = new System.Random(11); var blunt = new[] { "blunt" };
            var forms = new System.Collections.Generic.HashSet<string>(); var skills = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                var weapon = Chapter1Rewards.RollStageStamp(true, blunt, random);
                Assert.That(weapon.Kind, Is.EqualTo(Deviant.Run.StampKind.Weapon));
                Assert.That(weapon.BlueprintKind, Is.EqualTo("blunt"));
                forms.Add(weapon.FormId);
                var skill = Chapter1Rewards.RollStageStamp(false, blunt, random);
                Assert.That(skill.Kind, Is.EqualTo(Deviant.Run.StampKind.Skill));
                skills.Add(skill.Skill);
                Assert.That(Chapter1Rewards.RollStageStamp(true, new string[0], random).Kind, Is.EqualTo(Deviant.Run.StampKind.Skill));
            }
            CollectionAssert.AreEquivalent(new[] { "shield", "greatsword" }, forms);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, skills);
        }
    }
}
