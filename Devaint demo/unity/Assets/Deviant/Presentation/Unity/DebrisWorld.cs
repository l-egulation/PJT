using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Unity
{
    // Bodies never enter the gameplay scene; visible meshes have no colliders.
    public sealed class DebrisWorld : IDisposable
    {
        sealed class Slot
        {
            public Rigidbody2D body;
            public PolygonCollider2D collider;
            public Transform display;
            public MeshFilter mesh;
            public MeshRenderer renderer;
            public double expires,nextKick;
            public bool active;
            // Last melee sweep (execution + hit index) that launched this piece; one launch per swing.
            public Guid struckBy;public int struckHit;
        }
        sealed class Surface
        { public Collider2D original,proxy;public GameObject root;public Matrix4x4 matrix;public uint hash;public bool oneWay; }
        sealed class Command { public double at;public Action apply; }
        readonly Scene scene;
        readonly PhysicsScene2D physics;
        readonly Transform output;
        readonly Material material;
        readonly HitFeedbackSettings settings;
        readonly double step;
        readonly List<Slot> slots=new List<Slot>();
        readonly Dictionary<EntityId,Surface> surfaces=new Dictionary<EntityId,Surface>();
        readonly List<EntityId> removed=new List<EntityId>();
        readonly List<Command> commands=new List<Command>();
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        double now,target;
        bool disposed;
        int reservedBodies;
        public int ActiveCount {get;private set;}
        public long KickCount {get;private set;}
        public long StrikeCount {get;private set;}
        public int SurfaceCount=>surfaces.Count;
        public double SimulatedThrough=>now;
        public double Backlog=>Math.Max(0,target-now);
        public string LastError {get;private set;}
        public DebrisWorld(Transform output,Shader shader,HitFeedbackSettings settings,float fixedStep,float initialTime)
        {
            if(!output || !shader || settings==null || !HitFeedbackSettings.Positive(fixedStep))throw new ArgumentException("Explicit debris output and physics step required.");
            this.output=output;this.settings=settings;step=fixedStep;now=target=initialTime;
            scene=SceneManager.CreateScene("Hit feedback debris "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics=scene.GetPhysicsScene2D();
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,name="Debris shared sprite material"};material.SetFloat("_DstBlend",10);
        }
        public bool RegisterSurface(Collider2D source,bool oneWay)
        {
            if(disposed || !source || source.isTrigger)return false;
            // A Tilemap/Box participating in a composite is not an independent
            // physical surface. Resolve the registered source to its actual
            // composite geometry rather than cloning it twice or failing on the
            // already-merged TilemapCollider2D input.
            if(!(source is CompositeCollider2D)&&source.compositeOperation!=Collider2D.CompositeOperation.None)
            {
                var body=source.attachedRigidbody;var ownerComposite=body?body.GetComponent<CompositeCollider2D>():null;
                if(!ownerComposite){LastError="Merged terrain has no bound CompositeCollider2D: "+source.name;return false;}
                return RegisterSurface(ownerComposite,oneWay);
            }
            var id=source.GetEntityId();if(surfaces.ContainsKey(id))return true;
            var root=new GameObject("Debris terrain proxy");SceneManager.MoveGameObjectToScene(root,scene);
            Collider2D proxy=null;
            if(source is BoxCollider2D)proxy=root.AddComponent<BoxCollider2D>();
            else if(source is PolygonCollider2D)proxy=root.AddComponent<PolygonCollider2D>();
            else if(source is EdgeCollider2D)proxy=root.AddComponent<EdgeCollider2D>();
            else if(source is CircleCollider2D)proxy=root.AddComponent<CircleCollider2D>();
            else if(source is CapsuleCollider2D)proxy=root.AddComponent<CapsuleCollider2D>();
            else if(source is CompositeCollider2D composite)
            {
                // Copy actual composite paths, never its source GameObject/scripts.
                proxy=composite.geometryType==CompositeCollider2D.GeometryType.Polygons ? (Collider2D)root.AddComponent<PolygonCollider2D>() : null;
            }
            if(!proxy){Object.Destroy(root);LastError="Unsupported debris terrain: "+source.GetType().Name;return false;}
            if(oneWay)
            {
                var effector=root.AddComponent<PlatformEffector2D>();effector.useOneWay=true;effector.useOneWayGrouping=true;effector.surfaceArc=160;
                proxy.usedByEffector=true;
            }
            var surface=new Surface{original=source,proxy=proxy,root=root,oneWay=oneWay};surfaces.Add(id,surface);CopySurface(surface,true);return true;
        }
        void CopySurface(Surface surface,bool force=false)
        {
            var source=surface.original;
            surface.root.SetActive(source && source.isActiveAndEnabled && source.gameObject.activeInHierarchy);
            if(!source)return;
            var matrix=source.transform.localToWorldMatrix;uint hash=source.GetShapeHash();
            if(!force && matrix==surface.matrix && hash==surface.hash)return;
            surface.matrix=matrix;surface.hash=hash;
            surface.root.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            surface.root.transform.localScale=source.transform.lossyScale;
            surface.proxy.offset=source.offset;
            if(source is BoxCollider2D box){var p=(BoxCollider2D)surface.proxy;p.size=box.size;p.edgeRadius=box.edgeRadius;}
            else if(source is PolygonCollider2D polygon){var p=(PolygonCollider2D)surface.proxy;p.pathCount=polygon.pathCount;for(int i=0;i<p.pathCount;i++)p.SetPath(i,polygon.GetPath(i));}
            else if(source is EdgeCollider2D edge){var p=(EdgeCollider2D)surface.proxy;p.points=edge.points;p.edgeRadius=edge.edgeRadius;}
            else if(source is CircleCollider2D circle)((CircleCollider2D)surface.proxy).radius=circle.radius;
            else if(source is CapsuleCollider2D capsule){var p=(CapsuleCollider2D)surface.proxy;p.size=capsule.size;p.direction=capsule.direction;}
            else if(source is CompositeCollider2D composite)
            {var p=(PolygonCollider2D)surface.proxy;p.pathCount=composite.pathCount;for(int i=0;i<p.pathCount;i++){var points=new Vector2[composite.GetPathPointCount(i)];composite.GetPath(i,points);p.SetPath(i,points);}}
        }
        void Enqueue(double at,Action action)
        {
            var command=new Command{at=Math.Max(now,at),apply=action};int i=commands.Count;
            while(i>0 && commands[i-1].at>command.at)i--;commands.Insert(i,command);
        }
        public bool Spawn(DebrisBakeAsset bake,Vector3 position,Quaternion rotation,Vector3 scale,bool flip,Vector2 incoming,float at,int sortingOrder)
        {
            if(disposed || !bake || bake.version!=1 || !bake.source || bake.pieces==null || surfaces.Count==0 || Backlog>.5 || ActiveCount+reservedBodies+bake.pieces.Length>settings.MaxDebris)return false;
            if(scale.x<=0||scale.y<=0||!bake.IsCurrentSource)return false;
            int count=bake.pieces.Length;reservedBodies+=count;
            Enqueue(at,()=>{try{SpawnNow(bake,position,rotation,scale,flip,incoming,sortingOrder);}finally{reservedBodies-=count;}});return true;
        }
        void SpawnNow(DebrisBakeAsset bake,Vector3 position,Quaternion rotation,Vector3 scale,bool flip,Vector2 incoming,int order)
        {
            if(disposed || !output || !bake || ActiveCount+bake.pieces.Length>settings.MaxDebris)return;
            for(int i=0;i<bake.pieces.Length;i++)
            {
                var piece=bake.pieces[i];Slot slot=slots.Find(s=>!s.active);
                if(slot==null)
                {
                    slot=new Slot();var body=new GameObject("Debris physics body");SceneManager.MoveGameObjectToScene(body,scene);
                    slot.body=body.AddComponent<Rigidbody2D>();slot.body.gravityScale=1;slot.body.linearDamping=.3f;slot.body.angularDamping=.4f;
                    slot.collider=body.AddComponent<PolygonCollider2D>();
                    var display=new GameObject("Debris display",typeof(MeshFilter),typeof(MeshRenderer));display.transform.SetParent(output,false);
                    slot.display=display.transform;slot.mesh=display.GetComponent<MeshFilter>();slot.renderer=display.GetComponent<MeshRenderer>();slot.renderer.sharedMaterial=material;
                    slots.Add(slot);
                }
                var center=piece.center;if(flip)center.x=-center.x;
                slot.body.gameObject.SetActive(true);slot.display.gameObject.SetActive(true);
                slot.body.position=(Vector2)(position+rotation*Vector3.Scale(new Vector3(center.x,center.y,0),scale));slot.body.rotation=rotation.eulerAngles.z;
                slot.body.linearVelocity=new Vector2(incoming.x*settings.DebrisImpulse+(i%3-1)*.65f,settings.DebrisUpward+(i%2)*.5f);
                slot.body.angularVelocity=(i%2==0?1:-1)*(90+i*23);slot.body.mass=Mathf.Max(.01f,piece.area*.04f);
                var hull=new Vector2[piece.hull.Length];for(int p=0;p<hull.Length;p++)hull[p]=Vector2.Scale(piece.hull[p],new Vector2((flip?-1:1)*scale.x,scale.y));
                if(flip)Array.Reverse(hull);slot.collider.pathCount=1;slot.collider.SetPath(0,hull);
                foreach(var other in slots)if(other!=slot && other.active)Physics2D.IgnoreCollision(slot.collider,other.collider,true);
                slot.mesh.sharedMesh=flip?piece.mirroredMesh:piece.mesh;
                slot.display.localScale=scale;slot.renderer.sortingOrder=order;
                properties.Clear();properties.SetTexture("_MainTex",bake.source.texture);properties.SetColor("_Color",new Color(1,1,1,settings.DebrisAlpha));properties.SetFloat("_Desaturate",settings.DebrisDesaturate);slot.renderer.SetPropertyBlock(properties);
                slot.active=true;slot.expires=now+settings.DebrisLifetime;slot.nextKick=0;slot.struckBy=Guid.Empty;slot.struckHit=0;ActiveCount++;CopyPose(slot);
            }
        }
        public void FootSweep(Vector2 from,Vector2 to,float width,float at,float delta)
        {
            if(disposed || from==to || delta<=0)return;
            Enqueue(at,()=>{
                var d=to-from;float speed=Mathf.Min(d.magnitude/delta,settings.DebrisMaxKickSpeed);float direction=d.x>0?1:d.x<0?-1:0;
                var bounds=new Bounds((from+to)*.5f,new Vector3(Mathf.Abs(d.x)+width,Mathf.Abs(d.y)+.5f,10));
                foreach(var slot in slots)
                {
                    if(!slot.active || now<slot.nextKick || !bounds.Intersects(slot.collider.bounds))continue;
                    slot.body.WakeUp();slot.body.AddForce(new Vector2(direction*speed*.12f,settings.DebrisUpward*.4f)*slot.body.mass,ForceMode2D.Impulse);
                    slot.body.AddTorque(-direction*slot.body.mass*2,ForceMode2D.Impulse);slot.nextKick=now+settings.KickCooldown;
                    KickCount++;
                }
            });
        }
        // A player melee sweep box launches every piece it overlaps away from the swing.
        // Each piece reacts once per (execution, hit index) even though the box is sampled every step.
        // exposed(point) lets the caller reject pieces behind solid terrain, like other environment props.
        public void Strike(Vector2 center,Vector2 size,int direction,Guid execution,int hitIndex,float at,Func<Vector2,bool> exposed=null)
        {
            if(disposed || !(size.x>0) || !(size.y>0) || execution==Guid.Empty || settings.DebrisStrikeSpeed<=0 && settings.DebrisStrikeUpward<=0)return;
            Enqueue(at,()=>{
                var box=new Bounds(center,new Vector3(size.x,size.y,10));int n=0;
                foreach(var slot in slots)
                {
                    if(!slot.active || slot.struckBy==execution && slot.struckHit==hitIndex)continue;
                    var piece=slot.collider.bounds;if(!box.Intersects(piece))continue;
                    var point=new Vector2(Mathf.Clamp(piece.center.x,box.min.x,box.max.x),Mathf.Clamp(piece.center.y,box.min.y,box.max.y));
                    if(exposed!=null && !exposed(point))continue;
                    slot.struckBy=execution;slot.struckHit=hitIndex;
                    float side=direction>0?1:direction<0?-1:slot.body.position.x>=center.x?1:-1;
                    // Fixed per-piece spread keeps replays deterministic while the shower does not move as one block.
                    float spread=1+((n*7)%5-2)*.12f,lift=1+((n*5)%3)*.18f;
                    slot.body.WakeUp();
                    slot.body.linearVelocity=new Vector2(side*settings.DebrisStrikeSpeed*spread,settings.DebrisStrikeUpward*lift);
                    slot.body.angularVelocity=-side*(420+(n%4)*90);
                    slot.nextKick=now+settings.KickCooldown;n++;StrikeCount++;
                }
            });
        }
        static void CopyPose(Slot slot)
        {slot.display.SetPositionAndRotation(new Vector3(slot.body.position.x,slot.body.position.y,0),Quaternion.Euler(0,0,slot.body.rotation));}
        public void AdvanceTo(float worldNow)
        {
            if(disposed)return;if(float.IsNaN(worldNow)||float.IsInfinity(worldNow)||worldNow<target)throw new ArgumentException("Debris clock must be monotonic.");target=worldNow;
            bool unloaded=false;
            removed.Clear();foreach(var pair in surfaces)
            {
                if(!pair.Value.original){Object.Destroy(pair.Value.root);removed.Add(pair.Key);}
                else
                {
                    if(pair.Value.root.activeSelf&&(!pair.Value.original.isActiveAndEnabled||!pair.Value.original.gameObject.activeInHierarchy))unloaded=true;
                    CopySurface(pair.Value);
                }
            }
            foreach(var id in removed)surfaces.Remove(id);
            // Geometry unload invalidates unsupported remnants rather than inventing a floor.
            if(removed.Count>0||unloaded)foreach(var slot in slots)if(slot.active)Release(slot);
            for(int iterations=0;iterations<128;iterations++)
            {
                while(commands.Count>0 && commands[0].at<=now+.0000001){var cmd=commands[0];commands.RemoveAt(0);cmd.apply();}
                double remaining=target-now;
                if(remaining<step-.0000001 && (commands.Count==0 || commands[0].at>target))break;
                double dt=Math.Min(step,remaining);if(commands.Count>0)dt=Math.Min(dt,commands[0].at-now);
                if(dt<=.0000001)break;
                if(!physics.Simulate((float)dt))throw new InvalidOperationException("Local debris physics simulation failed.");now+=dt;
                foreach(var slot in slots)if(slot.active){if(now>=slot.expires || slot.body.position.y < -10000)Release(slot);else CopyPose(slot);}
            }
        }
        void Release(Slot slot){slot.active=false;slot.body.linearVelocity=Vector2.zero;slot.body.angularVelocity=0;slot.body.gameObject.SetActive(false);if(slot.display)slot.display.gameObject.SetActive(false);ActiveCount--;}
        public void Reset(float worldNow)
        {commands.Clear();reservedBodies=0;foreach(var slot in slots)if(slot.active)Release(slot);now=target=worldNow;KickCount=0;StrikeCount=0;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;commands.Clear();reservedBodies=0;foreach(var slot in slots){if(slot.display)Object.Destroy(slot.display.gameObject);}slots.Clear();
            surfaces.Clear();if(scene.IsValid() && scene.isLoaded)SceneManager.UnloadSceneAsync(scene);if(material)Object.Destroy(material);ActiveCount=0;
        }
    }
}
