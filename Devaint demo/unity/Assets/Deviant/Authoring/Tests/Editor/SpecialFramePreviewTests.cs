using System.IO;
using Deviant.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class SpecialFramePreviewTests
    {
        [Test] public void CompleteSourceLocalBodySamplingNeverConstructsRuntimeOrMutatesCanonicalInputs()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, SpecialFrameSource.RelativePath))) directory = directory.Parent;
            Assert.That(directory, Is.Not.Null); string repository = directory.FullName;
            string source = Path.Combine(repository, SpecialFrameSource.RelativePath); byte[] originalSource = File.ReadAllBytes(source);
            int actorCount = UnityEngine.Object.FindObjectsByType<CombatActor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int castCount = UnityEngine.Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var catalog = SpecialFrameSource.Load(repository, out var hash); string content = catalog.ContentHash;
            foreach (var action in catalog.Actions)
            {
                var timing = action.CompileMotion();
                for (int index = 99; index >= 0; index--)
                {
                    float seconds = action.Execution.End * index / 100;
                    int frame = action.BodyFrameAt(seconds, timing); Assert.That(frame, Is.InRange(0, timing.Frames.Count - 1));
                }
            }
            Assert.That(catalog.ContentHash, Is.EqualTo(content)); Assert.That(File.ReadAllBytes(source), Is.EqualTo(originalSource));
            Assert.That(SpecialFrameSource.Load(repository, out var afterHash).ContentHash, Is.EqualTo(content)); Assert.That(afterHash, Is.EqualTo(hash));
            Assert.That(UnityEngine.Object.FindObjectsByType<CombatActor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(actorCount));
            Assert.That(UnityEngine.Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(castCount));
        }
    }
}
