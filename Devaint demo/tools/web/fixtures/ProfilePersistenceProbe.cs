using System;
using System.IO;
using System.Runtime.InteropServices;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;

// Copied only into the isolated browser verification project, never the game build.
public sealed class ProfilePersistenceProbe : MonoBehaviour
{
    const string Unlock = "starting_gold_50";
    ProgressionService service;
    string error, path;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void ProfilePersistenceProbePublish(string json);
#endif
    [Serializable] sealed class State
    {
        public string profile_id, path, error;
        public long pigment, revision;
        public bool unlocked, story_seen, pending;
        public int runs_died;
    }
    void Start()
    {
        try
        {
            path = Path.Combine(Application.persistentDataPath, "browser-storage-fixture", "profile.json");
            Chapter1ProfileHost.UseStore(new FileProfileStore(path, d => JsonUtility.ToJson(d, true), JsonUtility.FromJson<ProfileData>));
            service = Chapter1ProfileHost.Service;
            error = service.LoadWarning;
        }
        catch (Exception e) { error = e.ToString(); }
    }
    void Update() => Publish();
    void Publish()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var p = service?.Snapshot;
        ProfilePersistenceProbePublish(JsonUtility.ToJson(new State { path = path, error = error ?? service?.LastSaveError,
            profile_id = p?.ProfileId, pigment = p?.Pigment ?? -1, revision = p?.Revision ?? -1,
            unlocked = p?.Has(Unlock) == true, story_seen = p?.HasSeen("browser-storage-fixture") == true,
            pending = service?.HasUnsavedPigment == true, runs_died = p?.RunsDied ?? -1 }));
#endif
    }
    void OnGUI()
    {
        float scale = Mathf.Max(1, Screen.height / 600f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUILayout.BeginArea(new Rect(24, 24, 700, 330));
        GUILayout.Label("Browser profile persistence fixture (synthetic data only)");
        GUILayout.Label("Profile: " + service?.Snapshot.ProfileId);
        GUILayout.Label("Pigment: " + service?.Snapshot.Pigment + "   Unlock: " + service?.Snapshot.Has(Unlock));
        if (service != null && GUILayout.Button("Save synthetic currency and unlock", GUILayout.Height(40)))
        {
            try
            {
                var run = Guid.NewGuid();
                for (int i = 0; i < 12; i++) service.AwardPigment(run, "fixture-" + i, "elite", false);
                if (!service.FlushPigment() || !service.SettleRun(run, RunOutcome.Died, false)) throw new IOException(service.LastSaveError);
                if (!service.Snapshot.Has(Unlock) && !service.TryPurchase(Unlock, out _)) throw new Exception("Synthetic purchase failed");
                service.MarkStorySeen("browser-storage-fixture");
            }
            catch (Exception e) { error = e.ToString(); }
        }
        if (service != null && GUILayout.Button("Award one synthetic normal kill (2 second autosave)", GUILayout.Height(40)))
            service.AwardPigment(Guid.NewGuid(), "fixture-periodic", "normal", false);
        GUILayout.Label("Pending pigment: " + service?.HasUnsavedPigment);
        GUILayout.Label(error ?? service?.LastSaveError ?? "No save error");
        GUILayout.EndArea();
    }
}
