using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Deviant.Chapter2;
using Deviant.Combat;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Music
{
    [Serializable]
    public sealed class MusicTrack
    {
        public string id, resource;
        // Tracks of one group are versions of the same piece (a quieter mix, say): switching between two files of a group
        // starts the new one where the old one is. Two cues on the same file and window share one voice instead.
        public string group;
        public float volume = 1, startSeconds, endSeconds;
        public bool loop = true;
        public float loopStartSeconds, loopCrossfadeSeconds;
        // 0: the library's fade times. Fade-in applies when this cue starts, fade-out when it is left.
        public float fadeInSeconds, fadeOutSeconds;

        public float RepeatStart => loopStartSeconds > 0 ? loopStartSeconds : startSeconds;
        public float RepeatCrossfade(float fallback) => loopCrossfadeSeconds > 0 ? loopCrossfadeSeconds : fallback;
        public float FadeIn(MusicLibrary library) => fadeInSeconds > 0 ? fadeInSeconds : library.fadeInSeconds;
        public float FadeOut(MusicLibrary library) => fadeOutSeconds > 0 ? fadeOutSeconds : library.fadeOutSeconds;

        // The same file and playback window: a switch only moves the playing voice's volume, playback goes on.
        public bool SharesPlayback(MusicTrack other) => other != null && other.resource == resource && other.loop == loop
            && other.startSeconds == startSeconds && other.endSeconds == endSeconds && other.RepeatStart == RepeatStart
            && other.loopCrossfadeSeconds == loopCrossfadeSeconds;

        public bool SameGroup(MusicTrack other) => other != null && !string.IsNullOrEmpty(group) && other.group == group;
    }

    [Serializable]
    public sealed class MusicLibrary
    {
        public const int Schema = 2;
        public int schema;
        // A cue change fades the old music out and the new one in with a slow start; tracks may override either time.
        public float fadeOutSeconds = 2, fadeInSeconds = 3, loopCrossfadeSeconds = 2, scheduleLeadSeconds = .1f;
        // The most fade progress one frame may apply: after a main-thread stall (a scene load) a fade goes on from the
        // volume that was last heard instead of jumping to its end.
        public float fadeStepLimitSeconds = .1f;
        public MusicTrack[] tracks;

        public void Validate()
        {
            if (schema != Schema || tracks == null || tracks.Length == 0 || !Positive(fadeOutSeconds) || !Positive(fadeInSeconds)
                || !Positive(loopCrossfadeSeconds) || !Positive(scheduleLeadSeconds) || !Positive(fadeStepLimitSeconds))
                throw new ArgumentException("Invalid background music library");
            var ids = new HashSet<string>();
            foreach (var t in tracks)
                if (t == null || string.IsNullOrEmpty(t.id) || !ids.Add(t.id) || string.IsNullOrEmpty(t.resource)
                    || !Positive(t.volume) || t.volume > 1 || !Finite(t.startSeconds) || t.startSeconds < 0
                    || !Finite(t.loopStartSeconds) || t.loopStartSeconds < 0 || t.RepeatStart < t.startSeconds
                    || !Finite(t.loopCrossfadeSeconds) || t.loopCrossfadeSeconds < 0
                    || !Finite(t.fadeInSeconds) || t.fadeInSeconds < 0 || !Finite(t.fadeOutSeconds) || t.fadeOutSeconds < 0
                    || !Finite(t.endSeconds) || t.endSeconds <= t.RepeatStart + t.RepeatCrossfade(loopCrossfadeSeconds) * 2)
                    throw new ArgumentException("Invalid background music track: " + t?.id);
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Positive(float v) => Finite(v) && v > 0;
    }

    // One persistent 2D music bed. DSP time keeps it independent from combat pause/hit-stop. A small pool of voices
    // (two, more only while fades overlap) carries cue fades, a cue left mid-fade and the authored repeat windows;
    // MP3/WAV originals stay untouched.
    [DefaultExecutionOrder(10000)]
    public sealed class BackgroundMusicPlayer : MonoBehaviour
    {
        const int MaxVoices = 4;

        public static BackgroundMusicPlayer Instance { get; private set; }
        public bool AutomaticSceneSelection { get; set; } = true;

        [Range(0, 1)]
        public float volume = 1;
        public string CurrentCue => active?.track?.id;
        public string RequestedCue { get; private set; }
        public int LoopCount { get; private set; }
        // How often a player death played the sting, and whether music is held silent for it now.
        public int DeathStings { get; private set; }
        public bool DeathLatched => deathLatched;
        public bool IsTransitioning
        {
            get
            {
                if (pendingTrack != null)
                    return true;
                foreach (var v in voices)
                    if (v.ramping || v != active && v.track != null)
                        return true;
                return false;
            }
        }

        public IReadOnlyList<AudioSource> Voices => sources;

        // Web builds: how often a music channel was found at a playback rate other than 1 and put back.
        public int WebPitchCorrections { get; private set; }

        // Unity's Web audio channel re-applies the last playback rate it holds to the next clip it starts, so a music
        // repeat or cue could come in sped up and stay so. Clips at least this long are music, which always plays at 1.
        const float WebMusicMinSeconds = 30;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int DeviantMusicPitchGuard(float minSeconds);
        [DllImport("__Internal")] static extern void DeviantMusicPitchDiagnostics();
        [DllImport("__Internal")] static extern int DeviantTitleAudioReady();
        [DllImport("__Internal")] static extern void DeviantHideTitleAudioButton();
#endif

        // The voice's level from the music data (before the player's music volume setting).
        public float GainOf(AudioSource source)
        {
            foreach (var v in voices)
                if (v.source == source)
                    return v.gain;
            return 0;
        }

        enum Curve { Smooth, EqualPowerIn, EqualPowerOut }

        sealed class Voice
        {
            public AudioSource source;
            public MusicTrack track;
            public double end, start;
            public float gain, from, to, seconds;
            public Curve curve;
            public bool ramping;

            // From the current gain to target, starting at `at` on the DSP clock.
            public void Ramp(float target, double at, float duration, Curve shape)
            {
                from = gain;
                to = target;
                start = at;
                seconds = Mathf.Max(.0001f, duration);
                curve = shape;
                ramping = true;
            }

            public void Advance(double now)
            {
                if (!ramping)
                    return;
                float p = Mathf.Clamp01((float)((now - start) / seconds));
                // Cue fades use smoothstep: a new track starts quietly and every fade lands without a corner.
                // The repeat overlap of one track keeps the equal-power pair.
                float k = curve == Curve.EqualPowerIn ? Mathf.Sin(p * Mathf.PI * .5f)
                    : curve == Curve.EqualPowerOut ? 1 - Mathf.Cos(p * Mathf.PI * .5f)
                    : p * p * (3 - 2 * p);
                gain = from + (to - from) * k;
                if (p >= 1)
                {
                    gain = to;
                    ramping = false;
                }
            }

            public void Release()
            {
                if (source)
                {
                    source.Stop();
                    source.clip = null;
                    source.volume = 0;
                }

                track = null;
                gain = from = to = 0;
                ramping = false;
            }
        }

        readonly List<Voice> voices = new List<Voice>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        MusicLibrary library;
        Func<string, AudioClip> resolver;
        Voice active;
        MusicTrack pendingTrack;
        AudioClip pendingClip;
        Scene previousScene;
        bool sceneResolved, deathLatched;
        double lastDsp = -1, nextEncounterScan;
        ForestCombatWorld forest;
        ClockExecutionerEncounter encounter;
        Chapter1MaintenanceFacility facility;
        StartingVillageWorld village;
        Chapter2World chapter2;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Ensure();
        public static BackgroundMusicPlayer Ensure()
        {
            if (Instance)
                return Instance;
            var existing = FindAnyObjectByType<BackgroundMusicPlayer>();
            return existing ? existing : new GameObject("Background music").AddComponent<BackgroundMusicPlayer>();
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            // The player's saved music volume (GameAudioSettings) applies from the first track.
            volume = GameAudioSettings.Music;
            for (int i = 0; i < 2; i++)
                AddVoice();

            var json = Resources.Load<TextAsset>("BackgroundMusic");
            if (!json)
                throw new InvalidOperationException("BackgroundMusic configuration is not deployed");
            Configure(JsonUtility.FromJson<MusicLibrary>(json.text), LoadClip);
            SceneManager.sceneLoaded += SceneLoaded;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browser console: deviantAudioDump() lists every channel's stored pitch and playback rate.
            DeviantMusicPitchDiagnostics();
#endif
        }

        Voice AddVoice()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            s.dopplerLevel = 0;
            s.pitch = 1;
            s.priority = 64;
            s.ignoreListenerPause = true;
            s.bypassReverbZones = true;
            s.volume = 0;
            // The manager owns the window/overlap; a raw full-clip loop would replay MP3 edge silence.
            s.loop = false;
            var voice = new Voice { source = s };
            voices.Add(voice);
            sources.Add(s);
            return voice;
        }

        public static AudioClip LoadClip(string resource) => resource.StartsWith("SelectedAudio/", StringComparison.Ordinal)
            ? SelectedAudioClips.Find(resource.Substring("SelectedAudio/".Length))
            : Deviant.Media.CompressedAudioResources.Load(resource);

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneResolved = false;
            ForgetScene();
        }

        void ForgetScene()
        {
            forest = null;
            encounter = null;
            facility = null;
            village = null;
            chapter2 = null;
            nextEncounterScan = 0;
            deathLatched = false;
        }

        public void Configure(MusicLibrary data, Func<string, AudioClip> load)
        {
            data.Validate();
            if (load == null)
                throw new ArgumentNullException(nameof(load));
            StopAll();
            library = data;
            resolver = load;
            clips.Clear();
            RequestedCue = null;
            LoopCount = 0;
            DeathStings = 0;
            lastDsp = -1;
            deathLatched = false;
        }

        public void Request(string cue)
        {
            if (library == null)
                throw new InvalidOperationException("Background music is not configured");
            if (cue == RequestedCue)
                return;
            MusicTrack track = null;
            if (cue != null)
            {
                track = Array.Find(library.tracks, t => t.id == cue);
                if (track == null)
                    throw new ArgumentException("Unknown music cue: " + cue);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (RequestedCue == "title")
                DeviantHideTitleAudioButton();
#endif
            RequestedCue = cue;
            pendingTrack = null;
            pendingClip = null;
            if (track == null)
            {
                BeginSilence(AudioSettings.dspTime);
                return;
            }

            if (Resume(track, AudioSettings.dspTime))
                return;

            pendingTrack = track;
            if (!clips.TryGetValue(track.resource, out pendingClip))
            {
                pendingClip = resolver(track.resource);
                if (!pendingClip)
                    throw new InvalidOperationException("Background music clip missing: " + track.resource);
                clips.Add(track.resource, pendingClip);
            }

            if (pendingClip.loadState == AudioDataLoadState.Unloaded)
                pendingClip.LoadAudioData();
        }

        void Update()
        {
            if (library == null)
                return;
            if (AutomaticSceneSelection)
                Request(SceneCue());
            double now = AudioSettings.dspTime;
            HoldFadesAcrossStall(now);
            bool canStart = pendingTrack != null;
#if UNITY_WEBGL && !UNITY_EDITOR
            // A start queued while Web Audio is suspended can expire before the first gesture.
            if (canStart && pendingTrack.id == "title")
                canStart = DeviantTitleAudioReady() != 0;
#endif
            if (canStart && pendingClip && pendingClip.loadState == AudioDataLoadState.Loaded)
            {
                StartCue(pendingTrack, pendingClip, now + library.scheduleLeadSeconds);
                pendingTrack = null;
                pendingClip = null;
            }

            ScheduleRepeat(now);
            foreach (var v in voices)
            {
                v.Advance(now);
                if (v.track == null)
                    continue;
                bool fadedOut = v != active && !v.ramping && v.gain <= 0;
                bool windowOver = now > v.end + .05 && !v.source.isPlaying;
                if (fadedOut || windowOver)
                {
                    if (v == active)
                        active = null;
                    v.Release();
                }
            }

            ApplyGains();
            KeepWebMusicPitch();
        }

        void KeepWebMusicPitch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int corrected = DeviantMusicPitchGuard(WebMusicMinSeconds);
            if (corrected <= 0)
                return;
            if (WebPitchCorrections == 0)
                Debug.LogWarning("Background music: a Web audio channel played music at a playback rate other than 1; reset to 1 (cue " + CurrentCue + ")");
            WebPitchCorrections += corrected;
#endif
        }

        string SceneCue()
        {
            var scene = SceneManager.GetActiveScene();
            if (!sceneResolved || scene != previousScene)
            {
                previousScene = scene;
                sceneResolved = true;
                ForgetScene();
            }

            if (scene.path == Chapter1RunHost.TitleScene)
                return "title";
            if (scene.path == Chapter1RunHost.VillageScene)
            {
                if (!village && Time.unscaledTimeAsDouble >= nextEncounterScan)
                {
                    village = FindAnyObjectByType<StartingVillageWorld>();
                    nextEncounterScan = Time.unscaledTimeAsDouble + .25;
                }
                // The hero turning to sketch at the well is not a death: no sting, the chase music fades out over the
                // track's own fade-out while the sketch spreads.
                if (village && village.OpeningMusicCue == "opening_erase")
                    return village.ErasureMet ? null : "opening_erase";
                return village && village.OpeningMusicCue != null ? village.OpeningMusicCue : "village";
            }
            bool inWaterway = scene.path == Chapter2EntryBridge.ScenePath;
            if (inWaterway || scene.path == RoseBlueBossScene.ScenePath)
            {
                if (!chapter2 && Time.unscaledTimeAsDouble >= nextEncounterScan)
                {
                    chapter2 = FindAnyObjectByType<Chapter2World>();
                    nextEncounterScan = Time.unscaledTimeAsDouble + .25;
                }

                if (deathLatched)
                    return null;
                if (chapter2 && chapter2.Ready)
                {
                    if (PlayerDeath(chapter2.arena.Player.Dead) || chapter2.Complete)
                        return null;
                    if (chapter2.Boss && chapter2.Boss.MusicActive)
                        return "chapter2_boss";
                    if (chapter2.CurrentRoom == "M")
                        return "maintenance";
                }

                return inWaterway ? "chapter2_field" : null;
            }

            if (Time.unscaledTimeAsDouble >= nextEncounterScan && (!forest || !encounter || !facility))
            {
                nextEncounterScan = Time.unscaledTimeAsDouble + .25;
                if (!forest)
                    foreach (var w in FindObjectsByType<ForestCombatWorld>())
                        if (w.gameObject.scene == scene)
                        {
                            forest = w;
                            break;
                        }
                // Both are added to the world when it becomes ready.
                if (forest && !encounter)
                    encounter = forest.GetComponent<ClockExecutionerEncounter>();
                if (forest && !facility)
                    facility = forest.GetComponent<Chapter1MaintenanceFacility>();
            }

            var player = forest && forest.Ready && forest.arena ? forest.arena.Player : null;
            bool dead = player && player.Dead;
            if (deathLatched || scene.path == Chapter1RunHost.ChapterScene && PlayerDeath(dead))
                return null;
            if (encounter && encounter.MusicActive)
                return "chapter1_boss";
            if (facility && player && !dead && facility.PlayerInside())
                return "maintenance";
            return scene.path == Chapter1RunHost.ChapterScene ? "chapter1_field" : null;
        }

        // Keep the death state through results and scene teardown, including while an old track is fading.
        bool PlayerDeath(bool playerDead)
        {
            if (deathLatched)
                return true;
            if (!playerDead)
                return false;

            deathLatched = true;
            DeathStings++;
            double now = AudioSettings.dspTime;
            foreach (var voice in voices)
                if (voice.track != null)
                    voice.Ramp(0, now, .12f, Curve.Smooth);
            PresentationSfx.Play("player-death");
            return true;
        }

        // A voice already playing this file and window goes on and only changes level: a quieter version of the cue, or
        // the cue that was just left and is still fading out.
        bool Resume(MusicTrack track, double now)
        {
            Voice keep = active != null && track.SharesPlayback(active.track) ? active : null;
            if (keep == null)
                foreach (var v in voices)
                    if (v.track != null && track.SharesPlayback(v.track) && v.source.isPlaying && (keep == null || v.end > keep.end))
                        keep = v;
            if (keep == null)
                return false;
            FadeOutOthers(keep, now);
            keep.track = track;
            keep.Ramp(track.volume, now, track.FadeIn(library), Curve.Smooth);
            active = keep;
            return true;
        }

        void StartCue(MusicTrack track, AudioClip clip, double at)
        {
            float start = track.startSeconds;
            var previous = active;
            if (previous != null && track.SameGroup(previous.track) && previous.source.isPlaying)
            {
                // Another version of the playing piece: it comes in where the old one is by then.
                float heard = previous.source.time + (float)Math.Max(0, at - AudioSettings.dspTime);
                float end = Mathf.Min(track.endSeconds, clip.length);
                if (heard >= track.startSeconds && heard < end - track.RepeatCrossfade(library.loopCrossfadeSeconds) * 2)
                    start = heard;
            }

            FadeOutOthers(null, at);
            var voice = FreeVoice();
            Play(voice, track, clip, start, at);
            voice.gain = 0;
            voice.Ramp(track.volume, at, track.FadeIn(library), Curve.Smooth);
            active = voice;
        }

        void BeginSilence(double now)
        {
            active = null;
            FadeOutOthers(null, now);
        }

        // Every other sounding voice fades out from the level it has now; one already fading to silence keeps its fade.
        void FadeOutOthers(Voice keep, double at)
        {
            foreach (var v in voices)
                if (v != keep && v.track != null && !(v.ramping && v.to <= 0))
                    v.Ramp(0, at, v.track.FadeOut(library), Curve.Smooth);
        }

        void ScheduleRepeat(double now)
        {
            if (active == null || !active.track.loop || pendingTrack != null)
                return;
            float overlap = active.track.RepeatCrossfade(library.loopCrossfadeSeconds);
            double start = active.end - overlap;
            if (now + library.scheduleLeadSeconds < start)
                return;
            // Schedule the approved overlap on the DSP clock instead of shortening it by a frame.
            start = Math.Max(start, now + .02);
            float remaining = Mathf.Max(.02f, (float)(active.end - start));
            var old = active;
            var next = FreeVoice();
            Play(next, old.track, old.source.clip, old.track.RepeatStart, start);
            next.gain = 0;
            // A repeat inside a cue fade heads for the level that fade is going to.
            next.Ramp(old.ramping ? old.to : old.gain, start, remaining, Curve.EqualPowerIn);
            if (!(old.ramping && old.to <= 0))
                old.Ramp(0, start, remaining, Curve.EqualPowerOut);
            active = next;
            LoopCount++;
        }

        Voice FreeVoice()
        {
            foreach (var v in voices)
                if (v.track == null && v != active)
                    return v;
            if (voices.Count < MaxVoices)
                return AddVoice();
            Voice quietest = null;
            foreach (var v in voices)
                if (v != active && (quietest == null || v.gain < quietest.gain))
                    quietest = v;
            quietest.Release();
            return quietest;
        }

        void Play(Voice voice, MusicTrack track, AudioClip clip, float start, double at)
        {
            float end = Mathf.Min(track.endSeconds, clip.length);
            if (end - start <= track.RepeatCrossfade(library.loopCrossfadeSeconds))
                throw new InvalidOperationException("Music playback window is too short: " + track.id);
            var s = voice.source;
            s.Stop();
            s.clip = clip;
            s.timeSamples = Mathf.RoundToInt(start * clip.frequency);
            voice.track = track;
            voice.end = at + (end - start);
            s.PlayScheduled(at);
            s.SetScheduledEndTime(voice.end);
        }

        // A frame may move a cue fade by at most fadeStepLimitSeconds; the rest of a long frame shifts the fade later.
        // Repeat overlaps follow the scheduled playback and are not held.
        void HoldFadesAcrossStall(double now)
        {
            if (lastDsp >= 0)
                foreach (var v in voices)
                    if (v.ramping && v.curve == Curve.Smooth)
                    {
                        double advanced = now - Math.Max(lastDsp, v.start);
                        if (advanced > library.fadeStepLimitSeconds)
                            v.start += advanced - library.fadeStepLimitSeconds;
                    }

            lastDsp = now;
        }

        void ApplyGains()
        {
            foreach (var v in voices)
                if (v.source)
                    v.source.volume = v.gain * Mathf.Clamp01(volume);
        }

        void StopAll()
        {
            foreach (var v in voices)
                v.Release();
            active = null;
            pendingTrack = null;
            pendingClip = null;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                DeviantHideTitleAudioButton();
#endif
                Instance = null;
            }
            SceneManager.sceneLoaded -= SceneLoaded;
            StopAll();
        }
    }
}
