using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    public enum Chapter1TestSpecies { FieldMouse, Mole, Boar, Scarecrow, HayBale, WaterBucket }
    [Serializable]
    public sealed class Chapter1TestPlacement
    {
        public Chapter1TestSpecies species;
        [Tooltip("발 위치, Unity 월드 좌표. Scene 뷰의 핸들로 이동할 수 있습니다.")]
        public Vector2 feet;
    }

    // Local test-scene configuration, not the production level or enemy balance source.
    public sealed class Chapter1MonsterTestSettings : MonoBehaviour
    {
        public ForestCombatWorld world;
        [Tooltip("켜면 기본 맵 배치 대신 아래 목록만 생성합니다. 비어 있으면 적 없이 시작합니다.")]
        public bool useCustomPlacements;
        public List<Chapter1TestPlacement> placements=new List<Chapter1TestPlacement>();
        public Chapter1TestSpecies selectedSpecies;
        public bool faceLeft=true;
        public bool PanelOpen { get; private set; }
        public bool ClickPlacement { get; private set; }
        public string Status { get; private set; }
        static readonly string[] ids={"01_field_mouse","02_mole","05_boar","07_scarecrow","08_hay_bale","10_water_bucket"};
        static readonly string[] names={"들쥐","두더지","멧돼지","허수아비","짚단","두레박"};
        public static string SpeciesId(Chapter1TestSpecies species)=>ids[(int)species];
        public MonsterPlacement[] BuildPlacements(ForestData data)
        {
            if(!useCustomPlacements)return data.actors;
            var result=new MonsterPlacement[placements.Count];
            for(int i=0;i<result.Length;i++)
            {
                int kind=Array.FindIndex(data.monsters,m=>m.id==SpeciesId(placements[i].species));
                if(kind<0)throw new InvalidOperationException("Selected monster is missing from this scene.");
                result[i]=new MonsterPlacement{kind=kind,x=placements[i].feet.x*32,y=-placements[i].feet.y*32};
            }
            return result;
        }
        public bool TryPlace(Chapter1TestSpecies species,Vector2 point,out FieldMouseEnemy actor)
            => TryPlace(SpeciesId(species),point,out actor);

        public bool TryPlace(string species,Vector2 point,out FieldMouseEnemy actor)
        {
            actor=null;
            if(!world || !world.Ready){Status="테스트 씬이 준비되지 않았습니다.";return false;}
            var kind=Array.Find(world.Data.monsters,m=>m.id==species);
            if(kind==null){Status="현재 씬에서 사용할 수 없는 몬스터입니다. 종류를 다시 선택하세요.";return false;}
            if(!float.IsFinite(point.x)||!float.IsFinite(point.y)){Status="유효한 배치 위치를 선택하세요.";return false;}
            Physics2D.SyncTransforms();
            RaycastHit2D floor=default;
            foreach(var hit in Physics2D.RaycastAll(point+Vector2.up*.25f,Vector2.down,24))
            {
                if(!hit.collider.GetComponent<ArenaSurface>() || hit.normal.y<.99f)continue;
                if(!floor.collider || hit.distance<floor.distance)floor=hit;
            }
            if(!floor.collider){Status="평평한 바닥 위를 선택하세요.";return false;}
            try { actor=world.SpawnMonster(species,new Vector2(point.x,floor.point.y+.003f),false,faceLeft?-1:1); }
            catch(ArgumentException){Status="현재 씬에서 이 몬스터의 배치 리소스를 사용할 수 없습니다.";return false;}
            catch(InvalidOperationException e){Status="몬스터를 배치할 수 없습니다: "+e.Message;return false;}
            if(!actor.FullSupport(actor.Position,actor.Position))
            {
                world.RemoveMonster(actor);actor=null;
                Status="몸 전체를 지지할 수 있는 평평한 바닥이 필요합니다.";return false;
            }
            Status=kind.name+" 배치 완료 / 몬스터 메뉴의 ‘적 AI 시작’으로 활성화";return true;
        }
        void Update()
        {
            if(DebugGameMenu.Installed||!DevFeatures.Enabled||world&&world.FullMap&&world.FullMap.IsOpen)return;
            var keyboard=Keyboard.current;
            if(keyboard!=null && keyboard.f4Key.wasPressedThisFrame){PanelOpen=!PanelOpen;if(!PanelOpen)ClickPlacement=false;}
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)ClickPlacement=false;
            if(!PanelOpen || !ClickPlacement || Mouse.current==null || !Mouse.current.leftButton.wasPressedThisFrame || !world || !world.Ready)return;
            Vector2 screen=Mouse.current.position.ReadValue();
            if(new Rect(10,78,370,254).Contains(new Vector2(screen.x,Screen.height-screen.y)))return;
            if (Deviant.Presentation.Unity.CameraDisplayProjection.TryScreenToWorldPoint(world.View,screen,out var point))
                TryPlace(selectedSpecies,point,out _);
        }
        void OnDisable(){ClickPlacement=false;PanelOpen=false;}
        void OnGUI()
        {
            if(DebugGameMenu.Installed||!DevFeatures.Enabled || !world || !world.Ready || world.FullMap&&world.FullMap.IsOpen)return;
            GUI.depth=-30;if(world.diagnostics.uiFont)GUI.skin.font=world.diagnostics.uiFont;
            if(!world.diagnostics.CompactHud&&GUI.Button(new Rect(Screen.width-164,70,154,28),PanelOpen?"몬스터 배치 닫기 [F4]":"몬스터 배치 [F4]"))
            {PanelOpen=!PanelOpen;if(!PanelOpen)ClickPlacement=false;}
            if(!PanelOpen)return;
            GUILayout.BeginArea(new Rect(10,242,370,254),GUI.skin.box);
            GUILayout.Label("테스트 몬스터 선택 / 배치");
            selectedSpecies=(Chapter1TestSpecies)GUILayout.SelectionGrid((int)selectedSpecies,names,3);
            faceLeft=GUILayout.Toggle(faceLeft,"왼쪽 바라보기");
            if(GUILayout.Button(ClickPlacement?"배치 모드 끄기":"클릭 배치 시작 / 바닥 위를 클릭"))ClickPlacement=!ClickPlacement;
            if(GUILayout.Button("선택한 몬스터만 / 플레이어 앞에 배치"))
            {
                var point=world.arena.Player.Position+Vector2.right*6;
                // Validate placement before replacing the existing test population.
                if(TryPlace(selectedSpecies,point,out var added))
                {
                    var existing=world.arena.enemies.ToArray();
                    foreach(var enemy in existing)if(enemy!=added)world.RemoveMonster(enemy);
                }
            }
            if(GUILayout.Button("현재 테스트 몬스터 모두 지우기"))
            {foreach(var enemy in world.arena.enemies.ToArray())world.RemoveMonster(enemy);Status="테스트 몬스터를 지웠습니다.";}
            GUILayout.Label(Status??"새 몬스터는 AI 정지 상태 / R로 현재 배치 리셋");
            GUILayout.EndArea();
        }
    }
}
