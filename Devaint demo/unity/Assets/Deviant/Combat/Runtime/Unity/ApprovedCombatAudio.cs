using System;
using System.Collections.Generic;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace Deviant.Combat
{
    // Media consumer only. Existing combat starts, clocks and cancellations
    // own the action; neither the clip nor this output can schedule gameplay.
    public sealed class ApprovedCombatAudio : IDisposable
    {
        [Serializable] public sealed class Catalog { public Entry[] entries; }
        [Serializable] public sealed class Entry
        {
            public string id, clip;
            public string weapon;
            public string[] variants;
            public bool silent;
            [NonSerialized] public int nextVariant;
            public int samples, frequency;
            public float nominalDuration, nominalImpact;
            public bool basic;
            [NonSerialized] public AudioClip media;
        }
        sealed class Playing
        {
            public ICueScope scope; public ICueHandle handle; public CombatCast cast;
            public Guid execution; public bool independent, activated;
            public Func<bool> tailEligible;
            public float conditionalFrom;
        }
        readonly CombatArena arena;
        readonly AudioCueFactory factory;
        readonly CuePlayer cues;
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        [Serializable] sealed class BasicRates { public int schema; public BasicRate[] entries; }
        [Serializable] sealed class BasicRate { public string id; public float rate; }
        readonly Dictionary<string, float> basicRates = new Dictionary<string, float>();
        readonly List<Playing> playing = new List<Playing>();
        readonly HashSet<Guid> started = new HashSet<Guid>();
        bool selectedSkillsLoaded;
        public string LastVariantClip { get; private set; }
        float sampledAt = float.NaN;
        AudioSource flight;
        CombatCast flyingCast;
        bool flightPaused;
        public int SelectedPlayCount { get; private set; }
        public string LastSelectedId { get; private set; }

        public bool EmitSelected(string id, Vector2 at, string actionId = null)
        {
            var clip = SelectedAudioClips.Find(id);
            if (!clip) return false;
            var entry = new Entry { id = id, clip = id, media = clip, samples = clip.samples, frequency = clip.frequency };
            if (Play(entry, Guid.NewGuid(), at, new ArenaClock(arena), 1, selected: true, actionId: actionId) == null) return false;
            SelectedPlayCount++; LastSelectedId = id;
            return true;
        }

        internal void StartMissile(CombatCast cast)
        {
            if (selectedSkillsLoaded) return; // Approved missile: explosion only.
            // The transform's one-shot is the bomb_move_special attack sound; only the flight loop starts here.
            var clip = SelectedAudioClips.Find("sfx_combat_missile_flight");
            if (!clip) return;
            StopMissile();
            if (!flight)
            {
                flight = arena.gameObject.AddComponent<AudioSource>();
                flight.playOnAwake = false;
                flight.loop = true;
                flight.spatialBlend = 0;
                flight.volume = .65f;
            }
            flyingCast = cast;
            flight.clip = clip;
            flight.Play();
        }

        void StopMissile()
        {
            if (flight) { flight.Stop(); flight.clip = null; }
            flyingCast = null; flightPaused = false;
        }

        void TickMissile(bool held)
        {
            if (!flight || !flight.clip) return;
            if (!flyingCast || !flyingCast.owner || flyingCast.owner.Dead || !flyingCast.MissileFlying)
            { StopMissile(); return; }
            if (flightPaused == held) return;
            flightPaused = held;
            if (held) flight.Pause(); else flight.UnPause();
        }
        public int AttackPlayCount { get; private set; }
        public int EventPlayCount { get; private set; }
        public int ActiveCount => cues.ActiveCount;
        public int LiveOutputCount => factory.LiveOutputCount;
        public string LastEventId { get; private set; }
        public string LastError { get; private set; }
        public ApprovedCombatAudio(CombatArena arena)
        {
            this.arena = arena; factory = new AudioCueFactory(arena.transform); cues = new CuePlayer(factory);
            try
            {
                var rateText = Resources.Load<TextAsset>("WeaponSounds/BasicPlaybackRates");
                if (!rateText) throw new InvalidOperationException("Export BasicPlaybackRates.json before playing weapon audio.");
                var rates = JsonUtility.FromJson<BasicRates>(rateText.text);
                if (rates.schema != 1) throw new InvalidOperationException("Invalid selected basic audio calibration.");
                foreach (var rate in rates.entries)
                {
                    if (string.IsNullOrEmpty(rate.id) || float.IsNaN(rate.rate) || float.IsInfinity(rate.rate) || rate.rate < .01f || rate.rate > 3)
                        throw new InvalidOperationException("Invalid selected basic audio rate.");
                    basicRates.Add(rate.id, rate.rate);
                }
                var json = Resources.Load<TextAsset>("ApprovedCombatAudio/Catalog");
                if (!json) return;
                var catalog = JsonUtility.FromJson<Catalog>(json.text);
                foreach (var entry in catalog.entries ?? Array.Empty<Entry>())
                {
                    entry.media = Deviant.Media.CompressedAudioResources.Load(entry.clip);
                    // A selected clip "sfx_weapon_<id>" replaces the approved render. Its impact is
                    // authored at the same nominalImpact, so timing, rate and cancellation stay.
                    var selected = SelectedAudioClips.Find("sfx_weapon_" + entry.id);
                    if (selected) { entry.media = selected; entry.samples = selected.samples; entry.frequency = selected.frequency; }
                    if (!entry.media) LastError = "Missing approved SFX: " + entry.clip;
                    entries.Add(entry.id, entry);
                }
                // Attacks the approved catalog never covered (hammer, shield, launcher, bomb, unarmed) take their
                // timing from WeaponSounds/Timing and their clip from the selected "sfx_weapon_<id>".
                var timing = Resources.Load<TextAsset>("WeaponSounds/Timing");
                foreach (var entry in timing ? JsonUtility.FromJson<Catalog>(timing.text).entries ?? Array.Empty<Entry>() : Array.Empty<Entry>())
                {
                    if (entries.ContainsKey(entry.id)) continue;
                    var selected = SelectedAudioClips.Find("sfx_weapon_" + entry.id);
                    if (!selected) { LastError = "Missing weapon SFX: sfx_weapon_" + entry.id; continue; }
                    entry.clip = "sfx_weapon_" + entry.id; entry.media = selected;
                    entry.samples = selected.samples; entry.frequency = selected.frequency;
                    entries.Add(entry.id, entry);
                }
                var skills = Resources.Load<TextAsset>("SelectedSkillAudio/Catalog");
                if (skills)
                {
                    // Preserve the launcher's existing charge-impact effect separately
                    // from the newly approved common bomb explosion.
                    if (entries.TryGetValue("explosion_bomb", out var legacyBlast))
                        entries["launcher_charge_impact"] = new Entry { id = "launcher_charge_impact", clip = legacyBlast.clip,
                            media = legacyBlast.media, samples = legacyBlast.samples, frequency = legacyBlast.frequency, weapon = "launcher" };
                    foreach (var entry in JsonUtility.FromJson<Catalog>(skills.text).entries ?? Array.Empty<Entry>())
                    {
                        entry.media = Deviant.Media.CompressedAudioResources.Load(entry.clip);
                        if (!entry.media) LastError = "Missing selected skill SFX: " + entry.clip;
                        entries[entry.id] = entry;
                    }
                    selectedSkillsLoaded = true;
                }
            }
            catch (Exception exception) { LastError = exception.Message; }
        }

        // Hit sounds belong to the player's weapon or skill, read from accepted hits (never from the monster hit).
        readonly Deviant.Combat.Contracts.CombatAcceptedHit[] hitPage = new Deviant.Combat.Contracts.CombatAcceptedHit[32];
        Deviant.Combat.Contracts.CombatOutcomeCursor hitCursor;
        bool hitBound;
        public int HitPlayCount { get; private set; }
        public string LastHitId { get; private set; }
        public const float HitSoundSpacing = .06f;
        float lastHitAt = float.NegativeInfinity;
        void ReadHits()
        {
            if (!hitBound) { hitBound = arena.HitEvents.TryGetCursor(out hitCursor); return; }
            while (arena.HitEvents.TryRead(hitCursor, hitPage, out var page))
            {
                hitCursor = page.Next;
                for (int i = 0; i < page.Count; i++)
                {
                    var hit = hitPage[i];
                    if (!arena.Player || hit.Origin.EffectiveSource.ActorId != arena.Player.ActorId || hit.Target.ActorId == arena.Player.ActorId) continue;
                    var id = WeaponHitSounds.For(hit.Origin);
                    // One sweep hitting several monsters (or a multi-hit tick) sounds once, so hits never stack into a spike.
                    if (id == null || id == LastHitId && hit.Now - lastHitAt < HitSoundSpacing) continue;
                    string actionId = hit.Origin.Kind == Deviant.Combat.Contracts.CombatHitKind.InkPart ? null : hit.Origin.ActionId;
                    if (EmitSelected(id, new Vector2(hit.TargetPose.X, hit.TargetPose.Y), actionId)) { HitPlayCount++; LastHitId = id; lastHitAt = hit.Now; }
                }
                if (page.Count < hitPage.Length) return;
            }
            hitBound = false; // a new combat session: bind at its current end on the next tick
        }
        // Fixed gain of non-weapon cues. The heal sound plays at half the usual 0.8 (user: too loud, 2026-11-16).
        public const float HealVolume = .4f;
        public static float FixedVolume(string id) => id == "sfx_player_heal" ? HealVolume : .8f;
        public static bool ContinuousTraversal(string id) =>
            id == "sfx_player_jump" || id == "sfx_player_double_jump" || id == "sfx_player_dash" || id == "sfx_player_wall_kick";
        public bool Has(string id) => entries.ContainsKey(id);
        public float LeadTime(string id) => entries.TryGetValue(id, out var entry) ? entry.nominalImpact : 0;
        public void Attack(CombatCast cast)
        {
            if (!cast || cast.owner != arena.Player || started.Contains(cast.ExecutionId) || !entries.TryGetValue(cast.Definition.Id, out var entry)) return;
            started.Add(cast.ExecutionId);
            if (entry.silent) return; // Its sound belongs to a real success/impact event.
            // Selected PCM already compensates for its original runtime rate. Keep that calibration,
            // independent of haste; forcing pitch=1 would also change the selected sound's timbre.
            // Swing starts at action start and accepted-hit cues own exact contact timing.
            float rate = entry.basic && basicRates.TryGetValue(entry.id, out var calibration) ? calibration : 1;
            var clock = new AttackClock(arena, cast, rate, entry.nominalImpact);
            var active = Play(entry, cast.ExecutionId, cast.owner.Position, clock, rate, attack: cast);
            if (active == null) return;
            active.cast = cast; active.execution = cast.ExecutionId;
            active.independent = entry.id == "base_sword_follow_up" || entry.id == "whip_follow_up" || entry.id == "greatsword_follow_up" || entry.id == "shuriken_followup_special";
            if(entry.id == "whip_follow_up")
            {
                active.tailEligible = () => cast.PresentationMarkedTargetAlive;
                active.conditionalFrom = cast.StartedAt + cast.MotionDuration;
            }
            AttackPlayCount++;
        }
        public bool Emit(string id, Vector2 at, float? startedAt = null)
        {
            if (!entries.TryGetValue(id, out var entry)) return false;
            if (entry.silent) return true;
            if (Play(entry, Guid.NewGuid(), at, new ArenaClock(arena, startedAt), 1) == null) return false;
            EventPlayCount++; return true;
        }
        public bool EmitSkillEvent(string actionId, string eventId, Vector2 at)
            => Emit(actionId + "__" + eventId, at);
        bool PlaybackHeld => arena.Controls.Current.SimulationPaused || Time.timeScale == 0 || arena.ManualSimulationActive;
        float PlaybackScale => Time.timeScale * (arena.Controls.EngineTimeBound ? 1 : arena.Controls.Current.SimulationScale);
        bool AttackHeld(CombatCast cast) => PlaybackHeld || cast &&
            (cast.ChargedMelee != null && !cast.PresentationActivated || cast.ChargePolicy != null && !cast.ChargeRelease.HasValue);
        Playing Play(Entry entry, Guid occurrence, Vector2 at, ICueClock clock, float rate, bool selected = false, CombatCast attack = null, string actionId = null)
        {
            Playing p = null;
            try
            {
                if (entry.variants != null && entry.variants.Length > 1)
                {
                    entry.clip = entry.variants[entry.nextVariant++ % entry.variants.Length];
                    entry.media = Deviant.Media.CompressedAudioResources.Load(entry.clip);
                    if (entry.media) { entry.samples = entry.media.samples; entry.frequency = entry.media.frequency; }
                }
                if (!entry.media) entry.media = Deviant.Media.CompressedAudioResources.Load(entry.clip);
                if (!entry.media) { LastError = "Missing approved SFX: " + entry.clip; return null; }
                var definition = new AudioCueDefinition("ApprovedSfx/" + entry.id, entry.clip, entry.samples, entry.frequency);
                // Player weapon attacks, skill effects, their accepted hits and the one-shot traversal
                // cues (jump, double jump, dash, wall kick) use continuous playback. Re-seeking every
                // poll snapped these short one-shots to the 20 ms simulation clock and broke them up
                // (2026-11-16). Footsteps, healing, player damage and monster cues keep their original
                // sampled playback. Volume settings stay weapon/skill-only: traversal keeps its fixed gain.
                bool weaponAudio = attack || WeaponAudioSettings.WeaponForCue(entry.id) != null
                    || entry.id == WeaponHitSounds.Ink || entry.id.StartsWith("explosion_", StringComparison.Ordinal);
                float volume = weaponAudio ? SkillAudioSettings.ForCue(entry.id, entry.weapon, actionId) : FixedVolume(entry.id);
                if ((weaponAudio || ContinuousTraversal(entry.id)) && !arena.ManualSimulationActive)
                    factory.PublishContinuous(definition, entry.media, rate, volume, () => AttackHeld(attack), () => PlaybackScale);
                else factory.Publish(definition, entry.media, rate, volume);
                LastVariantClip = entry.clip;
                p = new Playing { scope = cues.OpenScope("approved audio " + occurrence) };
                p.handle = p.scope.Play(definition, new CueOccurrence(occurrence, definition.Id), new CueAnchor(at.x, at.y, 1), clock);
                p.handle.Poll(); // Weapon audio starts now; other cues retain their original held first sample.
                playing.Add(p); if (!selected) LastEventId = entry.id; return p;
            }
            catch (Exception exception) { p?.scope.Dispose(); LastError = exception.Message; return null; }
        }
        public void Cancel(CombatCast cast)
        {
            if (flyingCast == cast) StopMissile();
            foreach (var p in playing)
                if (p.execution == cast.ExecutionId && !(p.independent && (p.activated || cast.PresentationActivated))) p.handle.Cancel();
        }
        public void Tick()
        {
            // A render frame between fixed steps is not a gameplay pause.
            // Leave DSP running in that gap; explicit/manual holds still sample
            // the equal clock so the existing audio output pauses immediately.
            bool held = arena.Controls.Current.SimulationPaused || Time.timeScale == 0 || arena.ManualSimulationActive;
            TickMissile(arena.Controls.Current.SimulationPaused || Time.timeScale == 0);
            if (sampledAt == arena.Now && !held) return;
            sampledAt = arena.Now;
            foreach (var p in playing)
            {
                if (p.cast)
                {
                    p.activated |= p.cast.PresentationActivated;
                    if (p.cast.PresentationCancelled && !(p.independent && p.activated)) p.handle.Cancel();
                }
                // A missed/dead marked target has no delayed tightening effect.
                // Keep the initial swing, but do not play the future tail.
                if(p.tailEligible != null && arena.Now >= p.conditionalFrom && !p.tailEligible()) p.handle.Cancel();
            }
            if (arena.Player && arena.Player.Dead) { Reset(); return; }
            ReadHits();
            cues.Tick();
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var p = playing[i]; var status = p.handle.Status;
                if (status != CueStatus.Completed && status != CueStatus.Cancelled && status != CueStatus.Failed) continue;
                if (status == CueStatus.Failed) LastError = p.handle.Error;
                p.scope.Dispose(); playing.RemoveAt(i);
            }
        }
        public void Reset() { StopMissile(); cues.Reset(); playing.Clear(); started.Clear(); sampledAt = float.NaN; hitBound = false; lastHitAt = float.NegativeInfinity; LastHitId = null; }
        public void Dispose() { StopMissile(); cues.Dispose(); factory.Dispose(); playing.Clear(); started.Clear(); }
        sealed class ArenaClock : ICueClock
        {
            readonly CombatArena arena; readonly float start;
            public ArenaClock(CombatArena arena, float? startedAt = null) { this.arena = arena; start = startedAt ?? arena.Now; }
            public float ReadElapsed() => arena.Now - start;
        }
        sealed class AttackClock : ICueClock
        {
            readonly CombatArena arena; readonly CombatCast cast; readonly float start, rate, impact;
            float shift;
            public AttackClock(CombatArena arena, CombatCast cast, float rate, float impact)
            { this.arena = arena; this.cast = cast; start = cast.StartedAt; this.rate = rate; this.impact = impact; }
            public float ReadElapsed()
            {
                // Charge release owns the shift. A held charge does not play a
                // future release at the minimum windup time.
                if (cast && cast.ChargedMelee != null)
                {
                    if (!cast.PresentationActivated) return 0;
                    shift = Mathf.Max(0, cast.ImpactTime - impact);
                }
                else if (cast && cast.ChargePolicy != null)
                {
                    if (!cast.ChargeRelease.HasValue) return 0;
                    shift = cast.ChargeRelease.Value.Shift;
                }
                return Mathf.Max(0, arena.Now - start - shift) * rate;
            }
        }
    }
}
