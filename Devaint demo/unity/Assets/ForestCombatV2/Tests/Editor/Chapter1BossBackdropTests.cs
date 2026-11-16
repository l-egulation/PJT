using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1BossBackdropTests
    {
        [UnityTest] public IEnumerator ApprovedBackdropLoadsInTheActualStageWithoutAddingCollision()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed=31;
            yield return new EnterPlayMode();
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&(!world||!world.Ready);i++){yield return null;world=Object.FindAnyObjectByType<ForestCombatWorld>();}
            Assert.That(world&&world.Ready,Is.True);
            var backdrop=world.GetComponent<Chapter1BossBackdrop>();
            Assert.That(backdrop&&backdrop.Ready,Is.True);
            var layout=Chapter1BossBackdrop.ReadLayout();
            Assert.That(backdrop.LayerCount,Is.EqualTo(layout.room.Length+layout.approach.Length));
            Assert.That(world.Data,Is.Not.Null,"The running world must retain its generated map.");
            Assert.That(world.Data.rooms,Is.Not.Null);
            var room=world.Data.rooms.Single(r=>r!=null&&r.kind=="boss");
            // 보스 앞 긴 통로(48u 통로 블록 + 양쪽 출입구 벽)에 승인 길목 배치를 비례 축소 없이 그대로 쓴다
            // EnterPlayMode의 도메인 다시 읽기 뒤라 지역 변수를 잡는 람다 대신 반복문으로 찾는다
            ForestPassage hall=null;foreach(var p in world.Data.passages)if(p.to==room.id)hall=p;
            Assert.That(hall,Is.Not.Null);
            Assert.That(hall.w,Is.GreaterThanOrEqualTo(BlockMap.BlockGenerator.Hall*BlockMap.BlockSpec.BlockW));
            Assert.That(hall.w,Is.EqualTo(layout.approachWidth).Within(.01f));
            // Query just the boss room: other districts load their own scenery resources.
            var bossOnly=new ForestData {rooms=new[]{room}};
            Assert.That(Stage1VillageExperiment.VillageTeamScenery.Create(bossOnly).props,Is.Empty,
                "Village trees and fountains must not overlap the dedicated boss interior.");
            var art=backdrop.transform.Find("Clock room rear art");
            var approach=backdrop.transform.Find("Clock approach rear art");
            Assert.That(art.GetComponentsInChildren<Collider2D>(),Is.Empty);
            Assert.That(approach.GetComponentsInChildren<Collider2D>(),Is.Empty);
            foreach(var renderer in art.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(renderer.sprite.texture.width,Is.GreaterThan(2));
                Assert.That(renderer.sortingOrder,Is.LessThan(0));
            }
            var rear=art.Find("room-rear").GetComponent<SpriteRenderer>();
            float floor=-(room.y+room.entryY);
            Assert.That(rear.bounds.min.y,Is.EqualTo(floor).Within(.02f));
            Assert.That(rear.bounds.size.x,Is.EqualTo(room.w).Within(.03f));
            var gear=art.Find("aged-gear-large-source");
            backdrop.Animate(0);var initial=gear.rotation;
            backdrop.Animate(5);Assert.That(Quaternion.Angle(initial,gear.rotation),Is.GreaterThan(10));
            var player=world.arena.Player;player.UseTestInput=true;player.maxHealth=player.health=100000;
            player.Position=new Vector2(room.x-5,floor+1);
            for(int i=0;i<15;i++)yield return null;
            world.RefreshCamera(0,true);
            for(int i=0;i<4;i++)yield return null;
            Capture(world.View,"unity-approach.png");
            // 긴 통로의 입구·가운데도 남긴다(화면 검토용)
            foreach(var (dx,name) in new[]{(hall.w-8f,"unity-approach-entry.png"),(hall.w/2f,"unity-approach-middle.png")})
            {
                player.Position=new Vector2(room.x-dx,floor+1);
                for(int i=0;i<15;i++)yield return null;
                world.RefreshCamera(0,true);
                for(int i=0;i<4;i++)yield return null;
                Capture(world.View,name);
            }
            player.Position=new Vector2(room.x+5,floor+1);
            var encounter=world.GetComponent<ClockExecutionerEncounter>();
            encounter.BeginIntro();encounter.AdvanceIntro(encounter.IntroDuration+.01f);
            world.GetComponent<Chapter1BossRoomEnclosure>().Hold();world.RefreshCamera(0,true);
            // Let the entrance landing dust settle before capturing the finished arena.
            yield return new WaitForSeconds(2);
            using(var scope=world.arena.Controls.OpenScope("backdrop verification"))
            {
                scope.PauseSimulation();backdrop.Animate(3);
                world.RefreshCamera(0,true);Capture(world.View,"unity-boss-room.png");
            }
            yield return new ExitPlayMode();
        }
        static void Capture(Camera camera,string name)
        {
            string folder=Path.GetFullPath("../outputs/boss-map-integration");Directory.CreateDirectory(folder);
            var previous=camera.targetTexture;var active=RenderTexture.active;var viewport=camera.rect;
            var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;camera.rect=viewport;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
        }
    }
}
