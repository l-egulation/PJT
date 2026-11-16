using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Deviant.Combat.Contracts;
using Unity.Profiling;
using UnityEngine;

namespace Deviant.Combat.Editor
{
    // Real-time frame cost in the editor play session, split by phase: idle, attacking with the
    // hit-feel layer, attacking without it, and attacking without the team hit feedback bridge. The first
    // phase absorbs one-time costs (first hit loads, editor start-up indexing).
    // Attacks go through CombatPlayer.SubmitInput on the live (automatic) simulation. Editor scripts cannot be
    // added as components, so it samples once per frame from Application.onBeforeRender.
    sealed class CombatFeelProfile
    {
        static readonly (string name, ProfilerCategory category)[] Markers =
        {
            ("Main Thread", ProfilerCategory.Internal), ("PlayerLoop", ProfilerCategory.Internal), ("EditorLoop", ProfilerCategory.Internal),
            ("FixedUpdate.ScriptRunBehaviourFixedUpdate", ProfilerCategory.Scripts), ("Update.ScriptRunBehaviourUpdate", ProfilerCategory.Scripts),
            ("PreLateUpdate.ScriptRunBehaviourLateUpdate", ProfilerCategory.Scripts), ("FixedUpdate.PhysicsFixedUpdate", ProfilerCategory.Physics),
            ("FixedUpdate.Physics2DFixedUpdate", ProfilerCategory.Physics), ("PostLateUpdate.FinishFrameRendering", ProfilerCategory.Render),
            ("Gfx.WaitForPresentOnGfxThread", ProfilerCategory.Render), ("GC.Collect", ProfilerCategory.Memory), ("GC Allocated In Frame", ProfilerCategory.Memory),
        };
        static readonly string[] Phases = { "warmup-attack", "idle", "attack-feel-on", "attack-feel-off", "attack-team-off" };
        const float PhaseSeconds = 6;
        public CombatArena arena; public Action prepare; public Action<bool> done; public string output;
        ProfilerRecorder[] recorders;
        readonly List<float>[] frames = Phases.Select(_ => new List<float>()).ToArray();
        readonly double[][] sums = Phases.Select(_ => new double[Markers.Length]).ToArray();
        float started = -1, nextPress; int phase = -1;
        MonoBehaviour bridge;

        public void Start()
        {
            recorders = Markers.Select(m => ProfilerRecorder.StartNew(m.category, m.name)).ToArray();
            Application.onBeforeRender += Update;
        }
        void Stop() { Application.onBeforeRender -= Update; foreach (var r in recorders) r.Dispose(); }
        void Update()
        {
            if (started < 0) { started = Time.realtimeSinceStartup; bridge = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.GetType().Name == "HitFeedbackBridge"); }
            float age = Time.realtimeSinceStartup - started; int now = Mathf.FloorToInt(age / PhaseSeconds);
            if (now != phase)
            {
                phase = now;
                if (phase >= Phases.Length) { Stop(); Write(); done(true); return; }
                arena.FeelFeedback.SetEffectsEnabled(phase != 3);
                SetBridge(phase != 4);
                prepare(); nextPress = age + .5f;
            }
            // Skip the first half second of each phase (preparation hitch).
            if (age - phase * PhaseSeconds > .5f)
            {
                frames[phase].Add(Time.unscaledDeltaTime * 1000);
                for (int i = 0; i < recorders.Length; i++) sums[phase][i] += recorders[i].LastValue;
            }
            if (phase != 1 && age >= nextPress && arena.Player)
            {
                nextPress = age + .22f;
                if (Mathf.FloorToInt(age * 2) % 6 == 5) prepare();                    // pushed targets come back in range
                arena.Player.SubmitInput(new CombatInput(0, CombatButtons.Basic));
            }
        }
        void SetBridge(bool on)
        {
            if (!bridge) return;
            var method = bridge.GetType().GetMethod("SetEffectEnabled"); var kind = method.GetParameters()[0].ParameterType;
            foreach (var value in Enum.GetValues(kind)) method.Invoke(bridge, new[] { value, (object)on });
        }
        void Write()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture; var sb = new StringBuilder("{\n");
            sb.Append("  \"gpu\": \"" + SystemInfo.graphicsDeviceName + "\", \"vSync\": " + QualitySettings.vSyncCount + ", \"targetFrameRate\": " + Application.targetFrameRate + ",\n");
            for (int p = 0; p < Phases.Length; p++)
            {
                var f = frames[p].OrderBy(v => v).ToList(); int n = Math.Max(1, f.Count);
                string Q(double q) => f.Count == 0 ? "0" : f[Math.Min(f.Count - 1, (int)(q * f.Count))].ToString("0.0", inv);
                sb.Append("  \"" + Phases[p] + "\": { \"frames\": " + f.Count + ", \"avgMs\": " + (f.Count == 0 ? 0 : f.Average()).ToString("0.0", inv) +
                    ", \"p50\": " + Q(.5) + ", \"p95\": " + Q(.95) + ", \"max\": " + Q(.999) + ", \"over33ms\": " + f.Count(v => v > 33.3f) + ", \"markersAvgMs\": {");
                for (int i = 0; i < Markers.Length; i++)
                {
                    double avg = sums[p][i] / n; bool bytes = Markers[i].name == "GC Allocated In Frame";
                    sb.Append((i > 0 ? ", " : "") + "\"" + Markers[i].name + "\": " + (bytes ? (avg / 1024).ToString("0.0", inv) + "" : (avg / 1e6).ToString("0.00", inv)));
                }
                sb.Append(" } }" + (p + 1 < Phases.Length ? "," : "") + "\n");
            }
            sb.Append("}\n"); File.WriteAllText(output, sb.ToString()); Debug.Log("FEEL_PROFILE " + sb);
        }
    }
}
