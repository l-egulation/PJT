using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using ForestCombatV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stage1VillageExperiment.Editor
{
    // The camera snapshots are inspection evidence. Reachability results below come only from
    // common-motor input replay beginning at the authored spawn, never from Warp().
    [InitializeOnLoad]
    public static class Stage1VillageVerification
    {
        const string Pending = "Stage1VillageVerification.Pending";
        const string OutputKey = "Stage1VillageVerification.Output";
        const string EntryRequested = "Stage1VillageVerification.EntryRequested";
        const string SavedOptionsEnabled = "Stage1VillageVerification.SavedOptionsEnabled";
        const string SavedOptions = "Stage1VillageVerification.SavedOptions";
        const string ErrorsKey = "Stage1VillageVerification.Errors";
        const string ReloadLocked = "Stage1VillageVerification.ReloadLocked";
        const float Dt = 1f / 120;
        static Stage1VillageWorld world;
        static CombatArena arena;
        static CombatArena.ManualSimulation simulation;
        static VillageArtTransition art;
        static readonly VillageArtMode[] artModes = { VillageArtMode.Color, VillageArtMode.Sketch, VillageArtMode.Region };
        static readonly VillageArtMode[] motionModes = { VillageArtMode.Authored, VillageArtMode.Color, VillageArtMode.Sketch, VillageArtMode.Region, VillageArtMode.Blend };
        static readonly float[] blendWeights = { 0, .25f, .5f, .75f, 1 };
        const int PixelSampleStep = 8;
        const float AuthoredSketchUntil = 5056, AuthoredNormalFrom = 5760, AuthoredEdgeCameraX = 5408;
        static Vector2[] authoredProbes;
        static float[] authoredProbeWeights;
        static Report report;
        static double deadline, captureAfter, quietUntil;
        static int phase, zone, routeIndex;
        static Route[] routes;
        static readonly List<string> errors = new List<string>();
        static readonly int[] routeZones = Environment.GetEnvironmentVariable("STAGE1_VILLAGE_VERIFY_ALL_ROUTES") == "1"
            ? Enumerable.Range(1, 25).ToArray() : new[] { 2, 4, 10, 24, 25 };
        static string Output => SessionState.GetString(OutputKey, Path.GetFullPath("../outputs/stage1-village/verification"));

        [Serializable] public sealed class Check { public string name, evidence; public bool pass; }
        [Serializable] public sealed class AnchorResult { public int zone; public string name, contactEvidence; public float requestedX, requestedY, actualX, actualY; public bool clear, grounded, sameFloor; }
        [Serializable] public sealed class ReplayStep
        {
            public int step, ticks, intermediateDropInputs;
            public string kind;
            public float startX, startY, targetX, targetY, actualX, actualY;
            public bool reached;
            public string failure;
            public List<string> inputEvents = new List<string>();
        }
        [Serializable] public sealed class ReplayResult { public int zone; public bool passed; public int completedSteps; public string failure; public List<ReplayStep> steps = new List<ReplayStep>(); }
        [Serializable] public sealed class MotionFrame
        {
            public string file, mode;
            public int zone, tick;
            public float playerX, playerY, cameraX, cameraY, farBottomScreenPixels, weight, boundaryPixels;
        }
        [Serializable] public sealed class ArtFrame
        {
            public string file, mode;
            public float weight, boundaryPixels, authoredSketchUntilPixels, authoredNormalFromPixels;
            public Vector2 feet, cameraPixels;
        }
        sealed class SceneSnapshot
        {
            readonly Transform[] transforms;
            readonly Matrix4x4[] matrices;
            readonly Vector2 feet, velocity, cameraPixels;
            readonly bool grounded;
            readonly long simulationTicks;
            readonly float cameraSize, cameraAspect;

            public SceneSnapshot()
            {
                var nodes = new HashSet<Transform>(world.GetComponentsInChildren<Transform>(true));
                foreach (var node in arena.Player.GetComponentsInChildren<Transform>(true)) nodes.Add(node);
                nodes.Add(world.View.transform);
                transforms = nodes.ToArray();
                matrices = new Matrix4x4[transforms.Length];
                for (int i = 0; i < transforms.Length; i++) matrices[i] = transforms[i].localToWorldMatrix;
                feet = arena.Player.Position; velocity = arena.Player.velocity; grounded = arena.Player.grounded;
                cameraPixels = new Vector2(world.CameraX, world.CameraY);
                cameraSize = world.View.orthographicSize; cameraAspect = world.View.aspect;
                simulationTicks = arena.SimulationTickCount;
            }

            public bool Unchanged(out string evidence)
            {
                evidence = transforms.Length + " object transforms, player motor state and camera projection held fixed; no simulation ticks advanced.";
                if (!feet.Equals(arena.Player.Position) || !velocity.Equals(arena.Player.velocity) || grounded != arena.Player.grounded ||
                    simulationTicks != arena.SimulationTickCount || !cameraPixels.Equals(new Vector2(world.CameraX, world.CameraY)) ||
                    cameraSize != world.View.orthographicSize || cameraAspect != world.View.aspect)
                { evidence = "Player, simulation or camera state changed during a display-only comparison."; return false; }
                for (int i = 0; i < transforms.Length; i++)
                    if (!transforms[i] || !matrices[i].Equals(transforms[i].localToWorldMatrix))
                    { evidence = "Object transform changed: " + (transforms[i] ? transforms[i].name : "destroyed object"); return false; }
                return true;
            }
        }
        [Serializable] public sealed class Report
        {
            public bool pass, fullRouteVerified, exitVerified;
            public string unityVersion, scene, controller, routeSource, routeSha256;
            public float simulationStep;
            public int surfaces, solidRectangles, oneWayPlatforms, areas, captures;
            public string scope = "All authored anchors checked with actual native body and settling; local walk/jump/dash/platform behavior; selected spawn-to-zone input replays. Warps are used only for isolated checks and camera captures.";
            public List<Check> checks = new List<Check>();
            public List<AnchorResult> anchors = new List<AnchorResult>();
            public List<ReplayResult> routes = new List<ReplayResult>();
            public string initialAuthoredCaptureFile;
            public List<string> authoredCaptureFiles = new List<string>();
            public List<string> normalCaptureFiles = new List<string>();
            public List<string> artCaptureFiles = new List<string>();
            public List<ArtFrame> artFrames = new List<ArtFrame>();
            public List<MotionFrame> motionFrames = new List<MotionFrame>();
            public List<string> errors = new List<string>();
            public List<string> limitations = new List<string>();
        }
        [Serializable] sealed class RouteFile { public Route[] routes; }
        [Serializable] sealed class ErrorStore { public List<string> values = new List<string>(); }
        [Serializable] sealed class Route { public int zone; public string name; public float[] target; public bool reachable; public Flight[] steps; }
        [Serializable] sealed class Flight { public float[] from, to; public string kind; public float duration, axisSpeed, secondJumpTime; }

        static Stage1VillageVerification()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            if (SessionState.GetBool(Pending, false))
            {
                string saved = SessionState.GetString(ErrorsKey, "");
                if (!string.IsNullOrEmpty(saved)) errors.AddRange(JsonUtility.FromJson<ErrorStore>(saved).values);
            }
        }

        public static void Run()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Use Unity 6000.6.5f1.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start village verification from edit mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string output = Environment.GetEnvironmentVariable("STAGE1_VILLAGE_VERIFY_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("../outputs/stage1-village/verification");
            SessionState.SetString(OutputKey, Path.GetFullPath(output));
            Directory.CreateDirectory(Output);
            if (!File.Exists(Stage1VillageBuilder.ScenePath) && !Stage1VillageBuilder.TryCreate()) return;
            EditorSceneManager.OpenScene(Stage1VillageBuilder.ScenePath);
            SessionState.SetBool(SavedOptionsEnabled, EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(SavedOptions, (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(EntryRequested, false);
            SessionState.SetBool(ReloadLocked, false);
            SessionState.SetString(ErrorsKey, "");
            SessionState.SetBool(Pending, true);
            errors.Clear();
            report = null;
            phase = zone = routeIndex = 0;
            deadline = EditorApplication.timeSinceStartup + 900;
            quietUntil = 0;
            // Scene creation and forced asset imports can queue a domain reload after their
            // API call returns. Entering play from this call stack would hot-reload the live
            // combat runtime and destroy its nonserialized owners. Wait in edit mode first.
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Pending, false) || (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
            errors.Add(message + "\n" + stack);
            SessionState.SetString(ErrorsKey, JsonUtility.ToJson(new ErrorStore { values = errors }));
        }

        static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            LockLiveRuntime();
        }

        static void LockLiveRuntime()
        {
            if (SessionState.GetBool(ReloadLocked, false)) return;
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            SessionState.SetBool(ReloadLocked, true);
        }

        static void ReleaseEditorPolicy()
        {
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(SavedOptionsEnabled, false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(SavedOptions, 0);
            if (!SessionState.GetBool(ReloadLocked, false)) return;
            SessionState.SetBool(ReloadLocked, false);
            AssetDatabase.AllowAutoRefresh();
            EditorApplication.UnlockReloadAssemblies();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 900;
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Verification exceeded its 15 minute deadline."); return; }
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quietUntil = 0; return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(EntryRequested, false)) return;
                if (quietUntil == 0) { quietUntil = EditorApplication.timeSinceStartup + 8; return; }
                if (EditorApplication.timeSinceStartup < quietUntil) return;
                // A fresh domain is required for real gameplay verification; restore the user's
                // option after this run rather than inheriting cached experiment static state.
                EditorSettings.enterPlayModeOptionsEnabled = false;
                SessionState.SetBool(EntryRequested, true);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            LockLiveRuntime();
            try
            {
                if (report == null)
                {
                    world = Object.FindAnyObjectByType<Stage1VillageWorld>();
                    if (!world || !world.Ready) { if (errors.Count > 0) Finish("Village initialization produced a runtime error."); return; }
                    arena = world.arena;
                    if (!arena || !arena.Player) return;
                    var presentation = world.GetComponent<VillagePresentation>();
                    if (!presentation || !presentation.Ready)
                    {
                        if (errors.Count > 0) Finish("Village presentation initialization produced a runtime error.");
                        return;
                    }
                    art = world.GetComponent<VillageArtTransition>();
                    if (!art) throw new InvalidOperationException("Village art transition component is missing from the ready presentation.");
                    arena.Player.UseTestInput = true;
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.CancelAttack(); }
                    simulation = arena.AcquireManualSimulation();
                    world.showHud = false;
                    report = new Report
                    {
                        unityVersion = Application.unityVersion,
                        scene = world.gameObject.scene.path,
                        controller = arena.Player.GetType().FullName,
                        simulationStep = Dt,
                        surfaces = world.SurfaceCount,
                        solidRectangles = world.Data.rects.Length,
                        oneWayPlatforms = world.Data.platforms.Length,
                        areas = world.Data.views.Length
                    };
                    AddCheck(Object.FindObjectsByType<CombatArena>().Length == 1, "One shared combat runtime", report.controller);
                    AddCheck(world.Data.views.Length == 24, "Twenty-four authored village areas", world.Data.views.Length.ToString());
                    AddCheck(arena.CompositionProfile == CombatCompositionProfile.Game, "Shared Game combat composition", arena.CompositionProfile.ToString());
                    InitializeAuthoredEvidence();
                    LoadRoutes();
                    phase = 1;
                    return;
                }
                if (phase == 1)
                {
                    CheckAnchors();
                    CheckLocalMovement();
                    CheckOneWayPlatforms();
                    CheckSharedMap();
                    CheckArtworkContacts();
                    phase = 2;
                    Save();
                    return;
                }
                if (phase == 2)
                {
                    if (routeIndex < routeZones.Length)
                    {
                        int wanted = routeZones[routeIndex++];
                        var route = routes?.FirstOrDefault(item => item.zone == wanted);
                        if (route != null) report.routes.Add(ReplayFromSpawn(route));
                        else report.limitations.Add("No replay plan was available for zone " + wanted + ".");
                        Save();
                        return;
                    }
                    phase = 3;
                    zone = 0;
                }
                if (phase == 3 || phase == 5)
                {
                    if (zone >= world.Data.views.Length)
                    {
                        if (phase == 3)
                        {
                            CaptureAuthoredEdgeFrames();
                            AddCheck(report.authoredCaptureFiles.Count == 24, "All 24 default authored areas captured before review modes",
                                "No SetDisplay or SetBoundary call precedes these area captures; the 5056..5760px range remains world anchored.");
                            zone = 0;
                            phase = 5;
                            Save();
                            return;
                        }
                        CaptureAuthoredEdgeComparisons();
                        CaptureArtComparisons();
                        CaptureMotionComparisons();
                        art.UseAuthoredRegions();
                        CaptureOverview();
                        VillageTerrainArtReview.Capture(Output);
                        Finish(null);
                        return;
                    }
                    arena.Player.ResetPlayer();
                    world.Warp(zone);
                    Advance(18);
                    world.RefreshCamera(0, true);
                    arena.Presentation.Draw(Dt);
                    arena.RangedPresentation?.Tick();
                    captureAfter = EditorApplication.timeSinceStartup + .12;
                    phase++;
                    return;
                }
                if ((phase == 4 || phase == 6) && EditorApplication.timeSinceStartup >= captureAfter)
                {
                    if (phase == 4) CaptureAuthoredArea(zone);
                    else { CaptureAreaModes(zone); report.captures++; }
                    zone++;
                    phase--;
                    Save();
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        static void LoadRoutes()
        {
            string path = Environment.GetEnvironmentVariable("STAGE1_VILLAGE_ROUTES");
            if (string.IsNullOrWhiteSpace(path)) path = Path.GetFullPath("../data/stage1-village-experiment/ballistic_routes.json");
            if (!File.Exists(path))
            {
                report.limitations.Add("Native complete-route replay unavailable because no ballistic_routes.json path was provided. Set STAGE1_VILLAGE_ROUTES to the layout planner output.");
                return;
            }
            string source = File.ReadAllText(path);
            routes = JsonUtility.FromJson<RouteFile>("{\"routes\":" + source + "}").routes;
            report.routeSource = path;
            using (var sha = SHA256.Create()) report.routeSha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            if (routes != null)
                foreach (var route in routes)
                    if (route.steps != null)
                        foreach (var step in route.steps)
                            if (step.kind == "walk" && step.from?.Length == 2 && step.to?.Length == 2 && step.duration <= 0)
                            {
                                // The planner represents a supported one-cell walk as topology
                                // only. Derive its input duration from the actual common run speed.
                                float distance = step.to[0] - step.from[0];
                                step.duration = Mathf.Abs(distance) / arena.Movement.RunSpeed;
                                step.axisSpeed = Mathf.Sign(distance) * arena.Movement.RunSpeed;
                            }
            bool valid = routes != null && routes.All(route => route.steps != null && route.steps.All(step =>
                step.from?.Length == 2 && step.to?.Length == 2 && step.duration > 0 && !float.IsNaN(step.duration) && !float.IsInfinity(step.duration)));
            if (!valid) { report.limitations.Add("Planner output contained invalid or nonpositive flight durations and was not replayed."); routes = null; }
        }

        static void CheckAnchors()
        {
            for (int index = 0; index < world.Data.views.Length; index++)
            {
                arena.Player.ResetPlayer();
                var feet = world.GetLandmarkSafeFeet(index);
                world.Warp(index);
                bool clear = ClearBody(feet);
                Advance(45);
                var actual = arena.Player.Position;
                report.anchors.Add(new AnchorResult
                {
                    zone = index + 1, name = world.Data.views[index].name,
                    requestedX = feet.x, requestedY = feet.y, actualX = actual.x, actualY = actual.y,
                    clear = clear, grounded = arena.Player.grounded, contactEvidence = DescribeSolidContacts(actual),
                    sameFloor = Mathf.Abs(actual.y - feet.y) <= .06f && Mathf.Abs(actual.x - feet.x) <= .03f
                });
            }
            var failures = report.anchors.Where(anchor => !anchor.clear || !anchor.grounded || !anchor.sameFloor).Select(anchor => anchor.zone.ToString()).ToArray();
            AddCheck(failures.Length == 0, "All 24 zone anchors settle on the authored floor", failures.Length == 0 ? "Full native player body clear; grounded after 45 real simulation steps at every area." : "Failed zones: " + string.Join(",", failures));
        }

        static bool ClearBody(Vector2 feet)
        {
            var player = arena.Player;
            var center = feet + new Vector2(player.offset.x * player.facing, player.offset.y);
            var half = player.size * .5f;
            // Unity's overlap broad phase includes contact skin around a touching floor.
            // All authored surfaces are axis-aligned boxes: test actual positive overlap
            // of their native collider bounds against the full (not shrunken) player body.
            foreach (var surface in world.TerrainSurfaces)
            {
                if (!surface || surface.oneWay) continue;
                var bounds = surface.GetComponent<BoxCollider2D>().bounds;
                float overlapX = Mathf.Min(center.x + half.x, bounds.max.x) - Mathf.Max(center.x - half.x, bounds.min.x);
                float overlapY = Mathf.Min(center.y + half.y, bounds.max.y) - Mathf.Max(center.y - half.y, bounds.min.y);
                if (overlapX > .001f && overlapY > .001f) return false;
            }
            return true;
        }

        static bool OnDroppableSupport()
        {
            var player = arena.Player;
            if (!player.grounded) return false;
            float left = player.Center.x - player.size.x * .5f + .04f;
            float right = player.Center.x + player.size.x * .5f - .04f;
            bool droppable = false;
            foreach (var surface in world.TerrainSurfaces)
            {
                if (!surface) continue;
                var bounds = surface.GetComponent<BoxCollider2D>().bounds;
                if (bounds.max.x <= left || bounds.min.x >= right || Mathf.Abs(bounds.max.y - player.Position.y) > .06f) continue;
                if (!surface.oneWay || surface.blockDropThrough) return false;
                droppable = true;
            }
            return droppable;
        }

        static string DescribeSolidContacts(Vector2 feet)
        {
            var player = arena.Player;
            var center = feet + new Vector2(player.offset.x * player.facing, player.offset.y);
            var half = player.size * .5f;
            var details = new List<string>();
            foreach (var hit in Physics2D.OverlapBoxAll(center, player.size - new Vector2(.01f, .01f), 0))
            {
                var surface = hit.GetComponent<ArenaSurface>();
                if (!surface || surface.oneWay) continue;
                var bounds = hit.bounds;
                float overlapX = Mathf.Min(center.x + half.x, bounds.max.x) - Mathf.Max(center.x - half.x, bounds.min.x);
                float overlapY = Mathf.Min(center.y + half.y, bounds.max.y) - Mathf.Max(center.y - half.y, bounds.min.y);
                details.Add(hit.name + " overlapX=" + overlapX.ToString("F5") + " overlapY=" + overlapY.ToString("F5"));
            }
            return string.Join("; ", details);
        }

        static void Place(Vector2 feet, Vector2 velocity = default)
        {
            arena.Player.ResetPlayer();
            arena.Player.Position = feet;
            arena.Player.velocity = velocity;
            Physics2D.SyncTransforms();
            arena.Player.grounded = arena.Player.ProbeGround();
        }

        static void Advance(int ticks, int axis = 0)
        {
            for (int index = 0; index < ticks; index++) simulation.Step(Dt, new CombatInput(axis));
        }

        static void CheckLocalMovement()
        {
            // The first area is tested exactly as delivered, with the authored spawn and terrain.
            Place(Stage1VillageWorld.PixelFeet(world.Data.spawn.x, world.Data.spawn.y));
            Advance(24);
            var start = arena.Player.Position;
            var solidProbe = world.TerrainSurfaces.First(surface => !surface.oneWay && surface.GetComponent<BoxCollider2D>().size.y >= 2);
            var solidCenter = solidProbe.GetComponent<BoxCollider2D>().bounds.center;
            AddCheck(ClearBody(start) && !ClearBody((Vector2)solidCenter - new Vector2(0, arena.Player.offset.y)),
                "Full player body clearance rejects actual solid penetration", "Exact native collider bounds permit touching support but reject a body inside solid terrain.");
            Advance(24, 1);
            float walked = arena.Player.Position.x - start.x;
            AddCheck(walked > 1.25f && arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - start.y) < .06f,
                "Common keyboard-direction walking on the starting floor", "24 ticks: " + walked.ToString("F4") + " world units; grounded=" + arena.Player.grounded);

            Place(start);
            Advance(3);
            simulation.Step(Dt, new CombatInput(pressed: CombatButtons.Jump));
            float highest = arena.Player.Position.y;
            for (int tick = 0; tick < 145; tick++) { simulation.Step(Dt); highest = Mathf.Max(highest, arena.Player.Position.y); }
            AddCheck(highest - start.y > 2.7f && arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - start.y) < .06f,
                "Single jump and landing use the shared motor", "Rise=" + (highest - start.y).ToString("F4") + "; landing=" + arena.Player.Position);

            Place(start);
            Advance(3);
            simulation.Step(Dt, new CombatInput(pressed: CombatButtons.Jump));
            highest = arena.Player.Position.y;
            for (int tick = 1; tick < 180; tick++)
            {
                simulation.Step(Dt, new CombatInput(pressed: tick == 36 ? CombatButtons.Jump : CombatButtons.None));
                highest = Mathf.Max(highest, arena.Player.Position.y);
            }
            AddCheck(highest - start.y > 5.4f && arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - start.y) < .06f,
                "Double jump and landing use the shared motor", "Rise=" + (highest - start.y).ToString("F4") + "; landing=" + arena.Player.Position);

            Place(start);
            Advance(3);
            arena.Player.SyncFacing(1);
            simulation.Step(Dt, new CombatInput(1, CombatButtons.Dash));
            Advance(34);
            float dash = arena.Player.Position.x - start.x;
            AddCheck(dash > 4 && dash < 5 && ClearBody(arena.Player.Position), "Common dash remains playable on native village collision", "Dash displacement=" + dash.ToString("F4"));
            AddCheck(!arena.SimulationFaulted, "Shared simulation has no fault", "Tick count=" + arena.SimulationTickCount);
        }

        static void CheckOneWayPlatforms()
        {
            int attempted = 0, landings = 0, drops = 0, ascents = 0;
            foreach (var platform in world.Data.platforms)
            {
                if (attempted >= 8) break;
                float top = -platform.y / 32f;
                float x = (platform.x + platform.w / 2f) / 32f;
                if (platform.blockDropThrough || !ClearBody(new Vector2(x, top + .25f)) || !ClearBody(new Vector2(x, top - 2.1f))) continue;
                attempted++;
                Place(new Vector2(x, top + .25f), Vector2.down * 2);
                Advance(30);
                if (arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - top) < .06f) landings++;
                simulation.Step(Dt, new CombatInput(pressed: CombatButtons.Drop));
                Advance(35);
                if (arena.Player.Position.y < top - .5f) drops++;
                Place(new Vector2(x, top - 2.1f));
                simulation.Step(Dt, new CombatInput(pressed: CombatButtons.Jump));
                float highest = arena.Player.Position.y;
                for (int tick = 0; tick < 75; tick++) { simulation.Step(Dt); highest = Mathf.Max(highest, arena.Player.Position.y); }
                if (highest > top + .4f) ascents++;
            }
            AddCheck(attempted > 0 && landings == attempted, "One-way ledge native landing", landings + "/" + attempted);
            AddCheck(attempted > 0 && drops == attempted, "One-way ledge native drop-through input", drops + "/" + attempted);
            AddCheck(attempted > 0 && ascents == attempted, "One-way ledge upward body passage", ascents + "/" + attempted + "; local seeded airborne-jump passage check, not a spawn-to-platform route proof.");
        }

        static ReplayResult ReplayFromSpawn(Route route)
        {
            var result = new ReplayResult { zone = route.zone };
            Place(Stage1VillageWorld.PixelFeet(world.Data.spawn.x, world.Data.spawn.y));
            Advance(18);
            if (!arena.Player.grounded) { result.failure = "Authored spawn did not settle."; return result; }
            for (int index = 0; index < route.steps.Length; index++)
            {
                var plan = route.steps[index];
                var start = arena.Player.Position;
                var target = new Vector2(plan.to[0], -plan.to[1]);
                var step = new ReplayStep { step = index, kind = plan.kind, startX = start.x, startY = start.y, targetX = target.x, targetY = target.y };
                result.steps.Add(step);
                if (Vector2.Distance(start, new Vector2(plan.from[0], -plan.from[1])) > 1.1f)
                { step.failure = "Actual previous landing is too far from the next authored launch."; result.failure = step.failure; break; }
                bool second = false, airborne = false;
                float accumulator = 0;
                // A descending flight can legitimately land on an intervening one-way
                // ledge. Extra time permits real Drop input through that ledge; the actor
                // remains under the common motor for every tick, with no repositioning.
                int budget = Mathf.CeilToInt(Mathf.Clamp(plan.duration + 2.5f, 1f, 6f) / Dt);
                float ratio = Mathf.Clamp01(Mathf.Abs(plan.axisSpeed) / arena.Movement.RunSpeed);
                float originalFallTime = arena.Player.LastFallTime;
                for (int tick = 0; tick < budget; tick++)
                {
                    float remaining = target.x - arena.Player.Position.x;
                    // Directional input moves a whole native tick and changes the actor's
                    // offset body facing. Stop within one tick instead of oscillating at a
                    // stair lip, where an unnecessary facing reversal pushes the body away.
                    float steeringDeadband = arena.Movement.RunSpeed * Dt;
                    int direction = remaining > steeringDeadband ? 1 : remaining < -steeringDeadband ? -1 : 0;
                    accumulator += ratio;
                    int axis = accumulator >= 1 ? direction : 0;
                    if (accumulator >= 1) accumulator -= 1;
                    // Once the planned duration is reached, close the last sub-tile landing error with actual directional input.
                    if (tick * Dt >= plan.duration && Mathf.Abs(remaining) > .08f) axis = direction;
                    CombatButtons pressed = CombatButtons.None;
                    if (tick == 0)
                        pressed = plan.kind == "drop" ? CombatButtons.Drop : plan.kind == "walk" ? CombatButtons.None : CombatButtons.Jump;
                    if (!second && plan.kind == "double_jump" && tick > 0 && tick * Dt >= plan.secondJumpTime)
                    { pressed |= CombatButtons.Jump; second = true; }
                    bool plannedJumpDelivered = plan.kind != "double_jump" || second;
                    if (tick > 1 && airborne && plannedJumpDelivered && (pressed & CombatButtons.Jump) == 0 &&
                        arena.Player.Position.y > target.y + .08f && OnDroppableSupport())
                    {
                        pressed = CombatButtons.Drop;
                        step.intermediateDropInputs++;
                    }
                    bool wasGrounded = arena.Player.grounded;
                    if (pressed != CombatButtons.None)
                        step.inputEvents.Add("tick=" + tick + " input=" + pressed + " at=" + arena.Player.Position.ToString("F4") +
                            " grounded=" + wasGrounded + " velocityY=" + arena.Player.VelocityY.ToString("F4"));
                    simulation.Step(Dt, new CombatInput(axis, pressed));
                    if (!wasGrounded && arena.Player.grounded)
                        step.inputEvents.Add("tick=" + tick + " landed=" + arena.Player.Position.ToString("F4"));
                    step.ticks++;
                    airborne |= !arena.Player.grounded;
                    bool landing = arena.Player.grounded && (airborne || plan.kind == "walk") &&
                        Mathf.Abs(arena.Player.Position.y - target.y) < .08f && Mathf.Abs(arena.Player.Position.x - target.x) < .15f;
                    if (landing)
                    {
                        Advance(3);
                        step.reached = arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - target.y) < .08f && Mathf.Abs(arena.Player.Position.x - target.x) < .15f;
                        if (step.reached) break;
                    }
                    if (arena.Player.LastFallTime != originalFallTime || arena.Player.Dead || arena.SimulationFaulted)
                    { step.failure = "Actual gameplay recovery/death/simulation fault occurred during replay."; break; }
                }
                step.actualX = arena.Player.Position.x;
                step.actualY = arena.Player.Position.y;
                if (!step.reached)
                {
                    if (step.failure == null) step.failure = "Input replay did not land at the planned support within its bounded flight window.";
                    result.failure = "Step " + index + ": " + step.failure;
                    break;
                }
                result.completedSteps++;
            }
            result.passed = result.completedSteps == route.steps.Length && result.failure == null;
            if (result.passed)
            {
                bool isExit = route.zone == 25 && route.name == "exit" && route.target?.Length == 2;
                if (route.zone > world.Data.views.Length && !isExit)
                {
                    result.passed = false;
                    result.failure = "The extra route has no explicit authored exit target.";
                    return result;
                }
                var destination = isExit ? new Vector2(route.target[0], -route.target[1]) : world.GetLandmarkSafeFeet(route.zone - 1);
                result.passed = arena.Player.grounded && Mathf.Abs(arena.Player.Position.x - destination.x) < .7f && Mathf.Abs(arena.Player.Position.y - destination.y) < .1f;
                if (!result.passed) result.failure = "Completed flight plan does not terminate at the actual zone anchor.";
            }
            return result;
        }

        static void RefreshPresentation(bool snapCamera = true)
        {
            if (snapCamera) world.RefreshCamera(0, true);
            foreach (var component in world.GetComponents<MonoBehaviour>())
            {
                if (!component || component == world) continue;
                var refresh = component.GetType().GetMethod("RefreshForCapture", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                refresh?.Invoke(component, null);
            }
        }

        static void InitializeAuthoredEvidence()
        {
            var probes = new List<Vector2>();
            foreach (var view in world.Data.views) probes.Add(new Vector2(view.x, view.y));
            for (int y = 0; y <= world.Data.height; y += 32)
            {
                probes.Add(new Vector2(AuthoredSketchUntil, y));
                probes.Add(new Vector2(AuthoredEdgeCameraX, y));
                probes.Add(new Vector2(AuthoredNormalFrom, y));
            }
            authoredProbes = probes.ToArray();
            authoredProbeWeights = new float[authoredProbes.Length];
            for (int i = 0; i < authoredProbes.Length; i++) authoredProbeWeights[i] = art.SampleWeight(authoredProbes[i]);
            CheckAuthoredCoordinates("initial startup");
            report.initialAuthoredCaptureFile = "initial-authored.png";
            var presentation = world.GetComponent<VillagePresentation>();
            bool animate = presentation.animateEnvironment;
            presentation.animateEnvironment = false;
            try
            {
                RefreshPresentation();
                arena.Presentation.Draw(0); arena.RangedPresentation?.Tick();
                CaptureArtFrame(report.initialAuthoredCaptureFile);
            }
            finally { presentation.animateEnvironment = animate; }
        }

        static void CheckAuthoredCoordinates(string label)
        {
            bool anchored = art.Mode == VillageArtMode.Authored && art.AuthoredSketchUntilPixels == AuthoredSketchUntil &&
                art.AuthoredNormalFromPixels == AuthoredNormalFrom;
            var failures = new List<string>();
            for (int i = 0; i < authoredProbes.Length; i++)
            {
                Vector2 probe = authoredProbes[i];
                float weight = art.SampleWeight(probe);
                bool expected = i < world.Data.views.Length ? weight == (i < 8 ? 1 : 0) :
                    probe.x <= AuthoredSketchUntil ? weight == 1 : probe.x >= AuthoredNormalFrom ? weight == 0 : weight > 0 && weight < 1;
                if (!expected || weight != authoredProbeWeights[i]) failures.Add(probe + "=" + weight);
            }
            AddCheck(anchored && failures.Count == 0, "Authored sketch 1-8 and normal 9-24 remain world anchored at " + label,
                "Mode=" + art.Mode + "; range=" + art.AuthoredSketchUntilPixels + ".." + art.AuthoredNormalFromPixels +
                "px; all 24 landmark coordinates plus boundary/midpoint probes every 32px vertically; camera=" + new Vector2(world.CameraX, world.CameraY) +
                "; changed/incorrect probes=" + failures.Count + (failures.Count == 0 ? "" : "; " + string.Join(", ", failures)));
        }

        static void CaptureAuthoredArea(int sample)
        {
            var presentation = world.GetComponent<VillagePresentation>();
            bool animate = presentation.animateEnvironment;
            presentation.animateEnvironment = false;
            try
            {
                RefreshPresentation();
                arena.Presentation.Draw(Dt); arena.RangedPresentation?.Tick();
                CheckAuthoredCoordinates("area " + (sample + 1));
                string filename = "zone-" + (sample + 1).ToString("D2") + "-authored.png";
                CaptureArtFrame(filename);
                report.authoredCaptureFiles.Add(filename);
            }
            finally { presentation.animateEnvironment = animate; }
        }

        static void PlaceAuthoredEdge(float cameraCenterX)
        {
            // Inspection only: the gameplay camera is 48px ahead of the feet. Do not
            // move either authored boundary to make a transition appear in the frame.
            Place(Stage1VillageWorld.PixelFeet(cameraCenterX - 48, world.Data.views[8].y));
            RefreshPresentation();
            arena.Presentation.Draw(0); arena.RangedPresentation?.Tick();
        }

        static void CaptureAuthoredEdgeFrames()
        {
            var presentation = world.GetComponent<VillagePresentation>();
            bool animate = presentation.animateEnvironment;
            presentation.animateEnvironment = false;
            try
            {
                foreach (int offset in new[] { 0, -128, 128 })
                {
                    float center = AuthoredEdgeCameraX + offset;
                    PlaceAuthoredEdge(center);
                    CheckAuthoredCoordinates("actual area edge camera " + center);
                    AddCheck(Mathf.Abs(world.View.transform.position.x * 32 - center) < .01f,
                        "Authored edge capture uses actual world camera " + center, "Camera center=" + world.View.transform.position.x * 32 + "; range never relocated.");
                    CaptureArtFrame("authored-boundary-" + Mathf.RoundToInt(center) + ".png");
                }
            }
            finally { presentation.animateEnvironment = animate; }
        }

        static void CaptureAuthoredEdgeComparisons()
        {
            var presentation = world.GetComponent<VillagePresentation>();
            bool animate = presentation.animateEnvironment;
            VillageArtMode savedMode = art.Mode;
            float savedWeight = art.Weight;
            presentation.animateEnvironment = false;
            try
            {
                foreach (int offset in new[] { 0, -128, 128 })
                {
                    float center = AuthoredEdgeCameraX + offset;
                    string suffix = Mathf.RoundToInt(center).ToString();
                    PlaceAuthoredEdge(center);
                    var snapshot = new SceneSnapshot();
                    art.UseAuthoredRegions();
                    CheckAuthoredCoordinates("rendered area edge camera " + center);
                    var authored = CaptureArtFrame("authored-boundary-" + suffix + "-comparison.png", true);
                    art.SetDisplay(VillageArtMode.Color);
                    var color = CaptureArtFrame("normal-boundary-" + suffix + ".png", true);
                    report.normalCaptureFiles.Add("normal-boundary-" + suffix + ".png");
                    art.SetDisplay(VillageArtMode.Sketch);
                    var sketch = CaptureArtFrame("sketch-boundary-" + suffix + ".png", true);
                    CheckAuthoredPixels(color, sketch, authored, "actual area edge camera " + center, offset == 0);
                    AddCheck(snapshot.Unchanged(out string evidence), "Authored area edge shares one unchanged camera and scene with both endpoints at " + center, evidence);
                }
            }
            finally { art.SetDisplay(savedMode, savedWeight); presentation.animateEnvironment = animate; }
        }

        static void CheckAuthoredPixels(Color32[] color, Color32[] sketch, Color32[] authored, string label, bool requireTransition = false)
        {
            int sketchSamples = 0, normalSamples = 0, mismatched = 0, intermediate = 0;
            const int columns = 1920 / PixelSampleStep;
            float cameraLeft = world.View.transform.position.x * 32 - 480;
            for (int i = 0; i < authored.Length; i++)
            {
                float x = cameraLeft + (i % columns) * PixelSampleStep * .5f + .25f;
                if (x <= AuthoredSketchUntil)
                { sketchSamples++; if (PixelDifference(authored[i], sketch[i]) != 0) mismatched++; }
                else if (x >= AuthoredNormalFrom)
                { normalSamples++; if (PixelDifference(authored[i], color[i]) != 0) mismatched++; }
                else if (PixelDifference(color[i], sketch[i]) >= 24 && PixelDifference(authored[i], color[i]) > 3 && PixelDifference(authored[i], sketch[i]) > 3)
                    intermediate++;
            }
            AddCheck(mismatched == 0 && sketchSamples + normalSamples > 0 && (!requireTransition || sketchSamples > 0 && normalSamples > 0 && intermediate >= 256),
                "Rendered authored areas match exact sketch/normal endpoints at " + label,
                "Sketch x<=5056 samples=" + sketchSamples + "; normal x>=5760 samples=" + normalSamples +
                "; endpoint mismatches=" + mismatched + "; intermediate pixels in actual area band=" + intermediate + "; no boundary relocation.");
        }

        static void CaptureAreaModes(int sample)
        {
            var presentation = world.GetComponent<VillagePresentation>();
            bool animate = presentation.animateEnvironment;
            VillageArtMode savedMode = art.Mode;
            float savedWeight = art.Weight, savedBoundary = art.BoundaryPixels;
            presentation.animateEnvironment = false;
            try
            {
                RefreshPresentation();
                arena.Presentation.Draw(Dt); arena.RangedPresentation?.Tick();
                var snapshot = new SceneSnapshot();
                art.UseAuthoredRegions();
                var authored = CaptureArtFrame("zone-" + (sample + 1).ToString("D2") + "-authored-comparison.png", true);
                Color32[] color = null, sketch = null;
                // Only the explicit Region review places its illustrative split in view.
                foreach (var mode in artModes)
                {
                    if (mode == VillageArtMode.Region) art.SetBoundary(world.CameraX + 480);
                    art.SetDisplay(mode);
                    string filename = "zone-" + (sample + 1).ToString("D2") + "-" + ModeName(mode) + ".png";
                    var pixels = CaptureArtFrame(filename, mode != VillageArtMode.Region);
                    if (mode == VillageArtMode.Color) { color = pixels; report.normalCaptureFiles.Add(filename); }
                    else if (mode == VillageArtMode.Sketch) sketch = pixels;
                }
                CheckAuthoredPixels(color, sketch, authored, "area " + (sample + 1));
                AddCheck(snapshot.Unchanged(out string evidence), "Area " + (sample + 1) + " modes preserve scene transforms", evidence);
            }
            finally
            {
                art.SetBoundary(savedBoundary); art.SetDisplay(savedMode, savedWeight);
                presentation.animateEnvironment = animate;
            }
        }

        static string ModeName(VillageArtMode mode) => mode.ToString().ToLowerInvariant();

        static Color32[] CaptureArtFrame(string filename, bool samplePixels = false)
        {
            var pixels = Capture(world.View, filename, 1920, 1080, samplePixels);
            report.artCaptureFiles.Add(filename);
            report.artFrames.Add(new ArtFrame { file = filename, mode = art.Mode.ToString(), weight = art.Weight,
                boundaryPixels = art.BoundaryPixels, authoredSketchUntilPixels = art.AuthoredSketchUntilPixels, authoredNormalFromPixels = art.AuthoredNormalFromPixels,
                feet = arena.Player.Position, cameraPixels = new Vector2(world.CameraX, world.CameraY) });
            return pixels;
        }

        static void CaptureArtComparisons()
        {
            var presentation = world.GetComponent<VillagePresentation>();
            AddCheck(presentation && presentation.Ready && art, "Village paired-art presentation is ready",
                "Rendered endpoints and transition samples below check functionality, not approval of the adopted art style.");
            if (!presentation || !presentation.Ready || !art) return;
            bool animate = presentation.animateEnvironment;
            VillageArtMode savedMode = art.Mode;
            float savedWeight = art.Weight, savedBoundary = art.BoundaryPixels;
            presentation.animateEnvironment = false;
            try
            {
                int[] samples = { 0, 12, 23 };
                string[] names = { "start", "fountain", "boss-approach" };
                for (int i = 0; i < samples.Length; i++)
                {
                    arena.Player.ResetPlayer(); world.Warp(samples[i]); Advance(18);
                    RefreshPresentation(); arena.Presentation.Draw(Dt); arena.RangedPresentation?.Tick();
                    var snapshot = new SceneSnapshot();
                    art.SetBoundary(world.CameraX + 480);
                    art.SetDisplay(VillageArtMode.Color);
                    var color = CaptureArtFrame("color-" + names[i] + ".png", true);
                    art.SetDisplay(VillageArtMode.Sketch);
                    var sketch = CaptureArtFrame("sketch-" + names[i] + ".png", true);
                    int different = 0;
                    for (int p = 0; p < color.Length; p++) if (PixelDifference(color[p], sketch[p]) >= 24) different++;
                    AddCheck(different >= 256, "Color and sketch produce distinct rendered scenery at " + names[i],
                        different + " of " + color.Length + " sampled scene pixels differ by at least 24 RGB levels in total; this is not style approval.");
                    foreach (float weight in blendWeights)
                    {
                        art.SetDisplay(VillageArtMode.Blend, weight);
                        var blend = CaptureArtFrame("blend-" + names[i] + "-" + Mathf.RoundToInt(weight * 100).ToString("D3") + ".png", true);
                        CheckBlendPixels(color, sketch, blend, weight, names[i]);
                    }
                    art.SetDisplay(VillageArtMode.Region);
                    var region = CaptureArtFrame("region-" + names[i] + ".png", true);
                    CheckRegionPixels(color, sketch, region, names[i]);
                    AddCheck(snapshot.Unchanged(out string evidence), "Endpoint, blend and region frames share one unchanged scene at " + names[i], evidence);
                    var probe = new Vector2(art.BoundaryPixels, world.CameraY + 270);
                    float sampled = art.SampleWeight(probe);
                    art.SetDisplay(VillageArtMode.Color);
                    AddCheck(art.SampleWeight(probe) == 0, "Color endpoint samples zero at " + names[i], probe.ToString());
                    art.SetDisplay(VillageArtMode.Sketch);
                    AddCheck(art.SampleWeight(probe) == 1, "Sketch endpoint samples one at " + names[i], probe.ToString());
                    art.SetDisplay(VillageArtMode.Region);
                    AddCheck(sampled > 0 && sampled < 1 && art.SampleWeight(probe + Vector2.left * 960) == 0 && art.SampleWeight(probe + Vector2.right * 960) == 1,
                        "Region has color-left, sketch-right and an intermediate band at " + names[i], "Fixed world probe=" + probe + "; band weight=" + sampled);
                }
                art.SetDisplay(VillageArtMode.Color);
                var before = world.View.transform.position;
                CaptureArtFrame("cloud-motion-00s.png");
                presentation.AdvanceVisualTime(5f);
                CaptureArtFrame("cloud-motion-05s.png");
                AddCheck(world.View.transform.position == before, "Cloud motion holds camera fixed", "Only visual time advanced.");
                report.limitations.Add("All mode captures and warped landmarks are visual inspection evidence only. Pixel differences do not approve warm-paper linework or prove traversal.");
            }
            finally
            {
                art.SetBoundary(savedBoundary); art.SetDisplay(savedMode, savedWeight);
                presentation.animateEnvironment = animate; RefreshPresentation();
            }
        }

        static int PixelDifference(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        static void CheckBlendPixels(Color32[] color, Color32[] sketch, Color32[] blend, float weight, string label)
        {
            int mismatched = 0, between = 0;
            for (int i = 0; i < color.Length; i++)
            {
                if (weight == 0 || weight == 1)
                { if (PixelDifference(blend[i], weight == 0 ? color[i] : sketch[i]) != 0) mismatched++; }
                else
                {
                    Color32 a = color[i], b = sketch[i], c = blend[i];
                    if (c.r < Mathf.Min(a.r, b.r) - 1 || c.r > Mathf.Max(a.r, b.r) + 1 ||
                        c.g < Mathf.Min(a.g, b.g) - 1 || c.g > Mathf.Max(a.g, b.g) + 1 ||
                        c.b < Mathf.Min(a.b, b.b) - 1 || c.b > Mathf.Max(a.b, b.b) + 1) mismatched++;
                    if (PixelDifference(a, b) >= 24 && PixelDifference(c, a) > 3 && PixelDifference(c, b) > 3) between++;
                }
            }
            AddCheck(mismatched == 0 && (weight == 0 || weight == 1 || between >= 256) && art.SampleWeight(Vector2.zero) == weight,
                "Rendered blend " + weight + " preserves endpoint correspondence at " + label,
                "Endpoint/out-of-range pixels=" + mismatched + "; genuinely intermediate pixels=" + between + "; camera and simulation frozen.");
        }

        static void CheckRegionPixels(Color32[] color, Color32[] sketch, Color32[] region, string label)
        {
            int left = 0, right = 0, mismatched = 0;
            const int columns = 1920 / PixelSampleStep;
            float cameraLeft = world.View.transform.position.x * 32 - 480;
            for (int i = 0; i < color.Length; i++)
            {
                if (PixelDifference(color[i], sketch[i]) < 24) continue;
                float x = cameraLeft + (i % columns) * PixelSampleStep * .5f;
                if (x < art.BoundaryPixels - 240)
                { left++; if (PixelDifference(region[i], color[i]) != 0) mismatched++; }
                else if (x > art.BoundaryPixels + 240)
                { right++; if (PixelDifference(region[i], sketch[i]) != 0) mismatched++; }
            }
            AddCheck(left > 0 && right > 0 && mismatched == 0, "Rendered world region matches both registered endpoints at " + label,
                "Color-left samples=" + left + "; sketch-right samples=" + right + "; mismatches outside transition band=" + mismatched);
        }


        static void CheckSharedMap()
        {
            var map = world.FullMap;
            AddCheck(map && map.MapTexture && map.MapTexture.width == world.Data.cols && map.MapTexture.height == world.Data.rows,
                "Shared minimap uses the village terrain", "Reuses Chapter1FullMap with the authored village occupancy grid.");
            if (!map) return;
            map.SetOpen(false);
            world.Warp(0); map.RefreshMinimap();
            Vector2 before = map.MinimapViewport.Center, fullCenter = map.Viewport.Center;
            var texture = map.MapTexture;
            world.Warp(12); map.RefreshMinimap();
            var size = new Vector2(map.MinimapBounds.width - 12, 118);
            var feet = new Vector2(arena.Player.Position.x * 32, -arena.Player.Position.y * 32);
            AddCheck(Vector2.Distance(before, map.MinimapViewport.Center) > 100 &&
                Vector2.Distance(map.MinimapViewport.ToScreen(feet, size), size / 2) < 1 &&
                map.Viewport.Center == fullCenter && map.MapTexture == texture && !arena.Controls.Current.SimulationPaused,
                "Minimap follows the player without pausing combat or changing full-map state", "Village area 1 to 13; same cached texture.");
            using (var other = arena.Controls.OpenScope("village map verification independent owner"))
            {
                other.PauseSimulation();
                map.SetOpen(true);
                AddCheck(map.IsOpen && arena.Controls.Current.SimulationPaused && arena.Controls.Current.Blocks(InputChannels.Combat),
                    "Full map owns its pause and combat input block", "Existing common control scope reused.");
                map.SetOpen(false);
                AddCheck(!map.IsOpen && arena.Controls.Current.SimulationPaused && !arena.Controls.Current.Blocks(InputChannels.Combat),
                    "Closing the map preserves another owner's pause", "Independent pause remains until its own scope is disposed.");
            }
            map.SetOpen(true); map.enabled = false;
            AddCheck(!map.IsOpen && !arena.Controls.Current.SimulationPaused && !arena.Controls.Current.Blocks(InputChannels.Combat),
                "Disabling the village map releases its own controls", "No map input/pause lease remains after disable.");
            map.enabled = true;
            world.Warp(0);
        }

        static void CaptureMotionComparisons()
        {
            var presentation = world.GetComponent<VillagePresentation>();
            if (!presentation || !presentation.Ready || !art) return;
            bool wasAnimated = presentation.animateEnvironment;
            VillageArtMode savedMode = art.Mode;
            float savedWeight = art.Weight, savedBoundary = art.BoundaryPixels;
            presentation.animateEnvironment = false;
            float minimumCoverage = float.PositiveInfinity;
            try
            {
                // Warps establish isolated visual cases only. Every subsequent frame uses
                // the real motor and unsnapped gameplay camera, never transform animation.
                foreach (var mode in motionModes)
                {
                    string modeName = mode == VillageArtMode.Blend ? "transition" : ModeName(mode);
                    foreach (int sample in new[] { 0, 5, 9 })
                    {
                        arena.Player.ResetPlayer(); world.Warp(sample); Advance(24); RefreshPresentation();
                        if (mode == VillageArtMode.Region) art.SetBoundary(world.CameraX + 480);
                        art.SetDisplay(mode, 0);
                        var fixedProbe = new Vector2(art.BoundaryPixels, world.CameraY + 270);
                        float probeWeight = art.SampleWeight(fixedProbe);
                        float initialCameraY = world.CameraY, cameraRise = 0;
                        for (int tick = 0; tick <= 180; tick++)
                        {
                            if (tick > 0)
                            {
                                var buttons = tick == 1 || tick == 37 ? CombatButtons.Jump : CombatButtons.None;
                                simulation.Step(Dt, new CombatInput(pressed: buttons));
                                world.RefreshCamera(Dt);
                            }
                            if (mode == VillageArtMode.Blend) art.SetDisplay(mode, tick / 180f);
                            RefreshPresentation(false);
                            minimumCoverage = Mathf.Min(minimumCoverage, presentation.FarRenderedBottomScreenPixels);
                            cameraRise = Mathf.Max(cameraRise, initialCameraY - world.CameraY);
                            if (tick == 0 || tick == 24 || tick == 48 || tick == 72 || tick == 96 || tick == 144 || tick == 180)
                                CaptureMotionFrame(presentation, sample, tick, "jump-zone-" + (sample + 1).ToString("D2") + "-" + modeName + "-" + tick.ToString("D3") + ".png");
                        }
                        AddCheck(cameraRise > 12, "Airborne visual regression moves the real camera in zone " + (sample + 1) + " (" + modeName + ")",
                            "Actual unsnapped camera rise=" + cameraRise.ToString("F2") + "px; jump inputs at ticks1/37.");
                        if (mode == VillageArtMode.Region)
                            AddCheck(art.SampleWeight(fixedProbe) == probeWeight, "Fixed region location survives jumping camera motion in zone " + (sample + 1),
                                "World probe=" + fixedProbe + "; before=" + probeWeight + "; after=" + art.SampleWeight(fixedProbe));
                        if (mode == VillageArtMode.Authored) CheckAuthoredCoordinates("jumping camera in area " + (sample + 1));
                    }

                    arena.Player.ResetPlayer(); world.Warp(11); Advance(24); RefreshPresentation();
                    if (mode == VillageArtMode.Region) art.SetBoundary(world.CameraX + 480);
                    art.SetDisplay(mode, 0);
                    var walkingProbe = new Vector2(art.BoundaryPixels, world.CameraY + 270);
                    float walkingWeight = art.SampleWeight(walkingProbe);
                    var front = presentation.UndergroundFrontSample;
                    Vector3 frontBefore = front ? front.position : Vector3.zero;
                    Vector2 rearBefore = presentation.UndergroundParallaxOffsetPixels;
                    float cameraBefore = world.CameraX;
                    float middleBefore = presentation.MidgroundSampleRenderer.transform.position.x * 32 - cameraBefore;
                    float distantBefore = presentation.FarSampleRenderer.transform.position.x * 32 - cameraBefore;
                    for (int tick = 0; tick <= 96; tick++)
                    {
                        if (tick > 0)
                        {
                            simulation.Step(Dt, new CombatInput(1));
                            world.RefreshCamera(Dt);
                        }
                        if (mode == VillageArtMode.Blend) art.SetDisplay(mode, tick / 96f);
                        RefreshPresentation(false);
                        if (tick % 24 == 0)
                            CaptureMotionFrame(presentation, 11, tick, "underground-walk-" + modeName + "-" + tick.ToString("D3") + ".png");
                    }
                    Vector2 rearDelta = presentation.UndergroundParallaxOffsetPixels - rearBefore;
                    float cameraDelta = world.CameraX - cameraBefore;
                    float middleDelta = presentation.MidgroundSampleRenderer.transform.position.x * 32 - world.CameraX - middleBefore;
                    float distantDelta = presentation.FarSampleRenderer.transform.position.x * 32 - world.CameraX - distantBefore;
                    AddCheck(Mathf.Abs(middleDelta) > Mathf.Abs(distantDelta) + 1 && Mathf.Abs(middleDelta) < cameraDelta,
                        "Middle scenery moves between distant scenery and fixed terrain (" + modeName + ")", "Camera=" + cameraDelta + "px; middle=" + middleDelta + "px; distant=" + distantDelta + "px.");
                    AddCheck(front && presentation.UndergroundFrontLayerCount > 0 && presentation.UndergroundRearLayerCount > 0,
                        "Underground front arches and rear wall are separate layers (" + modeName + ")", "Front=" + presentation.UndergroundFrontLayerCount + "; rear=" + presentation.UndergroundRearLayerCount);
                    AddCheck(front && front.position == frontBefore && cameraDelta > 16 && rearDelta.x > 1 && rearDelta.x < cameraDelta * .5f,
                        "Walking keeps torch columns world fixed while the rear wall shifts more slowly (" + modeName + ")", "Camera delta=" + cameraDelta.ToString("F2") + "px; rear offset delta=" + rearDelta + "; front world delta=" + (front ? (front.position - frontBefore).ToString() : "missing"));
                    if (mode == VillageArtMode.Region)
                        AddCheck(art.SampleWeight(walkingProbe) == walkingWeight, "Fixed region location survives continuous walking camera motion",
                            "World probe=" + walkingProbe + "; before=" + walkingWeight + "; after=" + art.SampleWeight(walkingProbe) + "; camera displacement=" + cameraDelta);
                    if (mode == VillageArtMode.Authored) CheckAuthoredCoordinates("continuous underground walking camera");
                }
                AddCheck(minimumCoverage >= 540, "Far village continues below every airborne gameplay viewport", "Minimum bottom=" + minimumCoverage.ToString("F2") + "px; viewport=540px; native=" + presentation.FarNativeSize);
                AddCheck(presentation.MidgroundLayerCount > 0 && presentation.MidgroundSampleRenderer &&
                    presentation.MidgroundSampleRenderer.sortingOrder > presentation.FarSampleRenderer.sortingOrder,
                    "Separate middle village sits in front of the distant skyline", "Middle native size=" + presentation.MidgroundNativeSize);

                // Fixed air/contact positions are visual inspection, not route evidence.
                var entrances = new[] { new Vector2(30, -90), new Vector2(172, -88), new Vector2(273, -87),
                    new Vector2(330, -98), new Vector2(419, -94), new Vector2(467, -94) };
                for (int index = 0; index < entrances.Length; index++)
                {
                    Place(entrances[index]); RefreshPresentation();
                    art.SetBoundary(world.CameraX + 480);
                    foreach (var mode in artModes)
                    {
                        art.SetDisplay(mode);
                        arena.Presentation.Draw(Dt);
                        CaptureArtFrame("shaft-entrance-" + (index + 1).ToString("D2") + "-" + ModeName(mode) + ".png");
                    }
                }
                var reviewFeet = new[] { new Vector2(328, -88), new Vector2(330, -98), new Vector2(172, -88),
                    new Vector2(124.25f, -48), new Vector2(216, -70), new Vector2(401.25f, -80) };
                for (int index = 0; index < reviewFeet.Length; index++)
                {
                    Place(reviewFeet[index] + Vector2.up * .02f); Advance(32); RefreshPresentation();
                    arena.Presentation.Draw(Dt); arena.RangedPresentation?.Tick();
                    art.SetBoundary(world.CameraX + 480);
                    foreach (var mode in artModes)
                    {
                        art.SetDisplay(mode);
                        CaptureArtFrame("contact-" + (index + 1).ToString("D2") + "-" + ModeName(mode) + ".png");
                    }
                }
                foreach (var mode in motionModes)
                {
                    Place(new Vector2(330, -97.98f)); Advance(24); RefreshPresentation();
                    if (mode == VillageArtMode.Region) art.SetBoundary(world.CameraX + 480);
                    art.SetDisplay(mode, 0);
                    float startY = arena.Player.Position.y, highestY = startY;
                    for (int tick = 0; tick <= 150; tick++)
                    {
                        if (tick > 0)
                        {
                            simulation.Step(Dt, new CombatInput(pressed: tick == 1 || tick == 37 ? CombatButtons.Jump : CombatButtons.None));
                            world.RefreshCamera(Dt);
                        }
                        if (mode == VillageArtMode.Blend) art.SetDisplay(mode, tick / 150f);
                        highestY = Mathf.Max(highestY, arena.Player.Position.y); RefreshPresentation(false);
                        if (tick % 30 == 0) CaptureMotionFrame(presentation, 16, tick, "entrance-jump-" + ModeName(mode) + "-" + tick.ToString("D3") + ".png");
                    }
                    AddCheck(highestY - startY > 4, "Actual double jump crosses the market cave entrance in mode " + mode,
                        "Feet rise=" + (highestY - startY) + " world units; actual motor and continuous camera follow.");
                }
                report.limitations.Add("Shaft entrance stills place the actor in known open air to inspect background masks; they do not prove a route or landing.");
            }
            finally
            {
                art.SetBoundary(savedBoundary); art.SetDisplay(savedMode, savedWeight);
                presentation.animateEnvironment = wasAnimated;
                RefreshPresentation();
            }
        }

        static void CheckArtworkContacts()
        {
            var pixels=new Dictionary<Texture2D,Color32[]>();
            var deckVariants=new HashSet<Texture2D>();
            int deckSamples=0,hedgeSamples=0;float maxDeckGap=0,maxHedgeGap=float.NegativeInfinity,minHedgeGap=float.PositiveInfinity;
            foreach(var r in world.GetComponentsInChildren<SpriteRenderer>())
            {
                var sprite=r.sprite;if(!sprite)continue;
                string asset=Path.GetFileName(sprite.name);
                bool deck=asset.StartsWith("platform_c0")&&!asset.StartsWith("platform_c01")&&!asset.StartsWith("platform_c05");
                bool hedge=asset=="hedge";
                if(!deck&&!hedge)continue;
                if(deck&&!deckVariants.Add(sprite.texture))continue;
                if(!pixels.TryGetValue(sprite.texture,out var rgba)){rgba=sprite.texture.GetPixels32();pixels.Add(sprite.texture,rgba);}
                int w=(int)sprite.rect.width,h=(int)sprite.rect.height,baseX=(int)sprite.rect.x,baseY=(int)sprite.rect.y;
                var edges=new List<float>();
                int from=deck?w/2:w/4,to=deck?from+1:w*3/4;
                for(int x=from;x<to;x++)
                {
                    int y=deck?h-1:0,step=deck?-1:1;
                    for(;y>=0&&y<h;y+=step)if(rgba[(baseY+y)*sprite.texture.width+baseX+x].a>=128)break;
                    if(y<0||y>=h)continue;
                    float localY=(y+(deck?1:0)-sprite.pivot.y)/sprite.pixelsPerUnit;
                    edges.Add(r.transform.TransformPoint(new Vector3((x+.5f-sprite.pivot.x)/sprite.pixelsPerUnit,localY,0)).y);
                }
                if(edges.Count==0)continue;edges.Sort();float edge=edges[edges.Count/2],center=r.bounds.center.x;
                var surfaces=world.TerrainSurfaces.Where(s=>s.oneWay==deck).Select(s=>s.GetComponent<Collider2D>().bounds)
                    .Where(b=>b.min.x<=center&&b.max.x>=center).OrderBy(b=>Mathf.Abs(b.max.y-edge)).ToArray();
                if(surfaces.Length==0)continue;
                float gap=(edge-surfaces[0].max.y)*32;
                if(deck){deckSamples++;maxDeckGap=Mathf.Max(maxDeckGap,Mathf.Abs(gap));}
                else{hedgeSamples++;maxHedgeGap=Mathf.Max(maxHedgeGap,gap);minHedgeGap=Mathf.Min(minHedgeGap,gap);}
            }
            AddCheck(deckSamples==3&&maxDeckGap<=1,"Color deck artwork meets actual collision tops",
                "Three middle sprite variants sampled through alpha and world transforms; largest gap="+maxDeckGap+" native px.");
            AddCheck(hedgeSamples>0&&maxHedgeGap<=0&&minHedgeGap>=-5,"Hedge bases meet terrain without floating",
                "Median opaque lower edge across middle half; samples="+hedgeSamples+"; gap range="+minHedgeGap+".."+maxHedgeGap+" native px.");
        }

        static void CaptureMotionFrame(VillagePresentation presentation, int sample, int tick, string filename)
        {
            arena.Presentation.Draw(Dt);
            arena.RangedPresentation?.Tick();
            CaptureArtFrame(filename);
            report.motionFrames.Add(new MotionFrame
            {
                file = filename, zone = sample + 1, tick = tick,
                playerX = arena.Player.Position.x, playerY = arena.Player.Position.y,
                cameraX = world.CameraX, cameraY = world.CameraY,
                farBottomScreenPixels = presentation.FarRenderedBottomScreenPixels,
                mode = art.Mode.ToString(), weight = art.Weight, boundaryPixels = art.BoundaryPixels
            });
        }







        static Color32[] Capture(Camera camera, string filename, int width, int height, bool samplePixels = false)
        {
            var oldTarget = camera.targetTexture;
            var oldRect = camera.rect;
            float oldAspect = camera.aspect;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                target.filterMode = FilterMode.Point;
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
                if (!samplePixels) return null;
                var source = image.GetRawTextureData<Color32>();
                int columns = (width + PixelSampleStep - 1) / PixelSampleStep;
                int rows = (height + PixelSampleStep - 1) / PixelSampleStep;
                var sampled = new Color32[columns * rows];
                int index = 0;
                for (int y = 0; y < height; y += PixelSampleStep)
                    for (int x = 0; x < width; x += PixelSampleStep) sampled[index++] = source[y * width + x];
                return sampled;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.rect = oldRect;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                if (image) Object.DestroyImmediate(image);
            }
        }

        static void CaptureOverview()
        {
            var camera = world.View;
            var position = camera.transform.position;
            float size = camera.orthographicSize;
            float aspect = camera.aspect;
            var presentation = world.GetComponents<MonoBehaviour>().FirstOrDefault(component => component && component.GetType().Name == "VillagePresentation");
            var setOverview = presentation ? presentation.GetType().GetMethod("SetOverview", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null) : null;
            try
            {
                setOverview?.Invoke(presentation, new object[] { true });
                int width = 3840, height = Mathf.RoundToInt(width * world.Data.height / (float)world.Data.width);
                camera.transform.position = new Vector3(world.Data.width / 64f, -world.Data.height / 64f, -10);
                camera.orthographicSize = world.Data.height / 64f;
                camera.aspect = width / (float)height;
                Capture(camera, "full-authored-world.png", width, height);
                report.limitations.Add("Overview renders actual terrain and prop placement with the presentation's full-world backdrop. Only native input replay is traversal evidence.");
            }
            finally
            {
                camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
                setOverview?.Invoke(presentation, new object[] { false });
            }
        }

        static void AddCheck(bool passed, string name, string evidence)
        {
            report.checks.Add(new Check { pass = passed, name = name, evidence = evidence });
        }

        static void Save()
        {
            if (report == null) return;
            report.errors = new List<string>(errors);
            File.WriteAllText(Path.Combine(Output, "native-verification.json"), JsonUtility.ToJson(report, true));
        }

        static void Finish(string failure)
        {
            SessionState.SetBool(Pending, false);
            if (report == null) report = new Report { unityVersion = Application.unityVersion, scene = Stage1VillageBuilder.ScenePath, simulationStep = Dt };
            if (!string.IsNullOrEmpty(failure)) errors.Add(failure);
            simulation?.Dispose(); simulation = null;
            report.fullRouteVerified = report.routes.Count == routeZones.Length && report.routes.All(route => route.passed);
            report.exitVerified = report.routes.Any(route => route.zone == 25 && route.passed);
            report.pass = errors.Count == 0 && report.checks.Count > 0 && report.checks.All(check => check.pass) && report.captures == report.areas;
            if (!report.fullRouteVerified) report.limitations.Add("Selected complete routes have not all passed native input replay. The independent local motor/anchor checks do not prove complete map traversal.");
            Save();
            ReleaseEditorPolicy();
            Debug.Log("STAGE1_VILLAGE_VERIFICATION local=" + report.pass + " selectedRoutes=" + report.fullRouteVerified + " " + Output);
            if (Application.isBatchMode) EditorApplication.Exit(report.pass && report.fullRouteVerified && report.exitVerified ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
