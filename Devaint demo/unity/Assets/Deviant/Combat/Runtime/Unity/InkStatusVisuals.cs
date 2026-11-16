using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Deviant.Combat
{
    // View only: no colliders, damage calls or Unity-time timers. Animations follow the combat clock (arena.Now), so they
    // freeze with hit-stop and paused menus.
    // Monster statuses from the 54 ink parts use the user-selected sheets (resources effects/ink-status, 2026-11-06):
    //   marks: a row of ink drops over the head in stacking colour order; each pop (MvpInkCued "mark_pop") bursts that drop,
    //          splashes ink behind the body and tints the body with the mark colour for 0.2 s.
    //   root: three ropes wound round the body (front half before the body, back half behind it).
    //   stun: a ring of stars over the head.  poison: skull bubbles at random points of the body, 2.5 per second per stack,
    //          and a purple body tint.
    // Run inks use the user-selected sheets of resources effects/ink-buffs (2026-11-12):
    //   slow: a purple snail crawling over the head and a faint violet body tint, replacing the foot ring.
    //   player: speed wind behind the body, the ink bubble of the normal shield (gain, hit, decaying), the dash shield
    //   crescent and its break, heal crosses with a green flash, red flames for the basic attack buff, blue flames while a
    //   skill carries the skill damage buff, and the rewinding clock of a skill cooldown cut.
    // Shields from skills and older parts, guards and the older frost/freeze/burn slots keep their code-drawn shapes.
    internal sealed class InkStatusVisuals : IDisposable
    {
        const float PPU=32, PopTint=.2f, SkullsPerStackPerSecond=2.5f, LargeBodyWidth=2, BlueFlameTime=1.2f, BlueFade=.2f, BuffBlink=.5f;
        static readonly Color PoisonTint=new Color32(150,60,210,255), HealFlash=new Color32(126,240,140,255), SlowTint=new Color32(126,87,194,255);
        readonly CombatArena arena;
        readonly Dictionary<CombatActor,ActorView> views=new Dictionary<CombatActor,ActorView>();
        readonly List<CombatActor> stale=new List<CombatActor>();
        readonly List<Object> assets=new List<Object>();
        readonly Dictionary<string,Sheet> sheets=new Dictionary<string,Sheet>();
        Sprite capsule,ice,rune,snow,flame;
        Material material,tintMaterial;
        bool sheetsLoaded;
        int revision;

        [Serializable] sealed class SheetInfo{public string name;public int columns,rows,frames,cellWidth,cellHeight;public float pivotX,pivotY;public float[] durationsMs;public bool loop,facing;public string[] variants;}
        [Serializable] sealed class SheetTable{public SheetInfo[] sheets;}
        sealed class Sheet{public SheetInfo Info;public Sprite[][] Frames;public float Length;}

        public InkStatusVisuals(CombatArena arena){this.arena=arena;arena.MvpInkCued+=OnCue;arena.RunInkFxCued+=OnRunInk;}
        public void Refresh()
        {
            revision++;Visit(arena.Player);
            foreach(var enemy in arena.enemies)Visit(enemy);
            stale.Clear();
            foreach(var pair in views)if(!pair.Key || pair.Key.arena!=arena || pair.Value.Seen!=revision)stale.Add(pair.Key);
            foreach(var actor in stale){views[actor].Dispose();views.Remove(actor);}
        }
        ActorView View(CombatActor actor)
        {
            if(!views.TryGetValue(actor,out var view)){EnsureAssets();view=new ActorView(this,actor);views.Add(actor,view);}
            return view;
        }
        void Visit(CombatActor actor)
        {
            if(!actor)return;
            var state=arena.ReadInkStatusVisuals(actor);
            var buffs=actor==arena.Player?arena.ReadRunInkBuffs():default;
            if(!views.ContainsKey(actor) && !state.Any && !buffs.Any)return;
            var view=View(actor);view.Seen=revision;view.Apply(state,buffs,arena.Now);
        }
        void OnCue(MvpInkCue cue)
        {
            if(cue.Kind!="mark_pop" || !cue.Target || cue.Target.arena!=arena || !arena.ShowInkStatusVisuals)return;
            View(cue.Target).Pop(cue.Count,arena.Now);
        }
        void OnRunInk(RunInkFxCue cue)
        {
            if(!arena.ShowInkStatusVisuals || !arena.Player || arena.Player.Dead)return;
            View(arena.Player).RunInk(cue,arena.Now);
        }
        public void Hide(){foreach(var view in views.Values)view.Hide();}
        public void Dispose()
        {
            arena.MvpInkCued-=OnCue;arena.RunInkFxCued-=OnRunInk;
            foreach(var view in views.Values)view.Dispose();views.Clear();
            foreach(var asset in assets)Release(asset);assets.Clear();sheets.Clear();
        }
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
        static float Stroke(float distance,float width)=>Mathf.Clamp01(1-Mathf.Abs(distance)/width);
        static float Segment(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
        static float Polygon(Vector2 p,Vector2[] vertices)
        {
            bool inside=false;float distance=float.MaxValue;
            for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
            {
                var a=vertices[j];var b=vertices[i];distance=Mathf.Min(distance,Segment(p,a,b));
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside?-distance:distance;
        }
        Sprite Texture(string name,int width,int height,Func<Vector2,float> alpha)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {var p=new Vector2((x+.5f)/width-.5f,((y+.5f)/height-.5f)*height/width);pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(alpha(p)));}
            texture.SetPixels(pixels);texture.Apply(false,true);assets.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),width,0,SpriteMeshType.FullRect);
            sprite.name=name;sprite.hideFlags=HideFlags.HideAndDontSave;assets.Add(sprite);return sprite;
        }
        void EnsureAssets()
        {
            if(capsule)return;
            var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default");
            if(shader){material=new Material(shader){name="Ink status unlit",hideFlags=HideFlags.HideAndDontSave};assets.Add(material);}
            // The body tint draws the actor's own silhouette in one colour, the same overlay as the white hit flash.
            tintMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            capsule=Texture("Status shield capsule",128,256,p=>
            {
                float d=new Vector2(p.x,Mathf.Max(0,Mathf.Abs(p.y)-.49f)).magnitude-.46f;
                float fill=d<0?.075f:0;
                float edge=.8f*Stroke(d,.017f)+.18f*Stroke(d,.055f);
                return Mathf.Max(fill,edge);
            });
            var corners=new[]{new Vector2(-.43f,-.58f),new Vector2(-.29f,-.94f),new Vector2(.29f,-.94f),new Vector2(.43f,-.58f),new Vector2(.43f,.58f),new Vector2(.29f,.94f),new Vector2(-.29f,.94f),new Vector2(-.43f,.58f)};
            ice=Texture("Status frozen crystal",128,256,p=>
            {
                float d=Polygon(p,corners);float edge=.85f*Stroke(d,.018f);
                float facets=0;
                if(d<0)facets=.28f*Mathf.Max(Stroke(Segment(p,new Vector2(-.4f,-.5f),new Vector2(.3f,.7f)),.012f),Stroke(Segment(p,new Vector2(.4f,-.55f),new Vector2(-.3f,.35f)),.012f));
                return Mathf.Max(d<0?.085f:0,Mathf.Max(edge,facets));
            });
            rune=Texture("Status guard rune",64,64,p=>
            {
                float outline=.8f*Stroke(p.magnitude-.39f,.025f);
                float diamond=Mathf.Abs(p.x)+Mathf.Abs(p.y)<.27f?.9f:0;
                if(Mathf.Abs(p.x)<.024f && Mathf.Abs(p.y)<.15f)diamond=0;
                return Mathf.Max(outline,diamond);
            });
            snow=Texture("Status frost stack",64,64,p=>
            {
                float a=0;for(int i=0;i<3;i++){float angle=i*Mathf.PI/3;var v=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.4f;a=Mathf.Max(a,Stroke(Segment(p,-v,v),.025f));}return a;
            });
            var fire=new[]{new Vector2(0,.46f),new Vector2(.17f,.08f),new Vector2(.22f,.24f),new Vector2(.31f,-.13f),new Vector2(.14f,-.39f),new Vector2(-.14f,-.39f),new Vector2(-.29f,-.13f),new Vector2(-.12f,.2f),new Vector2(-.09f,-.08f)};
            flame=Texture("Status burn flame",64,64,p=>Polygon(p,fire)<0?.85f:0);
            LoadSheets();
        }
        // resources effects/ink-status/runtime: columns are frames, rows are variants (mark colour, size); the pivot is in
        // pixels from the cell's top-left. A missing deployment leaves those statuses blank instead of breaking combat.
        // resources effects/ink-buffs/runtime uses the same layout (InkBuffFxSheets, InkBuffFx/).
        void LoadSheets()
        {
            if(sheetsLoaded)return;sheetsLoaded=true;
            LoadSheets("InkStatusFxSheets","InkStatusFx");LoadSheets("InkBuffFxSheets","InkBuffFx");
        }
        void LoadSheets(string tableName,string folder)
        {
            var table=Resources.Load<TextAsset>(tableName);
            if(!table){Debug.LogWarning("Ink effect sheets are not deployed ("+tableName+"): run tools/assets/update_assets.py");return;}
            foreach(var info in JsonUtility.FromJson<SheetTable>(table.text).sheets)
            {
                var texture=Resources.Load<Texture2D>(folder+"/"+info.name);
                if(!texture){Debug.LogWarning("Missing ink effect sheet: "+folder+"/"+info.name);continue;}
                var sheet=new Sheet{Info=info,Frames=new Sprite[info.rows][]};
                foreach(var ms in info.durationsMs)sheet.Length+=ms/1000f;
                var pivot=new Vector2(info.pivotX/info.cellWidth,1-info.pivotY/info.cellHeight);
                for(int row=0;row<info.rows;row++)
                {
                    sheet.Frames[row]=new Sprite[info.frames];
                    for(int i=0;i<info.frames;i++)
                    {
                        var sprite=Sprite.Create(texture,new Rect(i*info.cellWidth,texture.height-(row+1)*info.cellHeight,info.cellWidth,info.cellHeight),pivot,PPU,0,SpriteMeshType.FullRect);
                        sprite.name=info.name+" "+(info.variants!=null && row<info.variants.Length?info.variants[row]:row.ToString())+" "+i;
                        sprite.hideFlags=HideFlags.HideAndDontSave;assets.Add(sprite);sheet.Frames[row][i]=sprite;
                    }
                }
                sheets[info.name]=sheet;
            }
        }
        // The frame shown at age: a loop wraps around, a one-shot returns null after its last frame.
        internal Sprite Frame(string name,int variant,float age)
        {
            if(!sheets.TryGetValue(name,out var sheet) || age<0)return null;
            var frames=sheet.Frames[Mathf.Clamp(variant,0,sheet.Frames.Length-1)];
            if(sheet.Info.loop)age=Mathf.Repeat(age,sheet.Length);else if(age>=sheet.Length)return null;
            float t=age*1000;
            for(int i=0;i<frames.Length;i++){if(t<sheet.Info.durationsMs[i])return frames[i];t-=sheet.Info.durationsMs[i];}
            return frames[frames.Length-1];
        }
        internal float Length(string name)=>sheets.TryGetValue(name,out var sheet)?sheet.Length:0;
        internal bool Faces(string name)=>sheets.TryGetValue(name,out var sheet) && sheet.Info.facing;

        sealed class Particle{public SpriteRenderer Renderer;public string Sheet;public int Variant;public float Start;public Vector2 Local;public bool Back,Flip;}
        sealed class ActorView : IDisposable
        {
            readonly InkStatusVisuals owner;
            readonly CombatActor actor;
            readonly GameObject root;
            readonly List<SpriteRenderer> renderers=new List<SpriteRenderer>();
            readonly List<SpriteRenderer> actorRenderers=new List<SpriteRenderer>();
            readonly SpriteRenderer shield,ice,foot,guard,stun,bindFront,bindBack;
            readonly SpriteRenderer speedWind,bubble,dashShield,redBack,redFront,blueBack,blueFront;
            readonly List<SpriteRenderer> backRenderers=new List<SpriteRenderer>();
            readonly SpriteRenderer[] marks=new SpriteRenderer[7],frost=new SpriteRenderer[4],burns=new SpriteRenderer[3];
            readonly List<Particle> particles=new List<Particle>();
            readonly List<SpriteRenderer> pool=new List<SpriteRenderer>();
            readonly System.Random random;
            readonly MaterialPropertyBlock tintBlock=new MaterialPropertyBlock();
            SpriteRenderer actorVisual,tint;
            InkStatusBodyTint follower;
            int life=-1,front=14,back=-2;
            float nextSkull=float.NaN,flashAt=float.NegativeInfinity,blueAt=float.NegativeInfinity;
            bool blueEnhanced;
            Color flashColor;
            public int Seen;
            public ActorView(InkStatusVisuals owner,CombatActor actor)
            {
                this.owner=owner;this.actor=actor;random=new System.Random(actor.ActorId*7919+17);
                root=new GameObject("[Ink status] "+actor.name){hideFlags=HideFlags.DontSave};root.transform.SetParent(owner.arena.transform,false);
                shield=Add("Shield capsule",owner.capsule,new Color(.35f,1,.83f));ice=Add("Frozen crystal",owner.ice,new Color(.4f,.85f,1));
                foot=Add("Slow",null,Color.white);guard=Add("Next-hit guard",owner.rune,new Color(1,.78f,.25f));
                bindBack=Back(Add("Bind back",null,Color.white));bindFront=Add("Bind front",null,Color.white);stun=Add("Stun",null,Color.white);
                speedWind=Back(Add("Speed wind",null,Color.white));bubble=Add("Ink shield",null,Color.white);dashShield=Add("Dash shield",null,Color.white);
                redBack=Back(Add("Red flames back",null,Color.white));redFront=Add("Red flames front",null,Color.white);
                blueBack=Back(Add("Blue flames back",null,Color.white));blueFront=Add("Blue flames front",null,Color.white);
                for(int i=0;i<marks.Length;i++)marks[i]=Add("Mark "+(i+1),null,Color.white);
                for(int i=0;i<frost.Length;i++)frost[i]=Add("Frost "+(i+1),owner.snow,new Color(.5f,.86f,1));
                for(int i=0;i<burns.Length;i++)burns[i]=Add("Burn "+(i+1),owner.flame,new Color(1,.4f,.16f));
            }
            SpriteRenderer Add(string name,Sprite sprite,Color color,bool track=true)
            {
                var go=new GameObject(name);go.transform.SetParent(root.transform,false);
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;renderer.enabled=false;
                if(owner.material)renderer.sharedMaterial=owner.material;
                if(track)renderers.Add(renderer);return renderer;
            }
            SpriteRenderer Back(SpriteRenderer renderer){backRenderers.Add(renderer);return renderer;}
            static void Place(SpriteRenderer renderer,Vector2 position,Vector2 size,bool visible)
            {
                renderer.enabled=visible;if(!visible)return;
                renderer.transform.localPosition=position;var bounds=renderer.sprite.bounds.size;
                renderer.transform.localScale=new Vector3(size.x/bounds.x,size.y/bounds.y,1);
            }
            // Sheet frames are drawn at their native 32 px/u size.
            static void Show(SpriteRenderer renderer,Sprite sprite,Vector2 position,bool flip=false)
            {
                renderer.enabled=sprite;if(!sprite)return;
                renderer.sprite=sprite;renderer.transform.localPosition=position;renderer.transform.localScale=new Vector3(flip?-1:1,1,1);
            }
            float Width=>Mathf.Max(.5f,actor.size.x);
            float Height=>Mathf.Max(.7f,actor.size.y);
            static float Px(float pixels)=>pixels/PPU;
            Vector2 MarkSlot(int index,int count)=>new Vector2((index-(count-1)*.5f)*Px(9),Height*.5f+Px(10));
            Vector2 Feet=>new Vector2(0,-Height*.5f);
            // A run ink took effect on the player: one-shot sheets at the feet, a green flash for heals.
            public void RunInk(RunInkFxCue cue,float now)
            {
                int row=cue.Enhanced?1:0;
                switch(cue.Kind)
                {
                    case RunInkFxKind.Heal:Spawn("heal-crosses",row,now,Feet,false,false);flashColor=HealFlash;flashAt=now;break;
                    case RunInkFxKind.Shield:Spawn("shield-gain",0,now,Feet,false,false);break;
                    case RunInkFxKind.ShieldHit:Spawn("shield-hit",0,now,Feet,false,false);break;
                    case RunInkFxKind.CooldownReduction:Spawn("cdr-clock",row,now,Feet,false,false);break;
                    case RunInkFxKind.SkillDamage:blueAt=now;blueEnhanced=cue.Enhanced;break;
                }
            }
            // A mark pops: its drop bursts over the head, ink splashes behind the body, the body flashes the mark colour.
            public void Pop(int index,float now)
            {
                int i=Mathf.Clamp(index,1,7)-1;
                Spawn("mark-pop",i,now,MarkSlot(i,i+1),false,false);
                Spawn("mark-burst",i,now,new Vector2(Px(random.Next(-4,5)),Px(random.Next(-4,5))),true,random.Next(2)==0);
                flashColor=CombatArena.MvpMarkColors[i];flashAt=now;
            }
            void Spawn(string sheet,int variant,float now,Vector2 local,bool back,bool flip)
            {
                if(owner.Length(sheet)<=0)return;
                SpriteRenderer renderer;
                if(pool.Count>0){renderer=pool[pool.Count-1];pool.RemoveAt(pool.Count-1);}
                else{renderer=Add("Particle",null,Color.white,false);}
                renderer.gameObject.name=sheet;
                particles.Add(new Particle{Renderer=renderer,Sheet=sheet,Variant=variant,Start=now,Local=local,Back=back,Flip=flip});
            }
            void StepParticles(float now,int layer)
            {
                for(int i=particles.Count-1;i>=0;i--)
                {
                    var p=particles[i];var sprite=owner.Frame(p.Sheet,p.Variant,now-p.Start);
                    if(!sprite || now<p.Start-.0001f){p.Renderer.enabled=false;pool.Add(p.Renderer);particles.RemoveAt(i);continue;}
                    Show(p.Renderer,sprite,p.Local,p.Flip);
                    p.Renderer.sortingLayerID=layer;p.Renderer.sortingOrder=p.Back?back:front;
                }
            }
            void SpawnSkulls(int stacks,float now)
            {
                if(stacks<=0){nextSkull=float.NaN;return;}
                if(float.IsNaN(nextSkull))nextSkull=now;
                float every=1/(SkullsPerStackPerSecond*stacks);
                // A long pause (menu, hit-stop catch-up) must not release a flood of skulls at once.
                if(now-nextSkull>.5f)nextSkull=now;
                float halfWidth=Mathf.Max(0,Width*.5f-Px(4)),low=-Height*.5f+Px(6),high=Height*.5f-Px(8);
                while(nextSkull<=now)
                {
                    var at=new Vector2(((float)random.NextDouble()*2-1)*halfWidth,low+(float)random.NextDouble()*Mathf.Max(0,high-low));
                    Spawn("poison-skull",random.NextDouble()<.4?1:0,nextSkull,at,false,false);nextSkull+=every;
                }
            }
            void ApplyTint(int poison,bool slow,float now)
            {
                Color color=default;bool on=false;
                if(slow){color=SlowTint;color.a=.14f;on=true;}
                if(poison>0){color=PoisonTint;color.a=.26f+.1f*Mathf.Sin(now*4*Mathf.PI);on=true;}
                float u=(now-flashAt)/PopTint;
                if(u>=0 && u<1){color=flashColor;color.a=.6f*(1-u);on=true;}
                var visual=actor.visual;
                if(!on || !visual || !visual.enabled || !owner.tintMaterial){if(follower)follower.Wanted=false;if(tint)tint.enabled=false;return;}
                if(!tint || tint.transform.parent!=visual.transform)
                {
                    if(tint)Release(tint.gameObject);
                    var go=new GameObject("[Ink status] body tint"){hideFlags=HideFlags.DontSave};go.transform.SetParent(visual.transform,false);
                    tint=go.AddComponent<SpriteRenderer>();tint.sharedMaterial=owner.tintMaterial;
                    follower=go.AddComponent<InkStatusBodyTint>();follower.Source=visual;
                }
                // Species presenters rewrite sprite and flipX after the arena; the follower copies them again at the end of
                // the frame, so the silhouette is never mirrored against the body.
                follower.Wanted=true;follower.Follow();
                tintBlock.SetColor(ColorId,color);tint.SetPropertyBlock(tintBlock);
            }
            static readonly int ColorId=Shader.PropertyToID("_Color");
            public Color TintColor=>tint && tint.enabled?(tintBlock.GetColor(ColorId)):Color.clear;
            public void Apply(InkStatusVisualSnapshot state,RunInkBuffSnapshot buffs,float now)
            {
                bool transient=particles.Count>0 || now-flashAt<PopTint || now-blueAt<BlueFlameTime;
                if(!actor || !actor.isActiveAndEnabled || (!state.Any && !buffs.Any && !transient)){Hide();return;}
                if(!root.activeSelf)root.SetActive(true);
                root.transform.position=new Vector3(actor.Center.x,actor.Center.y,actor.transform.position.z-.02f);
                root.transform.rotation=Quaternion.identity;
                var scale=owner.arena.transform.lossyScale;root.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
                int layer=0;
                if(life!=state.Life || actorVisual!=actor.visual)
                {
                    life=state.Life;actorVisual=actor.visual;actor.GetComponentsInChildren(true,actorRenderers);
                    int high=10,low=int.MaxValue;layer=actorVisual?actorVisual.sortingLayerID:0;
                    foreach(var renderer in actorRenderers)if(renderer && !renderer.transform.IsChildOf(root.transform) && (!tint || renderer!=tint))
                    {high=Mathf.Max(high,renderer.sortingOrder);low=Mathf.Min(low,renderer.sortingOrder);if(renderer==actorVisual)layer=renderer.sortingLayerID;}
                    if(low==int.MaxValue)low=0;
                    front=high+4;back=low-2;
                    for(int i=0;i<renderers.Count;i++){renderers[i].sortingLayerID=layer;renderers[i].sortingOrder=front+i;}
                    foreach(var renderer in backRenderers)renderer.sortingOrder=back;
                }
                layer=bindBack.sortingLayerID;
                float width=Width,height=Height,top=height*.5f,bottom=-height*.5f;
                float phase=now*5+actor.ActorId*.71f,pulse=1+.025f*Mathf.Sin(phase),offset=actor.ActorId*.37f;
                // The approved shield presentation draws skill guards itself; the capsule then only shows extra ink shield layers.
                bool reviewedGuard=owner.arena.ShieldPresentation?.DisplaysGuard(actor)==true;
                Place(shield,Vector2.zero,new Vector2((width+.32f)*pulse,(height+.4f)*pulse),state.Shield>(reviewedGuard?actor.shield:0));
                Place(ice,Vector2.zero,new Vector2(width+.3f,height+.22f),state.Frozen);
                Place(guard,new Vector2(0,top+.48f),Vector2.one*.25f,state.Guard>0);
                // Root: front and back halves of the same ropes, the large set on bodies 2u wide or more.
                int size=width>=LargeBodyWidth?1:0;
                Show(bindFront,state.Rooted?owner.Frame("bind-front",size,now+offset):null,Vector2.zero);
                Show(bindBack,state.Rooted?owner.Frame("bind-back",size,now+offset):null,Vector2.zero);
                // Head stack: the mark row (oldest left), then the stun stars above it.
                int count=Mathf.Min(state.MarkDisplay,marks.Length);
                for(int i=0;i<marks.Length;i++)Show(marks[i],i<count?owner.Frame("mark-drop",i,now+offset+i*.13f):null,MarkSlot(i,count));
                Show(stun,state.Stunned?owner.Frame("stun-stars",0,now+offset):null,new Vector2(0,top+Px(count>0?17:3)));
                // Slow: the snail crawls over the head, above the marks and the stars.
                Show(foot,state.Slow>0?owner.Frame("slow-snail",0,now+offset):null,new Vector2(0,top+Px(3+(count>0?14:0)+(state.Stunned?18:0))));
                ApplyBuffs(buffs,now);
                for(int i=0;i<frost.Length;i++)Place(frost[i],new Vector2((i-(state.Frost-1)*.5f)*.2f,top+.22f+(count>0?.6f:0)),Vector2.one*.17f,i<state.Frost);
                for(int i=0;i<burns.Length;i++)Place(burns[i],new Vector2((i-1)*width*.28f,bottom+.23f+.055f*Mathf.Sin(phase+i*2)),new Vector2(.2f,.28f),state.Burning);
                SpawnSkulls(state.Poison,now);
                StepParticles(now,layer);
                ApplyTint(state.Poison,state.Slow>0,now);
            }
            // Player run ink buffs. A timed buff blinks in its last half second; sheets marked facing mirror with the body.
            void ApplyBuffs(RunInkBuffSnapshot buffs,float now)
            {
                bool blink=Mathf.FloorToInt(now*12)%2==1;
                bool speed=buffs.SpeedLeft>0 && !(buffs.SpeedLeft<BuffBlink && blink);
                ShowFacing(speedWind,speed?owner.Frame("speed-wind",buffs.SpeedEnhanced?1:0,now):null,"speed-wind");
                bool red=buffs.RedLeft>0 && !(buffs.RedLeft<BuffBlink && blink);int redRow=buffs.RedEnhanced?1:0;
                ShowFacing(redBack,red?owner.Frame("red-flames-back",redRow,now):null,"red-flames-back");
                ShowFacing(redFront,red?owner.Frame("red-flames-front",redRow,now):null,"red-flames-front");
                float blueAge=now-blueAt;bool blue=blueAge>=0 && blueAge<BlueFlameTime;int blueRow=blueEnhanced?1:0;
                ShowFacing(blueBack,blue?owner.Frame("blue-flames-back",blueRow,blueAge):null,"blue-flames-back");
                ShowFacing(blueFront,blue?owner.Frame("blue-flames-front",blueRow,blueAge):null,"blue-flames-front");
                var fade=new Color(1,1,1,blue?Mathf.Clamp01((BlueFlameTime-blueAge)/BlueFade):1);blueBack.color=blueFront.color=fade;
                Show(bubble,buffs.Shield>0?owner.Frame("shield-front",buffs.ShieldDecaying?1:0,now):null,Feet);
                // The dash shield holds its crescent, then breaks apart from the moment it starts to decay.
                bool dash=buffs.DashShield>0;
                ShowFacing(dashShield,dash?(buffs.DashDecayAge<0?owner.Frame("dash-shield",0,now):owner.Frame("dash-shield-break",0,buffs.DashDecayAge)):null,"dash-shield");
            }
            void ShowFacing(SpriteRenderer renderer,Sprite sprite,string sheet)=>Show(renderer,sprite,Feet,actor.facing<0 && owner.Faces(sheet));
            public int ParticleCount(string sheet){int n=0;foreach(var p in particles)if(p.Sheet==sheet && p.Renderer.enabled)n++;return n;}
            public void Hide()
            {
                foreach(var p in particles){p.Renderer.enabled=false;pool.Add(p.Renderer);}
                particles.Clear();nextSkull=float.NaN;flashAt=float.NegativeInfinity;blueAt=float.NegativeInfinity;
                if(follower)follower.Wanted=false;if(tint)tint.enabled=false;
                if(root && root.activeSelf){foreach(var renderer in renderers)if(renderer)renderer.enabled=false;root.SetActive(false);}
            }
            public void Dispose(){Hide();if(tint)Release(tint.gameObject);Release(root);}
        }
        // Test and diagnostics reads.
        internal int ParticleCount(CombatActor actor,string sheet)=>views.TryGetValue(actor,out var view)?view.ParticleCount(sheet):0;
        internal Color BodyTint(CombatActor actor)=>views.TryGetValue(actor,out var view)?view.TintColor:Color.clear;
    }
}
