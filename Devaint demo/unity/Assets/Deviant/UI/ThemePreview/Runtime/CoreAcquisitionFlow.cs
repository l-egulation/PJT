using System;
using System.Linq;
using UnityEngine;
using TMPro;
namespace Deviant.ThemePreview
{
    public sealed partial class ThemePreviewScreen
    {
        int acquiredWeaponId=-1,acquiredInkIndex=-1;
        bool HasAcquiredConfiguration=>acquiredWeaponId>=0||acquiredInkIndex>=0;
        void ResetAcquiredConfiguration(){acquiredWeaponId=acquiredInkIndex=-1;}

        // Caller opens the commerce bag first, preserving its receipt return route and inventory.
        // This method selects already-owned content and never grants or finalizes an acquisition.
        public void OpenAcquiredConfiguration(int weaponId,int inkIndex)
        {
            ResetAcquiredConfiguration();
            if(Inventory==null)return;
            CoreMode=true;ExtendedMode=false;coreClosed=false;bagTabs=false;
            CoreTab=1;CoreStage="list";CorePick=CoreSlot=CoreInk=action=0;detailScroll=0;
            if(weaponId>=0&&Inventory.BeginCraftById(weaponId))
            {
                acquiredWeaponId=weaponId;CorePick=Inventory.Sketches.FindIndex(w=>w.Id==weaponId);
                CoreStage="slots";
            }
            else if(weaponId<0&&Inventory.OwnedInkIndices.Contains(inkIndex))
            {
                acquiredInkIndex=inkIndex;CoreInk=Inventory.OwnedInkIndices.IndexOf(inkIndex);
                CoreStage="acquiredTargets";
            }
            Render();
        }
        bool HandleAcquiredKey(string key)
        {
            if(!HasAcquiredConfiguration||CoreTab!=1)return false;
            if(key=="Esc"&&(CoreStage=="slots"||CoreStage=="list"||CoreStage=="done"||CoreStage=="acquiredTargets"||CoreStage=="acquiredSlots"))
            {
                coreClosed=true;ResetAcquiredConfiguration();return true;
            }
            if(CoreStage=="acquiredTargets")
            {
                if(key!="Up"&&key!="Down"&&key!="Left"&&key!="Right"&&key!="F")return false;
                detailScroll=0;MoveGrid(key,Inventory.Sketches.Count,3,()=>{});
                if(key=="F"&&CorePick>=0&&CorePick<Inventory.Sketches.Count)
                {
                    Inventory.BeginCraft(CorePick);CoreSlot=Array.FindIndex(Inventory.Draft.Applied,i=>i<0);
                    if(CoreSlot<0)CoreSlot=0;
                    CoreStage="acquiredSlots";action=0;
                }
                return true;
            }
            if(CoreStage!="acquiredSlots")return false;
            var draft=Inventory.Draft;
            if(draft==null){CoreStage="acquiredTargets";return true;}
            if(key=="Left"||key=="Right"||key=="Up"||key=="Down"||key=="F")detailScroll=0;
            if(key=="Left") {if(action>0)action=1;else CoreSlot=Math.Max(0,CoreSlot-1);}
            if(key=="Right"){if(action==0)CoreSlot=Math.Min(draft.Applied.Length-1,CoreSlot+1);else action=2;}
            if(key=="Down")action=1;
            if(key=="Up")action=0;
            if(key=="F")
            {
                if(action==2){CoreStage="acquiredTargets";action=0;}
                else ApplyAcquiredInk();
            }
            return key=="Left"||key=="Right"||key=="Up"||key=="Down"||key=="F";
        }
        void ApplyAcquiredInk()
        {
            var draft=Inventory.Draft;
            if(draft==null||CoreSlot<0||CoreSlot>=draft.Applied.Length||draft.Applied[CoreSlot]>=0||!Inventory.OwnedInkIndices.Contains(acquiredInkIndex))return;
            Inventory.Apply(CoreSlot,acquiredInkIndex);
            CoreStage="slots";action=0;
        }
        void DrawAcquiredTargets()
        {
            Text(page,"AcquiredInkHeading",art.Package.inks[acquiredInkIndex].name+" · 부여할 스케치",138,174,850,42,28);
            if(Inventory.Sketches.Count==0)
            {
                Text(page,"NoAcquiredInkTargets","구성할 미확정 스케치가 없습니다.",138,350,850,80,32,muted);
                var c=DetailArea(1138,182,652,635);
                float y=Paragraph(c,"RegisteredInk",art.Package.inks[acquiredInkIndex].name,0,34);
                y=Paragraph(c,"RegisteredInkDescription",InkText(acquiredInkIndex),y+32,28);
                y=Paragraph(c,"AcquiredInkRules","잉크는 도감에 등록되어 있습니다.\n확정한 무기는 빈 칸도 바꿀 수 없습니다.",y+48,23,muted);EndDetail(c,y);
                Rule(1130,835,660);Button(page,"돌아가기",1130,862,650,true,()=>Handle("Esc"));return;
            }
            CorePick=Mathf.Clamp(CorePick,0,Inventory.Sketches.Count-1);DrawSketchGrid(true);
        }
        void DrawAcquiredSlots()
        {
            var w=Inventory.Draft;
            if(w==null){CoreStage="acquiredTargets";DrawAcquiredTargets();return;}
            WeaponArt(w.Art,138,182,125,110);Text(page,"CraftName",w.Name,303,193,600,45,34);
            Text(page,"CraftSkill",w.Skill,303,252,600,42,23,muted);
            Text(page,"PaletteHeading","잉크를 부여할 빈 칸",138,318,800,50,28);
            int first=Math.Max(0,CoreSlot-4);
            for(int i=first;i<Math.Min(first+5,w.Colors.Length);i++)
            {
                int slot=i;float x=138+(i-first)*169;bool selected=i==CoreSlot;float size=selected?122.5f:98;
                CorePalette(w,i,x+(145-size)/2,380,size,selected?1:.6f);
                var label=Text(page,"SlotLabel"+i,w.Applied[i]<0?"빈 칸":art.Package.inks[w.Applied[i]].name,x,482,145,60,24,selected?paper:muted);
                label.textWrappingMode=TextWrappingModes.Normal;label.alignment=TextAlignmentOptions.Top;label.overflowMode=TextOverflowModes.Ellipsis;
                if(selected)LabelWash(label,10,6);
                Click(Box("SlotHit"+i,page,x,365,145,205),()=>{CoreSlot=slot;action=0;Render();});
            }
            Fill(Box("PaletteRailTrack",page,138,538,828,8),new Color32(48,52,58,255));
            Fill(Box("PaletteRailThumb",page,138+first*828f/w.Colors.Length,538,828f*Mathf.Min(5,w.Colors.Length)/w.Colors.Length,8),new Color32(151,170,174,255));
            Text(page,"SlotPosition",$"{CoreSlot+1} / {w.Colors.Length}칸    ← →   옆으로 넘겨 보기",138,582,828,44,23,muted);
            Text(page,"AcquiredPaletteRules","구성 확정 전에는 임시 배치를 바꿀 수 있습니다.\n확정한 무기는 빈 칸을 포함해 바꿀 수 없습니다.",138,660,828,120,23,muted);
            var c=DetailArea(1138,182,652,635);bool match=art.Package.inks[acquiredInkIndex].color==w.Colors[CoreSlot];
            float y=Paragraph(c,"InkTitle",art.Package.inks[acquiredInkIndex].name,0,34);
            y=Paragraph(c,"InkDescription",InkText(acquiredInkIndex,match),y+32,30);
            y=Paragraph(c,"AppliedValue",AppliedSummary(acquiredInkIndex,match),y+28,27,gold);
            if(w.Applied[CoreSlot]>=0)y=Paragraph(c,"OccupiedSlotHint","이미 잉크가 놓인 칸입니다. 빈 칸을 선택하세요.",y+40,23,muted);
            EndDetail(c,y);Rule(1130,835,660);
            Button(page,"이 빈 칸에 적용",1130,862,300,action<2,()=>{ApplyAcquiredInk();Render();},w.Applied[CoreSlot]<0);
            Button(page,"다른 스케치 선택",1450,862,330,action==2,()=>{CoreStage="acquiredTargets";action=0;Render();});
        }
    }
}
