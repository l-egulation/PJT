using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ChapterWorldIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesCommonLayoutPipelineAndPlayer() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesCommonLayoutPipelineAndPlayer() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var originalMovement = arena.Movement.ToData();
            var sources = new GameObject("ChapterAuthoringSources"); sources.SetActive(false);
            var floorSource = new GameObject("BoxModuleSource"); floorSource.transform.SetParent(sources.transform, false);
            var floor = floorSource.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var markerSource = new GameObject("MarkerSource"); markerSource.transform.SetParent(sources.transform, false);
            var root = new GameObject("IsolatedChapterFixture"); root.transform.position = new Vector3(0, 1000, 0);
            var host = root.AddComponent<ChapterWorldHost>();
            var modules = new Dictionary<string, GameObject> { { "floor-8", floorSource } };
            var markers = new Dictionary<string, GameObject> { { "marker", markerSource } };
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var authored = ChapterFixtures.Good(); var captured = new ChapterFixtures.CapturedProvider();
                    foreach (var provider in new IChapterLayoutProvider[] { new AuthoredLayoutProvider(authored), captured })
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                        var profile = UnityChapterModules.ProfileFor(player);
                        Assert.That(profile.Width, Is.EqualTo(player.size.x)); Assert.That(profile.MovementVersion, Is.EqualTo(arena.Movement.ContentHash));
                        Assert.That(host.TryOpen(provider, new LayoutRequest(authored.Id, authored.Version, 17), profile, modules, markers, out var error), Is.True, error);
                        var session = host.Session; string resolved = session.ResolvedLayoutHash;
                        var chapterCamera = new ChapterCameraSource(host, session.Id, "focus", new Vector3(1, 2, -10));
                        using (var framing = arena.CameraRequests.OpenScope("chapter anchor fixture"))
                        {
                            var request = framing.Request(10, chapterCamera);
                            var output = Camera.main.GetComponent<Deviant.Presentation.Unity.CameraOutputBinding>(); output.ApplyNow();
                            Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(13, 1003, -10)));
                            request.Complete(); output.ApplyNow();
                        }
                        Assert.That(host.LiveViewCount, Is.Zero, "Open validates without instantiating gameplay views.");
                        Assert.That(session.TryLoadModule("east", out error), Is.True, error); Assert.That(session.TryLoadModule("west", out error), Is.True, error);
                        Assert.That(host.LiveViewCount, Is.EqualTo(4));
                        var solid = root.GetComponentInChildren<CompositeCollider2D>();
                        Assert.That(solid, Is.Not.Null); Assert.That(solid.GetComponent<ArenaSurface>(), Is.Not.Null);
                        Assert.That(solid.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Static));
                        Assert.That(root.GetComponentsInChildren<BoxCollider2D>().All(c => c.compositeOperation == Collider2D.CompositeOperation.Merge), Is.True, "The common motor sees a continuous static surface across module seams.");
                        Assert.That(host.TryGetWorldAnchor(session.Id, session.Layout.EntryAnchorId, out var start, out var startKind), Is.True);
                        Assert.That(host.TryGetWorldAnchor(session.Id, "goal", out var goal, out _), Is.True);
                        Assert.That(startKind, Is.EqualTo(LayoutAnchorKind.Traversal));
                        Assert.That(host.TryGetWorldAnchor(Guid.NewGuid(), "goal", out _, out _), Is.False);
                        player.Position = start; player.velocity = Vector2.zero; player.grounded = false; player.SyncFacing(1); Physics2D.SyncTransforms();
                        float travelTime = (goal.x - start.x) / arena.Movement.RunSpeed;
                        int steps = Mathf.FloorToInt(travelTime / .02f);
                        float remainder = travelTime - steps * .02f;
                        for (int i = 0; i < steps; i++) driver.Step(.02f, new CombatInput(1));
                        if (remainder > .000001f) driver.Step(remainder, new CombatInput(1));
                        Assert.That(player.Position.x, Is.EqualTo(goal.x).Within(.04f)); Assert.That(player.Position.y, Is.EqualTo(start.y).Within(.04f)); Assert.That(player.grounded, Is.True);
                        for (int i = 0; i < steps; i++) driver.Step(.02f, new CombatInput(-1));
                        if (remainder > .000001f) driver.Step(remainder, new CombatInput(-1));
                        Assert.That(player.Position.x, Is.EqualTo(start.x).Within(.04f)); Assert.That(player.grounded, Is.True, "The seam is traversable in both directions by the shared motor.");
                        var movement = arena.Movement.ToData(); movement.runSpeed = 4;
                        Assert.That(arena.TryApplyMovementAndReset(movement, out error), Is.True, error);
                        Assert.That(host.TryRevalidate(UnityChapterModules.ProfileFor(player), out error), Is.True, error);
                        Assert.That(session.ResolvedLayoutHash, Is.EqualTo(resolved)); Assert.That(session.Layout, Is.SameAs(provider is AuthoredLayoutProvider ? authored : captured.Result));
                        Assert.That(session.TryRetirePlacement(session.Id, "marker-west", out error), Is.True, error);
                        Assert.That(session.TryUnloadModule("west", out error), Is.True, error); Assert.That(session.TryLoadModule("west", out error), Is.True, error);
                        Assert.That(host.LiveViewCount, Is.EqualTo(3)); Assert.That(session.State.ReadPresence("marker-west"), Is.EqualTo(PlacementPresence.Retired));
                        Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                        Assert.That(session.TryUnloadModule("east", out error), Is.True, error); Assert.That(session.TryLoadModule("east", out error), Is.True, error);
                        Assert.That(host.LiveViewCount, Is.EqualTo(3));
                        var oldId = session.Id; host.Close();
                        Assert.That(session.IsActive, Is.False); Assert.That(host.LiveViewCount, Is.Zero); Assert.That(root.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
                        Assert.That(session.TryRetirePlacement(oldId, "marker-east", out _), Is.False);
                        Assert.That(host.TryGetWorldAnchor(oldId, "goal", out _, out _), Is.False);
                        Assert.Throws<InvalidOperationException>(() => chapterCamera.Read(), "An ended chapter cannot silently reuse its old camera anchor.");
                        Assert.That(arena.TryApplyMovementAndReset(originalMovement, out error), Is.True, error);
                    }
                    Assert.That(captured.Calls, Is.EqualTo(1), "Movement revalidation does not invoke a generator/provider again.");
                    Assert.That(floor.size, Is.EqualTo(new Vector2(8, 1))); Assert.That(floor.offset, Is.EqualTo(new Vector2(4, -.5f)));
                    // The same real-source adapter rejects malformed geometry before creating any view.
                    var gap = ChapterFixtures.Copy(authored, modules: new[] { new LayoutModule("west", "floor-8", default), new LayoutModule("east", "floor-8", new LayoutPoint(9, 0)) });
                    Assert.That(host.TryOpen(new AuthoredLayoutProvider(gap), new LayoutRequest(gap.Id, gap.Version), UnityChapterModules.ProfileFor(player), modules, markers, out var gapError), Is.False, gapError);
                    Assert.That(host.Session, Is.Null); Assert.That(host.LiveViewCount, Is.Zero);
                    Assert.That(host.TryOpen(new AuthoredLayoutProvider(authored), new LayoutRequest(authored.Id, authored.Version), UnityChapterModules.ProfileFor(player), modules, markers, out var openError), Is.True, openError);
                    var before = host.Session; string frozen = before.ResolvedLayoutHash;
                    floor.size = new Vector2(9, 1); // Simulate a changed authoring prefab after validation.
                    Assert.That(before.TryLoadModule("west", out _), Is.False); Assert.That(host.LiveViewCount, Is.Zero);
                    Assert.That(host.TryRevalidate(UnityChapterModules.ProfileFor(player), out _), Is.False);
                    host.Close();
                    Assert.That(host.TryOpen(new AuthoredLayoutProvider(authored), new LayoutRequest(authored.Id, authored.Version, null, frozen), UnityChapterModules.ProfileFor(player), modules, markers, out _), Is.False);
                    floor.size = new Vector2(8, 1);
                    Assert.That(host.TryOpen(new AuthoredLayoutProvider(authored), new LayoutRequest(authored.Id, authored.Version, null, frozen), UnityChapterModules.ProfileFor(player), modules, markers, out openError), Is.True, openError);
                    var last = host.Session; Assert.That(last.TryLoadModule("west", out openError), Is.True, openError);
                    Object.Destroy(root); yield return null;
                    Assert.That(last.IsActive, Is.False, "Destroying the composition host ends its chapter session.");
                    Assert.That(last.LoadedModuleCount, Is.Zero);
                    Assert.That(last.TryGetAnchor(last.Id, "entry", out _, out _), Is.False);
                }
            }
            finally
            {
                if (host) host.Close(); if (root) Object.Destroy(root); Object.Destroy(sources);
                arena.TryApplyMovementAndReset(originalMovement, out _);
            }
        }
    }
    public class ChapterModuleAdapterTests
    {
        [TestCase("rotation")] [TestCase("scale")] [TestCase("trigger")] [TestCase("one-way")]
        [TestCase("rounded")] [TestCase("auto-tiling")] [TestCase("body")] [TestCase("animation")]
        public void UnsupportedAuthoringGeometryFailsBeforeAnyViewExists(string fault)
        {
            var source = new GameObject("UnsupportedModuleSource"); source.SetActive(false);
            var root = new GameObject("ChapterValidationOnly");
            try
            {
                var box = source.AddComponent<BoxCollider2D>(); box.size = new Vector2(8, 1); box.offset = new Vector2(4, -.5f);
                switch (fault)
                {
                    case "rotation": source.transform.localRotation = Quaternion.Euler(0, 0, 10); break;
                    case "scale": source.transform.localScale = new Vector3(2, 1, 1); break;
                    case "trigger": box.isTrigger = true; break;
                    case "one-way": source.AddComponent<ArenaSurface>().oneWay = true; break;
                    case "rounded": box.edgeRadius = .1f; break;
                    case "auto-tiling": box.autoTiling = true; break;
                    case "body": source.AddComponent<Rigidbody2D>(); break;
                    case "animation": source.AddComponent<Animator>(); break;
                }
                using (var binding = new UnityChapterModules(root.transform, new Dictionary<string, GameObject> { { "source", source } }, new Dictionary<string, GameObject>()))
                {
                    Assert.That(binding.TryGetGeometry("source", out _, out var error), Is.False); Assert.That(error, Is.Not.Empty);
                    Assert.That(binding.LiveViewCount, Is.Zero); Assert.That(root.transform.childCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(root); }
        }
    }
}
