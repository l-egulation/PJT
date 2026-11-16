using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;
using Deviant.Combat.Contracts;
using ForestCombatV2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class InkStatusVisualTests
    {
        static void Prepare(bool art=false){DummyTrainingTools.Prepare();Object.FindAnyObjectByType<CombatArena>().loadCharacterArt=art;}
        [UnityTearDown] public IEnumerator TearDown()
        {if(Application.isPlaying)yield return new ExitPlayMode();DummyTrainingTools.SelectNormalMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        static void Tick(CombatArena.ManualSimulation sim,float time){for(int i=0;i<Mathf.CeilToInt(time/.02f);i++)sim.Step(.02f);}
        static void Equip(DummyTrainingRoom room,string id)
        {room.SetOpen(true);for(int i=0;i<6;i++)Assert.That(room.ApplyInkPart(0,i,i==0?id:null,out var error),Is.True,error);room.SetOpen(false);}
        static void Hit(CombatArena arena,CombatActor target,bool special=false)
        {
            var w=arena.Player.weapons[0];arena.TryBindActor(arena.Player,out var source);
            var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"status-test",Guid.Empty,CombatHitKind.Melee,
                stamp:new CombatAttackStamp(w.InstanceId,arena.catalog.forms[w.FormIndex].id,"status-test","test",!special,-1,w.InkPalette.Read()),sourceFacing:1);
            Assert.That(target.Receive(1,arena.Player,1,false,null,origin:origin),Is.True);
        }
        static void BindInk(WeaponGrowth growth,CombatPlayer player,int weapon,string color,string part)
        {var palette=growth.GrantPalette(new[]{color,color,color});growth.Bind(palette.Id,player.weapons[weapon].InstanceId);growth.Insert(player.weapons[weapon].InstanceId,0,growth.GrantInk(part).Id);}
        static Transform Root(CombatArena arena,CombatActor actor)=>arena.transform.Find("[Ink status] "+actor.name);
        static SpriteRenderer Renderer(CombatArena arena,CombatActor actor,string name)
        {var root=Root(arena,actor);Assert.That(root,Is.Not.Null,actor.name);return root.Find(name).GetComponent<SpriteRenderer>();}
        [UnityTest] public IEnumerator OpenRandomStageShowsSharedStatusesWithoutTrainingOverlay()
        {
            DummyTrainingTools.SelectNormalMode();
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();yield return null;
            for(int frame=0;frame<600&&!(Object.FindAnyObjectByType<ForestCombatWorld>()?.Ready??false);frame++)yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var diagnostics=world.diagnostics;
            Assert.That(world.Ready,Is.True);Assert.That(world.dummyTrainingOnly,Is.False);
            Assert.That(world.GetComponent<DummyTrainingRoom>(),Is.Null);
            Assert.That(world.Data.rooms.Length,Is.EqualTo(14));
            Assert.That(Object.FindAnyObjectByType<Chapter1RunBinding>(),Is.Not.Null);
            // 랜덤 런은 맨손으로 출발하고 스케치 장비가 무기를 맡는다(run-structure.md 런 시작 장비). 옛 실험 팔레트 편집은
            // 허수아비 훈련 테스트가 확인하고, 여기서는 실험 창이 전투를 멈추는지만 본다.
            Assert.That(diagnostics.SetInkExperimentOpen(true),Is.True);
            float now=arena.Now;yield return null;yield return null;
            Assert.That(arena.Now,Is.EqualTo(now));Assert.That(arena.Player.EditingAllowed,Is.True);
            // 스케치 장비 런에서는 옛 색상 잉크(MvpGrowth) 전투 효과가 꺼지고 독 잉크도 없어졌다(5de8feebd).
            // 몹 상태 표시는 허수아비 훈련 테스트가 맡고, 여기서는 플레이어 보호막 표시만 확인한다.
            arena.Player.shield=4;arena.Player.guardUntil=arena.Now+1;arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadInkStatusVisuals(arena.Player).Shield,Is.EqualTo(4));
            // 5df9340b부터 승인 방패 연출이 스킬 보호막을 그리면 잉크 캡슐은 숨는다(그보다 큰 잉크 보호막만 캡슐로 보인다).
            bool approvedGuard=arena.ShieldPresentation!=null && arena.ShieldPresentation.DisplaysGuard(arena.Player);
            Assert.That(Renderer(arena,arena.Player,"Shield capsule").enabled,Is.EqualTo(!approvedGuard),"the capsule only covers what the approved shield art does not draw");
            Assert.That(diagnostics.SetInkExperimentOpen(false),Is.True);
            Assert.That(arena.TestConfigurationOpen,Is.False);Assert.That(diagnostics.InkExperimentOpen,Is.False);
            using(var sim=arena.AcquireManualSimulation())Tick(sim,.1f);
            Assert.That(arena.Now,Is.GreaterThan(now));
            Assert.That(diagnostics.SetInkExperimentOpen(true),Is.True);
            diagnostics.enabled=false;
            Assert.That(diagnostics.InkExperimentOpen,Is.False);Assert.That(arena.TestConfigurationOpen,Is.False);
        }
        [UnityTest] public IEnumerator ShieldTracksAbsorptionExpiryAndReadOnlyViewsPauseWithSimulation()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            using(var sim=arena.AcquireManualSimulation())
            {
                Equip(room,"g_shield");for(int i=0;i<4;i++)Hit(arena,target);
                arena.RefreshInkStatusVisuals();var capsule=Renderer(arena,arena.Player,"Shield capsule");
                Assert.That(capsule.enabled,Is.True);Assert.That(arena.ReadInkStatusVisuals(arena.Player).Shield,Is.EqualTo(8));
                Assert.That(Root(arena,arena.Player).GetComponentsInChildren<Collider2D>(true),Is.Empty,"status decoration must never participate in combat collision");
                float hp=arena.Player.health,now=arena.Now,shield=arena.ReadInkShield();int hits=arena.Player.ReceivedHitCount;
                for(int i=0;i<10;i++)arena.ReadInkStatusVisuals(arena.Player);
                Assert.That(arena.Now,Is.EqualTo(now));Assert.That(arena.Player.health,Is.EqualTo(hp));Assert.That(arena.ReadInkShield(),Is.EqualTo(shield));Assert.That(arena.Player.ReceivedHitCount,Is.EqualTo(hits));
                room.SetOpen(true);var scale=capsule.transform.localScale;var position=capsule.transform.position;
                yield return null;yield return null;arena.RefreshInkStatusVisuals();
                Assert.That(arena.Now,Is.EqualTo(now));Assert.That(capsule.transform.localScale,Is.EqualTo(scale));Assert.That(capsule.transform.position,Is.EqualTo(position));
                room.SetOpen(false);arena.ShowInkStatusVisuals=false;arena.RefreshInkStatusVisuals();Assert.That(Root(arena,arena.Player).gameObject.activeSelf,Is.False);
                arena.Player.Receive(3,target,1,false,null);Assert.That(arena.Player.health,Is.EqualTo(hp));Assert.That(arena.ReadInkShield(),Is.EqualTo(5));
                arena.ShowInkStatusVisuals=true;arena.RefreshInkStatusVisuals();Assert.That(Root(arena,arena.Player).gameObject.activeSelf,Is.True);
                Tick(sim,3.2f);arena.RefreshInkStatusVisuals();Assert.That(arena.ReadInkStatusVisuals(arena.Player).Shield,Is.Zero);Assert.That(Root(arena,arena.Player).gameObject.activeSelf,Is.False);
                arena.Player.shield=4;arena.Player.guardUntil=arena.Now+1;arena.RefreshInkStatusVisuals();Assert.That(capsule.enabled,Is.True,"native skill shields use the same capsule");
                Tick(sim,1.1f);arena.RefreshInkStatusVisuals();Assert.That(Root(arena,arena.Player).gameObject.activeSelf,Is.False);
                arena.enabled=false;Assert.That(Root(arena,arena.Player).gameObject.activeSelf,Is.False);arena.enabled=true;
            }
        }
        [UnityTest] public IEnumerator ColdMarksBurnAndTargetResetUseTheSameViewsOnOrdinaryEnemiesAndDummies()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];var other=room.Targets[3];
            using(var sim=arena.AcquireManualSimulation())
            {
                Equip(room,"b_prison");Hit(arena,target,true);arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusVisuals(target).Frozen,Is.True);Assert.That(Renderer(arena,target,"Frozen crystal").enabled,Is.True);
                Tick(sim,.7f);arena.RefreshInkStatusVisuals();Assert.That(arena.ReadInkStatusVisuals(target).Frozen,Is.False);Assert.That(Renderer(arena,target,"Frozen crystal").enabled,Is.False);
                Equip(room,"b_slow");Hit(arena,target);arena.RefreshInkStatusVisuals();Assert.That(SheetOf(Renderer(arena,target,"Slow")),Is.EqualTo("slow-snail"),"slows show the snail over the head");
                Equip(room,"b_frost");Hit(arena,target);arena.RefreshInkStatusVisuals();Assert.That(arena.ReadInkStatusVisuals(target).Frost,Is.EqualTo(1));Assert.That(Renderer(arena,target,"Frost 1").enabled,Is.True);
                Equip(room,"p_echo");Hit(arena,target);Assert.That(arena.ReadInkStatusVisuals(target).Marks,Is.Zero);Tick(sim,.7f);arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusVisuals(target).Marks,Is.EqualTo(2));Assert.That(Renderer(arena,target,"Mark 2").enabled,Is.True);Assert.That(Renderer(arena,target,"Mark 3").enabled,Is.False);
                Equip(room,"r_burn");Hit(arena,target);arena.RefreshInkStatusVisuals();Assert.That(Renderer(arena,target,"Burn 1").enabled,Is.True);
                Equip(room,"p_mark");Hit(arena,other);arena.RefreshInkStatusVisuals();float now=arena.Now;var charge=arena.Player.weapons[0].Cooldown.Snapshot;
                target.Restore();arena.RefreshInkStatusVisuals();Assert.That(arena.ReadInkStatusVisuals(target).Any,Is.False);Assert.That(Root(arena,target).gameObject.activeSelf,Is.False);
                Assert.That(arena.ReadInkStatusVisuals(other).Marks,Is.EqualTo(1));Assert.That(Renderer(arena,other,"Mark 1").enabled,Is.True);
                Assert.That(arena.Now,Is.EqualTo(now));Assert.That(arena.Player.weapons[0].Cooldown.NextReady,Is.EqualTo(charge.NextReady));
                var ordinary=arena.SpawnEnemy(0,new Vector2(30,1000));ordinary.aggressive=false;
                Equip(room,"b_prison");Hit(arena,ordinary,true);arena.RefreshInkStatusVisuals();Assert.That(Renderer(arena,ordinary,"Frozen crystal").enabled,Is.True);
                ordinary.ResetEnemy();arena.RefreshInkStatusVisuals();Assert.That(Root(arena,ordinary).gameObject.activeSelf,Is.False);
                Tick(sim,5.1f);arena.RefreshInkStatusVisuals();Assert.That(arena.ReadInkStatusVisuals(other).Marks,Is.Zero);Assert.That(Root(arena,other).gameObject.activeSelf,Is.False);
            }
        }
        [UnityTest] public IEnumerator RenderMinimumStatusOverviewWithActualApprovedCharacter()
        {
            Prepare(true);yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();
            using(var sim=arena.AcquireManualSimulation())
            {
                arena.Player.Position=new Vector2(0,1000);arena.Player.velocity=Vector2.zero;
                for(int i=0;i<3;i++)room.Targets[i].Position=new Vector2(2+i*2,1000);
                Equip(room,"p_mark");for(int i=0;i<3;i++){Hit(arena,room.Targets[1]);Tick(sim,.62f);}
                // Put the player back after fixture ticks, then freeze simulation during capture.
                arena.Player.Position=new Vector2(0,1000);arena.Player.velocity=Vector2.zero;
                Equip(room,"g_shield");for(int i=0;i<4;i++)Hit(arena,room.Targets[5]);
                Equip(room,"r_burn");Hit(arena,room.Targets[2]);Equip(room,"b_prison");Hit(arena,room.Targets[0],true);
                arena.RefreshInkStatusVisuals();yield return null;
                Assert.That(Renderer(arena,arena.Player,"Shield capsule").enabled,Is.True);
                Assert.That(Renderer(arena,room.Targets[0],"Frozen crystal").enabled,Is.True);
                Assert.That(Renderer(arena,room.Targets[1],"Mark 3").enabled,Is.True);
                Capture(world.View);
            }
        }
        // 54종 잉크(MVP)가 몬스터에게 거는 상태는 resources effects/ink-status 확정 시트로 그린다(2026-11-06).
        static WeaponGrowth Growth(CombatArena arena)
        {
            var growth=new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
            arena.MvpGrowth=growth;return growth;
        }
        static void EquipMvp(WeaponGrowth growth,CombatPlayer player,int weapon,string color,string part)
        {var palette=growth.GrantPalette(new[]{color,color,color});growth.Bind(palette.Id,player.weapons[weapon].InstanceId);growth.Insert(player.weapons[weapon].InstanceId,0,growth.GrantInk(part).Id);}
        static void HitWith(CombatArena arena,CombatActor target,int weapon)
        {
            var w=arena.Player.weapons[weapon];arena.TryBindActor(arena.Player,out var source);
            var origin=new CombatHitOrigin(source,default,Guid.NewGuid(),0,"status-test",Guid.Empty,CombatHitKind.Melee,
                stamp:new CombatAttackStamp(w.InstanceId,"base_sword","status-test","test",true,-1,w.InkPalette.Read()),sourceFacing:1);
            Assert.That(target.Receive(.01f,arena.Player,1,true,null,origin:origin),Is.True);
        }
        static string SheetOf(SpriteRenderer renderer)=>renderer.enabled && renderer.sprite?renderer.sprite.name.Split(' ')[0]:null;
        static string VariantOf(SpriteRenderer renderer)=>renderer.sprite.name.Split(' ')[1];
        static SpriteRenderer[] Live(CombatArena arena,CombatActor actor,string sheet)
            =>Root(arena,actor).GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled && r.gameObject.name==sheet).ToArray();
        [UnityTest] public IEnumerator MvpMonsterStatusesUseTheSelectedSheets()
        {
            Prepare();yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();var arena=world.arena;var room=world.GetComponent<DummyTrainingRoom>();var target=room.Targets[2];
            var growth=Growth(arena);arena.Player.CancelAction();arena.Player.selected=0;arena.Player.facing=1;
            arena.RefreshInkStatusVisuals();
            using(var sim=arena.AcquireManualSimulation())
            {
                // 표식: 머리 위 잉크 방울이 쌓인 순번 색(행)으로 한 줄
                EquipMvp(growth,arena.Player,1,"red","ink3_red_04");
                for(int i=0;i<3;i++){HitWith(arena,target,0);sim.Step(.02f);}
                arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusVisuals(target).MarkDisplay,Is.EqualTo(3));
                var colours=new[]{"red","orange","yellow"};
                for(int i=0;i<3;i++)
                {var mark=Renderer(arena,target,"Mark "+(i+1));Assert.That(SheetOf(mark),Is.EqualTo("mark-drop"));Assert.That(VariantOf(mark),Is.EqualTo(colours[i]));}
                Assert.That(Renderer(arena,target,"Mark 4").enabled,Is.False);
                var drop=Renderer(arena,target,"Mark 1");Assert.That(drop.sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(drop.transform.localScale,Is.EqualTo(Vector3.one),"sheets draw at native size");
                // 다른 무기로 터트리면 노랑부터 하나씩: 머리 위 방울 터짐 + 몸 뒤 잉크 튐 + 몸을 그 표식 색으로 물들임
                HitWith(arena,target,1);sim.Step(.01f);arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusParticles(target,"mark-pop"),Is.EqualTo(1));Assert.That(arena.ReadInkStatusParticles(target,"mark-burst"),Is.EqualTo(1));
                var tint=arena.ReadInkStatusBodyTint(target);var yellow=CombatArena.MvpMarkColors[2];
                Assert.That(tint.r,Is.EqualTo(yellow.r).Within(.01f));Assert.That(tint.g,Is.EqualTo(yellow.g).Within(.01f));Assert.That(tint.a,Is.GreaterThan(.4f));
                Assert.That(Live(arena,target,"mark-burst")[0].sortingOrder,Is.LessThan(target.visual.sortingOrder),"the splash sits behind the body");
                Assert.That(Renderer(arena,target,"Mark 3").enabled,Is.False,"the popped drop leaves the row");
                Tick(sim,.6f);arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusParticles(target,"mark-burst"),Is.Zero,"one-shot splashes end");
                Assert.That(arena.ReadInkStatusBodyTint(target).a,Is.Zero,"the colour flash fades in 0.2 s");
                // 속박: 몸 중심을 감싼 밧줄 세 줄. 앞쪽 반은 몸 앞, 뒤쪽 반은 몸 뒤에 즉시 표시한다.
                target.rootUntil=arena.Now+1;arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusParticles(target,"chain-in"),Is.Zero);
                Tick(sim,.2f);arena.RefreshInkStatusVisuals();
                var front=Renderer(arena,target,"Bind front");var back=Renderer(arena,target,"Bind back");
                Assert.That(SheetOf(front),Is.EqualTo("bind-front"));Assert.That(SheetOf(back),Is.EqualTo("bind-back"));
                Assert.That(VariantOf(front),Is.EqualTo(target.size.x>=2?"large":"small"));
                Assert.That(front.sortingOrder,Is.GreaterThan(target.visual.sortingOrder));Assert.That(back.sortingOrder,Is.LessThan(target.visual.sortingOrder));
                Assert.That(front.transform.localPosition,Is.EqualTo(Vector3.zero),"the ropes are centered on the body");
                // 기절: 표식 줄 위의 별 고리
                target.stunUntil=arena.Now+.5f;arena.RefreshInkStatusVisuals();
                var stun=Renderer(arena,target,"Stun");Assert.That(SheetOf(stun),Is.EqualTo("stun-stars"));
                Assert.That(stun.transform.localPosition.y,Is.GreaterThan(Renderer(arena,target,"Mark 1").transform.localPosition.y));
                Tick(sim,1.1f);arena.RefreshInkStatusVisuals();
                Assert.That(Renderer(arena,target,"Bind front").enabled,Is.False);Assert.That(Renderer(arena,target,"Stun").enabled,Is.False);
                // 중독: 몸 범위 안 무작위 해골 거품(중첩당 초당 2.5개)과 보라 몸 색
                EquipMvp(growth,arena.Player,0,"purple","ink3_purple_05");
                HitWith(arena,target,0);sim.Step(.02f);Assert.That(arena.ReadInkStatusVisuals(target).Poison,Is.EqualTo(1));
                int seen=0;var half=new Vector2(target.size.x,target.size.y)*.5f;
                for(int i=0;i<40;i++)
                {
                    sim.Step(.02f);arena.RefreshInkStatusVisuals();seen=Mathf.Max(seen,arena.ReadInkStatusParticles(target,"poison-skull"));
                    foreach(var skull in Live(arena,target,"poison-skull"))
                    {var p=skull.transform.localPosition;Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(half.x+.001f));Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(half.y+.001f));}
                }
                Assert.That(seen,Is.InRange(1,3),"about 2.5 skulls a second for one stack, each living 0.8 s");
                var purple=arena.ReadInkStatusBodyTint(target);Assert.That(purple.b,Is.GreaterThan(purple.g));Assert.That(purple.a,Is.GreaterThan(.1f));
                // 몹 표현 코드(650~950)는 전투장(0)보다 늦게 좌우를 다시 쓴다. 보라 실루엣은 그 마지막 모양을 따른다.
                var body=target.visual;var purpleBody=body.transform.Find("[Ink status] body tint").GetComponent<SpriteRenderer>();
                Assert.That(purpleBody.flipX,Is.EqualTo(body.flipX));Assert.That(purpleBody.transform.localScale,Is.EqualTo(Vector3.one),"the tint mirrors with flipX, not a negative scale");
                body.flipX=!body.flipX;purpleBody.GetComponent<InkStatusBodyTint>().Follow();
                Assert.That(purpleBody.flipX,Is.EqualTo(body.flipX),"a presenter that mirrors the body after the arena mirrors the purple silhouette too");
                Assert.That(purpleBody.sprite,Is.SameAs(body.sprite));
                Tick(sim,3.2f);arena.RefreshInkStatusVisuals();
                Assert.That(arena.ReadInkStatusVisuals(target).Poison,Is.Zero);Assert.That(arena.ReadInkStatusBodyTint(target).a,Is.Zero);
                Assert.That(Root(arena,target).GetComponentsInChildren<Collider2D>(true),Is.Empty,"status decoration never takes part in combat collision");
            }
        }
        static void Capture(Camera camera)
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var texture=new RenderTexture(1280,480,24);var image=new Texture2D(1280,480,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float size=camera.orthographicSize;var rect=camera.rect;
            var clear=camera.clearFlags;var background=camera.backgroundColor;
            try
            {
                camera.transform.position=new Vector3(3,1001,-10);camera.orthographicSize=1.9f;camera.rect=new Rect(0,0,1,1);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.075f,.1f);camera.targetTexture=texture;
                camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1280,480),0,0);image.Apply();
                var pixels=image.GetPixels();int cyan=0,red=0,orange=0;
                foreach(var pixel in pixels)
                {if(pixel.g>.5f && pixel.b>.5f && pixel.r<.6f)cyan++;if(pixel.r>.45f && pixel.g<.2f && pixel.b<.2f)red++;if(pixel.r>.6f && pixel.g<.5f && pixel.b<.3f)orange++;}
                string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../outputs/ink-status-ui"));Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"overview.png"),image.EncodeToPNG());
                Assert.That(cyan,Is.GreaterThan(100),"shield/ice are actually rendered");Assert.That(red,Is.GreaterThan(20),"the first (red) mark drop is actually rendered");Assert.That(orange,Is.GreaterThan(20),"burn flames are actually rendered");
            }
            finally{camera.targetTexture=previous;camera.transform.position=position;camera.orthographicSize=size;camera.rect=rect;camera.clearFlags=clear;camera.backgroundColor=background;RenderTexture.active=active;texture.Release();Object.Destroy(texture);Object.Destroy(image);}
        }
    }
}
