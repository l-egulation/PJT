using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deviant.Replay.Editor
{
    // A data-only observer: never loads a gameplay scene or executes replay commands.
    public sealed class ReplayInspector : EditorWindow
    {
        ReplayChunk chunk;
        string error, path;
        float time;
        bool playing;
        double previous;
        readonly Dictionary<long, string> strings = new Dictionary<long, string>();
        readonly Dictionary<long, long[]> entities = new Dictionary<long, long[]>(), rects = new Dictionary<long, long[]>();
        readonly Dictionary<long, long> ui = new Dictionary<long, long>();
        readonly Dictionary<long, long> entityTimes = new Dictionary<long, long>();
        long[] pointer;
        readonly List<ReplayRecord> events = new List<ReplayRecord>();
        Vector2 scroll;
        [MenuItem("Tools/Deviant/Replay/Inspect recording")]
        static void Open() => GetWindow<ReplayInspector>("Replay observer");
        void OnEnable() { previous = EditorApplication.timeSinceStartup; EditorApplication.update += Advance; }
        void OnDisable() => EditorApplication.update -= Advance;
        void Advance()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && chunk != null) { time += (float)((now - previous) * 1000); if (time >= End) { time = End; playing = false; } Repaint(); }
            previous = now;
        }
        float End => chunk == null ? 0 : chunk.Records[chunk.Records.Count - 1].TimeMs;
        void OnGUI()
        {
            if (GUILayout.Button("Open local .replay.gz.ready file"))
            {
                string selected = EditorUtility.OpenFilePanel("Replay", Path.Combine(Application.persistentDataPath, "ReplayQueue"), "");
                if (!string.IsNullOrEmpty(selected))
                {
                    try { var f = new FileInfo(selected); if (f.Length > ReplayCodec.MaxCompressed) throw new InvalidDataException("Compressed file limit."); chunk = ReplayCodec.Decode(File.ReadAllBytes(selected)); path = selected; time = chunk.Header.StartMs; error = null; }
                    catch (Exception e) { chunk = null; error = e.GetType().Name + ": " + e.Message; }
                }
            }
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (chunk == null) return;
            EditorGUILayout.LabelField("Build", chunk.Header.BuildId); EditorGUILayout.LabelField("Content", chunk.Header.ContentHash);
            EditorGUILayout.LabelField("File", Path.GetFileName(path));
            EditorGUILayout.LabelField("Session", chunk.Header.SessionId);
            EditorGUILayout.LabelField("Segment", chunk.Header.SegmentId);
            if (ReplayUuid7.TryGetUtc(chunk.Header.SegmentId, out var segmentUtc))
                EditorGUILayout.LabelField("Segment created (UTC)", segmentUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.HelpBox("Schematic observation of recorded facts. No physics, AI or game input is executed. Offscreen paths and unsampled frames are unavailable.", MessageType.Info);
            playing = GUILayout.Toggle(playing, "Play"); time = EditorGUILayout.Slider("Session time (ms)", time, chunk.Header.StartMs, End);
            Apply((long)time);
            var area = GUILayoutUtility.GetRect(200, 300, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(area, new Color(.08f, .09f, .11f));
            entities.TryGetValue(0, out var camera); float cx = camera == null ? 0 : camera[1] / 256f, cy = camera == null ? 0 : camera[2] / 256f;
            float half = camera == null ? 10 : Mathf.Max(1, ReplayNumbers.Unfloat(camera[8]));
            float scale = area.height / (half * 2);
            foreach (var pair in entities)
            {
                if (pair.Key == 0) continue;
                var v = pair.Value; float x = area.center.x + (v[1] / 256f - cx) * scale, y = area.center.y - (v[2] / 256f - cy) * scale;
                if (!area.Contains(new Vector2(x, y))) continue;
                EditorGUI.DrawRect(new Rect(x - 3, y - 5, 6, 10), pair.Key == 1 ? Color.cyan : v[10] == 2 ? Color.yellow : Color.red);
            }
            foreach (var v in rects.Values)
            {
                if (v[5] < 0) continue;
                var rect = new Rect(area.x + v[1] / 4096f * area.width, area.y + v[2] / 4096f * area.height, v[3] / 4096f * area.width, v[4] / 4096f * area.height);
                bool selected = ui.TryGetValue(1, out var selection) && selection == v[0];
                EditorGUI.DrawRect(rect, selected ? new Color(0, 1, 1, .3f) : new Color(1, 1, 1, .08f));
                if (strings.TryGetValue(v[0], out var label)) GUI.Label(rect, label);
            }
            if (pointer != null && pointer[1] >= 0 && pointer[2] >= 0)
                EditorGUI.DrawRect(new Rect(area.x + pointer[1] / 4096f * area.width - 2, area.y + pointer[2] / 4096f * area.height - 2, 5, 5), Color.green);
            string Name(long id) => strings.TryGetValue(id, out var text) ? text : "none";
            foreach (var kind in new[] { 0L, 1L, 2L }) EditorGUILayout.LabelField(kind == 0 ? "Hover" : kind == 1 ? "Selection" : "Input focus", Name(ui.TryGetValue(kind, out var id) ? id : 0));
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(100));
            foreach (var e in events.TakeLast(50)) EditorGUILayout.LabelField(e.TimeMs + "ms  " + e.Kind + "  " + string.Join(",", e.Values));
            EditorGUILayout.EndScrollView();
        }
        void Apply(long target)
        {
            strings.Clear(); entities.Clear(); rects.Clear(); ui.Clear(); events.Clear(); entityTimes.Clear(); pointer = null;
            long pointerTime = 0;
            foreach (var r in chunk.Records)
            {
                if (r.TimeMs > target) break; var v = r.Values;
                switch (r.Kind)
                {
                    case ReplayKind.String: strings[v[0]] = r.Text; break;
                    case ReplayKind.Entity: entities[v[0]] = (long[])v.Clone(); entityTimes[v[0]] = r.TimeMs; break;
                    case ReplayKind.EntityExit: entities.Remove(v[0]); break;
                    case ReplayKind.UiRect: rects[v[0]] = v; break;
                    case ReplayKind.UiState: ui[v[0]] = v[1]; break;
                    case ReplayKind.Pointer: pointer = (long[])v.Clone(); pointerTime = r.TimeMs; break;
                    case ReplayKind.Hit: case ReplayKind.Command: case ReplayKind.UiActivate: case ReplayKind.Gap: events.Add(r); break;
                }
            }
            var filled = new HashSet<long>(); bool pointerFilled = false;
            foreach (var r in chunk.Records)
            {
                if (r.TimeMs <= target) continue;
                if (r.Kind == ReplayKind.Gap || r.Kind == ReplayKind.Clock || r.Kind == ReplayKind.SnapshotBegin) break;
                var v = r.Values;
                if (r.Kind == ReplayKind.EntityExit) filled.Add(v[0]);
                if (r.Kind == ReplayKind.Entity && filled.Add(v[0]) && entities.TryGetValue(v[0], out var old)) Interpolate(old, entityTimes[v[0]], v, r.TimeMs, target);
                if (r.Kind == ReplayKind.Pointer && !pointerFilled && pointer != null)
                { pointerFilled = true; if (pointer[1] >= 0 && v[1] >= 0) Interpolate(pointer, pointerTime, v, r.TimeMs, target); }
            }
        }
        static void Interpolate(long[] state, long from, long[] next, long until, long target)
        {
            if (until <= from) return; double t = (target - from) / (double)(until - from);
            state[1] = (long)Math.Round(state[1] + (next[1] - state[1]) * t);
            state[2] = (long)Math.Round(state[2] + (next[2] - state[2]) * t);
        }
    }
}
