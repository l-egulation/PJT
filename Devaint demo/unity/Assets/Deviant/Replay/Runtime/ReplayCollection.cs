using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Deviant.Replay
{
    public static class ReplayPlayerIdentity
    {
        public const string PreferenceKey = "deviant.telemetry.player_id.v1";
        public static string GetOrCreate(Func<string> read, Action<string> write)
        {
            string value = read();
            if (Guid.TryParseExact(value, "N", out var parsed) && parsed != Guid.Empty) return parsed.ToString("N");
            value = Guid.NewGuid().ToString("N"); write(value); return value;
        }
    }
    [Serializable]
    public sealed class ReplayCollectionSession
    {
        public string player_id, session_id, build_id, platform, started_at;
        // Legacy protocol version; automatic collection does not imply an explicit consent action.
        public int consent_version = 1;
        public string Encode()
        {
            if (!Guid.TryParseExact(player_id, "N", out var player) || player == Guid.Empty ||
                !Guid.TryParseExact(session_id, "N", out var session) || session == Guid.Empty ||
                build_id == null || !Regex.IsMatch(build_id, "^[A-Za-z0-9._-]{1,128}$") || consent_version != 1 ||
                !DateTimeOffset.TryParse(started_at, CultureInfo.InvariantCulture, DateTimeStyles.None, out var started))
                throw new ArgumentException("Invalid collection session.");
            started_at = started.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            if (platform != "web" && platform != "windows" && platform != "linux" && platform != "osx" && platform != "editor") throw new ArgumentException("Invalid collection platform.");
            return "{\"player_id\":\"" + player_id + "\",\"session_id\":\"" + session_id + "\",\"build_id\":\"" + build_id +
                "\",\"platform\":\"" + platform + "\",\"started_at\":\"" + started_at + "\",\"consent_version\":1}";
        }
    }
    [Serializable]
    public sealed class ReplayUploadBinding
    {
        public string endpoint;
        public ReplayCollectionSession session;
        public static string NormalizeEndpoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)) throw new ArgumentException("Collector requires HTTPS or loopback HTTP.");
            return uri.AbsoluteUri.TrimEnd('/');
        }
        public string Encode()
        {
            endpoint = NormalizeEndpoint(endpoint); if (endpoint.Length == 0 || session == null) throw new ArgumentException("Missing collection endpoint/session.");
            return "{\"endpoint\":\"" + endpoint.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"session\":" + session.Encode() + "}";
        }
    }
}
