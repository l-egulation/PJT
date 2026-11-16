using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Deviant.Replay.Unity
{
    [Serializable] public sealed class ReplayCollectorSettings { public string base_url = ""; }
    [Serializable] public sealed class ReplayUploadReceipt { public string state, sha256; public bool duplicate; }
    [Serializable] sealed class ReplayReceiptHeader { public string session, segment; public int chunk; }
    [Serializable] sealed class ReplayLocalReceipt { public string sha256; public ReplayReceiptHeader header; }

    public sealed class ReplayUploadClient : MonoBehaviour
    {
        string root, endpoint;
        Coroutine coroutine;
        ReplayUploadQueue.Read queueRead;
        ReplayUploadQueue.Result queueResult;
        UnityWebRequest active;
        readonly System.Random jitter = new System.Random();
        float backoff = 2;
        public string LastError { get; private set; }
        public long UploadedBytes { get; private set; }
        public int UploadedChunks { get; private set; }

        public void Configure(string queueRoot, string baseURL)
        {
            string normalized = ReplayUploadBinding.NormalizeEndpoint(baseURL);
            if (coroutine != null && root == queueRoot && endpoint == normalized) return;
            StopUploads(); root = Path.GetFullPath(queueRoot); endpoint = normalized;
            if (endpoint.Length != 0) { Directory.CreateDirectory(root); coroutine = StartCoroutine(Loop()); }
        }
        public void StopUploads()
        {
            queueRead?.Cancel();
            if (active != null) { active.Abort(); active.Dispose(); active = null; }
            if (coroutine != null) { StopCoroutine(coroutine); coroutine = null; }
        }
        void OnDisable() => StopUploads();
        void OnDestroy() => StopUploads();

        IEnumerator ReadQueue()
        {
            // A stopped/reconfigured reader is cancelled but never joined on the game thread.
            // Reap it before starting a new one so repeated configuration cannot create overlapping scans.
            if (queueRead != null) { yield return queueRead.Wait(); queueRead = null; }
            var read = queueRead = new ReplayUploadQueue.Read(root, endpoint);
            yield return read.Wait();
            if (read.Cancelled) yield break;
            queueRead = null; queueResult = read.Result;
            if (queueResult.Error != null) LastError = queueResult.Error;
            foreach (var rejection in queueResult.Rejections)
            {
                Reject(rejection.Path, rejection.Reason);
                yield return null;
            }
        }
        void Reject(string path, string reason) { try { File.WriteAllText(path + ".upload-error", reason); ReplayBrowserStorage.Sync(); LastError = reason; } catch (IOException) { LastError = "local_queue"; } }
        UnityWebRequest Request(string method, string url, byte[] data, string contentType, string player = null)
        {
            var request = new UnityWebRequest(url, method) { uploadHandler = new UploadHandlerRaw(data), downloadHandler = new DownloadHandlerBuffer(), timeout = 20 };
            request.SetRequestHeader("Content-Type", contentType); if (player != null) request.SetRequestHeader("X-Player-ID", player); return request;
        }
        UnityWebRequest Register(ReplayUploadBinding binding) => Request("POST", endpoint + "/v1/sessions", Encoding.UTF8.GetBytes(binding.session.Encode()), "application/json");
        bool Success(UnityWebRequest request) => request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
        bool Permanent(UnityWebRequest request) => request.responseCode >= 400 && request.responseCode < 500 && request.responseCode != 408 && request.responseCode != 429;
        float Delay(UnityWebRequest request)
        {
            LastError = "upload_retry";
            if (int.TryParse(request.GetResponseHeader("Retry-After"), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) && seconds > 0) return Math.Min(seconds, 86400);
            float value = backoff * (float)(.8 + jitter.NextDouble() * .4); backoff = Math.Min(300, backoff * 2); return value;
        }
        public static bool Confirms(byte[] response, string expected)
        {
            try { var receipt = JsonUtility.FromJson<ReplayUploadReceipt>(Encoding.UTF8.GetString(response)); return receipt != null && receipt.state == "committed" && receipt.sha256 == expected; }
            catch (ArgumentException) { return false; }
        }

        IEnumerator Loop()
        {
            while (true)
            {
                yield return ReadQueue();
                var queued = queueResult; queueResult = null;
                var file = queued?.File;
                if (file != null)
                {
                    float delay = 0; bool registered = false;
                    using (var request = Register(file.Binding))
                    {
                        active = request; yield return request.SendWebRequest(); active = null;
                        registered = Success(request);
                        if (!registered) { if (Permanent(request)) Reject(file.Path, "register_" + request.responseCode); else delay = Delay(request); }
                    }
                    if (!registered) { yield return new WaitForSecondsRealtime(Math.Max(1, delay)); continue; }
                    bool accepted = false;
                    string path = "/v1/sessions/" + file.Receipt.header.session + "/chunks/" + file.Receipt.header.segment + "/" + file.Receipt.header.chunk;
                    using (var request = Request("PUT", endpoint + path, file.Bytes, "application/octet-stream", file.Binding.session.player_id))
                    {
                        request.SetRequestHeader("X-Replay-SHA256", file.Receipt.sha256);
                        active = request; yield return request.SendWebRequest(); active = null;
                        accepted = Success(request) && Confirms(request.downloadHandler.data, file.Receipt.sha256);
                        if (!accepted) { if (Permanent(request)) Reject(file.Path, "upload_" + request.responseCode); else delay = Delay(request); }
                    }
                    if (accepted)
                    {
                        if (TryAcknowledge(file)) { UploadedBytes += file.Bytes.Length; UploadedChunks++; LastError = null; backoff = 2; }
                    }
                    else { yield return new WaitForSecondsRealtime(Math.Max(1, delay)); continue; }
                }
                yield return SyncOneStatus(queued?.Session);
                yield return new WaitForSecondsRealtime(2);
            }
        }
        bool TryAcknowledge(ReplayUploadQueue.Chunk file)
        {
            try { new ReplayStore(root).Acknowledge(file.Path, file.Receipt.sha256); ReplayBrowserStorage.Sync(); return true; }
            catch (IOException) { LastError = "local_acknowledgement"; return false; }
        }
        IEnumerator SyncOneStatus(ReplayUploadQueue.Status status)
        {
            if (status == null) yield break;
            bool registered; float retry = 0;
            using (var request = Register(status.Binding))
            {
                active = request; yield return request.SendWebRequest(); active = null; registered = Success(request);
                if (!registered) { if (Permanent(request)) MarkStatusError(status.Path, "register_" + request.responseCode); else retry = Delay(request); }
            }
            if (!registered) { if (retry > 0) yield return new WaitForSecondsRealtime(retry); yield break; }
            using (var request = Request("PUT", endpoint + "/v1/sessions/" + status.Binding.session.session_id + "/status", status.Bytes, "application/json", status.Binding.session.player_id))
            {
                active = request; yield return request.SendWebRequest(); active = null;
                if (Success(request)) { MarkStatusUploaded(status.Path, status.Hash); ReplayBrowserStorage.Sync(); }
                else if (Permanent(request)) MarkStatusError(status.Path, "status_" + request.responseCode);
                else retry = Delay(request);
            }
            if (retry > 0) yield return new WaitForSecondsRealtime(retry);
        }
        void MarkStatusError(string path, string error) { try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "collection.json.upload-error"), error); LastError = error; } catch (IOException) { LastError = "local_status"; } }
        void MarkStatusUploaded(string path, string hash) { try { File.WriteAllText(path + ".uploaded", hash); } catch (IOException) { LastError = "local_status"; } }
    }
}
