using System;
using System.IO;

namespace Deviant.Run
{
    public interface IProfileStore
    {
        // Returns the last valid profile, or null when none exists. Corrupt files are reported, never overwritten here.
        ProfileData Load(Func<ProfileData, string> validate, out string warning);
        // Throws when the profile could not be durably written; the previous valid file is kept.
        void Save(ProfileData data);
        // Replace play data, including the recovery backup, with a fresh profile.
        void Reset(ProfileData data);
    }

    // Serialisation is injected so this assembly stays free of UnityEngine (the Unity host passes JsonUtility).
    public sealed class FileProfileStore : IProfileStore
    {
        readonly string path;
        readonly Func<ProfileData, string> write;
        readonly Func<string, ProfileData> read;
        readonly int equipmentRevision;
        public string Path => path;
        public string BackupPath => path + ".bak";

        public FileProfileStore(string path, Func<ProfileData, string> write, Func<string, ProfileData> read, int equipmentRevision = 0)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            this.path = path; this.write = write ?? throw new ArgumentNullException(nameof(write)); this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.equipmentRevision = equipmentRevision;
        }

        public ProfileData Load(Func<ProfileData, string> validate, out string warning)
        {
            warning = null;
            foreach (var candidate in new[] { path, BackupPath })
            {
                if (!File.Exists(candidate)) continue;
                string problem;
                try
                {
                    var data = read(File.ReadAllText(candidate));
                    if (equipmentRevision > 0 && data != null && data.format == ProfileData.CurrentFormat && data.equipmentRevision < equipmentRevision)
                    {
                        // Approved one-time transition: remove both legacy copies so fallback cannot restore old unlocks.
                        File.Delete(path); File.Delete(BackupPath);
                        warning = "기존 플레이 데이터를 초기화했습니다. 새 무기·잉크 규칙으로 시작합니다.";
                        return null;
                    }
                    problem = validate(data);
                    if (problem == null)
                    {
                        if (candidate != path) warning = (warning == null ? "" : warning + " ") + "Recovered the profile from its backup.";
                        return data;
                    }
                }
                catch (Exception error) { problem = error.Message; }
                warning = (warning == null ? "" : warning + " ") + System.IO.Path.GetFileName(candidate) + " is unreadable: " + problem;
            }
            return null;
        }

        // temp file -> read back and compare -> atomic replace, keeping the previous file as the backup.
        public void Save(ProfileData data) => Write(data, false);
        public void Reset(ProfileData data) => Write(data, true);

        void Write(ProfileData data, bool reset)
        {
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string text = write(data), temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.ReadAllText(temp) != text) { File.Delete(temp); throw new IOException("Profile write verification failed."); }
            if (reset) File.Copy(temp, BackupPath, true);
            if (File.Exists(path)) File.Replace(temp, path, reset ? null : BackupPath, true);
            else File.Move(temp, path);
        }
    }

    // In-memory store for training, tests and development fixtures so they never touch the real profile.
    public sealed class MemoryProfileStore : IProfileStore
    {
        ProfileData saved;
        public bool FailNextSave { get; set; }
        public int Saves { get; private set; }
        public MemoryProfileStore(ProfileData initial = null) { saved = initial?.Clone(); }
        public ProfileData Load(Func<ProfileData, string> validate, out string warning)
        { warning = null; if (saved == null) return null; var problem = validate(saved); if (problem != null) { warning = problem; return null; } return saved.Clone(); }
        public void Reset(ProfileData data) => Save(data);
        public void Save(ProfileData data)
        {
            if (FailNextSave) { FailNextSave = false; throw new IOException("Simulated save failure."); }
            saved = data.Clone(); Saves++;
        }
    }
}
