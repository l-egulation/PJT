using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Threading.Tasks;
#endif
using UnityEngine;

[assembly: InternalsVisibleTo("Deviant.Replay.Tests")]

namespace Deviant.Replay.Unity
{
    // Only immutable queue data crosses the worker boundary. Requests and file acknowledgement remain on the owner.
    internal static class ReplayUploadQueue
    {
        internal sealed class Chunk { public string Path; public byte[] Bytes; public ReplayUploadBinding Binding; public ReplayLocalReceipt Receipt; }
        internal sealed class Status { public string Path, Hash; public byte[] Bytes; public ReplayUploadBinding Binding; }
        internal sealed class Rejection { public string Path, Reason; }
        internal sealed class Result
        {
            public Chunk File;
            public Status Session;
            public string Error;
            public readonly List<Rejection> Rejections = new List<Rejection>();
        }
        struct Entry { public string Path; public DateTime Written; }
        [Serializable] sealed class LocalStatus { public string session; public long last_ms, last_seq; public bool ended, truncated; }
        [Serializable] sealed class UploadStatus { public long last_ms, last_seq; public bool ended, truncated; }

        // Yield after each directory entry or candidate. WebGL shares this scan without a recursive, blocking GetFiles.
        static IEnumerator Scan(string root, string endpoint, Result result)
        {
            var directories = new Stack<string>(); directories.Push(root);
            var chunks = new List<Entry>(); var sessions = new List<Entry>();
            while (directories.Count > 0)
            {
                using (var entries = new DirectoryInfo(directories.Pop()).EnumerateFileSystemInfos().GetEnumerator())
                {
                    while (entries.MoveNext())
                    {
                        var entry = entries.Current;
                        if (entry is DirectoryInfo) directories.Push(entry.FullName);
                        else if (entry.Name.EndsWith(".ready.collection.json", StringComparison.Ordinal))
                            chunks.Add(new Entry { Path = entry.FullName, Written = entry.LastWriteTimeUtc });
                        else if (entry.Name == "collection.json")
                            sessions.Add(new Entry { Path = entry.FullName, Written = entry.LastWriteTimeUtc });
                        yield return null;
                    }
                }
                yield return null;
            }
            chunks.Sort((a,b) => a.Written.CompareTo(b.Written)); sessions.Sort((a,b) => a.Written.CompareTo(b.Written));
            foreach (var entry in chunks)
            {
                result.File = ReadChunk(entry.Path, endpoint, result);
                yield return null;
                if (result.File != null) break;
            }
            foreach (var entry in sessions)
            {
                result.Session = ReadStatus(entry.Path, endpoint);
                yield return null;
                if (result.Session != null) break;
            }
        }
        static Chunk ReadChunk(string bindingFile, string endpoint, Result result)
        {
            string path = bindingFile.Substring(0, bindingFile.Length - ".collection.json".Length);
            if (!File.Exists(path) || File.Exists(path + ".upload-error")) return null;
            if (new FileInfo(bindingFile).Length > 8192 || new FileInfo(path).Length > ReplayCodec.MaxCompressed)
            { result.Rejections.Add(new Rejection { Path = path, Reason = "local_size" }); return null; }
            var binding = JsonUtility.FromJson<ReplayUploadBinding>(File.ReadAllText(bindingFile));
            if (binding == null || binding.endpoint != endpoint) return null;
            binding.Encode();
            if (!File.Exists(path + ".json") || new FileInfo(path + ".json").Length > 65536)
            { result.Rejections.Add(new Rejection { Path = path, Reason = "missing_receipt" }); return null; }
            var receipt = JsonUtility.FromJson<ReplayLocalReceipt>(File.ReadAllText(path + ".json"));
            byte[] bytes = File.ReadAllBytes(path);
            if (receipt?.header == null || binding.session.session_id != receipt.header.session || !Guid.TryParseExact(receipt.header.segment, "N", out _) ||
                receipt.header.chunk < 0 || receipt.header.chunk > 100000 || ReplayStore.Hash(bytes) != receipt.sha256)
            { result.Rejections.Add(new Rejection { Path = path, Reason = "local_integrity" }); return null; }
            return new Chunk { Path = path, Bytes = bytes, Binding = binding, Receipt = receipt };
        }
        static Status ReadStatus(string path, string endpoint)
        {
            if (new FileInfo(path).Length > 8192 || File.Exists(path + ".upload-error")) return null;
            var binding = JsonUtility.FromJson<ReplayUploadBinding>(File.ReadAllText(path));
            if (binding == null || binding.endpoint != endpoint) return null;
            binding.Encode();
            string source = Path.Combine(Path.GetDirectoryName(path), "session.json");
            if (!File.Exists(source) || new FileInfo(source).Length > 8192) return null;
            byte[] bytes = File.ReadAllBytes(source); string hash = ReplayStore.Hash(bytes);
            if (File.Exists(source + ".uploaded") && File.ReadAllText(source + ".uploaded") == hash) return null;
            var local = JsonUtility.FromJson<LocalStatus>(Encoding.UTF8.GetString(bytes));
            if (local == null || local.session != binding.session.session_id) return null;
            var payload = new UploadStatus { last_ms = local.last_ms, last_seq = local.last_seq, ended = local.ended, truncated = local.truncated };
            return new Status { Path = source, Hash = hash, Bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)), Binding = binding };
        }

        internal sealed class Read
        {
            public readonly Result Result = new Result();
            public volatile bool Cancelled;
            readonly IEnumerator work;
            readonly bool cooperative;
            bool finished;
#if !UNITY_WEBGL || UNITY_EDITOR
            readonly Task worker;
#endif
            public Read(string root, string endpoint) : this(result => Scan(root, endpoint, result)) { }
            // The factory keeps storage work independently testable, including a deliberately slow directory read.
            internal Read(Func<Result, IEnumerator> createWork, bool useWorker = true)
            {
                work = createWork(Result);
#if !UNITY_WEBGL || UNITY_EDITOR
                cooperative = !useWorker;
                if (useWorker) worker = Task.Run(Run);
#else
                cooperative = true;
#endif
            }
            void Run()
            {
                try { while (!Cancelled && work.MoveNext()) { } }
                catch (Exception e) { Result.Error = e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException ? "local_queue" : e.GetType().Name; }
                finally { (work as IDisposable)?.Dispose(); }
            }
            public IEnumerator Wait()
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!cooperative)
                {
                    while (!worker.IsCompleted) yield return null;
                    // Already complete: this observes failures without waiting for storage on the main thread.
                    worker.GetAwaiter().GetResult();
                    yield break;
                }
#endif
                var budget = new Stopwatch();
                while (!finished && !Cancelled)
                {
                    budget.Restart();
                    try
                    {
                        do { if (!work.MoveNext()) { finished = true; break; } } while (budget.Elapsed.TotalMilliseconds < 2);
                    }
                    catch (Exception) { Result.Error = "local_queue"; finished = true; }
                    if (!finished) yield return null;
                }
                (work as IDisposable)?.Dispose();
            }
            public void Cancel()
            {
                Cancelled = true;
                if (cooperative) { (work as IDisposable)?.Dispose(); finished = true; }
            }
        }
    }
}
