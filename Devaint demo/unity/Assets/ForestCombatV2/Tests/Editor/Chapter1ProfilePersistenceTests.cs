using System;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1ProfilePersistenceTests
    {
        [TestCase("OnApplicationFocus", false, true)]
        [TestCase("OnApplicationPause", true, true)]
        [TestCase("OnApplicationFocus", true, false)]
        [TestCase("OnApplicationPause", false, false)]
        public void LeavingTheAppFlushesPendingPigmentWithoutReplayConsent(string callback, bool value, bool flush)
        {
            var store = new MemoryProfileStore();
            using (Chapter1ProfileHost.UseTemporaryStore(store))
            {
                var service = Chapter1ProfileHost.Service;
                int before = store.Saves;
                int amount = service.AwardPigment(Guid.NewGuid(), "browser-lifecycle-fixture", "normal", false);
                Assert.That(amount, Is.GreaterThan(0));
                Assert.That(service.HasUnsavedPigment, Is.True);
                var go = new GameObject("Profile lifecycle test");
                try
                {
                    var host = go.AddComponent<Chapter1ProfileHost>();
                    typeof(Chapter1ProfileHost).GetMethod(callback, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host, new object[] { value });
                    Assert.That(store.Saves, Is.EqualTo(before + (flush ? 1 : 0)));
                    Assert.That(service.HasUnsavedPigment, Is.EqualTo(!flush));
                    if (flush)
                        Assert.That(new ProgressionService(service.Catalog, store).Snapshot.Pigment, Is.EqualTo(amount));
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }
    }
}
