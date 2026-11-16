using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stage1VillageExperiment
{
    /// <summary>
    /// Opt-in standalone smoke run. Uses the shared simulation input and captures the
    /// displayed screen, including the native pixel presenter and ordinary IMGUI HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class VillageBuildSmoke : MonoBehaviour
    {
        const string Argument = "--village-smoke-output";
        const float Dt = 1f / 120;
        const double StartupTimeout = 60, CaptureTimeout = 20;
        static readonly VillageArtMode[] MotionModes = { VillageArtMode.Authored, VillageArtMode.Color, VillageArtMode.Sketch, VillageArtMode.Region, VillageArtMode.Blend };
        static readonly int[] JumpCaptureTicks = { 0, 1, 13, 25, 37, 49, 73, 97, 121, 146 };
        const int ArtFrameCount = 8, WalkFrameCount = 8;
        const float AuthoredSketchUntil = 5056, AuthoredNormalFrom = 5760, AuthoredEdgeCameraX = 5408;
        static readonly float[] AuthoredEdgeCameras = { AuthoredEdgeCameraX, AuthoredEdgeCameraX - 128, AuthoredEdgeCameraX + 128 };
        public Stage1VillageWorld world;

        enum Phase { Waiting, Settling, Capturing, Complete }
        enum CaptureKind { InitialAuthored, AuthoredArea, AuthoredBoundary, AuthoredSpawnComparison, Art, Walk, Jump, Final, FullMap, AuthoredComparison, NormalArea, SketchArea }
        CaptureKind captureKind;
        Phase phase;
        VillagePresentation presentation;
        VillageArtTransition art;
        VillagePixelViewport viewport;
        CombatArena arena;
        CombatArena.ManualSimulation simulation;
        readonly List<string> errors = new List<string>();
        readonly List<TestEnemy> enemies = new List<TestEnemy>();
        readonly List<bool> enemyAggression = new List<bool>();
        readonly Report report = new Report();
        string outputFolder, screenshotPath, activeCapturePath, runId;
        double startedAt, captureStartedAt, earliestCaptureAt;
        int settleFrames;
        bool smokeRequested, previousTestInput, previousRunInBackground, ownsInput, subscribed, previousAnimation;
        VillageArtMode previousArtMode;
        float previousArtWeight, previousBoundary, jumpHighest, sequenceProbeWeight;
        Vector2 sequenceStart, jumpStart, sequenceProbe, sequenceCameraStart;
        int motionSequence, motionFrame, jumpTick;
        int areaIndex, boundaryIndex;
        Vector2[] authoredProbes;
        float[] authoredProbeWeights;
        Color32[] authoredSpawnSamples, authoredAreaSamples;
        readonly List<Rect> actorPixelExclusions = new List<Rect>();
        CaptureFrame pendingFrame;
        Transform[] fixedTransforms;
        Matrix4x4[] fixedMatrices;
        Vector2 fixedFeet, fixedVelocity, fixedCamera;
        float fixedCameraSize, fixedCameraAspect;
        long fixedTicks;
        bool fixedGrounded;
        Rect playerPixelExclusion;
        Color32[] colorSamples, sketchSamples;
        int sourceBeginRender, sourceEndRender, outputBeginRender, outputEndRender, builtInSourcePreRender, builtInOutputPreRender, guiRepaints;
        int builtInSourcePreCull, builtInOutputPreCull, builtInSourcePostRender, builtInOutputPostRender;

        [Serializable]
        sealed class CameraEvidence
        {
            public string name, target, components;
            public bool enabled, active, targetCreated;
            public int cullingMask, targetDisplay;
            public float depth;
            public string clearFlags;
            public Color clearColor;
            public Rect viewportRect;
        }

        [Serializable]
        sealed class RenderEvidence
        {
            public string label, pipeline, pipelineInstance, qualityPipeline, qualityName, graphicsApi, graphicsDevice;
            public bool focused, runInBackground, batchMode;
            public int frameCount, renderedFrameCount, guiRepaints, cameraCount, displayCount;
            public int sourceBeginRender, sourceEndRender, outputBeginRender, outputEndRender, builtInSourcePreRender, builtInOutputPreRender;
            public int builtInSourcePreCull, builtInOutputPreCull, builtInSourcePostRender, builtInOutputPostRender;
            public string screenMode;
            public CameraEvidence source, output;
        }

        [Serializable]
        sealed class CaptureFrame
        {
            public string file, kind, mode;
            public int index, sequence, tick;
            public float simulationSeconds, weight, boundaryPixels, authoredSketchUntilPixels, authoredNormalFromPixels;
            public double captureSeconds;
            public Vector2 feet, cameraPixels;
            public bool grounded;
            public PixelEvidence pixels;
            public int authoredEndpointSamples, authoredEndpointMismatches;
        }

        [Serializable]
        sealed class PixelEvidence
        {
            public bool valid;
            public int samples, uniqueQuantizedColors;
            public float minimumLuminance, maximumLuminance, meanLuminance, nonBlackFraction, brightFraction;
            public string failure;
        }

        [Serializable]
        sealed class NativeDiagnostic
        {
            public string file, error;
            public PixelEvidence pixels;
        }

        [Serializable]
        sealed class AuthoredCheck
        {
            public string label;
            public bool pass;
            public int probes, incorrect, changed;
            public float sketchUntilPixels, normalFromPixels;
            public Vector2 cameraPixels;
        }

        [Serializable]
        sealed class Report
        {
            public string version, unityVersion, scene, utcTimestamp;
            public bool success, viewport960540, pointFiltered, hudEnabled, screenshotComplete;
            public bool minimapEnabled, fullMapOpened, fullMapClosed;
            public string fullMapScreenshotFile;
            public int screenWidth, screenHeight, nativeWidth, nativeHeight;
            public Vector2 walkStart, walkEnd;
            public float walkDisplacement, jumpRise;
            public bool walkGrounded, jumpLanded, basicAttackAccepted, simulationFaulted;
            public string basicAttackDefinition, screenshotFile;
            public long simulationTicks;
            public Vector2 walkCameraDeltaPixels;
            public List<CaptureFrame> walkFrames = new List<CaptureFrame>();
            public List<CaptureFrame> motionFrames = new List<CaptureFrame>();
            public List<CaptureFrame> artFrames = new List<CaptureFrame>();
            public CaptureFrame initialAuthoredFrame;
            public List<CaptureFrame> authoredFrames = new List<CaptureFrame>();
            public List<CaptureFrame> authoredBoundaryFrames = new List<CaptureFrame>();
            public List<CaptureFrame> normalFrames = new List<CaptureFrame>();
            public List<CaptureFrame> authoredEndpointFrames = new List<CaptureFrame>();
            public List<AuthoredCheck> authoredChecks = new List<AuthoredCheck>();
            public bool defaultAuthored, authoredWorldStable = true, authoredRenderedEndpoints = true;
            public bool fixedSceneUnchanged = true, worldRegionStable = true, renderedEndpointsDistinct;
            public string artScope = "Initial startup, all 24 authored areas and actual 5408px boundary cameras are captured before review modes. Area warps are inspection only; walk/jump sequences use the real shared motor. Normal-only camera PNGs support independent original-baseline comparison excluding animated characters. Pixel equality does not approve the adopted sketch style.";
            public PixelEvidence displayedPixels;
            public List<NativeDiagnostic> nativeDiagnostics = new List<NativeDiagnostic>();
            public List<RenderEvidence> rendering = new List<RenderEvidence>();
            public string[] errors;
        }

        void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            string requested = null;
            bool selected = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == Argument)
                {
                    selected = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) requested = args[i + 1];
                    break;
                }
                if (args[i].StartsWith(Argument + "=", StringComparison.Ordinal))
                { selected = true; requested = args[i].Substring(Argument.Length + 1); break; }
            }
            if (!selected) { enabled = false; return; }
            smokeRequested = true;
            startedAt = Time.realtimeSinceStartupAsDouble;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            RenderPipelineManager.beginCameraRendering += OnCameraBegin;
            RenderPipelineManager.endCameraRendering += OnCameraEnd;
            Camera.onPreRender += OnBuiltInPreRender;
            Camera.onPreCull += OnBuiltInPreCull;
            Camera.onPostRender += OnBuiltInPostRender;
            subscribed = true;
            report.version = Application.version;
            report.unityVersion = Application.unityVersion;
            report.utcTimestamp = DateTime.UtcNow.ToString("O");
            try
            {
                if (string.IsNullOrWhiteSpace(requested)) throw new ArgumentException(Argument + " requires an output folder.");
                outputFolder = Path.GetFullPath(requested);
                Directory.CreateDirectory(outputFolder);
                runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                report.screenshotFile = "standalone_1920x1080_" + runId + ".png";
                screenshotPath = Path.Combine(outputFolder, report.screenshotFile);
                if (Application.isBatchMode) throw new InvalidOperationException("Screen smoke requires a graphics-enabled, non-batch standalone player so the native presenter and HUD are rendered.");
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        void Update()
        {
            if (phase == Phase.Complete) return;
            try
            {
                if (phase == Phase.Waiting)
                {
                    if (!world) world = GetComponent<Stage1VillageWorld>();
                    if (!world) world = FindAnyObjectByType<Stage1VillageWorld>();
                    if (world && !presentation) presentation = world.GetComponent<VillagePresentation>();
                    if (world && !viewport) viewport = world.GetComponent<VillagePixelViewport>();
                    if (world && !art) art = world.GetComponent<VillageArtTransition>();
                    bool ready = world && world.Ready && presentation && presentation.Ready && viewport && viewport.Ready && art;
                    if (ready && Screen.width == 1920 && Screen.height == 1080)
                    {
                        PrepareSimulation();
                        QueueInitialAuthoredFrame();
                    }
                    else if (Time.realtimeSinceStartupAsDouble - startedAt > StartupTimeout)
                        Fail("Startup timed out: world=" + (world && world.Ready) + ", presentation=" + (presentation && presentation.Ready) + ", art=" + (bool)art + ", pixel viewport=" + (viewport && viewport.Ready) + ", screen=" + Screen.width + "x" + Screen.height);
                }
                else if (phase == Phase.Settling && --settleFrames <= 0 && Time.realtimeSinceStartupAsDouble >= earliestCaptureAt)
                {
                    phase = Phase.Capturing;
                    captureStartedAt = Time.realtimeSinceStartupAsDouble;
                    StartCoroutine(CaptureDisplayedFrame());
                }
                else if (phase == Phase.Capturing)
                {
                    if (ScreenshotWritten(out int width, out int height))
                    {
                        report.screenWidth = width; report.screenHeight = height;
                        bool complete = width == 1920 && height == 1080;
                        if (!complete) errors.Add("Screenshot dimensions were " + width + "x" + height + "; expected 1920x1080.");
                        PixelEvidence pixels = ReadScreenshotPixels(activeCapturePath, out Color32[] samples);
                        if (!pixels.valid) errors.Add("Displayed screen is blank or insufficiently rendered (" + Path.GetFileName(activeCapturePath) + "): " + pixels.failure);
                        if (captureKind == CaptureKind.InitialAuthored)
                        {
                            pendingFrame.pixels = pixels;
                            report.initialAuthoredFrame = pendingFrame;
                            areaIndex = 0;
                            QueueAuthoredArea();
                        }
                        else if (captureKind == CaptureKind.AuthoredArea)
                        {
                            pendingFrame.pixels = pixels;
                            report.authoredFrames.Add(pendingFrame);
                            if (++areaIndex < world.Data.views.Length) QueueAuthoredArea();
                            else { boundaryIndex = 0; QueueAuthoredBoundary(); }
                        }
                        else if (captureKind == CaptureKind.AuthoredBoundary)
                        {
                            pendingFrame.pixels = pixels;
                            report.authoredBoundaryFrames.Add(pendingFrame);
                            if (++boundaryIndex < AuthoredEdgeCameras.Length) QueueAuthoredBoundary();
                            else QueueAuthoredSpawnComparison();
                        }
                        else if (captureKind == CaptureKind.AuthoredSpawnComparison)
                        {
                            pendingFrame.pixels = pixels;
                            authoredSpawnSamples = samples;
                            QueueArtFrame();
                        }
                        else if (captureKind == CaptureKind.AuthoredComparison)
                        {
                            pendingFrame.pixels = pixels;
                            authoredAreaSamples = samples;
                            QueueAreaEndpoint(VillageArtMode.Color);
                        }
                        else if (captureKind == CaptureKind.NormalArea)
                        {
                            pendingFrame.pixels = pixels;
                            report.normalFrames.Add(pendingFrame);
                            CheckAuthoredEndpointPixels(authoredAreaSamples, samples, false, pendingFrame.file);
                            if (areaIndex < 8) QueueAreaEndpoint(VillageArtMode.Sketch);
                            else if (++areaIndex < world.Data.views.Length) QueueNormalAreaComparison();
                            else Finish();
                        }
                        else if (captureKind == CaptureKind.SketchArea)
                        {
                            pendingFrame.pixels = pixels;
                            CheckAuthoredEndpointPixels(authoredAreaSamples, samples, true, pendingFrame.file);
                            if (++areaIndex < world.Data.views.Length) QueueNormalAreaComparison();
                            else Finish();
                        }
                        else if (captureKind == CaptureKind.Art)
                        {
                            pendingFrame.pixels = pixels;
                            CheckArtPixels(samples);
                            report.artFrames.Add(pendingFrame);
                            if (report.artFrames.Count < ArtFrameCount) QueueArtFrame();
                            else BeginMotionSequence();
                        }
                        else if (captureKind == CaptureKind.Walk)
                        {
                            pendingFrame.pixels = pixels;
                            if (motionSequence == 0) report.walkFrames.Add(pendingFrame);
                            report.motionFrames.Add(pendingFrame);
                            if (++motionFrame < WalkFrameCount) QueueWalkFrame();
                            else { CheckWalk(); motionFrame = 0; jumpTick = 0; jumpStart = arena.Player.Position; jumpHighest = jumpStart.y; QueueJumpFrame(); }
                        }
                        else if (captureKind == CaptureKind.Jump)
                        {
                            pendingFrame.pixels = pixels;
                            report.motionFrames.Add(pendingFrame);
                            if (++motionFrame < JumpCaptureTicks.Length) QueueJumpFrame();
                            else
                            {
                                CheckJump();
                                if (++motionSequence < MotionModes.Length) BeginMotionSequence();
                                else
                                {
                                    RunAttack();
                                    captureKind = CaptureKind.Final;
                                    QueueCapture(screenshotPath, 12);
                                }
                            }
                        }
                        else if (captureKind == CaptureKind.FullMap)
                        {
                            world.FullMap.SetOpen(false);
                            report.fullMapClosed = !world.FullMap.IsOpen && !arena.Controls.Current.SimulationPaused;
                            if (!report.fullMapClosed) errors.Add("Full map left its pause active after closing.");
                            areaIndex = 0;
                            QueueNormalAreaComparison();
                        }
                        else
                        {
                            report.screenshotComplete = complete; report.displayedPixels = pixels;
                            var map = world.FullMap;
                            report.minimapEnabled = map && map.enabled && map.MapTexture;
                            if (!report.minimapEnabled) { Fail("Shared minimap is missing from the village player."); return; }
                            map.SetOpen(true);
                            report.fullMapOpened = map.IsOpen && arena.Controls.Current.SimulationPaused;
                            if (!report.fullMapOpened) { Fail("Shared full map did not open and pause combat."); return; }
                            captureKind = CaptureKind.FullMap;
                            report.fullMapScreenshotFile = "full_map_" + runId + ".png";
                            QueueCapture(Path.Combine(outputFolder, report.fullMapScreenshotFile), 3);
                        }
                    }
                    else if (Time.realtimeSinceStartupAsDouble - captureStartedAt > CaptureTimeout)
                        Fail("Displayed-screen PNG was not completely written within the capture timeout.");
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        void PrepareSimulation()
        {
            arena = world.arena;
            if (!arena || !arena.Player) throw new InvalidOperationException("Shared player runtime was not ready.");
            report.scene = world.gameObject.scene.name;
            previousAnimation = presentation.animateEnvironment;
            previousArtMode = art.Mode; previousArtWeight = art.Weight; previousBoundary = art.BoundaryPixels;
            presentation.animateEnvironment = false;
            previousTestInput = arena.Player.UseTestInput;
            ownsInput = true;
            arena.Player.UseTestInput = true;
            simulation = arena.AcquireManualSimulation();
            foreach (var enemy in arena.enemies)
            {
                if (!enemy) continue;
                enemies.Add(enemy); enemyAggression.Add(enemy.aggressive);
                enemy.aggressive = false;
                enemy.CancelAttack();
            }

            InitializeAuthoredEvidence();
            // Preserve the authored spawn and collision; no teleport or transform movement.
            Advance(24);
            report.walkStart = arena.Player.Position;
            world.RefreshCamera(0, true);
            arena.Presentation.Draw(0); arena.RangedPresentation?.Tick(); presentation.RefreshForCapture();
            RememberFixedScene();
        }

        void RememberFixedScene()
        {
            var nodes = new HashSet<Transform>(world.GetComponentsInChildren<Transform>(true));
            foreach (var node in arena.Player.GetComponentsInChildren<Transform>(true)) nodes.Add(node);
            nodes.Add(world.View.transform);
            fixedTransforms = new Transform[nodes.Count]; nodes.CopyTo(fixedTransforms);
            fixedMatrices = new Matrix4x4[fixedTransforms.Length];
            for (int i = 0; i < fixedTransforms.Length; i++) fixedMatrices[i] = fixedTransforms[i].localToWorldMatrix;
            fixedFeet = arena.Player.Position; fixedVelocity = arena.Player.velocity;
            fixedGrounded = arena.Player.grounded;
            fixedCamera = new Vector2(world.CameraX, world.CameraY); fixedTicks = arena.SimulationTickCount;
            fixedCameraSize = world.View.orthographicSize; fixedCameraAspect = world.View.aspect;
            Vector3 playerScreen = world.View.WorldToViewportPoint(arena.Player.Position);
            playerPixelExclusion = new Rect(playerScreen.x * 1920 - 144, playerScreen.y * 1080 - 96, 288, 352);
            RememberActorExclusions();
        }

        void InitializeAuthoredEvidence()
        {
            report.defaultAuthored = art.Mode == VillageArtMode.Authored;
            if (!report.defaultAuthored) errors.Add("Standalone did not start in default Authored mode: " + art.Mode);
            if (world.Data.views.Length != 24) errors.Add("Authored area evidence requires all 24 village landmarks.");
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
        }

        void CheckAuthoredCoordinates(string label)
        {
            var check = new AuthoredCheck { label = label, probes = authoredProbes.Length,
                sketchUntilPixels = art.AuthoredSketchUntilPixels, normalFromPixels = art.AuthoredNormalFromPixels,
                cameraPixels = new Vector2(world.CameraX, world.CameraY) };
            for (int i = 0; i < authoredProbes.Length; i++)
            {
                Vector2 probe = authoredProbes[i];
                float weight = art.SampleWeight(probe);
                bool expected = i < world.Data.views.Length ? weight == (i < 8 ? 1 : 0) :
                    probe.x <= AuthoredSketchUntil ? weight == 1 : probe.x >= AuthoredNormalFrom ? weight == 0 : weight > 0 && weight < 1;
                if (!expected) check.incorrect++;
                if (weight != authoredProbeWeights[i]) check.changed++;
            }
            check.pass = art.Mode == VillageArtMode.Authored && check.sketchUntilPixels == AuthoredSketchUntil && check.normalFromPixels == AuthoredNormalFrom &&
                check.incorrect == 0 && check.changed == 0;
            report.authoredChecks.Add(check);
            if (check.pass) return;
            report.authoredWorldStable = false;
            errors.Add("Authored world coordinates changed at " + label + ": mode=" + art.Mode + ", range=" + check.sketchUntilPixels + ".." + check.normalFromPixels +
                ", incorrect=" + check.incorrect + ", changed=" + check.changed);
        }

        void QueueInitialAuthoredFrame()
        {
            // Do not set a display mode: this frame must expose the delivered startup.
            CheckAuthoredCoordinates("initial displayed frame");
            QueueEvidenceFrame(CaptureKind.InitialAuthored, "initial_authored_" + runId + ".png", 0);
        }

        void PrepareArea(int index)
        {
            // Landmark warps are visual inspection only, never motor/route evidence.
            arena.Player.ResetPlayer(); world.Warp(index); Advance(18);
            world.RefreshCamera(0, true);
            arena.Presentation.Draw(0); arena.RangedPresentation?.Tick(); presentation.RefreshForCapture();
            RememberFixedScene();
        }

        void QueueAuthoredArea()
        {
            PrepareArea(areaIndex);
            CheckAuthoredCoordinates("default area " + (areaIndex + 1));
            QueueEvidenceFrame(CaptureKind.AuthoredArea, "zone_" + (areaIndex + 1).ToString("D2") + "_authored_" + runId + ".png", areaIndex);
        }

        void QueueAuthoredBoundary()
        {
            float center = AuthoredEdgeCameras[boundaryIndex];
            // Existing follow camera sits 48px ahead of the feet; use the real area edge.
            // Neither authored endpoint nor the review boundary is changed for this shot.
            arena.Player.Position = Stage1VillageWorld.PixelFeet(center - 48, world.Data.views[8].y);
            arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            arena.Player.grounded = arena.Player.ProbeGround();
            world.RefreshCamera(0, true);
            arena.Presentation.Draw(0); arena.RangedPresentation?.Tick(); presentation.RefreshForCapture();
            RememberFixedScene();
            CheckAuthoredCoordinates("actual area edge camera " + center);
            if (Mathf.Abs(world.View.transform.position.x * 32 - center) >= .01f)
                errors.Add("Authored area-edge camera did not reach world pixel " + center + ".");
            QueueEvidenceFrame(CaptureKind.AuthoredBoundary, "authored_boundary_" + Mathf.RoundToInt(center) + "_" + runId + ".png", boundaryIndex);
        }

        void QueueAuthoredSpawnComparison()
        {
            PrepareArea(0);
            report.walkStart = arena.Player.Position;
            CheckAuthoredCoordinates("return to spawn before review modes");
            QueueEvidenceFrame(CaptureKind.AuthoredSpawnComparison, "authored_spawn_comparison_" + runId + ".png", 0);
        }

        void QueueNormalAreaComparison()
        {
            PrepareArea(areaIndex);
            art.UseAuthoredRegions();
            CheckAuthoredCoordinates("endpoint comparison area " + (areaIndex + 1));
            QueueEvidenceFrame(CaptureKind.AuthoredComparison, "zone_" + (areaIndex + 1).ToString("D2") + "_authored_comparison_" + runId + ".png", areaIndex);
        }

        void QueueAreaEndpoint(VillageArtMode mode)
        {
            art.SetDisplay(mode);
            bool normal = mode == VillageArtMode.Color;
            QueueEvidenceFrame(normal ? CaptureKind.NormalArea : CaptureKind.SketchArea,
                "zone_" + (areaIndex + 1).ToString("D2") + (normal ? "_normal_" : "_sketch_") + runId + ".png", areaIndex);
        }

        void QueueEvidenceFrame(CaptureKind kind, string file, int index)
        {
            captureKind = kind;
            pendingFrame = new CaptureFrame { kind = kind.ToString(), index = index, file = file, mode = art.Mode.ToString(), weight = art.Weight,
                boundaryPixels = art.BoundaryPixels, authoredSketchUntilPixels = art.AuthoredSketchUntilPixels, authoredNormalFromPixels = art.AuthoredNormalFromPixels };
            if (kind == CaptureKind.AuthoredSpawnComparison || kind == CaptureKind.AuthoredComparison || kind == CaptureKind.SketchArea)
                report.authoredEndpointFrames.Add(pendingFrame);
            QueueCapture(Path.Combine(outputFolder, file));
        }

        void RememberActorExclusions()
        {
            actorPixelExclusions.Clear();
            foreach (var enemy in enemies)
            {
                if (!enemy) continue;
                foreach (var renderer in enemy.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    Bounds bounds = renderer.bounds;
                    Vector3 min = world.View.WorldToViewportPoint(bounds.min), max = world.View.WorldToViewportPoint(bounds.max);
                    actorPixelExclusions.Add(Rect.MinMaxRect(min.x * 1920 - 64, min.y * 1080 - 64, max.x * 1920 + 64, max.y * 1080 + 64));
                }
            }
        }

        void CheckAuthoredEndpointPixels(Color32[] authored, Color32[] endpoint, bool sketch, string label)
        {
            if (authored == null || endpoint == null || authored.Length != endpoint.Length)
            {
                report.authoredRenderedEndpoints = false;
                errors.Add("Missing displayed pixels for authored endpoint comparison: " + label);
                return;
            }
            float cameraLeft = world.View.transform.position.x * 32 - 480;
            int compared = 0, mismatched = 0;
            for (int i = 0; i < endpoint.Length; i++)
            {
                if (!AuthoredScenePixel(i)) continue;
                float x = cameraLeft + (i % 240) * 4 + .25f;
                if (sketch ? x > AuthoredSketchUntil : x < AuthoredNormalFrom) continue;
                compared++;
                if (PixelDifference(authored[i], endpoint[i]) != 0) mismatched++;
            }
            bool needsSamples = sketch || areaIndex >= 8;
            pendingFrame.authoredEndpointSamples = compared;
            pendingFrame.authoredEndpointMismatches = mismatched;
            if (mismatched == 0 && (!needsSamples || compared > 0)) return;
            report.authoredRenderedEndpoints = false;
            errors.Add("Displayed authored " + (sketch ? "sketch" : "normal") + " endpoint mismatch in " + label + ": compared=" + compared + ", mismatched=" + mismatched);
        }

        void QueueCapture(string path, int frames = 2)
        {
            activeCapturePath = path;
            phase = Phase.Settling; settleFrames = frames;
            earliestCaptureAt = Time.realtimeSinceStartupAsDouble + .1;
        }

        void QueueArtFrame()
        {
            int index = report.artFrames.Count;
            VillageArtMode mode = index == 0 ? VillageArtMode.Color : index == 1 ? VillageArtMode.Sketch : index == 7 ? VillageArtMode.Region : VillageArtMode.Blend;
            float weight = mode == VillageArtMode.Blend ? (index - 2) * .25f : .5f;
            // The centered split belongs only to explicit Region review, never Authored.
            if (mode == VillageArtMode.Region) art.SetBoundary(fixedCamera.x + 480);
            art.SetDisplay(mode, weight);
            captureKind = CaptureKind.Art;
            pendingFrame = new CaptureFrame { kind = "art", index = index, mode = mode.ToString(), weight = weight, boundaryPixels = art.BoundaryPixels,
                file = "art_" + mode.ToString().ToLowerInvariant() + "_" + Mathf.RoundToInt(weight * 100).ToString("D3") + "_" + runId + ".png" };
            QueueCapture(Path.Combine(outputFolder, pendingFrame.file));
        }

        void CheckArtPixels(Color32[] samples)
        {
            if (samples == null) { errors.Add("Art frame did not provide decoded 1920x1080 screen pixels: " + pendingFrame.file); return; }
            if (pendingFrame.index == 0) { colorSamples = samples; return; }
            if (pendingFrame.index == 1)
            {
                sketchSamples = samples;
                CheckAuthoredEndpointPixels(authoredSpawnSamples, sketchSamples, true, "initial authored spawn versus sketch endpoint");
                int different = 0;
                for (int i = 0; i < samples.Length; i++)
                    if (ScenePixel(i) && PixelDifference(colorSamples[i], samples[i]) >= 24) different++;
                report.renderedEndpointsDistinct = different >= 256;
                if (!report.renderedEndpointsDistinct) errors.Add("Color/sketch endpoints did not change enough rendered environment pixels: " + different);
                return;
            }
            if (colorSamples == null || sketchSamples == null) { errors.Add("Missing decoded endpoint pixels for " + pendingFrame.file); return; }
            int mismatched = 0, intermediate = 0, left = 0, right = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!ScenePixel(i)) continue;
                Color32 color = colorSamples[i], sketch = sketchSamples[i], current = samples[i];
                if (art.Mode == VillageArtMode.Region)
                {
                    if (PixelDifference(color, sketch) < 24) continue;
                    float worldX = world.View.transform.position.x * 32 - 480 + (i % 240) * 4;
                    if (worldX < art.BoundaryPixels - 240)
                    { left++; if (PixelDifference(color, current) != 0) mismatched++; }
                    else if (worldX > art.BoundaryPixels + 240)
                    { right++; if (PixelDifference(sketch, current) != 0) mismatched++; }
                }
                else if (art.Weight == 0 || art.Weight == 1)
                { if (PixelDifference(current, art.Weight == 0 ? color : sketch) != 0) mismatched++; }
                else
                {
                    if (current.r < Mathf.Min(color.r, sketch.r) - 1 || current.r > Mathf.Max(color.r, sketch.r) + 1 ||
                        current.g < Mathf.Min(color.g, sketch.g) - 1 || current.g > Mathf.Max(color.g, sketch.g) + 1 ||
                        current.b < Mathf.Min(color.b, sketch.b) - 1 || current.b > Mathf.Max(color.b, sketch.b) + 1) mismatched++;
                    if (PixelDifference(color, sketch) >= 24 && PixelDifference(current, color) > 3 && PixelDifference(current, sketch) > 3) intermediate++;
                }
            }
            if (mismatched != 0 || (art.Mode == VillageArtMode.Region ? left == 0 || right == 0 : art.Weight > 0 && art.Weight < 1 && intermediate < 256))
                errors.Add("Rendered art correspondence failed in " + pendingFrame.file + ": mismatches=" + mismatched + ", intermediate=" + intermediate + ", color-left=" + left + ", sketch-right=" + right);
            if (art.Mode == VillageArtMode.Blend && art.SampleWeight(new Vector2(fixedFeet.x * 32, -fixedFeet.y * 32)) != art.Weight)
                errors.Add("Blend SampleWeight did not match the requested weight.");
            if (art.Mode == VillageArtMode.Region)
            {
                var probe = new Vector2(art.BoundaryPixels, fixedCamera.y + 270);
                float center = art.SampleWeight(probe);
                if (center <= 0 || center >= 1 || art.SampleWeight(probe + Vector2.left * 960) != 0 || art.SampleWeight(probe + Vector2.right * 960) != 1)
                    errors.Add("World region did not provide color-left, sketch-right and an intermediate curved band.");
            }
        }

        bool ScenePixel(int index)
        {
            // HUD/minimap remain in every screenshot. Exclude them and the independently
            // animated player only from scenery pixel comparisons, never from blank checks.
            int x = index % 240 * 8, y = index / 240 * 8;
            return y < 780 && !playerPixelExclusion.Contains(new Vector2(x, y));
        }

        bool AuthoredScenePixel(int index)
        {
            if (!ScenePixel(index)) return false;
            var pixel = new Vector2(index % 240 * 8, index / 240 * 8);
            foreach (var exclusion in actorPixelExclusions) if (exclusion.Contains(pixel)) return false;
            return true;
        }

        static int PixelDifference(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        void CheckFixedScene()
        {
            string changed = null;
            if (!fixedFeet.Equals(arena.Player.Position) || !fixedVelocity.Equals(arena.Player.velocity) || fixedGrounded != arena.Player.grounded || fixedTicks != arena.SimulationTickCount ||
                !fixedCamera.Equals(new Vector2(world.CameraX, world.CameraY)) || fixedCameraSize != world.View.orthographicSize || fixedCameraAspect != world.View.aspect)
                changed = "player motor state, camera or simulation ticks";
            for (int i = 0; changed == null && i < fixedTransforms.Length; i++)
                if (!fixedTransforms[i] || !fixedMatrices[i].Equals(fixedTransforms[i].localToWorldMatrix))
                    changed = fixedTransforms[i] ? fixedTransforms[i].name : "destroyed scene transform";
            if (changed == null) return;
            report.fixedSceneUnchanged = false;
            errors.Add("Display-only art capture changed " + changed + " in " + pendingFrame.file);
        }

        void BeginMotionSequence()
        {
            // Alternate genuine right/left input to reuse the starting floor without
            // warping, resetting the actor, or replacing the common motor.
            sequenceStart = arena.Player.Position;
            sequenceCameraStart = new Vector2(world.CameraX, world.CameraY);
            motionFrame = 0;
            if (MotionModes[motionSequence] == VillageArtMode.Region) art.SetBoundary(world.CameraX + 480);
            art.SetDisplay(MotionModes[motionSequence], 0);
            sequenceProbe = new Vector2(art.BoundaryPixels, world.CameraY + 270);
            sequenceProbeWeight = art.SampleWeight(sequenceProbe);
            QueueWalkFrame();
        }

        void QueueWalkFrame()
        {
            int direction = motionSequence % 2 == 0 ? 1 : -1;
            if (motionFrame > 0)
                for (int tick = 0; tick < 12; tick++)
                {
                    simulation.Step(Dt, new CombatInput(direction));
                    if (MotionModes[motionSequence] == VillageArtMode.Blend)
                        art.SetDisplay(VillageArtMode.Blend, ((motionFrame - 1) * 12 + tick + 1) / 84f);
                    world.RefreshCamera(Dt);
                }
            QueueMotionFrame(CaptureKind.Walk, motionFrame * 12);
        }

        void QueueJumpFrame()
        {
            int target = JumpCaptureTicks[motionFrame];
            while (jumpTick < target)
            {
                simulation.Step(Dt, new CombatInput(pressed: jumpTick == 0 ? CombatButtons.Jump : CombatButtons.None));
                jumpTick++;
                jumpHighest = Mathf.Max(jumpHighest, arena.Player.Position.y);
                if (MotionModes[motionSequence] == VillageArtMode.Blend)
                    art.SetDisplay(VillageArtMode.Blend, 1 - jumpTick / 146f);
                world.RefreshCamera(Dt);
            }
            QueueMotionFrame(CaptureKind.Jump, jumpTick);
        }

        void QueueMotionFrame(CaptureKind kind, int tick)
        {
            arena.Presentation.Draw(0); arena.RangedPresentation?.Tick(); presentation.RefreshForCapture();
            captureKind = kind;
            string modeName = MotionModes[motionSequence] == VillageArtMode.Blend ? "transition" : art.Mode.ToString().ToLowerInvariant();
            pendingFrame = new CaptureFrame { index = motionFrame, sequence = motionSequence, tick = tick, kind = kind.ToString(),
                simulationSeconds = tick * Dt, mode = art.Mode.ToString(), weight = art.Weight, boundaryPixels = art.BoundaryPixels,
                file = kind.ToString().ToLowerInvariant() + "_" + modeName + "_" + tick.ToString("D3") + "_" + runId + ".png" };
            QueueCapture(Path.Combine(outputFolder, pendingFrame.file));
        }

        void CheckWalk()
        {
            Vector2 end = arena.Player.Position;
            float displacement = (end.x - sequenceStart.x) * (motionSequence % 2 == 0 ? 1 : -1);
            Vector2 cameraDelta = new Vector2(world.CameraX, world.CameraY) - sequenceCameraStart;
            if (displacement <= 1.25f || !arena.Player.grounded || Mathf.Abs(end.y - sequenceStart.y) > .06f)
                errors.Add("Shared walking did not advance along the authored starting floor in " + MotionModes[motionSequence] + ": " + displacement + " world units.");
            if (Mathf.Abs(cameraDelta.x) <= 1) errors.Add("Gameplay camera did not follow the actual walking input sequence in " + MotionModes[motionSequence] + ".");
            if (motionSequence == 0)
            {
                report.walkEnd = end; report.walkDisplacement = end.x - report.walkStart.x; report.walkGrounded = arena.Player.grounded;
                report.walkCameraDeltaPixels = report.walkFrames[7].cameraPixels - report.walkFrames[0].cameraPixels;
                if (report.walkCameraDeltaPixels.x <= 1) errors.Add("Gameplay camera did not follow the actual walking input sequence.");
            }
            CheckRegionProbe();
        }

        void CheckJump()
        {
            float rise = jumpHighest - jumpStart.y;
            bool landed = arena.Player.grounded && Mathf.Abs(arena.Player.Position.y - jumpStart.y) < .06f;
            if (motionSequence == 0) { report.jumpRise = rise; report.jumpLanded = landed; }
            if (rise <= 2.7f || !landed) errors.Add("Shared jump/landing failed in " + MotionModes[motionSequence] + ": rise=" + rise + ", landed=" + landed);
            CheckRegionProbe();
        }

        void CheckRegionProbe()
        {
            if (art.Mode == VillageArtMode.Authored)
            {
                CheckAuthoredCoordinates("real motor/camera motion");
                return;
            }
            if (art.Mode != VillageArtMode.Region || art.SampleWeight(sequenceProbe) == sequenceProbeWeight) return;
            report.worldRegionStable = false;
            errors.Add("Fixed world region sample changed after real camera/motor motion: " + sequenceProbe + ", before=" + sequenceProbeWeight + ", after=" + art.SampleWeight(sequenceProbe));
        }

        void RunAttack()
        {
            art.UseAuthoredRegions();

            Advance(3);
            simulation.Step(Dt, new CombatInput(pressed: CombatButtons.Basic));
            var cast = arena.Player.currentCast;
            report.basicAttackAccepted = cast && cast.isBasic;
            if (!report.basicAttackAccepted) errors.Add("Shared Basic input did not start a basic attack.");
            else
            {
                report.basicAttackDefinition = cast.Definition.Id;
                // Keep a visible action frame while the actual player renderer draws it.
                for (int tick = 0; tick < 120 && cast && cast.Age < cast.ImpactTime + .015f; tick++) simulation.Step(Dt);
            }
            report.simulationTicks = arena.SimulationTickCount;
            report.simulationFaulted = arena.SimulationFaulted;
            if (report.simulationFaulted) errors.Add("Shared simulation faulted during the smoke input sequence.");
            arena.Presentation.Draw(0);
            arena.RangedPresentation?.Tick();
            world.RefreshCamera(0, true);
            presentation.RefreshForCapture();
        }

        void Advance(int ticks, CombatInput input = default)
        { for (int i = 0; i < ticks; i++) simulation.Step(Dt, input); }

        IEnumerator CaptureDisplayedFrame()
        {
            // CaptureScreenshot reads the completed screen and IMGUI. It must not render
            // only world.View or replace the PixelViewport-owned camera target.
            yield return new WaitForEndOfFrame();
            if (phase != Phase.Capturing) yield break;
            try
            {
                var native = viewport ? viewport.NativeTexture : null;
                report.nativeWidth = native ? native.width : 0;
                report.nativeHeight = native ? native.height : 0;
                report.viewport960540 = viewport && viewport.Ready && report.nativeWidth == 960 && report.nativeHeight == 540;
                report.pointFiltered = native && native.filterMode == FilterMode.Point;
                report.hudEnabled = world && world.showHud;
                if (!report.viewport960540 || !report.pointFiltered) errors.Add("Capture did not use the ready 960x540 Point-filtered native viewport.");
                if (!report.hudEnabled) errors.Add("The ordinary gameplay HUD was disabled during screen capture.");
                if (captureKind != CaptureKind.Final && captureKind != CaptureKind.FullMap)
                {
                    if (captureKind != CaptureKind.Walk && captureKind != CaptureKind.Jump) CheckFixedScene();
                    pendingFrame.mode = art.Mode.ToString();
                    pendingFrame.weight = art.Weight;
                    pendingFrame.boundaryPixels = art.BoundaryPixels;
                    pendingFrame.authoredSketchUntilPixels = art.AuthoredSketchUntilPixels;
                    pendingFrame.authoredNormalFromPixels = art.AuthoredNormalFromPixels;
                    pendingFrame.feet = arena.Player.Position;
                    pendingFrame.grounded = arena.Player.grounded;
                    pendingFrame.cameraPixels = new Vector2(world.CameraX, world.CameraY);
                    pendingFrame.captureSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
                }
                ScreenCapture.CaptureScreenshot(activeCapturePath);
                // Diagnostic source buffer is separate evidence. It can never satisfy the
                // actual displayed-screen pixel check or replace the HUD-inclusive PNG.
                if (captureKind == CaptureKind.Final || captureKind == CaptureKind.FullMap || pendingFrame.index == 0)
                {
                    string label = captureKind == CaptureKind.Final || captureKind == CaptureKind.FullMap ? captureKind.ToString() :
                        pendingFrame.kind + "_" + pendingFrame.mode;
                    RecordRenderEvidence(label);
                    SaveNativeDiagnostic(native, label);
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        bool ScreenshotWritten(out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(activeCapturePath)) return false;
            try
            {
                using (var stream = new FileStream(activeCapturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 45) return false;
                    byte[] header = new byte[24], tail = new byte[12];
                    if (stream.Read(header, 0, header.Length) != header.Length) return false;
                    stream.Seek(-12, SeekOrigin.End);
                    if (stream.Read(tail, 0, tail.Length) != tail.Length) return false;
                    if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71 ||
                        tail[0] != 0 || tail[1] != 0 || tail[2] != 0 || tail[3] != 0 ||
                        tail[4] != 73 || tail[5] != 69 || tail[6] != 78 || tail[7] != 68) return false;
                    width = ReadBigEndian(header, 16); height = ReadBigEndian(header, 20);
                    return true;
                }
            }
            catch (IOException) { return false; }
        }

        static int ReadBigEndian(byte[] bytes, int index) =>
            bytes[index] << 24 | bytes[index + 1] << 16 | bytes[index + 2] << 8 | bytes[index + 3];

        PixelEvidence ReadScreenshotPixels(string path, out Color32[] sampled)
        {
            sampled = null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    return new PixelEvidence { failure = "PNG decoding failed." };
                Color32[] pixels = texture.GetPixels32();
                if ((captureKind == CaptureKind.Art || captureKind == CaptureKind.AuthoredSpawnComparison || captureKind == CaptureKind.AuthoredComparison ||
                    captureKind == CaptureKind.NormalArea || captureKind == CaptureKind.SketchArea) && texture.width == 1920 && texture.height == 1080)
                {
                    sampled = new Color32[240 * 135];
                    int index = 0;
                    for (int y = 0; y < 1080; y += 8)
                        for (int x = 0; x < 1920; x += 8) sampled[index++] = pixels[y * 1920 + x];
                }
                return MeasurePixels(texture, pixels);
            }
            finally { Destroy(texture); }
        }

        static PixelEvidence MeasurePixels(Texture2D texture, Color32[] pixels = null)
        {
            var evidence = new PixelEvidence { minimumLuminance = 1 };
            if (pixels == null) pixels = texture.GetPixels32();
            var colors = new HashSet<int>();
            double luminanceSum = 0;
            int visible = 0, bright = 0;
            // The native scene is enlarged exactly 2x. A 4px grid still samples tens of
            // thousands of independent scene pixels without counting AA noise as colors.
            for (int y = 0; y < texture.height; y += 4)
                for (int x = 0; x < texture.width; x += 4)
                {
                    Color32 color = pixels[y * texture.width + x];
                    float alpha = color.a / 255f;
                    float luminance = (.2126f * color.r + .7152f * color.g + .0722f * color.b) / 255f * alpha;
                    int r = Mathf.RoundToInt(color.r * alpha) >> 3, g = Mathf.RoundToInt(color.g * alpha) >> 3, b = Mathf.RoundToInt(color.b * alpha) >> 3;
                    colors.Add((r << 10) | (g << 5) | b);
                    evidence.minimumLuminance = Mathf.Min(evidence.minimumLuminance, luminance);
                    evidence.maximumLuminance = Mathf.Max(evidence.maximumLuminance, luminance);
                    luminanceSum += luminance;
                    if (luminance > .035f) visible++;
                    if (luminance > .2f) bright++;
                    evidence.samples++;
                }
            evidence.uniqueQuantizedColors = colors.Count;
            evidence.meanLuminance = evidence.samples > 0 ? (float)(luminanceSum / evidence.samples) : 0;
            evidence.nonBlackFraction = evidence.samples > 0 ? visible / (float)evidence.samples : 0;
            evidence.brightFraction = evidence.samples > 0 ? bright / (float)evidence.samples : 0;
            // These broad bounds cover both the pale sketch entrance and the colored
            // entrance with dark soil. Black+HUD-only, transparent and uniform buffers fail.
            evidence.valid = evidence.samples > 0 && evidence.nonBlackFraction >= .2f && evidence.brightFraction >= .02f &&
                evidence.maximumLuminance - evidence.minimumLuminance >= .2f && evidence.uniqueQuantizedColors >= 24;
            if (!evidence.valid)
                evidence.failure = "nonBlack=" + evidence.nonBlackFraction.ToString("F4") + ", bright=" + evidence.brightFraction.ToString("F4") +
                    ", luminanceRange=" + (evidence.maximumLuminance - evidence.minimumLuminance).ToString("F4") + ", quantizedColors=" + evidence.uniqueQuantizedColors;
            return evidence;
        }

        void SaveNativeDiagnostic(RenderTexture native, string label)
        {
            var diagnostic = new NativeDiagnostic { file = "diagnostic_native_960x540_" + label + "_" + runId + ".png" };
            report.nativeDiagnostics.Add(diagnostic);
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                if (!native || !native.IsCreated()) throw new InvalidOperationException("Native viewport buffer is unavailable.");
                texture = new Texture2D(native.width, native.height, TextureFormat.RGBA32, false, false);
                RenderTexture.active = native;
                texture.ReadPixels(new Rect(0, 0, native.width, native.height), 0, 0, false);
                texture.Apply(false, false);
                diagnostic.pixels = MeasurePixels(texture);
                File.WriteAllBytes(Path.Combine(outputFolder, diagnostic.file), texture.EncodeToPNG());
            }
            catch (Exception exception) { diagnostic.error = exception.ToString(); }
            finally { RenderTexture.active = previous; if (texture) Destroy(texture); }
        }

        void OnCameraBegin(ScriptableRenderContext context, Camera camera)
        {
            if (world && camera == world.View) sourceBeginRender++;
            if (viewport && camera == viewport.OutputCamera) outputBeginRender++;
        }

        void OnCameraEnd(ScriptableRenderContext context, Camera camera)
        {
            if (world && camera == world.View) sourceEndRender++;
            if (viewport && camera == viewport.OutputCamera) outputEndRender++;
        }

        void OnBuiltInPreRender(Camera camera)
        {
            if (world && camera == world.View) builtInSourcePreRender++;
            if (viewport && camera == viewport.OutputCamera) builtInOutputPreRender++;
        }

        void OnBuiltInPreCull(Camera camera)
        {
            if (world && camera == world.View) builtInSourcePreCull++;
            if (viewport && camera == viewport.OutputCamera) builtInOutputPreCull++;
        }

        void OnBuiltInPostRender(Camera camera)
        {
            if (world && camera == world.View) builtInSourcePostRender++;
            if (viewport && camera == viewport.OutputCamera) builtInOutputPostRender++;
        }

        void OnGUI()
        {
            if (smokeRequested && Event.current.type == EventType.Repaint) guiRepaints++;
        }

        void RecordRenderEvidence(string label)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var qualityPipeline = QualitySettings.renderPipeline;
            int quality = QualitySettings.GetQualityLevel();
            var evidence = new RenderEvidence {
                label = label, pipeline = pipeline ? pipeline.GetType().FullName + ":" + pipeline.name : "Built-in/null",
                pipelineInstance = RenderPipelineManager.currentPipeline != null ? RenderPipelineManager.currentPipeline.GetType().FullName : "none",
                qualityPipeline = qualityPipeline ? qualityPipeline.GetType().FullName + ":" + qualityPipeline.name : "Default/null",
                qualityName = quality >= 0 && quality < QualitySettings.names.Length ? QualitySettings.names[quality] : quality.ToString(),
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
                focused = Application.isFocused, runInBackground = Application.runInBackground, batchMode = Application.isBatchMode,
                frameCount = Time.frameCount, renderedFrameCount = Time.renderedFrameCount, guiRepaints = guiRepaints,
                cameraCount = Camera.allCamerasCount, displayCount = Display.displays.Length, screenMode = Screen.fullScreenMode.ToString(),
                sourceBeginRender = sourceBeginRender, sourceEndRender = sourceEndRender, outputBeginRender = outputBeginRender, outputEndRender = outputEndRender,
                builtInSourcePreRender = builtInSourcePreRender, builtInOutputPreRender = builtInOutputPreRender,
                builtInSourcePreCull = builtInSourcePreCull, builtInOutputPreCull = builtInOutputPreCull,
                builtInSourcePostRender = builtInSourcePostRender, builtInOutputPostRender = builtInOutputPostRender,
                source = InspectCamera(world ? world.View : null), output = InspectCamera(viewport ? viewport.OutputCamera : null)
            };
            report.rendering.Add(evidence);
            Debug.Log("VILLAGE_SMOKE_RENDER " + JsonUtility.ToJson(evidence));
        }

        static CameraEvidence InspectCamera(Camera camera)
        {
            if (!camera) return new CameraEvidence { name = "missing" };
            var componentNames = new List<string>();
            foreach (var component in camera.GetComponents<Component>()) if (component) componentNames.Add(component.GetType().FullName);
            return new CameraEvidence { name = camera.name, target = camera.targetTexture ? camera.targetTexture.name : "Display/backbuffer",
                components = string.Join(", ", componentNames), enabled = camera.enabled, active = camera.gameObject.activeInHierarchy,
                targetCreated = camera.targetTexture && camera.targetTexture.IsCreated(), cullingMask = camera.cullingMask,
                targetDisplay = camera.targetDisplay, depth = camera.depth, clearFlags = camera.clearFlags.ToString(), clearColor = camera.backgroundColor,
                viewportRect = camera.rect };
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors.Count < 64) errors.Add(type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack));
        }

        void Fail(string reason) { errors.Add(reason); Finish(); }

        void Finish()
        {
            if (phase == Phase.Complete) return;
            phase = Phase.Complete;
            Cleanup();
            report.errors = errors.ToArray();
            report.success = errors.Count == 0 && report.screenshotComplete && report.displayedPixels != null && report.displayedPixels.valid &&
                report.minimapEnabled && report.fullMapOpened && report.fullMapClosed && report.fixedSceneUnchanged && report.worldRegionStable && report.renderedEndpointsDistinct &&
                report.walkFrames.Count == 8 && report.walkFrames.TrueForAll(frame => frame.pixels != null && frame.pixels.valid) &&
                report.artFrames.Count == ArtFrameCount && report.artFrames.TrueForAll(frame => frame.pixels != null && frame.pixels.valid) &&
                report.motionFrames.Count == MotionModes.Length * (WalkFrameCount + JumpCaptureTicks.Length) &&
                report.motionFrames.TrueForAll(frame => frame.pixels != null && frame.pixels.valid) &&
                report.defaultAuthored && report.authoredWorldStable && report.authoredRenderedEndpoints &&
                report.initialAuthoredFrame != null && report.initialAuthoredFrame.mode == VillageArtMode.Authored.ToString() && report.initialAuthoredFrame.pixels != null && report.initialAuthoredFrame.pixels.valid &&
                report.authoredFrames.Count == 24 && report.authoredFrames.TrueForAll(frame => frame.mode == VillageArtMode.Authored.ToString() && frame.pixels != null && frame.pixels.valid) &&
                report.authoredBoundaryFrames.Count == AuthoredEdgeCameras.Length && report.authoredBoundaryFrames.TrueForAll(frame => frame.mode == VillageArtMode.Authored.ToString() && frame.pixels != null && frame.pixels.valid) &&
                report.normalFrames.Count == 24 && report.normalFrames.TrueForAll(frame => frame.mode == VillageArtMode.Color.ToString() && frame.pixels != null && frame.pixels.valid) &&
                report.authoredChecks.Count > 0 && report.authoredChecks.TrueForAll(check => check.pass);
            try
            {
                if (!string.IsNullOrEmpty(outputFolder))
                    File.WriteAllText(Path.Combine(outputFolder, "smoke_report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("Village standalone smoke " + (report.success ? "PASS: " : "FAIL: ") + outputFolder);
            }
            catch (Exception exception) { report.success = false; Debug.LogError("Village smoke report could not be written: " + exception); }
            enabled = false;
            Application.Quit(report.success ? 0 : 1);
        }

        void OnDestroy() { Cleanup(); }

        void Cleanup()
        {
            if (!smokeRequested) return;
            if (world && world.FullMap) world.FullMap.SetOpen(false);
            if (subscribed)
            {
                Application.logMessageReceived -= OnLog;
                RenderPipelineManager.beginCameraRendering -= OnCameraBegin;
                RenderPipelineManager.endCameraRendering -= OnCameraEnd;
                Camera.onPreRender -= OnBuiltInPreRender;
                Camera.onPreCull -= OnBuiltInPreCull;
                Camera.onPostRender -= OnBuiltInPostRender;
                subscribed = false;
            }
            if (simulation != null)
            {
                try { simulation.Dispose(); }
                catch (Exception exception) { errors.Add("Manual simulation cleanup: " + exception.Message); }
                simulation = null;
            }
            if (ownsInput)
            {
                if (presentation) presentation.animateEnvironment = previousAnimation;
                if (art) { art.SetBoundary(previousBoundary); art.SetDisplay(previousArtMode, previousArtWeight); }
                if (arena && arena.Player) arena.Player.UseTestInput = previousTestInput;
                for (int i = 0; i < enemies.Count; i++) if (enemies[i]) enemies[i].aggressive = enemyAggression[i];
                ownsInput = false;
            }
            Application.runInBackground = previousRunInBackground;
        }
    }
}
