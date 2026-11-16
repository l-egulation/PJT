using System;
using System.Linq;
using System.Text;
using Deviant.Combat.Contracts;
using Deviant.CombatLab;
using Deviant.CombatLab.Contracts;
using UnityEngine;

namespace Deviant.Authoring
{
    // Explicit Editor input export, not product save data. Both fixed scenario
    // segments must validate before the caller replaces its in-memory selection.
    public static class SwordDashInputSource
    {
        public const int SchemaVersion = 3;
        public const int MaximumFileBytes = 1024 * 1024;
        [Serializable] sealed class FileData { public int schemaVersion; public RecordingData attack; public RecordingData wall; }
        [Serializable] sealed class RecordingData
        {
            public string scenarioId;
            public float stepSeconds;
            public string contentHash;
            public SampleData[] inputs;
        }
        [Serializable] sealed class SampleData { public int axis; public int pressed; public bool specialHeld; public int released; public int climbAxis, heldSpecials; }
        public static SwordDashInputSet Load(string path, out string fileHash) => Deserialize(SourceFiles.Read(path, out fileHash, MaximumFileBytes));
        public static string Save(string path, string expectedFileHash, SwordDashInputSet inputs)
        {
            string json = Serialize(inputs);
            return expectedFileHash == null ? SourceFiles.Create(path, json) : SourceFiles.Save(path, expectedFileHash, json, MaximumFileBytes);
        }
        public static string Serialize(SwordDashInputSet inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            string json = JsonUtility.ToJson(new FileData { schemaVersion = SchemaVersion, attack = Encode(inputs.Attack), wall = Encode(inputs.Wall) }, true) + "\n";
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Input export exceeds the allowed file size.");
            return json;
        }
        public static SwordDashInputSet Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("An input recording document is required.", nameof(json));
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) throw new ArgumentException("Input document exceeds the allowed file size.", nameof(json));
            var data = JsonUtility.FromJson<FileData>(json);
            if (data == null || data.schemaVersion != SchemaVersion) throw new ArgumentException("Unsupported input recording schema.", nameof(json));
            return new SwordDashInputSet(Decode(data.attack, SwordDashInputSet.AttackTicks), Decode(data.wall, SwordDashInputSet.WallTicks));
        }
        static RecordingData Encode(CombatInputRecording recording) => new RecordingData {
            scenarioId = recording.ScenarioId, stepSeconds = recording.StepSeconds, contentHash = recording.ContentHash,
            inputs = recording.Inputs.Select(input => new SampleData { axis = input.Axis, pressed = (int)input.Pressed, specialHeld = input.SpecialHeld, released = (int)input.Released, climbAxis = input.ClimbAxis, heldSpecials = (int)input.HeldSpecials }).ToArray()
        };
        static CombatInputRecording Decode(RecordingData data, int expectedCount)
        {
            if (data == null || data.inputs == null || data.inputs.Length != expectedCount || data.inputs.Any(input => input == null))
                throw new ArgumentException("Input recording has missing or unsupported tick samples.");
            var recording = new CombatInputRecording(data.scenarioId, data.stepSeconds,
                data.inputs.Select(input => new CombatInput(input.axis, (CombatButtons)input.pressed, input.specialHeld, (CombatButtons)input.released, input.climbAxis, (CombatButtons)input.heldSpecials)).ToArray());
            if (!string.Equals(recording.ContentHash, data.contentHash, StringComparison.Ordinal))
                throw new ArgumentException("Input recording content hash does not match its tick samples.");
            return recording;
        }
    }
}
