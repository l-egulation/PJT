using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace Deviant.Integration
{
    public enum HitFeedbackEffect { HitVfx, HitFlash, CorpseDebris, CameraKick, WorldDistortion, PlayerDeathSlow }

    [DisallowMultipleComponent,DefaultExecutionOrder(750)]
    public sealed class HitFeedbackBridge:MonoBehaviour
    {
        sealed class Flash {public SpriteRenderer source,overlay;public float until;}
        sealed class Death
        {public long serial;public CombatActor actor;public CombatActorHandle handle;public Sprite sprite;public Vector2 incoming;public bool emitDebris;}
        CombatArena arena;
        HitFeedbackSettings settings;
        bool paperStyleConfigured,showPaperStyle,invalidOutlineColor;
        Color paperOutlineColor;
        string outlineColorInput;
        HitFeedbackTimeData time;
        HitFeedbackSession session;
        PrefabCueFactory factory;
        GameObject root;
        DebrisWorld debris;
        IDisposable meleeObservation;
        DebrisCatalog debrisCatalog;
        DebrisCatalog suppliedDebris;
        CameraKickMixer kick;
        CameraOutputBinding.KickSource kickSource;
        WorldDistortionOutput distortion;
        IControlScope slowScope;
        IControlLease playerDeathSlow;
        CombatOutcomeCursor cursor;
        readonly CombatAcceptedHit[] pageBuffer=new CombatAcceptedHit[128];
        readonly Dictionary<EntityId,Flash> flashes=new Dictionary<EntityId,Flash>();
        readonly List<EntityId> staleFlashes=new List<EntityId>();
        readonly Dictionary<int,CombatAcceptedHit> lethal=new Dictionary<int,CombatAcceptedHit>();
        readonly Dictionary<int,CombatActor> targets=new Dictionary<int,CombatActor>();
        readonly Dictionary<int,Death> deaths=new Dictionary<int,Death>();
        readonly List<int> stale=new List<int>();
        long serial,lastTick;
        int surfaceCursor;
        bool playerDeathHandled;
        bool configured,closing,physicsFailed;
        int disabledEffects;
        [Range(0,1)] public float motionIntensity=1;
        // F1 size of every hit VFX over its recipe scale; taken once from the data (prefabScale) and kept across resets.
        float vfxScale=-1;
        public bool Ready=>session!=null&&!closing;
        public int ActiveHits=>session?.ActiveCount??0;
        public int ActiveDebris=>debris?.ActiveCount??0;
        public long DebrisKicks=>debris?.KickCount??0;
        public long DebrisStrikes=>debris?.StrikeCount??0;
        public long StartedHits=>session?.Started??0;
        public long LostHitFacts {get;private set;}
        public string LastError {get;private set;}
        public string LastHitVfxKey {get;private set;}
        public Color PaperOutlineColor=>paperOutlineColor;
        public void SetPaperOutlineColor(Color color)
        {
            if(!Ready)throw new InvalidOperationException("Feedback session is not ready.");
            factory.PaperOutlineColor=color;paperOutlineColor=color;
            outlineColorInput="#"+ColorUtility.ToHtmlStringRGB(color);invalidOutlineColor=false;
        }
        public WorldDistortionOutput Distortion=>distortion;
        public static HitFeedbackBridge Attach(CombatArena arena,DebrisCatalog debrisBindings=null)
        {
            if(!arena||!arena.Player)throw new ArgumentException("Initialized combat required.");
            var existing=arena.GetComponent<HitFeedbackBridge>();
            if(existing)
            {
                if(!existing.configured){existing.suppliedDebris=debrisBindings;existing.Configure(arena);}
                return existing;
            }
            var bridge=arena.gameObject.AddComponent<HitFeedbackBridge>();bridge.suppliedDebris=debrisBindings;bridge.Configure(arena);return bridge;
        }
        void Configure(CombatArena owner)
        {
            arena=owner;configured=true;
            arena.ResetCompleted+=BindSession;arena.SimulationCompleted+=CompletedStep;
            arena.FeedbackAccentAdvanced+=AdvanceAccent;arena.Deactivated+=Close;
            arena.Player.Died+=OnPlayerDied;
            arena.DrawFeedbackOptions+=DrawOptions;
            BindSession();
        }
        void BindSession()
        {
            if(!isActiveAndEnabled||!arena||!arena.isActiveAndEnabled){Close();return;}
            Close();closing=false;
            try
            {
                var source=Resources.Load<TextAsset>("HitFeedback/HitFeedback");var timeSource=Resources.Load<TextAsset>("HitFeedback/HitFeedbackTime");
                var bindings=Resources.Load<HitFeedbackBindings>("HitFeedback/Generated/Bindings");
                if(!source||!timeSource||!bindings)throw new InvalidOperationException("Hit feedback bindings are not restored/generated. Run the licensed package restore and binding builder.");
                settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(source.text));time=JsonUtility.FromJson<HitFeedbackTimeData>(timeSource.text);time.Validate();
                LastHitVfxKey=null;
                foreach(var key in settings.RecipeKeys)
                    if(!Array.Exists(bindings.prefabs,b=>b!=null&&b.key==key&&b.prefab))
                        throw new InvalidOperationException("Hit VFX catalog changed. Rebuild Hit Feedback Bindings: "+key);
                root=new GameObject("Combat hit feedback output");root.transform.SetParent(transform,false);
                factory=new PrefabCueFactory(root.transform,bindings,settings.MaxInstances,settings.MaxParticles);
                if(vfxScale<0)vfxScale=settings.PrefabScale;
                if(!paperStyleConfigured)
                {
                    ColorUtility.TryParseHtmlString(settings.PaperOutlineColor,out paperOutlineColor);
                    outlineColorInput=settings.PaperOutlineColor;
                    paperStyleConfigured=true;
                }
                factory.PaperOutlineColor=paperOutlineColor;
                // Fixed by data (paperInk); no runtime comparison switches the style.
                factory.PaperInk=settings.PaperInk;
                session=new HitFeedbackSession(settings.MaxHits);
                kick=new CameraKickMixer(settings.MaxHits,settings.CameraMax);
                kickSource=CameraOutputBinding.GetOrCreate(arena.ViewCamera).BindKickSource(kick);
                var waveShader=bindings.distortionShader;
                if(waveShader)distortion=new WorldDistortionOutput(arena.ViewCamera,waveShader,settings.MaxWaves,settings.WaveMaxPixels);
                debrisCatalog=suppliedDebris?suppliedDebris:Resources.Load<DebrisCatalog>("HitFeedback/Generated/Debris");
                WarmAssets(bindings);
                debris=new DebrisWorld(root.transform,Shader.Find("Deviant/HitFeedback/Particle"),settings,Time.fixedDeltaTime,arena.Now);
                // Player melee sweep boxes (also whiffs) launch lying debris; they never become combat hits.
                meleeObservation=arena.ObserveEnvironmentContacts();
                slowScope=arena.Controls.OpenScope("player-death feedback");
                playerDeathHandled=arena.Player.Dead;
                surfaceCursor=0;physicsFailed=false;LastError=null;RegisterSurfaces();
                arena.TryGetCursor(out cursor);lastTick=arena.SimulationTickCount;
            }
            catch(Exception error){LastError=error.Message;Close();Debug.LogWarning("Hit feedback disabled: "+error.Message);}
        }
        // Reads every debris bake and VFX prefab while the session opens (behind the stage's load fade). Otherwise the
        // first death searched the catalog and read each bake from disk mid-fight, and each VFX prefab read itself on
        // its first hit: the largest synchronous loads in the fight frames (playtest 2026-11-04 lag analysis).
        public int WarmedAssetParts{get;private set;}
        void WarmAssets(HitFeedbackBindings bindings)
        {
            int touched=0;
            if(debrisCatalog&&debrisCatalog.bakes!=null)foreach(var bake in debrisCatalog.bakes)if(bake&&bake.source)touched+=bake.pieces?.Length??0;
            if(bindings.prefabs!=null)foreach(var binding in bindings.prefabs)if(binding!=null&&binding.prefab)touched+=binding.prefab.GetComponentsInChildren<Transform>(true).Length;
            WarmedAssetParts=touched;
        }
        void RegisterSurfaces()
        {
            while(surfaceCursor<arena.FeedbackSurfaces.Count)
            {
                var surface=arena.FeedbackSurfaces[surfaceCursor++];if(!surface)continue;
                var marker=surface.GetComponent<ArenaSurface>();
                if(!marker || !debris.RegisterSurface(surface,marker.oneWay))
                {physicsFailed=true;LastError="Unsupported/unregistered debris terrain geometry: "+surface.GetType().Name+" "+surface.name+"; "+debris.LastError;}
            }
        }
        // The player opens a new input session without a combat reset (CombatPlayer.ResetPlayer at arena start, stage
        // entry, respawn). Rebind then, as CombatFeelFeedback does; otherwise the cursor stays on the old session and
        // no accepted hit is ever read, so no prefab plays.
        bool FollowSession()
        {
            if(!arena||!arena.ReadModel.TryGetSession(out var current)||current.Equals(cursor.Session))return false;
            BindSession();return true;
        }
        void CompletedStep(CombatCompletedStep step)
        {
            if(!Ready)return;
            if(FollowSession())return;
            if(!step.Session.Equals(cursor.Session)||step.Tick!=lastTick+1){physicsFailed=true;LastError="Missing, duplicate or stale completed simulation step.";return;}
            lastTick=step.Tick;RegisterSurfaces();
            if(arena.FootSweepOverflow){physicsFailed=true;LastError="Player foot sweep buffer overflow; debris requires reset.";}
            if(physicsFailed)return;
            try
            {
                foreach(var sweep in arena.CompletedFootSweeps)debris.FootSweep(new Vector2(sweep.FromX,sweep.FromY),new Vector2(sweep.ToX,sweep.ToY),sweep.Width,step.Now,step.Delta);
                StrikeDebris(step.Now);
                debris.AdvanceTo(step.Now);
            }
            catch(Exception error){physicsFailed=true;LastError="Debris output stopped: "+error.Message;}
        }
        void StrikeDebris(float at)
        {
            if(debris.ActiveCount==0||!arena.Player)return;
            int player=arena.Player.ActorId;
            foreach(var sample in arena.CompletedEnvironmentAttacks)
            {
                if(sample.Origin.Kind!=CombatHitKind.Melee||sample.Origin.EffectiveSource.ActorId!=player)continue;
                var origin=new Vector2(sample.OriginX,sample.OriginY);
                // Same solid-terrain rule as other environment props: a swing does not reach debris through a wall.
                debris.Strike(new Vector2(sample.X,sample.Y),new Vector2(sample.Width,sample.Height),sample.Direction,sample.Origin.ExecutionId,sample.Origin.HitIndex,at,
                    point=>arena&&!arena.Occluded(origin,point));
            }
        }
        void AdvanceAccent(float delta)
        {
            if(!Ready)return;kick.Advance(delta);distortion?.Advance(delta);
        }
        void OnPlayerDied()
        {
            // This native event also covers lethal fall damage. A raw cleanup
            // call to Die while alive, a stale session, or repeated notification
            // must not restart/extend the death accent.
            if(!Ready||playerDeathHandled||!arena||!arena.Player||!arena.Player.Dead||
                !arena.ReadModel.TryGetSession(out var current)||!current.Equals(cursor.Session))return;
            playerDeathHandled=true;
            if(!IsEffectEnabled(HitFeedbackEffect.PlayerDeathSlow))return;
            try
            {
                if(slowScope is IFeedbackTimeScope scope)playerDeathSlow=scope.RequestSlow(time.scale,time.accentSeconds);
            }
            catch(Exception error){LastError="Player death accent failed: "+error.Message;}
        }
        public bool IsEffectEnabled(HitFeedbackEffect effect)
        {
            if((uint)effect>(uint)HitFeedbackEffect.PlayerDeathSlow)throw new ArgumentOutOfRangeException(nameof(effect));
            return (disabledEffects & (1<<(int)effect))==0;
        }
        public void SetEffectEnabled(HitFeedbackEffect effect,bool enabled)
        {
            if(IsEffectEnabled(effect)==enabled)return;
            int bit=1<<(int)effect;
            disabledEffects=enabled?disabledEffects & ~bit:disabledEffects | bit;
            if(enabled)return;
            switch(effect)
            {
                case HitFeedbackEffect.HitVfx:session?.ClearActive();break;
                case HitFeedbackEffect.HitFlash:
                    foreach(var flash in flashes.Values){flash.until=float.NegativeInfinity;if(flash.overlay)flash.overlay.enabled=false;}
                    break;
                case HitFeedbackEffect.CorpseDebris:
                    foreach(var death in deaths.Values)death.emitDebris=false;
                    if(arena)debris?.Reset(arena.Now);
                    break;
                case HitFeedbackEffect.CameraKick:kick?.Clear();break;
                case HitFeedbackEffect.WorldDistortion:distortion?.Clear();break;
                case HitFeedbackEffect.PlayerDeathSlow:playerDeathSlow?.Dispose();playerDeathSlow=null;break;
            }
        }
        public void SetMotionIntensity(float value)
        {if(!Ready)return;kick.Intensity=value;if(distortion!=null)distortion.Intensity=value;motionIntensity=value;}
        void DrawToggle(HitFeedbackEffect effect,string label)
        {
            bool before=IsEffectEnabled(effect),after=GUILayout.Toggle(before,label);
            if(before!=after)SetEffectEnabled(effect,after);
        }
        void DrawOptions()
        {
            if(!isActiveAndEnabled||!Ready)return;
            GUILayout.Label("명중, 화면 효과 개별 설정");
            DrawToggle(HitFeedbackEffect.HitVfx,"명중 VFX");
            // Whole-picture size over every recipe's own scale; starts at the data's prefabScale, not saved.
            GUILayout.Label("명중 VFX 크기: "+Mathf.RoundToInt(vfxScale*100)+"% (데이터 "+Mathf.RoundToInt(settings.PrefabScale*100)+"%)");
            vfxScale=GUILayout.HorizontalSlider(vfxScale,.2f,1.5f);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("30%"))vfxScale=.3f;
            if(GUILayout.Button("50%"))vfxScale=.5f;
            if(GUILayout.Button("75%"))vfxScale=.75f;
            if(GUILayout.Button("100%"))vfxScale=1;
            GUILayout.EndHorizontal();
            DrawPaperStyle();
            DrawToggle(HitFeedbackEffect.HitFlash,"명중 흰 점멸");
            DrawToggle(HitFeedbackEffect.CorpseDebris,"시체 물리 파편");
            DrawToggle(HitFeedbackEffect.CameraKick,"명중 카메라 반응");
            DrawToggle(HitFeedbackEffect.WorldDistortion,"명중 화면 왜곡");
            DrawToggle(HitFeedbackEffect.PlayerDeathSlow,"플레이어 사망 슬로우");
            GUILayout.Label("타격감 화면 반응: "+Mathf.RoundToInt(motionIntensity*100)+"%");
            float value=GUILayout.HorizontalSlider(motionIntensity,0,1);
            if(value!=motionIntensity)SetMotionIntensity(value);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("화면 반응 끄기"))SetMotionIntensity(0);
            if(GUILayout.Button("50%"))SetMotionIntensity(.5f);
            if(GUILayout.Button("100%"))SetMotionIntensity(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("카메라, 왜곡만 조정 / 슬로우는 플레이어 사망 때만");
        }
        void DrawPaperStyle()
        {
            if(GUILayout.Button("명중 VFX 외곽선 설정"+(showPaperStyle?"  ^":"  v")))showPaperStyle=!showPaperStyle;
            if(!showPaperStyle)return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("종이 화풍 / 외곽선 색상 (#RRGGBB)");
            GUILayout.BeginHorizontal();
            outlineColorInput=GUILayout.TextField(outlineColorInput,7);
            if(GUILayout.Button("색상 적용"))
            {
                if(HitFeedbackSettings.ValidOutlineColor(outlineColorInput)&&ColorUtility.TryParseHtmlString(outlineColorInput,out var color))SetPaperOutlineColor(color);
                else invalidOutlineColor=true;
            }
            GUILayout.EndHorizontal();
            if(invalidOutlineColor)GUILayout.Label("#과 6자리 색상 코드를 입력하세요.");
            if(GUILayout.Button("외곽선 기본값 복원"))
            {
                ColorUtility.TryParseHtmlString(settings.PaperOutlineColor,out var color);SetPaperOutlineColor(color);
            }
            GUILayout.EndVertical();
        }
        public void Sample()
        {
            if(!Ready||!arena)return;
            FollowSession();
            if(!Ready)return;
            try
            {
                SetMotionIntensity(motionIntensity);
                if(!arena.Player.Dead)
                {playerDeathHandled=false;playerDeathSlow?.Dispose();playerDeathSlow=null;}
                targets.Clear();foreach(var actor in arena.enemies)if(actor&&actor.isActiveAndEnabled)targets[actor.ActorId]=actor;
                int budget=512;
                while(budget>0 && arena.TryRead(cursor,pageBuffer,out var page))
                {
                    LostHitFacts+=page.DroppedBefore;
                    int count=Math.Min(budget,page.Count);
                    for(int i=0;i<count;i++)
                    {
                        var hit=pageBuffer[i];cursor=new CombatOutcomeCursor(cursor.Session,hit.EventSequence);budget--;
                        if(!hit.Target.Session.Equals(cursor.Session)||!hit.Origin.EffectiveSource.Session.Equals(cursor.Session))continue;
                        Consume(hit);
                    }
                    if(count==page.Count)cursor=page.Next;
                    if(page.Count<pageBuffer.Length)break;
                }
                session.Sample(arena.Now);
                DrawFlashes();
                stale.Clear();foreach(var pair in deaths)if(!pair.Value.actor||!pair.Value.actor.isActiveAndEnabled||!pair.Value.actor.Dead)stale.Add(pair.Key);
                foreach(var id in stale)deaths.Remove(id);
                stale.Clear();foreach(var pair in lethal)if(!targets.TryGetValue(pair.Key,out var actor)||!actor.Dead)stale.Add(pair.Key);
                foreach(var id in stale)lethal.Remove(id);
                if(session.LastError!=null)LastError=session.LastError;
            }
            catch(Exception error){LastError=error.Message;Close();Debug.LogWarning("Hit feedback output stopped: "+error.Message);}
        }
        void LateUpdate()
        {
            Sample();
            // Framing owners have already submitted their current baseline. The
            // existing output remains the sole Transform writer, including this kick.
            if(Ready&&arena.ViewCamera&&arena.ViewCamera.isActiveAndEnabled)CameraOutputBinding.GetOrCreate(arena.ViewCamera).ApplyNow();
        }
        void Consume(CombatAcceptedHit hit)
        {
            targets.TryGetValue(hit.Target.ActorId,out var target);
            if(!target || !hit.Contact.HasPoint || hit.Origin.EffectiveSource.ActorId!=arena.Player.ActorId)return;
            if(!hit.Before.Dead&&hit.After.Dead)lethal[target.ActorId]=hit;
            if(IsEffectEnabled(HitFeedbackEffect.HitFlash))FlashTarget(target.visual,hit.Now);
            // Data forms only qualify the attack; an unstamped hit has no form and stays without VFX.
            // The picture is fixed by data: criticalRecipe on a critical hit, otherwise one of the other recipes chosen by
            // the hit's own seed. Nothing at runtime switches it. The existing VFX toggle only suppresses media.
            if(settings.Recipe(hit.Origin.Stamp.FormId)==null)return;
            uint seed=unchecked((uint)hit.EventSequence ^ (uint)(hit.EventSequence>>32)*16777619u ^ 2166136261u);
            string key=settings.PickRecipe(seed,hit.Critical);
            var definition=factory.Definition(key);
            if(definition==null){LastError="No explicit recipe for captured attack stamp.";return;}
            float angle=hit.Contact.HasDirection?Mathf.Atan2(hit.Contact.IncomingY,hit.Contact.IncomingX)*Mathf.Rad2Deg:0;
            var captured=factory.Capture(key,angle+settings.Rotation(key),settings.Scale(key)*vfxScale,seed);
            var result=session.Consume(hit.EventSequence,hit.Origin.ExecutionId,hit.Contact.X,hit.Contact.Y,hit.Now,arena.Now,definition,captured,IsEffectEnabled(HitFeedbackEffect.HitVfx));
            if(result==HitFeedbackResult.Started)LastHitVfxKey=key;
            if(result==HitFeedbackResult.Started || result==HitFeedbackResult.AcceptedWithoutMedia)
            {
                bool strong=settings.Strong(hit.Origin.Stamp.ActionId);
                if(IsEffectEnabled(HitFeedbackEffect.CameraKick))kick.Add(hit.Contact.IncomingX,hit.Contact.IncomingY,settings.CameraPixels(strong),settings.CameraSeconds(strong));
                if(IsEffectEnabled(HitFeedbackEffect.WorldDistortion)&&settings.Distorts(hit.Origin.Stamp.ActionId))distortion?.Add(new Vector2(hit.Contact.X,hit.Contact.Y),settings.WaveSeconds,settings.WavePixels,settings.WaveRadius,settings.WaveWidth);
            }
        }
        void FlashTarget(SpriteRenderer visual,float at)
        {
            if(!visual)return;
            if(!flashes.TryGetValue(visual.GetEntityId(),out var flash))
            {
                var go=new GameObject("Accepted-hit white overlay");go.transform.SetParent(visual.transform,false);
                var overlay=go.AddComponent<SpriteRenderer>();overlay.sharedMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
                if(!overlay.sharedMaterial){Destroy(go);return;}
                go.AddComponent<HitFlashOverlay>().Source=visual;
                flash=new Flash{source=visual,overlay=overlay};flashes.Add(visual.GetEntityId(),flash);
            }
            flash.until=at+.065f;
        }
        void DrawFlashes()
        {
            staleFlashes.Clear();foreach(var pair in flashes)
            {
                var f=pair.Value;if(!f.source||!f.overlay){staleFlashes.Add(pair.Key);continue;}
                // Frame, flip and sorting follow in HitFlashOverlay after the species presenters.
                f.overlay.enabled=f.source.enabled&&arena.Now<f.until;f.overlay.color=new Color(1,1,1,.9f);
                f.overlay.GetComponent<HitFlashOverlay>().Follow();
            }
            foreach(var id in staleFlashes)flashes.Remove(id);
        }
        // Called by the actual motion presenter, not inferred from elapsed HP loss.
        public long BeginDeath(CombatActor actor,Sprite body)
        {
            Sample(); // The motion presenter may run before our ordinary LateUpdate.
            if(!Ready || !actor || !actor.Dead || !body || !arena.TryBindActor(actor,out var handle))return 0;
            if(deaths.TryGetValue(actor.ActorId,out var previous))return previous.serial;
            var death=new Death{serial=++serial,actor=actor,handle=handle,sprite=body,emitDebris=IsEffectEnabled(HitFeedbackEffect.CorpseDebris)};
            if(lethal.TryGetValue(actor.ActorId,out var hit)&&hit.Target.Equals(handle))
            {
                death.incoming=new Vector2(hit.Contact.IncomingX,hit.Contact.IncomingY);
            }
            lethal.Remove(actor.ActorId);deaths.Add(actor.ActorId,death);return death.serial;
        }
        public bool CompleteDeath(CombatActor actor,long token)
        {
            if(!Ready||!actor||token==0||!deaths.TryGetValue(actor.ActorId,out var death)||death.serial!=token)return false;
            deaths.Remove(actor.ActorId);
            if(!death.emitDebris||!IsEffectEnabled(HitFeedbackEffect.CorpseDebris)||!actor.Dead||!actor.isActiveAndEnabled||!arena.TryBindActor(actor,out var current)||!current.Equals(death.handle)||physicsFailed)return false;
            var bake=debrisCatalog?debrisCatalog.Find(death.sprite):null;
            if(!bake){LastError="Death body has no baked debris binding.";return false;}
            if(!bake.IsCurrentSource){LastError="Death body source changed; regenerate its debris cache before use.";return false;}
            return debris.Spawn(bake,actor.Position,Quaternion.identity,actor.transform.lossyScale,actor.visual&&actor.visual.flipX,death.incoming,arena.Now,actor.visual?actor.visual.sortingOrder:30);
        }
        public void CancelDeath(CombatActor actor,long token)
        {if(actor&&deaths.TryGetValue(actor.ActorId,out var death)&&death.serial==token)deaths.Remove(actor.ActorId);}
        public void ReportMissingDeathClip(CombatActor actor)
        {if(Ready&&actor&&actor.arena==arena)LastError="No death clip is bound; corpse debris is unsupported for "+actor.name+".";}
        void Close()
        {
            closing=true;serial++;session?.Dispose();session=null;factory?.Dispose();factory=null;
            playerDeathSlow?.Dispose();playerDeathSlow=null;slowScope?.Dispose();slowScope=null;kickSource?.Dispose();kickSource=null;kick?.Clear();distortion?.Dispose();distortion=null;
            meleeObservation?.Dispose();meleeObservation=null;debris?.Dispose();debris=null;deaths.Clear();lethal.Clear();targets.Clear();
            foreach(var f in flashes.Values)if(f.overlay){f.overlay.enabled=false;Destroy(f.overlay.gameObject);}flashes.Clear();
            if(root)
            {
                // Renaming retires lookup identity immediately. Reparenting is
                // forbidden while Unity is deactivating this owner hierarchy.
                root.name="Retired hit feedback output";root.SetActive(false);Destroy(root);
            }
            root=null;
        }
        void OnEnable(){if(configured)BindSession();}
        void OnDisable()=>Close();
        void OnDestroy()
        {
            if(arena){arena.ResetCompleted-=BindSession;arena.SimulationCompleted-=CompletedStep;arena.FeedbackAccentAdvanced-=AdvanceAccent;arena.Deactivated-=Close;arena.DrawFeedbackOptions-=DrawOptions;if(arena.Player)arena.Player.Died-=OnPlayerDied;}
            Close();
        }
    }
}
