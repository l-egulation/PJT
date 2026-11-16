using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class DummyTrainingRoom
    {
        [Serializable] public sealed class PartInfo { public string id, name, color, description, limit, requiresStamp, unityNote; public float cooldown; public bool duplicate; }
        [Serializable] public sealed class PartList { public PartInfo[] parts; }
        PartInfo[] parts;

        int weaponSlot, tab, colorFilter, selectedCandidateSlot, selectedPart;
        Vector2 scroll;
        GUIStyle text, small, heading, button, chosen, title;
        Font trainingFont;
        Texture2D buttonTexture, hoverTexture, selectedTexture;
        readonly List<Rect> labelRects = new List<Rect>();
        static readonly Color Paper = new Color32(28,37,39,255), Panel = new Color32(40,52,53,255), Ink = new Color32(235,234,215,255), Muted = new Color32(172,191,181,255), Gold = new Color32(221,184,109,255);
        static readonly string[] StatNames = BuildStatNames();
        static string[] BuildStatNames()
        {
            // Read the same equipment model used by combat; balance values are never copied here.
            var preview=new WeaponState();
            var names=new[]{"빈 칸","기본 피해","특수 피해","기본 공속","특수 쿨감"};
            for(int kind=1;kind<names.Length;kind++)
            {
                preview.StatSlots[0]=kind;
                float value=kind==1?preview.BasicBonus:kind==2?preview.SpecialBonus:kind==3?preview.Speed-1:preview.Reduction;
                names[kind]+=" "+(kind==4?"":"+")+(value*100).ToString("0.##")+"%";
            }
            return names;
        }
        static readonly string[] Colors = {"전체", "빨강", "노랑", "초록", "파랑", "보라"};
        static readonly string[] ColorIds = {"", "red", "yellow", "green", "blue", "purple"};
        void Styles()
        {
            if(!buttonTexture)
            {
                buttonTexture=UiTexture(new Color32(57,72,71,255));
                hoverTexture=UiTexture(new Color32(76,92,86,255));
                selectedTexture=UiTexture(new Color32(83,73,47,255));
            }
            text = new GUIStyle(GUI.skin.label) { font=trainingFont,fontSize = 12, wordWrap = true, padding=new RectOffset(0,0,0,0),contentOffset=Vector2.zero,normal = { textColor = Ink } };
            small = new GUIStyle(text) {fontSize = 10, normal = {textColor = Muted}};
            heading = new GUIStyle(text) {fontSize = 16, fontStyle = FontStyle.Bold};
            title = new GUIStyle(heading) {fontSize = 20};
            button = new GUIStyle(GUI.skin.button) {font=trainingFont,fontSize = 12, wordWrap = true, padding = new RectOffset(8,8,5,5), normal = {textColor = Ink,background=buttonTexture},hover={textColor=Ink,background=hoverTexture},active={textColor=Gold,background=selectedTexture},focused={textColor=Gold,background=hoverTexture}};
            chosen = new GUIStyle(button) {normal = {textColor = Gold,background=selectedTexture}};
        }
        static Texture2D UiTexture(Color color)
        {var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};texture.SetPixel(0,0,color);texture.Apply();return texture;}
        void ReleaseUiTextures(){if(buttonTexture)Destroy(buttonTexture);if(hoverTexture)Destroy(hoverTexture);if(selectedTexture)Destroy(selectedTexture);if(trainingFont)Destroy(trainingFont);}
        static void Fill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = old; }
        bool Button(Rect r, string label, bool active=false) => GUI.Button(r, label, active ? chosen : button);
        void OnGUI()
        {
            if (!world || !world.Ready) return;
            var oldFont = GUI.skin.font; var oldDepth = GUI.depth; var matrix = GUI.matrix;
            try
            {
                if(!trainingFont)trainingFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Segoe UI"},12);
                if(trainingFont)GUI.skin.font=trainingFont;
                else if(world.diagnostics.uiFont)GUI.skin.font=world.diagnostics.uiFont;
                Styles(); GUI.depth = -180;
                if(IsOpen) { DrawCustomizer(); return; }
                DrawLabels();

                float width = Mathf.Min(600, Screen.width - 32);
                Fill(new Rect(16,12,width,64),Paper);
                GUI.Label(new Rect(26,19,width-210,18),"허수아비 시험장  /  HP 1,000 × 6", text);
                GUI.Label(new Rect(26,47,width-210,16),PlayerControls.Prompt(world.arena.Player,"{DevelopmentReset} 표적 회복 / {Weapon1Basic}/{Weapon2Basic} 기본 1/2 / {Weapon1Special}/{Weapon2Special} 특수 1/2"), small);
                if(Button(new Rect(16+width-185,18,174,26),"장비 커스텀  [F1]"))SetOpen(true);
                if(Button(new Rect(16+width-185,46,174,24),"히트박스 "+(world.arena.showBoxes?"ON":"OFF")+"  [F2]",world.arena.showBoxes))ToggleHitboxes();
                Fill(new Rect(16,Screen.height-86,Screen.width-32,26),Paper);
                GUI.Label(new Rect(26,Screen.height-81,Screen.width-52,18),message,small);
                if(!world.diagnostics.ExternalHud)world.diagnostics.DrawCompactStatus(false);
            }
            finally {GUI.skin.font=oldFont;GUI.depth=oldDepth;GUI.matrix=matrix;}
        }
        void DrawLabels()
        {
            var ordered = new List<TrainingDummy>(targets);
            ordered.Sort((a,b)=>CameraDisplayProjection.WorldToScreenPoint(world.View,a.Position).x.CompareTo(CameraDisplayProjection.WorldToScreenPoint(world.View,b.Position).x));
            labelRects.Clear(); float width = Mathf.Min(140,(Screen.width-32)/3f), height = 82;
            foreach(var d in ordered)
            {
                var status=world.arena.ReadInkStatusVisuals(d);
                var p=CameraDisplayProjection.WorldToScreenPoint(world.View,d.Position+Vector2.up*(status.Marks>0||status.Frost>0?2.8f:2.15f)); if(p.z<0)continue;
                var area=new Rect(Mathf.Clamp(p.x-width/2,16,Screen.width-width-16),Mathf.Clamp(Screen.height-p.y-height,88,Mathf.Max(88,Screen.height-height-58)),width,height);
                var original=area;
                // Pack full label rectangles, not just text anchors. Keep a gap between neighboring cards.
                for(int attempt=0;attempt<40;attempt++)
                {
                    bool overlap=false;
                    foreach(var previous in labelRects)if(new Rect(previous.x-4,previous.y-4,previous.width+8,previous.height+8).Overlaps(area)){overlap=true;break;}
                    if(!overlap)break;
                    area.y=original.y-(attempt+1)*(height+8);
                    if(area.y<88){area.y=88+(attempt%3)*(height+8);area.x=Mathf.Clamp(original.x+(attempt/3%2==0?-1:1)*width,16,Screen.width-width-16);}
                }
                labelRects.Add(area);
                DrawLeader(new Vector2(p.x,Screen.height-p.y),new Vector2(area.center.x,area.yMax));
                Fill(area,Paper);
                GUI.Label(new Rect(area.x+7,area.y+5,area.width-14,15),d.name+(world.arena.ReadInkMarks(d)>0?" / 표식 "+world.arena.ReadInkMarks(d):"")+" / "+(d.Meter.HasHits?(d.Meter.Finished(world.arena.Now)?"결과":"측정"):"대기"),small);
                GUI.Label(new Rect(area.x+7,area.y+23,area.width-14,18),"피해 "+CombatDamageVariance.Format(d.Meter.Total),text);
                GUI.Label(new Rect(area.x+7,area.y+42,area.width-14,18),"DPS "+(d.Meter.HasHits?CombatDamageVariance.Format(d.Meter.Dps(world.arena.Now)):"—"),text);
                Fill(new Rect(area.x+7,area.y+78,area.width-14,3),Panel);
                Fill(new Rect(area.x+7,area.y+78,(area.width-14)*Mathf.Clamp01(d.health/d.maxHealth),3),Gold);
            }
        }
        static void DrawLeader(Vector2 a,Vector2 b)
        {
            var old=GUI.matrix; var delta=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y,delta.magnitude,1),Muted);GUI.matrix=old;
        }
        void DrawCustomizer()
        {
            float scale=Mathf.Min(Screen.width/1200f,Screen.height/760f);
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            Fill(new Rect(0,0,width,height),new Color(0,0,0,.7f));
            var area=new Rect((width-1120)/2,(height-700)/2,1120,700);Fill(area,Paper);
            GUI.Label(new Rect(area.x+28,area.y+20,850,38),"전투 장비 커스텀",title);
            GUI.Label(new Rect(area.x+28,area.y+62,950,24),"시험장 전용 / 편집 중 전투, 측정 시간 정지 / 두 무기 독립 설정",small);
            if(Button(new Rect(area.xMax-140,area.y+24,112,36),"닫기 [F1]"))SetOpen(false);
            for(int i=0;i<2;i++)if(Button(new Rect(area.x+28+i*132,area.y+104,122,38),"무기 "+(i==0?"A":"B"),weaponSlot==i)){weaponSlot=i;scroll=Vector2.zero;}
            if(Button(new Rect(area.x+330,area.y+104,250,38),"실제 장비 / 적용 가능",tab==0)){tab=0;scroll=Vector2.zero;}
            if(Button(new Rect(area.x+590,area.y+104,300,38),"색상 파츠 / "+InkPartCatalog.ImplementedCount+"종 시험 / "+(60-InkPartCatalog.ImplementedCount)+"종 보류",tab==1)){tab=1;scroll=Vector2.zero;}
            var content=new Rect(area.x+28,area.y+162,1064,474);
            if(tab==0)DrawEquipment(content);else DrawCandidates(content);
            Fill(new Rect(area.x+28,area.yMax-48,1064,1),Panel);
            GUI.Label(new Rect(area.x+28,area.yMax-38,1064,28),message,small);
        }
        public bool ApplyEquipment(int slot,int form,int special,int[] stats,out string error)
        {
            error=null;
            if(!IsOpen || slot<0 || slot>=2 || stats==null || stats.Length!=5){error="커스텀 화면에서 무기와 능력치 잉크 5칸을 지정하세요.";return false;}
            foreach(int kind in stats)if(kind<0||kind>4){error="알 수 없는 능력치 잉크입니다.";return false;}
            var w=world.arena.Player.weapons[slot];var before=(int[])w.StatSlots.Clone();
            w.StatSlots.CopyFrom(stats);
            if(!world.arena.Player.TryReconfigure(slot,form,special,out error)){w.StatSlots.CopyFrom(before);return false;}
            message="무기 "+(slot==0?"A":"B")+" 실제 장비에 적용됨 / 진행 중 효과는 기존 발동 수치 유지";return true;
        }
        void DrawEquipment(Rect area)
        {
            var arena=world.arena;var w=arena.Player.weapons[weaponSlot];var form=arena.catalog.forms[w.FormIndex];
            Fill(new Rect(area.x,area.y,520,area.height),Panel);Fill(new Rect(area.x+540,area.y,524,area.height),Panel);
            GUI.Label(new Rect(area.x+18,area.y+14,480,30),"01  무기 형태",heading);
            for(int i=0;i<arena.catalog.forms.Length;i++)
            {
                var f=arena.catalog.forms[i];var rect=new Rect(area.x+18+i%2*244,area.y+60+i/2*45,234,37);
                bool unavailable=arena.UnavailableTestForms.Contains(f.id);var enabled=GUI.enabled;GUI.enabled=!unavailable;
                if(Button(rect,f.name+(unavailable?" / 사용 불가":""),w.FormIndex==i))ApplyEquipment(weaponSlot,i,0,(int[])w.StatSlots.Clone(),out message);
                GUI.enabled=enabled;
            }
            GUI.Label(new Rect(area.x+18,area.y+302,484,30),"02  특수 공격 / 변환 잉크",heading);
            for(int i=0;i<form.specials.Length;i++)
                if(Button(new Rect(area.x+18+i%2*244,area.y+346+i/2*44,234,36),form.specials[i].name,!w.ExperimentalChargeStamp && w.SpecialIndex==i))
                { if(ApplyEquipment(weaponSlot,w.FormIndex,i,(int[])w.StatSlots.Clone(),out message))arena.Player.TrySetChargeStamp(weaponSlot,false,out _); }
            if(Button(new Rect(area.x+18,area.y+434,484,32),"별도 시험 차징 스탬프: "+(w.ExperimentalChargeStamp?"ON":"OFF"),w.ExperimentalChargeStamp))
                arena.Player.TrySetChargeStamp(weaponSlot,!w.ExperimentalChargeStamp,out message);
            GUI.Label(new Rect(area.x+560,area.y+14,480,30),"03  능력치 잉크 / 5칸",heading);
            for(int i=0;i<5;i++)
                if(Button(new Rect(area.x+560,area.y+60+i*48,484,39),(i+1)+"   "+StatNames[w.StatSlots[i]]+"   ›"))
                {var stats=(int[])w.StatSlots.Clone();stats[i]=(stats[i]+1)%5;ApplyEquipment(weaponSlot,w.FormIndex,w.SpecialIndex,stats,out message);}
            GUI.Label(new Rect(area.x+560,area.y+312,480,60),"클릭하여 잉크 순환 / 빈 칸 → 기본 피해 → 특수 피해 → 공속 → 쿨감",small);
            GUI.Label(new Rect(area.x+560,area.y+380,480,70),"기본 피해 +"+(w.BasicBonus*100).ToString("0")+"%   /   특수 피해 +"+(w.SpecialBonus*100).ToString("0")+"%\n기본 공속 ×"+w.Speed.ToString("0.##")+"   /   쿨감 "+(w.Reduction*100).ToString("0")+"%"+(form.id=="launcher" && !w.ExperimentalChargeStamp?"\n발사기: 특수 피해, 쿨감 잉크 비활성":""),text);
        }
        public bool ApplyInkPart(int slot,int paletteSlot,string id,out string error)
        {
            error=null;
            if(!IsOpen){error="커스텀 화면에서 편집하세요.";return false;}
            return world.arena.Player.TrySetInkPart(slot,paletteSlot,id,out error);
        }
        void DrawCandidates(Rect area)
        {
            if(parts==null){var asset=Resources.Load<TextAsset>("TrainingInkParts");parts=asset?JsonUtility.FromJson<PartList>(asset.text).parts:Array.Empty<PartInfo>();}
            GUI.Label(new Rect(area.x,area.y,1064,28),"공통 전투 시험 / 자유 6칸, 수치, 중복은 실험값 / 강철 주입 보류 / 나머지는 시험 적용 가능",text);
            for(int i=0;i<6;i++)
            {
                string id=world.arena.Player.weapons[weaponSlot].InkPalette.Read().Slots[i]?.PartId;var part=Array.Find(parts,p=>p.id==id);
                if(Button(new Rect(area.x+i*178,area.y+38,168,54),(i+1)+"  "+(part==null?"빈 시험 칸":part.name),selectedCandidateSlot==i))selectedCandidateSlot=i;
            }
            for(int i=0;i<Colors.Length;i++)if(Button(new Rect(area.x+i*112,area.y+110,102,32),Colors[i],colorFilter==i)){colorFilter=i;scroll=Vector2.zero;}
            var list=new List<int>();for(int i=0;i<parts.Length;i++)if(colorFilter==0||parts[i].color==ColorIds[colorFilter])list.Add(i);
            var viewport=new Rect(area.x,area.y+154,670,310);
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,646,Mathf.Ceil(list.Count/3f)*56));
            for(int j=0;j<list.Count;j++)
            {
                int i=list[j];if(Button(new Rect(j%3*215,j/3*56,205,48),parts[i].name+(InkPartCatalog.Implemented(parts[i].id)?"\n실험 효과 적용 가능":(parts[i].id=="g_armor"?"\n보류":"\n효과 미구현")),selectedPart==i))
                {
                    selectedPart=i;
                    if(ApplyInkPart(weaponSlot,selectedCandidateSlot,parts[i].id,out var error))message="실제 장비에 시험 적용됨 / 발동 당시 상태 유지";
                    else message=error;
                }
            }
            GUI.EndScrollView();
            Fill(new Rect(area.x+694,area.y+110,370,354),Panel);
            if(parts.Length>0)
            {
                var p=parts[Mathf.Clamp(selectedPart,0,parts.Length-1)];
                GUI.Label(new Rect(area.x+714,area.y+128,330,32),p.name,heading);
                GUI.Label(new Rect(area.x+714,area.y+173,330,110),p.description+(string.IsNullOrEmpty(p.requiresStamp)?"":"\n별도 시험 차징 스탬프 ON 필요"),text);
                GUI.Label(new Rect(area.x+714,area.y+286,330,120),"자체 쿨타임 "+p.cooldown+"초 / "+(p.duplicate?"중복 가능":"무기당 1개")+"\n"+(string.IsNullOrEmpty(p.unityNote)?p.limit:p.unityNote),small);
                if(Button(new Rect(area.x+714,area.y+416,330,34),"선택한 시험 칸 비우기") && ApplyInkPart(weaponSlot,selectedCandidateSlot,null,out message))message="시험 파츠 해제됨 / 이미 발동한 효과는 유지";
            }
        }
    }
}
