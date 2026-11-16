using System;
using System.Collections;
using System.Reflection;
using Deviant.Integration;
using Deviant.World;
using Deviant.World.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ForestCommonLayoutIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static MonoBehaviour Forest(CombatArena arena)
        {
            var game = arena.GetComponent("GameComposition"); return (MonoBehaviour)game.GetType().GetField("forest").GetValue(game);
        }
        static ChapterSession Chapter(MonoBehaviour world) => (ChapterSession)world.GetType().GetProperty("Chapter").GetValue(world);
        static bool Bind(MonoBehaviour world, LayoutBodyProfile profile, Vector2 entry, out string error)
        {
            var arguments = new object[] { profile, new LayoutPoint(entry.x, entry.y), null };
            bool result = (bool)world.GetType().GetMethod("TryBindLayoutProfile").Invoke(world, arguments); error = (string)arguments[2]; return result;
        }
        static void Ensure(MonoBehaviour world, float x) => world.GetType().GetMethod("EnsureChunks").Invoke(world, new object[] { x });

        [UnityTest] public IEnumerator ActualGameUsesOneWorldOwnedCommonSessionAcrossStreamingAndGameReset()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var world = Forest(arena); var chapter = Chapter(world);
            Assert.That(chapter, Is.Not.Null); Assert.That(chapter.IsActive, Is.True);
            Assert.That(chapter.Layout.Id, Is.EqualTo("prototype-forest-strip")); Assert.That(chapter.Layout.Repetition, Is.Not.Null);
            Assert.That(chapter.MovementProof.Succeeded, Is.True); Assert.That(chapter.MovementProof.Witnesses.Count, Is.GreaterThan(40));
            string map = chapter.ResolvedLayoutHash; var identity = chapter.Id;
            foreach (float x in new[] { 4.6875f, 192, 512, 768, 4.6875f })
            {
                Ensure(world, x);
                Assert.That(Chapter(world), Is.SameAs(chapter)); Assert.That(chapter.Id, Is.EqualTo(identity));
                Assert.That(chapter.ResolvedLayoutHash, Is.EqualTo(map));
            }
            Assert.That(chapter.Layout.TryResolveModule("forest-strip/0", out var first), Is.True);
            Assert.That(chapter.Layout.TryResolveModule("forest-strip/8", out var repeated), Is.True);
            Assert.That(first.Id, Is.Not.EqualTo(repeated.Id)); Assert.That(first.ContentId, Is.EqualTo(repeated.ContentId));
            var player = arena.Player; var weapons = player.weapons;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset();
                Assert.That(Chapter(world), Is.SameAs(chapter)); Assert.That(chapter.Id, Is.EqualTo(identity));
                Assert.That(player, Is.SameAs(arena.Player)); Assert.That(weapons, Is.SameAs(arena.Player.weapons));
                Assert.That(chapter.MovementProof.Succeeded, Is.True);
            }
        }

        [UnityTest] public IEnumerator MovementRevalidationKeepsMapAndLoadedIdsAndRejectsImpossibleCapabilities()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var world = Forest(arena); var chapter = Chapter(world);
            var original = UnityChapterModules.ProfileFor(arena.Player); var identity = chapter.Id; var layout = chapter.Layout;
            string map = chapter.ResolvedLayoutHash; int loaded = chapter.LoadedModuleCount;
            var slower = new LayoutBodyProfile(original.Width, original.Height, original.OffsetX, original.OffsetY, 4,
                "actual-test-slower-profile", original.Motion);
            Assert.That(Bind(world, slower, arena.Player.spawn, out var error), Is.True, error);
            Assert.That(chapter.Layout, Is.SameAs(layout)); Assert.That(chapter.Id, Is.EqualTo(identity));
            Assert.That(chapter.LoadedModuleCount, Is.EqualTo(loaded)); Assert.That(chapter.ResolvedLayoutHash, Is.EqualTo(map));
            Assert.That(chapter.Profile.WalkSpeed, Is.EqualTo(4)); Assert.That(chapter.MovementProof.ProfileHash, Is.EqualTo(slower.ContentHash));
            var noJump = new LayoutMotionProfile(original.Motion.Step, .01f, .01f, original.Motion.RiseGravity, original.Motion.FallGravity,
                original.Motion.DashSpeed, original.Motion.DashDuration, original.Motion.DashRecovery, original.Motion.DashInvulnerability,
                original.Motion.DashJumpCarrySpeed, original.Motion.DashJumpCarryDuration, "actual-test-no-jump");
            var impossible = new LayoutBodyProfile(original.Width, original.Height, original.OffsetX, original.OffsetY, 4, "actual-test-no-jump", noJump);
            Assert.That(Bind(world, impossible, arena.Player.spawn, out error), Is.False);
            Assert.That(error, Does.Contain("witness")); Assert.That(chapter.Profile, Is.SameAs(slower));
            Assert.That(chapter.Id, Is.EqualTo(identity)); Assert.That(chapter.ResolvedLayoutHash, Is.EqualTo(map));
            Assert.That(Bind(world, original, arena.Player.spawn, out error), Is.True, error);
        }

        [UnityTest] public IEnumerator AuthoredAndCapturedActualForestSourcesUseSameAssemblerWithoutMutatingLiveWorld()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var world = Forest(arena);
            var source = (IChapterLayoutProvider)world.GetType().GetField("layoutSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
            var catalog = (IChapterModuleCatalog)source;
            var supplied = source.Provide(new LayoutRequest("prototype-forest-strip", "1")); var original = supplied.Layout;
            var proof = (IChapterTraversalProof)source.GetType().GetMethod("CreateProof").Invoke(source, null);
            var profile = UnityChapterModules.ProfileFor(arena.Player); var playerAt = arena.Player.Position;
            long tick = arena.SimulationTickCount; int chunks = (int)world.GetType().GetProperty("ActiveCount").GetValue(world);
            var factory = new RejectingFactory();
            Assert.That(WorldAssembler.TryOpen(source, new LayoutRequest(original.Id, original.Version), profile, catalog, factory, out var first, out var error, proof), Is.True, error);
            using (first)
            {
                Assert.That(first.MovementProof.Succeeded, Is.True);
                Assert.That(first.ResolvedLayoutHash, Is.EqualTo(Chapter(world).ResolvedLayoutHash));
                Assert.That(first.LoadedModuleCount, Is.Zero);
            }
            // The provider result is captured without flattening its repeat
            // descriptor or changing source geometry/required content semantics.
            var captured = new CapturedProvider(original);
            Assert.That(WorldAssembler.TryOpen(captured, new LayoutRequest(original.Id, original.Version), profile, catalog, factory, out var second, out error, proof), Is.True, error);
            using (second) Assert.That(second.ResolvedLayoutHash, Is.EqualTo(Chapter(world).ResolvedLayoutHash));
            Assert.That(captured.Calls, Is.EqualTo(1)); Assert.That(factory.Calls, Is.Zero);
            Assert.That(arena.Player.Position, Is.EqualTo(playerAt)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
            Assert.That((int)world.GetType().GetProperty("ActiveCount").GetValue(world), Is.EqualTo(chunks));
        }
        [UnityTest] public IEnumerator ActualGameAndLabUseIdenticalCapturedSourceInputsAndCommonMotorProofTraces()
        {
            Type sourceType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if ((sourceType = assembly.GetType("Deviant.World.ForestLayoutSource")) != null) break;
            Assert.That(sourceType, Is.Not.Null);
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Deviant/World/Data/ForestIntegration.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deviant/World/Prefabs/ForestChunk.prefab");
            var chunk = prefab.GetComponent("InkForestChunk");
            string[] previousInputs = null, previousTraces = null;
            foreach (string scene in new[] { "Assets/Deviant/Game/Scenes/deviant.unity", "Assets/Deviant/Combat/Scenes/CombatV2Arena.unity" })
            {
                var open = Open(scene); while (open.MoveNext()) yield return open.Current;
                var arena = Object.FindAnyObjectByType<CombatArena>();
                var profile = UnityChapterModules.ProfileFor(arena.Player);
                var source = (IChapterLayoutProvider)Activator.CreateInstance(sourceType, data, chunk, profile, new LayoutPoint(4.6875f, -8.97f));
                var proof = (IChapterTraversalProof)sourceType.GetMethod("CreateProof").Invoke(source, null);
                var factory = new RejectingFactory();
                Assert.That(WorldAssembler.TryOpen(source, new LayoutRequest("prototype-forest-strip", "1"), profile,
                    (IChapterModuleCatalog)source, factory, out var session, out var error, proof), Is.True, error);
                using (session)
                {
                    var inputs = new string[session.MovementProof.Witnesses.Count]; var traces = new string[inputs.Length];
                    for (int index = 0; index < inputs.Length; index++)
                    { inputs[index] = session.MovementProof.Witnesses[index].InputHash; traces[index] = session.MovementProof.Witnesses[index].TraceHash; }
                    if (previousInputs != null) { Assert.That(inputs, Is.EqualTo(previousInputs)); Assert.That(traces, Is.EqualTo(previousTraces)); }
                    previousInputs = inputs; previousTraces = traces;
                }
                Assert.That(factory.Calls, Is.Zero);
                yield return new ExitPlayMode();
            }
        }

        [UnityTest] public IEnumerator SourceCollisionEditClosesWorldProofInsteadOfSilentlyUsingFallbackTerrain()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var world = Forest(arena); var chapter = Chapter(world);
            var data = (ScriptableObject)world.GetType().GetField("data").GetValue(world);
            var field = data.GetType().GetField("cells"); var cells = (Array)field.GetValue(data); var cell = cells.GetValue(0);
            var y = cell.GetType().GetField("y"); int previous = (int)y.GetValue(cell);
            var observers = world.GetType().GetField("LayoutEnded", BindingFlags.Instance | BindingFlags.NonPublic);
            var previousObservers = (Action)observers.GetValue(world);
            int notified = 0;
            Action failing = () => { notified++; throw new InvalidOperationException("fatal observer fixture"); };
            observers.SetValue(world, Delegate.Combine(failing, previousObservers));
            using (var driver = arena.AcquireManualSimulation())
            {
                try
                {
                    y.SetValue(cell, previous + 1);
                    var failure = Assert.Throws<TargetInvocationException>(() => Ensure(world, 768));
                    Assert.That(failure.InnerException, Is.TypeOf<AggregateException>());
                    Assert.That(notified, Is.EqualTo(1));
                    Assert.That(chapter.IsActive, Is.False); Assert.That(arena.enabled, Is.False);
                    long tick = arena.SimulationTickCount;
                    Assert.Throws<InvalidOperationException>(() => driver.Step(.02f));
                    Assert.Throws<InvalidOperationException>(() => driver.StepPending(.02f));
                    arena.SendMessage("FixedUpdate");
                    Assert.That(arena.SimulationTickCount, Is.EqualTo(tick));
                    Assert.That(Bind(world, UnityChapterModules.ProfileFor(arena.Player), arena.Player.spawn, out _), Is.False,
                        "Restoring/changing source never silently starts a replacement layout in the old world lifetime.");
                }
                finally { y.SetValue(cell, previous); observers.SetValue(world, previousObservers); }
            }
        }

        sealed class RejectingFactory : IChapterViewFactory
        {
            public int Calls;
            public IChapterView PrepareModule(LayoutModule module, string hash) { Calls++; throw new InvalidOperationException("Validation must not load a view."); }
            public IChapterView PreparePlacement(LayoutPlacement placement, LayoutPoint position) { Calls++; throw new InvalidOperationException("No entity content was supplied."); }
        }
        sealed class CapturedProvider : IChapterLayoutProvider
        {
            readonly ChapterLayout captured;
            public int Calls;
            public CapturedProvider(ChapterLayout captured) { this.captured = captured; }
            public LayoutSupply Provide(LayoutRequest request) { Calls++; return LayoutSupply.Success(captured); }
        }
        static IEnumerator Open(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; yield return null;
            Assert.That(Object.FindAnyObjectByType<CombatArena>().Player, Is.Not.Null);
        }
    }
}
