using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class RunInkSaveTests
    {
        [Test] public void TransitionDeletesLegacyProfileAndBackupButRetainsNewProfileOnReload()
        {
            string directory = Path.Combine(Path.GetTempPath(), "deviant-ink-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "profile.json");
            try
            {
                var legacy = ProfileData.CreateNew(Guid.NewGuid().ToString()); legacy.equipmentRevision = 0; legacy.pigment = 999;
                File.WriteAllText(path, JsonUtility.ToJson(legacy)); File.WriteAllText(path + ".bak", JsonUtility.ToJson(legacy));
                var store = new FileProfileStore(path, data => JsonUtility.ToJson(data), JsonUtility.FromJson<ProfileData>, 2);
                Assert.That(store.Load(_ => null, out string warning), Is.Null); Assert.That(warning, Is.Not.Null);
                Assert.That(File.Exists(path), Is.False); Assert.That(File.Exists(path + ".bak"), Is.False);
                var fresh = ProfileData.CreateNew(Guid.NewGuid().ToString()); fresh.pigment = 12; store.Save(fresh);
                Assert.That(store.Load(_ => null, out _)?.pigment, Is.EqualTo(12)); Assert.That(File.Exists(path), Is.True);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
