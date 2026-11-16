using System;
using System.Collections.Generic;
using Deviant.Input.Contracts;
using Deviant.UI.Theme;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        sealed class MapMark { public RectTransform Root; public Image Icon; public Text Label; public RectTransform Check; }
        readonly List<MapMark> approvedMapMarks=new List<MapMark>();
        readonly List<Image> mapCameraDashes=new List<Image>();
        ApprovedMapGeometry mapGeometry;
        ScrollRect mapLegend;
        Text mapChapter,mapZoom,mapExitLegend;
        float mapFitZoom=1;
        int mapMarkUsed;
        sealed class MapFooterPrompt { public RectTransform Root; public string Template, Current; public float Width; }
        readonly List<MapFooterPrompt> mapFooterPrompts=new List<MapFooterPrompt>();
        static readonly Color MapFooterInk=new Color32(179,187,196,255);
        static readonly Dictionary<string,Sprite> recoveredMapIcons=new Dictionary<string,Sprite>();
        static Sprite RecoveredMapIcon(string id)
        {
            if(!recoveredMapIcons.TryGetValue(id,out var sprite))
            {
                sprite=Resources.Load<Sprite>("RecoveredMapIcons/"+id);
                recoveredMapIcons[id]=sprite;
            }
            return sprite;
        }
        static Sprite MapArt(string id)=>RecoveredMapIcon(id=="workshop"?"workshop-yellow":id)??ApprovedTheme.Sprite("common/full-map-v1__assets__"+id);
        static Sprite MapWorldArt(int id)=>RecoveredMapIcon(id==0?"spirit":id==1?"sapling":id==2?"danger-chest":id==3?"trap":id==9?"exit":"world-"+id)??ApprovedTheme.Sprite("ui-assets-v1/world-"+id);

        RectTransform mapCloseRoot;
        Text mapCloseKey,mapCloseCaption;
        void LayoutMapClose()
        {
            if(!mapCloseRoot||!mapCloseKey||!mapCloseCaption)return;
            float width=FitApprovedKeycap(mapCloseKey);
            Place(mapCloseRoot,895-width-32,53,width+32,24);
            Place(mapCloseCaption,width+11,0,21,24);
        }
        void BuildApprovedMap()
        {
            // Figma 14:50041 / 14:50347 / 14:50653, at half the 1920x1080 design coordinates.
            mapPanel=Rect("Approved live map",MenuStage,0,0,960,540).gameObject;
            var page=mapPanel.transform;
            var backdrop=RegisterScreenBackdrop(Fill(page,0,0,960,540,Deviant.UI.Theme.ApprovedTheme.NavigationBackdrop));backdrop.raycastTarget=true;
            LiveArt(ApprovedTheme.SelectionWash,page,66,42,55,37,.44f);
            LiveWords("지도",page,75,48,45,25,22);
            mapChapter=LiveWords("",page,126,53,100,22,14,CoreMuted);
            LiveWords("일시정지　 |",page,748,53,80,24,12,CoreMuted);
            var closeRoot=mapCloseRoot=Rect("Close live map",page,813,53,72,24);
            var closeHit=closeRoot.gameObject.AddComponent<Image>();closeHit.color=Color.clear;
            var close=closeRoot.gameObject.AddComponent<Button>();close.transition=Selectable.Transition.None;close.navigation=new Navigation{mode=Navigation.Mode.None};

            close.onClick.AddListener(()=>{if(map)map.SetOpen(false);Refresh();});
            mapCloseKey=BindPrompt(ApprovedKeycap(close.transform,"",3,-1,44,19,11).GetComponentInChildren<Text>(),"{Map} / {Cancel}");
            mapCloseCaption=LiveWords("닫기",closeRoot,0,0,21,24,12,CoreMuted);
            LayoutMapClose();
            fullMap=MakeMap(page,75,106.5f,MapSize.x,MapSize.y,true);
            fullMap.Terrain.gameObject.SetActive(false);fullMap.Player.gameObject.SetActive(false);
            mapGeometry=Rect("Discovered room geometry",fullMap.Clip,0,0,MapSize.x,MapSize.y).gameObject.AddComponent<ApprovedMapGeometry>();mapGeometry.raycastTarget=false;
            BuildMapLegend(page);
            // Figma 14:50309 in native design pixels; parent menus use half-size coordinates.
            var footer=Rect("Figma map footer",page,75,471.9f,1620,62.195f);footer.localScale=Vector3.one*.5f;
            Fill(footer,0,0,1620,1,new Color32(168,175,182,64));
            MapHint(footer,0,157.172f,71.352f,80.35f,8.68f,"{MapPan}","지도 이동",null);
            MapHint(footer,191.17f,157.781f,70.188f,79.19f,9.09f,"{MapZoomIn} / {MapZoomOut}","확대·축소",null);
            MapHint(footer,382.95f,119.891f,34,43,10,"{MapFocus}","현재 위치",FocusMap);
            MapHint(footer,536.84f,156.695f,70.891f,79.89f,8.95f,"{MapFit}","전체 보기",FitMapExplored);
            RefreshMapFooterPrompts();
            var cameraHint=Rect("Current screen range hint",page,451,482,145,23);
            var rangeColor=new Color32(169,186,203,210);
            for(int x=0;x<17;x+=4)
            {
                float length=Mathf.Min(2,17-x);
                Fill(cameraHint,x,6,length,1,rangeColor).raycastTarget=false;
                Fill(cameraHint,x,16,length,1,rangeColor).raycastTarget=false;
            }
            for(int y=0;y<11;y+=4)
            {
                float length=Mathf.Min(2,11-y);
                Fill(cameraHint,0,6+y,1,length,rangeColor).raycastTarget=false;
                Fill(cameraHint,16,6+y,1,length,rangeColor).raycastTarget=false;
            }
            LiveWords("현재 화면 범위",cameraHint,24,0,120,23,11,CoreMuted).raycastTarget=false;
            mapZoom=Words("100%",footer,1569.6f,28.19f,50.398f,36.797f,23,false,MapFooterInk,TextAnchor.UpperLeft);
            mapZoom.font=ApprovedTheme.Font;mapZoom.color=MapFooterInk;
            RegisterScreen(mapPanel,close);
        }
        void MapHint(Transform footer,float x,float width,float capWidth,float captionX,float keyX,string key,string label,Action action)
        {
            var row=Rect("Map hint "+label,footer,x,21,width,MenuHintHeight * 2);
            if(action!=null){var hit=row.gameObject.AddComponent<Image>();hit.color=Color.clear;var button=row.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>{action();Refresh();});}
            Rounded("Figma map keycap",row,0,0,capWidth,MenuHintHeight * 2,4,1,Color.clear,new Color32(147,156,166,255));
            var keyRoot=Rect("Map key "+key,row,keyX,7.1f,capWidth-keyX*2,30);
            mapFooterPrompts.Add(new MapFooterPrompt{Root=keyRoot,Template=key,Width=capWidth-keyX*2});
            MapFooterWords(Rect("Map caption "+label,row,captionX,7,width-captionX,36.8f),label,MenuHintFontSize * 2);
        }
        void RefreshMapFooterPrompts()
        {
            LayoutMapClose();
            foreach(var entry in mapFooterPrompts)
            {
                // Hidden footers wait for the map to open: each Prompt is a regex pass, run every frame of play (QA 2026-11-16).
                if(!entry.Root||entry.Current!=null&&!entry.Root.gameObject.activeInHierarchy)continue;
                string value=Prompt(entry.Template);
                if(entry.Template=="{MapZoomIn} / {MapZoomOut}")
                {
                    string zoomIn=Prompt("{MapZoomIn}"),zoomOut=Prompt("{MapZoomOut}");
                    value=(zoomIn=="="||zoomIn=="+"?"＋":zoomIn)+" / "+(zoomOut=="-"?"−":zoomOut);
                }
                if(entry.Current==value)continue;
                entry.Current=value;
                for(int i=entry.Root.childCount-1;i>=0;i--){var child=entry.Root.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}
                float width=MapFooterWords(entry.Root,value,MenuHintFontSize * 2);
                // Rebound and pad labels fit without moving the Figma keycaps.
                if(width>entry.Width)foreach(Transform child in entry.Root){if(!child.gameObject.activeSelf)continue;var rect=(RectTransform)child;float scale=entry.Width/width;rect.anchoredPosition*=scale;rect.localScale=new Vector3(scale,1,1);}
            }
        }
        float MapFooterWords(RectTransform parent,string value,int size)
        {
            float x=0;
            for(int start=0;start<value.Length;)
            {
                var primary=ApprovedTheme.Font;
                Font selected=primary.HasCharacter(value[start])?primary:journalFont?journalFont:font;
                int end=start+1;
                while(end<value.Length&&(primary.HasCharacter(value[end])?primary:journalFont?journalFont:font)==selected)end++;
                var text=Words(value.Substring(start,end-start),parent,x,0,1000,40,size,false,MapFooterInk,TextAnchor.UpperLeft);
                text.text=value.Substring(start,end-start);text.font=selected;text.material=null;text.color=MapFooterInk;
                float width=text.preferredWidth;text.rectTransform.sizeDelta=new Vector2(width,40);x+=width;start=end;
            }
            return x;
        }
        void BuildMapLegend(Transform page)
        {
            var viewport=Rect("Map legend viewport",page,720,106.5f,165,340);
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Map legend content",viewport,0,0,153.5f,457);
            mapLegend=viewport.gameObject.AddComponent<ScrollRect>();mapLegend.viewport=viewport;mapLegend.content=content;
            mapLegend.horizontal=false;mapLegend.vertical=true;mapLegend.movementType=ScrollRect.MovementType.Clamped;mapLegend.scrollSensitivity=25;
            LiveWords("지도 표시",content,0,9,65,24,14);
            LiveWords("W/S 스크롤",content,65,12,85,20,11,CoreMuted);
            (string label,Sprite art)[] rows={
                ("현재 위치",MapArt("player")),("살아 있는 적",null),
                ("포탈 1",MapArt("portal-1")),("포탈 2",MapArt("portal-2")),
                ("미탐험 방향",MapArt("unknown")),("다음 챕터 출구",MapWorldArt(9)),
                ("보스 · 엘리트",MapArt("boss-crimson")),("상점",MapArt("workshop-yellow")),
                ("잉크 충전소",MapArt("recovery")),("봉인 도전방",MapArt("seal")),("상자방",MapArt("chest")),
                ("정령 구출",MapWorldArt(0)),("묘목 생존",MapWorldArt(1)),
                ("위험 상자",MapWorldArt(2)),("함정 수집",MapWorldArt(3))};
            var separator=Fill(content,0,177.5f,153.5f,.6f,new Color(.48f,.52f,.55f,.3f));
            separator.name="Map legend sections";separator.raycastTarget=false;
            for(int i=0;i<rows.Length;i++)
            {
                float y=38.5f+i*22+(i>=6?18:0);
                if(rows[i].art)Glyph(rows[i].art,content,0,y,19,19,Color.white).preserveAspect=true;
                else LiveWords("●",content,0,y,19,19,14,new Color32(217,106,102,255),TextAnchor.MiddleCenter);
                var caption=LiveWords(rows[i].label,content,28,y,125,20,12,CoreMuted);if(rows[i].label=="다음 챕터 출구")mapExitLegend=caption;
            }
            LiveRule(content,0,394,153.5f);
            var note=LiveWords("방 색과 아이콘으로 구분합니다. 완료한 이벤트는 옅은 아이콘과 체크로 표시합니다.",content,0,405,153.5f,52,11,CoreMuted);
            note.horizontalOverflow=HorizontalWrapMode.Wrap;
        }
        void ScrollMapLegend()
        {
            // W/S belongs only to the legend; the rebound MapPan action continues to move the map independently.
            if(!mapLegend)return;var keyboard=Keyboard.current;
            float direction=Controls&&Controls.UsingGamepad?Controls.Read(GameAction.Navigate).Y:
                keyboard!=null?(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0):0;
            float range=Mathf.Max(1,mapLegend.content.rect.height-mapLegend.viewport.rect.height);
            mapLegend.verticalNormalizedPosition=Mathf.Clamp01(mapLegend.verticalNormalizedPosition+direction*150*Time.unscaledDeltaTime/range);
        }
        void PaintApprovedMap(Chapter1MapViewport viewport)
        {
            mapGeometry.Present(map,viewport);mapChapter.text=chapter2?"2챕터":"1챕터";mapExitLegend.text=chapter2?"마을 출구":"다음 챕터 출구";
            mapZoom.text=Mathf.RoundToInt(viewport.Zoom/Mathf.Max(.001f,mapFitZoom)*100)+"%";
            mapMarkUsed=0;
            foreach(var room in map.MapData.rooms??Array.Empty<ForestRoom>())
            {
                if(!map.IsRoomDiscovered(room.id))continue;
                var centre=new Vector2((room.x+room.w*.5f)*32,(room.y+room.h*.5f)*32);
                Sprite sprite=null;bool complete=false;string label="";
                if(Chapter1MaintenanceFacility.IsFacility(room.kind)){sprite=MapArt("workshop-yellow");label="상점";}
                else if((room.kind=="boss"&&!map.BossDefeated)||room.kind=="elite"){sprite=MapArt("boss-crimson");label=room.kind=="elite"?"엘리트":"";}
                else if(room.kind=="seal")
                {
                    sprite=MapArt("seal");complete=sealChallenge&&sealChallenge.State==Chapter1SealChallenge.ChallengeState.Succeeded;
                    if(sealChallenge&&sealChallenge.Site!=null)centre=map.WorldMapPoint(sealChallenge.TabletPosition);
                }
                else if(room.kind=="chest") {sprite=MapArt("chest");complete=keyChest&&keyChest.Opened;}
                if(sprite)MapMarker("Room "+room.id+" "+room.kind,centre,sprite,25,label,complete,grounded:room.kind=="seal"&&sealChallenge&&sealChallenge.Site!=null);
            }
            foreach(var passage in map.MapData.passages??Array.Empty<ForestPassage>())
            {
                bool from=map.IsRoomDiscovered(passage.from),to=map.IsRoomDiscovered(passage.to);
                if(from==to)continue;
                // Only show the direction of an unexplored connection, never its room type or contents.
                var box=passage.spaces!=null&&passage.spaces.Length>0?passage.spaces[from?passage.spaces.Length-1:0]:new GroundRect{x=passage.x,y=passage.y,w=passage.w,h=passage.h};
                MapMarker("Unexplored "+passage.from+"-"+passage.to,new Vector2((box.x+box.w*.5f)*32,(box.y+box.h*.5f)*32),MapArt("unknown"),22,grounded:true,extraLift:box.h*16*viewport.Scale(MapSize)+8);
            }
            if(map.TryGetRecoveryMapPoint(out var recovery))MapMarker("Ink recovery",recovery,MapArt("recovery"),25,grounded:true);
            foreach(var enemy in host.Arena.enemies)if(map.TryGetEnemyMapPoint(enemy,out var at))MapMarker("Living enemy",map.WorldMapPoint(enemy.Position+Vector2.up*enemy.size.y*.5f),null,8,"●");
            var hero=host.Arena.Player;
            for(int slot=0;slot<Deviant.Combat.CombatPlayer.PortalCount;slot++)
                if(hero&&hero.HasPortal(slot))MapMarker("Portal "+(slot+1),map.WorldMapPoint(hero.PortalPosition(slot)),MapArt("portal-"+(slot+1)),25,grounded:true);
            if(map.BossDefeated)
            {
                if(chapter2&&chapter2.Exit&&chapter2.Exit.Unlocked)MapMarker("Chapter exit",map.WorldMapPoint(chapter2.Exit.Position),MapWorldArt(9),25,grounded:true);
                else if(boss&&boss.ExitOpen)MapMarker("Chapter exit",map.WorldMapPoint(boss.ExitPosition),MapWorldArt(9),25,grounded:true);
            }
            PaintMapCamera(viewport);
            MapMarker("Current player",map.PlayerMapPoint,MapArt("player"),30,"현재 위치",grounded:true);
            for(int i=mapMarkUsed;i<approvedMapMarks.Count;i++)approvedMapMarks[i].Root.gameObject.SetActive(false);
        }
        void MapMarker(string name,Vector2 point,Sprite sprite,float size,string label="",bool complete=false,bool grounded=false,float extraLift=0)
        {
            if(mapMarkUsed==approvedMapMarks.Count)
            {
                var root=Rect("Map mark",fullMap.Clip,0,0,0,0);
                var icon=Fill(root,0,0,1,1,Color.white);
                var text=LiveWords("",root,-50,-32,100,20,12,CorePaper,TextAnchor.MiddleCenter);
                text.gameObject.AddComponent<Outline>().effectColor=new Color32(24,25,28,255);
                // The approved Korean font has no U+2713 glyph. Draw the two check strokes as UI geometry.
                var check=Rect("Completed event check",root,7,0,13,13);
                var shortStroke=Fill(check,1,6,5,1.5f,new Color32(171,213,193,255));shortStroke.rectTransform.localEulerAngles=new Vector3(0,0,-45);
                var longStroke=Fill(check,4,8,10,1.5f,new Color32(171,213,193,255));longStroke.rectTransform.localEulerAngles=new Vector3(0,0,45);
                approvedMapMarks.Add(new MapMark{Root=root,Icon=icon,Label=text,Check=check});
            }
            var mark=approvedMapMarks[mapMarkUsed++];mark.Root.name=name;mark.Root.gameObject.SetActive(true);
            var screen=map.Viewport.ToScreen(point,MapSize);mark.Root.anchoredPosition=new Vector2(screen.x,-screen.y);
            mark.Icon.name=name.StartsWith("Portal ")?name.Substring(7)+"번 포탈":name;
            mark.Icon.sprite=sprite;mark.Icon.gameObject.SetActive(sprite);mark.Icon.preserveAspect=true;
            float top=grounded?size+MapMarkerGroundGap+extraLift:size/2;
            mark.Icon.rectTransform.sizeDelta=Vector2.one*size;mark.Icon.rectTransform.anchoredPosition=new Vector2(-size/2,top);
            mark.Icon.color=new Color(1,1,1,complete?.35f:1);
            mark.Label.text=label;mark.Label.fontSize=label=="●"?8:12;mark.Label.color=label=="●"?new Color32(217,106,102,255):CorePaper;
            mark.Label.rectTransform.anchoredPosition=new Vector2(-50,label=="●"?10:top+22);
            mark.Check.anchoredPosition=new Vector2(7,grounded?MapMarkerGroundGap+13+extraLift:0);
            mark.Check.gameObject.SetActive(complete);mark.Root.SetAsLastSibling();
        }
        void PaintMapCamera(Chapter1MapViewport viewport)
        {
            var box=map.CameraMapBounds;var pos=viewport.ToScreen(box.position,MapSize);var size=box.size*viewport.Scale(MapSize);int used=0;
            void Dash(float x,float y,float w,float h)
            {
                if(used==mapCameraDashes.Count)mapCameraDashes.Add(Fill(fullMap.Clip,0,0,1,1,new Color32(169,186,203,210)));
                var dash=mapCameraDashes[used++];dash.gameObject.SetActive(true);dash.rectTransform.anchoredPosition=new Vector2(x,-y);dash.rectTransform.sizeDelta=new Vector2(w,h);dash.transform.SetAsLastSibling();
            }
            for(float x=0;x<size.x;x+=8){float length=Mathf.Min(4,size.x-x);Dash(pos.x+x,pos.y,length,1);Dash(pos.x+x,pos.y+size.y,length,1);}
            for(float y=0;y<size.y;y+=8){float length=Mathf.Min(4,size.y-y);Dash(pos.x,pos.y+y,1,length);Dash(pos.x+size.x,pos.y+y,1,length);}
            for(int i=used;i<mapCameraDashes.Count;i++)mapCameraDashes[i].gameObject.SetActive(false);
        }
    }
}
