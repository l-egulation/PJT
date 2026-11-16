using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Deviant.ThemePreview
{
    public sealed partial class ThemePreviewScreen
    {
        public static bool LaunchCore;
        public bool CoreMode {get;private set;}
        public float DetailOffset=>detailScroll;
        public bool CoreClosed=>coreClosed;
        public CorePreviewState Inventory {get;private set;}
        public int CoreTab {get;private set;}
        public string CoreStage {get;private set;}="list";
        public int CorePick {get;private set;}
        public int CoreSlot {get;private set;}
        public int CoreInk {get;private set;}
        int bagCategory,target,action,createdId=-1;
        bool bagTabs,coreClosed;
        float detailScroll;
        ScrollRect descriptionScroll;
        readonly Color gold=new Color32(225,207,133,255);
        public void ResetCore(int inkCount=-1)
        {
            ResetAcquiredConfiguration();
            ExtendedMode=false;CoreMode=true; Inventory=new CorePreviewState(inkCount<0?art.Package.inks.Length:Math.Min(inkCount,art.Package.inks.Length));
            CoreTab=0;CoreStage="list";CorePick=CoreSlot=CoreInk=bagCategory=target=action=0;
            bagTabs=coreClosed=false;RenameOpen=false;renameInput=null;detailScroll=0;Render();
        }
        public void SwitchCore(int tab)
        {
            ResetAcquiredConfiguration();
            if(CoreStage=="done"||CoreStage=="destination") {CoreStage="list";createdId=-1;}
            CoreTab=(tab+3)%3;CoreStage=CoreTab==2?"target":"list";CorePick=0;detailScroll=0;bagTabs=false;Render();
        }
        void MoveGrid(string key,int count,int columns,Action up)
        {
            if(count==0){if(key=="Up")up();return;}
            int row=CorePick/columns;
            if(key=="Up"){if(row==0)up();else CorePick-=columns;}
            if(key=="Down"&&CorePick+columns<count)CorePick+=columns;
            if(key=="Left"&&CorePick%columns>0)CorePick--;
            if(key=="Right"&&CorePick%columns<columns-1&&CorePick+1<count)CorePick++;
        }
        public void HandleCore(string key)
        {
            LastKeyFrame=Frames;
            if(RenameOpen){if(key=="Esc")CloseRename(false);else if(key=="Enter")CloseRename(true);return;}
            if(coreClosed){if(key=="F"||key=="Esc"){coreClosed=false;Render();}return;}
            if(HandleAcquiredKey(key)){Render();return;}
            if(key=="W"||key=="S")
            {
                if(descriptionScroll!=null){Canvas.ForceUpdateCanvases();descriptionScroll.verticalNormalizedPosition=Mathf.Clamp01(descriptionScroll.verticalNormalizedPosition+(key=="W"?.18f:-.18f));detailScroll=1-descriptionScroll.verticalNormalizedPosition;}
                return;
            }
            if(key=="Q"||key=="E"){SwitchCore(CoreTab+(key=="Q"?-1:1));return;}
            detailScroll=0;
            if(CoreTab==0)
            {
                if(key=="Esc")coreClosed=true;
                else if(bagTabs){if(key=="Left"||key=="Right"){bagCategory=1-bagCategory;CorePick=0;}if(key=="F"||key=="Down")bagTabs=false;}
                else MoveGrid(key,bagCategory==0?Inventory.Sketches.Count:Inventory.InkCount,4,()=>bagTabs=true);
            }
            else if(CoreTab==1) CraftKey(key);
            else EquipmentKey(key);
            if(!RenameOpen)Render();
        }
        void CraftKey(string key)
        {
            switch(CoreStage)
            {
                case "list":
                    MoveGrid(key,Inventory.Sketches.Count,3,()=>{});
                    if(key=="F"&&Inventory.Sketches.Count>0){Inventory.BeginCraft(CorePick);CoreSlot=0;CoreStage="slots";}
                    if(key=="Esc")coreClosed=true;break;
                case "slots":
                    if(key=="Left")CoreSlot=Mathf.Max(0,CoreSlot-1);
                    if(key=="Right")CoreSlot=Mathf.Min(Inventory.Draft.Colors.Length-1,CoreSlot+1);
                    if(key=="F"&&Inventory.InkCount>0)CoreStage="ink";
                    if(key=="Down"){CoreStage="actions";action=0;}
                    if(key=="Esc")CoreStage="list";break;
                case "actions":
                    if(key=="Left")action=0;if(key=="Right")action=Inventory.Draft.Applied[CoreSlot]>=0?1:0;
                    if(key=="Up")CoreStage="slots";
                    if(key=="Down"){CoreStage="reviewButton";action=0;}
                    if(key=="Esc")CoreStage="slots";
                    if(key=="F"){if(action==1){Inventory.Remove(CoreSlot);CoreStage="slots";}else if(Inventory.InkCount>0)CoreStage="ink";}break;
                case "reviewButton":
                    if(key=="Up"||key=="Esc")CoreStage="slots";
                    if(key=="Left")action=0;if(key=="Right")action=1;
                    if(key=="F"){CoreStage=action==0?"review":"list";action=0;}break;
                case "ink":
                    if(key=="Left")CoreInk=Mathf.Max(0,CoreInk-1);
                    if(key=="Right")CoreInk=Mathf.Min(Inventory.InkCount-1,CoreInk+1);
                    if(key=="Down"){CoreStage="inkAction";action=0;}
                    if(key=="F"){Inventory.Apply(CoreSlot,Inventory.InkAt(CoreInk));CoreStage="slots";}
                    if(key=="Esc")CoreStage="slots";break;
                case "inkAction":
                    if(key=="Left")action=0;if(key=="Right")action=1;
                    if(key=="Up")CoreStage="ink";
                    if(key=="Esc")CoreStage="slots";
                    if(key=="F"){if(action==0)Inventory.Apply(CoreSlot,Inventory.InkAt(CoreInk));CoreStage="slots";}break;
                case "review":
                    if(key=="Left")action=0;if(key=="Right")action=1;
                    if(key=="F"&&action==1){CoreStage="slots";break;}
                    if(key=="Up"||key=="Esc")CoreStage="slots";
                    if(key=="F"){var w=Inventory.FinalizeCraft();if(w!=null){createdId=w.Id;CoreStage="done";action=0;CorePick=0;}}break;
                case "done":
                    if(key=="Left")action=0;if(key=="Right")action=1;
                    if(key=="Esc"||(key=="F"&&action==1))CoreStage="list";
                    else if(key=="F"){CoreStage="destination";target=0;}break;
                case "destination":
                    if(key=="Left")target=0;if(key=="Right")target=1;
                    if(key=="Esc")CoreStage="done";
                    if(key=="F")CoreStage="destinationAction";break;
                case "destinationAction":
                    if(key=="Up"||key=="Esc")CoreStage="destination";
                    if(key=="F"&&Inventory.Equip(target,createdId))CoreStage="list";break;
            }
        }
        void EquipmentKey(string key)
        {
            if(CoreStage=="target")
            {
                if(key=="Left")target=0;if(key=="Right")target=1;
                if((key=="F"||key=="Down")&&Inventory.Weapons.Count>0)CoreStage="weapons";
                if(key=="Esc")coreClosed=true;
            }
            else if(CoreStage=="weapons")
            {
                int count=Inventory.Weapons.Count;
                if(key=="Down"&&CorePick+3>=count){CoreStage="equipAction";action=0;}
                else MoveGrid(key,count,3,()=>CoreStage="target");
                if(key=="F"&&count>0)Inventory.Equip(target,Inventory.Weapons[CorePick].Id);
                if(key=="Esc")CoreStage="target";
            }
            else if(CoreStage=="equipAction")
            {
                if(key=="Left")action=0;if(key=="Right")action=1;
                if(key=="Down"&&Inventory.Weapons.Count>0)CoreStage="renameButton";
                if(key=="Up")CoreStage="weapons";
                if(key=="Esc")CoreStage="target";
                if(key=="F"){if(action==1)Inventory.Unequip(target);else if(Inventory.Weapons.Count>0)Inventory.Equip(target,Inventory.Weapons[CorePick].Id);}
            }
            else if(CoreStage=="renameButton")
            {
                if(key=="Up"||key=="Esc")CoreStage="equipAction";
                if(key=="F")BeginRename();
            }
        }
        void Rule(float x,float y,float w)=>Fill(Box("Rule",page,x,y,w,1),new Color32(82,90,96,255));
        void Click(RectTransform rt,Action callback)
        {
            var im=rt.gameObject.AddComponent<Image>();im.color=Color.clear;
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=im;b.transition=Selectable.Transition.None;b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>callback());
        }
        void CoreBottle(int index,float x,float y,float h,float alpha)
        {
            var ink=art.Package.inks[index];var v=ink.view;float scale=h/v[3];
            Art(page,ink.bottle,x,y,v[2]*scale,h,alpha);
            foreach(var pair in new[]{(ink.effect,ink.label),(ink.condition,ink.badge)})
            {var m=pair.Item2;Art(page,pair.Item1,x+(m.center[0]-v[0]-m.size/2)*scale,y+(m.center[1]-v[1]-m.size/2)*scale,m.size*scale,m.size*scale,alpha);}
        }
        void CorePalette(PreviewWeapon weapon,int slot,float x,float y,float width,float alpha,Transform parent=null)
        {
            parent=parent??page;float h=width*155/220;Art(parent,"palette/bases/"+weapon.Colors[slot],x,y,width,h,alpha);
            int i=weapon.Applied[slot];if(i<0)return;
            var ink=art.Package.inks[i];Art(parent,"palette/paint/"+ink.color,x,y,width,h,alpha);
            Art(parent,ink.effect,x+70*width/220,y+38*h/155,80*width/220,80*h/155,alpha);
        }
        void Durability(Transform parent,PreviewWeapon w,float x,float y,float width)
        {
            Fill(Box("DurabilityTrack",parent,x,y+12,width,width>150?10:7),new Color32(70,78,84,255));
            Fill(Box("DurabilityFill",parent,x,y+12,width*w.Durability/100,width>150?10:7),new Color32(162,195,191,255));
            Text(parent,"DurabilityPercent",$"{w.Durability:0}%",x+width+(width>150?22:12),y-5,width>150?95:60,42,width>150?28:23,muted);
        }
        // Each text block has its measured height inside a masked ScrollRect; no truncation to fake scrolling.
        RectTransform DetailArea(float x=958,float y=182,float w=832,float h=747)
        {
            var viewport=Box("DescriptionViewport",page,x,y,w,h);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Box("DescriptionContent",viewport,0,0,w-24,1);
            descriptionScroll=viewport.gameObject.AddComponent<ScrollRect>();descriptionScroll.viewport=viewport;descriptionScroll.content=content;
            descriptionScroll.horizontal=false;descriptionScroll.vertical=true;descriptionScroll.movementType=ScrollRect.MovementType.Clamped;descriptionScroll.inertia=false;
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            return content;
        }
        float Paragraph(Transform parent,string label,string value,float y,float size=27,Color? color=null)
        {
            var t=Text(parent,label,value,0,y,((RectTransform)parent).rect.width,100,size,color);t.lineSpacing=12;
            float h=t.GetPreferredValues(value,((RectTransform)parent).rect.width,0).y+12;
            t.rectTransform.sizeDelta=new Vector2(t.rectTransform.sizeDelta.x,h);return y+h;
        }
        void EndDetail(RectTransform content,float height)
        {
            content.sizeDelta=new Vector2(content.sizeDelta.x,height);Canvas.ForceUpdateCanvases();descriptionScroll.verticalNormalizedPosition=1-detailScroll;
            if(height>descriptionScroll.viewport.rect.height)
                {
                    // The shared footer supplies W/S help; do not repeat it over content.
                    var vp=descriptionScroll.viewport;float vh=vp.rect.height,thumb=Mathf.Max(35,vh*vh/height);
                    Fill(Box("ScrollTrack",vp,vp.rect.width-7,0,7,vh),new Color32(60,70,76,255));
                    var mark=Box("ScrollThumb",vp,vp.rect.width-7,detailScroll*(vh-thumb),7,thumb);Fill(mark,new Color32(157,180,187,255));
                    descriptionScroll.onValueChanged.AddListener(v=>{mark.anchoredPosition=new Vector2(mark.anchoredPosition.x,-(1-v.y)*(vh-thumb));});
                }
        }
        string InkText(int index,bool? match=null)=>CoreInkText.Description(art.Package.inks[index].description,match==true);
        string AppliedSummary(int index,bool match)=>(match?"색상 일치 ":"기본 ")+CoreInkText.Value(art.Package.inks[index].description,match)+" 적용";
        void WeaponArt(string id,float x,float y,float w,float h,float alpha=1)
        {
            var v=art.Find("ui-assets-v2/"+id).view;float scale=Mathf.Min(w/v[2],h/v[3]);
            Art(page,"ui-assets-v2/"+id,x+(w-v[2]*scale)/2,y+(h-v[3]*scale)/2,v[2]*scale,v[3]*scale,alpha);
        }
        void InkDetail(int index,bool? match=null)
        {
            var c=DetailArea(1138,583,641,234);float y=Paragraph(c,"InkTitle",art.Package.inks[index].name,0,34);
            y=Paragraph(c,"InkDescription",InkText(index,match),y+28,30);
            if(match.HasValue)y=Paragraph(c,"AppliedValue",AppliedSummary(index,match.Value),y+28,28,gold);
            EndDetail(c,y);
        }
        void DetailRule(Transform parent,float y)
        {Fill(Box("SectionDivider",parent,0,y,((RectTransform)parent).rect.width,1),new Color32(199,208,219,48));}
        float DetailHeading(Transform parent,string name,string title,float y)
        {DetailRule(parent,y);return Paragraph(parent,name,title,y+33,28,new Color32(214,220,225,255))+28;}
        void WeaponDetail(PreviewWeapon w,bool durability,bool onLeft=false)
        {
            bool bag=CoreTab==0,craft=CoreTab==1;
            var c=DetailArea(onLeft?138:craft?1138:958,bag?277:182,onLeft?852:craft?652:832,bag?535:craft?633:747);
            if(w==null){EndDetail(c,Paragraph(c,"Empty","장착된 무기 없음",0));return;}
            var v=art.Find("ui-assets-v2/"+w.Art).view;float scale=Mathf.Min(150/v[2],140/v[3]);
            Art(c,"ui-assets-v2/"+w.Art,(150-v[2]*scale)/2,(140-v[3]*scale)/2,v[2]*scale,v[3]*scale);
            Text(c,"WeaponName",w.Name,190,26,Mathf.Max(200,c.rect.width-190),45,34);
            Text(c,"WeaponSkillSummary",w.Skill,190,85,c.rect.width-190,42,23,muted);
            float y=180;
            if(durability){Durability(c,w,190,125,205);y=228;
                if(CoreTab==2){var rename=Text(c,"RenameWeaponAction","이름 변경",190,172,160,28,20,CoreStage=="renameButton"?paper:muted);
                    if(CoreStage=="renameButton")LabelWash(rename,10,5);
                    Click(Box("RenameWeaponHit",c,182,164,180,40),()=>BeginRename(w.Id));}}

            bool hasEffects=w.Applied.Any(i=>i>=0);
            if(!hasEffects&&!durability)
            {
                y=DetailHeading(c,"AttackHeader","기본 공격",y);y=Paragraph(c,"Attack",w.Attack,y)+48;
                y=DetailHeading(c,"SkillHeader","스킬 · "+w.Skill,y);y=Paragraph(c,"Skill",w.SkillDescription,y)+48;
            }
            y=DetailHeading(c,"PaletteHeader","팔레트",y);
            for(int slot=0;slot<w.Colors.Length;slot++)CorePalette(w,slot,slot*82,y,70,1,c);y+=100;
            y=DetailHeading(c,"EffectsHeader","적용 효과",y);
            if(hasEffects)
            {
                for(int i=0;i<w.Applied.Length;i++)if(w.Applied[i]>=0)
                {bool match=art.Package.inks[w.Applied[i]].color==w.Colors[i];y=Paragraph(c,"Effect"+i,InkText(w.Applied[i],match),y,30)+48;}
                y=DetailHeading(c,"AttackHeader","기본 공격",y);y=Paragraph(c,"Attack",w.Attack,y)+48;
                y=DetailHeading(c,"SkillHeader","스킬 · "+w.Skill,y);y=Paragraph(c,"Skill",w.SkillDescription,y)+48;
            }
            else {y=Paragraph(c,"NoEffects","적용한 잉크가 없습니다.",y,23,muted);
                if(durability){y=DetailHeading(c,"AttackHeader","기본 공격",y+48);y=Paragraph(c,"Attack",w.Attack,y)+48;y=DetailHeading(c,"SkillHeader","스킬 · "+w.Skill,y);y=Paragraph(c,"Skill",w.SkillDescription,y)+48;}}
            EndDetail(c,y+28);
        }
        void DrawCore()
        {
            descriptionScroll=null;
            if(coreClosed){Text(page,"Closed","독립 UI 닫힘",660,400,800,80,44);Button(page,"다시 열기",720,530,460,true,()=>HandleCore("F"));return;}
            Keycap(page,"Q",130,78.75f,35.156f,33);Keycap(page,"E",641.961f,78.75f,32.984f,33);
            string[] tabs={"가방","제작","장비"};
            for(int i=0;i<3;i++)
            {
                int tab=i;float x=235+i*139.6f;bool selected=CoreTab==i;
                var label=Text(page,"CoreTab"+i,tabs[i],x-(selected?4:0),selected?72:76,100,55,selected?41.44f:37,selected?new Color32(255,246,230,255):new Color(paper.r,paper.g,paper.b,.56f));
                if(selected)LabelWash(label,14,10);
                Click(Box("TabHit"+i,page,x-12,60,110,70),()=>SwitchCore(tab));
            }
            Text(page,"Pause","일시정지",1718,80,90,38,23,muted);
            float divider=CoreTab==1?1060:880;float top=CoreTab==0?269:174;
            Fill(Box("ColumnDivider",page,divider,top,1,929-top),new Color32(210,216,222,48));
            if(CoreTab==0)DrawBag();else if(CoreTab==1)DrawCraft();else DrawEquipment();
            DrawKeyboardFooter();
            if(RenameOpen)DrawRename();
        }
        void DrawSketchGrid(bool craft)
        {
            int columns=craft?3:4,pageSize=craft?6:8,start=(CorePick/pageSize)*pageSize;
            if(Inventory.Sketches.Count==0){Text(page,"NoSketch","보관한 스케치 없음",145,360,800,80,34,muted);return;}
            for(int i=start;i<Math.Min(start+pageSize,Inventory.Sketches.Count);i++)
            {
                int index=i;float cell=craft?252:145.75f;
                float x=138+(i-start)%columns*(craft?278:164),y=(craft?220:293)+(i-start)/columns*(craft?290:222);
                bool selected=i==CorePick&&(!bagTabs||craft);float size=craft?(selected?126:96):(selected?129.6f:96);
                var w=Inventory.Sketches[i];WeaponArt(w.Art,x+(cell-size)/2,y+32-(size-96)/2,size,size,selected?1:.45f);
                if(craft){var name=Text(page,"SketchName"+i,w.Name,x,y+164,cell,48,26,selected?paper:muted);name.alignment=TextAlignmentOptions.Top;if(selected)LabelWash(name);
                    var skill=Text(page,"SketchSkill"+i,w.Skill,x,y+218,cell,40,21,muted);skill.alignment=TextAlignmentOptions.Top;}
                Click(Box("SketchHit"+i,page,x,y,cell,craft?268:174),()=>{CorePick=index;bagTabs=false;if(craft)HandleCore("F");else Render();});
            }
            if(craft)Rule(130,835,860);
            Text(page,"SketchCount",$"보유 {Inventory.Sketches.Count}개 · ↕ 스크롤",130,873,650,48,23,muted);
            WeaponDetail(Inventory.Sketches[CorePick],false);
            if(craft){Rule(1130,835,660);Button(page,CoreStage=="acquiredTargets"?"이 스케치에 잉크 부여":"이 스케치로 제작",1130,862,650,true,()=>HandleCore("F"));}
        }
        void DrawBag()
        {
            Rule(130,832,680);Rule(950,832,840);Text(page,"BagNote","정보 확인 · 잉크 적용은 제작에서",950,873,840,40,23,muted);
            for(int i=0;i<2;i++){int category=i;float x=140+i*154;var categoryLabel=Text(page,"Category"+i,i==0?"스케치":"잉크",x,183,120,48,bagTabs&&bagCategory==i?36:33,bagCategory==i?paper:muted);if(bagCategory==i)LabelWash(categoryLabel);Click(Box("CategoryHit"+i,page,x-10,174,130,60),()=>{bagCategory=category;CorePick=0;bagTabs=true;Render();});}
            if(bagCategory==0){CorePick=Mathf.Clamp(CorePick,0,Math.Max(0,Inventory.Sketches.Count-1));DrawSketchGrid(false);return;}
            if(Inventory.InkCount==0){Text(page,"EmptyInk","보유 잉크 없음",145,350,700,60,34);return;}
            int start=(CorePick/8)*8;
            for(int i=start;i<Math.Min(start+8,Inventory.InkCount);i++)
            {int index=i;float x=155+(i-start)%4*164,y=293+(i-start)/4*222;bool selected=i==CorePick&&!bagTabs;CoreBottle(Inventory.InkAt(i),x+(116-(selected?145:116))*.5f,y+(145-(selected?145:116))*.5f,selected?145:116,selected?1:.48f);Click(Box("InkHit"+i,page,x-10,y,160,155),()=>{CorePick=index;bagTabs=false;detailScroll=0;Render();});}
            Text(page,"InkCount",$"{CorePick+1} / {Inventory.InkCount}   ·   ↑↓ 목록 이동",130,873,680,48,23,muted);
            var c=DetailArea(958,277,832,535);float h=Paragraph(c,"InkTitle",art.Package.inks[Inventory.InkAt(CorePick)].name,0,40);h=Paragraph(c,"InkDescription",InkText(Inventory.InkAt(CorePick)),h+50);EndDetail(c,h);
        }
        void DrawCraft()
        {
            if(CoreStage=="acquiredTargets"){DrawAcquiredTargets();return;}
            if(CoreStage=="acquiredSlots"){DrawAcquiredSlots();return;}
            if(CoreStage=="list"){DrawSketchGrid(true);return;}
            if(CoreStage=="done"||CoreStage.StartsWith("destination")){DrawCraftDone();return;}
            var w=Inventory.Draft;if(w==null){CoreStage="list";DrawCraft();return;}
            WeaponArt(w.Art,138,182,125,110);Text(page,"CraftName",w.Name,303,193,600,45,34);Text(page,"CraftSkill",w.Skill,303,252,600,42,23,muted);
            bool inkActive=CoreStage=="ink"||CoreStage=="inkAction";
            bool paletteActive=CoreStage=="slots"||CoreStage=="actions"||CoreStage=="reviewButton";
            Text(page,"PaletteHeading","팔레트",138,318,800,50,28,paletteActive?paper:muted);
            int first=Math.Max(0,CoreSlot-4);float railY=538;
            for(int i=first;i<Math.Min(first+5,w.Colors.Length);i++)
            {int slot=i;float x=138+(i-first)*169;bool sel=i==CoreSlot;float size=sel?122.5f:98;CorePalette(w,i,x+(145-size)/2,380,size,!paletteActive?.45f:sel?1:.6f);
             string label=w.Applied[i]<0?"빈 칸":art.Package.inks[w.Applied[i]].name;
             var t=Text(page,"SlotLabel"+i,label,x,482,145,60,24,sel?paper:muted);t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.Top;t.overflowMode=TextOverflowModes.Ellipsis;if(sel&&paletteActive)LabelWash(t,10,6);
             Click(Box("SlotHit"+i,page,x,365,145,205),()=>{CoreSlot=slot;CoreStage="slots";Render();});}
            Text(page,"SlotPosition",$"{CoreSlot+1} / {w.Colors.Length}칸    ← →   옆으로 넘겨 보기",138,railY+44,828,44,23,muted);
            if(paletteActive)Button(page,w.Applied[CoreSlot]>=0?"잉크 다시 선택":"잉크 선택",138,railY+114,335,(CoreStage=="actions"&&action==0)||CoreStage=="slots",()=>{CoreStage="ink";Render();},Inventory.InkCount>0);
            if(paletteActive&&w.Applied[CoreSlot]>=0)Button(page,"임시 적용 해제",500,railY+114,420,CoreStage=="actions"&&action==1,()=>{Inventory.Remove(CoreSlot);CoreStage="slots";Render();});
            if(paletteActive){Rule(130,835,860);Button(page,"제작 내용 확인",130,862,280,CoreStage=="reviewButton"&&action==0,()=>{CoreStage="review";action=0;Render();});
            Button(page,"돌아가기",430,862,250,CoreStage=="reviewButton"&&action==1,()=>{CoreStage="list";Render();});}
            else Text(page,"PaletteRules","모든 색의 잉크를 적용할 수 있습니다.\n같은 색을 넣으면 효과가 증가합니다.",138,railY+108,828,120,23,muted);

            if(CoreStage=="review")
            {
                var c=DetailArea(1138,182,652,635);bool empty=w.Applied.All(i=>i<0);
                float y=Paragraph(c,"Review",empty?"빈 팔레트로 제작":"제작할 무기",0,34)+32;
                y=Paragraph(c,"ReviewEffects","적용 효과",y,28,muted)+28;
                if(empty)y=Paragraph(c,"EmptyEffects","적용한 잉크가 없습니다.",y,27,muted)+48;
                for(int i=0;i<w.Applied.Length;i++)if(w.Applied[i]>=0){y=Paragraph(c,"ReviewEffectName"+i,art.Package.inks[w.Applied[i]].name,y,27)+22;y=Paragraph(c,"ReviewEffect"+i,InkText(w.Applied[i],art.Package.inks[w.Applied[i]].color==w.Colors[i]),y,30)+48;}
                y=Paragraph(c,"ReviewCopy","확정한 잉크는 바꿀 수 없습니다.\n빈 칸도 확정 후에는 채울 수 없습니다.",y,23,muted);EndDetail(c,y);
                Rule(1130,835,660);Button(page,"제작 확정",1130,862,300,action==0,()=>{action=0;HandleCore("F");});
                Button(page,"돌아가기",1450,862,300,action==1,()=>{CoreStage="slots";Render();});return;
            }
            Fill(Box("PaletteRailTrack",page,138,railY,828,8),new Color32(48,52,58,255));
            Fill(Box("PaletteRailThumb",page,138+first*828f/w.Colors.Length,railY,828f*Mathf.Min(5,w.Colors.Length)/w.Colors.Length,8),new Color32(151,170,174,255));
            if(!inkActive)
            {
                var c=DetailArea(1138,182,652,747);
                float y=Paragraph(c,"SelectedPalette","선택한 팔레트",0,34,muted);
                int applied=w.Applied[CoreSlot];
                if(applied>=0){y=Paragraph(c,"InkTitle",art.Package.inks[applied].name,y+34,30);y=Paragraph(c,"InkDescription",InkText(applied,art.Package.inks[applied].color==w.Colors[CoreSlot]),y+28,30);y=Paragraph(c,"AppliedValue",AppliedSummary(applied,art.Package.inks[applied].color==w.Colors[CoreSlot]),y+28,27,gold);}
                else y=Paragraph(c,"SelectedPaletteCopy","잉크를 넣지 않은 칸입니다.\n선택한 칸의 잉크를 확인합니다.",y+28,23,muted);
                EndDetail(c,y);return;
            }
            Text(page,"InkHeading","적용할 잉크",1138,182,652,50,34,inkActive?gold:muted);
            if(Inventory.InkCount==0){Text(page,"EmptyInk","보유 잉크 없음",1100,350,650,90,34,muted);return;}
            int inkFirst=Math.Max(0,CoreInk-3);
            for(int i=inkFirst;i<Math.Min(inkFirst+4,Inventory.InkCount);i++)
            {int ink=i;float x=1160+(i-inkFirst)*156;bool sel=inkActive&&CoreInk==i;CoreBottle(Inventory.InkAt(i),x,sel?282:300,sel?170:136,sel?1:inkActive?.55f:.35f);Click(Box("CraftInkHit"+i,page,x-10,270,145,215),()=>{CoreInk=ink;CoreStage="ink";detailScroll=0;Render();});}
            Fill(Box("InkRailTrack",page,1138,503,617,8),new Color32(48,52,58,255));Fill(Box("InkRailThumb",page,1138+inkFirst*617f/Inventory.InkCount,503,617f*Mathf.Min(4,Inventory.InkCount)/Inventory.InkCount,8),new Color32(151,170,174,255));Rule(1138,550,617);
            int shown=inkActive?Inventory.InkAt(CoreInk):w.Applied[CoreSlot];
            if(shown>=0)InkDetail(shown,art.Package.inks[shown].color==w.Colors[CoreSlot]);
            else Text(page,"EmptySlotDetail","빈 칸\n\nF로 적용할 잉크를 선택하세요.",1138,580,617,180,32,muted);
            if(inkActive){Rule(1130,835,660);Button(page,"이 칸에 적용",1130,862,300,CoreStage=="ink"||action==0,()=>{Inventory.Apply(CoreSlot,Inventory.InkAt(CoreInk));CoreStage="slots";Render();});Button(page,"돌아가기",1450,862,280,CoreStage=="inkAction"&&action==1,()=>{CoreStage="slots";Render();});}
        }
        public void EquipCreated(){if(Inventory.Equip(target,createdId))CoreStage="list";Render();}
        void DrawCraftDone()
        {
            var w=Inventory.Weapons.First(x=>x.Id==createdId);WeaponDetail(w,true,true);
            bool done=CoreStage=="done";Text(page,"CraftDone",done?"제작 완료":"장착할 자리",1138,182,652,60,34);
            Rule(1130,835,660);
            if(done)
            {Text(page,"CraftDoneCopy","완성된 무기를 어떻게 할까요?",1138,276,652,70,27,muted);
             Button(page,"장착하기",1130,862,300,action==0,()=>{CoreStage="destination";Render();});Button(page,"보관하기",1450,862,300,action==1,()=>{CoreStage="list";Render();});}
            else
            {for(int i=0;i<2;i++){int slot=i;float x=1138+i*278;var t=Text(page,"Destination"+i,"무기 "+(i+1),x,290,230,45,32,target==i?paper:muted);t.alignment=TextAlignmentOptions.Top;if(target==i)LabelWash(t);
             var n=Text(page,"DestinationName"+i,Inventory.EquippedWeapon(i)?.Name??"빈 자리",x,345,230,42,23,muted);n.alignment=TextAlignmentOptions.Top;n.textWrappingMode=TextWrappingModes.NoWrap;n.overflowMode=TextOverflowModes.Ellipsis;
             Click(Box("DestinationHit"+i,page,x,275,230,120),()=>{target=slot;CoreStage="destinationAction";Render();});}
             Button(page,Inventory.Cooldown>0?$"장착 대기 {Inventory.Cooldown:0}초":"선택한 자리에 장착",1130,862,410,CoreStage=="destinationAction",()=>EquipCreated(),Inventory.CanEquip(target,createdId));
             Button(page,"돌아가기",1550,862,240,false,()=>{CoreStage="done";Render();});}
        }
        void DrawEquipment()
        {
            Rule(130,354,680);Rule(130,834,680);
            Text(page,"TargetHeading",CoreStage=="target"?"장착할 자리 선택":"선택한 자리 · 무기 "+(target+1),130,174,680,45,30,CoreStage=="target"?gold:muted);
            for(int i=0;i<2;i++)
            {int slot=i;float x=130+i*278;var targetLabel=Text(page,"Target"+i,"무기 "+(i+1),x,245,230,45,CoreStage=="target"&&target==i?38:32,CoreStage=="target"?paper:muted);targetLabel.alignment=TextAlignmentOptions.Top;if(target==i)LabelWash(targetLabel);
             var t=Text(page,"EquippedName"+i,Inventory.EquippedWeapon(i)?.Name??"빈 자리",x,287,230,40,23,muted);t.alignment=TextAlignmentOptions.Top;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
             Click(Box("TargetHit"+i,page,x,231,230,100),()=>{target=slot;CoreStage="target";Render();});}
            Text(page,"WeaponListHeading","완성된 무기",138,379,650,45,28,CoreStage=="target"?muted:gold);
            int count=Inventory.Weapons.Count;CorePick=Mathf.Clamp(CorePick,0,Math.Max(0,count-1));int first=CorePick/3*3;
            for(int i=first;i<Math.Min(first+3,count);i++)
            {int index=i;var w=Inventory.Weapons[i];float x=138+(i-first)%3*223,y=454+(i-first)/3*280;bool selected=CoreStage!="target"&&CorePick==i;
             WeaponArt(w.Art,x+45,y,111,111,CoreStage=="target"?.42f:selected?1:.55f);
             var t=Text(page,"WeaponCell"+i,w.Name,x,y+142,201,48,selected?28:25,CoreStage=="target"?new Color(muted.r,muted.g,muted.b,.42f):selected?paper:muted);t.alignment=TextAlignmentOptions.Top;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;if(selected)LabelWash(t);
             Durability(page,w,x+27,y+205,95);if(Inventory.Equipped.Contains(w.Id)){var badge=Text(page,"EquippedBadge"+i,"장착중",x,y+250,201,35,20,gold);badge.alignment=TextAlignmentOptions.Top;}
             Click(Box("WeaponHit"+i,page,x,y,201,280),()=>{CorePick=index;CoreStage="weapons";detailScroll=0;Render();});}
            if(count==0)Text(page,"NoWeapons","완성된 무기 없음",145,570,800,80,34,muted);
            var chosen=count>0?Inventory.Weapons[CorePick]:null;WeaponDetail(CoreStage=="target"?Inventory.EquippedWeapon(target):chosen,true);
            string label=chosen==null?"장착할 무기 없음":Inventory.Equipped[target]==chosen.Id?"장착중":Inventory.Equipped[1-target]==chosen.Id?"자리 교환":Inventory.Cooldown>0?$"장착 대기 {Inventory.Cooldown:0}초":"장착";
            Button(page,CoreStage=="target"?"이 자리 선택":label,130,862,300,CoreStage=="target"||(CoreStage=="equipAction"&&action==0),()=>{if(CoreStage=="target")HandleCore("F");else {if(chosen!=null)Inventory.Equip(target,chosen.Id);Render();}},CoreStage=="target"?count>0:chosen!=null&&Inventory.CanEquip(target,chosen.Id));
            Button(page,Inventory.Cooldown>0?$"해제 대기 {Inventory.Cooldown:0}초":"해제",450,862,350,CoreStage=="equipAction"&&action==1,()=>{Inventory.Unequip(target);Render();},Inventory.Cooldown==0&&Inventory.Equipped[target]>=0);
        }

        public bool RenameOpen { get; private set; }
        TMP_InputField renameInput;
        bool renameComposing;
        Keyboard renameKeyboard;
        void RenameComposition(UnityEngine.InputSystem.LowLevel.IMECompositionString value){renameComposing=value.Count>0;}
        PreviewWeapon renameWeapon;
        string renameReturnStage;
        public void BeginRename(int weaponId=-1)
        {
            if(Inventory.Weapons.Count==0)return;
            renameWeapon=weaponId<0?Inventory.Weapons[Mathf.Clamp(CorePick,0,Inventory.Weapons.Count-1)]:Inventory.Weapons.FirstOrDefault(w=>w.Id==weaponId);
            if(renameWeapon==null)return;
            renameReturnStage=CoreStage;RenameOpen=true;renameComposing=false;renameKeyboard=Keyboard.current;
            if(renameKeyboard!=null)renameKeyboard.onIMECompositionChange+=RenameComposition;Render();
        }
        void UpdateRename(Keyboard keyboard)
        {
            if(keyboard.escapeKey.wasPressedThisFrame)CloseRename(false);
            else if((keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame)&&!renameComposing)CloseRename(true);
        }
        public void CloseRename(bool save)
        {
            if(!RenameOpen)return;
            if(save&&!Inventory.Rename(renameWeapon.Id,renameInput.text))return;
            if(renameKeyboard!=null)renameKeyboard.onIMECompositionChange-=RenameComposition;renameKeyboard=null;
            RenameOpen=false;renameInput=null;CoreStage=renameReturnStage;Render();
        }
        void DrawRename()
        {
            var dim=Box("RenameModalBackdrop",page,0,0,1920,1080);var block=Fill(dim,new Color32(16,19,23,181));block.raycastTarget=true;
            Frame(736,358.9f,448,362.2f);
            Text(page,"RenameTitle","무기 이름 변경",784,402,352,38,23);
            Text(page,"RenameHint",renameWeapon.Name+" · 최대 20자",784,465,352,28,16,muted);
            var host=Box("WeaponNameInput",page,784,507,352,53);Fill(host,new Color32(20,24,28,145)).raycastTarget=true;
            Fill(Box("InputUnderline",host,0,52,352,1),new Color32(172,204,211,255));
            var value=Text(host,"InputValue",renameWeapon.Name,10,0,332,53,23);value.alignment=TextAlignmentOptions.MidlineLeft;
            value.textWrappingMode=TextWrappingModes.NoWrap;
            renameInput=host.gameObject.AddComponent<TMP_InputField>();renameInput.textViewport=host;renameInput.textComponent=value;
            renameInput.lineType=TMP_InputField.LineType.SingleLine;renameInput.characterLimit=20;renameInput.richText=false;renameInput.text=renameWeapon.Name;
            var counter=Text(page,"RenameCharacterCount",renameInput.text.Length+" / 20",1040,580,96,28,16,muted);counter.alignment=TextAlignmentOptions.TopRight;
            renameInput.onValueChanged.AddListener(v=>counter.text=v.Length+" / 20");
            Rule(784,619,352);
            Keycap(page,"Enter",788,650,40,22,13);Text(page,"RenameSave","저장",838,646,60,30,23);
            Keycap(page,"Esc",917,650,33,22,13);Text(page,"RenameCancel","취소",960,646,60,30,23);
            Click(Box("RenameSaveHit",page,784,640,100,44),()=>CloseRename(true));Click(Box("RenameCancelHit",page,910,640,110,44),()=>CloseRename(false));
            renameInput.Select();renameInput.ActivateInputField();
        }
    }
}
