using System;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace Deviant.Game
{
    // Run before profile hosts, settings, and replay uploaders read any saved data.
    public static class WebBuildStorageReset
    {
        // Fixed reset cutoff: 2026-11-16 14:00:00 KST. Change only for an intentional save reset.
        public static readonly DateTimeOffset ResetBeforeUtc = new DateTimeOffset(2026, 11, 16, 5, 0, 0, TimeSpan.Zero);
        const string MarkerName = ".deviant-web-reset-utc";

#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            ResetIfOlder(Application.persistentDataPath, ResetBeforeUtc, () =>
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            });
        }
#endif

        // The callback keeps tests away from the developer's actual PlayerPrefs.
        public static bool ResetIfOlder(string root, DateTimeOffset resetBeforeUtc, Action clearPreferences)
        {
            if (clearPreferences == null) throw new ArgumentNullException(nameof(clearPreferences));
            string marker = Path.Combine(root, MarkerName);
            if (File.Exists(marker) && DateTimeOffset.TryParseExact(File.ReadAllText(marker), "O",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var completedCutoff) && completedCutoff >= resetBeforeUtc)
                return false;

            Directory.CreateDirectory(root);
            // Keep the IDBFS mount itself, but remove all old files (including profile backups and replay queues).
            foreach (string directory in Directory.GetDirectories(root)) Directory.Delete(directory, true);
            foreach (string file in Directory.GetFiles(root)) File.Delete(file);
            clearPreferences();
            // Record the completed cutoff, not the device clock or build time. Rebuilds and clock skew cannot trigger a reset.
            // Write last: a failed reset must be retried on the next launch.
            File.WriteAllText(marker, resetBeforeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            return true;
        }
    }
}
