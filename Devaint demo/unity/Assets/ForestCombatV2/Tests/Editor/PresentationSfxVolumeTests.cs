using System;
using NUnit.Framework;
using UnityEngine;

namespace ForestCombatV2.Tests
{
    // Item acquire/pickup sounds play at half the usual 0.8 (user: too loud, 2026-11-16).
    public sealed class PresentationSfxVolumeTests
    {
        [TestCase("altar-acquire", .4f)]
        [TestCase("gold-pickup", .4f)]
        [TestCase("pigment-pickup", .4f)]
        [TestCase("unlock", .8f)]
        public void AcquireSoundsArePlayedAtHalfGain(string id, float volume)
        {
            var catalog = Resources.Load<PresentationSfx>("PresentationSfx");
            Assert.That(catalog, Is.Not.Null);
            var entry = Array.Find(catalog.entries, e => e.id == id);
            Assert.That(entry, Is.Not.Null, id);
            Assert.That(entry.volume, Is.EqualTo(volume).Within(.001f), id);
        }
    }
}
