using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Deviant.Definitions
{
    public sealed class PrefabCueDefinition : ICueDefinition
    {
        public string Id { get; }
        public string PrefabKey { get; }
        public float Duration { get; }
        public bool Loop => false;
        public string ContentHash { get; }
        public PrefabCueDefinition(string id, string prefabKey, float duration, string bindingVersion)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(prefabKey) || string.IsNullOrWhiteSpace(bindingVersion))
                throw new ArgumentException("Stable cue, prefab and binding version are required.");
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            Id = id; PrefabKey = prefabKey; Duration = duration;
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                "prefab-cue-v1|" + id + "|" + prefabKey + "|" + duration.ToString("R", CultureInfo.InvariantCulture) + "|" + bindingVersion))).Replace("-", "").ToLowerInvariant();
        }
    }
}
