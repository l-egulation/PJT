using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Scene-local owner: all pauses and input blocks are released on disable or scene replacement.
    [DefaultExecutionOrder(-10000)]
    public sealed partial class DebugGameMenu : MonoBehaviour
    {
        public static bool Installed { get; private set; }
        public static bool IsOpen { get; private set; }
        ForestCombatWorld world;
        CombatArena arena;
        RunEconomy otherEconomy;
        RunStash otherStash;
        public static void InstallFor(CombatArena target, RunEconomy economy, RunStash stash = null) { if(!DevFeatures.Enabled || target.GetComponent<DebugGameMenu>())return;var menu=target.gameObject.AddComponent<DebugGameMenu>();menu.arena=target;menu.otherEconomy=economy;menu.otherStash=stash; }
        IControlScope scope;
        GameObject pointerBlocker;
        Deviant.Input.Unity.LocalInput captured;
        int category=-1, selected, slot, form, skill, inkSlot, paletteSlot, partLevel, trial, species;
        int audioWeapon;
        long goldAmount, pigmentAmount; int step=100;
        string status="", confirmLabel;
        Picker picker, renderedPicker;
        sealed class Picker { public string Title; public string[] Options; public int Current, ReturnIndex; public Action<int> Apply; }
        Action confirmation;
        Vector2 scroll, cursor;
        bool placing;
        int placementStartedFrame;
        Vector2 lastPlacementMouse;
        public Vector2 MonsterCursor => cursor;
        float repeatAt;
        bool revealSelection;
        int renderedCategory = int.MinValue;
        Action renderedConfirmation;
        const float PanelFraction = .70710678f; // Width × height = half of the screen area.
        readonly Dictionary<string,int> desiredTiers = new Dictionary<string,int>();
        readonly List<TestEnemy> spawned = new List<TestEnemy>();
        readonly List<Row> rows=new List<Row>();
        static readonly string[] Categories={"이동, 진행","플레이어, 재화, 전투","장비, 잉크","로그라이트 해금","몬스터","표시, 진단","전용 시험장","무기별 음량","스킬별 음량"};
        string currentGroup = "";
        sealed class Row
        {
            public string Label, Caption, Reason, Group;
            public float Share;
            public Action Run;
            public Action<int> Change;
            public string[] Tabs; public int TabIndex; public Action<int> SelectTab;
            public bool Selectable => Reason == null && (Run != null || Change != null || Tabs != null);
            public bool Information => Run == null && Change == null && Tabs == null;
        }
        public string SelectedLabel => selected >= 0 && selected < rows.Count ? rows[selected].Label : null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded-=Loaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded+=Loaded;
            Loaded(default,default);
        }
        static void Loaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if(!DevFeatures.Enabled)return;
            var w=FindFirstObjectByType<ForestCombatWorld>();
            if(w&&!w.GetComponent<DebugGameMenu>())w.gameObject.AddComponent<DebugGameMenu>();
        }
        void Awake(){world=GetComponent<ForestCombatWorld>();Installed=true;}
        public bool Open()
        {
            if(IsOpen)return true;
            if(world)arena=world.arena;
            if(!DevFeatures.Enabled||!arena||!arena.Player||world&&!world.Ready)return false;
             if(!pointerBlocker)
            {
                pointerBlocker=new GameObject("Debug pointer capture",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
                pointerBlocker.transform.SetParent(transform,false);
                var canvas=pointerBlocker.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;
                var shield=new GameObject("Shield",typeof(RectTransform),typeof(UnityEngine.UI.Image));shield.transform.SetParent(pointerBlocker.transform,false);
                var rect=shield.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                shield.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
            }
            pointerBlocker.SetActive(true);
            captured=PlayerControls.For(arena.Player); if(captured)captured.MenuCaptured=true; Acquire(); IsOpen=true; category=-1;selected=0;status="";scroll=Vector2.zero;renderedCategory=int.MinValue;return true;
        }
        void Acquire(){scope=arena.Controls.OpenScope("debug menu");scope.PauseSimulation();scope.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation|InputChannels.Dialogue);arena.Player.ClearInputState();}
        public void Close(){if(pointerBlocker)pointerBlocker.SetActive(false);scope?.Dispose();scope=null;if(captured)captured.MenuCaptured=false;captured=null;IsOpen=false;placing=false;confirmation=null;picker=null;arena?.Player?.ClearInputState();}
        void OnDisable(){Close();Installed=false;}
        void Add(string label,Action run=null,Action<int> change=null,string reason=null,float share=0,string caption=null)
            =>rows.Add(new Row{Label=label,Caption=caption,Run=run,Change=change,Reason=reason,Group=currentGroup,Share=share>0?share:run != null ? .5f : 1});
        void Tabs(string label, string[] choices, int index, Action<int> select, string caption=null, float share=1)
            => rows.Add(new Row { Label=label, Caption=caption, Group=currentGroup, Tabs=choices, TabIndex=index, SelectTab=select, Share=share });
        void Choice(string label, string[] options, int index, Action<int> apply, string caption=null, float share=0)
        {
            Add(label+": "+(options.Length>0&&index>=0?options[Mathf.Clamp(index,0,options.Length-1)]:"선택"),
                options.Length==0 ? (Action)null : ()=>{picker=new Picker{Title=label,Options=options,Current=index,Apply=apply,ReturnIndex=selected};selected=Mathf.Clamp(index,0,options.Length-1)+1;scroll=Vector2.zero;},
                share:share,caption:caption==null?null:caption+": "+(index>=0&&index<options.Length?options[index]:"선택"));
        }
        WeaponGrowth Growth => Binding ? Binding.Run?.Growth : arena ? arena.MvpGrowth : null;
        static string ColorName(string color) => color switch { "red"=>"빨강", "yellow"=>"노랑", "green"=>"초록", "blue"=>"파랑", "purple"=>"보라", "colorless"=>"무색", "neutral"=>"무색", _=>color };
        string[] SlotColors(int index) => Growth.Data.colorWeights.Select(w=>w.color).Concat(index==0?new[]{"colorless"}:Array.Empty<string>()).Distinct().ToArray();
        public void SelectWeapon(int index)
        {
            if(!arena||index<0||index>=arena.Player.weapons.Count)return;
            slot=index;form=arena.Player.weapons[slot].FormIndex;skill=arena.Player.weapons[slot].SpecialIndex;
            SelectPaletteSlot(0);SelectInkSlot(0);status="";
        }
        public void SelectPaletteSlot(int index)
        {
            if (RunSketch != null)
            {
                if (index >= 0 && index < (RunSketch.Equipped(slot)?.Slots.Count ?? 0)) paletteSlot = index;
                return;
            }
            var palette=Growth?.Equipped(arena.Player.weapons[slot].InstanceId);
            if(palette==null||index<0||index>=palette.Slots.Count)return;
            paletteSlot=index;
        }
        public void SelectInkSlot(int index)
        {
            if (RunSketch != null)
            {
                if (index >= 0 && index < (RunSketch.Equipped(slot)?.Slots.Count ?? 0)) inkSlot = index;
                return;
            }
            var palette=Growth?.Equipped(arena.Player.weapons[slot].InstanceId);
            if(palette==null||index<0||index>=palette.Slots.Count)return;
            inkSlot=index;partLevel=palette.Slots[index].Level;
        }
        public bool TryApplyPaletteColor(string color, out string error)
        {
            if (RunSketch != null) return EditSketchWeapon((equipment, weapon) => equipment.DebugSetSlotColor(weapon.Id, paletteSlot, color), out error);
            error=null;
            if(!IsOpen||scope==null||Growth==null){error="치트 모드에서 팔레트를 선택하세요.";return false;}
            try{Growth.DebugSetSlotColor(arena.Player.weapons[slot].InstanceId,paletteSlot,color);return true;}
            catch(InvalidOperationException e){error=e.Message;return false;}
        }
        public bool TryApplyInkPart(string id, int level, out string error)
        {
            if (RunSketch != null)
            {
                if (level != 0) { error = "런 잉크는 팔레트 색상이 일치할 때 강화 효과를 사용합니다."; return false; }
                return TryApplyRunInk(id, out error);
            }
            error=null;
            if(!IsOpen||scope==null||Growth==null){error="치트 모드에서 잉크 칸을 선택하세요.";return false;}
            try{Growth.DebugSetPart(arena.Player.weapons[slot].InstanceId,inkSlot,id,level);partLevel=level;return true;}
            catch(InvalidOperationException e){error=e.Message;return false;}
        }
        public bool TryResizePalette(int count, out string error)
        {
            if (RunSketch != null) return ResizeSketchPalette(count, out error);
            error=null;
            if(!IsOpen||scope==null||Growth==null){error="치트 모드에서 팔레트를 선택하세요.";return false;}
            try{Growth.DebugResizePalette(arena.Player.weapons[slot].InstanceId,count);paletteSlot=Mathf.Clamp(paletteSlot,0,count-1);inkSlot=Mathf.Clamp(inkSlot,0,count-1);return true;}
            catch(InvalidOperationException e){error=e.Message;return false;}
        }
        void ChangeEquipment(int targetForm,int targetSkill)
        { status=TryApplyEquipment(slot,targetForm,targetSkill,out var error)?"장비 변경 완료":error; }
        void ChangeInkPart(string id)
        { status=TryApplyInkPart(id,partLevel,out var error)?"파츠 변경 완료":error; }
        void ChangeInkLevel(int level)
        {
            var palette=Growth.Equipped(arena.Player.weapons[slot].InstanceId);
            status=TryApplyInkPart(palette.Slots[inkSlot].Part?.id,level,out var error)?"강화 +"+level+" 변경 완료":error;
        }
        void Group(string title) => currentGroup = title;
        void EnsureSelection()
        {
            if (selected < 0 || selected >= rows.Count || !rows[selected].Selectable)
                selected = rows.FindIndex(row => row.Selectable);
        }
        public void ChangeSelectedValue(int direction)
        {
            Build();EnsureSelection();if(selected<0||direction==0)return;
            var row=rows[selected];int delta=direction>0?1:-1;
            if(row.Tabs!=null)row.SelectTab(Mathf.Clamp(row.TabIndex+delta,0,row.Tabs.Length-1));
            else row.Change?.Invoke(delta);
        }
        public void MoveSelection(int direction)
        {
            if (rows.Count == 0) return;
            int start = selected;
            for (int i = 0; i < rows.Count; i++)
            {
                selected = (selected + (direction < 0 ? -1 : 1) + rows.Count) % rows.Count;
                if (rows[selected].Selectable) { revealSelection = true; return; }
            }
            selected = start;
        }
        public void NavigateSelection(Vector2 direction)
        {
            Build();EnsureSelection();if(selected<0)return;
            var lines=PackLines();
            int current=lines.FindIndex(line=>line.Contains(selected));
            if(current<0)return;
            bool horizontal=Mathf.Abs(direction.x)>Mathf.Abs(direction.y);
            if(horizontal&&(rows[selected].Change!=null||rows[selected].Tabs!=null))
            { ChangeSelectedValue(direction.x>0?1:-1);return; }
            int delta=(horizontal?direction.x:direction.y)>0?1:-1;
            if(!horizontal)delta=-delta;
            if(horizontal)
            {
                var peers=lines[current];int start=peers.IndexOf(selected);
                for(int i=start+delta;i>=0&&i<peers.Count;i+=delta)
                    if(rows[peers[i]].Selectable){selected=peers[i];revealSelection=true;return;}
            }
            else if(category<0||picker!=null)
            {
                float x=LineCenter(lines[current],selected);
                for(int step=1;step<=lines.Count;step++)
                {
                    var line=lines[(current+delta*step+lines.Count*step)%lines.Count];
                    int candidate=-1;float distance=float.MaxValue;
                    foreach(int index in line)
                    {
                        float next=Mathf.Abs(LineCenter(line,index)-x);
                        if(rows[index].Selectable&&next<distance){distance=next;candidate=index;}
                    }
                    if(candidate>=0){selected=candidate;revealSelection=true;return;}
                }
            }
            else MoveSelection(delta);
        }

        public void SelectCategory(int value)
        {
            if (!IsOpen || value < 0 || value >= Categories.Length) return;
            category = value; selected = -1; status = "";
            if (value == 2 && arena.Player.weapons.Count > slot)
            { SelectWeapon(slot); }
            if (value == 1)
            { goldAmount = (Binding ? Binding.Run?.Economy : otherEconomy)?.Gold ?? 0; pigmentAmount = Chapter1ProfileHost.Service.Snapshot.Pigment; }
            Build(); EnsureSelection(); revealSelection = true;
        }
        public void Back()
        {
            if (!IsOpen) return;
            if (placing) placing = false;
            else if (picker != null) { selected=picker.ReturnIndex;picker=null; }
            else if (confirmation != null) confirmation = null;
            else if (category >= 0) { category = -1; selected = 0; status = ""; }
            else Close();
            scroll = Vector2.zero; revealSelection = true;
        }
        Chapter1RunBinding Binding=>world?world.GetComponent<Chapter1RunBinding>():null;
        void Ask(string label,Action action){confirmLabel=label;confirmation=action;selected=0;}
        void Perform(Action action){try{action();}catch(Exception e){status=e.Message;}}
        void Travel(Func<bool> action)
        {
            // Release only our scope: another owner's pause or input block still refuses travel.
            scope?.Dispose();scope=null;
            try{status=action()?"이동 완료":"현재 상태에서는 이동할 수 없습니다.";}
            finally{if(this&&IsOpen)Acquire();}
        }
        public string Status => status;
        string StageTravelUnavailableReason => !DevFeatures.Enabled ? "치트 모드가 꺼져 있습니다."
            : ChapterSceneFade.Busy ? "씬 전환 중입니다."
            : !arena || !arena.Player ? "플레이어가 준비되지 않았습니다."
            : arena.Player.Dead ? "사망 상태에서는 이동할 수 없습니다."
            : arena.Player.CheckpointBusy ? "포탈 동작 중에는 이동할 수 없습니다." : null;

        void RequestStageTravel(int chapter)
        {
            string reason = StageTravelUnavailableReason;
            if (reason != null) { status = reason; return; }
            string destination = chapter == 0 ? "마을" : chapter + "챕터";
            Ask(destination + "로 이동 / 현재 런의 진행, 장비, 체력은 이어지지 않습니다. 클리어 기록은 추가하지 않습니다.", () =>
            {
                reason = StageTravelUnavailableReason;
                if (reason != null) { status = reason; return; }
                if (chapter == 2)
                {
                    Close();
                    if (Chapter1RunHost.EnterChapterTwoForDebug == null || !Chapter1RunHost.EnterChapterTwoForDebug())
                    { Open(); SelectCategory(0); status = "현재 상태에서는 2챕터로 이동할 수 없습니다."; }
                }
                else
                {
                    Close();
                    var host = Chapter1RunHost.Ensure();
                    if (chapter == 0) host.GoToVillage(false);
                    else host.StartNewRun();
                }
            });
        }

        public bool TryTravelToBoss()
        {
            if(!IsOpen||scope==null||!scope.IsActive){status="치트 모드를 연 상태에서 이동하세요.";return false;}
            bool moved=Chapter1DebugTravel.TryToBoss(world,out var error,consoleCommand:true);
            status=moved?"보스방 이동 완료 / 기존 정지 상태는 유지됩니다.":error;
            return moved;
        }
        public bool ExecuteSelected()
        {
            Build(); EnsureSelection();
            if (selected < 0 || rows[selected].Run == null) return false;
            Perform(rows[selected].Run);
            return true;
        }

        void ResetGame()
        {
            if (!DevFeatures.Enabled || !IsOpen || scope == null || !scope.IsActive) return;
            var profile = Chapter1ProfileHost.Service;
            if (!profile.ResetProfile())
            {
                status = "게임 초기화 실패: " + profile.LastSaveError;
                return;
            }
            var session = Chapter1RunHost.Ensure();
            session.DiscardCurrentRun();
            Close();
            session.GoToTitle();
        }

        public bool TryTravelToSkySummit()
        {
            var village = world ? world.GetComponent<StartingVillageWorld>() : null;
            bool moved = false;
            Travel(() => { moved = DevFeatures.Enabled && village && village.Ready && village.WarpToSkySummit(); return moved; });
            return moved;
        }

        public bool TryApplyEquipment(int targetSlot, int targetForm, int targetSkill, out string error)
        {
            error = null;
            if (!DevFeatures.Enabled || !IsOpen || scope == null || !arena || !arena.Player)
            { error = "치트 모드를 연 상태에서 적용하세요."; return false; }
            var player = arena.Player;
            var forms = arena.catalog.forms;
            if (targetSlot < 0 || targetSlot >= player.weapons.Count || targetForm < 0 || targetForm >= forms.Length ||
                targetSkill < 0 || targetSkill > 3 || targetSkill >= forms[targetForm].specials.Length)
            { error = "현재 카탈로그에 없는 형태, 스킬 조합입니다."; return false; }
            if (player.Dead || player.Dashing || player.CheckpointBusy)
            { error = "사망, 대시, 포탈 중에는 장비를 변경할 수 없습니다."; return false; }
            if (RunSketch != null) return ApplySketchEquipment(targetSlot, targetForm, targetSkill, out error);
            string id = forms[targetForm].id, kind = Chapter1Rewards.BlueprintKindOf(id);
            string basic = kind == "sword" ? "base_sword" : kind == "blunt" ? "hammer" : "launcher";
            var weaponStamp = id == basic ? null : RunStamp.Weapon(kind, id);
            var skillStamp = targetSkill == 0 ? null : RunStamp.SkillStamp(targetSkill);
            bool old = arena.TestConfigurationOpen;
            arena.TestConfigurationOpen = true;
            try
            {
                // The menu pauses an in-flight attack. End it explicitly so the requested debug loadout can take over.
                player.CancelAction(); player.ClearInputState();
                if (!player.TryReconfigure(targetSlot, targetForm, targetSkill, out error)) return false;
                player.weapons[targetSlot].ExperimentalChargeStamp = false;
                player.SetWeaponSlotReady(targetSlot, true);
                var stash = Binding ? Binding.Run?.Stash : otherStash;
                stash?.DebugConfigureStamps(targetSlot, weaponStamp, skillStamp);
                slot = targetSlot; form = targetForm; skill = targetSkill;
                return true;
            }
            finally { arena.TestConfigurationOpen = old; }
        }

        void Build()
        {
            rows.Clear(); currentGroup = "";
            if(picker!=null)
            {
                var current=picker;Group(current.Title);Add("항목을 선택하면 상위 설정으로 돌아갑니다.");
                for(int i=0;i<current.Options.Length;i++){int index=i;Add((i==current.Current?"● ":"")+current.Options[i],()=>{current.Apply(index);selected=current.ReturnIndex;picker=null;scroll=Vector2.zero;});}
                return;
            }
            if(confirmation!=null){Group("변경 확인");Add(confirmLabel);Add("확인하고 적용",()=>{var a=confirmation;confirmation=null;Perform(a);});Add("취소",()=>Back());return;}
            if(category<0){for(int i=0;i<Categories.Length;i++){int c=i;Add(Categories[i],()=>SelectCategory(c));}return;}
            var player=arena.Player;var binding=Binding;var growth=binding?binding.Run?.Growth:arena.MvpGrowth;var economy=binding?binding.Run?.Economy:otherEconomy;
            switch(category)
            {
                case 0:
                    Group("스테이지 이동");
                    Add("마을로 이동", () => RequestStageTravel(0), reason: StageTravelUnavailableReason);
                    Add("1챕터로 이동 / 새 테스트 런", () => RequestStageTravel(1), reason: StageTravelUnavailableReason);
                    Add("2챕터로 이동 / 새 테스트 런", () => RequestStageTravel(2), reason: StageTravelUnavailableReason ?? (Chapter1RunHost.EnterChapterTwoForDebug == null ? "2챕터 진입 기능이 준비되지 않았습니다." : null));
                    if(!world){Add("이 씬의 구간 이동은 지원하지 않습니다",reason:"1챕터 이동 전용");break;}
                    Group("마을 / 하늘길");
                    var village=world.GetComponent<StartingVillageWorld>();
                    Add("하늘길(점프맵) 정상으로 이동 / 연습",()=>TryTravelToSkySummit(),reason:village&&village.Ready?null:"마을에서만 사용합니다");
                    Add("도착 연출은 메뉴를 닫으면 이어집니다. 연습 기록으로 남으며 최고 기록에서는 제외됩니다.");
                    Group("보스, 기믹 이동");
                    int bossIndex=Chapter1DebugTravel.BossIndex(world);
                    Add("현재 런 보스방으로 이동",()=>TryTravelToBoss(),reason:world.proceduralRooms&&!world.dummyTrainingOnly&&bossIndex>=0?null:"1챕터 랜덤 스테이지 전용");
                    var gimmick=world.GetComponent<Chapter1GimmickDevKeys>();
                    Add("봉인 석판",()=>Travel(()=>gimmick&&gimmick.JumpToSeal()),reason:gimmick?null:"현재 씬에서 지원하지 않습니다");
                    Add("잠긴 상자",()=>Travel(()=>gimmick&&gimmick.JumpToChest()),reason:gimmick?null:"현재 씬에서 지원하지 않습니다");
                    Add("열쇠 보유 적",()=>Travel(()=>gimmick&&gimmick.JumpToCarrier()),reason:gimmick?null:"현재 씬에서 지원하지 않습니다");
                    Group("구간 이동");
                    for(int i=0;i<(world.Data?.views?.Length??0);i++){int n=i;Add("구간 / "+world.Data.views[i].name,()=>Travel(()=>{if(player.Dead||player.CheckpointBusy||arena.Controls.Current.SimulationPaused)return false;world.Warp(n);return true;}));}
                    Group("런 재시작");
                    Add("새 배치로 시작",()=>Ask("현재 런을 끝내고 새 배치 시작",()=>{Close();Chapter1RunHost.Ensure().StartNewRun();}));break;
                case 1:
                    Group("전투, 수량 입력");
                    Add("전투 상태 초기화",()=>{arena.ResetTest();status="초기화 완료";},share:.3f);
                    Add("한 번에 변경할 수량: "+step,null,d=>step=d>0?Math.Min(1000000,step*10):Math.Max(1,step/10),share:.7f);
                    Group("보상 / 현재 런");
                    Add("받아 둔 보상: " + (RunSketch?.Banked.ToString() ?? "없음"));
                    Add("보상 +1", () => TryAddReward(), reason: RewardGrantUnavailableReason);
                    Group("골드 / 현재 런");
                    Add("현재 보유: "+(economy?.Gold.ToString()??"없음"),share:.24f);
                    Add("목표 수량: "+goldAmount,null,d=>goldAmount=d>0?Math.Min(int.MaxValue,goldAmount+step):Math.Max(0,goldAmount-step),share:.51f);
                    Add("골드 적용",()=>{economy.DebugSetGold((int)goldAmount);status="골드 적용 완료";},reason:economy==null?"런 재화 없음":null,share:.25f);
                    var service=Chapter1ProfileHost.Service;
                    Group(service.Catalog.Data.currency.displayName+" / 영구 저장");
                    Add("현재 보유: "+service.Snapshot.Pigment,share:.24f);
                    Add("목표 수량: "+pigmentAmount,null,d=>pigmentAmount=d>0?Math.Min(int.MaxValue,pigmentAmount+step):Math.Max(0,pigmentAmount-step),share:.51f);
                    Add("재화 저장",()=>{long target=pigmentAmount;Ask("재화를 "+target+"로 저장",()=>status=service.DebugSetPigment(target)?"저장 완료":service.LastSaveError);},share:.25f);
                    Group("게임 초기화 / 모든 플레이 데이터");
                    Add("게임 초기화",()=>Ask("모든 진행, 재화, 해금, 스토리, 기록과 현재 런을 초기화하고 타이틀로 돌아갑니다. 음량, 단축키 설정은 유지합니다.",ResetGame));
                    break;
                case 2:
                    if (growth?.Sketch != null) { BuildSketchEquipmentRows(); break; }
                    Group("무기, 스탬프 / 선택 즉시 반영");
                    Tabs("편집할 무기",Enumerable.Range(0,player.weapons.Count).Select(i=>"무기 "+(i+1)).ToArray(),slot,SelectWeapon,caption:"무기");
                    if(player.weapons.Count==0){Add("도면을 먼저 선택하세요.");break;}
                    form=player.weapons[slot].FormIndex;var f=arena.catalog.forms[form];skill=player.weapons[slot].SpecialIndex;
                    Choice("무기 형태",arena.catalog.forms.Select(x=>x.name).ToArray(),form,index=>ChangeEquipment(index,Mathf.Clamp(player.weapons[slot].SpecialIndex,0,arena.catalog.forms[index].specials.Length-1)));
                    Choice("스킬",f.specials.Select(x=>x.name).ToArray(),skill,index=>ChangeEquipment(player.weapons[slot].FormIndex,index));
                    if(growth==null){Add("이 런에 잉크 성장 데이터가 없습니다.");break;}
                    var weapon=player.weapons[slot].InstanceId;var palette=growth.Equipped(weapon);
                    Group("팔레트 / 칸별 색상");
                    if(palette!=null)
                    {
                        paletteSlot=Mathf.Clamp(paletteSlot,0,palette.Slots.Count-1);inkSlot=Mathf.Clamp(inkSlot,0,palette.Slots.Count-1);
                        var slotLabels=palette.Slots.Select((cell,index)=>(index+1)+"번 / "+ColorName(cell.Color)).ToArray();
                        Tabs("편집할 팔레트 칸",slotLabels,paletteSlot,SelectPaletteSlot,caption:"편집할 칸");
                        Tabs("칸 수 (즉시 변경)",new[]{"3칸","4칸","5칸"},palette.Slots.Count-3,index=>status=TryResizePalette(index+3,out var error)?"팔레트 칸 수 변경 완료":error,caption:"칸 수",share:.5f);
                        var available=SlotColors(paletteSlot);
                        Choice("색상 (선택 즉시 적용)",available.Select(ColorName).ToArray(),Array.IndexOf(available,palette.Slots[paletteSlot].Color),index=>status=TryApplyPaletteColor(available[index],out var error)?"선택한 칸 색상 변경 완료":error,caption:"색상");
                        Group("잉크 파츠");
                        Tabs("편집할 잉크 칸",slotLabels,inkSlot,SelectInkSlot,caption:"편집할 칸");
                        var candidates=growth.Data.parts.Where(x=>palette.Slots[inkSlot].Color=="colorless"||x.color==palette.Slots[inkSlot].Color).ToArray();
                        int candidate=Array.IndexOf(candidates,palette.Slots[inkSlot].Part);
                        partLevel=palette.Slots[inkSlot].Level;
                        Choice("장착할 파츠",candidates.Select(x=>x.name).ToArray(),candidate,index=>ChangeInkPart(candidates[index].id));
                        Add("선택 칸 비우기",()=>{growth.DebugSetPart(weapon,inkSlot,null);status="파츠 해제 완료";});
                        Tabs("강화 단계 (즉시 변경)",Enumerable.Range(0,6).Select(level=>"+"+level).ToArray(),partLevel,ChangeInkLevel,caption:"강화 단계");
                        var preview=palette.Slots[inkSlot].Part;
                        if(preview!=null)
                        {
                            Add("효과 설명: "+preview.description);
                            var stats=preview.StatsAt(partLevel);if(!string.IsNullOrEmpty(stats))Add("강화 +"+partLevel+" 효과: "+stats);
                        }
                    }
                    else Tabs("칸 수 (즉시 변경)",new[]{"3칸","4칸","5칸"},0,index=>status=TryResizePalette(index+3,out var error)?"팔레트 생성 완료":error,caption:"칸 수");
                    Group("시험용 잉크 세트");
                    var trialTool=world?world.GetComponent<Chapter1InkFxTrialKeys>():null;
                    if(trialTool){var trials=Chapter1InkFxTrialKeys.SetNames;Choice("시험 세트 (즉시 변경)",trials,trial,index=>{trial=index;trialTool.Apply(index);});}
                    else Add("시험 세트는 1챕터에서 사용합니다.");
                    break;
                case 3:
                    var profile=Chapter1ProfileHost.Service;
                    Add("영구 저장 / 조건을 무시하고 임의 설정 / 미충족 조건은 적용 결과에 표시 / 런 보너스는 다음 출발에 반영");
                    foreach(var building in profile.Catalog.Buildings)
                    {

                        foreach(var u in profile.Catalog.Unlocks.Where(u=>u.building==building.id&&profile.Catalog.FirstTier(u)==u))
                        {
                            Group(building.name+" / "+u.name);
                            var first=u; var line=new List<ProgressionUnlock>();
                            for(var next=u;next!=null;next=profile.Catalog.NextTier(next))line.Add(next);
                            int current=line.Count(x=>profile.Snapshot.Has(x.id));
                            if(!desiredTiers.ContainsKey(u.id))desiredTiers[u.id]=current;
                            int target=desiredTiers[u.id];
                            Add("강화 단계 / 현재 "+current+" / 목표 "+target+" (0=미해금, 최대 "+line.Count+")",null,d=>desiredTiers[first.id]=Mathf.Clamp(desiredTiers[first.id]+d,0,line.Count),share:.7f,caption:"현재 "+current+" → 목표 "+target+" / "+line.Count);
                            var effect=target>0?line[target-1].effect:first.effect;
                            bool conflict=growth!=null&&line.Skip(target).Any(x=>profile.Snapshot.Has(x.id)&&x.effect.part!=null&&player.weapons.Any(w=>growth.SnapshotSlots(w.InstanceId).Any(s=>s.Part?.id==x.effect.part)));
                            Add("해금, 강화 단계 저장",()=>Ask(first.name+" → "+target+"단계 저장",()=>status=profile.DebugSetTier(first.id,target,force:true)?profile.LastDebugNotice:profile.LastSaveError),reason:conflict?"현재 런에 장착된 파츠의 해금은 취소할 수 없습니다":null,share:.3f,caption:"단계 저장");
                            Add(target == 0 ? "적용 효과: 미해금" : "적용 효과: "+DescribeEffect(effect,growth));
                        }
                    }
                    break;
                case 4:
                    if(!world){Add("이 씬의 몬스터 배치는 지원하지 않습니다",reason:"1챕터 시험 몬스터 전용");break;}
                    var settings=world.GetComponent<Chapter1MonsterTestSettings>();if(!settings){settings=world.gameObject.AddComponent<Chapter1MonsterTestSettings>();settings.world=world;}
                    Group("시험 몬스터 배치");
                    var monsters=world.Data.monsters;
                    species=Mathf.Clamp(species,0,Mathf.Max(0,monsters.Length-1));
                    Choice("몬스터 종류",monsters.Select(m=>m.name).ToArray(),species,index=>species=index);
                    Add("배치 위치 선택 (마우스, 키보드, 패드)",()=>BeginMonsterPlacement(),reason:monsters.Length==0?"현재 씬에 배치 가능한 몬스터가 없습니다":null);
                    Group("적 AI");
                    Add("적 AI 시작",()=>{foreach(var e in arena.enemies)if(e)e.aggressive=true;});Add("적 AI 정지",()=>{foreach(var e in arena.enemies)if(e)e.aggressive=false;});
                    Group("시험 몬스터 제거");
                    Add("현재 테스트 몬스터 제거",()=>Ask("현재 테스트 몬스터 제거",()=>{foreach(var e in spawned.ToArray())if(e)world.RemoveMonster(e);spawned.Clear();}));break;
                case 5:
                    Group("화면 표시");
                    Add("적 체력바: "+DevFeatures.ShowEnemyHealthBars,()=>DevFeatures.ShowEnemyHealthBars=!DevFeatures.ShowEnemyHealthBars);
                    Add("판정 표시: "+arena.showBoxes,()=>arena.showBoxes=!arena.showBoxes);
                    Add("진단 HUD: "+arena.showDebugUI,()=>{arena.showDebugUI=!arena.showDebugUI;if(world&&world.diagnostics)world.diagnostics.showDebugUI=arena.showDebugUI;});break;
                case 6:
                    if(!world){Add("보스 전용 시험장에서 사용",reason:"현재 씬은 전용 시험장이 아닙니다");break;}
                    Group("보스전 단계");
                    var boss=world.GetComponent<ClockExecutionerEncounter>();
                    Add("광폭화 단계 적용",()=>boss.DebugTestHealth(false),reason:world.clockBossTestOnly&&boss?null:"보스 전용 시험장에서 사용");
                    Add("최종 광폭화 단계 적용",()=>boss.DebugTestHealth(true),reason:world.clockBossTestOnly&&boss?null:"보스 전용 시험장에서 사용");
                    Group("전투 재시작");
                    Add("보스전 재시작",()=>boss.Retry(),reason:world.clockBossTestOnly&&boss?null:"보스 전용 시험장에서 사용");break;
                case 7:
                    Group("공격·적중음 · 기기에 저장");
                    Add("좌우 / −·+로 5%씩 조절 · 기본값은 팀 기본 믹스(AudioMixDefaults) · 0%는 음소거 · 다음 공격부터 반영");
                    Add("기본공격·적중음과 개별 설정하지 않은 스킬에 적용합니다. 스킬별 음량은 별도 메뉴에서 조절하세요.");
                    for(int i=0;i<WeaponAudioSettings.Forms.Length;i++)
                    {
                        string audioForm=WeaponAudioSettings.Forms[i], audioName=WeaponAudioSettings.Names[i];
                        Add(audioName+" 음량: "+Mathf.RoundToInt(WeaponAudioSettings.Get(audioForm)*100)+"%", null,
                            direction=>{WeaponAudioSettings.Set(audioForm,WeaponAudioSettings.Get(audioForm)+direction*WeaponAudioSettings.Step);status=audioName+" 음량 저장 · 메뉴를 닫고 공격해서 확인하세요.";});
                    }
                    Add("모든 무기 음량을 팀 기본값으로 초기화",()=>{WeaponAudioSettings.ResetAll();status="무기별 음량 초기화 완료 · 팀 기본값";});break;
                case 8:
                    Group("스킬별 음량 · 기기에 자동 저장");
                    Add("좌우 / −·+로 5%씩 조절 · 0%는 음소거 · 다음 소리부터 반영");
                    Add("투척·폭발·설치·이동·적중음까지 해당 스킬에 함께 적용합니다.");
                    var audioForms=arena.catalog.forms.Where(x=>x.specials!=null&&x.specials.Any(s=>s!=null&&SkillAudioSettings.Contains(s.id))).ToArray();
                    if(audioForms.Length==0){Add("조절 가능한 스킬이 없습니다.");break;}
                    audioWeapon=Mathf.Clamp(audioWeapon,0,audioForms.Length-1);
                    Choice("음량을 조절할 무기",audioForms.Select(x=>x.name).ToArray(),audioWeapon,index=>audioWeapon=index);
                    Group(audioForms[audioWeapon].name+" · 개별 설정은 무기 음량과 독립적");
                    foreach(var audioSkill in audioForms[audioWeapon].specials.Where(s=>s!=null&&SkillAudioSettings.Contains(s.id)))
                    {
                        string id=audioSkill.id, name=audioSkill.name;
                        string mode=SkillAudioSettings.IsOverridden(id)?"개별 설정":WeaponAudioSettings.TrySkillDefault(id,out _)?"팀 기본값":"무기 음량 따름";
                        Add(name+": "+Mathf.RoundToInt(SkillAudioSettings.Get(id)*100)+"% · "+mode,null,
                            direction=>{SkillAudioSettings.Set(id,SkillAudioSettings.Get(id)+direction*WeaponAudioSettings.Step);status=name+" 음량 저장 · 메뉴를 닫고 사용해서 확인하세요.";});
                        Add(name+" · 기본값으로 복원",()=>{SkillAudioSettings.Reset(id);status=name+" · 기본값(팀 기본값 또는 무기 음량)으로 복원";});
                    }
                    Group("전체 스킬 초기화");
                    Add("모든 스킬을 기본값으로 복원",()=>{SkillAudioSettings.ResetAll();status="모든 스킬의 개별 설정 해제 · 무기별 음량은 유지합니다.";});break;
            }
        }
        static string DescribeEffect(ProgressionEffect effect, WeaponGrowth growth) => effect.kind switch
        {
            ProgressionIds.PaletteSlotCount => $"팔레트 {effect.value}칸",
            ProgressionIds.FirstSlotWildcard => $"무기 {effect.weaponSlot} 첫 칸 무색",
            ProgressionIds.StartingGoldBonus => $"시작 골드 +{effect.amount}",
            ProgressionIds.DestroyedInkGoldBonus => $"잉크 파괴 골드 +{effect.amount}",
            ProgressionIds.AttackBonusPercent => $"공격력 +{effect.value}%",
            ProgressionIds.MaxHealthBonus => $"최대 체력 +{effect.value}",
            ProgressionIds.BagSlotCount => $"가방 {effect.value}칸",
            ProgressionIds.MoveSpeedBonusPercent => $"이동 속도 +{effect.value}%",
            ProgressionIds.DashCharges => $"대시 {effect.value}회",
            ProgressionIds.InkPartUnlock => "잉크 해금 / "+(growth?.Data.parts.FirstOrDefault(p=>p.id==effect.part)?.name ?? effect.part),
            _ => "단계 효과 적용"
        };

        float MenuScale => Mathf.Max(.5f,Mathf.Min(Screen.width/1280f,Screen.height/720f));
        Rect PanelArea(float scale)
        {
            float width=Screen.width/scale,height=Screen.height/scale;
            float panelWidth=width*PanelFraction,panelHeight=placing?Mathf.Min(220,height*PanelFraction):height*PanelFraction;
            return new Rect((width-panelWidth)/2,placing?24:(height-panelHeight)/2,panelWidth,panelHeight);
        }
        public bool BeginMonsterPlacement()
        {
            if(!IsOpen||scope==null||!world||!world.Ready||species<0||species>=world.Data.monsters.Length)return false;
            placing=true;placementStartedFrame=Time.frameCount;
            cursor=arena.Player.Position+Vector2.right*4;ClampMonsterCursor();
            lastPlacementMouse=Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero;
            status="마우스/방향키/왼쪽 스틱 이동 / 클릭/Enter/A 배치 / Esc/B 뒤로";
            return true;
        }
        void ClampMonsterCursor()
        {
            if(!world.View)return;
            var view=world.View.WorldToViewportPoint(cursor);
            view.x=Mathf.Clamp01(view.x);view.y=Mathf.Clamp01(view.y);
            cursor=world.View.ViewportToWorldPoint(view);
        }
        public bool TryPointMonsterCursor(Vector2 screen)
        {
            if(!IsOpen||!placing||!world||!world.View)return false;
            float scale=MenuScale;
            if(PanelArea(scale).Contains(new Vector2(screen.x,Screen.height-screen.y)/scale))return false;
            if(screen.x<0||screen.y<0||screen.x>=Screen.width||screen.y>=Screen.height||
                !Deviant.Presentation.Unity.CameraDisplayProjection.TryScreenToWorldPoint(world.View,screen,out var point))
            { status="게임 화면 안의 위치를 선택하세요.";return false; }
            cursor=point;return true;
        }
        public bool TryPlaceMonster()
        {
            if(!IsOpen||scope==null||!placing||!world||!world.Ready)return false;
            var settings=world.GetComponent<Chapter1MonsterTestSettings>();
            if(!settings){settings=world.gameObject.AddComponent<Chapter1MonsterTestSettings>();settings.world=world;}
            if(species<0||species>=world.Data.monsters.Length){status="몬스터 종류를 다시 선택하세요.";return false;}
            bool success=settings.TryPlace(world.Data.monsters[species].id,cursor,out var enemy);
            if(success)spawned.Add(enemy);
            status=settings.Status;return success;
        }

        void Update()
        {
            var k=Keyboard.current;var g=Gamepad.current;
            bool toggle=k!=null&&k.ctrlKey.isPressed&&k.backquoteKey.wasPressedThisFrame||g!=null&&g.leftStickButton.isPressed&&g.rightStickButton.wasPressedThisFrame;
            if(IsOpen&&k!=null&&k.escapeKey.wasPressedThisFrame){Back();return;}
            if(toggle){if(IsOpen)Close();else Open();return;}if(!IsOpen)return;
            if(!DevFeatures.Enabled){Close();return;}
            bool back=k!=null&&k.backspaceKey.wasPressedThisFrame||g!=null&&g.buttonEast.wasPressedThisFrame;
            if(back){Back();return;}
            Vector2 nav=g!=null?g.leftStick.ReadValue()+g.dpad.ReadValue():Vector2.zero;
            if(k!=null)nav+=new Vector2((k.rightArrowKey.isPressed?1:0)-(k.leftArrowKey.isPressed?1:0),(k.upArrowKey.isPressed?1:0)-(k.downArrowKey.isPressed?1:0));
            bool submit=k!=null&&k.enterKey.wasPressedThisFrame||g!=null&&g.buttonSouth.wasPressedThisFrame;
            if(placing)
            {
                // The press that opens placement must not create the first monster as well.
                if(Time.frameCount==placementStartedFrame)return;
                var mouse=Mouse.current;
                bool click=mouse!=null&&mouse.leftButton.wasPressedThisFrame;
                bool pointerValid=false;
                if(mouse!=null)
                {
                    var point=mouse.position.ReadValue();
                    if(point!=lastPlacementMouse||click)pointerValid=TryPointMonsterCursor(point);
                    lastPlacementMouse=point;
                }
                if(nav.sqrMagnitude>.04f)
                { cursor+=Vector2.ClampMagnitude(nav,1)*12*Time.unscaledDeltaTime;ClampMonsterCursor(); }
                if(submit||click&&pointerValid)TryPlaceMonster();
                return;
            }
            if(category==2&&picker==null)
            {
                int tab=k!=null&&k.tabKey.wasPressedThisFrame?(k.shiftKey.isPressed?-1:1):g!=null&&g.rightShoulder.wasPressedThisFrame?1:g!=null&&g.leftShoulder.wasPressedThisFrame?-1:0;
                if(tab!=0)SelectWeapon((slot+tab+arena.Player.weapons.Count)%arena.Player.weapons.Count);
            }
            Build();EnsureSelection();if(selected<0)return;
            if(nav.sqrMagnitude<.2f)repeatAt=0;
            else if(Time.unscaledTime>=repeatAt){repeatAt=Time.unscaledTime+.18f;if(Mathf.Abs(nav.y)>.5f||Mathf.Abs(nav.x)>.5f)NavigateSelection(nav);}
            if(submit)ExecuteSelected();
        }
    }
}
