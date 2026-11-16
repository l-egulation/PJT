using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed class Chapter2WaterwayBackground : MonoBehaviour
    {
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<Material> cities=new List<Material>();Camera view;Material material;
        readonly List<SpriteRenderer> walls = new List<SpriteRenderer>();
        public readonly struct WindowPlacement
        {
            public readonly Vector2 Position;
            public readonly Rect Room;
            public WindowPlacement(Vector2 position, Rect room) { Position = position; Room = room; }
        }
        readonly List<WindowPlacement> windows = new List<WindowPlacement>();
        public IReadOnlyList<WindowPlacement> Windows => windows;
        public IReadOnlyList<SpriteRenderer> Walls => walls;
        public void SetWallLightingMaterial(Material value) { foreach (var wall in walls) if (wall) wall.sharedMaterial = value ? value : material; }
        Sprite Sprite(string key){if(!sprites.TryGetValue(key,out var s)){var t=Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+key);if(!t)throw new InvalidOperationException("Missing approved waterway background: "+key);s=UnityEngine.Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.zero,32,0,SpriteMeshType.FullRect);sprites[key]=s;owned.Add(s);}return s;}
        SpriteRenderer Place(string key,Vector2 at,Vector2 scale,int order){var go=new GameObject("Waterway · "+key);go.transform.SetParent(transform,false);go.transform.position=new Vector3(at.x,at.y,0);go.transform.localScale=new Vector3(scale.x,scale.y,1);var r=go.AddComponent<SpriteRenderer>();r.sprite=Sprite(key);r.sharedMaterial=material;r.sortingOrder=order;return r;}
        public void Build(Chapter2World world)
        {
            view=world.sceneCamera;material=world.spriteMaterial;var d=world.Layout;
            if(world.Sewer!=null&&Resources.Load<Texture2D>(Chapter2SewerDressing.Root+"wall-level")){BuildModern(world);return;}
            float left=d.surfaces.Min(s=>s.x)-32,right=d.surfaces.Max(s=>s.x+s.w)+32,bottom=d.surfaces.Min(s=>s.y)-32,top=d.surfaces.Max(s=>s.y+s.h)+32;
            for(float y=Mathf.Floor(bottom/15)*15;y<top;y+=15)for(float x=Mathf.Floor(left/16)*16;x<right;x+=16)walls.Add(Place("wall",new Vector2(x,y),Vector2.one,-50));
            float min=d.rooms.Min(r=>r.y),max=d.rooms.Max(r=>r.y+r.h);int index=0;
            foreach(var room in d.rooms){
                var occupied=new List<Rect>();float scale=Mathf.Min(.34f,(room.h-2)/31f,(room.w-4)/24f);var at=new Vector2(room.x+room.w*.5f-12*scale,room.y+.5f);
                var pipeKey=index%3==0?"pipe":index%3==1?"pipe-outlet":"pipe-return";
                Place(pipeKey,new Vector2(room.x+1,room.y+2),Vector2.one*.5f,-38);
                if(scale>.12f&&room.kind!="start"){
                    Place("portal-view",at,Vector2.one*scale,-42);Place("arch",at,Vector2.one*scale,-40);occupied.Add(new Rect(at.x,at.y,24*scale,31*scale));
                }
                float grade=Mathf.InverseLerp(min,max,room.y+room.h);int count=grade>.72f?3:grade>.4f?1:0;
                for(int n=0;n<count;n++){
                    var pos=new Vector2(room.x+3+n*6,room.y+room.h-6);var box=new Rect(pos.x,pos.y,3,3.5f);
                    if(occupied.Any(r=>r.Overlaps(box))||d.surfaces.Any(s=>s.Bounds.Overlaps(box)))continue;
                    var frame=Place("window-frame",pos,Vector2.one*.5f,-32);Place("grille",pos+Vector2.one*.5f,Vector2.one*.5f,-30);
                    windows.Add(new WindowPlacement(pos, new Rect(room.x, room.y, room.w, room.h)));
                    var city=GameObject.CreatePrimitive(PrimitiveType.Quad);city.name="Window distant city";city.transform.SetParent(transform,false);city.transform.position=pos+new Vector2(1.5f,1.75f);city.transform.localScale=new Vector3(2,2.5f,1);Destroy(city.GetComponent<Collider>());
                    var shader=world.windowCityShader?world.windowCityShader:Shader.Find("Deviant/Chapter2/WindowCity");if(!shader)throw new InvalidOperationException("Waterway window shader missing");var mat=new Material(shader);mat.mainTexture=Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+"city");mat.SetFloat("_Phase",(index++*.137f)% .8f);cities.Add(mat);owned.Add(mat);var renderer=city.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.sortingOrder=-34;
                }
            }
            if(d.bossHall!=null&&d.bossHall.w>0)BuildBossHall(world,d.bossHall,d.bossHallToLeft,ref index);
        }
        // v26 화풍: 뒷벽·문·등·채광·기둥 받침·깊이감·웅덩이 물은 Chapter2SewerDressing 이 Chapter2SewerPlan 대로 놓는다.
        // 예전 벽 그림·아치·방 배관·창(바깥 도시)과 초근경 버팀벽은 두지 않는다. 보스 앞 통로 장식은 유지한다.
        public void AddLit(SpriteRenderer r){walls.Add(r);if(r)r.sharedMaterial=material;}
        void BuildModern(Chapter2World world)
        {
            var d=world.Layout;
            float left=d.surfaces.Min(s=>s.x)-32,right=d.surfaces.Max(s=>s.x+s.w)+32,bottom=d.surfaces.Min(s=>s.y)-32,top=d.surfaces.Max(s=>s.y+s.h)+32;
            int index=0;
            if(d.bossHall!=null&&d.bossHall.w>0){if(!BuildPaintedBossHall(d.bossHall,d.bossHallToLeft))BuildBossHall(world,d.bossHall,d.bossHallToLeft,ref index);}
            var dressing=gameObject.AddComponent<Chapter2SewerDressing>();dressing.Build(world,this,Rect.MinMaxRect(left,bottom,right,top));
            gameObject.AddComponent<Chapter2SewerActorLight>().Bind(world,dressing);
        }
        // v26 화풍의 보스 앞 통로: 「보스 길목·보스방 시안」 2차(2026-11-12 사용자 OK)를 그린 뒷배경 한 장(48×6u, 등빛·어둠이 구워져 있다)과
        // 바닥 격자 줄 한 장(resources environments/stage2-boss-hall-room-v26-20261113). 보스방이 통로 왼쪽이면 좌우를 뒤집는다. 예전 장식은 두지 않는다.
        public const int PaintedHallOrder=-32,PaintedHallGrateOrder=2;
        public SpriteRenderer PaintedHall{get;private set;}
        bool BuildPaintedBossHall(C2Bounds hall,bool toLeft)
        {
            var back=Resources.Load<Texture2D>(RoseBlueStage.ArtRoot+"boss-hall");var grates=Resources.Load<Texture2D>(RoseBlueStage.ArtRoot+"boss-hall-grates");if(!back||!grates)return false;
            SpriteRenderer Put(string name,Texture2D t,float y,int order)
            {
                var sp=UnityEngine.Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,0),32,0,SpriteMeshType.FullRect);owned.Add(sp);
                var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=new Vector3(hall.x+hall.w/2,y,0);
                var r=go.AddComponent<SpriteRenderer>();r.sprite=sp;r.sharedMaterial=material;r.sortingOrder=order;r.flipX=toLeft;BossHallDecorCount++;return r;
            }
            PaintedHall=Put("Waterway boss hall · painted",back,hall.y,PaintedHallOrder);
            Put("Waterway boss hall · floor grates",grates,hall.y-(grates.height+1)/32f,PaintedHallGrateOrder);
            return true;
        }
        // 보스 앞 48u 통로 장식(그림 배경이 배치되지 않았을 때). 사용자 검토 시안(2026-11-11 「일단 이대로」)의 좌표를 그대로 옮긴다.
        // 시안 좌표: 앞 방 벽 바깥면 0, 통로 안쪽 3~51, 보스방 벽 51~52, 바닥 0 위가 +. 보스방이 왼쪽이면 좌우를 뒤집는다.
        // ① 평범한 수로(창 두 개·세로 배관) ② 배관 통로(가로 배관·아치 홈) ③ 보스 예고(Rose 화살·명중 자국·올라간 수문).
        public int BossHallDecorCount{get;private set;}
        static readonly (float x,float y,int frame)[] HallStains={(37,3.2f,3),(41.5f,1.6f,2),(44,4.2f,3),(46.5f,2.4f,2),(48.6f,3.6f,3),(50,1.2f,2)};
        static readonly (float x,float y,float rot)[] HallArrows={(36.5f,2.8f,-.18f),(39.2f,4.1f,.12f),(40.8f,1.2f,-.32f),(42.6f,3.1f,.05f),(43.8f,.25f,-.9f),(45,4.6f,-.1f),(45.9f,1.9f,.22f),
            (47.1f,3.3f,-.2f),(47.9f,.25f,-.75f),(48.8f,4.4f,.16f),(49.4f,2.2f,-.08f),(50.1f,3.7f,.3f),(50.6f,1.1f,-.25f),(46.6f,.25f,-1f)};
        public static int BossHallArrowCount=>HallArrows.Length;
        Sprite Centered(string key,Rect? rect=null)
        {
            string id=key+"@center"+(rect.HasValue?"#"+rect.Value.x:"");
            if(sprites.TryGetValue(id,out var s))return s;
            var t=Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+key);if(!t)throw new InvalidOperationException("Missing approved waterway background: "+key);
            s=UnityEngine.Sprite.Create(t,rect??new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),32,0,SpriteMeshType.FullRect);sprites[id]=s;owned.Add(s);return s;
        }
        void BuildBossHall(Chapter2World world,C2Bounds hall,bool toLeft,ref int index)
        {
            Vector2 At(float x,float y)=>new Vector2(toLeft?hall.x+hall.w-(x-3):hall.x+(x-3),hall.y+y);
            // x·y는 그림 왼쪽 아래(시안 기준). 회전은 시안 화면(아래로 +y)의 시계 방향 라디안
            SpriteRenderer Deco(string name,string key,float x,float y,float scale,int order,bool flip=false,float rot=0,float alpha=1,Rect? rect=null)
            {
                var sp=Centered(key,rect);float w=sp.rect.width*scale/32,h=sp.rect.height*scale/32;
                var go=new GameObject(name);go.transform.SetParent(transform,false);
                go.transform.position=At(x+w/2,y+h/2);go.transform.localScale=new Vector3(scale,scale,1);
                go.transform.rotation=Quaternion.Euler(0,0,(toLeft?1:-1)*rot*Mathf.Rad2Deg);
                var r=go.AddComponent<SpriteRenderer>();r.sprite=sp;r.sharedMaterial=material;r.sortingOrder=order;r.flipX=flip^toLeft;r.color=new Color(1,1,1,alpha);
                BossHallDecorCount++;return r;
            }
            var room=new Rect(hall.x,hall.y,hall.w,hall.h);
            foreach(float wx in new[]{6f,11f})
            {
                var frame=Deco("Waterway · window-frame","window-frame",wx,1.5f,.5f,-32);Deco("Waterway · grille","grille",wx+.5f,2f,.5f,-30);
                var pos=(Vector2)frame.bounds.min;windows.Add(new WindowPlacement(pos,room));
                var city=GameObject.CreatePrimitive(PrimitiveType.Quad);city.name="Window distant city";city.transform.SetParent(transform,false);city.transform.position=pos+new Vector2(1.5f,1.75f);city.transform.localScale=new Vector3(2,2.5f,1);Destroy(city.GetComponent<Collider>());
                var shader=world.windowCityShader?world.windowCityShader:Shader.Find("Deviant/Chapter2/WindowCity");if(!shader)throw new InvalidOperationException("Waterway window shader missing");
                var mat=new Material(shader);mat.mainTexture=Resources.Load<Texture2D>(Chapter2WaterwayArt.Root+"city");mat.SetFloat("_Phase",(index++*.137f)%.8f);cities.Add(mat);owned.Add(mat);
                var renderer=city.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.sortingOrder=-34;
            }
            Deco("Waterway boss hall · pipe","pipe",15,-.4f,.5f,-38);
            foreach(float ax in new[]{20.5f,27.5f}){Deco("Waterway boss hall · portal-view","portal-view",ax,-.1f,.2f,-42);Deco("Waterway boss hall · arch","arch",ax,-.1f,.2f,-40);}
            Deco("Waterway boss hall · pipe-horizontal","pipe-horizontal",17.5f,2.1f,.5f,-38);
            Deco("Waterway boss hall · pipe-horizontal","pipe-horizontal",25.5f,2.1f,.5f,-38,flip:true);
            Deco("Waterway boss hall · pipe-outlet","pipe-outlet",33,2.4f,.5f,-38);
            // 명중 자국은 7칸 시트의 한 칸(64px), 시안에서 칸 가운데를 (x,y)에 둔다
            foreach(var s in HallStains)Deco("Waterway boss hall · arrow impact","rose-arrow-impact",s.x-1,s.y-1,1,-36,alpha:.55f,rect:new Rect(s.frame*64,0,64,64));
            foreach(var a in HallArrows)Deco("Waterway boss hall · rose arrow","rose-arrow",a.x,a.y,1,-35,flip:true,rot:a.rot);
            Deco("Waterway boss hall · gate","gate",45.6f,4.4f,.3f,-39);
        }
        void LateUpdate(){if(!view)return;foreach(var m in cities)m.SetFloat("_Camera",view.transform.position.x*.015f*32/960f);}
        void OnDestroy(){foreach(var o in owned)if(o)Destroy(o);}
    }
}
