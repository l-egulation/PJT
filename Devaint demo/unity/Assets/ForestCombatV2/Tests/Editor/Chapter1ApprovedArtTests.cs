using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using Stage1VillageExperiment;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1ApprovedArtTests
    {
        // The second jump steps on a puff of air (user choice 2026-11-05, A 「뭉게 발판」): it appears where the feet were,
        // stays there while the hero rises and is gone after its 13 frames.
        [UnityTest] public IEnumerator TheSecondJumpLeavesAPuffWhereTheFeetWere()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null; yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            var arena=world.arena; var p=arena.Player;
            var art=Deviant.Combat.DoubleJumpPuff.Frames();
            Assert.That(art,Has.Length.EqualTo(13).And.All.Not.Null,"the 13 frames are deployed");
            Assert.That(art[0].pixelsPerUnit,Is.EqualTo(64));Assert.That(art[0].texture.filterMode,Is.EqualTo(FilterMode.Point));
            var floor=new GameObject("Double jump floor");floor.transform.position=new Vector3(-500,99.5f,0);
            floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);floor.AddComponent<ArenaSurface>();Physics2D.SyncTransforms();
            Deviant.Combat.DoubleJumpPuff Puff()=>Object.FindAnyObjectByType<Deviant.Combat.DoubleJumpPuff>();
            using(var driver=arena.AcquireManualSimulation())
            {
                p.Position=new Vector2(-500,100.003f);p.velocity=Vector2.zero;p.grounded=true;
                Vector2 feet=default;Deviant.Combat.DoubleJumpPuff puff=null;
                for(int tick=0;tick<40;tick++)
                {
                    var buttons=tick==2||tick==30?CombatButtons.Jump:CombatButtons.None;
                    if(tick==30)feet=p.Position;
                    driver.Step(1f/120,new CombatInput(0,buttons));arena.Presentation.Draw(1f/120);
                    if(tick==2)Assert.That(Puff(),Is.Null,"the first jump kicks off the ground, no puff");
                    if(tick==30){puff=Puff();Assert.That(puff,Is.Not.Null,"the second jump leaves a puff");}
                }
                Assert.That(Vector2.Distance(puff.transform.position,feet),Is.LessThan(.01f),"where the feet were");
                Assert.That(p.Position.y,Is.GreaterThan(puff.transform.position.y+.5f),"the hero rises away from it");
                for(int tick=0;tick<80;tick++){driver.Step(1f/120,new CombatInput(0,CombatButtons.None));yield return null;if(!puff)break;}
                Assert.That(puff==null,Is.True,"gone after its frames");
            }
            Object.Destroy(floor);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator AirDashKeepsNativeScaleThroughFallAndLanding()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null; yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            var arena=world.arena; var p=arena.Player;
            var floor=new GameObject("Air dash regression floor");
            floor.transform.position=new Vector3(-500,99.5f,0);
            floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);floor.AddComponent<ArenaSurface>();
            Physics2D.SyncTransforms();
            var trace=new System.Text.StringBuilder();
            using(var driver=arena.AcquireManualSimulation())
            {
                p.Position=new Vector2(-500,100.003f);p.velocity=Vector2.zero; p.grounded=true;
                for(int tick=0;tick<150;tick++)
                {
                    var buttons=tick==2?CombatButtons.Jump:tick==20?CombatButtons.Dash:CombatButtons.None;
                    driver.Step(1f/120,new CombatInput(1,buttons));arena.Presentation.Draw(1f/120);
                    world.GetComponent<Chapter1TraversalPresentation>().RefreshPresentation();
                    var sprite=p.visual.sprite;
                    Assert.That(sprite,Is.Not.Null);
                    Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32),arena.Presentation.CurrentClip);
                    // The jump stretch (JumpMotionPresentation) may lengthen the body in the air; a dash keeps it native.
                    if(p.Dashing) Assert.That(p.visual.transform.localScale,Is.EqualTo(Vector3.one));
                    else Assert.That(p.visual.transform.localScale.y,Is.InRange(.83f,1.17f));
                    if(p.Dashing) Assert.That(UnityEditor.AssetDatabase.GetAssetPath(sprite),
                        Does.Contain("ApprovedTraversal/jump/"),"Air dash must not switch to a compressed ground pose");
                    trace.AppendLine(tick+" "+arena.Presentation.CurrentClip+" "+UnityEditor.AssetDatabase.GetAssetPath(sprite)+" "+sprite.bounds.size+" grounded="+p.grounded);
                }
            }
            System.IO.Directory.CreateDirectory("../outputs/air-dash");
            System.IO.File.WriteAllText("../outputs/air-dash/trace.txt",trace.ToString());
            Object.Destroy(floor);
            yield return new ExitPlayMode();
        }

        // The combat stage draws the approved 32px lawn and wood, the separated native background and the seeded sky
        // (docs/development/gameplay/village/stage1-village-experiment.md); colliders and the old GameObjects stay.
        [UnityTest] public IEnumerator ApprovedLawnBackgroundAndSkyReplaceTheCombatStageArt()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            yield return new EnterPlayMode(); yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            var village=world.GetComponent<VillagePresentation>();
            for(int i=0;i<300&&(!world.Ready||!village.Ready);i++)yield return null;
            var art=village.CombatArt;
            Assert.That(art&&art.Ready,Is.True);
            var data=world.Data;
            Assert.That(art.TerrainCells,Is.EqualTo(data.ground.Count(g=>g!=0)));
            Assert.That(art.WoodPieces,Is.EqualTo(data.platforms.Sum(p=>Mathf.Max(1,p.w/32))));
            Assert.That(art.TerrainRoot.localPosition.y,Is.EqualTo(Stage1CombatArt.SurfaceLift),"tile tops sit 8px above the walking line");
            foreach(var name in new[]{"Paired reference terrain - shared contacts and complete artwork","Sky follows camera"})
                foreach(var renderer in world.transform.Find(name).GetComponentsInChildren<Renderer>(true))
                    Assert.That(renderer.enabled,Is.False,name);
            Assert.That(art.BackgroundRoot.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(15),"three bands, five mirrored copies each");
            Assert.That(art.Atmosphere,Is.Not.Null);
            Assert.That((Color32)world.View.backgroundColor,Is.EqualTo(Stage1CombatArt.Paper));
            int first=System.Array.FindIndex(data.ground,g=>g!=0);
            var tile=art.TerrainRoot.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>().GetSprite(new Vector3Int(first%data.cols,-(first/data.cols)-1,0));
            Assert.That(tile,Is.Not.Null,"first solid cell has a lawn tile");
            Assert.That(tile.texture.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(tile.pixelsPerUnit,Is.EqualTo(32));
            var vanishing=world.GetComponent<Chapter1VanishingPlatforms>();
            if(vanishing)
            {
                vanishing.CollectArt();
                foreach(var (index,_) in world.VanishingSurfaces)
                    Assert.That(vanishing.ArtPiecesOf(index),Is.GreaterThan(0),"vanishing deck "+index+" fades the new wood");
            }
            // The background bands follow the camera with their own parallax and stay pixel-snapped.
            var copies=art.BackgroundRoot.GetComponentsInChildren<MeshRenderer>();
            var start=new Vector3[copies.Length];
            for(int i=0;i<copies.Length;i++)start[i]=copies[i].transform.position;
            world.View.transform.position+=new Vector3(7,0,0);
            art.Refresh();
            bool moved=false;
            for(int i=0;i<copies.Length;i++)
            {
                var p=copies[i].transform.position;moved|=p!=start[i];
                Assert.That(Mathf.Abs(p.x*32-Mathf.Round(p.x*32)),Is.LessThan(1e-3f),"pixel-snapped "+copies[i].name);
            }
            Assert.That(moved,Is.True,"the bands follow the camera");
            // Boss approach and boss room: the pixel rear art replaces the old backdrop's renderers.
            Assert.That(art.BossRoot,Is.Not.Null);
            foreach(var name in new[]{"Clock room rear art","Clock approach rear art"})
            {
                var old=world.transform.Find(name);
                if(old)foreach(var renderer in old.GetComponentsInChildren<Renderer>(true))Assert.That(renderer.enabled,Is.False,name);
            }
            ForestRoom boss=null;foreach(var r in data.rooms)if(r!=null&&r.kind=="boss")boss=r;
            float bossFloor=-(boss.y+boss.entryY);
            var colonnade=art.BossRoot.Find("approach-colonnade").GetComponent<SpriteRenderer>();
            Assert.That(colonnade.drawMode,Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(colonnade.bounds.max.x,Is.EqualTo(boss.x).Within(.02f),"the colonnade runs up to the boss room");
            Assert.That(colonnade.bounds.size.x,Is.LessThanOrEqualTo(Stage1CombatArt.ColonnadeLength+.02f));
            Assert.That(colonnade.bounds.min.y,Is.EqualTo(bossFloor).Within(.02f));
            var towers=art.BossRoot.Find("boss-room-towers").GetComponent<SpriteRenderer>();
            Assert.That(towers.bounds.min.x,Is.EqualTo(boss.x).Within(.02f));Assert.That(towers.bounds.min.y,Is.EqualTo(bossFloor).Within(.02f));
            Assert.That(towers.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            yield return new ExitPlayMode();
        }

        [Test] public void NativeAssetsAndApprovedDurationsAreComplete()
        {
            var art = ApprovedTraversalArt.Load();
            Assert.That(art.jumps.Length, Is.EqualTo(13));
            var clips = art.jumps.SelectMany(s => s.clips).Concat(new[]{art.wallContact,art.wallKick,art.portalDraw,art.portalMelt});
            foreach (var clip in clips) foreach(var sprite in clip.sprites)
            {
                Assert.That(sprite,Is.Not.Null,clip.name);
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            }
            var data=JsonUtility.FromJson<ManualCheckpointData>(Resources.Load<TextAsset>("ManualCheckpoint").text);
            // Sped up after the 2026-11-05 playtest 「너무 느려」; the motions keep their frames and play faster.
            Assert.That(data.installSeconds,Is.EqualTo(2f));Assert.That(data.enterSeconds,Is.EqualTo(.64f));Assert.That(data.appearSeconds,Is.EqualTo(.8f));
            foreach(var motion in new[]{data.installMotion,data.enterMotion,data.appearMotion})
                foreach(var frame in motion.frames) Assert.That(Resources.Load<Sprite>(frame),Is.Not.Null,frame);
        }

        [UnityTest] public IEnumerator VillageWallAndPortalPresentationFollowRealPlayerState()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=42;
            Object.FindAnyObjectByType<VillagePixelViewport>().enableInBatchMode=true;
            yield return new EnterPlayMode(); yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            var village=world.GetComponent<VillagePresentation>();
            for(int i=0;i<600&&(!world.Ready||!village.Ready||!world.GetComponent<VillagePixelViewport>().Ready);i++)yield return null;
            Assert.That(village.Ready,Is.True);Assert.That(village.PropCount,Is.GreaterThan(0));
            Chapter1DisplayAlignmentTests.AssertProjection(world.View,world.GetComponent<VillagePixelViewport>().DisplayRect);
            Assert.That(village.FarSampleRenderer,Is.Not.Null);
            Assert.That(world.GetComponent<VillageArtTransition>().Mode,Is.EqualTo(VillageArtMode.Sketch));
            Assert.That(world.GetComponent<VillagePixelViewport>().enabled,Is.True);
            foreach(var enemy in world.arena.enemies.OfType<FieldMouseEnemy>())
                Assert.That(enemy.distantIdleRange,Is.EqualTo(new Vector2(64,24)),"The village adapter must preserve the procedural scene's distant-idle policy");
            var colors=world.transform.Find("Original color terrain with registered paper drawings");
            var platforms=world.transform.Find("One-way cream stone decks");
            Assert.That(colors.gameObject.activeSelf,Is.False,"Fully transparent color terrain must not be submitted in pure Sketch");
            Assert.That(platforms.gameObject.activeSelf,Is.False);
            var transition=world.GetComponent<VillageArtTransition>();
            transition.SetDisplay(VillageArtMode.Color);village.RefreshForCapture();
            Assert.That(colors.gameObject.activeSelf&&platforms.gameObject.activeSelf,Is.True);
            Assert.That(village.ReferenceGroundRoot.activeSelf,Is.False,"Pure Color must not submit transparent sketch terrain");
            transition.SetDisplay(VillageArtMode.Blend,.5f);village.RefreshForCapture();
            Assert.That(colors.gameObject.activeSelf&&platforms.gameObject.activeSelf&&village.ReferenceGroundRoot.activeSelf,Is.True,"Both endpoints are required during compositing");
            village.SetReferenceGroundEnabled(false);transition.SetDisplay(VillageArtMode.Sketch);village.RefreshForCapture();
            Assert.That(colors.gameObject.activeSelf&&platforms.gameObject.activeSelf,Is.True,"Legacy sketch must remain available when complete reference art is disabled");
            village.SetReferenceGroundEnabled(true);village.RefreshForCapture();
            Assert.That(colors.gameObject.activeSelf,Is.False);
            foreach(var renderer in world.Visuals.GetComponentsInChildren<Renderer>()) Assert.That(renderer.enabled,Is.False,"No old forest terrain under the village art");
            foreach(var e in world.arena.enemies)if(e)e.aggressive=false;
            var p=world.arena.Player;
            var motion=world.GetComponent<Chapter1TraversalPresentation>();
            var portal=world.GetComponent<Chapter1ManualCheckpoint>();
            using(var driver=world.arena.AcquireManualSimulation())
            {
                void Step(float seconds)
                { for(int i=0;i<Mathf.CeilToInt(seconds*120);i++)driver.Step(1f/120); }
                driver.Reset();Step(.5f);world.RefreshCamera(0,true);village.RefreshForCapture();
                Capture(world.View,"village-start");
                Assert.That(p.TryInstallPortal(0),Is.True,p.CheckpointMessage);
                Step(1f);portal.RefreshPresentation();
                Assert.That(p.visual.sprite.name,Does.StartWith("F0"));
                Assert.That(p.visual.transform.localScale,Is.EqualTo(Vector3.one));
                Capture(world.View,"portal-draw");
                Assert.That(p.Receive(1,world.arena.enemies[0],1,false,null),Is.True);
                portal.RefreshPresentation();
                Assert.That(p.HasCheckpoint,Is.False);Assert.That(p.CheckpointBusy,Is.False);
                Assert.That(portal.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Checkpoint action").enabled,Is.False);
                driver.Reset();Step(.5f);p.SyncFacing(-1);
                Assert.That(p.TryInstallPortal(0),Is.True,p.CheckpointMessage);Step(2.05f);portal.RefreshPresentation();
                var destination=p.PortalPosition(0);
                var installed=portal.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Installed portal 1");
                Assert.That(installed.enabled&&installed.flipX,Is.True,"Left-facing portal art follows its installation");
                world.Warp(1);Step(.1f);
                Assert.That(p.TryInstallPortal(1),Is.True,p.CheckpointMessage);Step(2.05f);portal.RefreshPresentation();
                var far=portal.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Installed portal 2");
                Assert.That(far.enabled,Is.True);
                Assert.That(p.TryTravelThroughPortal(),Is.True,p.CheckpointMessage);
                var origin=p.Position;Step(.4f);portal.RefreshPresentation();
                Assert.That(p.Position,Is.EqualTo(origin));Assert.That(p.visual.color.r,Is.LessThan(1));
                world.RefreshCamera(0,true);village.RefreshForCapture();Capture(world.View,"portal-enter");
                Step(.5f);portal.RefreshPresentation();
                Assert.That(p.PortalPhase,Is.EqualTo(CheckpointPhase.Appearing));
                Assert.That(p.Position,Is.EqualTo(destination));Assert.That(p.Invulnerable,Is.True);
                Step(.05f);portal.RefreshPresentation();village.RefreshForCapture();Capture(world.View,"portal-appear");
                Step(.6f);portal.RefreshPresentation();
                Assert.That(p.visual.color,Is.EqualTo(Color.white));Assert.That(p.visual.transform.localPosition,Is.EqualTo(Vector3.zero));
                Assert.That(installed.enabled&&far.enabled,Is.True,"Both portals survive the crossing");
                driver.Reset();portal.RefreshPresentation();Assert.That(installed.enabled||far.enabled,Is.False);

                // Empty space above the generated map isolates left/right contact
                // from the room topology while retaining the real collision solver.
                var wall=new GameObject("Approved wall art test");wall.transform.position=new Vector3(204,200,0);
                wall.AddComponent<BoxCollider2D>().size=new Vector2(2,80);wall.AddComponent<ArenaSurface>();
                foreach(int side in new[]{1,-1})
                {
                    driver.Reset();p.Position=new Vector2(side>0?202.34f:205.66f,200);
                    p.velocity=Vector2.zero;p.grounded=false;p.SyncFacing(side);Physics2D.SyncTransforms();
                    driver.Step(1f/120,new CombatInput(side,climbAxis:1));world.arena.Presentation.Draw(1f/120);motion.RefreshPresentation();
                    Assert.That(p.WallClimbing,Is.True);Assert.That(motion.CurrentMotion,Is.EqualTo("wall_contact"));
                    Assert.That(p.visual.flipX,Is.EqualTo(side<0));
                    Assert.That(p.visual.sprite,Is.Not.Null);
                    Assert.That(p.visual.sprite.texture.width,Is.EqualTo(460),"Approved cleaned wall frame is used");
                    Assert.That(p.visual.transform.localScale,Is.EqualTo(new Vector3(1.08f,1.08f,1)),"Wall contact uses its body correction without inheriting jump stretch");
                    Assert.That(world.arena.Presentation.ActiveJumpAfterimages,Is.Zero);
                    driver.Step(1f/120,new CombatInput(side,CombatButtons.Jump,climbAxis:1));world.arena.Presentation.Draw(1f/120);motion.RefreshPresentation();
                    Assert.That(p.visual.transform.localScale,Is.EqualTo(Vector3.one),"Wall kick owns its native pose");
                    Assert.That(world.arena.Presentation.ActiveJumpAfterimages,Is.Zero);
                    Assert.That(motion.CurrentMotion,Is.EqualTo("wall_kick"));Assert.That(p.visual.flipX,Is.EqualTo(side<0));
                    Assert.That(p.VelocityY,Is.GreaterThan(19));Assert.That(p.WallJumpHeading,Is.EqualTo(-side));
                    p.stunUntil=world.arena.Now+1;driver.Step(1f/120);motion.RefreshPresentation();
                    Assert.That(motion.CurrentMotion,Is.Null,"Stun interrupts art along with wall movement");
                }
                Object.Destroy(wall);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator JumpLandingReturnsToNativeScaleInTheActualChapter()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null; yield return null;
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var arena = world.arena; var player = arena.Player;
            var floor = new GameObject("Landing scale test floor");
            floor.transform.position = new Vector3(-500, 99.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            using (var driver = arena.AcquireManualSimulation())
            {
                player.Position = new Vector2(-500, 100.003f);
                player.velocity = Vector2.zero; player.grounded = true;
                bool airborne = false, landed = false;
                for (int tick = 0; tick < 160; tick++)
                {
                    // Holding a direction lands straight into the run clip, the case that read as the body growing.
                    driver.Step(1f / 120, new CombatInput(1, tick == 2 ? CombatButtons.Jump : CombatButtons.None));
                    arena.Presentation.Draw(1f / 120);
                    airborne |= !player.grounded;
                    if (airborne && player.grounded)
                    {
                        landed = true;
                        Assert.That(player.visual.transform.localScale, Is.EqualTo(Vector3.one),
                            "Every grounded frame stays at the native body size, including the first landing frame.");
                        Assert.That(arena.Presentation.ActiveJumpAfterimages, Is.Zero, "No fall echo lingers over the landed body.");
                    }
                }
                Assert.That(airborne && landed, Is.True, "The actual player jumps and lands.");
                var smoothing = player.GetComponent<ActorRenderInterpolation>();
                Assert.That(smoothing, Is.Not.Null, "The chapter draws the 120 Hz body between fixed steps.");
                Assert.That(player.visual.transform.parent.name, Is.EqualTo("Render interpolation only"));
                Assert.That(world.CameraPresentationPosition, Is.Not.Null, "The camera follows the drawn position, not the raw step.");
            }
            Object.Destroy(floor);
            yield return new ExitPlayMode();
        }

        static void Capture(Camera camera,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var texture=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                string output=System.IO.Path.GetFullPath("../outputs/approved-art");System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),image.EncodeToPNG());
            }
            finally {camera.targetTexture=previous;RenderTexture.active=active;texture.Release();Object.Destroy(image);Object.Destroy(texture);}
        }
    }
}
