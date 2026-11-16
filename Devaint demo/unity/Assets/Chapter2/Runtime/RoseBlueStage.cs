using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class RoseBluePlacement
    {
        public string id,kind,assetId,name;public string[] phases;public float x,y,w,h;public int layer;public bool flipX;
        public Vector2 Feet => new Vector2(x,y);
    }
    [Serializable] public sealed class RoseBlueArenaData
    {
        public string schema,layoutStatus,sourceSha256;public float width,height;
        public RoseBluePlacement[] surfaces,markers,devices;
        public static RoseBlueArenaData Load()
        {
            var text=Resources.Load<TextAsset>("RoseBlueArena");
            if(!text)throw new InvalidOperationException("Generate RoseBlueArena.json before starting Chapter2");
            var data=JsonUtility.FromJson<RoseBlueArenaData>(text.text);
            if(data.layoutStatus!="user-approved"||data.width!=30||data.height!=18)throw new InvalidOperationException("Unapproved Rose/Blue arena");
            data.Field(0);return data;
        }
        public Battlefield Field(int seed)=>new Battlefield(new Box(0,0,width,height),surfaces.Where(s=>s.kind=="platform").Select(s=>new Platform(s.x,s.x+s.w,s.y+s.h,s.layer-1)).ToArray(),seed);
        public RoseBluePlacement Marker(string id)=>markers.First(m=>m.id==id);
    }
    public sealed class RoseBlueStage : MonoBehaviour
    {
        public const float Width=30,Height=18;
        public RoseBlueArenaData Data {get;private set;}
        public Battlefield Field {get;private set;}
        public Vector2 Origin {get;private set;}
        public Rect Area=>new Rect(Origin.x,Origin.y,30,18);
        public float Floor=>Origin.y+3;
        public Vector2 CameraCenter=>Origin+new Vector2(15,9);
        public readonly List<ArenaSurface> Surfaces=new List<ArenaSurface>();
        readonly List<GameObject> platforms=new List<GameObject>();
        GameObject floor,walls;
        // v26 보스방 그림(resources environments/stage2-boss-hall-room-v26-20261113, 「보스 길목·보스방 시안」 2차 2026-11-12 OK):
        // 바닥 위 30×15u 뒷배경, 바닥 30u×3u(+이끼 8px). 1페이즈 공중전은 바닥이 없으므로 전장 30×18u 전체를 덮는 공중전 판
        // (바닥 자리까지 벽·수문·물줄기가 아래로 이어진 그림)을 그 위에 켠다(사용자 2026-11-13 「배경을 밑까지 이어야 끊겨 보이지 않는다」).
        // 그림이 배치되지 않았으면 예전처럼 기술용 단색 바닥을 그린다.
        public const string ArtRoot=Chapter2WaterwayArt.Root+"Boss/";
        public const int BackdropOrder=-32,AirBackdropOrder=-31;
        Texture2D floorArt;GameObject air;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public SpriteRenderer Backdrop{get;private set;}
        public bool AirBackdropShown=>air&&air.activeSelf;
        SpriteRenderer Art(string name,string key,Vector2 at,int order,CombatArena arena)
        {
            var t=Resources.Load<Texture2D>(ArtRoot+key);if(!t)return null;
            var sp=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.zero,32,0,SpriteMeshType.FullRect);owned.Add(sp);
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=at;
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sp;r.sortingOrder=order;if(arena.spriteMaterial)r.sharedMaterial=arena.spriteMaterial;return r;
        }
        void OnDestroy(){foreach(var o in owned)if(o)Destroy(o);}
        readonly List<GameObject> entryFloor=new List<GameObject>();
        public void OpenBottomEntries(IEnumerable<C2Port> ports,CombatArena arena)
        {
            var holes=ports.Where(p=>p.enabled&&p.side=="bottom").OrderBy(p=>p.x).ToArray();if(holes.Length==0)return;
            floor.SetActive(false);float left=Area.xMin;
            foreach(var port in holes){float start=Mathf.Clamp(port.x-port.opening/2,Area.xMin,Area.xMax),end=Mathf.Clamp(port.x+port.opening/2,Area.xMin,Area.xMax);
                if(start>left)entryFloor.Add(Surface("Encounter floor beside entrance",new Rect(left,Origin.y,start-left,3),false,arena,false));left=Mathf.Max(left,end);}
            if(left<Area.xMax)entryFloor.Add(Surface("Encounter floor beside entrance",new Rect(left,Origin.y,Area.xMax-left,3),false,arena,false));
            Physics2D.SyncTransforms();
        }
        public Vector2 World(Vector2 local)=>Origin+local;
        public Vector2 PlatformCenter(int index)=>Origin+new Vector2((float)Field[index].Center,(float)Field[index].Top);
        public Rect PlatformDanger(int index)
        {var p=Field[index];return new Rect(Origin.x+(float)p.Left,Origin.y+(float)p.Top,(float)(p.Right-p.Left),(float)Tuning.PlatformDamageHeight);}
        public void Build(CombatArena arena,Vector2 origin,int seed)
        {
            Data=RoseBlueArenaData.Load();Field=Data.Field(seed);Origin=origin;
            floorArt=Resources.Load<Texture2D>(ArtRoot+"boss-floor");
            Backdrop=Art("Rose Blue boss room backdrop","boss-room",origin+new Vector2(0,3),BackdropOrder,arena);
            var airView=Art("Rose Blue phase1 air backdrop","boss-room-air",origin,AirBackdropOrder,arena);air=airView?airView.gameObject:null;
            foreach(var s in Data.surfaces){var go=Surface(s.id,new Rect(origin.x+s.x,origin.y+s.y,s.w,s.h),s.kind=="platform",arena,s.kind=="platform");if(s.kind=="platform")platforms.Add(go);else floor=go;}
            walls=new GameObject("Invisible climbable enclosure");walls.transform.SetParent(transform,false);
            foreach(var rect in new[]{new Rect(origin.x-1,origin.y-5,1,24),new Rect(origin.x+30,origin.y-5,1,24),new Rect(origin.x,origin.y+18,30,1)})
            {var go=Surface("Invisible boundary",rect,false,arena,false,false);go.transform.SetParent(walls.transform,true);}
            walls.SetActive(false);ShowPlatforms(false);
        }
        GameObject Surface(string label,Rect r,bool oneWay,CombatArena arena,bool sketch,bool visible=true)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.position=r.center;
            var body=go.AddComponent<BoxCollider2D>();body.size=r.size;var surface=go.AddComponent<ArenaSurface>();surface.oneWay=oneWay;Surfaces.Add(surface);arena.RegisterFeedbackSurface(body);
            if(visible&&!oneWay&&floorArt)
            {
                // 그림 바닥: 이 조각이 차지하는 전장 x 범위만 잘라 쓴다. 그림 아래 = 전장 y 0, 위 8px 는 이끼(바닥 윗면 위로 넘친다).
                float lx=Mathf.Clamp(r.xMin-Origin.x,0,Width),rx=Mathf.Clamp(r.xMax-Origin.x,0,Width);
                var sp=Sprite.Create(floorArt,new Rect(Mathf.Round(lx*32),0,Mathf.Max(1,Mathf.Round((rx-lx)*32)),floorArt.height),new Vector2(.5f,(r.center.y-Origin.y)*32/floorArt.height),32,0,SpriteMeshType.FullRect);owned.Add(sp);
                var art=go.AddComponent<SpriteRenderer>();art.sprite=sp;art.sortingOrder=1;if(arena.spriteMaterial)art.sharedMaterial=arena.spriteMaterial;
                visible=false;
            }
            if(visible){var view=go.AddComponent<SpriteRenderer>();view.sprite=arena.TechnicalSprite;view.color=sketch?new Color(.84f,.82f,.75f):new Color(.32f,.4f,.39f);view.sortingOrder=1;view.drawMode=SpriteDrawMode.Sliced;view.size=r.size;if(arena.spriteMaterial)view.sharedMaterial=arena.spriteMaterial;}
            if(visible&&oneWay){var tile=Resources.Load<Sprite>("RoseBluePhase1/tile");if(!tile)throw new InvalidOperationException("Deploy the approved phase1 platform tile");var view=go.GetComponent<SpriteRenderer>();view.sprite=tile;view.color=Color.white;view.drawMode=SpriteDrawMode.Tiled;view.size=r.size;}
            return go;
        }
        public void Enclose()=>walls.SetActive(true);
        public void ReleaseEnclosure()=>walls.SetActive(false);
        public void ShowPlatforms(bool active){foreach(var p in entryFloor)p.SetActive(false);foreach(var p in platforms)p.SetActive(active);floor.SetActive(!active);if(air)air.SetActive(active);Physics2D.SyncTransforms();}
        // Remove only the boss encounter rectangle; preserve the portions of shared surfaces outside it.
        public static C2Surface[] Cutout(C2Surface[] surfaces,Rect area)
        {
            var output=new List<C2Surface>();
            foreach(var s in surfaces)
            {
                var a=s.Bounds;if(!a.Overlaps(area)){output.Add(s);continue;}
                float l=Mathf.Max(a.xMin,area.xMin),r=Mathf.Min(a.xMax,area.xMax),b=Mathf.Max(a.yMin,area.yMin),t=Mathf.Min(a.yMax,area.yMax);
                var parts=new[]{new Rect(a.xMin,a.yMin,l-a.xMin,a.height),new Rect(r,a.yMin,a.xMax-r,a.height),new Rect(l,a.yMin,r-l,b-a.yMin),new Rect(l,t,r-l,a.yMax-t)};
                for(int i=0;i<parts.Length;i++){var p=parts[i];if(p.width>.0001f&&p.height>.0001f)output.Add(new C2Surface{id=s.id+"-outside-boss-"+i,x=p.x,y=p.y,w=p.width,h=p.height,oneWay=s.oneWay});}
            }
            return output.ToArray();
        }
    }
}
