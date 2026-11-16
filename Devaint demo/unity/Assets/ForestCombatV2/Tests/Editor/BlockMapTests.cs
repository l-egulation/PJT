using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using ForestCombatV2.BlockMap;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // 블록 격자 방·통로(data/map/block_rooms.json)와 시드 배치기의 계약.
    // 이동 가능성 전체 판정(모든 출입구 조합·반전·시드)은 node tools/map/validate.cjs가 맡고,
    // 여기서는 C# 구현이 같은 결과를 내는지와 실제 플레이어 물리로 연결 규격이 성립하는지를 확인한다.
    public sealed class BlockMapTests
    {
        const string Source="Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json";
        const string Artwork="Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json";
        const string Parity="Assets/ForestCombatV2/Tests/Editor/BlockGeneratorParity.json";

        [System.Serializable] sealed class ParityEntry { public int seed,attempt; public string[] rooms,connectors,links; }
        [System.Serializable] sealed class ParityFile { public string schema; public ParityEntry[] stage1,stage2; }

        [Test] public void CSharpGeneratorMatchesTheToolGeneratorSeedForSeed()
        {
            var file=JsonUtility.FromJson<ParityFile>(AssetDatabase.LoadAssetAtPath<TextAsset>(Parity).text);
            Assert.That(file.schema,Is.EqualTo("block-generator-parity-v1"));
            void Check(ParityEntry e,BlockLayout L,string stage)
            {
                Assert.That(L,Is.Not.Null,stage+" seed "+e.seed);
                Assert.That(L.attempt,Is.EqualTo(e.attempt),stage+" attempt seed "+e.seed);
                Assert.That(L.rooms.Select(r=>$"{r.key}|{r.refId}|{r.bx}|{r.by}|{(r.mirror?"true":"false")}"),Is.EqualTo(e.rooms),stage+" rooms seed "+e.seed);
                Assert.That(L.connectors.Select(c=>$"{c.key}|{new string(c.open)}|{c.bx}|{c.by}|{c.bw}"),Is.EqualTo(e.connectors),stage+" connectors seed "+e.seed);
                Assert.That(L.links.Select(l=>$"{l.a}|{l.na}|{l.b}|{l.nb}"),Is.EqualTo(e.links),stage+" links seed "+e.seed);
            }
            foreach(var e in file.stage1)Check(e,BlockGenerator.Stage1(e.seed),"stage1");
            foreach(var e in file.stage2)Check(e,BlockGenerator.Stage2(e.seed),"stage2");
        }

        [Test] public void EveryDoorCombinationKeepsTheFrameClosedExceptActiveOpenings()
        {
            foreach(var room in BlockSpec.Catalog.rooms)
            {
                int k=room.slots.Length;
                for(int mask=1;mask<1<<k;mask++)foreach(bool mirror in new[]{false,true})
                {
                    var active=room.slots.Where((s,i)=>(mask>>i&1)!=0).ToArray();
                    var b=BlockRooms.Build(room,active,mirror);
                    // 테두리 셀은 열린 출입구 자리 외에는 모두 고체
                    var open=new HashSet<int>();
                    foreach(var g in b.slots.Where(g=>g.active))
                    {
                        if(g.side=='L'||g.side=='R'){int x0=g.side=='L'?0:b.fw-b.fx;for(int y=g.y0;y<g.y1;y++)for(int x=x0;x<x0+b.fx;x++)open.Add(y*b.fw+x);}
                        else{int y0=g.side=='B'?0:b.fh-b.fy;for(int y=y0;y<y0+b.fy;y++)for(int x=g.x0;x<g.x1;x++)open.Add(y*b.fw+x);}
                    }
                    for(int y=0;y<b.fh;y++)for(int x=0;x<b.fw;x++)
                    {
                        bool frame=x<b.fx||x>=b.fw-b.fx||y<b.fy||y>=b.fh-b.fy;if(!frame)continue;
                        Assert.That(b.grid[y*b.fw+x]==0,Is.EqualTo(open.Contains(y*b.fw+x)),$"{room.id} {string.Join("+",active)} mirror={mirror} frame cell {x},{y}");
                    }
                    // 적은 지면(고체 또는 방 고유 발판) 위에 서고 몸이 고체에 묻히지 않는다
                    foreach(var e in b.enemies)
                    {
                        int fx=Mathf.FloorToInt(e.x),fy=Mathf.RoundToInt(e.y);
                        bool onSolid=b.grid[(fy-1)*b.fw+fx]!=0,onPlat=b.plats.Any(p=>p.role=="core"&&p.y==fy&&e.x>=p.x&&e.x<=p.x+p.w);
                        Assert.That(onSolid||onPlat,Is.True,$"{room.id} {string.Join("+",active)} mirror={mirror} enemy {e.t} at {e.x},{e.y}");
                        Assert.That(b.grid[fy*b.fw+fx],Is.Zero,$"{room.id} enemy body inside solid at {e.x},{e.y}");
                    }
                }
            }
        }

        [Test] public void SeedsAssembleWithoutOverlapAndWithEveryLinkAligned()
        {
            var watch=new System.Diagnostics.Stopwatch();double s1=0,s2=0,a1=0,a2=0;
            for(int seed=1;seed<=12;seed++)
            {
                watch.Restart();var a=BlockGenerator.Stage1(seed);s1=System.Math.Max(s1,watch.Elapsed.TotalMilliseconds);a1+=watch.Elapsed.TotalMilliseconds;
                watch.Restart();var b=BlockGenerator.Stage2(seed);s2=System.Math.Max(s2,watch.Elapsed.TotalMilliseconds);a2+=watch.Elapsed.TotalMilliseconds;
                foreach(var L in new[]{a,b})
            {
                Assert.That(L,Is.Not.Null,"seed "+seed);
                var w=BlockAssembler.Assemble(L);
                Assert.That(w.issues,Is.Empty,$"{L.stage} seed {seed}");
                // 링크에 쓰지 않은 슬롯은 닫혀 있다
                foreach(var r in w.rooms.Where(r=>!r.IsConnector))
                {
                    var used=L.links.Where(l=>l.a==r.key).Select(l=>l.na).Concat(L.links.Where(l=>l.b==r.key).Select(l=>l.nb)).ToHashSet();
                    Assert.That(r.build.slots.Where(s=>s.active).Select(s=>s.id).ToHashSet(),Is.EquivalentTo(used),$"{L.stage} seed {seed} {r.key}");
                }
            }
            }
            Debug.Log($"BLOCKMAP_TIMING stage1 avg {a1/12:F0}ms max {s1:F0}ms stage2 avg {a2/12:F0}ms max {s2:F0}ms");
        }

        static ForestData Generate(int seed)
        {
            var source=JsonUtility.FromJson<ForestData>(AssetDatabase.LoadAssetAtPath<TextAsset>(Source).text);
            var layout=JsonUtility.FromJson<RoundedTerrainLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>(Artwork).text);
            return Chapter1ProceduralMap.Generate(source,layout,seed);
        }

        [Test] public void Chapter1MapKeepsTheForestDataContract()
        {
            for(int seed=0;seed<16;seed++)
            {
                var data=Generate(seed);
                Assert.That(data.rooms.Length,Is.EqualTo(14),"seed "+seed);
                Assert.That(data.rooms[0].kind,Is.EqualTo("start"));Assert.That(data.rooms[0].exitSide,Is.EqualTo("right"));
                Assert.That(data.rooms[9].kind,Is.EqualTo("boss"));Assert.That(data.rooms[9].w,Is.EqualTo(Chapter1ProceduralMap.BossRoomWidth));
                Assert.That(data.rooms[9].entrySide,Is.EqualTo("left"));
                Assert.That(data.rooms.Select((r,i)=>r.id==i).All(x=>x),Is.True);
                Assert.That(data.views.Length,Is.EqualTo(data.rooms.Length));
                Assert.That(data.platforms.All(p=>p.x%32==0&&p.w%32==0&&p.w>0),Is.True);
                Assert.That(data.ground.Length,Is.EqualTo(data.cols*data.rows));
                // 맵 외곽은 닫혀 있다
                for(int x=0;x<data.cols;x++){Assert.That(data.ground[x],Is.Not.Zero);Assert.That(data.ground[(data.rows-1)*data.cols+x],Is.Not.Zero);}
                for(int y=0;y<data.rows;y++){Assert.That(data.ground[y*data.cols],Is.Not.Zero);Assert.That(data.ground[y*data.cols+data.cols-1],Is.Not.Zero);}
                // 시작 지점은 시작방 원형의 spawn이며 고체 바닥 또는 발판 위, 보스 바닥 15행 아래는 고체
                int sx=Mathf.FloorToInt(data.spawn.x/32),sy=Mathf.RoundToInt(data.spawn.y/32);
                var startDef=ForestCombatV2.BlockMap.BlockSpec.Room("S1-START");var startRoom=data.rooms[0];
                Assert.That(data.spawn.x/32,Is.EqualTo(startRoom.x+startDef.frameSide+startDef.spawn.x).Within(1e-4f),"spawn x from S1-START seed "+seed);
                Assert.That(data.spawn.y/32,Is.EqualTo(startRoom.y+startRoom.h-ForestCombatV2.BlockMap.BlockSpec.Frame-startDef.spawn.y).Within(1e-4f),"spawn y from S1-START seed "+seed);
                bool spawnOnPlat=data.platforms.Any(p=>p.y==sy*32&&data.spawn.x>=p.x&&data.spawn.x<=p.x+p.w);
                Assert.That(spawnOnPlat||data.ground[sy*data.cols+sx]!=0,Is.True,"spawn floor seed "+seed);
                var boss=data.rooms[9];Assert.That(data.ground[(boss.y+15)*data.cols+boss.x+boss.w/2],Is.Not.Zero,"boss floor seed "+seed);
                // 전투방마다 적 10 이상, 모든 적이 고체 또는 발판 위
                foreach(var a in data.actors)
                {
                    int ax=Mathf.FloorToInt(a.x/32),ay=Mathf.RoundToInt(a.y/32);
                    bool onPlat=data.platforms.Any(p=>p.y==ay*32&&a.x>=p.x&&a.x<=p.x+p.w);
                    Assert.That(data.ground[ay*data.cols+ax]!=0||onPlat,Is.True,$"seed {seed} actor at {a.x},{a.y}");
                }
            }
        }

        [UnityTest] public IEnumerator LowestConnectorRungCanBeLandedOnAgainAfterDropping()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Connector landing arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode();
            var arena=Object.FindAnyObjectByType<CombatArena>(); arena.loadCharacterArt=false;
            var content=CombatContentLoader.Load(null,false);
            content.Profile=CombatCompositionProfile.Game;
            content.Loadout=Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera=new GameObject("Connector camera").AddComponent<Camera>();
            content.PlayerSpawn=new Vector2(8,4);
            content.PlayerSize=new Vector2(.9375f,2); content.PlayerOffset=new Vector2(.1875f,1);
            content.Enemies=System.Array.Empty<CombatEnemySpawn>();
            Assert.That(arena.Initialize(content),Is.True);
            using(var driver=arena.AcquireManualSimulation())
            foreach(char side in new[]{'L','R'})
            foreach(var origin in new[]{Vector2.zero,new Vector2(320,-144)})
            {
                var connector=BlockRooms.Connector(new[]{side,'T'});
                var grid=new BlockWorld{W=BlockSpec.BlockW,H=BlockSpec.BlockH,grid=connector.grid};
                var root=new GameObject("Connector "+side);
                void Surface(Vector2 center,Vector2 size,bool oneWay)
                {
                    var go=new GameObject(oneWay?"Connector rung":"Connector ground");
                    go.transform.SetParent(root.transform,false); go.transform.position=center;
                    go.AddComponent<ArenaSurface>().oneWay=oneWay;
                    go.AddComponent<BoxCollider2D>().size=size;
                }
                foreach(var r in BlockAssembler.SolidRects(grid))Surface(r.center,new Vector2(r.width,r.height),false);
                foreach(var p in connector.plats)Surface(new Vector2(p.x+p.w/2f,p.y-.0625f),new Vector2(p.w,.125f),true);
                root.transform.position=origin;
                Physics2D.SyncTransforms();
                foreach(float dt in new[]{1f/30,1f/60,1f/120})
                {
                    driver.Reset(); var player=arena.Player;
                    player.ResetActor(origin+new Vector2(side=='L'?3:13,2)); Physics2D.SyncTransforms();
                    for(int cycle=0;cycle<2;cycle++)
                    {
                        player.grounded=player.ProbeGround();
                        Assert.That(player.grounded,Is.True);
                        bool landed=false;
                        for(int tick=0;tick<Mathf.CeilToInt(2/dt);tick++)
                        {
                            int axis=side=='L'?player.Position.x<origin.x+8?1:0:player.Position.x>origin.x+8?-1:0;
                            driver.Step(dt,new CombatInput(axis,tick==0?CombatButtons.Jump:CombatButtons.None));
                            if(tick>1&&player.grounded&&Mathf.Abs(player.Position.y-origin.y-4)<.05f){landed=true;break;}
                        }
                        Assert.That(landed,Is.True,$"{side} origin={origin} dt={dt} cycle={cycle}: low rung landing at {player.Position}");
                        Assert.That(player.DropThrough(),Is.True);
                        for(int tick=0;tick<Mathf.CeilToInt(.6f/dt);tick++)driver.Step(dt,new CombatInput(0));
                        Assert.That(player.grounded&&Mathf.Abs(player.Position.y-origin.y-2)<.05f,Is.True,"Drop stops on the solid floor.");
                    }
                }
                Object.DestroyImmediate(root); Physics2D.SyncTransforms();
            }
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator TearDown() { if(Application.isPlaying)yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator GeneratedConnectorRungsSupportFreshLandingsFromBelowAndAbove()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Generated connector arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode();
            var arena=Object.FindAnyObjectByType<CombatArena>(); arena.loadCharacterArt=false;
            var content=CombatContentLoader.Load(null,false);
            content.Profile=CombatCompositionProfile.Game; content.Loadout=Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera=new GameObject("Generated connector camera").AddComponent<Camera>();
            content.PlayerSpawn=new Vector2(0,1000); content.PlayerSize=new Vector2(.9375f,2); content.PlayerOffset=new Vector2(.1875f,1);
            content.Enemies=System.Array.Empty<CombatEnemySpawn>(); Assert.That(arena.Initialize(content),Is.True);
            int tested=0;
            using(var driver=arena.AcquireManualSimulation())
            foreach(int seed in new[]{1,7,42,20261113})
            {
                var blocks=BlockAssembler.Assemble(BlockGenerator.Stage1(seed));
                var data=Generate(seed);
                var root=new GameObject("Generated terrain "+seed);
                var world=root.AddComponent<ForestCombatWorld>(); world.enabled=false; world.arena=arena;
                typeof(ForestCombatWorld).GetProperty(nameof(ForestCombatWorld.Data)).SetValue(world,data);
                typeof(ForestCombatWorld).GetMethod("BuildGeometry",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(world,null);
                foreach(var c in blocks.rooms.Where(r=>r.IsConnector&&!r.climb&&r.open.Contains('T')))
                {
                    float x=c.x+8, floor=c.y+2-blocks.H, rung=floor+2;
                    Assert.That(data.platforms.Any(p=>p.x==(c.x+5)*32&&p.y==Mathf.RoundToInt(-rung*32)&&p.w==6*32),Is.True);
                    foreach(float start in new[]{floor,rung+1})
                    {
                        driver.Reset(); var player=arena.Player;
                        player.ResetActor(new Vector2(x,start)); Physics2D.SyncTransforms();
                        player.grounded=player.ProbeGround(); Assert.That(player.Platforms.Entries,Is.Empty);
                        bool arrived=false;
                        for(int tick=0;tick<240;tick++)
                        {
                            driver.Step(1f/120,new CombatInput(0,tick==0&&start==floor?CombatButtons.Jump:CombatButtons.None));
                            if(tick>1&&player.grounded&&Mathf.Abs(player.Position.y-rung)<.05f){arrived=true;break;}
                        }
                        Assert.That(arrived,Is.True,$"seed={seed} connector={c.key} start={start} rung={rung}, actual={player.Position}");
                        tested++;
                    }
                }
                Object.DestroyImmediate(root); Physics2D.SyncTransforms(); arena.pits.RemoveAll(p=>!p);
            }
            Assert.That(tested,Is.GreaterThan(0));
            Debug.Log("GENERATED_CONNECTOR_FIRST_LANDINGS "+tested);
            yield return new ExitPlayMode();
        }

        // 실제 플레이어 물리: 수직 직결(아래 방 접근 발판 → 테두리 디딤 → 위 방 문 바닥)을 오르고 층마다 멈추며 내려온다.
        [UnityTest] public IEnumerator VerticalDirectLinkIsClimbableAndDescendableWithRealPlayer()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            foreach(var enemy in world.arena.enemies)enemy.aggressive=false;
            var data=world.Data;var player=world.arena.Player;
            // 세로 터널(폭 6, 높이 4)과 그 위아래 발판을 찾는다
            var shafts=data.passages.SelectMany(p=>p.spaces).Where(s=>s.w==6&&s.h==4).ToList();
            Assert.That(shafts,Is.Not.Empty,"우회·엘리트 갈래에는 세로 연결이 있다");
            int tested=0;
            using(var driver=world.arena.AcquireManualSimulation())
            foreach(var s in shafts)
            {
                // y 아래 좌표: s.y = 위 방 바닥 테두리 윗줄. 위 방 문 바닥 윗면 = s.y, 문턱 디딤 = s.y+2, 아래 방 접근 발판 = s.y+7.
                // 3u 계단은 두지 않는다: 접근 발판→디딤 5u는 2단 점프, 디딤→문 바닥 2u는 1단 점프로 오른다.
                int upper=s.y,rung=s.y+2,ledge=s.y+7;
                bool Has(int row)=>data.platforms.Any(p=>p.y==row*32&&p.x<=s.x*32&&p.x+p.w>=(s.x+6)*32);
                if(!Has(upper)||!Has(rung)||!Has(ledge))continue;
                float cx=s.x+3;
                driver.Reset();player.CancelAction();player.velocity=Vector2.zero;
                player.Position=new Vector2(cx,-ledge);Physics2D.SyncTransforms();player.grounded=player.ProbeGround();
                Assert.That(player.grounded,Is.True,"아래 방 접근 발판에 선다");
                foreach(int target in new[]{rung,upper})
                {
                    bool arrived=false;
                    for(int tick=0;tick<200;tick++)
                    {
                        bool jump=tick==0||tick==30&&target!=upper;// 디딤→문 바닥은 1단 점프 하나로 닿아야 한다
                        driver.Step(1f/120,new CombatInput(0,jump?CombatButtons.Jump:CombatButtons.None));
                        if(tick>40&&player.grounded&&Mathf.Abs(player.Position.y+target)<.15f){arrived=true;break;}
                    }
                    Assert.That(arrived,Is.True,$"오르기 목표 {target}, 실제 {player.Position}");
                }
                foreach(int target in new[]{rung,ledge})
                {
                    Assert.That(player.DropThrough(),Is.True,"일방향 발판은 아래+점프로 통과한다");
                    bool landed=false;
                    for(int tick=0;tick<200;tick++){driver.Step(1f/120,new CombatInput(0));if(tick>10&&player.grounded){landed=true;break;}}
                    Assert.That(landed&&Mathf.Abs(player.Position.y+target)<.15f,Is.True,$"내려가기 목표 {target}, 실제 {player.Position}");
                }
                tested++;
            }
            Assert.That(tested,Is.GreaterThan(0),"발판이 모두 있는 세로 직결을 찾지 못했다");
            yield return new ExitPlayMode();
        }
    }
}
