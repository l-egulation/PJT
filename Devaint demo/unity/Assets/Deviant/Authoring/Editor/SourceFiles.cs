using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Deviant.Authoring
{
    public static class SourceFiles
    {
        public static string Read(string path, out string fileHash, int maximumBytes = int.MaxValue)
        {
            byte[] bytes = ReadBytes(path, maximumBytes);
            fileHash = Hash(bytes);
            return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        }

        public static string Create(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            Replace(path, bytes, () => { }, true, true);
            return Hash(bytes);
        }

        public static string Save(string path, string expectedHash, string text, int maximumBytes = int.MaxValue)
        {
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            if (bytes.Length > maximumBytes) throw new IOException("Source exceeds the allowed file size.");
            CheckUnchanged(path, expectedHash, maximumBytes);
            Replace(path, bytes, () => CheckUnchanged(path, expectedHash, maximumBytes), false);
            return Hash(bytes);
        }

        public static void Copy(string source, string expectedHash, string destination)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Source and generated paths must differ.");
            CheckUnchanged(source, expectedHash);
            byte[] bytes = File.ReadAllBytes(source);
            if (Hash(bytes) != expectedHash) throw new IOException("Source changed during export. Reload first.");
            Replace(destination, bytes, () => CheckUnchanged(source, expectedHash), true);
        }

        static void Replace(string path, byte[] bytes, Action beforeReplace, bool allowCreate, bool createOnly = false)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                beforeReplace();
                if (createOnly) File.Move(temporary, path); // Atomic no-overwrite creation, including a concurrent creator.
                else if (!allowCreate || File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static void CheckUnchanged(string path, string expected, int maximumBytes = int.MaxValue)
        {
            if (string.IsNullOrEmpty(expected) || !File.Exists(path) || Hash(ReadBytes(path, maximumBytes)) != expected)
                throw new IOException("Source changed outside this window. Reload before saving; no file was overwritten.");
        }

        static byte[] ReadBytes(string path, int maximumBytes)
        {
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var buffer = new MemoryStream())
            {
                if (file.Length > maximumBytes) throw new IOException("Source exceeds the allowed file size.");
                var block = new byte[8192]; int count;
                while ((count = file.Read(block, 0, block.Length)) > 0)
                {
                    if (buffer.Length > maximumBytes - count) throw new IOException("Source exceeds the allowed file size.");
                    buffer.Write(block, 0, count);
                }
                return buffer.ToArray();
            }
        }

        static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
