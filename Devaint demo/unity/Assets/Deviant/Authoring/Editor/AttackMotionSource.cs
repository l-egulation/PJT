using System;
using System.Diagnostics;
using System.IO;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class AttackMotionSource
    {
        public const string RelativePath = "data/combat/presentation/base_sword_attack1.json";
        public const string CatalogPath = "unity/Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json";
        public static AttackMotionDocument Load(string path, out string hash)
        {
            var document = JsonUtility.FromJson<AttackMotionDocument>(SourceFiles.Read(path, out hash, FrameMotionSource.MaximumFileBytes));
            _ = new FrameAttackMotionSource(document);
            return document;
        }
        public static string Save(string path, string expectedHash, AttackMotionDocument document)
        {
            var motion = new FixedAttackMotion(document);
            if (Load(path, out _).mode == FrameAttackMotionSource.FrameMode)
                throw new ArgumentException("FrameTime source is active. Its fixed media/art binding is read-only; edit and save in Frame Timing Preview.");
            return SourceFiles.Save(path, expectedHash, FrameMotionSource.Serialize(new FrameAttackMotionSource(motion.ToDocument())), FrameMotionSource.MaximumFileBytes);
        }
        public static string Regenerate(string repository, string python = "python")
        {
            var start = new ProcessStartInfo(python) {
                WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(Path.Combine(repository, "tools/combat/export_attack_motion.py"));
            using (var process = Process.Start(start))
            {
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(); throw new IOException("Motion generation timed out; source remains saved."); }
                string output = stdout.GetAwaiter().GetResult(), errors = stderr.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new IOException("Motion generation failed: " + errors + output);
                return output;
            }
        }
    }
}
