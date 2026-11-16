using System;
using System.Diagnostics;
using System.IO;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class BasicSwordSource
    {
        public const string ActionId = "base_sword_basic_0";
        public static BasicMeleeDocument Load(string path, out string hash)
        {
            var document = JsonUtility.FromJson<BasicMeleeDocument>(SourceFiles.Read(path, out hash));
            Validate(document);
            return document;
        }
        public static BasicMeleeDefinition Validate(BasicMeleeDocument document)
        {
            if (document == null || document.schemaVersion != 1 || document.id != ActionId)
                throw new ArgumentException("Expected schemaVersion 1 and base_sword_basic_0.");
            if (!BasicMeleeDefinition.TryCreate(document.id, document.hit?.ToData(), out var definition, out var error))
                throw new ArgumentException(error);
            return definition;
        }
        public static string Save(string repository, string expectedHash, BasicMeleeDocument draft)
        {
            var definition = Validate(draft);
            string path = Path.Combine(repository, FrameMotionSource.ActionPath);
            var current = Load(path, out _); var numeric = Validate(current);
            if (numeric.Impact != definition.Impact || numeric.Active != definition.Active || numeric.Duration != definition.Duration)
            {
                if (MeleeFrameSource.HasFrameOverride(repository, ActionId)) throw new ArgumentException("The delegated first FrameTime source owns contact/active/end markers; numeric timing is read-only until explicit fixed return.");
            }
            var snapshot = new BasicMeleeDocument { schemaVersion = 1, id = definition.Id, hit = BasicMeleeHitData.From(definition) };
            return SourceFiles.Save(path, expectedHash, JsonUtility.ToJson(snapshot, true) + "\n");
        }

        public static string Regenerate(string repository, string python = "python")
        {
            string output = "";
            foreach (string script in new[] { "build_melee_design.py", "export_unity_catalog.py" })
            {
                var start = new ProcessStartInfo(python) {
                    WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.ArgumentList.Add(Path.Combine(repository, "tools/combat", script));
                using (var process = Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(30000))
                    { process.Kill(); throw new IOException(script + " timed out; source remains saved. Regenerate again."); }
                    output += stdout.GetAwaiter().GetResult();
                    string errors = stderr.GetAwaiter().GetResult();
                    if (process.ExitCode != 0) throw new IOException(script + " failed: " + errors + output);
                }
            }
            return output;
        }
    }
}
