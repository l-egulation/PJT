using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // A crowd hit used to allocate a fresh 0.7–1.5 MB bake per burst (GC pauses in fight frames).
    // Reused frame arrays and per-thread scratch must keep every baked pixel identical to a fresh bake.
    public sealed class CombatFeelPixelFxPoolTests
    {
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static readonly Type Fx = typeof(CombatFeelPixelFx);
        GameObject host;
        CombatFeelPixelFx fx;
        [SetUp] public void Create() { host = new GameObject("pixel fx pool test"); fx = host.AddComponent<CombatFeelPixelFx>(); }
        [TearDown] public void Destroy() { if (host) Object.DestroyImmediate(host); }
        static object Spec(string name) => Fx.GetField(name, F).GetValue(null);
        static int Size(object spec) => (int)spec.GetType().GetField("S").GetValue(spec);
        static Color32[][] Fresh(object spec) { int S = Size(spec); var f = new Color32[4][]; for (int i = 0; i < 4; i++) f[i] = new Color32[S * S]; return f; }
        Color32[][] Bake(object spec, int seed, Color32[][] into)
            => (Color32[][])Fx.GetMethod("BuildBurst", F).Invoke(fx, new object[] { spec, .41f, true, -1, new System.Random(seed), into });

        [Test] public void ReusedFramesAndScratchBakeTheSamePixelsAsAFreshBake()
        {
            var heavy = Spec("Heavy");
            Color32[][] reference = null;
            // A new thread starts with empty per-thread scratch: the reference never sees reused buffers.
            var thread = new Thread(() => reference = Bake(heavy, 7, Fresh(heavy))); thread.Start(); thread.Join();
            // Leave larger/smaller dirty scratch on this thread and dirty output arrays from earlier bursts.
            Bake(Spec("Kill"), 3, Fresh(Spec("Kill"))); Bake(Spec("Basic"), 5, Fresh(Spec("Basic")));
            var dirty = Fresh(heavy);
            foreach (var frame in dirty) for (int i = 0; i < frame.Length; i++) frame[i] = new Color32(9, 9, 9, 255);
            var reused = Bake(heavy, 7, dirty);
            Assert.That(reused, Is.SameAs(dirty), "The bake writes into the leased arrays instead of allocating new ones.");
            for (int f = 0; f < 4; f++) Assert.That(reused[f], Is.EqualTo(reference[f]), "Frame " + f + " pixels changed with reused buffers.");
        }

        [Test] public void FinishedBurstsReturnTheirFramesAndRunningBakesKeepThem()
        {
            var basic = Spec("Basic");
            var burstType = Fx.GetNestedType("Burst", F);
            var lease = Fx.GetMethod("LeaseFrames", F); var release = Fx.GetMethod("ReturnFrames", F);
            object Burst(Task<Color32[][]> job) { var b = Activator.CreateInstance(burstType); burstType.GetField("spec").SetValue(b, basic); burstType.GetField("job").SetValue(b, job); return b; }

            var frames = (Color32[][])lease.Invoke(fx, new object[] { Size(basic) });
            release.Invoke(fx, new[] { Burst(Task.FromResult(frames)) });
            Assert.That(fx.PooledFrameSets, Is.EqualTo(1));
            Assert.That(lease.Invoke(fx, new object[] { Size(basic) }), Is.SameAs(frames), "The next burst of the same size reuses the returned arrays.");
            Assert.That(fx.PooledFrameSets, Is.Zero);

            var running = new TaskCompletionSource<Color32[][]>();
            release.Invoke(fx, new[] { Burst(running.Task) });
            Assert.That(fx.PooledFrameSets, Is.Zero, "A bake still writing on the worker must not hand its arrays to another burst.");
            running.SetResult(frames);
        }
    }
}
