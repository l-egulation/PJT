using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Deviant.UI.Theme;

namespace ForestCombatV2
{
    // Presentation only: the live binding retains rolls, registration, pause and bag ownership.
    // Coordinates are the approved 1920x1080 Figma layout expressed at half scale.
    public sealed class SketchRewardChoiceScreen
    {
        public static float DimOpacity => ApprovedTheme.MenuBackdrop.a;
        public static readonly Color Gold = new Color32(217,203,132,255);
        static readonly Color Cream = new Color32(243,239,229,255), Muted = new Color32(174,182,192,255), Accent = new Color32(173,210,222,255);
        public sealed class Item
        {
            public string Name, Kind, Detail, ArtPath, FallbackArtPath, LegacyArtPath, FormId, InkId, SkillName, Attack;
            public IReadOnlyList<string> Slots;
            public bool IsInk;
            public int InkUses;
        }
        public Action<int> Chosen;
        public Action Continued, Configured, Rerolled;
        // Shown beside the bag key while the run owns shared rerolls; null hides the reroll button.
        public string RerollLabel;
        public static readonly Rect RerollButton = new Rect(650, 499, 245, 24);
        public int Selected { get; private set; }
        public bool Acquired { get; private set; }
        public float ScrollOffset => scroll[Acquired ? 0 : Selected];
        public IReadOnlyList<Item> Items { get; private set; } = Array.Empty<Item>();
        object identity;
        bool accepted;
        int banked;
        string heading, bagKeyLabel, submitKeyLabel = "F", navigateKeyLabel = "← →", cancelKeyLabel = "Esc";
        GUIStyle title, name, detail, kind, small, selectedName, sectionLabel, skillTitle;
        Font font;
        readonly Dictionary<string, Texture2D> images = new Dictionary<string, Texture2D>();
        readonly float[] scroll = new float[3], scrollMax = new float[3];
        Vector2 lastPointer = new Vector2(float.NaN,float.NaN);

