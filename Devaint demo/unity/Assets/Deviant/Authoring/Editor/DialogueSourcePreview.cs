using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Authoring
{
    // A private preview scene and real Presenter/uGUI binding. Nothing here can
    // access a Run, chapter, combat host or production command registration.
    public sealed class DialogueSourcePreview : IDisposable
    {
        public const string NpcId = "fixture.dialogue-source.preview-npc";
        public const string PlacementId = "fixture.dialogue-source.preview-placement";
        readonly Scene scene;
        readonly ControlSession controls = new ControlSession();
        readonly PreviewConditions conditions = new PreviewConditions();
        readonly PreviewTargets targets;
        readonly DialogueCommandBindings commands;
        readonly Camera camera;
        readonly TextMeshProUGUI speaker, body;
        readonly Button advance, cancel;
        readonly Button[] choices;
        readonly TMP_Text[] labels;
        readonly NpcDialogueHost host;
        readonly DialogueView view;
        readonly List<string> commandLog = new List<string>();
        readonly RenderTexture texture;
        bool disposed;
        public NpcDialogueHost Host => host;
        public DialogueView View => view;
        public RenderTexture Texture => texture;
        public Scene PreviewScene => scene;
        public IDialogueCommands Current => host.Current;
        public IReadOnlyList<string> CommandLog => commandLog.AsReadOnly();
        public ControlState ControlState => controls.Current;
        public int ChoiceCapacity => choices.Length;
        public DialogueSourcePreview(CompiledDialogueSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (!font) throw new InvalidOperationException("The existing LiberationSans SDF dialogue preview font is unavailable.");
                int capacity = Math.Max(1, source.Definition.Lines.Max(line => line.Choices.Count));
                int height = Math.Max(320, 200 + capacity * 42);
                texture = new RenderTexture(760, height, 24) { name = "DialogueSourcePreview", hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
                var cameraRoot = Root("DialogueSourcePreviewCamera", typeof(Camera)); camera = cameraRoot.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = height * .005f;
                camera.aspect = 760f / height; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .08f, .11f);
                camera.targetTexture = texture; camera.enabled = false; camera.cullingMask = 1 << 5; camera.scene = scene;
                var canvasRoot = Root("DialogueSourcePreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasRoot.layer = 5; var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                var canvasRect = canvasRoot.GetComponent<RectTransform>(); canvasRect.sizeDelta = new Vector2(760, height); canvasRect.localScale = Vector3.one * .01f;
                var panel = Child(canvasRoot.transform, "Panel", typeof(Image)); panel.GetComponent<Image>().color = new Color(.12f, .14f, .19f);
                Place(panel, 16, 16, 728, height - 32);
                speaker = Text(panel.transform, "Speaker", font, 22); Place(speaker.gameObject, 20, 12, 680, 30);
                body = Text(panel.transform, "Body", font, 20); Place(body.gameObject, 20, 46, 680, 70);
                choices = new Button[capacity]; labels = new TMP_Text[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    choices[i] = MakeButton(panel.transform, "Choice" + i, font, "", out var label); labels[i] = label;
                    Place(choices[i].gameObject, 20, 122 + i * 42, 680, 36);
                }
                advance = MakeButton(panel.transform, "Advance", font, "Advance", out _); cancel = MakeButton(panel.transform, "Cancel", font, "Cancel", out _);
                Place(advance.gameObject, 20, height - 84, 320, 34); Place(cancel.gameObject, 360, height - 84, 340, 34);
                view = canvasRoot.AddComponent<DialogueView>(); view.Configure(panel, speaker, body, advance, cancel, choices, labels);
                host = Root("DialogueSourcePreviewHost").AddComponent<NpcDialogueHost>();
                targets = new PreviewTargets(source.Definition);
                var callbacks = new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>>(StringComparer.Ordinal);
                foreach (var commandId in source.References.CommandIds)
                    callbacks.Add(commandId, (context, request) => { commandLog.Add(request.CommandId); return new DialogueCommandResult(DialogueCommandStatus.Succeeded); });
                commands = new DialogueCommandBindings(callbacks);
                host.Configure(targets, conditions, new InteractionPolicy(0, true, false, true), view, new DialogueSourceText(source.References), controls,
                    new DialoguePresentationPolicy(InputChannels.None, false), commands, new DialogueSourceBinding(NpcId, source));
            }
            catch { Dispose(); throw; }
        }
        GameObject Root(string name, params Type[] components)
        {
            var result = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(result, scene); return result;
        }
        static GameObject Child(Transform parent, string name, params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform) }; types.AddRange(components);
            var result = new GameObject(name, types.ToArray()) { hideFlags = HideFlags.HideAndDontSave, layer = 5 };
            result.transform.SetParent(parent, false); return result;
        }
        static void Place(GameObject root, float x, float y, float width, float height)
        {
            var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size)
        {
            var root = Child(parent, name, typeof(TextMeshProUGUI)); var result = root.GetComponent<TextMeshProUGUI>();
            result.font = font; result.fontSize = size; result.color = Color.white; result.raycastTarget = false; result.textWrappingMode = TextWrappingModes.Normal;
            return result;
        }
        static Button MakeButton(Transform parent, string name, TMP_FontAsset font, string caption, out TMP_Text label)
        {
            var root = Child(parent, name, typeof(Image), typeof(Button)); root.GetComponent<Image>().color = new Color(.2f, .26f, .34f);
            var button = root.GetComponent<Button>(); var text = Text(root.transform, "Label", font, 18); text.text = caption; text.alignment = TextAlignmentOptions.Center;
            var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            label = text; return button;
        }
        public bool TryStart(out string error)
        {
            ThrowIfDisposed(); var candidates = host.Query(out error);
            if (candidates.Count != 1) { error = error ?? "Preview target is unavailable."; return false; }
            bool started = host.TryInteract(candidates[0], out error); Refresh(); return started;
        }
        public bool TryApply(CompiledDialogueSource candidate, out string error)
        {
            ThrowIfDisposed(); error = null;
            if (candidate == null) { error = "A complete preview candidate is required."; return false; }
            if (candidate.Definition.Lines.Any(line => line.Choices.Count > choices.Length)) { error = "Candidate requires more preview choice slots; close and start a new preview."; return false; }
            bool applied = host.TryApplySource(candidate, out error); Refresh(); return applied;
        }
        public void SetCondition(string id, bool value) { ThrowIfDisposed(); conditions.Values[id] = value; Refresh(); }
        public bool GetCondition(string id) => !conditions.Values.TryGetValue(id, out var value) || value;
        public void Advance() { ThrowIfDisposed(); if (advance.isActiveAndEnabled && advance.interactable) advance.onClick.Invoke(); Refresh(); }
        public void Cancel() { ThrowIfDisposed(); if (cancel.isActiveAndEnabled && cancel.interactable) cancel.onClick.Invoke(); Refresh(); }
        public void Select(int index)
        {
            ThrowIfDisposed(); if (index < 0 || index >= choices.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (choices[index].isActiveAndEnabled && choices[index].interactable) choices[index].onClick.Invoke(); Refresh();
        }
        public void Click(Vector2 normalized)
        {
            ThrowIfDisposed(); var pixel = new Vector2(normalized.x * texture.width, normalized.y * texture.height);
            foreach (var button in choices.Concat(new[] { advance, cancel }))
                if (button.isActiveAndEnabled && button.interactable && RectTransformUtility.RectangleContainsScreenPoint(button.GetComponent<RectTransform>(), pixel, camera))
                { button.onClick.Invoke(); Refresh(); return; }
        }
        public void Refresh()
        {
            ThrowIfDisposed(); host.Refresh(); Canvas.ForceUpdateCanvases(); speaker.ForceMeshUpdate(); body.ForceMeshUpdate();
            foreach (var label in labels) label.ForceMeshUpdate(); camera.Render();
        }
        void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(DialogueSourcePreview)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (host) host.Close(DialogueEndReason.OwnerDisposed);
            commands?.Dispose(); controls.Dispose();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (texture) { texture.Release(); Object.DestroyImmediate(texture); }
        }
        sealed class PreviewConditions : INarrativeConditions
        {
            public readonly Dictionary<string, bool> Values = new Dictionary<string, bool>(StringComparer.Ordinal);
            public bool Evaluate(string id, Guid world, string placement) => !Values.TryGetValue(id, out var value) || value;
        }
        sealed class PreviewTargets : IInteractionTargets
        {
            readonly Guid world = Guid.NewGuid(); readonly NpcDefinition npc;
            public PreviewTargets(DialogueDefinition definition) { npc = new NpcDefinition(NpcId, definition); }
            NpcTarget Read() => new NpcTarget(world, PlacementId, npc, 0, false);
            public IReadOnlyList<NpcTarget> ReadTargets() => new[] { Read() };
            public bool TryRead(Guid session, string placement, out NpcTarget target) { target = session == world && placement == PlacementId ? Read() : null; return target != null; }
        }
    }
}
