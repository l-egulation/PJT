using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1ProceduralMapTests
    {
        const string Source="Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json";
        const string Artwork="Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json";
        const string Catalog="Assets/ForestCombatV2/Resources/Chapter1Rooms.json";
        const string Connectors="Assets/ForestCombatV2/Resources/Chapter1Connectors.json";

        static ForestData Generate(int seed)
        {
            var source=JsonUtility.FromJson<ForestData>(AssetDatabase.LoadAssetAtPath<TextAsset>(Source).text);
            var layout=JsonUtility.FromJson<RoundedTerrainLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>(Artwork).text);
            return Chapter1ProceduralMap.Generate(source,layout,seed);
        }

        [Test] public void SelectedHandoffKeepsAllCombatGeometryAndMonsterPositions()
        {
            var authored=JsonUtility.FromJson<Chapter1RoomCatalog>(Resources.Load<TextAsset>("Chapter1SelectedRooms").text);
            var catalog=JsonUtility.FromJson<Chapter1RoomCatalog>(Resources.Load<TextAsset>("Chapter1Rooms").text);
            Assert.That(authored.rooms.Length,Is.EqualTo(17));
            Assert.That(catalog.rooms.Length,Is.EqualTo(12));
            Assert.That(catalog.rooms.Any(r=>r.id=="sam_12"||r.id=="draft_room_staggered_eaves_13"||r.id=="draft_room_three_courts_15"),Is.False);
            Assert.That(catalog.rooms.Any(r=>r.name.Contains("두물길")),Is.True);
            foreach(var room in catalog.rooms)
            {
                var original=authored.rooms.Single(r=>r.id==room.id);
                Assert.That(room.width,Is.EqualTo(original.width));Assert.That(room.height,Is.EqualTo(original.height));
                Assert.That(room.terrain.Length,Is.EqualTo(original.terrain.Length));
                for(int i=0;i<room.terrain.Length;i++)
                    Assert.That(JsonUtility.ToJson(room.terrain[i]),Is.EqualTo(JsonUtility.ToJson(original.terrain[i])));
                Assert.That(room.monsters.Length,Is.EqualTo(original.monsters.Length));
                for(int i=0;i<room.monsters.Length;i++)
                {Assert.That(room.monsters[i].x,Is.EqualTo(original.monsters[i].x));Assert.That(room.monsters[i].y,Is.EqualTo(original.monsters[i].y));}
                Assert.That(room.decorations.Length,Is.EqualTo(original.decorations.Length));
                for(int i=0;i<room.decorations.Length;i++)
                    Assert.That(JsonUtility.ToJson(room.decorations[i]),Is.EqualTo(JsonUtility.ToJson(original.decorations[i])));
            }
        }

        [Test] public void AuthoredSceneryRetainsSizeLayerAndMirrorsAroundRoomWidth()
        {
            var item=new Chapter1RoomDecoration{assetId="village:phase2_tree",x=4,y=5,width=3,height=4,rotation=30,order=-17};
            var room=new ForestRoom{id=0,x=10,y=20,w=32,h=18,kind="combat",authoredDecorations=new[]{item}};
            var data=new ForestData{rooms=new[]{room}};
            var normal=Stage1VillageExperiment.VillageTeamScenery.Create(data).props.Single();
            Assert.That(normal.x,Is.EqualTo(14*32));Assert.That(normal.y,Is.EqualTo(25*32));
            Assert.That(normal.width,Is.EqualTo(3*32));Assert.That(normal.height,Is.EqualTo(4*32));
            Assert.That(normal.sortingOrder,Is.EqualTo(-17));Assert.That(normal.rotation,Is.EqualTo(30));Assert.That(normal.flipX,Is.False);
            room.mirrored=true;
            var mirrored=Stage1VillageExperiment.VillageTeamScenery.Create(data).props.Single();
            Assert.That(mirrored.x,Is.EqualTo((10+32-4)*32));Assert.That(mirrored.flipX,Is.True);Assert.That(mirrored.rotation,Is.EqualTo(-30));
            room.authoredDecorations=new Chapter1RoomDecoration[0];
            Assert.That(Stage1VillageExperiment.VillageTeamScenery.Create(data).props,Is.Empty,"intentionally empty scenery must not be randomly filled");
        }

        [Test] public void SeedsSelectEightDistinctCombatTemplatesAndConnectEveryRoom()
        {
            var templates=JsonUtility.FromJson<Chapter1RoomCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(Catalog).text);
            Assert.That(templates.rooms.Length,Is.EqualTo(12));
            var orders=new HashSet<string>();
            foreach(int seed in new[]{17,42,113,1409})
            {
                var data=Generate(seed);
                Assert.That(data.rooms.Count(r=>r.kind=="combat"),Is.EqualTo(Chapter1ProceduralMap.CombatRooms));
                Assert.That(Chapter1ProceduralMap.CombatRooms,Is.EqualTo(8));
                Assert.That(data.rooms.Count(r=>r.kind=="seal"&&r.mainPath),Is.EqualTo(1),"one seal challenge on the main route");
                Assert.That(data.rooms.Count(r=>r.kind=="chest"&&!r.mainPath),Is.EqualTo(1),"one locked chest bypass");
                Assert.That(data.rooms.Select(r=>r.sourceZone).Distinct().Count(),Is.EqualTo(data.rooms.Length));
                Assert.That(data.actors.Length,Is.GreaterThanOrEqualTo(80));
                Assert.That(data.rooms.Any(r=>r.kind=="shop"),Is.False);
                Assert.That(data.rooms.Count(r=>r.kind=="maintenance"),Is.EqualTo(1));
                Assert.That(data.rooms[9].kind,Is.EqualTo("boss"));
                Assert.That(data.rooms[9].x,Is.GreaterThan(data.rooms.Where(r=>r.kind=="combat").Max(r=>r.x)));
                Assert.That(data.passages.Length,Is.EqualTo(15));
                // Rooms 10-11 are the two bypasses (one is the locked chest room); each rejoins the main route.
                foreach(int bypass in new[]{10,11})
                {
                    Assert.That(new[]{"combat","chest"},Does.Contain(data.rooms[bypass].kind));
                    Assert.That(data.rooms[bypass].mainPath,Is.False);
                    Assert.That(data.passages.Count(p=>p.from==bypass&&data.rooms[p.to].mainPath),Is.EqualTo(1),"Bypass rooms rejoin the main route");
                }
                foreach(var room in data.rooms.Where(r=>r.kind=="combat"))
                {
                    Assert.That(room.sourceZone,Is.InRange(100,100+templates.rooms.Length-1));
                    Assert.That(data.actors.Count(a=>a.zone==room.id),Is.GreaterThanOrEqualTo(10));
                }
                orders.Add(string.Join(",",data.rooms.Select(r=>r.sourceZone)));
            }
            Assert.That(orders.Count,Is.GreaterThan(1),"A new seed should change the room arrangement.");
        }




        [Test] public void BranchShaftsHaveDroppableDoorLandingsAndSolidBottoms()
        {
            foreach(int seed in new[]{17,42,113,1409})
            {
                var data=Generate(seed);
                Assert.That(data.platforms.All(p=>!p.blockDropThrough),Is.True);
                foreach(var p in data.passages)
                foreach(int foot in new[]{p.y,p.y+p.h-1})
                    Assert.That(data.ground[foot*data.cols+p.x+5]!=0||data.platforms.Any(t=>t.y==foot*32&&t.x<=(p.x+5)*32&&t.x+t.w>(p.x+5)*32),
                        Is.True,$"{seed}: missing landing at {p.x+5},{foot}");
            }
        }

        [UnityTest] public IEnumerator StartingFloorCannotBeDroppedThroughOrCauseFallDamage()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var player=world.arena.Player;
            for(int i=0;i<120&&!player.grounded;i++)yield return null;
            Assert.That(player.grounded,Is.True,"The player must land on the starting floor.");
            var spawn=player.Position;
            var health=player.health;
            Assert.That(player.DropThrough(),Is.False,"The starting floor must reject down+jump.");
            for(int i=0;i<30;i++)yield return null;
            Assert.That(player.grounded,Is.True);
            Assert.That(player.Position.y,Is.EqualTo(spawn.y).Within(.1f));
            Assert.That(player.health,Is.EqualTo(health));
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator StraightConnectorIsOneContinuousSolidFloor()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var baseProfile=JsonUtility.FromJson<Deviant.Definitions.LocomotionData>(Resources.Load<TextAsset>("PlayerLocomotion").text);
            Assert.That(world.arena.Movement.FallGravity,Is.EqualTo(baseProfile.fallGravity).Within(.001f));
            // Do not capture world in a lambda in this iterator: the closure can
            // be created before EnterPlayMode and is lost during domain reload.
            ForestPassage passage=null;
            foreach(var candidate in world.Data.passages)
                if(candidate.h==1&&world.Data.ground[candidate.y*world.Data.cols+candidate.x+5]!=0){passage=candidate;break;}
            Assert.That(passage,Is.Not.Null);
            var player=world.arena.Player;
            player.Position=new Vector2(passage.x+passage.w/2f,-passage.y);
            player.velocity=Vector2.zero;
            Physics2D.SyncTransforms();
            player.grounded=player.ProbeGround();
            Assert.That(player.grounded,Is.True,"The player must stand on the connector floor.");
            Assert.That(world.Data.ground[passage.y*world.Data.cols+passage.x+passage.w/2],Is.Not.Zero);
            var health=player.health;
            Assert.That(player.DropThrough(),Is.False,"A straight floor must not invite a purposeless drop.");
            for(int i=0;i<30;i++)yield return null;
            Assert.That(player.grounded,Is.True);
            Assert.That(player.Position.y,Is.EqualTo(-passage.y).Within(.2f));
            Assert.That(player.health,Is.EqualTo(health));
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator UpperAndLowerOpenShaftsAllowSafeDescent()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            AssertSafeBranchDrops(world);
            yield return new ExitPlayMode();
        }
        // Keep captured lambdas out of the iterator: Unity domain reload does not
        // preserve compiler-generated closure objects across EnterPlayMode.
        static void AssertSafeBranchDrops(ForestCombatWorld world)
        {
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            var player=world.arena.Player;
            using(var driver=world.arena.AcquireManualSimulation())
            foreach(var passage in world.Data.passages.Where(p=>p.h>1&&world.Data.rooms[p.from].mainPath).Take(2))
            {
                driver.Reset();
                player.Position=new Vector2(passage.x+passage.w/2f,-passage.y);
                player.velocity=Vector2.zero;Physics2D.SyncTransforms();player.grounded=player.ProbeGround();
                Assert.That(player.grounded,Is.True,"Every door level has a landing");
                Assert.That(player.DropThrough(),Is.True,"The upper landing permits deliberate descent");
                float startY=player.Position.y,health=player.health;
                for(int tick=0;tick<720&&!player.grounded;tick++)driver.Step(1f/120);
                Assert.That(player.grounded,Is.True,$"{world.Data.generationSeed}: branch {passage.from}->{passage.to} needs a landing");
                Assert.That(player.Position.y,Is.LessThan(startY-.5f));
                Assert.That(player.health,Is.EqualTo(health));
            }
        }

        [UnityTest] public IEnumerator CombatRoomWarpLandsOnAuthoredTerrain()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            foreach(var enemy in world.arena.enemies)enemy.aggressive=false;
            foreach(int room in world.Data.rooms.Where(r=>r.kind=="combat").Select(r=>r.id))
            {
                world.Warp(room);
                Assert.That(world.arena.Player.grounded,Is.True,$"Room {room} warp must land on a surface.");
                int tileX=Mathf.FloorToInt(world.arena.Player.Position.x);
                int foot=Mathf.RoundToInt(-world.arena.Player.Position.y);
                for(int y=foot-2;y<foot;y++)
                    Assert.That(world.Data.ground[y*world.Data.cols+tileX],Is.Zero,
                        $"Room {room} warp must place the player above the exposed surface, not inside the hill.");
                var health=world.arena.Player.health;
                for(int i=0;i<30;i++)yield return null;
                Assert.That(world.arena.Player.grounded,Is.True,$"Room {room} warp must remain on the surface.");
                Assert.That(world.arena.Player.health,Is.EqualTo(health),$"Room {room} warp must not trigger a fall.");
            }
            yield return new ExitPlayMode();
        }







        [Test] public void FinalEarthArtworkCoversEverySolidExactlyOnceAndRemainsDeterministic()
        {
            var first=Generate(42);var second=Generate(42);
            CollectionAssert.AreEqual(first.ground,second.ground);
            CollectionAssert.AreEqual(first.renderGround.Select(c=>(c.x,c.y,c.id)),second.renderGround.Select(c=>(c.x,c.y,c.id)));
            var painted=first.renderGround.ToDictionary(c=>c.y*first.cols+c.x);
            for(int i=0;i<first.ground.Length;i++)if(first.ground[i]!=0)
                Assert.That(painted.ContainsKey(i),Is.True,$"Unpainted earth at {i%first.cols},{i/first.cols}");
            // Export actual generator output for the review preview; no second JS generator.
            string output=System.IO.Path.GetFullPath("../outputs/chapter1-procedural/earth-layouts");
            System.IO.Directory.CreateDirectory(output);
            foreach(int seed in new[]{17,42,113})
                System.IO.File.WriteAllText(System.IO.Path.Combine(output,$"seed-{seed}.json"),JsonUtility.ToJson(Generate(seed)));
        }



        [UnityTest] public IEnumerator VisibleGroundDoesNotDamagePlayer()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            AssertVisibleFloorsSafe(world);
            yield return new ExitPlayMode();
        }
        static void AssertVisibleFloorsSafe(ForestCombatWorld world)
        {
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            var player=world.arena.Player;
            using(var driver=world.arena.AcquireManualSimulation())
            {
                void Land(float x,int foot)
                {
                    driver.Reset();player.Position=new Vector2(x,-foot+.75f);
                    player.velocity=new Vector2(0,-2);player.grounded=false;Physics2D.SyncTransforms();
                    float health=player.health;
                    for(int tick=0;tick<360&&!player.grounded;tick++)driver.Step(1f/120);
                    Assert.That(player.grounded,Is.True,$"Visible floor at {x},{foot} must be landable");
                    Assert.That(player.Position.y,Is.EqualTo(-foot).Within(.2f));
                    Assert.That(player.health,Is.EqualTo(health),"Visible ground must not cause fall damage");
                    Assert.That(player.DropThrough(),Is.False,"Lowest floor must look and behave as solid ground");
                }
                // 전투방마다 가운데에서 가까운, 위가 비어 있는 가장 낮은 고체 바닥(아래 출입구 구멍은 건너뛴다)
                foreach(var room in world.Data.rooms.Where(r=>r.kind=="combat"))
                {
                    int floor=-1,x=0;
                    for(int d=0;d<room.w/2&&floor<0;d++)foreach(int cx in new[]{room.x+room.w/2+d,room.x+room.w/2-d})
                    {
                        var rows=Enumerable.Range(room.y+1,room.h-1).Where(y=>world.Data.ground[y*world.Data.cols+cx]!=0&&world.Data.ground[(y-1)*world.Data.cols+cx]==0&&y<room.y+room.h-1).ToList();
                        if(rows.Count>0&&world.Data.ground[(rows.Last()+1)*world.Data.cols+cx]!=0){floor=rows.Last();x=cx;break;}
                    }
                    Assert.That(floor,Is.GreaterThan(0),room.name+" has a solid floor");
                    Land(x+.5f,floor);
                }
            }
            Assert.That(world.arena.pits.All(p=>p.bounds.max.y < -world.Data.height/32f),Is.True,"Recovery only outside the world");
        }


        [UnityTest] public IEnumerator FallingCameraFollowsSmoothlyNearVerticalCenter()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var player=world.arena.Player;
            player.Position=new Vector2(player.Position.x,-57);
            // This test advances the logical pose directly without fixed/render ticks.
            // Render interpolation has separate coverage in VillageDisplayRegressionTests.
            world.CameraPresentationPosition=null;
            // Scene framing is independently tunable; measure falling follow around a neutral center.
            float authoredOffset=world.cameraVerticalOffsetPixels;
            world.RefreshCamera(0,true);
            float authoredCameraY=world.CameraY;
            world.cameraVerticalOffsetPixels=0;
            world.RefreshCamera(0,true);
            Assert.That(world.CameraY-authoredCameraY,Is.EqualTo(authoredOffset).Within(.01f),
                "The authored framing offset must still affect the snapped camera.");
            Assert.That(world.View.GetComponent<AudioListener>(),Is.Not.Null);
            float renderedY=world.View.transform.position.y;
            player.Position+=new Vector2(0,-.25f/32);
            world.RefreshCamera(0,true);
            Assert.That(world.View.transform.position.y,Is.LessThan(renderedY-.001f),
                "Subpixel player movement should not be rounded out of the camera output.");
            float previous=world.CameraY;
            float fallVelocity=-20;
            player.velocity=new Vector2(0,fallVelocity);
            player.grounded=false;
            for(int i=0;i<30;i++)
            {
                player.Position+=new Vector2(0,-20f/60);
                world.RefreshCamera(1f/60);
                Assert.That(world.CameraY,Is.GreaterThan(previous),"The camera should follow the fall.");
                Assert.That(world.CameraY-previous,Is.LessThan(32),"The camera must not snap between frames.");
                previous=world.CameraY;
            }
            Assert.That(-player.Position.y*32-world.CameraY,Is.InRange(200,390),
                "The falling player should stay near the center of the 540px view.");
            for(int i=0;i<20;i++)
            {
                fallVelocity-=104.1667f/60;
                player.velocity=new Vector2(0,fallVelocity);
                player.Position+=new Vector2(0,fallVelocity/60);
                world.RefreshCamera(1f/60);
                Assert.That(-player.Position.y*32-world.CameraY,Is.InRange(200,390),
                    "Camera follow should keep pace as falling accelerates.");
            }
            yield return new ExitPlayMode();
        }


        // 마을 배경 구조물(집·담장·나무 등)은 발판과 그 아래 덩굴 그림 영역에 겹치지 않는다(사용자 요청 2026-11-06).
        [Test] public void VillageStructuresDoNotOverlapDecks()
        {
            foreach(int seed in new[]{1,17,42,113,1409})
            {
                var data=Generate(seed);
                foreach(var prop in Stage1VillageExperiment.VillageTeamScenery.Create(data).props)
                {
                    if(prop.kind=="phase2_grass"||prop.width>0)continue;
                    var image=Resources.Load<Texture2D>("Village/color/props/"+prop.kind);
                    float scale=prop.scale>0?prop.scale:1;
                    float left=prop.x-image.width*scale*.5f,right=prop.x+image.width*scale*.5f,top=prop.y-image.height*scale,bottom=prop.y;
                    foreach(var p in data.platforms)
                        Assert.That(p.x<right&&left<p.x+p.w&&p.y<bottom&&top<p.y+3*32,Is.False,
                            $"{seed}: {prop.kind} at {prop.x/32:F1},{prop.y/32} overlaps the deck at {p.x/32},{p.y/32}");
                }
            }
        }

        static bool Inside(FoliagePlatform p,ForestRoom r)=>p.x>=r.x*32&&p.x+p.w<=(r.x+r.w)*32&&p.y>=r.y*32&&p.y<(r.y+r.h)*32;

        // 봉인 도전방은 정비소 뒤 주 경로 자리, 잠긴 상자방은 정비소에서 또는 그 뒤에서 갈라지는 우회 갈래다(사용자 결정 2026-11-05).
        [Test] public void SealChallengeFollowsTheFacilityAndChestTakesABypassAfterIt()
        {
            var catalog=BlockMap.BlockSpec.Catalog;
            var sealForms=new HashSet<string>();var chestForms=new HashSet<string>();
            foreach(int seed in Enumerable.Range(0,48))
            {
                var data=Generate(seed);
                var facility=data.rooms.Single(r=>r.kind=="maintenance");
                var seal=data.rooms.Single(r=>r.kind=="seal");var chest=data.rooms.Single(r=>r.kind=="chest");
                Assert.That(seal.mainPath,Is.True,$"{seed}: the seal room is on the main route");
                Assert.That(seal.id,Is.GreaterThan(facility.id).And.LessThan(9),$"{seed}: the seal room is after the facility, before the boss");
                Assert.That(seal.entrySide,Is.EqualTo("left"));Assert.That(seal.exitSide,Is.EqualTo("right"));
                Assert.That(data.passages.Count(p=>p.from==seal.id||p.to==seal.id),Is.EqualTo(2),$"{seed}: the seal room has only its two gated side doors");
                foreach(var bypass in data.rooms.Where(r=>!r.mainPath&&r.id>=10&&r.id<=11))
                {
                    int from=data.passages.Single(p=>p.to==bypass.id).from,to=data.passages.Single(p=>p.from==bypass.id).to;
                    Assert.That(data.rooms[from].mainPath&&data.rooms[to].mainPath,Is.True);
                    Assert.That(seal.id>from&&seal.id<to,Is.False,$"{seed}: a bypass must not skip the seal room");
                }
                Assert.That(chest.mainPath,Is.False);
                Assert.That(data.passages.Single(p=>p.to==chest.id).from,Is.GreaterThanOrEqualTo(facility.id),$"{seed}: the chest bypass leaves at or after the facility");
                Assert.That(data.passages.Count(p=>p.from==chest.id&&data.rooms[p.to].mainPath),Is.EqualTo(1),$"{seed}: the chest bypass rejoins the main route");
                Assert.That(data.rooms.Any(r=>Chapter1KeyChest.CarrierRoom(data,r)),Is.True,$"{seed}: a main combat room beyond the chest can carry the key");
                var chestDef=catalog.rooms[chest.sourceZone-100];var sealDef=catalog.rooms[seal.sourceZone-100];
                Assert.That(chestDef.kind,Is.EqualTo("chest"));Assert.That(sealDef.kind,Is.EqualTo("seal"));
                foreach(var room in new[]{seal,chest})
                {
                    var marker=data.landmarks.Single(l=>l.zone==room.id&&l.kind==room.kind);
                    int col=Mathf.FloorToInt(marker.x/32),row=Mathf.RoundToInt(marker.y/32);
                    Assert.That(col,Is.InRange(room.x+2,room.x+room.w-3));
                    Assert.That(data.ground[row*data.cols+col],Is.Not.Zero,$"{seed}: the {room.kind} marker stands on earth");
                    for(int up=1;up<=3;up++)Assert.That(data.ground[(row-up)*data.cols+col],Is.Zero,$"{seed}: the {room.kind} marker is not buried");
                    Assert.That(data.actors.Count(a=>a.zone==room.id),Is.Zero,$"{seed}: gimmick rooms start empty");
                }
                sealForms.Add(sealDef.form);chestForms.Add(chestDef.form);
            }
            CollectionAssert.AreEquivalent(new[]{"A","B","C"},sealForms);
            CollectionAssert.AreEquivalent(new[]{"A","B","C"},chestForms);
        }

        // 전투 맵에는 지워지는 발판이 없다(2026-11-06): 템플릿에 표시된 상자방 발판도 단단한 발판이다.
        [Test] public void CombatMapHasNoVanishingDecks()
        {
            foreach(int seed in Enumerable.Range(0,32))
                Assert.That(Generate(seed).platforms.Count(p=>p.vanishing),Is.Zero,$"{seed}: no vanishing decks in the combat map");
        }

        [UnityTest] public IEnumerator KeyCarrierTraversesPlatformsGrantsKeyImmediatelyAndOnlyTheKeyOpensTheChest()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var keys=world.GetComponent<Chapter1KeyChest>();
            for(int i=0;i<60&&(!keys||!keys.Carrier);i++){yield return null;keys=world.GetComponent<Chapter1KeyChest>();}
            Assert.That(keys,Is.Not.Null);
            var carrier=keys.Carrier;var chest=keys.Chest;var player=world.arena.Player;
            Assert.That(carrier,Is.Not.Null,"a carrier is chosen");
            Assert.That(chest,Is.Not.Null,"the locked chest stands in the chest room");
            Assert.That(carrier.maxHealth,Is.EqualTo(Chapter1KeyChest.CarrierHealth));
            Assert.That(Chapter1EnemyRole.Of(carrier),Is.EqualTo(Chapter1EnemyRole.Normal));
            ForestRoom carrierRoom=null,facility=null;
            foreach(var room in world.Data.rooms)
            {
                if(room.kind=="maintenance")facility=room;
                if(new Rect(room.x,-(room.y+room.h),room.w,room.h).Contains(carrier.spawn+Vector2.up*.5f))carrierRoom=room;
            }
            Assert.That(carrierRoom.kind,Is.EqualTo("combat"));
            Assert.That(carrierRoom.mainPath,Is.True);
            Assert.That(Chapter1KeyChest.CarrierRoom(world.Data,carrierRoom),Is.True,"the carrier waits beyond the chest room");
            Assert.That(keys.GlowPieces,Is.EqualTo(39),"the carrier glows: a halo, six drifting specks and four edge rings");

            // The carrier may use every room but the boss room.
            foreach(var room in world.Data.rooms)
            {
                if(room.kind!="boss")continue;
                var boss=new Rect(room.x,-(room.y+room.h),room.w,room.h);
                foreach(var l in keys.Route.Landings)
                    Assert.That(l.Allowed&&boss.Contains(new Vector2(l.Mid,l.Y+.1f)),Is.False,"no boss room landing on the carrier's routes");
            }

            // Place the carrier on an actual one-way deck next to the player. A normal hit must not stop its run.
            world.Warp(carrierRoom.id);
            foreach(var enemy in world.arena.enemies)if(enemy&&enemy!=carrier)enemy.aggressive=false;
            FoliagePlatform deck=null;
            foreach(var p in world.Data.platforms)
            {
                var mid=new Vector2((p.x+p.w*.5f)/32f,-p.y/32f);
                if(p.blockDropThrough || !keys.CarrierArea.Contains(mid+Vector2.up*.1f) ||
                    carrier.Blocked(mid,carrier.facing) || carrier.Blocked(mid+Vector2.down*.3f,carrier.facing))continue;
                deck=p;break;
            }
            Assert.That(deck,Is.Not.Null,"a droppable deck over clear space");
            carrier.Position=new Vector2((deck.x+deck.w*.5f)/32f,-deck.y/32f);
            carrier.velocity=Vector2.zero;Physics2D.SyncTransforms();
            carrier.grounded=carrier.ProbeGround();
            Assert.That(carrier.grounded,Is.True);
            Assert.That(carrier.Receive(1,player,1,true,null,bypass:true),Is.True);
            Assert.That(carrier.staggerUntil,Is.LessThanOrEqualTo(world.arena.Now));
            player.Position=carrier.Position+Vector2.left*3;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            var before=carrier.Position;
            using(var simulation=world.arena.AcquireManualSimulation())
                for(int i=0;i<120;i++){simulation.Step(1f/120);player.health=player.maxHealth;}
            Assert.That(keys.Route.Fleeing,Is.True,"the player three tiles away starts the run");
            Assert.That(Vector2.Distance(carrier.Position,before),Is.GreaterThan(2f),$"the carrier runs (decision {keys.LastDecision})");
            Assert.That(carrier.Brain.Executing,Is.False);

            // Locked: F without the key only shakes the chest.
            Assert.That(keys.TryOpenChest(),Is.False,"F does nothing out of reach");
            player.Position=chest.Position+Vector2.left;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(keys.TryOpenChest(),Is.True);
            Assert.That(keys.Opened,Is.False);
            Assert.That(chest.IsOpen,Is.False);
            Assert.That(keys.NoKeyShowing,Is.True,"F without the key shows the crossed-out key");
            Assert.That(chest.hurtCollider&&chest.hurtCollider.enabled,Is.True,"the locked chest takes hits so a swing gets an answer");
            for(float t=Time.time;Time.time-t<1.6f;)yield return null;
            Assert.That(keys.NoKeyShowing,Is.False,"the crossed-out key fades");
            chest.Receive(10,player,1,true,null);
            Assert.That(keys.NoKeyShowing,Is.True,"a hit without the key shows the crossed-out key");
            Assert.That(keys.Opened,Is.False);

            Assert.That(Chapter1KeyChest.CanCarry(carrier),Is.True,"a plain walking monster carries the key");
            Assert.That(carrier.GetType(),Is.EqualTo(typeof(FieldMouseEnemy)),"no fixed or custom-movement species");
            int pickupsBefore=world.GetComponent<Chapter1Rewards>().Pickups.Count;
            Assert.That(Vector2.Distance(player.Position,carrier.Position),Is.GreaterThan(2f),"kill from beyond pickup reach");
            carrier.Receive(10000,player,1,true,null,true);
            // The stage reports defeated enemies during its next Update. No pickup movement is needed.
            for(int i=0;i<10&&!keys.HasKey;i++)yield return null;
            Assert.That(keys.HasKey,Is.True,"the key is granted on defeat without approaching the carrier");
            var key=keys.KeyEffect;
            Assert.That(key,Is.Not.Null,"acquisition feedback appears immediately");
            Assert.That(Vector2.Distance(key.transform.position,player.Position+Vector2.up*(player.size.y+.3f)),Is.LessThan(1f),"feedback follows the player, not the defeated carrier");
            for(int i=0;i<5;i++)yield return null;
            Assert.That(world.GetComponent<Chapter1Rewards>().Pickups.Count,Is.EqualTo(pickupsBefore),"the carrier grants no other loot");

            chest.Receive(10,player,1,true,null);
            Assert.That(keys.Opened,Is.False,"a hit does not open the chest even with the key");
            player.Position=chest.Position+Vector2.left;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(keys.TryOpenChest(),Is.True,"F beside the chest opens it with the key");
            Assert.That(keys.Opened,Is.True);
            Assert.That(chest.IsOpen,Is.True);
            Assert.That(keys.HasKey,Is.False,"the key is used up");
            // The key opens a choice of an ink or a blueprint (user decision 2026-11-13): no gold, health or stamp.
            var sketch=world.GetComponent<Chapter1RunBinding>().Run.Sketch;
            Assert.That(keys.LastReward,Is.EqualTo("choice"));
            Assert.That(keys.LastStamp,Is.Null,"no stamp is thrown out");
            Assert.That(sketch.PendingOffer,Is.Not.Null,"the chest opens a choice");
            Assert.That(sketch.PendingIsEvent,Is.True);
            Assert.That(sketch.PendingOffer.Length,Is.EqualTo(3));
            Assert.That(sketch.PendingOffer.Any(c=>c.IsInk),Is.True,"an unregistered ink is offered");
            Assert.That(sketch.PendingOffer.Any(c=>!c.IsInk),Is.True,"a blueprint weapon is offered");
            foreach(var c in sketch.PendingOffer.Where(c=>!c.IsInk))
                Assert.That(c.Weapon.Slots,Is.EqualTo(sketch.Planner.Rules.BossSlots(sketch.Stage)),"chest weapons use the stage's largest slot count");
            yield return null;
            Assert.That(world.arena.Controls.Current.SimulationPaused,Is.True,"the choice pauses the run");
            int picked=sketch.WeaponsPicked;
            int weaponIndex=System.Array.FindIndex(sketch.PendingOffer,c=>!c.IsInk);
            Assert.That(sketch.Choose(weaponIndex),Is.Not.Null,"the blueprint arrives unconfirmed");
            Assert.That(sketch.WeaponsPicked,Is.EqualTo(picked),"the chest weapon is outside the normal weapon guarantee");
            Assert.That(sketch.PendingOffer,Is.Null);
            yield return new ExitPlayMode();
        }

        [Test] public void ChestStampIsAFormStampForACarriedBlueprintOrASkillStamp()
        {
            var random=new System.Random(7);int weapon=0,skill=0;var kinds=new List<string>{"sword"};
            for(int i=0;i<2000;i++)
            {
                var stamp=Chapter1KeyChest.ChooseChestStamp(kinds,random);
                if(stamp.Kind==Deviant.Run.StampKind.Weapon){weapon++;Assert.That(stamp.BlueprintKind,Is.EqualTo("sword"),"only the carried blueprint");}
                else skill++;
            }
            Assert.That(weapon/2000f,Is.EqualTo(.5f).Within(.06f));Assert.That(skill/2000f,Is.EqualTo(.5f).Within(.06f));
            for(int i=0;i<50;i++)Assert.That(Chapter1KeyChest.ChooseChestStamp(new List<string>(),random).Kind,Is.EqualTo(Deviant.Run.StampKind.Skill),"no blueprint: a skill stamp");
        }

        [UnityTest] public IEnumerator SealChallengeDropsThreePagesThenOpensTheGatesWithoutATimeLimit()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var seal=world.GetComponent<Chapter1SealChallenge>();
            Assert.That(seal,Is.Not.Null);
            Assert.That(seal.Prepare(),Is.True);
            var player=world.arena.Player;var room=seal.Room;
            world.Warp(room.id);
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            player.Position=seal.TabletPosition;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(seal.TryStart(),Is.True);
            Assert.That(seal.GatesShut,Is.True,"both doors shut");
            Assert.That(seal.State,Is.EqualTo(Chapter1SealChallenge.ChallengeState.Active));
            for(int page=0;page<Chapter1SealChallenge.PageSizes.Length;page++)
            {
                Assert.That(seal.Page,Is.EqualTo(page));
                int expected=Chapter1SealChallenge.PageSizes[page],count=0;
                for(int i=0;i<4000&&count<expected;i++)
                {
                    yield return null;
                    player.health=player.maxHealth;
                    count=0;foreach(var enemy in seal.Spawned)count++;
                }
                Assert.That(count,Is.EqualTo(expected),$"page {page+1} drops {expected}");
                foreach(var enemy in seal.Spawned)
                {
                    Assert.That(enemy.Position.x,Is.InRange(room.x+1f,room.x+room.w-1f),"falls inside the room");
                    var drop=enemy.GetComponent<Deviant.Inventory.Unity.InkDropSource>();
                    Assert.That(drop!=null&&drop.tableKey=="",Is.True,"challenge monsters drop no fixed ink");
                    Assert.That(enemy.GetComponent<Chapter1NoInkReward>(),Is.Not.Null,"challenge monsters drop no reward ink");
                    enemy.Receive(10000,player,1,true,null,true);
                }
                for(int i=0;i<4000&&seal.Page==page&&seal.State==Chapter1SealChallenge.ChallengeState.Active;i++)yield return null;
            }
            Assert.That(seal.State,Is.EqualTo(Chapter1SealChallenge.ChallengeState.Succeeded));
            foreach(var pickup in world.GetComponent<Chapter1Rewards>().Pickups)
                Assert.That(pickup.Kind==Chapter1RewardKind.ColoredInk||pickup.Kind==Chapter1RewardKind.InkPart,Is.False,"no ink from the 30 page monsters");
            Assert.That(seal.GatesShut,Is.False,"the gates open on success");
            Assert.That(seal.RewardChest,Is.Not.Null,"the reward chest appears");
            seal.RewardChest.Receive(10,player,1,true,null);
            Assert.That(seal.RewardChest.IsOpen,Is.False,"a hit does not open the reward chest");
            player.Position=seal.RewardChest.Position+Vector2.left;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(seal.TryOpenReward(),Is.True,"F beside the chest opens it");
            Assert.That(seal.RewardChest.IsOpen,Is.True);
            var augments=world.GetComponent<AugmentRewardChoice>();
            Assert.That(augments.IsOpen,Is.True,"the reward chest opens the augment choice");
            string taken=augments.Offer[0].id;
            Assert.That(augments.TryTake(0),Is.True);
            Assert.That(augments.Augments.Has(taken),Is.True,"the card joins the run's maintenance augments");
            Assert.That(seal.TryStart(),Is.False,"one try per run");

            // The development reset returns the untouched challenge with no clock: it can be started again and waits.
            world.arena.ResetTest();
            for(int i=0;i<5;i++)yield return null;
            Assert.That(seal.State,Is.EqualTo(Chapter1SealChallenge.ChallengeState.Dormant));
            Assert.That(seal.RewardChest,Is.Null);
            world.Warp(room.id);
            player.Position=seal.TabletPosition;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(seal.TryStart(),Is.True);
            float startedAt=world.arena.Now;
            for(int i=0;i<20000&&world.arena.Now<startedAt+50;i++){yield return null;player.health=player.maxHealth;}
            Assert.That(seal.State,Is.EqualTo(Chapter1SealChallenge.ChallengeState.Active),"no time limit: still running past the old 45 seconds");
            Assert.That(seal.GatesShut,Is.True);
            yield return new ExitPlayMode();
        }

        // Augments are never sold (user decision 2026-11-13): the seal reward chest opens a free choice at once and the boss
        // clear queues one behind its weapon choice. The old 12-card run augments are gone.
        [UnityTest] public IEnumerator AugmentChoicesAreFreeAndTheBossChoiceWaitsForTheWeaponChoice()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            for(int i=0;i<10;i++)yield return null;
            Assert.That(Resources.Load<TextAsset>("Chapter1RunAugments"),Is.Null,"the old run augment catalog is removed");
            var augments=world.GetComponent<AugmentRewardChoice>();
            var run=world.GetComponent<Chapter1RunBinding>().Run;
            Assert.That(augments,Is.Not.Null);Assert.That(augments.Augments,Is.SameAs(run.Growth.Augments),"the choice uses the run's augments");
            var arena=world.arena;
            int gold=run.Growth.Economy.Gold;

            // Seal chest: opens at once and pauses.
            Assert.That(augments.OpenChoice(),Is.True);
            Assert.That(arena.Controls.Current.SimulationPaused,Is.True,"the game pauses while choosing");
            Assert.That(augments.Offer.Length,Is.EqualTo(3));
            Assert.That(augments.Offer[0].Stable,Is.True,"one stable card");
            Assert.That(augments.Offer[1].Stable||augments.Offer[2].Stable,Is.False,"two risk cards");
            Assert.That(augments.Offer[1].group,Is.Not.EqualTo(augments.Offer[2].group),"of different groups");
            Assert.That(augments.OpenChoice(),Is.False,"one choice at a time");
            string first=augments.Offer[1].id;
            Assert.That(augments.TryTake(1),Is.True);
            Assert.That(augments.IsOpen,Is.False);
            Assert.That(arena.Controls.Current.SimulationPaused,Is.False);
            Assert.That(run.Growth.Augments.Has(first),Is.True);
            Assert.That(run.Growth.Economy.Gold,Is.EqualTo(gold),"the augment is free");

            // Boss clear: the boss weapon choice comes first, then the augment choice. Chapter 1 leads on to chapter 2, so
            // its boss gives one; the last chapter's boss does not (user decision 2026-11-13).
            Assert.That(world.GetComponent<Chapter1BossEncounter>().ClearEndsRun,Is.False,"chapter 1's exit leads on to chapter 2");
            Assert.That(Deviant.Chapter2.Chapter2World.BossAugmentFollows,Is.False,"chapter 2 is the last chapter");
            Assert.That(run.Sketch,Is.Not.Null);
            Assert.That(run.Sketch.OpenBossOffer(),Is.True);
            augments.QueueChoice();
            for(int i=0;i<5;i++)yield return null;
            Assert.That(augments.IsOpen,Is.False,"the augment choice waits for the weapon choice");
            Assert.That(augments.Queued,Is.EqualTo(1));
            run.Sketch.Choose(0);
            for(int i=0;i<30&&!augments.IsOpen;i++)yield return null;
            Assert.That(augments.IsOpen,Is.True,"the augment choice follows the weapon choice");
            Assert.That(Offers(augments,first),Is.False,"an owned card is not offered again");
            int owned=run.Growth.Augments.Owned.Count;
            Assert.That(augments.TryTake(0),Is.True);
            Assert.That(run.Growth.Augments.Owned.Count,Is.EqualTo(owned+1));
            Assert.That(augments.Queued,Is.Zero);
            yield return new ExitPlayMode();
        }
        // No closures in UnityTest iterators: a captured local is lost on the EnterPlayMode domain reload.
        static bool Offers(AugmentRewardChoice choice,string id)=>choice.Offer.Any(a=>a.id==id);

        [UnityTest] public IEnumerator GimmickDevKeysPlaceThePlayerBesideTheSealChestAndCarrier()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            for(int i=0;i<10;i++)yield return null;
            var dev=world.GetComponent<Chapter1GimmickDevKeys>();
            Assert.That(dev,Is.Not.Null,"installed in the Editor");
            var player=world.arena.Player;
            Assert.That(dev.JumpToSeal(),Is.True);
            Assert.That(world.GetComponent<Chapter1StartingBlueprints>().Complete,Is.True,"jumping ahead equips two blueprints");
            Assert.That(Vector2.Distance(player.Position,world.GetComponent<Chapter1SealChallenge>().TabletPosition),Is.LessThan(3f));
            Assert.That(dev.JumpToChest(),Is.True);
            Assert.That(Vector2.Distance(player.Position,world.GetComponent<Chapter1KeyChest>().Chest.Position),Is.LessThan(3f));
            Assert.That(dev.JumpToCarrier(),Is.True);
            Assert.That(Vector2.Distance(player.Position,world.GetComponent<Chapter1KeyChest>().Carrier.Position),Is.LessThan(3f));
            yield return new ExitPlayMode();
        }

        // No closures in UnityTest iterators: a captured local is lost on the EnterPlayMode domain reload.
        static bool SolidCell(ForestData data,int x,int y)=>x<0||y<0||x>=data.cols||y>=data.rows||data.ground[y*data.cols+x]!=0;

        [UnityTest] public IEnumerator KeyCarrierClimbsAWallInsteadOfBeingCornered()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var keys=world.GetComponent<Chapter1KeyChest>();
            for(int i=0;i<60&&(!keys||!keys.Carrier);i++){yield return null;keys=world.GetComponent<Chapter1KeyChest>();}
            var carrier=keys.Carrier;var player=world.arena.Player;var area=keys.CarrierArea;var data=world.Data;
            // Find a 2..6 tile wall (either side) with a clear shaft and top: the carrier's room first, then any room.
            int baseX=-1,footRow=-1,height=0,side=1;Rect wallRoom=area;
            var searchRooms=new List<Rect>{area};
            foreach(var r in data.rooms)if(r.kind=="combat")searchRooms.Add(new Rect(r.x,-(r.y+r.h),r.w,r.h));
            foreach(var room in searchRooms)
            {
                for(int x=Mathf.CeilToInt(room.xMin)+4;x<Mathf.FloorToInt(room.xMax)-4&&baseX<0;x++)
                for(int row=Mathf.RoundToInt(-room.yMax)+3;row<Mathf.RoundToInt(-room.yMin)&&baseX<0;row++)
                for(int dir=-1;dir<=1&&baseX<0;dir+=2)
                {
                    if(!SolidCell(data,x,row)||SolidCell(data,x,row-1)||!SolidCell(data,x+dir,row-1))continue;
                    int h=1;while(h<=Chapter1KeyChest.MaxClimbTiles&&SolidCell(data,x+dir,row-1-h))h++;
                    if(h>Chapter1KeyChest.MaxClimbTiles||h<2||SolidCell(data,x+dir,row-1-h)||SolidCell(data,x+dir,row-2-h))continue;
                    bool shaft=true;for(int k=1;k<=h+2;k++)if(SolidCell(data,x,row-k))shaft=false;
                    if(!shaft||!SolidCell(data,x-dir,row)||!SolidCell(data,x-2*dir,row)||SolidCell(data,x-dir,row-1)||SolidCell(data,x-2*dir,row-1))continue;
                    baseX=x;footRow=row;height=h;side=dir;wallRoom=room;
                }
                if(baseX>=0)break;
            }
            Assume.That(baseX,Is.GreaterThanOrEqualTo(0),"no climbable wall anywhere on this seed");
            // Isolate the intended landing above this wall from other platforms elsewhere in the generated map.
            float lip=baseX+(side>0?1:0);
            keys.SetCarrierAreaForVerification(new Rect(lip-2, -footRow+height-.2f,4,.5f));
            foreach(var enemy in world.arena.enemies)if(enemy&&enemy!=carrier)enemy.aggressive=false;
            carrier.Position=new Vector2(baseX+.5f+side*.1f,-footRow);carrier.velocity=Vector2.zero;
            player.Position=new Vector2(baseX+.5f-side*2.6f,-footRow);player.velocity=Vector2.zero;
            Physics2D.SyncTransforms();carrier.grounded=carrier.ProbeGround();
            float startY=carrier.Position.y,from=world.arena.Now;bool climbed=false;
            for(int i=0;i<4000&&world.arena.Now<from+3f;i++)
            {
                yield return null;
                player.Position=new Vector2(baseX+.5f-side*2.6f,-footRow);player.health=player.maxHealth;
                if(carrier.Position.y>=startY+height-.2f){climbed=true;break;}
            }
            Assert.That(climbed,Is.True,$"climbs the {height}-tile wall (carrier {carrier.Position}, decision {keys.LastDecision})");
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator DistantIdleEnemiesResumeWhenTheirRoomIsApproached()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            // The far room is the last main combat room before the boss. It lies beyond the locked chest, so its
            // walkers may include the key carrier (a movement override that only flees), which is left out here.
            // No lambdas here: EnterPlayMode reloads the domain, and closure storage created before it is lost.
            int farId=-1;
            foreach(var room in world.Data.rooms)
                if(room.kind=="combat"&&room.mainPath&&room.id<9)
                    foreach(var actor in world.Data.actors)if(actor.zone==room.id){farId=room.id;break;}
            Assert.That(farId,Is.GreaterThanOrEqualTo(0),"far combat room");
            var player=world.arena.Player;Assert.That(player,Is.Not.Null,"player");
            var far=new List<FieldMouseEnemy>();
            for(int i=0;i<world.Data.actors.Length&&i<world.arena.enemies.Count;i++)
                if(world.Data.actors[i].zone==farId&&world.arena.enemies[i] is FieldMouseEnemy mob&&mob&&mob.MovementOverride==null)far.Add(mob);
            Assert.That(far,Is.Not.Empty,"far room enemies");
            foreach(var enemy in far)
            {
                Assert.That(enemy.distantIdleRange.x,Is.EqualTo(64),"distance rule outside rooms");
                Assert.That(enemy.OutsideActiveArea,Is.Not.Null,"room activation");
                Assert.That(enemy.OutsideActiveArea(enemy),Is.True,"a walker of a far room rests");
                Assert.That(Mathf.Abs(enemy.Position.x-player.Position.x),Is.GreaterThan(64));
                enemy.Step(1f/120);
                Assert.That(enemy.Focus,Is.Null,"a distant idle enemy stays asleep");
            }
            world.Warp(farId);
            // Room activation: every walker of the player's room wakes, an upper-deck one beyond the old 24-unit
            // vertical range included.
            var rooms=new Chapter1RoomActivity(world.Data,world.arena);
            int farIndex=-1;for(int i=0;i<world.Data.rooms.Length;i++)if(world.Data.rooms[i].id==farId)farIndex=i;
            Assert.That(rooms.RoomAt(player.Position),Is.EqualTo(farIndex),"warped into the far room");
            bool beyondVertical=false;
            foreach(var enemy in far)
            {
                beyondVertical|=Mathf.Abs(enemy.Position.y-player.Position.y)>enemy.distantIdleRange.y;
                Assert.That(rooms.Rests(enemy.Position),Is.False,"room awake: "+enemy.name+" at "+enemy.Position);
                Assert.That(enemy.OutsideActiveArea(enemy),Is.False,"the world's rule wakes "+enemy.name+" at "+enemy.Position);
                // Prototype actors (chargers, ranged) run their own brain without Focus; plain walkers sense at once.
                if(enemy.GetType()!=typeof(FieldMouseEnemy))continue;
                enemy.Step(1f/120);
                Assert.That(enemy.Focus,Is.SameAs(player),"awake in the player's room: "+enemy.name+" at "+enemy.Position);
            }
            if(!beyondVertical)Debug.Log("This map's far room has no walker beyond the old 24-unit vertical range.");
            yield return new ExitPlayMode();
        }
    }
}
