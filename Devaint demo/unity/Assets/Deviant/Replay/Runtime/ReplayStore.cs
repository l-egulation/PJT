using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Deviant.Replay
{
    public sealed class ReplayFile
    {
        public string Path, Sha256;
        public ReplayChunk Chunk;
        public int CompressedBytes, UncompressedBytes;
    }
    public sealed class ReplayStore
    {
        public string Root { get; }
        public ReplayStore(string root) { Root = System.IO.Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root))); Directory.CreateDirectory(Root); }
        public ReplayFile Commit(ReplayHeader header, IList<ReplayRecord> records, ReplayUploadBinding binding = null)
        {
            string bindingJson = null;
            if (binding != null)
            {
                bindingJson = binding.Encode();
                if (binding.session.session_id != header.SessionId || binding.session.build_id != header.BuildId) throw new ArgumentException("Collection binding disagrees with replay.");
                string existingBinding = System.IO.Path.Combine(Root, header.SessionId, "collection.json");
                if (File.Exists(existingBinding) && File.ReadAllText(existingBinding) != bindingJson) throw new InvalidDataException("A recorded session cannot change player or collector.");
            }
            var packed = ReplayCodec.Encode(header, records, out int rawBytes);
            var checkedChunk = ReplayCodec.Decode(packed);
            string directory = System.IO.Path.Combine(Root, header.SessionId, header.SegmentId); Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, header.ChunkIndex.ToString("D6") + ".replay.gz.ready");
            string hash = Hash(packed);
            if (File.Exists(path)) { if (Hash(File.ReadAllBytes(path)) != hash) throw new IOException("Replay index conflict."); }
            else { WriteAtomic(path, packed); }
            var result = new ReplayFile { Path = path, Sha256 = hash, Chunk = checkedChunk, CompressedBytes = packed.Length, UncompressedBytes = rawBytes };
            SaveReceipt(result);
            if (bindingJson != null)
            {
                string sessionBinding = System.IO.Path.Combine(Root, header.SessionId, "collection.json");
                if (File.Exists(sessionBinding) && File.ReadAllText(sessionBinding) != bindingJson) throw new InvalidDataException("A recorded session cannot change player or collector.");
                WriteAtomic(sessionBinding, Encoding.UTF8.GetBytes(bindingJson));
                WriteAtomic(path + ".collection.json", Encoding.UTF8.GetBytes(bindingJson));
            }
            Trim(256L * 1024 * 1024, TimeSpan.FromDays(7), DateTime.UtcNow); return result;
        }
        public IReadOnlyList<ReplayFile> Recover(Action<string> rejected = null)
        {
            var files = new List<ReplayFile>();
            foreach (string path in Directory.EnumerateFiles(Root, "*.replay.gz.ready", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(path); if (info.Length > ReplayCodec.MaxCompressed) throw new InvalidDataException("File size.");
                    var packed = File.ReadAllBytes(path); var chunk = ReplayCodec.Decode(packed);
                    string expected = System.IO.Path.Combine(Root, chunk.Header.SessionId, chunk.Header.SegmentId, chunk.Header.ChunkIndex.ToString("D6") + ".replay.gz.ready");
                    if (System.IO.Path.GetFullPath(path) != expected) throw new InvalidDataException("File identity/path mismatch.");
                    var file = new ReplayFile { Path = path, Sha256 = Hash(packed), Chunk = chunk, CompressedBytes = packed.Length, UncompressedBytes = chunk.RawBytes };
                    SaveReceipt(file);
                    // Inventory recovery retains metadata, not every expanded chunk in memory.
                    file.Chunk = new ReplayChunk(chunk.Header, new[] { chunk.Records[chunk.Records.Count - 1] }) { RawBytes = chunk.RawBytes };
                    files.Add(file);
                }
                catch (Exception e) when (e is IOException || e is FormatException || e is ArgumentException || e is OverflowException) { rejected?.Invoke(System.IO.Path.GetFileName(path)); }
            }
            files.Sort((a, b) => { int c = string.CompareOrdinal(a.Chunk.Header.SessionId, b.Chunk.Header.SessionId); if (c == 0) c = a.Chunk.Header.FirstSequence.CompareTo(b.Chunk.Header.FirstSequence); return c; });
            return files.AsReadOnly();
        }
        public void Acknowledge(string path, string committedSha256)
        {
            path = System.IO.Path.GetFullPath(path);
            if (!path.StartsWith(Root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || !path.EndsWith(".replay.gz.ready", StringComparison.Ordinal)) throw new ArgumentException("Replay path.");
            if (Hash(File.ReadAllBytes(path)) != committedSha256) throw new InvalidDataException("Commit hash mismatch.");
            File.Delete(path); if (File.Exists(path + ".json")) File.Delete(path + ".json");
            if (File.Exists(path + ".collection.json")) File.Delete(path + ".collection.json");
            if (File.Exists(path + ".upload-error")) File.Delete(path + ".upload-error");
        }
        public void Trim(long byteLimit, TimeSpan maxAge, DateTime nowUtc)
        {
            var files = new List<FileInfo>(); long total = 0;
            foreach (string p in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            { if (p.EndsWith(".tmp", StringComparison.Ordinal)) { File.Delete(p); continue; } if (p.EndsWith(".ready", StringComparison.Ordinal)) { try { var f = new FileInfo(p); long size = f.Length; files.Add(f); total += size; } catch (FileNotFoundException) { /* uploader acknowledged concurrently */ } } }
            files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            foreach (var f in files) if (total > byteLimit || nowUtc - f.LastWriteTimeUtc > maxAge)
            {
                // Metadata survives to make missing chunks explicit instead of silently joining their neighbours.
                WriteAtomic(f.FullName + ".evicted", Encoding.ASCII.GetBytes("local_eviction"));
                total -= f.Length; f.Delete(); if (File.Exists(f.FullName + ".json")) File.Delete(f.FullName + ".json");
                if (File.Exists(f.FullName + ".collection.json")) File.Delete(f.FullName + ".collection.json");
                if (File.Exists(f.FullName + ".upload-error")) File.Delete(f.FullName + ".upload-error");
            }
            foreach (string directory in Directory.GetDirectories(Root))
            {
                DateTime latest = Directory.GetLastWriteTimeUtc(directory);
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    if (File.GetLastWriteTimeUtc(path) > latest) latest = File.GetLastWriteTimeUtc(path);
                if (nowUtc - latest > maxAge) Directory.Delete(directory, true);
            }
        }
        public void DeleteAll() { if (Directory.Exists(Root)) Directory.Delete(Root, true); Directory.CreateDirectory(Root); }
        public void RevokePendingUploads()
        {
            foreach (string path in Directory.EnumerateFiles(Root, "*.collection.json", SearchOption.AllDirectories)) File.Delete(path);
            foreach (string path in Directory.EnumerateFiles(Root, "collection.json", SearchOption.AllDirectories))
            { File.Delete(path); if (File.Exists(path + ".upload-error")) File.Delete(path + ".upload-error"); }
        }
        public void MarkSession(string session, long elapsedMs, long lastSequence, bool ended, bool truncated)
        {
            if (!Guid.TryParseExact(session, "N", out _) || elapsedMs < 0 || lastSequence < 0) throw new ArgumentException("Session status.");
            string directory = System.IO.Path.Combine(Root, session); Directory.CreateDirectory(directory);
            string json = "{\"session\":\"" + session + "\",\"last_ms\":" + elapsedMs + ",\"last_seq\":" + lastSequence +
                ",\"ended\":" + (ended ? "true" : "false") + ",\"truncated\":" + (truncated ? "true" : "false") + "}";
            WriteAtomic(System.IO.Path.Combine(directory, "session.json"), Encoding.UTF8.GetBytes(json));
        }
        static void SaveReceipt(ReplayFile file)
        {
            var h = file.Chunk.Header; var tail = file.Chunk.Records[file.Chunk.Records.Count - 1];
            string json = "{\"sha256\":\"" + file.Sha256 + "\",\"compressed_bytes\":" + file.CompressedBytes + ",\"uncompressed_bytes\":" + file.UncompressedBytes + ",\"end_ms\":" + tail.TimeMs + ",\"last_seq\":" + tail.Sequence + ",\"header\":" + h.Encode() + "}";
            WriteAtomic(file.Path + ".json", Encoding.UTF8.GetBytes(json));
        }
        internal static void WriteAtomic(string path, byte[] data)
        {
            string temp = path + ".tmp";
            using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { f.Write(data, 0, data.Length); f.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
