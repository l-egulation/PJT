using System;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Media.Tests
{
    public sealed class CompressedAudioIntegrationTests
    {
        [Test] public void RegisteredPayloadsAndSelectedIdDecodeThroughResources()
        {
            var data = Resources.Load<TextAsset>("CompressedAudio/Catalog");
            Assert.That(data, Is.Not.Null, "Restore the pinned media deployment before integration tests.");
            var catalog = JsonUtility.FromJson<CompressedAudioResources.Catalog>(data.text);
            Assert.That(catalog.schema, Is.EqualTo(1));
            Assert.That(catalog.entries.Length, Is.GreaterThan(0));
            CompressedAudioResources.Entry selected = null;
            foreach (var entry in catalog.entries)
            {
                var payload = Resources.Load<TextAsset>(entry.payload);
                Assert.That(payload, Is.Not.Null, entry.payload);
                Assert.That(payload.bytes.Length, Is.GreaterThan(47));
                if (selected == null && !string.IsNullOrEmpty(entry.id)) selected = entry;
            }
            Assert.That(selected, Is.Not.Null);
            var clip = CompressedAudioResources.LoadSelected(selected.id);
            Assert.That(clip, Is.Not.Null, selected.id);
            Assert.That(clip.samples, Is.GreaterThan(0));
            Assert.That(clip.frequency, Is.EqualTo(48000));
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(CompressedAudioResources.LoadSelected(selected.id), Is.SameAs(clip), "decoded clips must be reused");
        }
    }
}
