using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using UnityEditor;
using UnityEngine;

namespace Deviant.CombatLab
{
    // Editor-owned scheduling for an explicitly selected, initialized Lab. The
    // manual lease suspends only this arena's automatic FixedUpdate driver.
    public sealed class LabExecutionController : IDisposable
    {
        public const int MaximumTicksPerUpdate = 128;
        readonly CombatArena arena;
        readonly List<TargetBinding> targets = new List<TargetBinding>();
        CombatArena.ManualSimulation simulation;
        IControlScope controls;
        IControlLease pause;
        VisualOutputScope visualOutput;
        CombatInputSession session;
        bool hasSession, disposed, executing;
        double previousEditorTime, backlog;
        float slowFactor = 1;

        public CombatArena Arena => arena;
        public bool IsDisposed => disposed;
        public bool IsPaused => pause != null && pause.IsActive;
        public bool IsExternallyPaused => !IsPaused && arena && arena.Controls.Current.SimulationPaused;
        public bool IsInputBlocked => arena && arena.Player && arena.Player.InputBlocked;
        public double BacklogSeconds => backlog;
        public int LastUpdateTicks { get; private set; }
        public int Axis => arena && arena.Player ? arena.Player.CurrentInput.Axis : 0;
        public bool SpecialHeld => arena && arena.Player && arena.Player.CurrentInput.SpecialHeld;
        public bool VisualOutputEnabled => visualOutput == null;
        public bool VisualOutputOverridden => visualOutput != null && !visualOutput.IsSuppressing;
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }
        public int TargetCount => targets.Count;
        public string LastError { get; private set; }
        public float FixedStep => Time.fixedDeltaTime;
        public float SlowFactor
        {
            get => slowFactor;
            set
            {
                Check();
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "A local slow factor must be greater than zero and at most one.");
                PumpEditorTime();
                slowFactor = value;
            }
        }

        public LabExecutionController(CombatArena arena)
        {
            if (!Application.isPlaying || !arena || !arena.isActiveAndEnabled || !arena.Player ||
                arena.CompositionProfile != CombatCompositionProfile.Lab)
                throw new InvalidOperationException("Select an initialized, active Lab runtime in Play Mode. Game runtimes are never controlled here.");
            if (arena.ManualSimulationActive)
                throw new InvalidOperationException("The selected Lab already has a manual simulation owner. Release it before binding.");
            if (!arena.ReadModel.TryGetSession(out var current) || !arena.ReadModel.TryReadPlayer(current, out _))
                throw new InvalidOperationException("The selected Lab has no readable initialized combat session.");
            this.arena = arena;
            try
            {
                simulation = arena.AcquireManualSimulation();
                controls = arena.Controls.OpenScope("Lab execution window");
                pause = controls.PauseSimulation();
                BindReadHandles();
                arena.Initialized += OnReset;
                arena.ResetCompleted += OnReset;
                arena.Deactivated += Dispose;
                previousEditorTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += OnEditorUpdate;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            }
            catch { Dispose(); throw; }
        }

        public void Run()
        {
            Check();
            if (arena.SimulationFaulted) throw new InvalidOperationException("Reset the Lab after a failed simulation step.");
            if (!IsPaused) return;
            ClearInput();
            pause.Dispose(); pause = null;
            previousEditorTime = EditorApplication.timeSinceStartup;
            LastError = null;
        }

        public void Pause()
        {
            Check();
            try { PumpEditorTime(); }
            finally
            {
                if (!disposed)
                {
                    if (!IsPaused) pause = controls.PauseSimulation();
                    ClearInput();
                    LastUpdateTicks = 0;
                }
            }
        }

        // Removing our own pause is not permission to remove another owner's
        // request. In all outcomes our pause is restored before returning.
        public bool SingleTick(CombatInput input = default)
        {
            Check();
            if (!IsPaused) throw new InvalidOperationException("Pause this controller before requesting a single tick.");
            executing = true;
            LastUpdateTicks = 0;
            var ownPause = pause;
            pause = null;
            try
            {
                ownPause.Dispose();
                ClearInput();
                if (arena.Controls.Current.SimulationPaused)
                {
                    LastUpdateTicks = 0;
                    return false;
                }
                simulation.Step(ValidatedStep(), input);
                LastUpdateTicks = 1;
                LastError = null;
                return true;
            }
            finally
            {
                executing = false;
                if (disposed) ReleaseOwnedResources();
                else
                {
                    pause = controls.PauseSimulation();
                    ClearInput();
                    previousEditorTime = EditorApplication.timeSinceStartup;
                }
            }
        }

        public void Reset()
        {
            Check();
            executing = true;
            try { simulation.Reset(); }
            finally { executing = false; if (disposed) ReleaseOwnedResources(); }
            // ResetCompleted explicitly rebinds the new epoch's read handles.
            LastError = null;
        }

        public void SetContinuousInput(int requestedAxis, bool held)
        {
            Check();
            var input = new CombatInput(requestedAxis, specialHeld: held);
            if (IsPaused || IsInputBlocked || IsExternallyPaused) { ClearInput(); return; }
            arena.Player.SubmitInput(input);
        }

        public bool QueueButtons(CombatButtons buttons, CombatButtons releaseButtons = CombatButtons.None)
        {
            Check();
            var input = new CombatInput(Axis, buttons, SpecialHeld, releaseButtons);
            if (IsPaused || IsExternallyPaused || IsInputBlocked) { ClearInput(); return false; }
            arena.Player.SubmitInput(input);
            return true;
        }

        public void SetVisualOutput(bool enabled)
        {
            Check();
            if (enabled) { visualOutput?.Dispose(); visualOutput = null; }
            else if (visualOutput == null) visualOutput = new VisualOutputScope(arena.ViewCamera);
        }

        public bool TryReadPlayer(out ActorReadSnapshot snapshot)
        {
            snapshot = default;
            return !disposed && hasSession && arena && arena.ReadModel.TryReadPlayer(session, out snapshot);
        }

        public string TargetName(int index) => targets[index].Name;
        public bool TryReadTarget(int index, out ActorReadSnapshot snapshot)
        {
            snapshot = default;
            return !disposed && hasSession && arena && arena.ReadModel.TryReadActor(targets[index].Handle, out snapshot);
        }

        // Elapsed time is unscaled Editor time, not Unity deltaTime. The budget
        // bounds one editor callback, never discards elapsed time, and leaves
        // outstanding simulation seconds visible as backlog. Pauses accrue no
        // new time, while any backlog already earned is retained until Run.
        public int AdvanceElapsed(double unscaledElapsedSeconds)
        {
            Check();
            if (double.IsNaN(unscaledElapsedSeconds) || double.IsInfinity(unscaledElapsedSeconds) || unscaledElapsedSeconds < 0 || unscaledElapsedSeconds > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(unscaledElapsedSeconds));
            LastUpdateTicks = 0;
            arena.AdvanceFeedbackAccent((float)unscaledElapsedSeconds);
            float feedbackScale=arena.Controls.Current.SimulationScale;
            if (IsPaused || arena.Controls.Current.SimulationPaused)
            {
                if (!IsPaused) ClearInput();
                return 0;
            }
            if (IsInputBlocked) ClearInput();
            double earned = unscaledElapsedSeconds * slowFactor * feedbackScale;
            if (earned > double.MaxValue - backlog)
                throw new ArgumentOutOfRangeException(nameof(unscaledElapsedSeconds), "The retained backlog would overflow.");
            backlog += earned;
            float step = ValidatedStep();
            while (backlog >= step && LastUpdateTicks < MaximumTicksPerUpdate)
            {
                if (arena.Controls.Current.SimulationPaused) { ClearInput(); break; }
                if (IsInputBlocked) ClearInput();
                executing = true;
                try { simulation.StepPending(step); }
                finally { executing = false; if (disposed) ReleaseOwnedResources(); }
                backlog -= step;
                LastUpdateTicks++;
                if (disposed) break;
            }
            return LastUpdateTicks;
        }

        void OnEditorUpdate()
        {
            // EditorApplication may already have captured this delegate when a
            // prior subscriber disposes us during the same dispatch.
            if (disposed) return;
            if (!Application.isPlaying || !arena || !arena.isActiveAndEnabled || !arena.Player)
            { Dispose(); return; }
            try { PumpEditorTime(); }
            catch (Exception error)
            {
                if (disposed) throw;
                LastError = error.Message;
                if (!IsPaused && controls.IsActive) pause = controls.PauseSimulation();
                ClearInput();
                previousEditorTime = EditorApplication.timeSinceStartup;
            }
        }

        void PumpEditorTime()
        {
            double now = EditorApplication.timeSinceStartup;
            double elapsed = Math.Max(0, now - previousEditorTime);
            previousEditorTime = now;
            AdvanceElapsed(elapsed);
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Dispose();
        }

        void OnReset()
        {
            if (disposed) return;
            backlog = 0;
            LastUpdateTicks = 0;
            ClearInput();
            previousEditorTime = EditorApplication.timeSinceStartup;
            BindReadHandles();
        }

        void BindReadHandles()
        {
            targets.Clear();
            hasSession = arena.ReadModel.TryGetSession(out session);
            if (!hasSession || !arena.ReadModel.TryReadPlayer(session, out var player)) return;
            OriginX = player.Kinematics.X; OriginY = player.Kinematics.Y;
            foreach (var enemy in arena.enemies)
                if (enemy && arena.TryBindActor(enemy, out var handle)) targets.Add(new TargetBinding(enemy.name, handle));
        }

        void ClearInput()
        {
            if (arena && arena.Player) arena.Player.ClearInputState();
        }

        float ValidatedStep()
        {
            float step = Time.fixedDeltaTime;
            if (float.IsNaN(step) || float.IsInfinity(step) || step <= 0)
                throw new InvalidOperationException("The existing Unity fixed step must be finite and positive.");
            return step;
        }

        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabExecutionController));
            if (executing) throw new InvalidOperationException("The Lab execution controller is already advancing or resetting.");
            if (!Application.isPlaying || !arena || !arena.isActiveAndEnabled || !arena.Player)
                throw new InvalidOperationException("The bound Lab is no longer active.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            if (arena)
            {
                arena.Initialized -= OnReset;
                arena.ResetCompleted -= OnReset;
                arena.Deactivated -= Dispose;
            }
            ClearInput(); targets.Clear(); hasSession = false;
            // OnDisable can run synchronously inside a damage/reset callback.
            // ManualSimulation must finish its guard before releasing the lease.
            if (!executing) ReleaseOwnedResources();
        }

        void ReleaseOwnedResources()
        {
            try { visualOutput?.Dispose(); }
            finally
            {
                visualOutput = null;
                try { simulation?.Dispose(); }
                finally
                {
                    simulation = null;
                    try { pause?.Dispose(); }
                    finally { pause = null; controls?.Dispose(); controls = null; }
                }
            }
        }

        readonly struct TargetBinding
        {
            public string Name { get; }
            public CombatActorHandle Handle { get; }
            public TargetBinding(string name, CombatActorHandle handle) { Name = name; Handle = handle; }
        }

        // Camera-scoped output suppression includes views spawned after binding.
        // Presenters, cue handles, renderer state and all combat logic keep
        // running. Only restore our write; a later camera owner's write wins.
        sealed class VisualOutputScope : IDisposable
        {
            readonly Camera camera;
            readonly int originalMask;
            bool disposed;
            public bool IsSuppressing => !disposed && camera && camera.cullingMask == 0;
            public VisualOutputScope(Camera camera)
            {
                if (!camera) throw new InvalidOperationException("The selected Lab has no view camera.");
                this.camera = camera; originalMask = camera.cullingMask;
                camera.cullingMask = 0;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (camera && camera.cullingMask == 0) camera.cullingMask = originalMask;
            }
        }
    }
}
