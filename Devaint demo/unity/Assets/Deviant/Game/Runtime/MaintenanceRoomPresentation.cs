using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Game
{
    // Scene composition for the adopted shop art. It owns only its input lease and keyboard handoff.
    [DefaultExecutionOrder(-250)]
    public sealed class MaintenanceRoomPresentation : MonoBehaviour
    {
        [Serializable] public sealed class Art { public string id; public int[] crop; public int width,height; }
        [Serializable] public sealed class Content { public int schema; public string[] first,firstAfterVillage,second; public Art[] art; public float[] speechPitch; public float speechVolume; }
        sealed class Visits { public readonly bool[] seen=new bool[2]; }
        sealed class Site { public Rect room; public Vector2 floor,centre; public int columns; public Transform root,npcFacing,npcUpper,sign,glow,art; public Vector3 npcScale; }
        static readonly ConditionalWeakTable<RunSession,Visits> runVisits=new ConditionalWeakTable<RunSession,Visits>();
        // Real runs remember each chapter's first visit in the profile, so later runs skip the intro and portal call-back.
        public static readonly string[] IntroStories={"maintenance-intro-chapter1","maintenance-intro-chapter2"};
        readonly Visits standaloneVisits=new Visits();
        readonly List<Site> sites=new List<Site>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        ForestCombatWorld first; Chapter2World second; CommonMaintenanceShop shop; CombatArena arena; Camera view;
        Content content; Visits visits; int chapter; bool built,keyboardWasEnabled,profileBacked; CombatKeyboardInput keyboard;
        IControlScope scope; IControlLease combatLease; MaintenanceBubbleView bubble; AudioSource audioSource; AudioClip[] voices;
        public enum Step { Free, Approach, Dialogue, Shop, AwaitDeparture, PortalNotebook }
        PortalNotebookView portalNotebook;
        // Real sketch runs sell from shelves in the room; the artisan only talks (user decision 2026-11-14).
        MaintenanceShelfDisplay shelves;
        bool Shelves=>shelves&&shelves.Active;
        public MaintenanceShelfDisplay ShelfDisplay=>shelves;
        public bool UsesWorldShelves=>Shelves;
        public Step Phase {get;private set;}
        Site active; int line; int lastCharacters; int consumedFrame=-1; float began,lineBegan,lastSound=-1,approachLastX,stuckAt; bool revealed;
        Vector2 departureOrigin; float closedAt;
        public bool Capturing => Phase==Step.Approach||Phase==Step.Dialogue||Phase==Step.PortalNotebook;
        public bool Seen => visits!=null&&visits.seen[chapter]||profileBacked&&Chapter1ProfileHost.Service.Snapshot.HasSeen(IntroStories[chapter]);
        public Vector2 ReviewAnchor => sites.Count>0?sites[0].floor:Vector2.zero;
        public int SiteCount=>sites.Count;
        public string CurrentText=>content==null?null:PlayerControls.Prompt(arena.Player,merchantSmallTalk?MerchantGreetings[0]:Lines[line]);
        // A hero who first met the artisan in the village after an early death (the first-run visit, playtest 2026-11-09)
        // is greeted as that customer: 8 lines instead of 10, the same shop and portal call-back.
        bool MetInVillage=>chapter==0&&profileBacked&&Chapter1ProfileHost.Service.Snapshot.HasSeen(TemporaryGameGui.FirstRunVisitStory);
        string[] Lines=>chapter==1?content.second:MetInVillage?content.firstAfterVillage:content.first;
        // The shop opens on the line before the four call-back lines (wait, portal question, portal rule, goodbye).
        int ShopLine=>chapter==1?2:Lines.Length-5;
        public static void Attach(ForestCombatWorld world,CommonMaintenanceShop service)
        { if(!world||world.villageHub||!world.proceduralRooms||world.dummyTrainingOnly)return;var p=world.GetComponent<MaintenanceRoomPresentation>()??world.gameObject.AddComponent<MaintenanceRoomPresentation>();p.first=world;p.shop=service; }
        public static void Attach(Chapter2World world,CommonMaintenanceShop service)
        { if(!world)return;var p=world.GetComponent<MaintenanceRoomPresentation>()??world.gameObject.AddComponent<MaintenanceRoomPresentation>();p.second=world;p.shop=service; }
        // Enabled presentations, so the per-frame capture check (GUI Update and Refresh, shelf interaction) needs no scene
        // search: FindObjectsByType plus a LINQ closure ran three or more times every frame (QA 2026-11-16).
        static readonly List<MaintenanceRoomPresentation> live=new List<MaintenanceRoomPresentation>();
        void OnEnable(){if(!live.Contains(this))live.Add(this);}
        public static bool IsCapturing(CombatArena owner)
        {
            if(!owner)return false;
            for(int i=live.Count-1;i>=0;i--){var p=live[i];if(!p){live.RemoveAt(i);continue;}if(p.arena==owner&&(p.Capturing||p.consumedFrame==Time.frameCount))return true;}
            return false;
        }
        Site NearbyMerchant()=>sites.FirstOrDefault(s=>s.room.Contains(arena.Player.Position)&&Mathf.Abs(arena.Player.Position.x-s.floor.x)<(Shelves?2:5)&&Mathf.Abs(arena.Player.Position.y-s.floor.y)<2);
        bool merchantSmallTalk;
        static readonly string[] MerchantGreetings={"무슨 일이니? 진열된 물건을 보고 가렴."};
        public bool TryInteractMerchant()
        {
            if(!isActiveAndEnabled||!built||!arena.Player||arena.Player.Dead||Capturing||Phase==Step.AwaitDeparture||shop.IsOpen||arena.Controls.Current.SimulationPaused||arena.Controls.Current.Blocks(InputChannels.Interaction))return false;
            var site=NearbyMerchant();if(site==null)return false;
            if(!Seen){Begin(site);return true;}
            merchantSmallTalk=true;Begin(site);StartTalk(0);return true;
        }
        public bool MayOpenShop()
        { if(!built)return true;if(Shelves||Capturing)return false;if(Phase==Step.Shop)return true;return NearbyMerchant()!=null; }
        public bool TryGetInteractionAnchor(out Vector3 head)
        {
            head=default;
            if(!isActiveAndEnabled||!built||!arena.Player||arena.Player.Dead||Capturing||Phase==Step.AwaitDeparture||shop.IsOpen||arena.Controls.Current.SimulationPaused||arena.Controls.Current.Blocks(InputChannels.Interaction))return false;
            var site=NearbyMerchant();if(site==null)return false;
            head=site.npcFacing.position+Vector3.up*(20f/32+100f/32*site.npcUpper.localScale.y+.5f);
            return true;
        }
        bool Ready=>first?first.Ready:second&&second.Ready;
        void Build()
        {
            arena=first?first.arena:second.arena;view=first?first.View:second.sceneCamera;chapter=second?1:0;
            var run=first?first.GetComponent<Chapter1RunBinding>()?.Run:second.SourceRun;
            visits=run!=null?runVisits.GetValue(run,_=>new Visits()):standaloneVisits;profileBacked=run!=null;
            var json=Resources.Load<TextAsset>("MaintenanceShops/MaintenanceDialogue");
            if(!json)throw new InvalidOperationException("Deploy MaintenanceDialogue and adopted shop images first.");
            content=JsonUtility.FromJson<Content>(json.text);
            if(content.schema!=1||content.first.Length!=10||content.firstAfterVillage==null||content.firstAfterVillage.Length!=8||content.second.Length!=3)throw new InvalidOperationException("Invalid maintenance dialogue data");
            shelves=GetComponent<MaintenanceShelfDisplay>()??gameObject.AddComponent<MaintenanceShelfDisplay>();shelves.Initialize(arena,chapter,first?first.spriteMaterial:second.spriteMaterial);shelves.Resize=Relayout;
            if(first){foreach(var r in first.Data.rooms.Where(r=>Chapter1MaintenanceFacility.IsFacility(r.kind)))AddSite(new Rect(r.x,-(r.y+r.h),r.w,r.h),new Vector2(r.x+r.w*.5f,-(r.y+r.entryY)));}
            else {var r=second.Layout.Room("M");AddSite(r.Bounds,second.Layout.maintenance.Vector);}
            bubble=MaintenanceBubbleView.Create(view);
            voices=new[]{MakeVoice(content.speechPitch[0],1450),MakeVoice(content.speechPitch[1],1100)};
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.ignoreListenerPause=false;audioSource.volume=1;
            arena.ResetCompleted+=OnReset;built=true;
            // Saves that met the artisan before the lock (or left before its call-back) learn the portal from the next run.
            if(chapter==0&&Seen)MarkPortalTaught();
        }
        Sprite ArtSprite(string id)
        {
            if(sprites.TryGetValue(id,out var cached))return cached;
            var a=content.art.First(x=>x.id==id);var t=Resources.Load<Texture2D>("MaintenanceShops/"+id);
            if(!t)throw new InvalidOperationException("Missing adopted maintenance sprite: "+id);
            t.filterMode=FilterMode.Point;var b=a.crop;
            var sprite=Sprite.Create(t,new Rect(b[0],t.height-b[3],b[2]-b[0],b[3]-b[1]),new Vector2(.5f,0),32,0,SpriteMeshType.FullRect);owned.Add(sprite);sprites[id]=sprite;return sprite;
        }
        Transform Place(Site site,string id,Vector2 offset,int order,int heightOverride=0)
        {
            var a=content.art.First(x=>x.id==id);var s=ArtSprite(id);var go=new GameObject(id);go.transform.SetParent(site.root,false);go.transform.localPosition=offset;
            go.transform.localScale=new Vector3(a.width/s.rect.width,(heightOverride>0?heightOverride:a.height)/s.rect.height,1);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=s;sr.sortingLayerID=arena.Player.visual.sortingLayerID;sr.sortingOrder=order;
            var mat=first?first.spriteMaterial:second.spriteMaterial;if(mat)sr.sharedMaterial=mat;
            return go.transform;
        }
        // The stock came out another row length than the drawing placed for the room: swap the drawing and move the artisan,
        // desk, lantern and sign with it. Returns the artisan's new floor point for the shelf.
        Vector2 Relayout(int index,int columns)
        {
            var s=sites[index];var layout=MaintenanceShelfDisplay.Display(chapter,columns);
            s.columns=layout.columns;s.floor=new Vector2(s.centre.x+layout.shift,s.centre.y);s.root.position=s.floor;
            if(s.art)Destroy(s.art.gameObject);s.art=Place(s,layout.Art(chapter),new Vector2(layout.art,0),-18);
            s.sign.localPosition=(Vector3)layout.sign+Vector3.up*SignHeight/32f;
            return s.floor;
        }
        // The approved room display (2026-11-14): one shop drawing per chapter and row length, centred right of the artisan's
        // floor point (MaintenanceShelfDisplay.Display, resources maintenance-shop-shelves-20261114 layout.json). The longer
        // drawings run further right than the room's centre allows, so the artisan stands left of the centre by the layout's
        // shift (chapter 1: 0, 2, 4.5, 7 units for 3-6 places; chapter 2: 1.5 units at 6 places).
        static readonly Vector2[] Lamp={new Vector2(-2.1875f,4.0625f),new Vector2(-3.84375f,4.5f)};
        const int SignHeight=60;
        void AddSite(Rect room,Vector2 floor)
        {
            var layout=MaintenanceShelfDisplay.Display(chapter,shelves.PlannedColumns);var centre=floor;floor.x+=layout.shift;
            shelves.AddSite(room,floor,chapter==0?"chapter1:"+sites.Count:shop.SketchFacility,layout.columns);
            var s=new Site{room=room,floor=floor,centre=centre,columns=layout.columns};s.root=new GameObject("Adopted maintenance shop · "+sites.Count).transform;s.root.SetParent(transform,false);s.root.position=floor;
            s.art=Place(s,layout.Art(chapter),new Vector2(layout.art,0),-18);
            // Desk and character contact lines are the room centre floor, not the backdrop's transparent canvas.
            int playerOrder=arena.Player.visual.sortingOrder;
            Place(s,chapter==0?"stage1-desk":"stage2-desk",Vector2.zero,playerOrder-2);
            var id=chapter==0?"npc1":"npc2";var texture=ArtSprite(id).texture;var width=texture.width;
            var feet=Sprite.Create(texture,new Rect(0,0,width,20),new Vector2(.5f,0),32,0,SpriteMeshType.FullRect);
            var upper=Sprite.Create(texture,new Rect(0,20,width,100),new Vector2(.5f,0),32,0,SpriteMeshType.FullRect);owned.Add(feet);owned.Add(upper);
            s.npcFacing=new GameObject("Merchant facing player").transform;s.npcFacing.SetParent(s.root,false);s.npcFacing.localPosition=new Vector3(.5f,0);
            var f=new GameObject("Merchant grounded feet").AddComponent<SpriteRenderer>();f.transform.SetParent(s.npcFacing,false);f.transform.localPosition=Vector3.zero;f.sprite=feet;f.sortingLayerID=arena.Player.visual.sortingLayerID;f.sortingOrder=playerOrder-3;
            var u=new GameObject("Merchant breathing upper body").AddComponent<SpriteRenderer>();u.transform.SetParent(s.npcFacing,false);u.transform.localPosition=new Vector3(0,20f/32);u.sprite=upper;u.sortingLayerID=arena.Player.visual.sortingLayerID;u.sortingOrder=playerOrder-3;s.npcUpper=u.transform;s.npcScale=u.transform.localScale;
            var material=first?first.spriteMaterial:second.spriteMaterial;
            // Match the hero's ink contour. Separate owned materials keep the two animated slices out of a shared sprite batch.
            var outlineSource=Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);
            foreach(var renderer in new[]{f,u})
            {
                if(outlineSource){var outline=new Material(outlineSource){name="Merchant outline · "+renderer.name};owned.Add(outline);renderer.sharedMaterial=outline;}
                else if(material)renderer.sharedMaterial=material;
            }
            var lamp=Place(s,chapter==0?"lantern-stage1":"lantern",Lamp[chapter],-10);
            s.sign=Place(s,chapter==0?"sign-stage1":"sign-stage2",layout.sign,-10);
            // Pivot at the top chain eye so a sign swings from its mount, never from its centre.
            var signRenderer=s.sign.GetComponent<SpriteRenderer>();var old=signRenderer.sprite;
            var topPivot=Sprite.Create(old.texture,old.rect,new Vector2(.5f,1),32,0,SpriteMeshType.FullRect);owned.Add(topPivot);signRenderer.sprite=topPivot;s.sign.localPosition+=Vector3.up*old.rect.height/32;
            var lightTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var colors=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/32;colors[y*64+x]=new Color(1,.64f,.2f,Mathf.Pow(Mathf.Max(0,1-d),2)*.34f);}lightTexture.SetPixels(colors);lightTexture.Apply();owned.Add(lightTexture);
            var glowSprite=Sprite.Create(lightTexture,new Rect(0,0,64,64),Vector2.one*.5f,32);owned.Add(glowSprite);var g=new GameObject("Breathing lantern light").AddComponent<SpriteRenderer>();g.transform.SetParent(s.root,false);g.transform.localPosition=lamp.localPosition+Vector3.up*.45f;g.sprite=glowSprite;g.sortingOrder=-9;if(material)g.sharedMaterial=material;s.glow=g.transform;
            sites.Add(s);
        }
        void Update()
        {
            if(!built){if(Ready)Build();return;}
            var p=arena.Player;
            if(!p||p.Dead||(first&&first.GetComponent<Chapter1RunBinding>()?.ShowingResult==true)||(second&&second.ShowingResult)){Cancel();return;}
            if(active!=null&&!active.room.Contains(p.Position)){Cancel();return;}
            if(Phase==Step.PortalNotebook){portalNotebook.Tick();return;}
            if(Phase==Step.Free&&!Seen&&!shop.IsOpen&&!arena.Controls.Current.SimulationPaused&&!arena.Controls.Current.Blocks(InputChannels.Combat|InputChannels.Interaction))
            {
                var site=sites.FirstOrDefault(s=>s.room.Contains(p.Position)&&Mathf.Abs(p.Position.y-s.floor.y)<1.25f&&Mathf.Abs(p.Position.x-s.floor.x)<8);
                if(site!=null&&p.grounded&&!p.currentCast&&!p.Dashing)Begin(site);
            }
            if(Phase==Step.Approach){
                if(arena.Controls.Current.SimulationPaused)return;
                float stop=active.floor.x+(p.Position.x<=active.floor.x?-3.8f:3.8f),distance=stop-p.Position.x;
                if(Mathf.Abs(p.Position.x-approachLastX)>.02f){approachLastX=p.Position.x;stuckAt=Time.unscaledTime;}
                if(!p.grounded){p.SubmitInput(default(Deviant.Combat.Contracts.CombatInput));if(Time.unscaledTime-began>6)Cancel();return;}
                if(Mathf.Abs(distance)<.2f||Time.unscaledTime-began>3||Time.unscaledTime-stuckAt>.7f){StartTalk(0);}
                else p.SubmitInput(new Deviant.Combat.Contracts.CombatInput(distance>0?1:-1));
            }
            if(Phase==Step.Shop&&!shop.IsOpen){
                if(chapter==0)AwaitDeparture();
                else Complete();
            }
            if(Phase==Step.AwaitDeparture){
                if(shop.IsOpen){Phase=Step.Shop;return;}
                // Closing the menu returns control. Call back only after actually walking away from the counter.
                float moved=Mathf.Abs(p.Position.x-departureOrigin.x);
                float away=Mathf.Abs(p.Position.x-active.floor.x)-Mathf.Abs(departureOrigin.x-active.floor.x);
                bool leaving=Shelves?!shelves.InShopZone(p.Position):moved>=1.5f&&away>=1.25f;
                if(Time.unscaledTime-closedAt>=.35f&&leaving&&p.grounded&&!p.currentCast&&!p.Dashing&&
                    !arena.Controls.Current.SimulationPaused&&!arena.Controls.Current.Blocks(InputChannels.Combat|InputChannels.Interaction))AcquireTalk(6);
            }
            if(Capturing)
            {
                var input=PlayerControls.For(p);
                if(input&&input.TakeBack()){input.SuppressGameplay();consumedFrame=Time.frameCount;Complete();return;}
                if(input&&Phase==Step.Dialogue&&input.Take(Deviant.Input.Contracts.GameAction.Advance))
                {input.SuppressGameplay();consumedFrame=Time.frameCount;Advance();}
            }
        }
        void Begin(Site site)
        {
            active=site;consumedFrame=Time.frameCount;arena.Player.CancelAction();arena.Player.ClearInputState();keyboard=arena.Player.GetComponent<CombatKeyboardInput>();keyboardWasEnabled=keyboard&&keyboard.enabled;if(keyboardWasEnabled)keyboard.enabled=false;
            scope=arena.Controls.OpenScope("maintenance first visit");scope.BlockInput(InputChannels.Interaction|InputChannels.Navigation);Phase=Step.Approach;began=stuckAt=Time.unscaledTime;approachLastX=arena.Player.Position.x;
        }
        void StartTalk(int index)
        {
            arena.Player.SubmitInput(default(Deviant.Combat.Contracts.CombatInput));arena.Player.velocity=new Vector2(0,arena.Player.velocity.y);arena.Player.SyncFacing(active.floor.x>=arena.Player.Position.x?1:-1);
            combatLease=scope.BlockInput(InputChannels.Combat);Phase=Step.Dialogue;line=index;lastCharacters=0;revealed=false;lastCharacters=0;lineBegan=Time.unscaledTime;
        }
        void AcquireTalk(int index){Begin(active);StartTalk(index);}
        void AwaitDeparture(){Phase=Step.AwaitDeparture;departureOrigin=arena.Player.Position;closedAt=Time.unscaledTime;}
        public void Advance()
        {
            if(Phase!=Step.Dialogue)return;
            if(!revealed&&(Time.unscaledTime-lineBegan)*30<CurrentText.Length){revealed=true;return;}
            if(merchantSmallTalk){Cancel();return;}
            if(line==ShopLine){
                // Reaching the shop counts as the first visit for later runs, even if the call-back is cut short by leaving the room.
                ReleaseControl();MarkProfileSeen();
                // Shelves: the hero browses at once; chapter 1 calls back once it walks out of the shop corner.
                if(Shelves){if(chapter==0)AwaitDeparture();else FinishIntroduction();return;}
                Phase=Step.Shop;
                if(!shop.TryOpen())Complete();return;
            }
            line++;if(line>=Lines.Length){Complete();return;}revealed=false;lastCharacters=0;lineBegan=Time.unscaledTime;
        }
        // The brief portal notice hands off to the illustrated notebook before releasing gameplay.
        void Complete()
        {
            if(merchantSmallTalk){Cancel();return;}
            if(chapter==0&&(Phase==Step.Dialogue||Phase==Step.Approach))
            {
                Phase=Step.PortalNotebook;scope.PauseSimulation();
                if(bubble){bubble.SetBars(false);bubble.Hide();}
                portalNotebook=PortalNotebookView.Create(transform,arena.Player,ClosePortalNotebook);
                return;
            }
            FinishIntroduction();
        }
        public void ClosePortalNotebook()
        {
            if(Phase!=Step.PortalNotebook)return;
            consumedFrame=Time.frameCount;
            if(portalNotebook){portalNotebook.gameObject.SetActive(false);Destroy(portalNotebook.gameObject);portalNotebook=null;}
            FinishIntroduction();
        }
        void FinishIntroduction(){if(visits!=null)visits.seen[chapter]=true;MarkProfileSeen();if(chapter==0)MarkPortalTaught();ReleaseControl();Phase=Step.Free;active=null;if(bubble){bubble.SetBars(false);bubble.Hide();}}
        void MarkProfileSeen(){if(profileBacked)Chapter1ProfileHost.Service.MarkStorySeen(IntroStories[chapter]);}
        void MarkPortalTaught(){if(profileBacked&&!Chapter1ProfileHost.Service.Snapshot.HasSeen(Chapter1ManualCheckpoint.PortalTaughtStory))Chapter1ProfileHost.Service.MarkStorySeen(Chapter1ManualCheckpoint.PortalTaughtStory);}
        void Cancel(){merchantSmallTalk=false;if(portalNotebook){portalNotebook.gameObject.SetActive(false);Destroy(portalNotebook.gameObject);portalNotebook=null;}if(Phase==Step.Shop&&shop&&shop.IsOpen)shop.Close();ReleaseControl();Phase=Step.Free;active=null;if(bubble){bubble.SetBars(false);bubble.Hide();}}
        void ReleaseControl(){combatLease=null;scope?.Dispose();scope=null;if(arena&&arena.Player)arena.Player.ClearInputState();if(keyboardWasEnabled&&keyboard)keyboard.enabled=true;keyboardWasEnabled=false;}
        void OnReset(){Cancel();}
        void LateUpdate()
        {
            if(!built)return;float now=Time.unscaledTime;
            foreach(var s in sites){
                float side=arena.Player.Position.x-(s.floor.x+.5f);if(Mathf.Abs(side)>.3f)s.npcFacing.localScale=new Vector3(side>0?-1:1,1,1);
                float lift=Mathf.Round((1-Mathf.Cos(now*Mathf.PI*2/(chapter==0?1.1f:1.5f)))*(chapter==0?2:1.5f));s.npcUpper.localScale=new Vector3(1,(100+lift)/100,1);s.sign.localRotation=Quaternion.Euler(0,0,Mathf.Sin(now*1.35f)*2.58f);s.glow.localScale=Vector3.one*(2+.2f*Mathf.Sin(now*2.4f));}
            bubble.SetBars(Phase==Step.Approach||Phase==Step.Dialogue);
            if(Phase!=Step.Dialogue){bubble.Hide();return;}
            int count=revealed?CurrentText.Length:Mathf.Min(CurrentText.Length,(int)((now-lineBegan)*30));
            bubble.Show(CurrentText,count,chapter==0?"마을 장인":"수로 정비공",chapter==0,active.floor+new Vector2(.5f,120f/32),now);
            if(!revealed&&count>lastCharacters&&count>0&&count%2==0&&!char.IsWhiteSpace(CurrentText[count-1])&&!char.IsPunctuation(CurrentText[count-1])&&now-lastSound>.055f){lastSound=now;audioSource.pitch=1+(count%5-2)*.035f;audioSource.PlayOneShot(voices[chapter],content.speechVolume);}lastCharacters=count;
        }
        AudioClip MakeVoice(float pitch,float cutoff){var clip=SynthesizeVoice(pitch,cutoff);owned.Add(clip);return clip;}
        // The merchant's letter sound: a falling triangle tone through a one-pole low-pass, 42 ms. The caller owns the clip
        // (the village artisan's bubble uses the same voice).
        public static AudioClip SynthesizeVoice(float pitch,float cutoff)
        {
            const int rate=48000;var samples=new float[2016];float phase=0,filtered=0,alpha=1-Mathf.Exp(-2*Mathf.PI*cutoff/rate);
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;phase+=pitch*Mathf.Pow(.62f,Mathf.Min(1,t/.032f))/rate;float triangle=2/Mathf.PI*Mathf.Asin(Mathf.Sin(phase*2*Mathf.PI));filtered+=alpha*(triangle-filtered);float env=t<.003f?t/.003f:Mathf.Pow(.0001f,(t-.003f)/.035f);samples[i]=filtered*env;}
            var clip=AudioClip.Create("Original synthesized merchant dialogue",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDisable(){live.Remove(this);Cancel();}
        void OnDestroy(){Cancel();if(arena)arena.ResetCompleted-=OnReset;foreach(var o in owned)if(o)Destroy(o);if(bubble)Destroy(bubble.gameObject);}
    }
}
