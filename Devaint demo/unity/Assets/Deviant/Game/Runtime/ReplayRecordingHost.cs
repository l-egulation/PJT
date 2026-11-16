using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using Deviant.Replay;
using Deviant.Replay.Unity;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deviant.Game
{
    [DefaultExecutionOrder(10000)]
    public sealed class ReplayRecordingHost : MonoBehaviour
    {
        // Retain the preference key so an explicit stop from an older build stays stopped.
        const string RecordingPreferenceKey = "deviant.replay.local-consent.v1";
        static ReplayRecordingHost instance;
        public static ReplayRecordingHost Instance => instance;
        public bool Recording => recorder != null;
        public string LastError { get; private set; }
        public string QueuePath => Path.Combine(Application.persistentDataPath, "ReplayQueue");
        readonly string session = ReplayUuid7.NewId();
        string collectorEndpoint = "", playerId, startedAt;
        ReplayUploadClient uploader;
        string segment, lastHash = "", scenePath = "", contentHash;
        int index, nextEntity = 2;
        long nextSequence = 1, startMs, lastInputSeq = -1, lastInputRevision = -1;
        double origin, nextWorld, nextEnemies, nextDiscover;
        bool recordingEnabled, focused = true;
        ReplayRecorder recorder;
        ReplayStore store;
        Task<ReplayFile> pending;
        CombatArena arena;
        LocalInput input;
        CombatInputSession combatSession;
        CombatOutcomeCursor outcomeCursor, hitCursor;
        readonly CombatCommandOutcome[] outcomes = new CombatCommandOutcome[256];
        readonly CombatAcceptedHit[] hits = new CombatAcceptedHit[256];
        readonly Dictionary<int, long> actorIds = new Dictionary<int, long>();
        readonly Dictionary<EntityId, long> objectIds = new Dictionary<EntityId, long>();
        readonly Dictionary<GameAction, (float, float)> heldValues = new Dictionary<GameAction, (float, float)>();
        readonly HashSet<long> visible = new HashSet<long>(), currentVisible = new HashSet<long>();
        readonly List<long> leaving = new List<long>();
        readonly Vector3[] corners = new Vector3[4];
        string runId = "";
        int growthRevision = -1;
        int pointerFrame = -1, pointerButtons;
        long Ms => Math.Max(0, (long)((Time.realtimeSinceStartupAsDouble - origin) * 1000));
        long Tick => arena ? arena.SimulationTickCount : -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Replay recording"); DontDestroyOnLoad(go); go.AddComponent<ReplayRecordingHost>();
        }
        void Awake()
        {
            if (instance == this) return;
            var settings = Resources.Load<TextAsset>("ReplayCollector");
            InitializeRecording(settings ? settings.text : "");
        }
        void InitializeRecording(string settingsJson)
        {
            if (instance == this) return;
            instance = this; origin = Time.realtimeSinceStartupAsDouble;
            recordingEnabled = PlayerPrefs.GetInt(RecordingPreferenceKey, 1) == 1;
            startedAt = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            try { collectorEndpoint = ReplayUploadBinding.NormalizeEndpoint(string.IsNullOrEmpty(settingsJson) ? "" : JsonUtility.FromJson<ReplayCollectorSettings>(settingsJson)?.base_url); }
            catch (ArgumentException) { collectorEndpoint = ""; Debug.LogWarning("Invalid replay collector endpoint; uploads disabled."); }
            Guard(RefreshUploader);
            focused = Application.isFocused; ReplayUiTarget.Observed += UiObserved;
            SceneManager.activeSceneChanged += SceneChanged;
        }
        static bool ProductScene(string name) => name == "Chapter1Title" || name == "StartingVillage" || name == "Chapter1Procedural" || name == "Chapter2Procedural";
        public void SetRecordingEnabled(bool enabled)
        {
            if (recordingEnabled == enabled) return;
            recordingEnabled = enabled; PlayerPrefs.SetInt(RecordingPreferenceKey, enabled ? 1 : 0); PlayerPrefs.Save();
            if (!enabled)
            {
                uploader?.StopUploads();
                recorder = null;
                // Join the already bounded background write before deleting its output.
                try { pending?.GetAwaiter().GetResult(); } catch (Exception) { }
                pending = null; store = store ?? new ReplayStore(QueuePath); store.DeleteAll(); ReplayBrowserStorage.Sync();
            }
            else { LastError = null; RefreshUploader(); }
        }
        void RefreshUploader()
        {
            if (!recordingEnabled || collectorEndpoint.Length == 0) { uploader?.StopUploads(); return; }
            playerId = ReplayPlayerIdentity.GetOrCreate(() => PlayerPrefs.GetString(ReplayPlayerIdentity.PreferenceKey, ""),
                value => { PlayerPrefs.SetString(ReplayPlayerIdentity.PreferenceKey, value); PlayerPrefs.Save(); });
            if (!uploader) uploader = gameObject.AddComponent<ReplayUploadClient>();
            uploader.Configure(QueuePath, collectorEndpoint);
        }
        static string CollectionPlatform()
        {
#if UNITY_EDITOR
            return "editor";
#elif UNITY_WEBGL
            return "web";
#elif UNITY_STANDALONE_WIN
            return "windows";
#elif UNITY_STANDALONE_OSX
            return "osx";
#else
            return "linux";
#endif
        }
        public void DeleteLocalRecordings()
        {
            SetRecordingEnabled(false); store = store ?? new ReplayStore(QueuePath); store.DeleteAll(); ReplayBrowserStorage.Sync();
        }
        void SceneChanged(Scene previous, Scene next) { Guard(() => { CloseChunk(); recorder = null; arena = null; input = null; scenePath = ""; }); }
        void Discover()
        {
            if (Time.realtimeSinceStartupAsDouble < nextDiscover) return;
            nextDiscover = Time.realtimeSinceStartupAsDouble + .5;
            var found = FindAnyObjectByType<CombatArena>();
            if (found && found.gameObject.scene != SceneManager.GetActiveScene()) found = null;
            if (arena != found) { if (recorder != null) CloseChunk(); recorder = null; arena = found; }
            input = FindAnyObjectByType<LocalInput>();
            foreach (var gui in FindObjectsByType<TemporaryGameGui>()) ReplayUiTarget.RegisterTree(gui.transform);
        }
        void LateUpdate() => Guard(Poll);
        void Poll()
        {
            CompletePending();
            if (!recordingEnabled || !ProductScene(SceneManager.GetActiveScene().name) || LastError != null) return;
            Discover();
            if (arena && arena.ReadModel.TryGetSession(out var current) && !current.Equals(combatSession))
            { CloseChunk(); recorder = null; combatSession = current; outcomeCursor = new CombatOutcomeCursor(current); arena.HitEvents.TryGetCursor(out hitCursor); }
            if (recorder == null)
            {
                // Do not race a segment transition against the previous segment's immutable write.
                if (pending != null) return;
                segment = ReplayUuid7.NewId(); index = 0; lastHash = ""; actorIds.Clear(); objectIds.Clear(); visible.Clear(); nextEntity = 2;
                scenePath = SceneManager.GetActiveScene().path; BindObservationStart(); BeginChunk();
            }
            if (focused)
            {
                ReadInput(); ReadCombat(); var mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.rightButton.wasReleasedThisFrame || mouse.middleButton.wasPressedThisFrame || mouse.middleButton.wasReleasedThisFrame)) CaptureUi(Ms);
            }
            long ms = Ms;
            if (Time.realtimeSinceStartupAsDouble >= nextWorld) { nextWorld = Time.realtimeSinceStartupAsDouble + .1; CaptureWorld(ms, false); CaptureUi(ms); }
            CaptureClock(ms); CaptureRun(ms);
            if (ms - startMs >= 30000 || recorder.EstimatedBytes > 1500000 || recorder.Count > 55000)
            { CloseChunk(); if (pending != null && !pending.IsCompleted) { /* one immutable write; next chunk can collect independently */ } BeginChunk(); }
        }
        void BeginChunk()
        {
            startMs = Ms; recorder = new ReplayRecorder(startMs); growthRevision = -1;
            recorder.Add(ReplayKind.Scene, startMs, Tick, recorder.Intern(scenePath, startMs), recorder.Intern(Application.unityVersion, startMs));
            var world = arena ? arena.GetComponent<ForestCombatWorld>() : null;
            if (world && world.Data?.rooms != null)
                foreach (var room in world.Data.rooms)
                    recorder.Add(ReplayKind.Layout, startMs, Tick, room.id, Str(room.kind, startMs), room.x, room.y, room.w, room.h, room.mirrored ? 1 : 0, room.sourceZone, world.Data.generationSeed);
            var second = arena ? arena.GetComponent<Deviant.Chapter2.Chapter2World>() : null;
            if (second && second.Layout?.rooms != null)
                foreach (var room in second.Layout.rooms)
                    recorder.Add(ReplayKind.Layout, startMs, Tick, Str(room.id, startMs), Str(room.prototypeId, startMs), ReplayNumbers.Quantize(room.x), ReplayNumbers.Quantize(room.y), ReplayNumbers.Quantize(room.w), ReplayNumbers.Quantize(room.h), room.main ? 1 : 0, 2, second.seed);
            CaptureWorld(startMs, true); CaptureUi(startMs); CaptureClock(startMs); CaptureRun(startMs);
            recorder.Add(ReplayKind.SnapshotEnd, startMs, Tick);
        }
        void CloseChunk()
        {
            if (recorder == null) return;
            CompletePending();
            if (pending != null) throw new IOException("Replay writer backlog; recording stopped.");
            var candidates = recorder.SealCandidates(); recorder = null;
            // Commit already trims on its writer worker. Do not traverse the whole history on the first game-frame flush.
            if (store == null) store = new ReplayStore(QueuePath);
            if (contentHash == null)
            {
                var catalog = Resources.Load<TextAsset>("MeleeCatalog");
                contentHash = ReplayStore.Hash(System.Text.Encoding.UTF8.GetBytes(catalog ? catalog.text : "catalog-unavailable"));
            }
            string build = Application.buildGUID; if (string.IsNullOrEmpty(build)) build = "editor";
            var header = new ReplayHeader { SessionId = session, SegmentId = segment, RunId = runId, BuildId = build, ContentHash = contentHash,
                UiVersion = "build-" + build, ChunkIndex = index++, FirstSequence = nextSequence, StartMs = startMs, PreviousSha256 = lastHash };
            var outputStore = store;
            var binding = recordingEnabled && collectorEndpoint.Length != 0 ? new ReplayUploadBinding
            {
                endpoint = collectorEndpoint,
                session = new ReplayCollectionSession { player_id = playerId, session_id = session, build_id = build,
                    platform = CollectionPlatform(), started_at = startedAt }
            } : null;
            Func<ReplayFile> commit = () =>
            {
                var file = outputStore.Commit(header, ReplaySimplifier.Simplify(candidates), binding);
                var tail = file.Chunk.Records[file.Chunk.Records.Count - 1];
                outputStore.MarkSession(header.SessionId, tail.TimeMs, tail.Sequence, false, false); return file;
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            pending = Task.FromResult(commit());
#else
            pending = Task.Run(commit);
#endif
        }
        void CompletePending()
        {
            if (pending == null || !pending.IsCompleted) return;
            var file = pending.GetAwaiter().GetResult(); pending = null; lastHash = file.Sha256;
            nextSequence = checked(file.Chunk.Records[file.Chunk.Records.Count - 1].Sequence + 1);
            ReplayBrowserStorage.Sync();
        }
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                LastError = e.GetType().Name; recorder = null;
                try { pending?.GetAwaiter().GetResult(); pending = null; store?.MarkSession(session, Ms, Math.Max(0, nextSequence - 1), false, true); } catch (Exception) { }
                Debug.LogWarning("Replay recording stopped: " + LastError);
            }
        }
        long ActorId(CombatActorHandle handle)
        {
            if (handle.ActorId <= 0) return 0;
            if (handle.ActorId == combatSession.ActorId) return 1;
            if (!actorIds.TryGetValue(handle.ActorId, out var id)) actorIds.Add(handle.ActorId, id = nextEntity++);
            return id;
        }
        int Str(string text, long ms) => recorder.Intern(text, ms, Tick);
        void BindObservationStart()
        {
            if (arena && arena.ReadModel.TryGetSession(out var current))
            {
                combatSession = current;
                if (arena.Commands.TryReadOutcomes(new CombatOutcomeCursor(current), outcomes, out var page)) outcomeCursor = new CombatOutcomeCursor(current, page.LatestEventSequence);
                arena.HitEvents.TryGetCursor(out hitCursor);
            }
            if (input && input.EventCount > 0)
            { var e = input.EventAt(input.EventCount - 1); lastInputRevision = e.Revision; lastInputSeq = e.Sequence; }
            heldValues.Clear();
        }
        void ReadInput()
        {
            if (!input) return;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var textField = selected ? selected.GetComponent<InputField>() : null;
            if (textField && textField.isFocused) return;
            // LocalInput has already polled once this frame; these are read-only observations.
            for (int i = 0; i < input.EventCount; i++)
            {
                var e = input.EventAt(i);
                if (e.Revision == lastInputRevision && e.Sequence <= lastInputSeq) continue;
                lastInputRevision = e.Revision; lastInputSeq = e.Sequence;
                if (e.Action == GameAction.Point || e.Action == GameAction.Scroll || e.Action == GameAction.Navigate || e.Action == GameAction.MapPan) continue;
                if (e.Phase == InputPhase.Changed && heldValues.TryGetValue(e.Action, out var last) && last.Item1 == e.Value.X && last.Item2 == e.Value.Y) continue;
                heldValues[e.Action] = (e.Value.X, e.Value.Y);
                recorder.Changed(ReplayKind.Input, (long)e.Action, Ms, Tick, (long)e.Action, (long)e.Phase,
                    ReplayNumbers.Quantize(e.Value.X, 256), ReplayNumbers.Quantize(e.Value.Y, 256), (long)e.Device.Family, input.IsConsumed(i) ? 1 : 0, e.Sequence);
            }
        }
        void ReadCombat()
        {
            if (!arena) return;
            int pages = 0;
            while (pages++ < 8 && arena.Commands.TryReadOutcomes(outcomeCursor, outcomes, out var page))
            {
                outcomeCursor = page.Next;
                if (page.DroppedBefore > 0) recorder.Add(ReplayKind.Gap, Ms, Tick, 1, page.DroppedBefore, 0);
                for (int i = 0; i < page.Count; i++)
                {
                    var e = outcomes[i]; if (e.Command.Kind == CombatCommandKind.EnemyChase) continue;
                    long ms = Ms;
                    recorder.Add(ReplayKind.Command, ms, e.TickCount, ActorId(e.Command.Actor), (long)e.Command.Kind, (long)e.State, (long)e.Reason,
                        e.EventSequence, e.Command.Sequence, Str(e.ExecutionId.ToString("N"), ms), ReplayNumbers.Float(e.Now), Str(e.Snapshot.Action.ContentId, ms));
                }
                if (page.Count < outcomes.Length) break;
            }
            pages = 0;
            while (pages++ < 8 && arena.HitEvents.TryRead(hitCursor, hits, out var page))
            {
                hitCursor = page.Next;
                if (page.DroppedBefore > 0) recorder.Add(ReplayKind.Gap, Ms, Tick, 2, page.DroppedBefore, 0);
                for (int i = 0; i < page.Count; i++)
                {
                    var e = hits[i]; long ms = Ms;
                    recorder.Add(ReplayKind.Hit, ms, e.TickCount, ActorId(e.Origin.Source), ActorId(e.Target), e.EventSequence,
                        ReplayNumbers.Float(e.Damage), ReplayNumbers.Float(e.Absorbed), ReplayNumbers.Float(e.Before.Health), ReplayNumbers.Float(e.After.Health),
                        ReplayNumbers.Quantize(e.TargetPose.X), ReplayNumbers.Quantize(e.TargetPose.Y), Str(e.Origin.ActionId, ms),
                        Str(e.Origin.ExecutionId.ToString("N"), ms), e.Origin.Command.Sequence, ReplayNumbers.Float(e.Now), ActorId(e.Origin.Reflector), (long)e.Origin.Kind);
                }
                if (page.Count < hits.Length) break;
            }
        }
        void CaptureWorld(long ms, bool force)
        {
            var cam = arena ? arena.ViewCamera : Camera.main;
            if (cam) recorder.Changed(ReplayKind.Entity, 0, ms, Tick, 0, ReplayNumbers.Quantize(cam.transform.position.x), ReplayNumbers.Quantize(cam.transform.position.y),
                0, 0, 0, 0, 0, ReplayNumbers.Float(cam.orthographicSize), 0, 0, 0);
            if (!arena || !arena.Player) return;
            if (arena.ReadModel.TryReadPlayer(combatSession, out var player)) CaptureActor(player, ms);
            if (!force && Time.realtimeSinceStartupAsDouble < nextEnemies) return;
            nextEnemies = Time.realtimeSinceStartupAsDouble + .2; currentVisible.Clear();
            float halfHeight = cam ? cam.orthographicSize + 2 : 20, halfWidth = cam ? cam.orthographicSize * cam.aspect + 2 : 30;
            Vector3 center = cam ? cam.transform.position : arena.Player.transform.position;
            foreach (var enemy in arena.enemies)
            {
                if (!enemy || Mathf.Abs(enemy.transform.position.x - center.x) > halfWidth || Mathf.Abs(enemy.transform.position.y - center.y) > halfHeight) continue;
                if (arena.TryBindActor(enemy, out var handle) && arena.ReadModel.TryReadActor(handle, out var state)) { currentVisible.Add(ActorId(handle)); CaptureActor(state, ms); }
            }
            CaptureShots(arena.projectiles, center, halfWidth, halfHeight, ms);
            CaptureShots(arena.playerShots, center, halfWidth, halfHeight, ms);
            CaptureShots(arena.bombs, center, halfWidth, halfHeight, ms);
            leaving.Clear(); foreach (long id in visible) if (!currentVisible.Contains(id)) leaving.Add(id);
            foreach (long id in leaving) { recorder.Add(ReplayKind.EntityExit, ms, Tick, id, 1); recorder.Forget(ReplayKind.Entity, id); }
            visible.Clear(); foreach (long id in currentVisible) visible.Add(id);
        }
        void CaptureShots<T>(List<T> shots, Vector3 center, float width, float height, long ms) where T : Component
        {
            foreach (var shot in shots)
            {
                if (!shot) continue; var p = shot.transform.position;
                if (Mathf.Abs(p.x - center.x) > width || Mathf.Abs(p.y - center.y) > height) continue;
                if (!objectIds.TryGetValue(shot.GetEntityId(), out var id)) objectIds.Add(shot.GetEntityId(), id = nextEntity++);
                currentVisible.Add(id);
                recorder.Changed(ReplayKind.Entity, id, ms, Tick, id, ReplayNumbers.Quantize(p.x), ReplayNumbers.Quantize(p.y), 0, 0, 0, 0, 0, 0, 0, 2, 0);
            }
        }
        void CaptureActor(ActorReadSnapshot a, long ms)
        {
            recorder.Changed(ReplayKind.Entity, ActorId(a.Handle), ms, a.TickCount, ActorId(a.Handle), ReplayNumbers.Quantize(a.Kinematics.X), ReplayNumbers.Quantize(a.Kinematics.Y),
                a.Kinematics.Facing, ReplayNumbers.Float(a.Vitals.Health), ReplayNumbers.Float(a.Vitals.Maximum), ReplayNumbers.Float(a.Vitals.Shield),
                Str(a.Action.ContentId, ms), ReplayNumbers.Float(a.Action.StartedAt), Str(a.ActiveWeaponContentId, ms), a.IsPlayer ? 1 : 0, (long)a.Action.Phase);
            if (a.IsPlayer && a.Resources.HasLoadout)
            {
                WeaponChargeReadSnapshot[] resources = { a.Resources.Slot0, a.Resources.Slot1 };
                for (int i = 0; i < resources.Length; i++)
                {
                    var w = resources[i];
                    recorder.Changed(ReplayKind.Weapon, i, ms, a.TickCount, i, Str(w.InstanceId.ToString("N"), ms), Str(w.FormId, ms), Str(w.SpecialId, ms),
                        w.Capacity, w.Available, ReplayNumbers.Float(w.Period), ReplayNumbers.Float(w.NextReady), a.Resources.ActiveSlot == i ? 1 : 0);
                }
            }
        }
        void CaptureUi(long ms)
        {
            var mouse = Mouse.current;
            if (focused && mouse != null)
            {
                var p = mouse.position.ReadValue(); var cam = arena ? arena.ViewCamera : Camera.main;
                Rect viewport = cam ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
                bool inside = viewport.Contains(p);
                recorder.Changed(ReplayKind.Pointer, 0, ms, Tick, 0, inside ? ReplayNumbers.Quantize((p.x - viewport.x) / viewport.width, 4096) : -1,
                    inside ? ReplayNumbers.Quantize(1 - (p.y - viewport.y) / viewport.height, 4096) : -1, Cursor.visible ? 1 : 0, (long)Cursor.lockState);
                for (int button = 0; button < 3; button++)
                {
                    var control = button == 0 ? mouse.leftButton : button == 1 ? mouse.rightButton : mouse.middleButton;
                    if (pointerFrame != Time.frameCount) { pointerFrame = Time.frameCount; pointerButtons = 0; }
                    int down = 1 << (button * 2), up = 1 << (button * 2 + 1);
                    if (control.wasPressedThisFrame && (pointerButtons & down) == 0) { recorder.Add(ReplayKind.UiState, ms, Tick, 3 + button, 1, 0); pointerButtons |= down; }
                    if (control.wasReleasedThisFrame && (pointerButtons & up) == 0) { recorder.Add(ReplayKind.UiState, ms, Tick, 3 + button, 0, 0); pointerButtons |= up; }
                }
            }
            foreach (var marker in ReplayUiTarget.Active)
            {
                if (!marker || !marker.isActiveAndEnabled || string.IsNullOrEmpty(marker.Id)) continue;
                int id = Str(marker.Id, ms); if (marker.IsScreen) recorder.Changed(ReplayKind.Screen, id, ms, Tick, id, marker.OpenVersion);
                var rect = marker.transform as RectTransform; if (!rect) continue;
                rect.GetWorldCorners(corners); var canvas = marker.GetComponentInParent<Canvas>(); var uiCam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]);
                var selectable = marker.GetComponent<Selectable>();
                recorder.Changed(ReplayKind.UiRect, id, ms, Tick, id, ReplayNumbers.Quantize(a.x / Math.Max(1, Screen.width), 4096), ReplayNumbers.Quantize(1 - b.y / Math.Max(1, Screen.height), 4096),
                    ReplayNumbers.Quantize((b.x - a.x) / Math.Max(1, Screen.width), 4096), ReplayNumbers.Quantize((b.y - a.y) / Math.Max(1, Screen.height), 4096), selectable && selectable.IsInteractable() ? 1 : 0, marker.OpenVersion);
                var scroll = marker.GetComponent<ScrollRect>();
                if (scroll)
                {
                    recorder.Changed(ReplayKind.UiState, 100000L + id * 2, ms, Tick, 10, id, ReplayNumbers.Float(scroll.normalizedPosition.x));
                    recorder.Changed(ReplayKind.UiState, 100001L + id * 2, ms, Tick, 11, id, ReplayNumbers.Float(scroll.normalizedPosition.y));
                }
            }
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var target = selected ? selected.GetComponentInParent<ReplayUiTarget>() : null;
            recorder.Changed(ReplayKind.UiState, 1, ms, Tick, 1, target ? Str(target.Id, ms) : 0, 0);
            var field = selected ? selected.GetComponent<InputField>() : null;
            recorder.Changed(ReplayKind.UiState, 2, ms, Tick, 2, field && field.isFocused && target ? Str(target.Id, ms) : 0, 0);
        }
        void UiObserved(ReplayUiTarget marker, int kind) => Guard(() =>
        {
            if (recorder == null || !focused || string.IsNullOrEmpty(marker.Id)) return;
            long ms = Ms;
            if (kind < 4 || kind == 6) { CaptureWorld(ms, true); CaptureUi(ms); }
            int id = Str(marker.Id, ms);
            if (kind == 6) recorder.Add(ReplayKind.UiActivate, ms, Tick, id, 0);
            else if (kind < 4) recorder.Add(ReplayKind.UiState, ms, Tick, kind < 2 ? 0 : 1, kind % 2 == 0 ? id : 0, 1);
            else if (marker.IsScreen) recorder.Add(ReplayKind.Screen, ms, Tick, id, kind == 4 ? marker.OpenVersion : 0);
            if (kind == 5) recorder.Add(ReplayKind.UiRect, ms, Tick, id, 0, 0, 0, 0, -1, marker.OpenVersion);
        });
        void CaptureClock(long ms) => recorder.Changed(ReplayKind.Clock, 0, ms, Tick, focused ? 1 : 0, ReplayNumbers.Float(Time.timeScale),
            arena && arena.Controls.Current.SimulationPaused ? 1 : 0, Screen.width, Screen.height, ReplayNumbers.Float(Screen.safeArea.x), ReplayNumbers.Float(Screen.safeArea.y), arena ? (int)arena.Controls.Current.Blocked : 0);
        void CaptureRun(long ms)
        {
            var host = Chapter1RunHost.Instance; var run = host ? host.Flow?.Current : null;
            bool inRun = SceneManager.GetActiveScene().name == "Chapter1Procedural" || SceneManager.GetActiveScene().name == "Chapter2Procedural";
            runId = inRun && run != null ? run.RunId.ToString("N") : "";
            recorder.Changed(ReplayKind.Run, 0, ms, Tick, Str(runId, ms), run?.Seed ?? 0, run?.Chapter ?? 0, run?.Kills ?? 0, run?.Result.HasValue == true ? (int)run.Result.Value.Outcome + 1 : 0);
            if (inRun && run?.Economy != null) recorder.Changed(ReplayKind.Economy, 0, ms, Tick, 0, run.Economy.Gold);
            if (inRun && run?.Growth != null && growthRevision != run.Growth.Revision)
            {
                var growth = run.Growth; growthRevision = growth.Revision;
                recorder.Add(ReplayKind.Growth, ms, Tick, 0, growthRevision, growth.AttackLevel, 0, 0, 0, 0);
                foreach (var palette in growth.Palettes)
                    for (int i = 0; i < palette.Slots.Count; i++)
                    {
                        var s = palette.Slots[i];
                        recorder.Add(ReplayKind.Growth, ms, Tick, 1, Str(palette.Id.ToString("N"), ms), Str(palette.BoundWeapon.ToString("N"), ms), i, Str(s.Color, ms), Str(s.Part?.id, ms), s.Level);
                    }
                foreach (var ink in growth.Inks) recorder.Add(ReplayKind.Growth, ms, Tick, 2, Str(ink.Id.ToString("N"), ms), Str(ink.Part.id, ms), 0, 0, 0, 0);
                recorder.Add(ReplayKind.Growth, ms, Tick, 3, 0, 0, 0, 0, 0, 0);
            }
        }
        void OnApplicationFocus(bool value) => Guard(() => { focused = value; if (recorder != null) { recorder.Add(ReplayKind.Clock, Ms, Tick, value ? 1 : 0, ReplayNumbers.Float(Time.timeScale), 0, Screen.width, Screen.height, 0, 0, 0); if (!value) recorder.Add(ReplayKind.Pointer, Ms, Tick, 0, -1, -1, 0, 0); } });
        void OnApplicationQuit() => Guard(() => { CloseChunk(); pending?.GetAwaiter().GetResult(); CompletePending(); if (recordingEnabled) store?.MarkSession(session, Ms, Math.Max(0, nextSequence - 1), true, LastError != null); ReplayBrowserStorage.Sync(); });
        void OnApplicationPause(bool paused) { if (paused) Guard(() => { CloseChunk(); pending?.GetAwaiter().GetResult(); CompletePending(); ReplayBrowserStorage.Sync(); }); }
        void OnDestroy() { ReplayUiTarget.Observed -= UiObserved; SceneManager.activeSceneChanged -= SceneChanged; if (instance == this) instance = null; }
    }
}
