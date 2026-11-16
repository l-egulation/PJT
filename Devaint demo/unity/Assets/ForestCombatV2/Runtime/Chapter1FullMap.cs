using System;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using Deviant.Control.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // The map owns its controls and cached texture; the scene supplies only data
    // and live positions. This also lets art experiments reuse the existing map.
    public sealed class Chapter1MapSource
    {
        public ForestData Data;
        public CombatArena Arena;
        public CombatDiagnostics Diagnostics;
        public Func<bool> Ready, Visible, BossDefeated;
        public Func<Vector2> CameraPosition;
        public Func<Vector2,Vector2> WorldToMap;
        public string Title = "1챕터 / 전체 지도";
        public string PlatformLabel = "나무 발판";
        public float MinimapTop = 78;
    }

    // Map coordinates use the authored top-left origin, in pixels; no extra camera.
    public sealed class Chapter1MapViewport
    {
        public readonly Vector2 Size;
        public Vector2 Center { get; private set; }
        public float Zoom { get; private set; }=1;
        public float MaximumZoom { get; set; }=16;
        public Chapter1MapViewport(Vector2 size){Size=size;Fit();}
        public void Fit(){Zoom=1;Center=Size/2;}
        // Frames an area (map pixels) with a margin, so a fresh run's explored strip fills the well.
        public void FitTo(Rect bounds,Vector2 viewport,float margin=64)
        {
            Zoom=1;float whole=Scale(viewport);
            var size=bounds.size+Vector2.one*margin*2;
            Zoom=Mathf.Clamp(Mathf.Min(viewport.x/size.x,viewport.y/size.y)/whole,1,MaximumZoom);
            Center=bounds.center;Clamp(viewport);
        }
        public float Scale(Vector2 viewport)=>Mathf.Min(viewport.x/Size.x,viewport.y/Size.y)*Zoom;
        public Vector2 ToScreen(Vector2 point,Vector2 viewport)=>(point-Center)*Scale(viewport)+viewport/2;
        public Vector2 ToMap(Vector2 point,Vector2 viewport)=>(point-viewport/2)/Scale(viewport)+Center;
        public void Focus(Vector2 point,Vector2 viewport){Center=point;Clamp(viewport);}
        public void Pan(Vector2 delta,Vector2 viewport){Center-=delta/Scale(viewport);Clamp(viewport);}
        public void ZoomAt(float factor,Vector2 anchor,Vector2 viewport)
        {
            var before=ToMap(anchor,viewport);Zoom=Mathf.Clamp(Zoom*factor,1,MaximumZoom);
            Center+=before-ToMap(anchor,viewport);Clamp(viewport);
        }
        void Clamp(Vector2 viewport)
        {
            var half=viewport/(2*Scale(viewport));
            Center=new Vector2(half.x>=Size.x/2?Size.x/2:Mathf.Clamp(Center.x,half.x,Size.x-half.x),
                half.y>=Size.y/2?Size.y/2:Mathf.Clamp(Center.y,half.y,Size.y-half.y));
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class Chapter1FullMap : MonoBehaviour
    {
        public ForestCombatWorld World { get; private set; }
        public bool BossDefeated => source?.BossDefeated?.Invoke() ?? false;
        public Chapter1MapViewport Viewport { get; private set; }
        public Chapter1MapViewport MinimapViewport { get; private set; }
        bool CompactHud=>source?.Diagnostics&&source.Diagnostics.CompactHud;
        public Rect MinimapBounds=>CompactHud?new Rect(Screen.width-Mathf.Min(154,Screen.width-24)-10,40,Mathf.Min(154,Screen.width-24),88):new Rect(12,source?.MinimapTop??78,Mathf.Min(260,Screen.width-24),154);
        Vector2 MinimapSize=>new Vector2(MinimapBounds.width-12,MinimapBounds.height-(CompactHud?24:36));
        public bool IsOpen=>scope!=null;
        public Texture2D MapTexture { get; private set; }
        Color32[] mapPixels;
        bool[] discoveredRooms;
        RectInt explored;
        // The discovered rooms and passages in map pixels; the whole map when it has no rooms.
        public Rect ExploredBounds=>discoveredRooms==null||explored.width<=0?new Rect(Vector2.zero,Viewport.Size):new Rect(explored.x*32,explored.y*32,explored.width*32,explored.height*32);
        IControlScope scope;
        bool savedDebugUI;
        Chapter1MapSelector hiddenSelector;
        Chapter1MapSource source;
        bool SourceReady=>source!=null&&source.Arena&&source.Ready();
        bool dragging;
        GUIStyle title,body,small,pin;
        // Brighter floor and edge so the walkable ground reads on the dark map well (playtest 2026-11-04).
        static readonly Color Ground=new Color32(112,134,104,255),Edge=new Color32(214,228,176,255),
            Enemy=new Color32(255,105,105,255),Earth=new Color32(213,167,89,255),Wood=new Color32(180,145,108,255),Player=new Color32(112,239,217,255);
        public void Initialize(ForestCombatWorld world)
        {
            World=world;
            Initialize(new Chapter1MapSource { Data=world.Data, Arena=world.arena, Diagnostics=world.diagnostics,
                Ready=()=>world&&world.Ready, CameraPosition=()=>new Vector2(world.CameraX,world.CameraY),
                BossDefeated=()=> { var encounter=world?world.GetComponent<Chapter1BossEncounter>():null;return encounter&&encounter.Boss&&encounter.Boss.Dead; } });
        }
        public void Initialize(Chapter1MapSource binding)
        {
            if(binding==null||binding.Data==null||!binding.Arena||binding.Ready==null||binding.CameraPosition==null)
                throw new ArgumentException("Map requires authored terrain, combat controls and live camera positions.",nameof(binding));
            SetOpen(false);
            if(MapTexture)Destroy(MapTexture);
            source=binding;
            discoveredRooms=null;mapPixels=null;
            Viewport=new Chapter1MapViewport(new Vector2(source.Data.width,source.Data.height));
            MinimapViewport=new Chapter1MapViewport(Viewport.Size);
            var miniSize=MinimapSize;
            MinimapViewport.ZoomAt(Mathf.Max(1,miniSize.x/(2880*MinimapViewport.Scale(miniSize))),miniSize/2,miniSize);
            var data=source.Data;var pixels=new Color32[data.cols*data.rows];
            for(int y=0;y<data.rows;y++)for(int x=0;x<data.cols;x++)
                if(data.ground[y*data.cols+x]!=0)
                    pixels[(data.rows-1-y)*data.cols+x]=(y==0||data.ground[(y-1)*data.cols+x]==0)?Edge:Ground;
            foreach(var p in data.platforms)
            {
                int y=Mathf.Clamp(p.y/32,0,data.rows-1);
                for(int x=Mathf.Max(0,p.x/32);x<Mathf.Min(data.cols,Mathf.CeilToInt((p.x+p.w)/32f));x++)
                    pixels[(data.rows-1-y)*data.cols+x]=p.earthArtwork?Earth:Wood;
            }
            MapTexture=new Texture2D(data.cols,data.rows,TextureFormat.RGBA32,false){name="Chapter1 cached map",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            if(data.rooms!=null&&data.rooms.Length>0)
            {
                mapPixels=pixels;discoveredRooms=new bool[data.rooms.Length];
                RevealCurrentRoom();
            }
            else {MapTexture.SetPixels32(pixels);MapTexture.Apply(false,true);}
        }
        public bool IsRoomDiscovered(int id)=>discoveredRooms==null||id>=0&&id<discoveredRooms.Length&&discoveredRooms[id];
        public bool TryGetRecoveryMapPoint(out Vector2 point)
        {
            point = default;
            var station = GetComponent<StageRecoveryStation>();
            if (!SourceReady || !station || !station.Placed || station.Remaining <= 0) return false;
            point = MapPoint(station.Position + Vector2.up * .25f);
            foreach (var room in source.Data.rooms)
                if (IsRoomDiscovered(room.id) && new Rect(room.x * 32, room.y * 32, room.w * 32, room.h * 32).Contains(point)) return true;
            return false;
        }
        public bool TryGetEnemyMapPoint(TestEnemy enemy, out Vector2 point)
        {
            point=default;
            if(!SourceReady||!enemy||enemy.Dead||!enemy.isActiveAndEnabled)return false;
            var mapPoint=MapPoint(enemy.Position+Vector2.up*.25f);
            point=mapPoint;
            if(discoveredRooms==null)return true;
            foreach(var room in source.Data.rooms)
                if(IsRoomDiscovered(room.id)&&Contains(room.x,room.y,room.w,room.h))return true;
            foreach(var passage in source.Data.passages)
            {
                if(!IsRoomDiscovered(passage.from)&&!IsRoomDiscovered(passage.to))continue;
                if(passage.spaces!=null)
                {
                    foreach(var space in passage.spaces)
                        if(Contains(space.x,space.y-1,space.w,space.h+4))return true;
                }
                else if(Contains(passage.x,passage.y,passage.w,passage.h))return true;
            }
            return false;
            bool Contains(int x,int y,int w,int h)=>new Rect(x*32,y*32,w*32,h*32).Contains(mapPoint);
        }
        void RevealCurrentRoom()
        {
            if(discoveredRooms==null||!SourceReady||!source.Arena.Player)return;
            // Feet may lie exactly on a room floor (chapter 2 excludes its border tiles).
            // Match room ownership by sampling a quarter unit above the feet, not the excluded floor edge.
            var point=MapPoint(source.Arena.Player.Position+Vector2.up*.25f);
            foreach(var room in source.Data.rooms)
                if(point.x>=room.x*32&&point.x<(room.x+room.w)*32&&point.y>=room.y*32&&point.y<(room.y+room.h)*32)
                {
                    if(discoveredRooms[room.id])return;
                    discoveredRooms[room.id]=true;
                    var data=source.Data;var visible=new Color32[mapPixels.Length];explored=default;
                    foreach(var r in data.rooms)if(discoveredRooms[r.id])CopyArea(r.x-1,r.y-1,r.w+2,r.h+2);
                    for(int i=0;i<data.passages.Length;i++)
                    {
                        var p=data.passages[i];
                        if(!discoveredRooms[p.from]&&!discoveredRooms[p.to])continue;
                        if(p.spaces!=null)
                        {
                            foreach(var space in p.spaces)CopyArea(space.x,space.y-1,space.w,space.h+4);
                        }
                        else if(data.passageSpaces!=null&&i<data.passageSpaces.Length)
                        {
                            var space=data.passageSpaces[i];
                            CopyArea(space.x,space.y-1,space.w,space.h+2);
                        }
                        else CopyArea(p.x,p.y,p.w,p.h);
                    }
                    MapTexture.SetPixels32(visible);MapTexture.Apply(false,false);
                    return;
                    void CopyArea(int x,int y,int w,int h)
                    {
                        int x0=Mathf.Max(0,x),y0=Mathf.Max(0,y),x1=Mathf.Min(data.cols,x+w),y1=Mathf.Min(data.rows,y+h);
                        if(x1>x0&&y1>y0)
                        {
                            if(explored.width<=0)explored=new RectInt(x0,y0,x1-x0,y1-y0);
                            else{int ex=Mathf.Min(explored.xMin,x0),ey=Mathf.Min(explored.yMin,y0);explored=new RectInt(ex,ey,Mathf.Max(explored.xMax,x1)-ex,Mathf.Max(explored.yMax,y1)-ey);}
                        }
                        for(int row=Mathf.Max(0,y);row<Mathf.Min(data.rows,y+h);row++)
                            for(int col=Mathf.Max(0,x);col<Mathf.Min(data.cols,x+w);col++)
                            {
                                int index=(data.rows-1-row)*data.cols+col;
                                visible[index]=mapPixels[index];
                            }
                    }
                }
        }
        public void SetOpen(bool open)
        {
            if(open==IsOpen)return;
            if(open)
            {
                if(!SourceReady||!isActiveAndEnabled)return;
                var next=source.Arena.Controls.OpenScope("chapter1 full map");
                try{next.BlockInput(InputChannels.Combat|InputChannels.Interaction);next.PauseSimulation();scope=next;}
                catch{next.Dispose();throw;}
                if(source.Diagnostics){savedDebugUI=source.Diagnostics.showDebugUI;source.Diagnostics.showDebugUI=false;}
                foreach(var root in gameObject.scene.GetRootGameObjects())
                {
                    var selector=root.GetComponent<Chapter1MapSelector>();
                    if(selector&&selector.enabled){hiddenSelector=selector;selector.enabled=false;break;}
                }
            }
            else
            {
                scope?.Dispose();scope=null;dragging=false;
                if(source?.Diagnostics)source.Diagnostics.showDebugUI=savedDebugUI;
                if(hiddenSelector)hiddenSelector.enabled=true;
                hiddenSelector=null;
            }
        }
        public bool ExternalInput { get; set; }
        public Vector2 PlayerMapPoint => PlayerPoint;
        // A world position on the map (the GUI marks the installed portal with it).
        public Vector2 WorldMapPoint(Vector2 world) => MapPoint(world);
        public ForestData MapData => source?.Data;
        // Source camera coordinates are already in authored top-left map pixels in both chapters.
        public Rect CameraMapBounds => new Rect(source.CameraPosition(),new Vector2(960,540));
        Rect Area=>new Rect(16,88,Mathf.Max(1,Screen.width-32),Mathf.Max(1,Screen.height-146));
        Vector2 MapPoint(Vector2 p)=>source.WorldToMap!=null?source.WorldToMap(p):new Vector2(p.x*32,-p.y*32);
        Vector2 PlayerPoint=>MapPoint(source.Arena.Player.Position);
        public void FocusPlayer(){if(Viewport!=null)Viewport.Focus(PlayerPoint,Area.size);}
        public void FitExplored(){if(Viewport!=null)Viewport.FitTo(ExploredBounds,Area.size);}
        void Update()
        {
            if(!SourceReady)return;
            RevealCurrentRoom();
            if(ExternalInput)return;
            var input=Deviant.Combat.PlayerControls.For(source.Arena.Player);if(!input)return;
            if(input.Take(Deviant.Input.Contracts.GameAction.Map)){input.SuppressGameplay();SetOpen(!IsOpen);return;}
            if(!IsOpen)return;
            input.SuppressGameplay();
            if(input.TakeBack()){SetOpen(false);return;}
            if(input.Take(Deviant.Input.Contracts.GameAction.MapFit))FitExplored();
            if(input.Take(Deviant.Input.Contracts.GameAction.MapFocus))FocusPlayer();
            if(input.Take(Deviant.Input.Contracts.GameAction.MapZoomIn))Viewport.ZoomAt(1.25f,Area.size/2,Area.size);
            if(input.Take(Deviant.Input.Contracts.GameAction.MapZoomOut))Viewport.ZoomAt(.8f,Area.size/2,Area.size);
            var pan=input.Read(Deviant.Input.Contracts.GameAction.MapPan);
            Viewport.Pan(new Vector2(-pan.X,pan.Y)*(320*Time.unscaledDeltaTime),Area.size);
        }
        void OnDisable()=>SetOpen(false);
        void LateUpdate()
        {
            RefreshMinimap();
        }
        public void RefreshMinimap(){if(SourceReady&&!IsOpen)MinimapViewport.Focus(PlayerPoint,MinimapSize);}
        void OnDestroy(){SetOpen(false);if(MapTexture)Destroy(MapTexture);}
        static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        static void Outline(Rect r,Color color)
        {Fill(new Rect(r.x,r.y,r.width,1),color);Fill(new Rect(r.x,r.yMax-1,r.width,1),color);Fill(new Rect(r.x,r.y,1,r.height),color);Fill(new Rect(r.xMax-1,r.y,1,r.height),color);}
        void Styles()
        {
            if(title!=null)return;
            var font=source.Diagnostics?source.Diagnostics.uiFont:null;
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold};title.normal.textColor=new Color32(231,236,216,255);
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=14};body.normal.textColor=new Color32(191,206,195,255);
            small=new GUIStyle(body){fontSize=12,alignment=TextAnchor.MiddleCenter};
            pin=new GUIStyle(body){fontStyle=FontStyle.Bold};pin.normal.textColor=Player;
        }
        public bool ExternalPresentation { get; set; }

        string InputHint(string template) => Deviant.Combat.PlayerControls.Prompt(source.Arena.Player, template);
        void OnGUI()
        {
            if (ExternalPresentation) return;
            if(!SourceReady||(!IsOpen&&source.Visible!=null&&!source.Visible()))return;
            int depth=GUI.depth;var color=GUI.color;var font=GUI.skin.font;
            try
            {
                GUI.depth=-1000;GUI.color=Color.white;if(source.Diagnostics)GUI.skin.font=source.Diagnostics.uiFont;Styles();
                if(!IsOpen){DrawMinimap();return;}
                Fill(new Rect(0,0,Screen.width,Screen.height),new Color32(13,23,25,255));
                GUI.Label(new Rect(24,13,Mathf.Max(120,Screen.width-390),32),source.Title,title);
                string roomName="길목";float nearest=float.PositiveInfinity;
                for(int i=0;i<source.Data.views.Length;i++)if(IsRoomDiscovered(i))
                {var v=source.Data.views[i];float d=(new Vector2(v.x,v.y)-PlayerPoint).sqrMagnitude;if(d<nearest){nearest=d;roomName=v.name;}}
                GUI.Label(new Rect(24,49,Mathf.Max(120,Screen.width-380),25),"현재 위치 / "+roomName+"   /   지도 확인 중 전투 정지",body);
                var area=Area;Viewport.Focus(Viewport.Center,area.size);HandlePointer(area);DrawMap(area);
                float x=Screen.width-346;
                if(GUI.Button(new Rect(x,20,40,30),"−"))Viewport.ZoomAt(.8f,area.size/2,area.size);
                GUI.Label(new Rect(x+44,20,70,30),Mathf.RoundToInt(Viewport.Zoom*100)+"%",small);
                if(GUI.Button(new Rect(x+118,20,40,30),"+"))Viewport.ZoomAt(1.25f,area.size/2,area.size);
                if(GUI.Button(new Rect(x+170,20,152,30),InputHint("지도 닫기 [{Map} / {Cancel}]")))SetOpen(false);
                if(GUI.Button(new Rect(Screen.width-322,55,148,26),InputHint("내 위치로 [{MapFocus}]")))FocusPlayer();
                if(GUI.Button(new Rect(Screen.width-164,55,140,26),InputHint("전체 보기 [{MapFit}]")))FitExplored();
                float y=Screen.height-44;
                Legend(24,y,Edge,"지면");Legend(105,y,Earth,"흙 발판");Legend(211,y,Wood,source.PlatformLabel);Legend(330,y,Player,"현재 위치");Legend(445,y,Enemy,"적 몬스터");
                GUI.Label(new Rect(Mathf.Max(570,Screen.width-385),y,370,26),Deviant.Combat.PlayerControls.For(source.Arena.Player)?.UsingGamepad == true ? InputHint("{MapZoomIn}/{MapZoomOut} 확대, 축소  {MapPan} 이동") : "휠 / + − 확대, 축소     드래그 이동",body);

                // Consume clicks even in the margins so test tooling behind the map cannot react.
                if(Event.current.isMouse||Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            finally{GUI.depth=depth;GUI.color=color;GUI.skin.font=font;}
        }
        void DrawMinimap()
        {
            var panel=MinimapBounds;
            Fill(panel,new Color32(13,23,25,245));Outline(panel,new Color32(68,91,79,255));
            if(CompactHud) {
                var label=new GUIStyle(body){fontSize=10};
                GUI.Label(new Rect(panel.x+6,panel.y+1,panel.width-12,18),InputHint("주변 지도     [{Map}] 전체"),label);
                DrawMap(new Rect(panel.x+6,panel.y+18,MinimapSize.x,MinimapSize.y),MinimapViewport,true);return;
            }
            GUI.Label(new Rect(panel.x+8,panel.y+3,80,24),"미니맵",body);
            Fill(new Rect(panel.x+79,panel.y+11,6,6),Enemy);
            GUI.Label(new Rect(panel.x+89,panel.y+3,28,24),"적",body);
            GUI.Label(new Rect(panel.xMax-102,panel.y+3,96,24),InputHint("{Map} 전체 지도"),body);
            var area=new Rect(panel.x+6,panel.y+29,panel.width-12,118);
            DrawMap(area,MinimapViewport,true);
        }
        void Legend(float x,float y,Color color,string text){Fill(new Rect(x,y+7,9,9),color);GUI.Label(new Rect(x+16,y,105,26),text,body);}
        void HandlePointer(Rect area)
        {
            var e=Event.current;
            if(e.type==EventType.ScrollWheel&&area.Contains(e.mousePosition)){Viewport.ZoomAt(Mathf.Pow(1.15f,-e.delta.y),e.mousePosition-area.position,area.size);e.Use();}
            if(e.type==EventType.MouseDown&&e.button==0&&area.Contains(e.mousePosition)){dragging=true;e.Use();}
            if(e.type==EventType.MouseDrag&&dragging){Viewport.Pan(e.delta,area.size);e.Use();}
            if(e.type==EventType.MouseUp){dragging=false;}
        }
        void DrawMap(Rect area)=>DrawMap(area,Viewport,false);
        void DrawMap(Rect area,Chapter1MapViewport view,bool compact)
        {
            Fill(area,new Color32(20,34,35,255));Outline(area,new Color32(50,70,66,255));
            GUI.BeginGroup(area);
            try
            {
                float scale=view.Scale(area.size);var origin=view.ToScreen(Vector2.zero,area.size);
                var rect=new Rect(origin,view.Size*scale);
                for(float x=origin.x%64;x<area.width;x+=64)Fill(new Rect(x,0,1,area.height),new Color32(28,44,43,255));
                for(float y=origin.y%64;y<area.height;y+=64)Fill(new Rect(0,y,area.width,1),new Color32(28,44,43,255));
                GUI.DrawTexture(rect,MapTexture,ScaleMode.StretchToFill,true);Outline(rect,new Color32(68,91,79,255));
                var camera=view.ToScreen(source.CameraPosition(),area.size);
                Outline(new Rect(camera,new Vector2(960,540)*scale),new Color32(114,155,161,255));
                for(int i=0;i<source.Data.views.Length;i++)
                {
                    if(!IsRoomDiscovered(i))continue;
                    var v=source.Data.views[i];var p=view.ToScreen(new Vector2(v.x,v.y),area.size);
                    Fill(new Rect(p.x-2,p.y-2,4,4),new Color32(183,195,158,255));
                    GUI.Label(new Rect(p.x-18,p.y-25,36,21),(i+1).ToString("00"),small);
                }
                if(source.Data.landmarks!=null)foreach(var landmark in source.Data.landmarks){
                    if(!IsRoomDiscovered(landmark.zone))continue;
                    if(landmark.kind=="boss"&&BossDefeated)continue;
                    var p=view.ToScreen(new Vector2(landmark.x,landmark.y),area.size);
                    Color color=Chapter1MaintenanceFacility.IsFacility(landmark.kind)?new Color32(88,211,177,255):new Color32(236,111,106,255);
                    Fill(new Rect(p.x-5,p.y-5,10,10),color);
                    if(!compact){var label=new GUIStyle(small){alignment=TextAnchor.MiddleLeft};label.normal.textColor=color;
                        GUI.Label(new Rect(Mathf.Clamp(p.x+8,4,Mathf.Max(4,area.width-184)),Mathf.Clamp(p.y+8,0,area.height-26),180,24),landmark.name,label);
                    }
                }
                if (TryGetRecoveryMapPoint(out var recoveryPoint))
                {
                    var p = view.ToScreen(recoveryPoint, area.size);
                    var green = new Color32(107, 224, 161, 255);
                    Fill(new Rect(p.x - 5, p.y - 2, 10, 4), green);
                    Fill(new Rect(p.x - 2, p.y - 5, 4, 10), green);
                    if (!compact) GUI.Label(new Rect(p.x + 8, p.y - 14, 120, 24), "잉크 충전소", small);
                }
                foreach(var enemy in source.Arena.enemies)
                {
                    if(!TryGetEnemyMapPoint(enemy,out var point))continue;
                    var p=view.ToScreen(point,area.size);
                    float radius=compact?2.5f:3.5f;
                    if(p.x<0||p.y<0||p.x>=area.width||p.y>=area.height)continue;
                    Fill(new Rect(p.x-radius-1,p.y-radius-1,radius*2+2,radius*2+2),new Color32(13,23,25,255));
                    Fill(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),Enemy);
                }
                var hero=source.Arena.Player;
                for(int slot=0;hero&&slot<CombatPlayer.PortalCount;slot++)
                {
                    if(!hero.HasPortal(slot))continue;
                    var p=view.ToScreen(WorldMapPoint(hero.PortalPosition(slot)),area.size);
                    var colour=slot==0?new Color32(170,120,236,255):new Color32(112,190,255,255);
                    Fill(new Rect(p.x-12,p.y-12,24,24),new Color32(18,16,14,255));
                    Outline(new Rect(p.x-11,p.y-11,22,22),colour);
                    GUI.Label(new Rect(p.x-11,p.y-11,22,22),(slot+1).ToString(),small);
                }
                var player=view.ToScreen(PlayerPoint,area.size);
                Fill(new Rect(player.x-7,player.y-7,14,14),new Color32(13,23,25,255));
                Outline(new Rect(player.x-7,player.y-7,14,14),Player);Fill(new Rect(player.x-3,player.y-3,6,6),Color.white);
                if(!compact)GUI.Label(new Rect(Mathf.Clamp(player.x+12,4,Mathf.Max(4,area.width-96)),Mathf.Clamp(player.y-13,0,area.height-26),94,26),"현재 위치",pin);
            }
            finally{GUI.EndGroup();}
        }
    }
}
