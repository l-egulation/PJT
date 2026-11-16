using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    // 2챕터 런타임 조립 배치의 C2Layout 계약과 실제 플레이어 물리로 본 벽타기 통로.
    public sealed class Chapter2BlockLayoutTests
    {
        [Test] public void RuntimeLayoutsKeepTheChapterTwoContractForManySeeds()
        {
            var bank=C2Bank.Load();
            for(int seed=0;seed<12;seed++)
            {
                var l=bank.Generate(seed);
                Assert.That(C2Bank.Validate(l),Is.Empty,"seed "+seed);
                Assert.That(l.rooms.Select(r=>r.id),Is.EquivalentTo(new[]{"S","M","E","B","SEAL","CHEST"}.Concat(Enumerable.Range(1,C2Bank.CombatRooms).Select(i=>"F"+i.ToString("00")))));
                // 봉인 도전방: 주 경로에서 갈라지는 막다른 방이라 철창은 쓰는 출입구 하나에만 내려온다(stage-gimmicks.md)
                var seal=l.Room("SEAL");var chest=l.Room("CHEST");
                Assert.That(seal.kind,Is.EqualTo("seal"));Assert.That(chest.kind,Is.EqualTo("chest"));
                Assert.That(seal.main||chest.main,Is.False,"gimmick rooms are branches seed "+seed);
                Assert.That(l.seal.roomId,Is.EqualTo("SEAL"));Assert.That(l.seal.gates.Length,Is.EqualTo(1),"one gate on the dead-end seal seed "+seed);
                Assert.That(l.ports.Count(p=>p.roomId=="SEAL"&&p.enabled),Is.EqualTo(1),"seal room has one open door seed "+seed);
                Assert.That(seal.Bounds.Contains(l.seal.Marker+Vector2.up*.25f),Is.True,"tablet inside the seal room seed "+seed);
                Assert.That(chest.Bounds.Contains(l.chest.Marker+Vector2.up*.25f),Is.True,"chest inside the chest room seed "+seed);
                Assert.That(l.ports.Count(p=>p.roomId=="CHEST"&&p.enabled),Is.EqualTo(2),"the chest room sits inside the loop seed "+seed);
                // 열쇠 운반자는 상자 갈래 뒤의 주 경로 전투방에서 고른다
                Assert.That(l.carrierRooms,Is.Not.Empty,"carrier rooms seed "+seed);
                Assert.That(l.carrierRooms.All(id=>l.Room(id).kind=="combat"&&l.Room(id).main),Is.True,"carrier rooms are main combat rooms seed "+seed);
                Assert.That(l.carrierMap,Is.Not.Null);Assert.That(l.carrierMapOffset,Is.EqualTo(l.carrierMap.rows));
                Assert.That(l.actors.Count(a=>a.id=="E:elite"&&a.roomId=="E"),Is.EqualTo(1));
                Assert.That(l.actors.Select(a=>a.id).Distinct().Count(),Is.EqualTo(l.actors.Length));
                // 보스 전투장(30×18, 원점 boss-(0,3))은 B 방 안쪽과 같은 폭이고 바닥이 같다
                var b=l.Room("B");Assert.That(l.boss.x,Is.EqualTo(b.x));Assert.That(l.boss.x+30,Is.EqualTo(b.x+b.w));Assert.That(l.boss.y,Is.EqualTo(b.y));
                // 입구·출구는 위쪽: 시작방과 보스방 모두 꼭대기 근처
                float top=l.rooms.Max(r=>r.y+r.h);
                Assert.That(l.Room("S").y+l.Room("S").h,Is.EqualTo(top).Within(24f),"start near top seed "+seed);
                // 방 범위는 안쪽이라 테두리(2+2)만큼 차이 난다: 보스방 꼭대기가 시작방 바닥 높이 이상
                Assert.That(b.y+b.h+4,Is.GreaterThanOrEqualTo(l.Room("S").y),"boss in the top band seed "+seed);
                Assert.That(l.wallJumpSpaces.Length,Is.InRange(1,3),"벽타기 통로 1~3개 seed "+seed);
                // 보스 앞 48u 가로 통로: 바닥이 보스 전투장 바닥과 같고, 보스 쪽 끝이 보스방 벽(1u)에 닿는다
                Assert.That(l.bossHall,Is.Not.Null,"boss hall seed "+seed);
                Assert.That(l.bossHall.w,Is.EqualTo(48));Assert.That(l.bossHall.h,Is.EqualTo(6));
                Assert.That(l.bossHall.y,Is.EqualTo(l.boss.y),"boss hall floor seed "+seed);
                if(l.bossHallToLeft)Assert.That(l.bossHall.x,Is.EqualTo(b.x+b.w+1),"boss on the left seed "+seed);
                else Assert.That(l.bossHall.x+l.bossHall.w,Is.EqualTo(b.x-1),"boss on the right seed "+seed);
                Assert.That(l.surfaces.All(s=>s.x==Mathf.Round(s.x)&&s.w==Mathf.Round(s.w)),Is.True,"정수 격자 seed "+seed);
                Assert.That(JsonUtility.ToJson(bank.Generate(seed)),Is.EqualTo(JsonUtility.ToJson(l)),"같은 시드 같은 배치 "+seed);
            }
        }

        [Test] public void DoorApproachZonesCoverEveryOpenSideDoor()
        {
            var l=C2Bank.Load().Generate(17);
            foreach(var p in l.ports.Where(p=>p.enabled&&(p.side=="left"||p.side=="right")))
            {
                var zones=Chapter2BlockLayout.ApproachZones(l,p.roomId);
                Assert.That(zones.Any(z=>z.xMin<=p.x+.01f&&z.xMax>=p.x-.01f&&Mathf.Abs(z.yMin-(p.y-p.opening/2))<.01f),Is.True,p.roomId+":"+p.id);
            }
        }

        // 벽타기 수직 통로: 문 바닥에서 벽 한 면을 ↑로 올라 꼭대기 착지 발판에 서고, 2u 위 방 바닥으로 오른다.
        [UnityTest] public IEnumerator WallClimbCorridorIsClimbableWithRealPlayer()
        {
            EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var setup=Object.FindAnyObjectByType<Chapter2World>();setup.seed=17;
            yield return new EnterPlayMode();
            Chapter2World world=null;float deadline=Time.realtimeSinceStartup+30;
            do{yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);
            var shaft=world.Layout.wallJumpSpaces.First();var player=world.arena.Player;
            foreach(var e in world.arena.enemies)e.aggressive=false;
            using(var driver=world.arena.AcquireManualSimulation())
            {
                driver.Reset();player.CancelAction();player.velocity=Vector2.zero;
                player.Position=new Vector2(shaft.x+.6f,shaft.y);Physics2D.SyncTransforms();player.grounded=player.ProbeGround();
                Assert.That(player.grounded,Is.True,"벽타기 통로 문 바닥에 선다");
                // 점프로 벽 쪽에 뜬 뒤 ↑를 누른 채 벽을 오른다(↑+점프는 벽차기라 쓰지 않는다)
                float landing=shaft.y+shaft.h;bool above=false;
                for(int tick=0;tick<400&&!above;tick++)
                {
                    driver.Step(1f/120,new CombatInput(-1,tick==0?CombatButtons.Jump:CombatButtons.None,climbAxis:tick>=20?1:0));
                    above=player.Position.y>landing+.3f;
                }
                Assert.That(above,Is.True,$"벽 한 면으로 착지 발판 위까지 오른다: {player.Position}, 목표 {landing}");
                bool landed=false;
                for(int tick=0;tick<120&&!landed;tick++){driver.Step(1f/120,new CombatInput(1));landed=player.grounded&&Mathf.Abs(player.Position.y-landing)<.15f;}
                Assert.That(landed,Is.True,$"착지 발판에 선다: {player.Position}");
                // 착지 발판에서 위 방 바닥 문 발판까지 4u: 2단 점프
                bool upper=false;
                for(int tick=0;tick<200&&!upper;tick++){driver.Step(1f/120,new CombatInput(0,tick==0||tick==30?CombatButtons.Jump:CombatButtons.None));upper=tick>40&&player.grounded&&Mathf.Abs(player.Position.y-(landing+4))<.15f;}
                Assert.That(upper,Is.True,$"위 방 바닥으로 오른다: {player.Position}");
            }
            yield return new ExitPlayMode();
        }

        // 봉인 도전(8·10·12마리 3페이지)과 잠긴 상자·열쇠 운반자가 2챕터에서도 1챕터 구성품으로 돈다(stage-gimmicks.md).
        [UnityTest] public IEnumerator SealChallengeAndKeyCarrierRunInChapterTwo()
        {
            EditorSceneManager.OpenScene(Chapter2Tools.ScenePath);
            var setup=Object.FindAnyObjectByType<Chapter2World>();setup.seed=17;
            yield return new EnterPlayMode();
            Chapter2World world=null;float deadline=Time.realtimeSinceStartup+30;
            do{yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);
            var seal=world.Seal;var keys=world.Keys;var player=world.arena.Player;
            Assert.That(seal.Prepare(),Is.True);Assert.That(seal.Host,Is.InstanceOf<Chapter2GimmickHost>());
            Assert.That(ForestCombatV2.Chapter1KeyChest.LoadSprite(Chapter2GimmickHost.Art,"seal-dormant",Vector2.zero),Is.Not.Null,"recoloured chapter 2 art is placed");
            for(int i=0;i<10&&!keys.Chest;i++)yield return null;
            var carrier=AssertKeyCarrier(world);
            // 봉인 도전: 철창은 막다른 방의 출입구 하나, 3페이지가 각 페이지 크기만큼 떨어진다
            world.WarpToRoom("SEAL");foreach(var e in world.arena.enemies)e.aggressive=false;
            player.Position=seal.TabletPosition;player.velocity=Vector2.zero;Physics2D.SyncTransforms();
            Assert.That(seal.TryStart(),Is.True);Assert.That(seal.GatesShut,Is.True);Assert.That(seal.Site.Gates.Length,Is.EqualTo(1));
            var room=world.Layout.Room("SEAL");
            for(int page=0;page<ForestCombatV2.Chapter1SealChallenge.PageSizes.Length;page++)
            {
                int expected=ForestCombatV2.Chapter1SealChallenge.PageSizes[page],count=0;
                for(int i=0;i<4000&&count<expected;i++){yield return null;player.health=player.maxHealth;count=seal.Spawned.Count();}
                Assert.That(count,Is.EqualTo(expected),$"page {page+1} drops {expected}");
                foreach(var enemy in seal.Spawned.ToArray())
                {
                    Assert.That(enemy,Is.InstanceOf<Chapter2NormalEnemy>());
                    Assert.That(enemy.Position.x,Is.InRange(room.x-1f,room.x+room.w+1f),"falls inside the seal room");
                    enemy.Receive(10000,player,1,true,null,true);
                }
                for(int i=0;i<4000&&seal.Page==page&&seal.State==ForestCombatV2.Chapter1SealChallenge.ChallengeState.Active;i++){yield return null;player.health=player.maxHealth;}
            }
            Assert.That(seal.State,Is.EqualTo(ForestCombatV2.Chapter1SealChallenge.ChallengeState.Succeeded));
            Assert.That(seal.GatesShut,Is.False);Assert.That(seal.RewardChest,Is.Not.Null);
            // 운반자를 멀리서 쓰러뜨려도 열쇠를 즉시 습득한다
            Assert.That(Vector2.Distance(player.Position,carrier.Position),Is.GreaterThan(2f));
            carrier.Receive(10000,player,1,true,null,true);
            // The stage reports defeated enemies during its next Update. No pickup movement is needed.
            for(int i=0;i<10&&!keys.HasKey;i++)yield return null;
            Assert.That(keys.HasKey,Is.True,"the key is granted immediately in chapter 2 as well");
            Assert.That(keys.KeyEffect,Is.Not.Null);
            Assert.That(Vector2.Distance(keys.KeyEffect.transform.position,player.Position+Vector2.up*(player.size.y+.3f)),Is.LessThan(1f));
            yield return new ExitPlayMode();
        }
        // EnterPlayMode reloads the domain: keep lambdas out of the iterator (their captures do not survive the reload).
        // 열쇠 운반자: 상자 갈래 뒤 주 경로 전투방의 걷는 몹(K4·M3)이고 운반자 경로로만 움직인다
        static Chapter2NormalEnemy AssertKeyCarrier(Chapter2World world)
        {
            var keys=world.Keys;
            Assert.That(keys.Chest,Is.Not.Null,"the locked chest stands in its room");
            var carrier=keys.Carrier as Chapter2NormalEnemy;
            Assert.That(carrier,Is.Not.Null,"a chapter 2 walker carries the key");
            Assert.That(carrier.ArtId=="K4"||carrier.ArtId=="M3",Is.True,carrier.ArtId);
            Assert.That(carrier.FollowsMovementOverride,Is.True);Assert.That(carrier.maxHealth,Is.EqualTo(ForestCombatV2.Chapter1KeyChest.CarrierHealth));
            string carrierId=world.Actors.First(p=>p.Value==carrier).Key;
            Assert.That(world.Layout.carrierRooms,Does.Contain(world.Layout.actors.First(a=>a.id==carrierId).roomId),"the carrier comes from a main combat room past the chest branch");
            Assert.That(keys.Route.Landings.Any(l=>l.Allowed&&Mathf.Abs(l.Y-carrier.spawn.y)<.1f&&carrier.spawn.x>=l.X0-.5f&&carrier.spawn.x<=l.X1+.5f),Is.True,"the route stands where the carrier spawned");
            return carrier;
        }
    }
}
