using System;
using UnityEditor;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    [CustomEditor(typeof(Chapter1MonsterTestSettings))]
    public sealed class Chapter1MonsterTestSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var settings=(Chapter1MonsterTestSettings)target;
            EditorGUILayout.HelpBox("편집: Use Custom Placements를 켜고 Placements에 종과 발 위치를 지정하세요. Scene 핸들로 이동한 뒤 씬을 저장합니다.\n실행: F4로 종 선택·클릭 배치. 새 적의 AI는 F1에서 시작합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying || !settings.world || !settings.world.worldJson))
            {
                if(GUILayout.Button("기본 맵 배치를 목록으로 가져오기"))
                {
                    Undo.RecordObject(settings,"Copy test monster placements");
                    var data=JsonUtility.FromJson<ForestData>(settings.world.worldJson.text);settings.placements.Clear();
                    foreach(var item in data.actors)
                        foreach(Chapter1TestSpecies species in Enum.GetValues(typeof(Chapter1TestSpecies)))
                            if(data.monsters[item.kind].id==Chapter1MonsterTestSettings.SpeciesId(species))
                                settings.placements.Add(new Chapter1TestPlacement{species=species,feet=new Vector2(item.x/32,-item.y/32)});
                    settings.useCustomPlacements=true;EditorUtility.SetDirty(settings);
                }
                if(GUILayout.Button("선택 종을 배치 목록에 추가"))
                {
                    Undo.RecordObject(settings,"Add test monster placement");
                    var data=JsonUtility.FromJson<ForestData>(settings.world.worldJson.text);
                    settings.placements.Add(new Chapter1TestPlacement{species=settings.selectedSpecies,feet=new Vector2(data.spawn.x/32+6,-data.spawn.y/32)});
                    settings.useCustomPlacements=true;EditorUtility.SetDirty(settings);
                }
            }
        }
        void OnSceneGUI()
        {
            var settings=(Chapter1MonsterTestSettings)target;
            if(Application.isPlaying || !settings.useCustomPlacements)return;
            foreach(var placement in settings.placements)
            {
                var point=(Vector3)placement.feet;
                Handles.Label(point+Vector3.up*.2f,placement.species.ToString());
                EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(point,Quaternion.identity);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings,"Move test monster feet");placement.feet=new Vector2(moved.x,moved.y);
                    EditorUtility.SetDirty(settings);
                }
            }
        }
        [MenuItem("Tools/Deviant/Meadow Dusk/Select Monster Placement Settings")]
        public static void SelectSettings()
        {
            var settings=UnityEngine.Object.FindFirstObjectByType<Chapter1MonsterTestSettings>();
            if(!settings){MeadowDuskBuilder.Open();settings=UnityEngine.Object.FindFirstObjectByType<Chapter1MonsterTestSettings>();}
            if(settings)Selection.activeGameObject=settings.gameObject;
        }
    }
}
