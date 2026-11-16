using System;
using System.IO;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Owns the one ProgressionService of the process and its save file. Builds keep the profile under
    // persistentDataPath/profile; Editor play sessions use a separate editor/ folder so development never touches a
    // player's progress. Tests replace the store before first use.
    public sealed class Chapter1ProfileHost : MonoBehaviour
    {
        const float FlushInterval = 2f;
        static ProgressionService service;
        static IProfileStore overrideStore;
        static Chapter1ProfileHost instance;
        float nextFlush;

        public static string ProfilePath => Path.Combine(Application.persistentDataPath, Application.isEditor ? "profile/editor" : "profile", "profile.json");

        public static ProgressionService Service
        {
            get
            {
                if (service != null) return service;
                var text = Resources.Load<TextAsset>("RogueliteProgression");
                if (!text) throw new InvalidOperationException("RogueliteProgression resource is missing; run tools/combat/export_roguelite_progression.py.");
                var catalog = new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(text.text));
                var store = overrideStore ?? new FileProfileStore(ProfilePath, data => JsonUtility.ToJson(data, true), JsonUtility.FromJson<ProfileData>, equipmentRevision: 2);
                service = new ProgressionService(catalog, store);
                if (service.LoadWarning != null) Debug.LogWarning("Profile: " + service.LoadWarning);
                EnsureFlusher();
                return service;
            }
        }

        // Test and training hook: the next Service uses this store instead of the real file.
        public static void UseStore(IProfileStore store) { overrideStore = store; service = null; }

        // A development session can visit any shared GUI without touching the normal profile.
        // Restore the exact previous in-memory owner too, including any pending pigment.
        public static IDisposable UseTemporaryStore(IProfileStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var lease = new TemporaryStore(overrideStore, service);
            UseStore(store);
            return lease;
        }
        sealed class TemporaryStore : IDisposable
        {
            readonly IProfileStore previousStore;
            readonly ProgressionService previousService;
            bool disposed;
            public TemporaryStore(IProfileStore store, ProgressionService current) { previousStore = store; previousService = current; }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; overrideStore = previousStore; service = previousService;
            }
        }

        static void EnsureFlusher()
        {
            if (instance || !Application.isPlaying) return;
            var go = new GameObject("Profile host");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Chapter1ProfileHost>();
        }

        // Pigment from kills is held in memory and written in batches; quitting writes what is left.
        void Update()
        {
            if (service == null || Time.unscaledTime < nextFlush) return;
            nextFlush = Time.unscaledTime + FlushInterval;
            if (service.HasUnsavedPigment && !service.FlushPigment()) Debug.LogWarning("Profile: pigment save failed, will retry: " + service.LastSaveError);
        }
        void FlushPendingPigment()
        {
            if (service != null && service.HasUnsavedPigment && !service.FlushPigment())
                Debug.LogWarning("Profile: pigment save failed, will retry: " + service.LastSaveError);
        }
        void OnApplicationPause(bool paused) { if (paused) FlushPendingPigment(); }
        void OnApplicationFocus(bool focused) { if (!focused) FlushPendingPigment(); }
        void OnApplicationQuit() { FlushPendingPigment(); }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
