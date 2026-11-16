using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Deviant.Run;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        bool approvedBagCategory;
        bool inventoryRemovePressed, inventoryRenamePressed, inventorySwapPressed;
        string MenuKey(string keyboard, Deviant.Input.Contracts.GameAction action) => Controls && Controls.UsingGamepad
            ? keyboard == "Q / E" ? KeyLabel(Deviant.Input.Contracts.GameAction.Inventory)+"/"+KeyLabel(Deviant.Input.Contracts.GameAction.Interact) : KeyLabel(action)
            : keyboard;
        string InventoryKey(string key) => key switch {
            "F" => MenuKey(key, Deviant.Input.Contracts.GameAction.Submit),
            "Esc" => MenuKey(key, Deviant.Input.Contracts.GameAction.Cancel),
            "R" => KeyLabel(Deviant.Input.Contracts.GameAction.InventoryRemove),
            "N" => KeyLabel(Deviant.Input.Contracts.GameAction.InventoryRename),
            "X" => KeyLabel(Deviant.Input.Contracts.GameAction.SwapWeaponOrder),
            "C" => MenuKey(key, Deviant.Input.Contracts.GameAction.Cancel),
            "←" or "→" => Controls && Controls.UsingGamepad ? "LS " + key : key,
            _ => key };
        string InventoryPromptSignature => string.Join("/", new[]{ InventoryKey("F"), InventoryKey("Esc"), InventoryKey("R"), InventoryKey("N"), InventoryKey("X"), MenuKey("W / S", Deviant.Input.Contracts.GameAction.MapPan), MenuKey("← → ↑ ↓", Deviant.Input.Contracts.GameAction.Navigate) });
        readonly Dictionary<string,Vector2> approvedScrollPositions=new Dictionary<string,Vector2>();
        readonly Dictionary<RectTransform,string> approvedScrollKeys=new Dictionary<RectTransform,string>();
        string approvedListSelection,approvedPaletteSelection,approvedInkSelection;
        Material approvedInactiveArt;
        const float CraftTransferCenterX=298, CraftTransferSlotStep=386;
        float craftTransferX=CraftTransferCenterX,craftTransferArrowStart;
        RectTransform craftTransferCard,craftTransferCue;
        readonly Text[] craftTransferArrows=new Text[3];

        void DimApprovedArt(RawImage art)
        {
            if(!approvedInactiveArt)
            {
                var shader=Resources.Load<Shader>("ApprovedUi/InactiveArt");
                if(shader){approvedInactiveArt=new Material(shader){hideFlags=HideFlags.HideAndDontSave};approvedInactiveArt.SetFloat("_Saturation",0);}
            }
            art.material=approvedInactiveArt;art.color=new Color(1,1,1,.3f);
        }
        Color InventoryInkColor(RunInkDefinition ink)=>ink?.color switch {
            "red"=>new Color32(231,149,156,255),"yellow"=>new Color32(225,206,130,255),
            "green"=>new Color32(167,203,179,255),"blue"=>new Color32(166,202,219,255),
            "purple"=>new Color32(196,167,212,255),_=>CorePaper };
        void InventoryLabel(Button button,string label,Color color,int size=16)
        {
            var text=button.GetComponentInChildren<Text>();text.text=label;text.color=color;text.fontSize=size;
            button.GetComponent<ApprovedActionVisual>().enabled=false;
        }
        Button InventoryAction(string name,string label,string key,Transform parent,float x,float y,float width,Action action,bool selected=true,bool enabled=true)
        {
            var button=LiveButton(name,(string.IsNullOrEmpty(key)?"":key+"  ")+label,parent,x,y,width,32,action,selected,enabled,18);
            return button;
        }
        void DrawInventoryHeader(EquipmentBook b,string title)
        {
            LiveWords(title,b.Body,65,31,300,42,30);
            for(int i=0;i<2;i++)
            {
                int tab=i==0?0:2;bool selected=(liveNavigation>=0?liveNavigation:liveTab==2?2:0)==tab;
                LiveButton("Core tab "+tab,i==0?"가방":"장비",b.Body,638+i*83,38,64,32,()=>SetLiveTab(tab),selected,size:22);
            }
            Fill(b.Body,805,38,.6f,31,new Color32(210,216,222,60));
            var closeKey=InventoryKey("Esc");
            ApprovedKeycap(b.Body,closeKey,812,40,Mathf.Max(29,closeKey.Length*7+8),25,12);
            InventoryAction("Inventory close","닫기","",b.Body,866,38,42,()=>Presenter.Close(),false);
            LiveRule(b.Body,65,87,830);
            LiveRule(b.Body,65,489,830);DrawEquipmentFooter(b.Body);
        }
        void DrawApprovedBagCraft(EquipmentBook b)
        {
            if(liveStage=="done"||liveStage=="craftTarget"){DrawApprovedCraftResult(b,LiveWeapon());return;}
            DrawInventoryHeader(b,liveTab==0?"가방":"제작");
            Fill(b.Body,502,104,.6f,361,new Color32(210,216,222,40));
            if(liveTab==0||liveStage=="list")DrawApprovedInventoryList(b);else DrawApprovedCrafting(b);
            if(liveStage=="review")DrawInventoryConfirmation(b,LiveWeapon());
        }

        RectTransform ApprovedScroll(string name,Transform parent,float x,float y,float width,float height)
        {return JournalScroll(name,parent,x,y,width,height);}

        void RestoreApprovedScroll(RectTransform content,string key,float width,float height)
        {
            var scroll=content.parent.GetComponent<ScrollRect>();
            content.sizeDelta=new Vector2(Mathf.Max(scroll.viewport.rect.width,width),Mathf.Max(scroll.viewport.rect.height,height));
            approvedScrollPositions.TryGetValue(key,out var pos);
            content.anchoredPosition=new Vector2(Mathf.Clamp(pos.x,-Mathf.Max(0,width-scroll.viewport.rect.width),0),Mathf.Clamp(pos.y,0,Mathf.Max(0,height-scroll.viewport.rect.height)));
            approvedScrollKeys[content]=key;
            scroll.onValueChanged.AddListener(_=>approvedScrollPositions[key]=content.anchoredPosition);
        }

        void SaveApprovedScrollPositions()
        {
            // A model event can rebuild before ScrollRect's LateUpdate emits onValueChanged.
            foreach(var entry in approvedScrollKeys)if(entry.Key)approvedScrollPositions[entry.Value]=entry.Key.anchoredPosition;
            approvedScrollKeys.Clear();
        }

        void RevealApprovedItem(RectTransform content,float start,float size,bool horizontal=false)
        {
            float viewport=horizontal?content.parent.GetComponent<RectTransform>().rect.width:content.parent.GetComponent<RectTransform>().rect.height;
            float extent=horizontal?content.rect.width:content.rect.height;
            float offset=horizontal?-content.anchoredPosition.x:content.anchoredPosition.y;
            if(start<offset)offset=start;else if(start+size>offset+viewport)offset=start+size-viewport;
            offset=Mathf.Clamp(offset,0,Mathf.Max(0,extent-viewport));
            content.anchoredPosition=horizontal?new Vector2(-offset,0):new Vector2(0,offset);
        }

        RectTransform ApprovedHorizontalScroll(string name,Transform parent,float x,float y,float width,float height)
        {
            var content=ApprovedScroll(name,parent,x,y,width,height);
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.vertical=false;scroll.horizontal=true;
            scroll.verticalScrollbar.gameObject.SetActive(false);scroll.verticalScrollbar=null;
            return content;
        }

        void DrawApprovedInventoryList(EquipmentBook b)
        {
            // Receipt configuration can enter the bag without introducing a third public tab.
            if(liveTab==1){liveTab=0;liveFilter=0;}
            bool inks=liveFilter==1;
            for(int i=0;i<2;i++)
            {
                int filter=i;
                LiveButton("Core filter "+i,i==0?"스케치":"잉크",b.Body,65+i*90,103,76,28,
                    ()=>{liveFilter=filter;livePick=0;approvedBagCategory=true;},liveNavigation<0&&liveFilter==i,size:18);
            }
            var weapons=LiveWeapons();var inkList=LiveInks();int count=inks?inkList.Length:weapons.Length;
            livePick=Mathf.Clamp(livePick,0,Math.Max(0,count-1));
            var content=ApprovedScroll("Approved inventory list",b.Body,65,145,413,280);
            const float stepX=98,stepY=139;
            bool active=!approvedBagCategory&&liveNavigation<0;
            for(int i=0;i<count;i++)
            {
                int index=i;bool selected=active&&livePick==i;
                var weapon=inks?null:weapons[i];var ink=inks?inkList[i]:null;
                var button=LiveButton("Core item "+i,"",content,i%4*stepX,i/4*stepY,91,130,
                    ()=>{bool again=livePick==index&&!approvedBagCategory;livePick=index;approvedBagCategory=false;if(again)CraftSelectedBagItem();});
                InventoryLabel(button,"",CoreMuted);
                var art=LiveArt(inks?ApprovedTheme.Ink(ink.id):LiveWeaponArt(weapon),button.transform,10,5,71,71);
                if(inks&&!selected)DimApprovedArt(art);
                else if(!inks&&!weapon.Confirmed){DimApprovedArt(art);art.color=Color.white;}
                var color=inks?(selected?InventoryInkColor(ink):CoreMuted):selected?CoreGold:CorePaper;
                var name=LiveWords(inks?ink.name:LiveWeaponName(weapon),button.transform,0,83,91,23,selected?16:15,color,TextAnchor.UpperCenter);
                FitEquipmentName(name,91);
                string status=inks?$"{Sketch.AvailableInkUses(ink.id)}/{Sketch.TotalInkUses(ink.id)}":weapon.Confirmed?$"내구도 {Mathf.CeilToInt(weapon.Durability/Mathf.Max(1,weapon.Maximum)*100)}%":$"{weapon.Slots.Count}칸 / 미확정";
                LiveWords(status,button.transform,0,109,91,20,12,inks?selected?CorePaper:CoreMuted:selected?CoreGold:CoreMuted,TextAnchor.UpperCenter);
            }
            if(count==0)LiveWords(inks?"보유한 잉크가 없습니다.":"보유한 스케치가 없습니다.",content,0,0,390,30,16,CoreMuted);
            string listKey="inventory/"+liveFilter;
            RestoreApprovedScroll(content,listKey,413,Mathf.CeilToInt(count/4f)*stepY);
            string selection=listKey+"/"+livePick;
            if(approvedListSelection!=selection){RevealApprovedItem(content,livePick/4*stepY,130);approvedListSelection=selection;}
            liveDetail=ApprovedScroll("Approved item description",b.Body,535,104,360,319);
            float bottom=30;
            if(active&&count>0)
            {
                if(inks){sketchSelectedInk=inkList[livePick].id;bottom=DrawApprovedInkDescription(liveDetail,inkList[livePick],345,0,false);}
                else{sketchSelectedWeapon=weapons[livePick].Id;bottom=DrawApprovedWeaponDescription(liveDetail,weapons[livePick],345);}
            }
            RestoreApprovedScroll(liveDetail,listKey+"/detail/"+livePick,360,bottom+12);
            LiveRule(b.Body,535,435,360);
            if(active&&count>0)InventoryAction("Inventory primary",inks?"스케치 선택하기":weapons[livePick].Confirmed?"장착하기":"제작하기","F",b.Body,535,447,260,CraftSelectedBagItem);
        }

        float ApprovedParagraph(Transform parent,string text,float y,float width,int size=14,Color? color=null,float spacing=1.7f)
        {
            // Unity's generator wraps rich text at the actual viewport width.
            var label=LiveWords(text,parent,0,y,width,1,size,color);label.lineSpacing=spacing/1.2f;
            float height=Mathf.Max(size*spacing,label.preferredHeight);label.rectTransform.sizeDelta=new Vector2(width,height);
            return y+height;
        }

        float ApprovedSection(Transform parent,string text,float y,float width)
        {LiveRule(parent,0,y,width);LiveWords(text,parent,0,y+16.5f,width,22,14);return y+48.5f;}

        string ApprovedEffect(RunInkDefinition ink,bool matched)
        {return Regex.Replace(LiveInkText(ink,matched),@"(쿨타임 )<color=#B4D38D>([^<]+)</color>","$1$2");}

        // Keep the original condition/effect palette; summaries already contain final stacked values.
        static string ApprovedGroupedEffect(string description)
        {
            var lines=description.Split('\n');
            for(int i=0;i<lines.Length;i++)
            {
                if(i==0)lines[i]="<color=#ACD4E0>"+lines[i]+"</color>";
                else if(!lines[i].StartsWith("쿨타임 ")&&!lines[i].StartsWith("잉크 쿨타임 "))
                    lines[i]=Regex.Replace(lines[i],@"[+\-]?\d+(?:\.\d+)?(?:%|초)?|기절|슬로우|속박|회복|방어막|이동속도|기본 공격 피해|스킬 피해",
                        "<color=#B4D38D>$0</color>");
            }
            return string.Join("\n",lines);
        }

        string ApprovedBasicDescription(RunInkWeapon weapon)
        {
            var attacks=host.Arena.catalog.forms.FirstOrDefault(f=>f.id==weapon.Form)?.basics;
            if(attacks==null||attacks.Length==0)return "기본 공격";
            var names=attacks.Select(a=>a.name).Where(n=>!string.IsNullOrWhiteSpace(n)).ToArray();
            if(names.Length>0)return string.Join(" → ",names);
            // Several current melee catalog entries have no authored attack name.
            return (attacks.Length>1?attacks.Length+"연속 공격":"기본 공격")+" / 피해 "+string.Join(" → ",attacks.Select(a=>a.damage.ToString("0.#")));
        }

        float DrawApprovedWeaponDescription(Transform parent,RunInkWeapon weapon,float width)
        {
            LiveArt(LiveWeaponArt(weapon),parent,0,0,68,68);
            var title=LiveWords(LiveWeaponName(weapon),parent,85,0,width-85,34,26);title.name="Inventory weapon title";FitEquipmentName(title,width-85);
            LiveWords(EquipmentSkill(weapon),parent,85,39,width-85,24,15,CoreMuted);
            float y=86;
            if(weapon.Confirmed){DrawEquipmentDurability(parent,weapon,0,y,width-52);y+=38;}
            LiveRule(parent,0,y,width);y+=22;
            LiveWords("기본 공격",parent,0,y,width,20,13,CoreMuted);y+=28;
            y=ApprovedParagraph(parent,ApprovedBasicDescription(weapon),y,width,19)+24;
            LiveWords("스킬",parent,0,y,width,20,13,CoreMuted);y+=28;
            y=ApprovedParagraph(parent,EquipmentSkill(weapon),y,width,19)+8;
            y=ApprovedParagraph(parent,LiveSkillDescription(weapon),y,width,14,CoreMuted)+25;
            LiveWords("팔레트 / "+weapon.Slots.Count+"칸",parent,0,y,width,20,13,CoreMuted);y+=29;
            for(int i=0;i<weapon.Slots.Count;i++){var slot=weapon.Slots[i];LiveArt(ApprovedTheme.Palette(slot.Color,slot.Ink?.id),parent,i*43,y,36,26);}
            y+=45;
            if(weapon.Slots.Any(s=>s.Ink!=null))y=DrawApprovedEffects(parent,weapon,y,width);
            return y;
        }
        float DrawApprovedMiniPalette(Transform parent,RunInkWeapon weapon,float y,float width)
        {
            LiveWords("팔레트",parent,0,y,width,20,13,CoreMuted);y+=28;
            for(int i=0;i<weapon.Slots.Count;i++){var slot=weapon.Slots[i];LiveArt(ApprovedTheme.Palette(slot.Color,slot.Ink?.id),parent,i*41,y,35,25);}
            return y+44;
        }
        float DrawApprovedEffects(Transform parent,RunInkWeapon weapon,float y,float width)
        {
            LiveRule(parent,0,y,width);LiveWords("적용 효과",parent,0,y+16,width,20,13,CoreMuted);
            return DrawGroupedInkEffects(parent,weapon,y+46,width);
        }
        float DrawGroupedInkEffects(Transform parent,RunInkWeapon weapon,float y,float width)
        {
            foreach(var group in RunInkGroup.Collect(weapon,Sketch.Catalog))
            {
                y=ApprovedParagraph(parent,group.Title,y,width,16,InventoryInkColor(group.Ink))+8;
                y=ApprovedParagraph(parent,ApprovedGroupedEffect(group.Description),y,width,14)+22;
            }
            return y;
        }
        float InventoryInkSummary(Transform parent,RunInkWeapon weapon,float width)
        {
            float y=0;
            foreach(var group in RunInkGroup.Collect(weapon,Sketch.Catalog))
                y=ApprovedParagraph(parent,group.Ink.name+" × "+group.Mounts.Length,y,width,15,InventoryInkColor(group.Ink),1.3f)+7;
            if(y==0)y=ApprovedParagraph(parent,"잉크 없이 기본 공격과 스킬을 사용할 수 있습니다.",0,width,13,CoreMuted);
            return y;
        }
        float DrawApprovedInkDescription(Transform parent,RunInkDefinition ink,float width,float y,bool matched,bool compact=false)
        {
            y=ApprovedParagraph(parent,ink.name,y,width,compact?18:25,InventoryInkColor(ink))+10;
            if(!compact)
            {
                y=ApprovedParagraph(parent,$"남은 횟수 {Sketch.AvailableInkUses(ink.id)}회 / 총 횟수 {Sketch.TotalInkUses(ink.id)}회",y,width,14,CoreMuted)+20;
                LiveArt(ApprovedTheme.Ink(ink.id),parent,0,y,74,104);y+=124;
                LiveWords("효과",parent,0,y,width,20,13,CoreMuted);y+=28;
            }
            y=ApprovedParagraph(parent,ApprovedEffect(ink,matched),y,width,14,CoreMuted)+12;
            return y;
        }
        Button ApprovedCoreAction(EquipmentBook b,string label,float x,float y,float width,Action action,bool enabled=true,bool primary=false,bool emphasizeAvailable=false)
        {
            int index=liveActions.Count;
            var button=InventoryAction("Core action "+index,label,liveAction==index+1||primary?"F":"",b.Body,x,y,width,action,liveAction==index+1||primary,enabled);
            if(enabled&&emphasizeAvailable)
            {
                var visual=button.GetComponent<ApprovedActionVisual>();if(visual)visual.NormalColor=CoreGold;
                button.GetComponentInChildren<Text>().color=CoreGold;
            }
            liveActions.Add(button);return button;
        }
        void DrawApprovedCrafting(EquipmentBook b)
        {
            var weapon=LiveWeapon();if(weapon==null||weapon.Destroyed){SetLiveTab(0);return;}
            liveSlot=Mathf.Clamp(liveSlot,0,weapon.Slots.Count-1);
            if(liveStage=="ink"&&!CanChooseLiveInk){liveStage="palette";liveAction=0;}
            bool palette=liveStage=="palette"&&liveAction==0;
            var left=Rect("Craft editing information",b.Body,65,104,413,317);
            if(liveAction>0&&liveStage!="review")left.gameObject.AddComponent<CanvasGroup>().alpha=.35f;
            LiveArt(LiveWeaponArt(weapon),left,0,0,60,60);
            LiveWords(LiveWeaponName(weapon),left,79,0,330,32,26);
            LiveWords(EquipmentSkill(weapon),left,79,37,330,23,15,CoreMuted);
            LiveWords("팔레트",left,0,77,150,23,18);
            LiveWords($"{weapon.Slots.Count(s=>s.Ink!=null)} / {weapon.Slots.Count}칸 적용",left,250,80,160,22,13,CoreMuted,TextAnchor.UpperRight);
            float cell=413f/weapon.Slots.Count;
            for(int i=0;i<weapon.Slots.Count;i++)
            {
                int slot=i;bool selected=i==liveSlot&&palette;var value=weapon.Slots[i];
                var button=LiveButton("Craft slot "+i,"",left,i*cell,112,cell-4,75,()=>{liveSlot=slot;liveAction=0;liveStage="palette";});
                InventoryLabel(button,"",CoreMuted);
                LiveArt(ApprovedTheme.Palette(value.Color,value.Ink?.id),button.transform,0,0,cell-9,49,selected?1:.65f);
                LiveWords((i+1)+"번 칸",button.transform,0,57,cell-4,20,13,selected?CoreGold:CoreMuted,TextAnchor.UpperCenter);
            }
            if(palette)InventoryAction("Choose slot ink","잉크 선택","F",left,0,199,160,()=>OpenLiveInkPicker(),true,CanChooseLiveInk);
            if(weapon.Slots[liveSlot].Ink!=null)InventoryAction("Clear selected slot","선택한 칸 비우기","R",left,222,199,190,()=>LiveTrade(b,()=>Sketch.Place(weapon.Id,liveSlot,null)),false);
            LiveRule(left,0,234,413);LiveWords("현재 적용한 잉크",left,0,246,413,20,13,CoreMuted);
            var summary=ApprovedScroll("Craft applied inks",left,0,274,413,55);
            RestoreApprovedScroll(summary,"applied/"+weapon.Id,413,InventoryInkSummary(summary,weapon,393));
            if(liveStage!="ink")liveDetail=summary;
            LiveRule(b.Body,65,435,413);
            ApprovedCoreAction(b,"제작하기",65,447,150,()=>{liveStage="review";liveAction=1;},emphasizeAvailable:true);
            ApprovedCoreAction(b,"돌아가기",224,447,145,BackApprovedCraft);
            DrawApprovedCraftInk(b,weapon);
        }
        void ApplyInventoryInk()
        {
            var weapon=LiveWeapon();var ink=LiveInks().ElementAtOrDefault(livePick);if(weapon==null||ink==null)return;
            LiveTrade(nativeBag,()=>Sketch.Place(weapon.Id,liveSlot,ink.id));
            if(weapon.Slots[liveSlot].Ink?.id==ink.id){liveStage="palette";liveAction=0;liveInk=null;}
        }
        void DrawApprovedCraftInk(EquipmentBook b,RunInkWeapon weapon)
        {
            bool active=liveStage=="ink";
            var pane=Rect("Craft owned inks",b.Body,535,104,360,317);
            if(!active)pane.gameObject.AddComponent<CanvasGroup>().alpha=.35f;
            LiveWords("보유 잉크",pane,0,0,200,26,21);
            if(active)LiveWords("적용할 곳 / "+(liveSlot+1)+"번 칸",pane,205,4,155,22,13,CoreBlue,TextAnchor.UpperRight);
            var inks=LiveInks();livePick=Mathf.Clamp(livePick,0,Math.Max(0,inks.Length-1));
            var grid=ApprovedScroll("Craft ink list",pane,0,40,360,164);
            for(int i=0;i<inks.Length;i++)
            {
                int index=i;bool selected=active&&i==livePick;var ink=inks[i];
                var button=LiveButton("Craft ink "+i,"",grid,i%3*112,i/3*96,105,91,()=>{if(active)livePick=index;});
                InventoryLabel(button,"",CoreMuted);
                var art=LiveArt(ApprovedTheme.Ink(ink.id),button.transform,33,0,39,46);if(!selected)DimApprovedArt(art);
                var name=LiveWords(ink.name,button.transform,0,50,105,20,14,selected?InventoryInkColor(ink):CoreMuted,TextAnchor.UpperCenter);FitEquipmentName(name,105);
                LiveWords($"{Sketch.AvailableInkUses(ink.id)}/{Sketch.TotalInkUses(ink.id)}",button.transform,0,73,105,18,12,selected?CorePaper:CoreMuted,TextAnchor.UpperCenter);
            }
            RestoreApprovedScroll(grid,"craft-ink-list",360,Mathf.CeilToInt(inks.Length/3f)*96);
            string selection=weapon.Id+"/"+liveSlot+"/"+livePick;
            if(active&&approvedInkSelection!=selection){RevealApprovedItem(grid,livePick/3*96,91);approvedInkSelection=selection;}
            if(active&&inks.Length>0)
            {
                LiveRule(pane,0,217,360);
                liveDetail=ApprovedScroll("Craft ink description",pane,0,231,360,94);
                float y=DrawApprovedInkDescription(liveDetail,inks[livePick],340,0,inks[livePick].color==weapon.Slots[liveSlot].Color,true);
                RestoreApprovedScroll(liveDetail,"craft-ink-detail/"+inks[livePick].id,360,y);
            }
            LiveRule(b.Body,535,435,360);
            if(active)InventoryAction("Apply selected ink",(liveSlot+1)+"번 칸에 적용","F",b.Body,535,447,300,ApplyInventoryInk,true,
                inks.Length>0&&(Sketch.AvailableInkUses(inks[livePick].id)>0||weapon.Slots[liveSlot].Ink?.id==inks[livePick].id));
        }
        void DrawInventoryConfirmation(EquipmentBook b,RunInkWeapon weapon)
        {
            var overlay=Rect("Craft confirmation modal",b.Body,0,0,960,540);
            Fill(overlay,0,0,960,540,new Color32(0,0,0,185)).raycastTarget=true;
            ApprovedTheme.CreateFrame(overlay,170,93,620,354).raycastTarget=true;
            var panel=Rect("Craft confirmation",overlay,170,93,620,354);
            // The frame uses the existing theme, while all palette pixels come from palette-package-v6.
            LiveWords("이 구성으로 제작할까요?",panel,30,23,560,40,28);
            int used=weapon.Slots.Count(s=>s.Ink!=null);
            LiveWords($"{LiveWeaponName(weapon)} / {used}칸 적용 / 빈 칸 {weapon.Slots.Count-used}개",panel,30,77,560,23,14,CoreMuted);
            for(int i=0;i<weapon.Slots.Count;i++)
            {
                var slot=weapon.Slots[i];LiveArt(ApprovedTheme.Palette(slot.Color,slot.Ink?.id),panel,30+i*95,119,76,53);
                LiveWords((i+1)+"번 칸",panel,30+i*95,180,76,21,12,CoreMuted,TextAnchor.UpperCenter);
            }
            LiveRule(panel,30,214,560);
            var summary=ApprovedScroll("Confirmed ink summary",panel,30,228,560,56);
            RestoreApprovedScroll(summary,"confirm/"+weapon.Id,560,InventoryInkSummary(summary,weapon,540));
            LiveRule(panel,30,297,560);
            LiveWords("확정 시 잉크가 소모되며 이후 구성은 변경할 수 없습니다.",panel,30,318,300,22,10,CoreMuted,TextAnchor.MiddleLeft);
            InventoryAction("Confirm craft","제작 확정",liveAction==1?"F":"",panel,350,310,125,ConfirmInventoryCraft,liveAction==1);
            InventoryAction("Continue craft","계속 편집",liveAction==2?"F":"",panel,485,310,120,()=>{liveStage="palette";liveAction=1;},liveAction==2);
        }
        void ConfirmInventoryCraft()
        {
            var weapon=LiveWeapon();if(weapon==null||weapon.Confirmed)return;
            LiveTrade(nativeBag,()=>Sketch.Confirm(weapon.Id));
            if(weapon.Confirmed)
            {
                liveStage="craftTarget";liveAction=0;liveTarget=-1;
                craftTransferX=CraftTransferCenterX;craftTransferArrowStart=Time.unscaledTime;
            }
        }
        void DrawInventoryWeaponCard(Transform parent,string name,RunInkWeapon weapon,float x,float y,float width,float height,bool selected,Action choose)
        {
            var button=LiveButton(name,"",parent,x,y,width,height,choose);
            InventoryLabel(button,"",CoreMuted);
            // Quiet outline distinguishes the active destination without a filled highlight.
            Color edge=selected?CoreGold:new Color32(99,110,117,255);
            Fill(button.transform,0,0,width,.7f,edge);Fill(button.transform,0,height-.7f,width,.7f,edge);
            Fill(button.transform,0,0,.7f,height,edge);Fill(button.transform,width-.7f,0,.7f,height,edge);
            if(weapon==null){LiveWords("맨손",button.transform,20,45,width-40,36,24,selected?CoreGold:CoreMuted);return;}
            LiveArt(LiveWeaponArt(weapon),button.transform,20,28,48,48);
            var title=LiveWords(LiveWeaponName(weapon),button.transform,82,32,width-100,32,24,selected?CoreGold:CoreMuted);FitEquipmentName(title,width-100);
            DrawEquipmentDurability(button.transform,weapon,20,88,width-80,true);
            for(int i=0;i<weapon.Slots.Count;i++){var slot=weapon.Slots[i];LiveArt(ApprovedTheme.Palette(slot.Color,slot.Ink?.id),button.transform,20+i*37,119,31,23);}
        }
        void SelectCraftTransferTarget(int target)
        {
            if(target==liveTarget)return;
            liveTarget=target;craftTransferArrowStart=Time.unscaledTime;
        }
        void DrawCraftTransferDirection(Transform parent,int target,float x,string glyph)
        {
            if(liveTarget==target)return;
            var button=LiveButton("Craft transfer direction "+target,"",parent,x,65,28,28,
                ()=>SelectCraftTransferTarget(target),enabled:liveTarget!=target);
            InventoryLabel(button,"",CoreMuted);
            ApprovedKeycap(button.transform,InventoryKey(glyph),0,0,Mathf.Max(27,InventoryKey(glyph).Length*7+6),26,12);
        }
        void DrawApprovedCraftResult(EquipmentBook b,RunInkWeapon weapon)
        {
            if(weapon==null){SetLiveTab(0);return;}
            LiveWords("제작 완료",b.Body,300,22,360,37,29,CorePaper,TextAnchor.MiddleCenter);
            // Keep this X across content rebuilds so quick left/right changes continue the same motion.
            craftTransferCard=Rect("New crafted weapon transfer",b.Body,craftTransferX,75,362,163);
            DrawInventoryWeaponCard(craftTransferCard,"New crafted weapon",weapon,0,0,362,163,true,()=>{});
            DrawCraftTransferDirection(craftTransferCard,0,-36,"←");
            DrawCraftTransferDirection(craftTransferCard,1,370,"→");
            for(int i=0;i<2;i++)
            {
                int slot=i;float x=105+i*CraftTransferSlotStep;
                DrawInventoryWeaponCard(b.Body,"Craft destination "+i,Sketch.Equipped(i),x,326,362,163,i==liveTarget,()=>SelectCraftTransferTarget(slot));
                LiveWords("무기 "+(i+1),b.Body,x+20,336,150,19,12,CoreMuted);
            }
            if(liveTarget>=0)
            {
                craftTransferCue=Rect("Craft transfer cue",b.Body,craftTransferX+181,243,200,76);
                for(int i=0;i<craftTransferArrows.Length;i++)
                    craftTransferArrows[i]=LiveWords("↓",craftTransferCue,-12,0,24,22,20,CoreGold,TextAnchor.MiddleCenter);
                bool can=Sketch.State.Arsenal.CanEquip(weapon.Id,liveTarget,out string reason);
                InventoryAction("Complete equip",Sketch.Equipped(liveTarget)==null?"장착하기":"교체하기","F",craftTransferCue,30,22,168,FinishInventoryCraft,true,can);
                if(!can)LiveWords(reason,b.Body,105,300,750,20,12,CoreGold,TextAnchor.UpperCenter);
            }
            else
            {
                craftTransferCue=null;
                LiveWords(MenuKey("← →",Deviant.Input.Contracts.GameAction.Navigate)+"  교체할 자리 선택",b.Body,300,270,360,25,14,CoreMuted,TextAnchor.MiddleCenter);
            }
            InventoryAction("Complete store","보관하기","C",b.Body,403,500,160,CloseInventoryCompletion,true);
            TickCraftTransferVisual(false);
        }
        void TickCraftTransferVisual(bool move=true)
        {
            if(liveStage!="done"&&liveStage!="craftTarget"||!craftTransferCard)return;
            float destination=liveTarget<0?CraftTransferCenterX:105+liveTarget*CraftTransferSlotStep;
            if(move)
            {
                float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
                craftTransferX=Mathf.Lerp(craftTransferX,destination,blend);
                if(Mathf.Abs(craftTransferX-destination)<.1f)craftTransferX=destination;
            }
            craftTransferCard.anchoredPosition=new Vector2(craftTransferX,-75);
            if(!craftTransferCue)return;
            craftTransferCue.anchoredPosition=new Vector2(craftTransferX+181,-243);
            for(int i=0;i<craftTransferArrows.Length;i++)
            {
                var arrow=craftTransferArrows[i];if(!arrow)continue;
                float phase=Mathf.Repeat((Time.unscaledTime-craftTransferArrowStart)/.9f+i/3f,1);
                arrow.rectTransform.anchoredPosition=new Vector2(-12,-phase*54);
                var color=CoreGold;color.a=Mathf.Sin(phase*Mathf.PI)*.85f;arrow.color=color;
            }
        }
        void CloseInventoryCompletion()
        {
            liveReceiptConfiguration=false;craftTransferCard=craftTransferCue=null;
            SetLiveTab(0);Presenter.Close();
        }
        void FinishInventoryCraft()
        {
            if(liveTarget<0||liveTarget>1)return;
            var weapon=LiveWeapon();if(weapon==null)return;
            LiveTrade(nativeBag,()=>Sketch.Equip(weapon.Id,liveTarget));
            if(Sketch.Equipped(liveTarget)==weapon)CloseInventoryCompletion();
        }
        void BackApprovedCraft()
        {
            if(liveStage=="done"||liveStage=="craftTarget"){CloseInventoryCompletion();return;}
            if(liveStage=="ink"||liveStage=="review"){liveStage="palette";liveAction=0;return;}
            if(liveTab==1)
            {
                if(liveReceiptConfiguration){liveReceiptConfiguration=false;Presenter.Close();return;}
                var id=liveWeapon;SetLiveTab(0);approvedBagCategory=false;livePick=Math.Max(0,Array.FindIndex(LiveWeapons(),w=>w.Id==id));return;
            }
            if(!approvedBagCategory){approvedBagCategory=true;return;}Presenter.Close();
        }
        void TickApprovedBagCraftInput()
        {
            if(liveStage=="done"||liveStage=="craftTarget")
            {
                if(Keyboard.current?.cKey.wasPressedThisFrame==true||LiveBackPressed){CloseInventoryCompletion();return;}
                if(LiveSubmitPressed){FinishInventoryCraft();return;}
                int move=TakeLiveNavigation();if(Math.Abs(move)==1)SelectCraftTransferTarget(move<0?0:1);return;
            }
            if(LiveBackPressed){BackApprovedCraft();return;}
            if(liveStage=="review")
            {
                int move=TakeLiveNavigation();if(Math.Abs(move)==1)liveAction=move<0?1:2;
                if(LiveSubmitPressed){if(liveAction==1)ConfirmInventoryCraft();else{liveStage="palette";liveAction=1;}}return;
            }
            if(liveTab==0)
            {
                if(LiveSubmitPressed){if(approvedBagCategory)approvedBagCategory=false;else CraftSelectedBagItem();return;}
                int move=TakeLiveNavigation();
                if(approvedBagCategory)
                {if(Math.Abs(move)==1){liveFilter=move<0?0:1;livePick=0;}else if(move==2)approvedBagCategory=false;}
                else if(move==-2&&livePick<4)approvedBagCategory=true;
                else if(move!=0)livePick=Mathf.Clamp(livePick+(Math.Abs(move)==1?move:move/2*4),0,Math.Max(0,(liveFilter==1?LiveInks().Length:LiveWeapons().Length)-1));
            }
            else if(liveStage=="ink")
            {
                if(LiveSubmitPressed){ApplyInventoryInk();return;}
                int move=TakeLiveNavigation();if(move!=0)livePick=Mathf.Clamp(livePick+(Math.Abs(move)==1?move:move/2*3),0,Math.Max(0,LiveInks().Length-1));
            }
            else if(liveStage=="palette")
            {
                var weapon=LiveWeapon();if(weapon==null)return;
                if(inventoryRemovePressed&&liveAction==0)LiveTrade(nativeBag,()=>Sketch.Place(weapon.Id,liveSlot,null));
                if(LiveSubmitPressed)
                {
                    if(liveAction==1){liveStage="review";liveAction=1;}
                    else if(liveAction==2)BackApprovedCraft();else OpenLiveInkPicker();return;
                }
                int move=TakeLiveNavigation();
                if(liveAction>0){if(move==-2)liveAction=0;else if(Math.Abs(move)==1)liveAction=move<0?1:2;}
                else if(move==2)liveAction=1;
                else if(Math.Abs(move)==1)liveSlot=Mathf.Clamp(liveSlot+move,0,weapon.Slots.Count-1);
            }
            ScrollLiveDetail();
        }
    }
}
