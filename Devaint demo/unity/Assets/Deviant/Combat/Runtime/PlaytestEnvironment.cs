using System;
using Deviant.Control.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Combat
{
    // Test tooling only. These values never write back to the combat catalog or release balance.
    public sealed class PlaytestEnvironment : MonoBehaviour
    {
        [Serializable] public sealed class BasicSpeedTrial
        {
            public string form;
            public float scale;
            public float lastHitExtraRecoverySeconds;
        }
        [Serializable] public sealed class Settings
        {
            public int monstersPerGroup,groupCount;
            public float groupSpacing,memberSpacing,firstGroupOffset;
            public BasicSpeedTrial[] basicSpeedTrials;
        }
        public Deviant.Combat.CombatArena standaloneArena;
        public Func<Settings,int> GroupSpawner;
        Deviant.Combat.CombatArena Arena => standaloneArena;
        public Settings Current { get; private set; }
        public int SpawnedCount { get; private set; }
        IControlScope scope;
        bool ready;
        string members,groups,spacing,message;
        public static Settings ApplyBasicSpeedTrials(CombatArena arena)
        {
            var source=Resources.Load<TextAsset>("PlaytestEnvironment");
            if(!source)throw new InvalidOperationException("Missing PlaytestEnvironment; run tools/combat/export_unity_catalog.py");
            var settings=JsonUtility.FromJson<Settings>(source.text);
            arena.ConfigureTestBasicSpeeds(settings.basicSpeedTrials);
            return settings;
        }
        public void Initialize()
        {
            if(!Arena || GroupSpawner==null)throw new InvalidOperationException("The test workbench needs an arena and a map-specific group spawner.");
            Current=ApplyBasicSpeedTrials(Arena);
            members=Current.monstersPerGroup.ToString();groups=Current.groupCount.ToString();
            spacing=Current.groupSpacing.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ready=true;
            Arena.ExternalTestPanel=true;
            Arena.showDebugUI=false;Arena.showPanel=false;
            Rebuild();
        }
        public void SetOpen(bool open)
        {
            if(!ready)return;
            var arena=Arena;arena.showPanel=open;
            if(open && scope==null)
            {
                scope=arena.Controls.OpenScope("test environment settings");
                scope.BlockInput(InputChannels.Combat);scope.PauseSimulation();
                arena.TestConfigurationOpen=true;
            }
            else if(!open)
            {
                arena.TestConfigurationOpen=false;scope?.Dispose();scope=null;
            }
        }
        void Update()
        {
            if(!ready)return;
            // The normal keyboard adapter is blocked by this scope while the panel is open.
            if(DevFeatures.Enabled && Keyboard.current!=null && Keyboard.current.f1Key.wasPressedThisFrame)SetOpen(!Arena.showPanel);
            if(Arena.showPanel!=(scope!=null))SetOpen(Arena.showPanel);
        }
        public bool ApplyGroups(string memberText,string groupText,string spacingText)
        {
            if(!ready || !int.TryParse(memberText,out var m)||m<1||m>8 || !int.TryParse(groupText,out var g)||g<1||g>4 ||
                !float.TryParse(spacingText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var s)||
                float.IsNaN(s)||float.IsInfinity(s)||s<20||s>40 || s<(m-1)*Current.memberSpacing+12)
            {message="무리당 1~8마리, 1~4무리, 중심 간격20~40칸. 무리 사이 빈 간격은12칸 이상으로 설정하세요.";return false;}
            Current.monstersPerGroup=m;Current.groupCount=g;Current.groupSpacing=s;
            members=memberText;groups=groupText;spacing=spacingText;
            Rebuild();return true;
        }
        void Rebuild()
        {
            SpawnedCount=GroupSpawner(Current);
            message="일반 테스트 적 "+SpawnedCount+"마리 배치 / 보스 제외";
        }
        void OnGUI()
        {
            if(!ready || !Arena.Player)return;
            var arena=Arena;if(arena.uiFont)GUI.skin.font=arena.uiFont;
            if(GUI.Button(new Rect(Screen.width-205,65,193,32),arena.showPanel?"테스트 설정 닫기 [F1]":"테스트 설정 [F1]"))SetOpen(!arena.showPanel);
            if(!arena.showPanel)return;
            float width=Mathf.Min(480,Screen.width*.55f);
            arena.DrawEquipmentPanel(new Rect(Screen.width-width-12,105,width,Screen.height-117));
            var rect=new Rect(12,105,Mathf.Min(375,Screen.width-width-36),300);
            GUI.Box(rect,GUIContent.none);
            GUILayout.BeginArea(rect,GUI.skin.box);
            GUILayout.Label("무리 배치 / 테스트용 초기값");
            members=Field("무리당 마릿수",members);
            groups=Field("무리 수",groups);
            spacing=Field("무리 중심 간격 (칸)",spacing);
            if(GUILayout.Button("무리 다시 배치"))ApplyGroups(members,groups,spacing);
            GUILayout.Label(message);
            GUILayout.Label("설정 중 전투, 시간 정지 / 닫으면 재개");
            GUILayout.Label("스펙은 오른쪽 슬롯별로 설정합니다.");
            GUILayout.Label(PlayerControls.Prompt(arena.Player,"{Weapon1Basic}/{Weapon2Basic} 기본1/2 / {Weapon1Special}/{Weapon2Special} 스킬1/2 / {DevelopmentReset} 리셋"));
            GUILayout.EndArea();
        }
        static string Field(string label,string value)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label);value=GUILayout.TextField(value,GUILayout.Width(70));GUILayout.EndHorizontal();return value;
        }
        void OnDisable(){if(Arena){Arena.TestConfigurationOpen=false;Arena.ExternalTestPanel=false;}scope?.Dispose();scope=null;}
    }
}
