using System;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    // First-visit opening of the starting village: three static backdrops with narration in the order of
    // docs/design/story/opening/opening.md (data/story/starting_village_opening.json). It ends at the same farmhouse well where
    // control is handed over. Skipping jumps to the awakening scene so its facts are still shown; only a finished
    // sequence is recorded as seen. Replays from the record farmhouse never change the record.
    public sealed partial class TemporaryGameGui
    {
        [Serializable] sealed class StoryActor { public string sprite, clip; public float x, baseline; public int scale = 1; public bool flipX; }
        [Serializable] sealed class StoryLineCast { public StoryActor[] actors; }
        [Serializable] sealed class StoryScene { public string backdrop; public int[] steps; public string[] lines; public StoryLineCast[] cast; }
        [Serializable] sealed class StoryData { public int schemaVersion; public string id, title; public StoryScene[] scenes; }
        public enum StoryOutcome { None, Completed, Skipped, Failed }

        GameObject storyPanel;
        Image storyBackdrop;
        Text storyLine, storyProgress;
        StoryData story;
        bool storyChecked, storyActive, storyFirstVisit, storySkipped;
        int storyScene, storyLineIndex, storyAdvancedFrame = -1;
        public StoryOutcome LastStoryOutcome { get; private set; }

        StoryData Story
        {
            get
            {
                if (story != null) return story;
                var text = Resources.Load<TextAsset>("HubStory");
                if (text) story = JsonUtility.FromJson<StoryData>(text.text);
                return story;
            }
        }
        bool StoryRequired => Story != null && !Profile.Snapshot.HasSeen(Story.id);

        void BuildStory()
        {
            storyPanel = Picture("", stage, 0, 0, 960, 540, PaperGuiSkin.Paper, true).gameObject; storyPanel.name = "이야기";
            storyBackdrop = Picture("", storyPanel.transform, 0, 0, 960, 540); storyBackdrop.type = Image.Type.Simple;
            for (int i = 0; i < storyActors.Length; i++)
            { storyActors[i] = Picture("", storyPanel.transform, 0, 0, 1, 1, Color.white); storyActors[i].type = Image.Type.Simple; storyActors[i].name = "Actor " + i; }
            var box = Picture("panel", storyPanel.transform, 40, 404, 880, 112);
            storyLine = Label("", box.transform, 24, 14, 832, 60, 18);
            storyLine.alignment = TextAnchor.UpperLeft;
            storyProgress = Label("", box.transform, 24, 80, 300, 22, 12, Muted);
            var next = Button("다음", box.transform, 716, 70, 140, 32, AdvanceStoryPress, true);
            Deviant.UI.Theme.ApprovedTheme.ControlHint(BindPrompt(Label("", box.transform, 430, 80, 280, MenuHintHeight, MenuHintFontSize, Muted), "[{Advance}] 다음  [{Pause}] 건너뛰기")).alignment = TextAnchor.MiddleRight;
            RegisterScreen(storyPanel, next);
        }

        // Called from HubUpdate once the lobby is ready: the first visit plays the opening before control is given.
        void CheckFirstVisitStory()
        {
            if (storyChecked) return;
            storyChecked = true;
            if (Story == null) { LastStoryOutcome = StoryOutcome.Failed; Debug.LogError("HubStory resource is missing; run tools/assets/build_hub_bindings.py."); return; }
            // A new profile plays the opening in the village (TemporaryGameGui.Opening). The still scenes below are not
            // shown any more: their benefactor story was dropped (user decision 2026-11-01).
            if (StoryRequired && !StartOpening() && !Profile.MarkStorySeen(Story.id))
                Debug.LogWarning("Opening was not recorded: " + Profile.LastSaveError);
        }

        void PlayStory(bool firstVisit)
        {
            if (Story == null || storyActive) return;
            storyActive = true; storyFirstVisit = firstVisit; storySkipped = false; storyScene = 0; storyLineIndex = 0;
            LastStoryOutcome = StoryOutcome.None;
            hubOpen = null; OpenHubPanel(storyPanel);
            ShowStoryLine();
        }

        void ShowStoryLine()
        {
            var scene = Story.scenes[storyScene];
            var sprite = Resources.Load<Sprite>(scene.backdrop);
            storyBackdrop.sprite = sprite; storyBackdrop.enabled = sprite;
            if (!sprite) Debug.LogWarning("Story backdrop missing: " + scene.backdrop);
            storyLine.text = scene.lines[storyLineIndex];
            ShowCast(scene.cast != null && storyLineIndex < scene.cast.Length ? scene.cast[storyLineIndex].actors : null);
            storyProgress.text = $"{Story.title} / {storyScene + 1} / {Story.scenes.Length}";
        }

        // Still character sprites of the current line (data cast), drawn over the backdrop with their bottom pivot on the
        // baseline. A clip actor plays its frames once (the helper's dissolve) and holds the last frame.
        readonly Image[] storyActors = new Image[3];
        readonly SpriteFlipbook[] storyClips = new SpriteFlipbook[3];
        public int StoryActorsShown { get { int n = 0; foreach (var a in storyActors) if (a && a.enabled) n++; return n; } }

        void ShowCast(StoryActor[] actors)
        {
            for (int i = 0; i < storyActors.Length; i++)
            {
                var image = storyActors[i]; var actor = actors != null && i < actors.Length ? actors[i] : null;
                storyClips[i] = null;
                var sprite = actor != null ? Resources.Load<Sprite>(actor.sprite) : null;
                image.enabled = sprite;
                if (!sprite) { if (actor != null) Debug.LogWarning("Story character missing: " + actor.sprite); continue; }
                if (!string.IsNullOrEmpty(actor.clip)) { storyClips[i] = new SpriteFlipbook(actor.clip); storyClips[i].Play(); }
                image.sprite = sprite;
                var rect = image.rectTransform; var size = sprite.rect.size * Mathf.Max(1, actor.scale);
                rect.sizeDelta = size;
                // Stage coordinates run from the top-left; place the sprite's own pivot on (x, baseline).
                var pivot = sprite.pivot / sprite.rect.size;
                rect.anchoredPosition = new Vector2(actor.x - size.x * pivot.x, -(actor.baseline - size.y * (1 - pivot.y)));
                // Mirrored actors flip around the rect's left edge, so the anchor moves to the right edge.
                rect.localScale = new Vector3(actor.flipX ? -1 : 1, 1, 1);
                if (actor.flipX) rect.anchoredPosition += new Vector2(size.x, 0);
            }
        }

        void TickCast()
        {
            for (int i = 0; i < storyActors.Length; i++)
            {
                var clip = storyClips[i]; if (clip == null) continue;
                var frame = clip.Current ?? (clip.Frames.Length > 0 ? clip.Frames[clip.Frames.Length - 1] : null);
                if (frame) storyActors[i].sprite = frame;
            }
        }

        // One line per press: Enter reaches both the key handler and the focused '다음' button in the same frame.
        void AdvanceStoryPress()
        {
            if (Time.frameCount == storyAdvancedFrame) return;
            storyAdvancedFrame = Time.frameCount; AdvanceStory();
        }

        void AdvanceStory()
        {
            if (!storyActive) return;
            var scene = Story.scenes[storyScene];
            if (++storyLineIndex < scene.lines.Length) { ShowStoryLine(); return; }
            if (++storyScene < Story.scenes.Length) { storyLineIndex = 0; ShowStoryLine(); return; }
            FinishStory(storySkipped ? StoryOutcome.Skipped : StoryOutcome.Completed);
        }

        // Skip never drops the ending: it moves to the awakening scene, which the player still reads to close.
        void SkipStory()
        {
            if (!storyActive) return;
            int last = Story.scenes.Length - 1;
            if (storyScene < last) { storySkipped = true; storyScene = last; storyLineIndex = 0; ShowStoryLine(); }
            else FinishStory(StoryOutcome.Skipped);
        }

        void FinishStory(StoryOutcome outcome)
        {
            storyActive = false; LastStoryOutcome = outcome;
            if (storyFirstVisit)
            {
                if (!Profile.MarkStorySeen(Story.id) && !Profile.Snapshot.HasSeen(Story.id))
                    Debug.LogWarning("Opening was not recorded: " + Profile.LastSaveError);
                // Control is handed over at the same well where the awakening took place.
                var well = village.Find(StartingVillageWorld.ReturnWell);
                if (well != null)
                {
                    var player = village.World.arena.Player;
                    player.Position = well.Position; player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    village.World.RefreshCamera(0, true);
                }
            }
            CloseHubPanel();
        }

        void StoryInput(Keyboard k)
        {
            TickCast();
            if (TakeBack()) SkipStory();
            else if (TakeAdvance()) AdvanceStoryPress();
        }

        // The record book's name of a seen story: the story script's own title, the visits' title with the piece number for
        // a visit (artisan-visit-N has no title of its own), and a Korean name for every other story the game records, so
        // no raw English id shows in the list; an unknown id stays as it is.
        string StoryTitle(string id)
        {
            if (Story != null && Story.id == id) return Story.title;
            const string visit = "artisan-visit-";
            if (id.StartsWith(visit, System.StringComparison.Ordinal) && int.TryParse(id.Substring(visit.Length), out int piece))
                return $"{(string.IsNullOrEmpty(VisitData?.title) ? "마을 장인의 방문" : VisitData.title)} {piece}";
            switch (id)
            {
                case FirstReturnStory: return "첫 회귀";
                case FirstRunVisitStory: return "첫 런 뒤 장인";
                case "maintenance-intro-chapter1": return "1챕터 정비소 첫 만남";
                case "maintenance-intro-chapter2": return "2챕터 정비소 첫 만남";
                case Chapter1ManualCheckpoint.PortalTaughtStory: return "포탈 안내";
                case ForestCombatV2.Chapter1FirstRunInkGuide.GuideInkStory: return "첫 잉크 줍기";
                case ForestCombatV2.Chapter1FirstRunInkGuide.GuideBagStory: return "첫 팔레트 장착";
                case Chapter1VillageInkPickup.DiscoveryStory: return "붉은 잉크 발견";
                default: return id;
            }
        }
    }
}
