using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class DebugGameMenu
    {
        const float RowHeight=52, RowGap=6, ColumnGap=8;
        GUIStyle titleStyle,hintStyle,statusStyle,buttonStyle,selectedStyle,scrollStyle,sectionStyle,infoStyle,valueStyle,selectedValueStyle,tabStyle,selectedTabStyle;
        readonly List<Rect> rowBounds=new List<Rect>();
        readonly List<Section> sections=new List<Section>();
        Vector2 statusScroll;
        string renderedStatus;
        struct Section { public string Title; public Rect Bounds; }

        // Each line preserves reading order. Group boundaries and full-width descriptions start a new line.
        List<List<int>> PackLines()
        {
            var result=new List<List<int>>();List<int> line=null;float used=0;string group=null;
            for(int i=0;i<rows.Count;i++)
            {
                if(line==null||group!=rows[i].Group||used+rows[i].Share>1.001f)
                { line=new List<int>();result.Add(line);used=0;group=rows[i].Group; }
                line.Add(i);used+=rows[i].Share;
            }
            return result;
        }
        float LineCenter(List<int> line,int index)
        {
            float x=0;
            foreach(int peer in line){if(peer==index)return x+rows[peer].Share/2;x+=rows[peer].Share;}
            return x;
        }
        void PrepareStyles()
        {
            if(buttonStyle!=null)return;
            var font=arena.uiFont?arena.uiFont:GUI.skin.font;
            titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=26,fontStyle=FontStyle.Bold,wordWrap=true};
            hintStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};
            statusStyle=new GUIStyle(hintStyle){fontSize=20};
            buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=20,alignment=TextAnchor.MiddleLeft,wordWrap=true,
                padding=new RectOffset(10,10,8,8),margin=new RectOffset()};
            buttonStyle.normal.textColor=buttonStyle.hover.textColor=buttonStyle.active.textColor=Color.white;
            selectedStyle=new GUIStyle(buttonStyle){fontStyle=FontStyle.Bold};
            selectedStyle.normal.textColor=selectedStyle.hover.textColor=selectedStyle.active.textColor=new Color(1,.9f,.45f);
            scrollStyle=new GUIStyle(GUI.skin.verticalScrollbar){fixedWidth=24};
            infoStyle=new GUIStyle(hintStyle){padding=new RectOffset(4,4,2,2)};
            sectionStyle=new GUIStyle(hintStyle){fontSize=20,fontStyle=FontStyle.Bold};
            sectionStyle.normal.textColor=new Color(.65f,.8f,1);
            valueStyle=new GUIStyle(infoStyle){fontSize=20,alignment=TextAnchor.MiddleLeft};
            selectedValueStyle=new GUIStyle(valueStyle){fontStyle=FontStyle.Bold};
            selectedValueStyle.normal.textColor=new Color(1,.9f,.45f);
            tabStyle=new GUIStyle(buttonStyle){alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,6,6)};
            selectedTabStyle=new GUIStyle(selectedStyle){alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,6,6)};
            titleStyle.normal.textColor=valueStyle.normal.textColor=Color.white;
            hintStyle.normal.textColor=infoStyle.normal.textColor=new Color(.8f,.84f,.9f);
            statusStyle.normal.textColor=new Color(1,.9f,.45f);
        }
        string RowText(Row row)=>row.Caption??row.Label;
        string ActionText(Row row)=>RowText(row)+(row.Reason==null?"":"\n"+row.Reason);
        float TabLabelWidth(float width)=>width<450?84:128;
        float MeasureRow(Row row,float width)
        {
            if(row.Tabs!=null)
            {
                float tabWidth=(width-TabLabelWidth(width)-ColumnGap*(row.Tabs.Length-1))/Mathf.Max(1,row.Tabs.Length);
                float height=Mathf.Max(RowHeight,selectedValueStyle.CalcHeight(new GUIContent("▶ "+RowText(row)),TabLabelWidth(width)-8));
                foreach(string tab in row.Tabs)height=Mathf.Max(height,selectedTabStyle.CalcHeight(new GUIContent(tab),Mathf.Max(24,tabWidth)));
                return height;
            }
            if(row.Information)return Mathf.Max(28,infoStyle.CalcHeight(new GUIContent(ActionText(row)),width));
            var style=row.Change!=null?selectedValueStyle:selectedStyle;
            return Mathf.Max(RowHeight,style.CalcHeight(new GUIContent("▶ "+ActionText(row)),Mathf.Max(24,width-(row.Change!=null?120:0))));
        }
        float ArrangeRows(float width)
        {
            rowBounds.Clear();sections.Clear();for(int i=0;i<rows.Count;i++)rowBounds.Add(default);
            float y=0;string group=null;int section=-1;
            foreach(var line in PackLines())
            {
                string next=rows[line[0]].Group;
                if(group!=next)
                {
                    if(section>=0){var old=sections[section];old.Bounds.height=y-old.Bounds.y+4;sections[section]=old;}
                    if(group!=null)y+=10;
                    group=next;section=-1;
                    if(!string.IsNullOrEmpty(group))
                    {
                        section=sections.Count;sections.Add(new Section{Title=group,Bounds=new Rect(0,y,width,0)});
                        y+=8+Mathf.Max(26,sectionStyle.CalcHeight(new GUIContent(group),width-20))+6;
                    }
                }
                float inset=string.IsNullOrEmpty(group)?0:10;
                float available=width-inset*2-ColumnGap*(line.Count-1),x=inset,height=0;
                foreach(int index in line)height=Mathf.Max(height,MeasureRow(rows[index],available*rows[index].Share));
                foreach(int index in line)
                {
                    float cellWidth=available*rows[index].Share;
                    rowBounds[index]=new Rect(x,y,cellWidth,height);x+=cellWidth+ColumnGap;
                }
                y+=height+RowGap;
            }
            if(section>=0){var old=sections[section];old.Bounds.height=y-old.Bounds.y+4;sections[section]=old;y+=4;}
            return y;
        }
        void DrawRows(Rect viewport,ref Action requestedAction)
        {
            Build();EnsureSelection();
            if(renderedCategory!=category||renderedConfirmation!=confirmation||renderedPicker!=picker)
            {
                scroll=Vector2.zero;renderedCategory=category;renderedConfirmation=confirmation;renderedPicker=picker;revealSelection=true;
            }
            float width=viewport.width-28,height=ArrangeRows(width);
            if(revealSelection&&selected>=0)
            {
                var rect=rowBounds[selected];
                if(rect.yMin<scroll.y)scroll.y=rect.yMin;
                else if(rect.yMax>scroll.y+viewport.height)scroll.y=rect.yMax-viewport.height;
                revealSelection=false;
            }
            scroll.y=Mathf.Clamp(scroll.y,0,Mathf.Max(0,height-viewport.height));
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,width,height),false,true,GUIStyle.none,scrollStyle);
            foreach(var section in sections)
            {
                GUI.color=new Color(.1f,.13f,.18f);GUI.DrawTexture(section.Bounds,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(new Rect(section.Bounds.x+10,section.Bounds.y+8,section.Bounds.width-20,section.Bounds.height),section.Title,sectionStyle);
            }
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];var rect=rowBounds[i];bool focused=selected==i;
                bool enabled=GUI.enabled;GUI.enabled=enabled&&row.Reason==null;
                GUI.backgroundColor=focused?new Color(.3f,.48f,.72f):new Color(.3f,.34f,.4f);
                string text=(focused?"▶ ":"")+ActionText(row);
                if(row.Tabs!=null)
                {
                    float labelWidth=TabLabelWidth(rect.width);
                    GUI.Label(new Rect(rect.x,rect.y,labelWidth-8,rect.height),(focused?"▶ ":"")+RowText(row),focused?selectedValueStyle:valueStyle);
                    float tabWidth=(rect.width-labelWidth-ColumnGap*(row.Tabs.Length-1))/Mathf.Max(1,row.Tabs.Length);
                    for(int tab=0;tab<row.Tabs.Length;tab++)
                    {
                        int index=tab;
                        if(GUI.Button(new Rect(rect.x+labelWidth+tab*(tabWidth+ColumnGap),rect.y,tabWidth,rect.height),row.Tabs[tab],tab==row.TabIndex?selectedTabStyle:tabStyle))
                        {selected=i;requestedAction=()=>row.SelectTab(index);}
                    }
                }
                else if(row.Information)GUI.Label(rect,ActionText(row),infoStyle);
                else if(row.Change!=null)
                {
                    GUI.Label(new Rect(rect.x,rect.y,rect.width-120,rect.height),text,focused?selectedValueStyle:valueStyle);
                    if(GUI.Button(new Rect(rect.xMax-112,rect.y,52,rect.height),"−",tabStyle)){selected=i;requestedAction=()=>row.Change(-1);}
                    if(GUI.Button(new Rect(rect.xMax-52,rect.y,52,rect.height),"＋",tabStyle)){selected=i;requestedAction=()=>row.Change(1);}
                }
                else if(GUI.Button(rect,text,focused?selectedStyle:buttonStyle)){selected=i;requestedAction=row.Run;}
                GUI.enabled=enabled;
            }
            GUI.EndScrollView();
        }
        void OnGUI()
        {
            if(!IsOpen)return;
            PrepareStyles();
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;var oldBackground=GUI.backgroundColor;int oldDepth=GUI.depth;
            Action requestedAction=null;bool back=false,close=false;
            try
            {
                float scale=MenuScale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.depth=-1000;
                var area=PanelArea(scale);GUI.color=new Color(.055f,.065f,.085f,.98f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=Color.white;
                var content=new Rect(area.x+16,area.y+12,area.width-32,area.height-24);
                GUI.Label(new Rect(content.x,content.y,content.width-184,44),"치트 모드 / "+(category<0?"분류":Categories[category]),titleStyle);
                GUI.backgroundColor=new Color(.3f,.34f,.4f);
                back=GUI.Button(new Rect(content.xMax-176,content.y,80,44),"뒤로",tabStyle);
                close=GUI.Button(new Rect(content.xMax-88,content.y,88,44),"닫기",tabStyle);
                string hint=placing?"마우스, 방향키, 스틱 이동 / 클릭/Enter/A 배치 / Esc/B 뒤로":
                    "↑↓ 항목  ←→ 값/열  Enter/A 선택  Esc/B 뒤로"+(category==2?"  Tab, LB/RB 무기":"");
                float hintHeight=Mathf.Max(26,hintStyle.CalcHeight(new GUIContent(hint),content.width));
                GUI.Label(new Rect(content.x,content.yMax-hintHeight,content.width,hintHeight),hint,hintStyle);
                float top=content.y+50;
                if(renderedStatus!=status){renderedStatus=status;statusScroll=Vector2.zero;}
                if(!string.IsNullOrEmpty(status))
                {
                    float natural=statusStyle.CalcHeight(new GUIContent(status),content.width-28),statusHeight=Mathf.Min(64,Mathf.Max(28,natural));
                    statusScroll=GUI.BeginScrollView(new Rect(content.x,top,content.width,statusHeight),statusScroll,new Rect(0,0,content.width-28,natural),false,false,GUIStyle.none,scrollStyle);
                    GUI.Label(new Rect(0,0,content.width-28,natural),status,statusStyle);GUI.EndScrollView();top+=statusHeight+6;
                }
                if(placing)GUI.Label(new Rect(content.x,top,content.width,32),world.Data.monsters[species].name+" / 배치 위치: "+cursor,statusStyle);
                else DrawRows(new Rect(content.x,top,content.width,Mathf.Max(52,content.yMax-hintHeight-6-top)),ref requestedAction);
                if(placing&&world.View)
                {
                    var point=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View,cursor);
                    GUI.Label(new Rect(point.x/scale-16,(Screen.height-point.y)/scale-24,48,48),"＋",titleStyle);
                }
                if(requestedAction!=null)Perform(requestedAction);
                if(back)Back();if(close)Close();
            }
            finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.backgroundColor=oldBackground;GUI.depth=oldDepth;}
        }
    }
}
