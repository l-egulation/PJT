using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Combat
{
    // Test-branch presentation. Receives committed hits, never applies damage,
    // writes actor poses, clears input, or takes another owner's pause lease.
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed partial class CombatFeelFeedback : MonoBehaviour
    {
        CombatArena arena;
        CombatFeelSettings settings;
        CombatOutcomeCursor cursor;
        bool bound;
        readonly CombatAcceptedHit[] hits = new CombatAcceptedHit[128];
        readonly List<Flash> flashes = new List<Flash>();
        readonly List<Impact> impacts = new List<Impact>();
        readonly System.Random visualRandom = new System.Random(7319);
        readonly int[] impactBag={0,1,2,3,4,5};
        int impactBagIndex=6;
        public int LastImpactVariant {get;private set;}=-1;
        int NextImpactVariant() {
            if(impactBagIndex==6) {
                for(int i=5;i>0;i--){int j=visualRandom.Next(i+1);int v=impactBag[i];impactBag[i]=impactBag[j];impactBag[j]=v;}
                if(impactBag[0]==LastImpactVariant){int v=impactBag[0];impactBag[0]=impactBag[1];impactBag[1]=v;}
                impactBagIndex=0;
            }
            return LastImpactVariant=impactBag[impactBagIndex++];
        }
        CameraOutputBinding.AdditiveOffset cameraOffset;
        ParticleSystem particles;
        readonly ParticleSystem.Particle[] particleFlight = new ParticleSystem.Particle[384];
        readonly Dictionary<uint, float> particleFloors = new Dictionary<uint, float>();
        readonly HashSet<uint> liveParticles = new HashSet<uint>();
        readonly List<uint> expiredParticles = new List<uint>();
        uint nextParticleSeed = 1;
        Material material;
        float stopLeft, shakeLeft, shakeAge, shakeStrength, shakeDuration = 1;
        public CombatFeelPixelFx PixelFx { get; private set; }
        public CombatFeelEnemyMotion EnemyMotion { get; private set; }
        public CombatFeelCamera CombatCamera { get; private set; }
        public bool UsePixelEffects => EffectsEnabled && settings != null && settings.pixelEffects && PixelFx;
        // Monster hits always draw this dot burst together with the weapon's paper-ink prefab (HitFeedbackBridge).
        // Both come from data only; there is no runtime comparison that hides or swaps either layer.
        public bool UsePixelHits => UsePixelEffects && settings.pixelHits;
        float holdBannerUntil;
        public bool ShowsLegacyImpact => EffectsEnabled && settings != null && !UsePixelHits && settings.inkParticles > 0;
        int direction = 1, preset = 1;
        public bool EffectsEnabled { get; private set; }
        public bool UseInkSwordTrail => EffectsEnabled && settings != null && settings.inkSwordTrail;
        public bool UseContinuousSwordTrail => UseInkSwordTrail && settings.continuousSwordTrail;
        public bool CompactHud => settings!=null&&settings.compactHud;
        public bool UseWhipTrail => EffectsEnabled&&settings!=null&&settings.whipTrail;
        public float PlayerHurtPeakOpacity => settings==null?0:settings.playerHurtOpacity;
        readonly Dictionary<Guid,float> bladeDirections=new Dictionary<Guid,float>();
        readonly Queue<Guid> bladeDirectionOrder=new Queue<Guid>();
        public void ObserveBladeSweep(Guid execution,Vector3 tangent)
        {
            if(tangent.sqrMagnitude<.000004f)return;
            if(!bladeDirections.ContainsKey(execution)){bladeDirectionOrder.Enqueue(execution);if(bladeDirectionOrder.Count>128)bladeDirections.Remove(bladeDirectionOrder.Dequeue());}
            bladeDirections[execution]=Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg;
        }
        public bool HitStopEnabled => settings != null && settings.hitStopEnabled;
        public int ActiveImpactCount => impacts.Count;
        public Vector3 LastImpactCenter { get; private set; }
        public float LastImpactAngle { get; private set; }
        public bool HitStopped => EffectsEnabled && stopLeft > 0;
        public float HitStopRemaining => stopLeft;
        public int PresentedHits { get; private set; }
        public long DroppedHits { get; private set; }
        public int ActiveParticleCount => particles ? particles.particleCount : 0;
        public Vector3 CurrentCameraOffset { get; private set; }
        public string PresetName => preset == 0 ? "Light" : preset == 1 ? "Balanced" : "Strong";
        float Strength => preset == 0 ? .65f : preset == 1 ? 1 : 1.35f;
        sealed class Flash
        {
            public SpriteRenderer target, overlay;
            public float remaining, duration = 1;
        }
        sealed class Impact
        {
            public SpriteRenderer target;
            public SpriteRenderer[] rays;
            public SpriteRenderer[] edges;
            public float age;
            public bool heavy;
        }
        float Random(float min, float max) => min + (float)visualRandom.NextDouble() * (max - min);

        public void Bind(CombatArena owner, CombatFeelSettings configuration)
        {
            if (arena) throw new InvalidOperationException("Hit feedback is already bound.");
            configuration.Validate(); arena = owner; settings = configuration;
            EffectsEnabled = configuration.enabled;
            material = Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
            if (!material) throw new InvalidOperationException("The existing unlit hit material is required.");
            cameraOffset = CameraOutputBinding.GetOrCreate(owner.ViewCamera).BindAdditiveOffset("combat feel test");
            CreateParticles();
            CreateFinisherArt();
            PixelFx = new GameObject("Combat feel pixel fx").AddComponent<CombatFeelPixelFx>(); PixelFx.Bind(owner, configuration);
            EnemyMotion = gameObject.AddComponent<CombatFeelEnemyMotion>(); EnemyMotion.Bind(owner, configuration);
            CombatCamera = gameObject.AddComponent<CombatFeelCamera>(); CombatCamera.Bind(owner, configuration);
            arena.ResetCompleted += ResetFeedback;
            arena.Deactivated += ResetFeedback;
            bound = arena.HitEvents.TryGetCursor(out cursor);
        }
        void CreateParticles()
        {
            var emitter = new GameObject("Combat feel ink impacts");
            emitter.transform.SetParent(transform, false);
            particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 1;
            main.maxParticles = 384; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.startSize3D = true;
            main.useUnscaledTime = true;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var force = particles.forceOverLifetime;
            force.enabled = true; force.space = ParticleSystemSimulationSpace.World; force.y = -13.75f;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = 85;
            // Village terrain contains broad collision volumes behind the visible ledges.
            // Bounce against each burst's contact floor so those volumes cannot eat the particles.
            var collision = particles.collision; collision.enabled = false;
        }
        void Update()
        {
            if (!arena || !DevFeatures.Enabled) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame) SetEffectsEnabled(!EffectsEnabled);
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame)
            { preset = (preset + 1) % 3; ResetFeedback(); }
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) SetHitStopEnabled(!HitStopEnabled);
            if (keyboard != null && keyboard.f12Key.wasPressedThisFrame)
            { PrepareHoldCompare.Cycle(); holdBannerUntil = Time.unscaledTime + 2.5f; }
        }
        void LateUpdate() { if (arena) TickPresentation(Time.unscaledDeltaTime); }
        public void SetEffectsEnabled(bool value)
        { EffectsEnabled = value; ResetFeedback(); }
        public void SetHitStopEnabled(bool value) { settings.hitStopEnabled = value; stopLeft = 0; }
        public void ResetFeedback()
        {
            stopLeft = shakeLeft = shakeAge = shakeStrength = 0;
            if (PixelFx) PixelFx.Clear();
            if (EnemyMotion) EnemyMotion.Clear();
            if (CombatCamera) CombatCamera.Clear();
            CurrentCameraOffset = Vector3.zero;
            if (cameraOffset != null && cameraOffset.IsActive) cameraOffset.Submit(Vector3.zero);
            if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleFloors.Clear(); liveParticles.Clear(); expiredParticles.Clear();
            foreach (var impact in impacts) DisposeImpact(impact);
            impacts.Clear();
            bladeDirections.Clear();bladeDirectionOrder.Clear();
            ResetFinishers();
            foreach (var flash in flashes)
            { flash.remaining = 0; if (flash.overlay) flash.overlay.enabled = false; }
            if (arena) bound = arena.HitEvents.TryGetCursor(out cursor);
        }
        public void TickPresentation(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (!arena || !arena.isActiveAndEnabled || !arena.Player) { ResetFeedback(); return; }
            if (!bound || !arena.ReadModel.TryGetSession(out var session) || !session.Equals(cursor.Session))
            { ResetFeedback(); return; }
            if (arena.Controls.Current.SimulationPaused)
            {
                if (particles && particles.isPlaying) particles.Pause();
                CurrentCameraOffset = Vector3.zero;
                if (cameraOffset != null && cameraOffset.IsActive) cameraOffset.Submit(Vector3.zero);
                return;
            }
            if (particles && particles.isPaused) particles.Play();
            stopLeft = Mathf.Max(0, stopLeft - delta);
            shakeLeft = Mathf.Max(0, shakeLeft - delta); shakeAge += delta;
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var flash = flashes[i];
                if (!flash.target || !flash.overlay)
                { if (flash.overlay) Destroy(flash.overlay.gameObject); flashes.RemoveAt(i); continue; }
                flash.remaining = Mathf.Max(0, flash.remaining - delta);
            }
            TickImpacts(delta);
            TickFinishers(delta);
            PollHits();
            BounceParticles();
            foreach (var flash in flashes)
            {
                flash.overlay.enabled = EffectsEnabled && flash.remaining > 0 && flash.target.enabled;
                if (!flash.overlay.enabled) continue;
                flash.overlay.sprite = flash.target.sprite; flash.overlay.flipX = flash.target.flipX;
                flash.overlay.flipY = flash.target.flipY;
                flash.overlay.sortingLayerID = flash.target.sortingLayerID;
                flash.overlay.sortingOrder = flash.target.sortingOrder + 3;
                flash.overlay.color = new Color(1, 1, 1, Mathf.Min(.95f, flash.remaining / flash.duration));
            }
            float envelope = shakeLeft / shakeDuration;
            CurrentCameraOffset = EffectsEnabled && shakeLeft > 0
                ? new Vector3((direction * Mathf.Cos(shakeAge * 155) + Mathf.Sin(shakeAge * 93) * .35f) * shakeStrength * envelope,
                    Mathf.Sin(shakeAge * 117) * shakeStrength * .55f * envelope, 0)
                : Vector3.zero;
            if (cameraOffset != null && cameraOffset.IsActive) cameraOffset.Submit(CurrentCameraOffset);
        }
        void BounceParticles()
        {
            int count = particles.GetParticles(particleFlight);
            liveParticles.Clear(); expiredParticles.Clear();
            for (int i = 0; i < count; i++)
            {
                var particle = particleFlight[i]; liveParticles.Add(particle.randomSeed);
                if (!particleFloors.TryGetValue(particle.randomSeed, out float floor)) continue;
                var at = particle.position; var velocity = particle.velocity;
                if (at.y < floor && velocity.y < 0) {
                    at.y = floor; velocity.y = -velocity.y * .45f; velocity.x *= .8f;
                    particle.position = at; particle.velocity = velocity; particleFlight[i] = particle;
                }
            }
            particles.SetParticles(particleFlight, count);
            foreach (var pair in particleFloors) if (!liveParticles.Contains(pair.Key)) expiredParticles.Add(pair.Key);
            foreach (var seed in expiredParticles) particleFloors.Remove(seed);
        }
        void PollHits()
        {
            if (!arena.ReadModel.TryReadPlayer(cursor.Session, out var player)) return;
            for (int pageIndex = 0; pageIndex < 4; pageIndex++)
            {
                if (!arena.HitEvents.TryRead(cursor, hits, out var page)) { ResetFeedback(); return; }
                cursor = page.Next; DroppedHits += page.DroppedBefore;
                for (int i = 0; i < page.Count; i++)
                {
                    var hit = hits[i];
                    if (!EffectsEnabled || !hit.Target.Session.Equals(cursor.Session) ||
                        hit.Damage <= 0 && hit.Absorbed <= 0 ||
                        hit.Origin.EffectiveSource.ActorId != player.Handle.ActorId && hit.Target.ActorId != player.Handle.ActorId)
                        continue;
                    var target = Resolve(hit.Target);
                    if (!target || !target.visual) continue;
                    var point = arena.ViewCamera.WorldToViewportPoint(target.Center);
                    if (point.z <= 0 || point.x < -.1f || point.x > 1.1f || point.y < -.1f || point.y > 1.1f) continue;
                    Present(hit, target);
                }
                if (page.Count < hits.Length) break;
            }
        }
        CombatActor Resolve(CombatActorHandle handle)
        {
            if (!arena.ReadModel.TryReadActor(handle, out _)) return null;
            if (arena.Player.ActorId == handle.ActorId) return arena.Player;
            foreach (var enemy in arena.enemies) if (enemy && enemy.ActorId == handle.ActorId) return enemy;
            return null;
        }
        void Present(CombatAcceptedHit hit, CombatActor target)
        {
            PresentedHits++;
            if(target.isPlayer) {
                PresentFinisher(hit,target,CombatFeelArt.WorldCenter(target.visual),false);
                return;
            }
            // Tuned per action (hit_feedback.json actions); unlisted actions keep the global values.
            var feel = settings.Find(hit.Origin.ActionId);
            bool killed = hit.Before.Health > 0 && hit.After.Dead;
            bool heavy = feel != null ? feel.heavy : hit.Damage + hit.Absorbed >= settings.heavyDamageThreshold || heavyExecutions.Contains(hit.Origin.ExecutionId);
            float stop = feel != null ? feel.hitStop : heavy ? settings.heavyHitStopSeconds : settings.hitStopSeconds;
            float shakeDistance = feel != null ? feel.shakeDistance : settings.shakeDistance * (heavy ? 1.3f : 1), shakeSeconds = feel != null ? feel.shakeSeconds : settings.shakeSeconds;
            if (killed) { stop = Mathf.Max(stop, settings.kill.hitStop); shakeDistance = Mathf.Max(shakeDistance * 1.3f, settings.kill.shakeDistance); shakeSeconds = Mathf.Max(shakeSeconds, settings.kill.shakeSeconds); }
            stopLeft = settings.hitStopEnabled ? Mathf.Max(stopLeft, Mathf.Min(.12f, stop * Strength)) : 0;
            // A light hit does not cut off a stronger shake that is still running.
            float nextShake = Mathf.Min(.2f, shakeDistance * Strength);
            if (nextShake >= shakeStrength * (shakeLeft > 0 ? shakeLeft / shakeDuration : 0))
            { shakeDuration = shakeLeft = shakeSeconds; shakeAge = 0; shakeStrength = nextShake; }
            if (arena.ReadModel.TryReadActor(hit.Origin.EffectiveSource, out var source))
                direction = hit.TargetPose.X >= source.Kinematics.X ? 1 : -1;
            if (settings.contactFlash)
            {
                var flash = flashes.Find(f => f.target == target.visual);
                if (flash == null)
                {
                    var overlay = new GameObject("Combat feel contact flash"); overlay.transform.SetParent(target.visual.transform, false);
                    var renderer = overlay.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                    flash = new Flash { target = target.visual, overlay = renderer }; flashes.Add(flash);
                }
                flash.remaining = flash.duration = feel != null ? feel.flashSeconds : settings.flashSeconds;
            }
            var origin = CombatFeelArt.WorldCenter(target.visual);
            float impactAngle=bladeDirections.TryGetValue(hit.Origin.ExecutionId,out var cutAngle)?cutAngle:(hit.Origin.HitId!=null&&hit.Origin.HitId.Contains("basic_1")?-32:32)*direction;
            EnemyMotion.Hit(target, feel != null ? feel.lift : 0, heavy || killed, direction, killed);
            if (UsePixelHits)
            {
                string action = hit.Origin.ActionId ?? "";
                float angle = bladeDirections.TryGetValue(hit.Origin.ExecutionId, out var blade) ? blade :
                    action.StartsWith("hammer_", StringComparison.Ordinal) ? -58 * direction : action == "base_sword_default" ? 0 :
                    (action.EndsWith("_basic_1", StringComparison.Ordinal) ? -28 : 28) * direction;
                bool slash = action.StartsWith("base_sword", StringComparison.Ordinal) || action.StartsWith("hammer_", StringComparison.Ordinal) || killed;
                LastImpactAngle = Mathf.Repeat(angle, 180); LastImpactCenter = origin;
                PixelFx.Hit(target, origin, heavy, killed, direction, angle + Random(-8, 8), slash);
                PresentFinisher(hit, target, origin, heavy);
                return;
            }
            if (settings.centeredImpact) SpawnImpact(target.visual, origin, heavy,impactAngle);
            PresentFinisher(hit, target, origin, heavy);
            if (!particles.isPlaying) particles.Play();
            for (int i = 0; i < settings.inkParticles; i++)
            {
                float angle = Random(-1.1f, .22f);
                float speed = Random(2, 4.75f) * settings.particleSpread * Strength * (heavy ? 1.3f : 1);
                uint seed = nextParticleSeed++;
                particleFloors[seed] = target.Position.y + settings.particleSize * .5f;
                particles.Emit(new ParticleSystem.EmitParams {
                    position = origin, velocity = new Vector3(direction * Mathf.Cos(angle) * speed, -Mathf.Sin(angle) * speed, 0),
                    startLifetime = settings.burstSeconds, startSize3D = Vector3.one * settings.particleSize,
                    rotation = 0, startColor = new Color32(255, 255, 255, 255), randomSeed = seed
                }, 1);
            }
        }
        void SpawnImpact(SpriteRenderer target, Vector3 origin, bool heavy,float angle)
        {
            int variant=NextImpactVariant();
            angle=Mathf.Repeat(angle+Random(-22,22),180);
            LastImpactAngle = angle; LastImpactCenter = origin;
            var impact = new Impact { target = target, rays = new SpriteRenderer[settings.flashDirections],edges=new SpriteRenderer[settings.flashDirections],heavy=heavy };
            for (int i = 0; i < impact.rays.Length; i++)
            {
                var go = new GameObject("Centered white impact " + i); go.transform.SetParent(transform, false);
                var ray = go.AddComponent<SpriteRenderer>(); ray.sharedMaterial = material;ray.sprite=i==0?CombatFeelArt.ImpactVariants[variant]:cutLineArt;
                ray.sortingLayerID = target.sortingLayerID; ray.sortingOrder = target.sortingOrder + 6;
                ray.color = Color.white;
                go.transform.rotation = Quaternion.Euler(0,0,angle+(i==0?0:78));
                go.transform.localScale = i==0?(heavy?new Vector3(settings.impactScale*2.2f,1.65f,1):new Vector3(settings.impactScale,1.05f,1)):
                    (heavy?new Vector3(1.15f,1.25f,1):new Vector3(.38f,.75f,1));
                impact.rays[i] = ray;
                var outline=new GameObject("Cut line ink edge");outline.transform.SetParent(go.transform,false);
                var edge=outline.AddComponent<SpriteRenderer>();edge.sprite=ray.sprite;edge.sharedMaterial=material;edge.sortingLayerID=target.sortingLayerID;edge.sortingOrder=ray.sortingOrder-1;
                outline.transform.localScale=i==0?new Vector3(1.04f,1.08f,1):new Vector3(1.02f,1.5f,1);impact.edges[i]=edge;
            }
            impacts.Add(impact); DrawImpact(impact);
        }
        void TickImpacts(float delta)
        {
            for (int i = impacts.Count - 1; i >= 0; i--)
            {
                var impact = impacts[i]; impact.age += delta;
                if (!impact.target || impact.age >= settings.impactSeconds + (impact.rays.Length - 1) * settings.directionDelay)
                { DisposeImpact(impact); impacts.RemoveAt(i); continue; }
                DrawImpact(impact);
            }
        }
        void DrawImpact(Impact impact)
        {
            var origin = CombatFeelArt.WorldCenter(impact.target);
            for (int i = 0; i < impact.rays.Length; i++)
            {
                var ray = impact.rays[i]; float t = (impact.age - i * settings.directionDelay) / settings.impactSeconds;
                ray.enabled = (impact.target.enabled||OwnsDeathVisual(impact.target)) && t >= 0 && t < 1;
                ray.transform.position = origin;
                float alpha=t<.45f?1:Mathf.Clamp01((1-t)/.55f);
                ray.color=new Color(1,1,1,alpha);impact.edges[i].enabled=ray.enabled;impact.edges[i].color=new Color(.15f,.16f,.15f,alpha*.6f);
            }
        }
        static void DisposeImpact(Impact impact)
        {
            foreach (var ray in impact.rays) if (ray)
            { ray.enabled = false; if (Application.isPlaying) Destroy(ray.gameObject); else DestroyImmediate(ray.gameObject); }
        }
        public bool ExternalHud { get; set; }
        void OnGUI()
        {
            if (!arena || !arena.Player) return;
            if(!ExternalHud&&!CompactHud)GUI.Box(new Rect(12, Screen.height - 36, 610, 26),
                "Hit feel " + (EffectsEnabled ? "ON" : "OFF") + " [F6]   |   " + PresetName + " [F7]   |   Hit-stop " + (HitStopEnabled ? "ON" : "OFF") + " [F8]");
            if (!ExternalHud&&DevFeatures.Enabled) DrawHoldCompare();
            DrawDamageNumbers();
        }
        GUIStyle holdCompareStyle;
        void DrawHoldCompare()
        {
            if (Event.current.type != EventType.Repaint) return;
            holdCompareStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
            bool banner = Time.unscaledTime < holdBannerUntil;
            holdCompareStyle.fontSize = Mathf.RoundToInt(Screen.height / 540f * (banner ? 18 : 11));
            float width = Screen.width * (banner ? .42f : .3f), height = holdCompareStyle.fontSize * 2.2f;
            GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height * (banner ? .3f : .91f), width, height),
                "준비 자세 [F12] " + PrepareHoldCompare.Name, holdCompareStyle);
        }
        void OnDisable() { ResetFeedback(); }
        void OnDestroy()
        {
            if (arena) { arena.ResetCompleted -= ResetFeedback; arena.Deactivated -= ResetFeedback; }
            cameraOffset?.Dispose();
            foreach (var flash in flashes) if (flash.overlay) Destroy(flash.overlay.gameObject);
            foreach (var impact in impacts) DisposeImpact(impact);
            DisposeFinisherArt();
            if (PixelFx) Destroy(PixelFx.gameObject);
        }
    }
}