        public static Rect Viewport(float width,float height)
        {
            float scale=Mathf.Min(width/960,height/540);
            return new Rect((width-960*scale)/2,(height-540*scale)/2,960*scale,540*scale);
        }
        public static Rect Card(int index)=>new Rect(65+index*300,120,230,350);
        public static string WeaponArt(string form,string skill=null)=>"SketchRewardArt/Weapons/"+form+(string.IsNullOrEmpty(skill)?"":"/"+skill);
        public static string InkArt(string id)=>"SketchRewardArt/Inks/"+id;
        public void Offer(object key,IReadOnlyList<Item> items,int remaining,bool boss,string title=null)
        {
            if(!ReferenceEquals(identity,key)||Acquired) ResetSelection();
            identity=key;Items=items;Acquired=false;banked=remaining;heading=title??(boss?"보스 보상":"보상 선택");
        }
        public void Picked(object key,Item item)
        {
            if(!ReferenceEquals(identity,key)||!Acquired)ResetSelection();
            identity=key;Items=new[]{item};Acquired=true;
        }
        void ResetSelection(){Selected=0;accepted=false;Array.Clear(scroll,0,scroll.Length);Array.Clear(scrollMax,0,scrollMax.Length);}
        public void Move(int direction)
        {
            int count=Acquired?2:Items.Count;if(count>0)Selected=(Selected+direction%count+count)%count;
        }
        public void Scroll(float delta)
        {
            int at=Acquired?0:Selected;if(at<0||at>=scroll.Length)return;
            scroll[at]=Mathf.Clamp(scroll[at]+delta,0,scrollMax[at]);
        }
        public void Activate()
        {
            if(accepted||Items.Count==0)return;
            accepted=true;
            if(Acquired){if(Selected==0)Continued?.Invoke();else Configured?.Invoke();}
            else Chosen?.Invoke(Selected);
        }
        // A parked pointer never overrides keyboard/controller focus.
        public void Pointer(Vector2 screen,float width,float height)
        {
            if(screen==lastPointer)return;lastPointer=screen;
            var view=Viewport(width,height);var p=(new Vector2(screen.x,height-screen.y)-view.position)/(view.width/960);
            int count=Acquired?2:Items.Count;
            for(int i=0;i<count;i++)if((Acquired?AfterButton(i):Card(i)).Contains(p)){Selected=i;return;}
        }
        public static void Dim()
        {
            var color=GUI.color;GUI.color=DrawColor(ApprovedTheme.MenuBackdrop);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;
        }
        public void Draw(Font fallbackFont,string bagKey,string submitKey,string navigateKey="← →",string cancelKey="Esc",bool drawBackdrop=true)
        {
            bagKeyLabel=bagKey;submitKeyLabel=submitKey;navigateKeyLabel=navigateKey;cancelKeyLabel=cancelKey;
            Styles(ApprovedTheme.Font?ApprovedTheme.Font:fallbackFont);
            var matrix=GUI.matrix;var color=GUI.color;var view=Viewport(Screen.width,Screen.height);if(drawBackdrop)Dim();
            GUI.matrix=Matrix4x4.TRS(view.position,Quaternion.identity,Vector3.one*(view.width/960));GUI.color=Color.white;
            try{Header();if(Acquired)DrawAcquired();else DrawOffer();}
            finally{GUI.matrix=matrix;GUI.color=color;}
        }
        void Styles(Font next)
        {
            if(title!=null&&font==next)return;font=next;
            title=Style(23,Cream);name=Style(20,Cream);selectedName=Style(20,Gold);detail=Style(14,new Color32(218,222,226,255));kind=Style(12,Muted);small=Style(12,Muted);
            sectionLabel=Style(10,Muted);skillTitle=Style(17,Cream);skillTitle.fontStyle=FontStyle.Bold;
        }
        GUIStyle Style(int size,Color color)=>new GUIStyle(GUI.skin.label){font=font,fontSize=size,alignment=TextAnchor.UpperLeft,wordWrap=true,richText=true,normal={textColor=color},padding=new RectOffset(),margin=new RectOffset()};
        void Header()
        {
            GUI.Label(new Rect(65,32,450,30),heading??"보상 선택",title);
            GUI.Label(new Rect(844,40,70,20),"일시정지",small);
            GUI.Label(new Rect(65,85,400,30),heading=="보스 보상"?"보스 보상   3종 중 1개 선택":"보상 기회   "+banked,detail);
        }
        void DrawOffer()
        {
            for(int i=0;i<Items.Count;i++)
            {
                var r=Card(i);var item=Items[i];bool selected=i==Selected;
                if(i>0)Fill(new Rect(r.x-25,r.y,0.5f,r.height),new Color(.4f,.44f,.46f,.35f));
                var old=GUI.color;GUI.color=new Color(1,1,1,selected?1:.74f);
                GUI.Label(new Rect(r.x+12,r.y+5,r.width-36,20),item.IsInk?"잉크":"스케치",kind);GUI.color=old;
                float size=selected?76:70;var artRect=new Rect(r.x+(r.width-size)/2,r.y+38+(86-size)/2,size,size);
                GUI.color=new Color(1,1,1,selected?1:.6f);Art(artRect,item);GUI.color=old;
                var nr=new Rect(r.x+12,r.y+129,r.width-36,32);
                GUI.color=new Color(1,1,1,selected?1:.74f);
                GUI.Label(nr,item.Name,selected?selectedName:name);GUI.color=old;
                Fill(new Rect(r.x+12,r.y+164,r.width-24,.5f),new Color(.6f,.64f,.66f,.32f));
                DrawDetails(new Rect(r.x+12,r.y+176,r.width-30,r.height-214),item,i,selected);
                if(selected)Action(new Rect(r.x+12,r.yMax-28,r.width-24,25),"선택하기",true,Activate);
                if(Hit(new Rect(r.x,r.y,r.width,182))){Selected=i;return;}
            }
            if(RerollLabel!=null)Action(RerollButton,RerollLabel,false,()=>Rerolled?.Invoke());
            Footer(false);
        }
        static Rect AfterButton(int index)=>new Rect(230+index*220,445,index==0?195:180,30);
        void DrawAcquired()
        {
            var item=Items[0];
            GUI.Label(new Rect(230,136,160,22),"획득",Style(12,new Color32(171,205,136,255)));
            Art(new Rect(230,166,75,75),item);GUI.Label(new Rect(325,174,405,44),item.Name,selectedName);
            GUI.Label(new Rect(325,208,405,30),item.IsInk?$"잉크 부여 횟수 +{item.InkUses}회":"가방에 보관됐습니다.",small);
            Fill(new Rect(230,253,500,.5f),new Color(.4f,.44f,.46f,.55f));
            DrawDetails(new Rect(230,270,500,152),item,0);
            Fill(new Rect(230,429,500,.5f),new Color(.4f,.44f,.46f,.55f));
            for(int i=0;i<2;i++)
            {
                int at=i;string caption=i==0?(item.IsInk?"계속":"가방에 넣고 계속"):(item.IsInk?"지금 부여하기":"지금 구성하기");
                Action(AfterButton(i),caption,i==Selected,()=>{Selected=at;Activate();});
            }
            Footer(true);
        }
        void DrawDetails(Rect region,Item item,int at,bool focused=true)
        {
            float width=region.width-16,cursor=0;
            var blocks=new List<(string text,GUIStyle style,float y,float height)>();
            void Block(string text,GUIStyle style,float gap)
            {
                text=Deviant.UI.Theme.ApprovedInkText.Display(text);
                if(string.IsNullOrEmpty(text))return;
                text=WrapWords(text,width,value=>style.CalcSize(new GUIContent(value)).x);
                float h=style.CalcHeight(new GUIContent(text),width);
                blocks.Add((text,style,cursor,h));cursor+=h+gap;
            }
            if(item.IsInk)Block("잉크 효과",sectionLabel,5);
            else
            {
                if(!string.IsNullOrEmpty(item.Attack)){Block("기본 공격",sectionLabel,5);Block(item.Attack,detail,14);}
                if(!string.IsNullOrEmpty(item.SkillName)){Block("스킬",sectionLabel,4);Block(item.SkillName,skillTitle,8);}
            }
            Block(item.Detail,detail,0);
            float textHeight=cursor;
            float contentHeight=textHeight+(item.IsInk?8:76);scrollMax[at]=Mathf.Max(0,contentHeight-region.height);scroll[at]=Mathf.Clamp(scroll[at],0,scrollMax[at]);
            bool overflow=scrollMax[at]>0;var pos=GUI.BeginScrollView(region,new Vector2(0,scroll[at]),new Rect(0,0,width,Mathf.Max(region.height,contentHeight)),false,false,GUIStyle.none,GUIStyle.none);
            scroll[at]=pos.y;
            var old=GUI.color;GUI.color=new Color(1,1,1,focused?1:.74f);
            foreach(var block in blocks)GUI.Label(new Rect(0,block.y,width,block.height),block.text,block.style);
            if(!item.IsInk){GUI.Label(new Rect(0,textHeight+20,160,20),"팔레트",kind);GUI.color=new Color(1,1,1,focused?1:.6f);Slots(new Rect(0,textHeight+44,width,28),item.Slots);}
            GUI.color=old;
            GUI.EndScrollView();
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(overflow)
            {
                var track=new Rect(region.xMax-8,region.y,4,region.height);
                float thumbHeight=Mathf.Max(20,region.height*region.height/contentHeight);
                float travel=region.height-thumbHeight;
                var pointer=Event.current;
                if(pointer.type==EventType.MouseDown&&pointer.button==0&&new Rect(track.x-5,track.y,14,track.height).Contains(pointer.mousePosition))
                {GUIUtility.hotControl=control;pointer.Use();}
                if(GUIUtility.hotControl==control)
                {
                    scroll[at]=Mathf.Clamp01((pointer.mousePosition.y-track.y-thumbHeight/2)/travel)*scrollMax[at];
                    if(pointer.rawType==EventType.MouseUp){GUIUtility.hotControl=0;pointer.Use();}
                    else if(pointer.type==EventType.MouseDrag)pointer.Use();
                }
                Fill(track,new Color(.4f,.44f,.46f,.25f));
                Fill(new Rect(track.x,track.y+travel*scroll[at]/scrollMax[at],track.width,thumbHeight),new Color(Accent.r,Accent.g,Accent.b,.65f));
            }
        }
        void Footer(bool receipt)
        {
            Fill(new Rect(65,491.5f,830,.5f),new Color(.4f,.44f,.46f,.55f));
            float x=65;
            Hint(ref x,navigateKeyLabel,receipt?"버튼 선택":"보상 선택",Mathf.Max(29,navigateKeyLabel.Length*7));
            Hint(ref x,submitKeyLabel,receipt?"실행":"획득",Mathf.Max(17,submitKeyLabel.Length*7));
            if(receipt)Hint(ref x,cancelKeyLabel,"계속",Mathf.Max(25,cancelKeyLabel.Length*7));else Hint(ref x,bagKeyLabel??"Tab","가방 확인",34);
            Hint(ref x,navigateKeyLabel=="← →"?"↑ ↓":navigateKeyLabel,"설명 스크롤",Mathf.Max(29,navigateKeyLabel.Length*7));
        }
        void Hint(ref float x,string key,string caption,float keyWidth)
        {
            var keyStyle=new GUIStyle(small){fontSize=ApprovedTheme.ControlHintFontSize,alignment=TextAnchor.MiddleCenter,wordWrap=false};
            keyWidth=Mathf.Max(keyWidth,Mathf.Ceil(keyStyle.CalcSize(new GUIContent(key)).x)+12);
            Key(new Rect(x,502,keyWidth,ApprovedTheme.ControlHintHeight),key,ApprovedTheme.ControlHintFontSize);
            var style=new GUIStyle(small){fontSize=ApprovedTheme.ControlHintFontSize,alignment=TextAnchor.MiddleLeft,wordWrap=false};
            float width=Mathf.Ceil(style.CalcSize(new GUIContent(caption)).x)+4;
            GUI.Label(new Rect(x+keyWidth+8,502,width,ApprovedTheme.ControlHintHeight),caption,style);x+=keyWidth+width+28;
        }
        void Action(Rect rect,string caption,bool selected,Action clicked)
        {
            float keyWidth=Mathf.Max(17,submitKeyLabel.Length*8);
            float left=selected?keyWidth+8:0;var textRect=new Rect(rect.x+left,rect.y,rect.width-left,rect.height);
            var style=new GUIStyle(detail){alignment=TextAnchor.MiddleLeft};
            if(selected){style.normal.textColor=Gold;Key(new Rect(rect.x,rect.y+(rect.height-17)/2,keyWidth,17),submitKeyLabel);}
            GUI.Label(textRect,caption,style);if(Hit(rect))clicked();
        }
        void Key(Rect r,string text,int size=12){Border(r,Accent,.5f);var s=new GUIStyle(small){fontSize=size,alignment=TextAnchor.MiddleCenter,wordWrap=false};GUI.Label(r,text,s);}
        void Wash(Rect r,string text,GUIStyle style,float verticalPadding=3)
        {
            var wash=ApprovedTheme.SelectionWash;if(!wash)return;
            float width=Mathf.Min(r.width+14,style.CalcSize(new GUIContent(text)).x+14);
            float height=Mathf.Min(r.height+verticalPadding*2,style.CalcHeight(new GUIContent(text),r.width)+verticalPadding*2);
            float y=style.alignment==TextAnchor.MiddleLeft?r.center.y-height/2:r.y-verticalPadding;
            var old=GUI.color;GUI.color=new Color(1,1,1,.48f);
            GUI.DrawTexture(new Rect(r.x-7,y,width,height),wash,ScaleMode.StretchToFill,true);GUI.color=old;
        }
        // Unity's Korean label wrapping may split the last syllable onto its own line.
        // Measure whole space-delimited words first; tags remain intact across the inserted newline.
        public static string WrapWords(string text,float width,Func<string,float> measure)
        {
            if(string.IsNullOrEmpty(text))return "";
            var result=new StringBuilder();
            foreach(var paragraph in text.Replace("\r","").Split('\n'))
            {
                if(result.Length>0)result.Append('\n');
                string line="";
                foreach(var word in paragraph.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate=line.Length==0?word:line+" "+word;
                    string plain=Regex.Replace(candidate,"<[^>]*>","");
                    if(line.Length>0&&measure(plain)>width){result.Append(line).Append('\n');line=word;}
                    else line=candidate;
                }
                result.Append(line);
            }
            return result.ToString();
        }
        void Art(Rect rect,Item item)
        {
            var image=item.IsInk?ApprovedTheme.Ink(item.InkId):ApprovedTheme.Texture("ui-assets-v2/"+item.FormId);
            if(!image)image=Load(item.ArtPath)??Load(item.FallbackArtPath)??Load(item.LegacyArtPath);
            if(image)GUI.DrawTexture(rect,image,ScaleMode.ScaleToFit,true);
        }
        Texture2D Load(string path)
        {
            if(string.IsNullOrEmpty(path))return null;if(images.TryGetValue(path,out var found)&&found)return found;
            var image=Resources.Load<Texture2D>(path);if(image)images[path]=image;return image;
        }
        void Slots(Rect r,IReadOnlyList<string> slots)
        {
            if(slots==null)return;float size=Mathf.Min(32,(r.width-8)/Mathf.Max(1,slots.Count)-4);
            for(int i=0;i<slots.Count;i++)
            {
                var texture=ApprovedTheme.Palette(slots[i]??"neutral");var rect=new Rect(r.x+i*(size+4),r.y,size,size*.72f);
                if(texture)GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);else Fill(rect,SlotColor(slots[i]));
            }
        }
        public static Color SlotColor(string color)
        {
            switch(color){case "red":return new Color32(182,124,114,255);case "yellow":return new Color32(196,175,98,255);case "green":return new Color32(121,160,120,255);case "blue":return new Color32(123,152,179,255);case "purple":return new Color32(155,127,180,255);default:return new Color32(98,105,95,255);}
        }
        static bool Hit(Rect r)
        {
            int id=GUIUtility.GetControlID(FocusType.Passive,r);var e=Event.current;
            switch(e.GetTypeForControl(id))
            {
                case EventType.MouseDown:if(e.button==0&&r.Contains(e.mousePosition)){GUIUtility.hotControl=id;e.Use();}break;
                case EventType.MouseUp:if(e.button==0&&GUIUtility.hotControl==id){GUIUtility.hotControl=0;e.Use();return r.Contains(e.mousePosition);}break;
            }
            return false;
        }
        static Color DrawColor(Color color)=>QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
        static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=DrawColor(c);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        static void Border(Rect r,Color color,float w=1){Fill(new Rect(r.x,r.y,r.width,w),color);Fill(new Rect(r.x,r.yMax-w,r.width,w),color);Fill(new Rect(r.x,r.y,w,r.height),color);Fill(new Rect(r.xMax-w,r.y,w,r.height),color);}
    }
}
