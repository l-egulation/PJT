using System.Collections.Generic;
using ForestCombatV2;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        sealed class MapView
        {
            public RectTransform Clip;
            public RawImage Terrain;
            public ApprovedMapGeometry Geometry;
            public Image PlayerGlyph;
            // The hero mark: its pivot sits on the hero's map point; the halo pulses and the pointer bobs above it.
            public RectTransform Player, PlayerPointer;
            public Image PlayerHalo;
            public readonly List<Image> Marks = new List<Image>();
            public readonly List<Image> Icons = new List<Image>();
        }
        MapView fullMap, miniMap;
        Chapter1MapViewport miniViewport;
        const float CombatMinimapScale = .82f;
        const float MinimapVisibleWidth=48*32; // About one and a half camera widths, independent of chapter size or full-map zoom.
        const float MapMarkerGroundGap=2;

        double nextMiniMarkers;
        // The full map follows the approved 1920x1080 handoff at half scale; the minimap keeps its HUD frame.
        static readonly Vector2 MapSize=new Vector2(595,340);
        static readonly Color MiniWell=new Color32(20,20,18,205);

        void BuildMap()
        {
            BuildApprovedMap();
            var frame=CombatArt("minimap-frame-compact",rightTop.transform,"minimap",1251,98,394,394f*201/313,contain:false);
            // Scale the whole map together, preserving its proportions, thin frame and right edge.
            frame.rectTransform.anchoredPosition += new Vector2(frame.rectTransform.sizeDelta.x * (1 - CombatMinimapScale), 0);
            frame.rectTransform.localScale = Vector3.one * CombatMinimapScale;
            frame.name="Minimap";frame.raycastTarget=true;
            var open=frame.gameObject.AddComponent<Button>();open.transition=Selectable.Transition.None;open.onClick.AddListener(()=>{if(map)map.SetOpen(true);Refresh();});
            // Keep the live terrain and markers inside the approved frame's straight inner area.
            miniMap=MakeMap(frame.transform,22,22,frame.rectTransform.sizeDelta.x-44,frame.rectTransform.sizeDelta.y-44,false);
            miniMap.Clip.GetComponent<Image>().color=Color.clear;
            miniMap.Terrain.gameObject.SetActive(false);
            miniMap.Geometry=Rect("Local walkable terrain",miniMap.Clip,0,0,miniMap.Clip.sizeDelta.x,miniMap.Clip.sizeDelta.y).gameObject.AddComponent<ApprovedMapGeometry>();
            miniMap.Geometry.raycastTarget=false;miniMap.Geometry.StrokeWidth=2.6f;miniMap.Geometry.FillOpacity=2;
            miniMap.Geometry.transform.SetAsFirstSibling();
            // 「지도 [M]」 inside the minimap's lower right, over the map, in the bound key (user 2026-11-09).
            var mapKey=BindPrompt(Words("",frame.transform,frame.rectTransform.sizeDelta.x-22-216,frame.rectTransform.sizeDelta.y-22-42,210,40,24,false,CombatIvory,TextAnchor.MiddleRight),"지도 [{Map}]");
            mapKey.name="Minimap key";mapKey.raycastTarget=false;
            var mapKeyEdge=mapKey.gameObject.AddComponent<Outline>();mapKeyEdge.effectColor=new Color32(24,20,16,255);mapKeyEdge.effectDistance=new Vector2(2,-2);

        }
        MapView MakeMap(Transform parent,float x,float y,float width,float height,bool interactive)
        {
            var clip=Picture("",parent,x,y,width,height,interactive?Color.clear:MiniWell,interactive).rectTransform;clip.name="Map viewport";
            clip.GetComponent<Image>().sprite=null;
            clip.gameObject.AddComponent<RectMask2D>();
            var terrain=Rect("Explored terrain",clip,0,0,width,height).gameObject.AddComponent<RawImage>();terrain.raycastTarget=false;
            var view=new MapView {Clip=clip,Terrain=terrain};
            // The full map draws the hero at 2x (whole pixels, still crisp) so it stands out of a zoomed-out chapter.
            view.Player=Rect("Player marker",clip,0,0,0,0);view.Player.pivot=new Vector2(.5f,.5f);view.Player.localScale=Vector3.one*(interactive?2:1);
            view.PlayerHalo=Glyph(PlayerHalo,view.Player,0,0,PlayerHaloSize,PlayerHaloSize,PlayerMarkColor);
            view.PlayerHalo.rectTransform.pivot=new Vector2(.5f,.5f);view.PlayerHalo.rectTransform.anchoredPosition=Vector2.zero;
            float heroSize=interactive?IconSize:30;
            view.PlayerGlyph=Glyph(ApprovedFullMapIcon(PlayerIcon),view.Player,-heroSize/2,-heroSize/2,heroSize,heroSize,Color.white);
            // Map positions are feet, not sprite centres. Keep the point exact and lift only its artwork.
            view.PlayerGlyph.rectTransform.anchoredPosition=new Vector2(-heroSize/2,heroSize+MapMarkerGroundGap);
            view.PlayerHalo.rectTransform.anchoredPosition=new Vector2(0,heroSize/2+MapMarkerGroundGap);
            view.PlayerGlyph.preserveAspect=true;
            if(!interactive)view.PlayerHalo.rectTransform.sizeDelta=Vector2.one*30;
            view.PlayerPointer=Glyph(PlayerPointer,view.Player,-PointerPattern[0].Length/2f-1,0,PointerPattern[0].Length+2,PointerPattern.Length+2,Color.white).rectTransform;
            view.PlayerPointer.gameObject.SetActive(interactive);
            if(interactive)
            {
                var trigger=clip.gameObject.AddComponent<EventTrigger>();
                var scroll=new EventTrigger.Entry {eventID=EventTriggerType.Scroll};
                scroll.callback.AddListener(data=>
                {
                    if(!map||map.Viewport==null)return;
                    var pointer=(PointerEventData)data;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(clip,pointer.position,null,out var local);
                    map.Viewport.ZoomAt(pointer.scrollDelta.y>0?1.25f:.8f,new Vector2(local.x,-local.y),MapSize);
                });trigger.triggers.Add(scroll);
                var drag=new EventTrigger.Entry {eventID=EventTriggerType.Drag};
                drag.callback.AddListener(data=>
                {
                    if(!map||map.Viewport==null)return;
                    var delta=((PointerEventData)data).delta/(GetComponent<Canvas>().scaleFactor*MenuStage.localScale.x);
                    map.Viewport.Pan(new Vector2(delta.x,-delta.y),MapSize);
                });trigger.triggers.Add(drag);
            }
            return view;
        }
        void ZoomMap(float factor) {if(map&&map.Viewport!=null)map.Viewport.ZoomAt(factor,MapSize/2,MapSize);}
        void FocusMap() {if(map&&map.Viewport!=null)map.Viewport.Focus(map.PlayerMapPoint,MapSize);}
        // Opening the map and 전체 보기 frame what the run has explored, not the whole unexplored chapter (playtest 2026-11-04).
        void FitMapExplored() {if(map&&map.Viewport!=null){map.Viewport.MaximumZoom=256;map.Viewport.FitTo(map.ExploredBounds,MapSize);mapFitZoom=map.Viewport.Zoom;}}
        bool fullMapShown;
        void RefreshMap()
        {
            miniMap.Clip.parent.gameObject.SetActive(map&&map.MapTexture&&!map.IsOpen);
            if(!map||!map.MapTexture||map.Viewport==null)return;
            if(miniViewport==null||miniViewport.Size!=map.Viewport.Size)
            {
                miniViewport=new Chapter1MapViewport(map.Viewport.Size){MaximumZoom=256};
                var area=miniMap.Clip.sizeDelta;
                miniViewport.ZoomAt((area.x/MinimapVisibleWidth)/miniViewport.Scale(area),area/2,area);
            }
            // Standing on a floor puts useful rooms above the feet; keep more of that space in view.
            var localSize=miniMap.Clip.sizeDelta;
            var focus=map.PlayerMapPoint-Vector2.up*(localSize.y/miniViewport.Scale(localSize)*.18f);
            miniViewport.Focus(focus,localSize);
            PaintMap(miniMap,miniViewport);
            if(map.IsOpen&&!fullMapShown){FitMapExplored();mapFitZoom=map.Viewport.Zoom;mapLegend.verticalNormalizedPosition=1;}
            fullMapShown=map.IsOpen;
            if(map.IsOpen)PaintMap(fullMap,map.Viewport);
        }
        void PaintMap(MapView view,Chapter1MapViewport viewport)
        {
            if(view==fullMap){PaintApprovedMap(viewport);return;}
            var size=view.Clip.sizeDelta;
            view.Geometry.Present(map,viewport);
            var point=viewport.ToScreen(map.PlayerMapPoint,size);
            // Snap the character mark to whole pixels; its halo animates on unscaled time.
            float half=IconSize/2f,reach=half*view.Player.localScale.x;
            view.Player.anchoredPosition=new Vector2(Mathf.Round(point.x-reach)+reach,Mathf.Round(-point.y+reach)-reach);
            float pulse=(float)(Time.unscaledTimeAsDouble%1.2)/1.2f;
            view.PlayerHalo.rectTransform.localScale=Vector3.one*Mathf.Lerp(.55f,1.45f,pulse);
            var halo=PlayerMarkColor;halo.a=(byte)(220*(1-pulse));view.PlayerHalo.color=halo;
            float bob=Mathf.Round(Mathf.PingPong(Time.unscaledTime*5,2));
            view.PlayerPointer.anchoredPosition=new Vector2(view.PlayerPointer.anchoredPosition.x,half+2+PointerPattern.Length+2+bob);
            if(view==miniMap)
            {
                if(Time.unscaledTimeAsDouble<nextMiniMarkers)return;
                nextMiniMarkers=Time.unscaledTimeAsDouble+.1;
            }
            int used=0,usedIcons=0;
            if(map.MapData.rooms!=null)
            {
                foreach(var room in map.MapData.rooms)
                {
                    if(!map.IsRoomDiscovered(room.id))continue;
                    var centre=new Vector2((room.x+room.w*.5f)*32,(room.y+room.h*.5f)*32);
                    if(room.kind=="boss"&&!map.BossDefeated)Icon(centre,BossIcon);
                    else if(Chapter1MaintenanceFacility.IsFacility(room.kind))Icon(centre,ShopIcon);
                    // Gimmick rooms keep their mark until used: the seal until its one try, the chest until opened.
                    else if(room.kind=="seal"&&!(sealChallenge&&sealChallenge.State!=Chapter1SealChallenge.ChallengeState.Dormant&&!(sealChallenge.RewardChest&&!sealChallenge.RewardChest.IsOpen)))Icon(centre,SealIcon);
                    else if(room.kind=="chest"&&!(keyChest&&keyChest.Opened))Icon(centre,ChestIcon);
                }
            }
            if(map.TryGetRecoveryMapPoint(out var recoveryPoint))Icon(recoveryPoint,RecoveryIcon,grounded:true);
            foreach(var enemy in host.Arena.enemies)if(map.TryGetEnemyMapPoint(enemy,out _))Mark(map.WorldMapPoint(enemy.Position+Vector2.up*enemy.size.y*.5f),new Color32(217,106,102,255));
            var hero=host.Arena.Player;
            // Read the committed position for each slot: moving C never moves V's map marker, or vice versa.
            for(int slot=0;slot<Deviant.Combat.CombatPlayer.PortalCount;slot++)
            {
                if(!hero||!hero.HasPortal(slot))continue;
                var position=map.WorldMapPoint(hero.PortalPosition(slot));
                Icon(position,NumberedPortalIcon(slot),28,grounded:true).name=(slot+1)+"번 포탈";
            }
            for(int i=used;i<view.Marks.Count;i++)view.Marks[i].gameObject.SetActive(false);
            for(int i=usedIcons;i<view.Icons.Count;i++)view.Icons[i].gameObject.SetActive(false);
            // Icons over the enemy dots, the hero over everything.
            for(int i=0;i<usedIcons;i++)view.Icons[i].transform.SetAsLastSibling();
            view.Player.transform.SetAsLastSibling();
            void Mark(Vector2 position,Color color)
            {
                // Small round coral marks leave the walkable contours readable in a crowded room.
                if(used==view.Marks.Count)view.Marks.Add(Rounded("Living enemy",view.Clip,0,0,6,6,3,1,color,new Color32(18,16,14,255)));
                var mark=view.Marks[used++];mark.gameObject.SetActive(true);mark.color=color;
                var projected=viewport.ToScreen(position,size);mark.rectTransform.anchoredPosition=new Vector2(Mathf.Round(projected.x-3),Mathf.Round(-projected.y+3));
            }
            Image Icon(Vector2 position,Sprite sprite,float iconSize=24,bool grounded=false)
            {
                sprite=sprite==RecoveryIcon?MapArt("recovery")??sprite:
                    sprite==ShopIcon?MapArt("workshop-yellow")??sprite:ApprovedFullMapIcon(sprite);
                if(usedIcons==view.Icons.Count)view.Icons.Add(Glyph(sprite,view.Clip,0,0,IconSize,IconSize,Color.white));
                var icon=view.Icons[usedIcons++];icon.gameObject.SetActive(true);icon.sprite=sprite;icon.raycastTarget=false;icon.preserveAspect=true;
                icon.name=sprite.name;
                icon.rectTransform.sizeDelta=Vector2.one*iconSize;
                var projected=viewport.ToScreen(position,size);
                float top=grounded?iconSize+MapMarkerGroundGap:iconSize/2;
                icon.rectTransform.anchoredPosition=new Vector2(Mathf.Round(projected.x-iconSize/2f),Mathf.Round(-projected.y+top));
                return icon;
            }
        }
    }
}
