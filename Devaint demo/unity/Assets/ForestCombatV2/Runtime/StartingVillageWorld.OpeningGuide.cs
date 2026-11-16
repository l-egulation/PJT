using System;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // The playable opening's teaching and its first fight (HTML prototype 04_테스트/오프닝 테스트/튜토리얼길테스트.html,
    // docs/design/story/opening/opening-playable.md): the guide ghost that shows each move, the village broom fought bare-handed,
    // the punch, the ink flicker before the blueprints and the F keycap kept over a station the banner points at.
    public sealed partial class StartingVillageWorld
    {
        // ---- Guide ghost: a translucent copy of the hero playing a demonstration recorded from the prototype at 60 fps
        // (data/story/opening_ghosts.json, compiled to Resources/HubOpeningGhosts). It shows after a short pause, fades in,
        // plays, holds its last pose, fades and plays again while the lesson is up.
        [Serializable] public sealed class GhostFile { public int schemaVersion; public float fps, wait, fade, alpha, endHold, endFade, closeAfter; public GhostTrack[] ghosts; }
        [Serializable] public sealed class GhostTrack
        {
            public string id, text; public string[] keys, codes, needHeld, clip, held, tapped;
            public float startX, startY; public float[] x, y, clipTime; public int[] face, punch;
            // A shot flying at the ghost (the dash-dodge demonstration): the sprite shotArt at shotScale, from shotX shotY
            // above the ground at frame shotFrom, moving shotSpeed units a second until frame shotTo (shotTo 0: none).
            public string shotArt; public int shotFrom, shotTo; public float shotX, shotY, shotSpeed, shotScale = 1;
        }

        // The demonstrations' shot: shotArt "ink-ball" is a round ink ball (user 2026-11-10: a plain round ball), anything
        // else a Resources sprite.
        public const string InkBallArt = "ink-ball";
        static Sprite inkBall;
        public static Sprite ShotSprite(string art)
        {
            if (art != InkBallArt) return string.IsNullOrEmpty(art) ? null : Resources.Load<Sprite>(art);
            if (inkBall) return inkBall;
            // 16 px across at 32 px a unit: ink with a darker rim and a paper glint up left.
            const int size = 16; var px = new Color32[size * size];
            Color32 rim = new Color32(0x14, 0x11, 0x10, 255), ink = new Color32(0x2e, 0x29, 0x26, 255), glint = new Color32(0xef, 0xe6, 0xcf, 255);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > size / 2f) continue;
                    float gx = dx + 2.5f, gy = dy - 2.5f;
                    px[y * size + x] = d > size / 2f - 1.5f ? rim : gx * gx + gy * gy < 3.2f ? glint : ink;
                }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            t.SetPixels32(px); t.Apply(false, true);
            inkBall = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            inkBall.hideFlags = HideFlags.DontSave;
            return inkBall;
        }

        GhostFile ghostFile;
        GhostTrack ghost;
        SpriteRenderer ghostRenderer;
        float ghostStarted, ghostRunStarted, ghostEnded;
        public GhostFile Ghosts => ghostFile ??= LoadGhosts();
        // The recordings, read once for anyone (the move cards of the controls strip, in the village or a chapter).
        static GhostFile sharedGhosts;
        public static GhostFile SharedGhosts => sharedGhosts ??= LoadGhosts();
        public GhostTrack Ghost => ghost;
        // True once the ghost has shown the whole move at least once.
        public bool GhostShownOnce { get; private set; }
        // The frame on screen (-1 before the ghost moves) and the ghost's alpha (0..1 of its full alpha).
        public int GhostFrame { get; private set; } = -1;
        public float GhostVisibility { get; private set; }
        // Seconds the current play has run (the keys light by it).
        public float GhostTime => GhostFrame < 0 ? 0 : GhostFrame / Ghosts.fps;
        public bool GhostEnded => ghostEnded >= 0;
        public Vector2 GhostFeet => ghost == null ? Vector2.zero : new Vector2(ghost.x[Mathf.Max(0, GhostFrame)], GroundTop + ghost.y[Mathf.Max(0, GhostFrame)]);

        static GhostFile LoadGhosts()
        {
            var text = Resources.Load<TextAsset>("HubOpeningGhosts");
            if (!text) { Debug.LogError("HubOpeningGhosts resource is missing; run tools/assets/build_hub_bindings.py."); return new GhostFile { ghosts = new GhostTrack[0], fps = 60 }; }
            return JsonUtility.FromJson<GhostFile>(text.text);
        }

        public GhostTrack FindGhost(string id) => Array.Find(Ghosts.ghosts ?? new GhostTrack[0], g => g.id == id);

        public void StartGhost(GhostTrack track)
        {
            StopGhost();
            ghost = track; if (ghost == null || ghost.x == null || ghost.x.Length == 0) { ghost = null; return; }
            ghostStarted = Time.unscaledTime; ghostRunStarted = -1; ghostEnded = -1; GhostShownOnce = false; GhostFrame = -1;
            // The view takes in the whole move and the hero: the usual follow keeps the hero low on screen, so a lesson
            // that opens on a ledge (climb, wall kick) left the ghost's ground start below the view.
            var player = World.arena.Player;
            float left = Mathf.Min(player.Position.x, Min(ghost.x)), right = Mathf.Max(player.Position.x, Max(ghost.x));
            float bottom = Mathf.Min(player.Position.y, GroundTop + Min(ghost.y)), top = Mathf.Max(player.Position.y, GroundTop + Max(ghost.y)) + GhostFrameAbove;
            GhostFraming = new Vector2((left + right) / 2, (bottom + top) / 2);
            FindGhostWallFrames(ghost, out ghostWallClip, out ghostWallTime);
            var visual = player.visual;
            ghostRenderer = new GameObject("Opening guide ghost").AddComponent<SpriteRenderer>();
            ghostRenderer.transform.SetParent(transform, false);
            ghostRenderer.sharedMaterial = visual.sharedMaterial; ghostRenderer.sortingLayerID = visual.sortingLayerID; ghostRenderer.sortingOrder = visual.sortingOrder - 1;
            ghostRenderer.enabled = false;
        }

        // Where the camera centres while a ghost shows its move (null without a ghost). The keys float about 3.1 above the
        // higher head, so the frame reaches that far above the feet.
        public Vector2? GhostFraming { get; private set; }
        const float GhostFrameAbove = 3.6f;
        static float Min(float[] values) { float m = float.MaxValue; foreach (var v in values) m = Mathf.Min(m, v); return m; }
        static float Max(float[] values) { float m = float.MinValue; foreach (var v in values) m = Mathf.Max(m, v); return m; }

        public void StopGhost()
        {
            GhostFraming = null;
            if (ghostRenderer) Destroy(ghostRenderer.gameObject);
            ghostRenderer = null; ghost = null; GhostFrame = -1; GhostVisibility = 0;
        }

        void TickGhost()
        {
            if (ghost == null || !ghostRenderer) return;
            var file = Ghosts; float now = Time.unscaledTime;
            float shown = now - ghostStarted - file.wait;
            if (shown < 0) { ghostRenderer.enabled = false; GhostVisibility = 0; return; }
            float fadeIn = Mathf.Clamp01(shown / file.fade), fadeOut = 1;
            if (shown >= file.fade && ghostRunStarted < 0) ghostRunStarted = now;
            if (ghostEnded >= 0)
            {
                fadeOut = Mathf.Max(0, 1 - (now - ghostEnded) / file.endFade);
                if (now - ghostEnded > file.endHold) { ghostRunStarted = now; ghostEnded = -1; fadeOut = 1; }
            }
            int frame = ghostRunStarted < 0 ? 0 : Mathf.Min(ghost.x.Length - 1, Mathf.FloorToInt((now - ghostRunStarted) * file.fps));
            if (ghostRunStarted >= 0 && ghostEnded < 0 && frame >= ghost.x.Length - 1) { ghostEnded = now; GhostShownOnce = true; }
            GhostFrame = ghostRunStarted < 0 ? -1 : frame;
            GhostVisibility = fadeIn * fadeOut;
            DrawGhostFrame(ghostRenderer, ghost, ghostWallClip, ghostWallTime, frame, ghostEnded >= 0 ? now - ghostEnded : -1, file.alpha * GhostVisibility, ref ghostArc);
        }

        // How a ghost stands at one frame of its recording: the sprite, where it is drawn (x and the height above the walking
        // line, units), its scale and facing. The hero's clip, or the wall art on a wall frame (heldFor: seconds the last pose
        // has been held, -1 while it plays). Shared by the road's ghosts and the controls window (which draws them on the GUI).
        public struct GhostPose { public Sprite sprite; public float x, y, scale; public bool flip; }

        // reach: the hero's hitbox half width plus its offset (where its hands meet a wall), NaN without a hero.
        public static GhostPose PoseAt(CharacterAnimationSet set, WallClimbVisuals climb, bool human, float reach, GhostTrack track,
            string[] wallClips, float[] wallTimes, int frame, float heldFor)
        {
            var pose = new GhostPose { sprite = TrackFrame(set, track.clip[frame], track.clipTime[frame], track.punch != null && track.punch[frame] != 0),
                flip = track.face[frame] < 0, x = track.x[frame], y = track.y[frame], scale = 1 };
            // On a wall the ghost climbs, kicks off and stands up over the ledge in the same art Chapter1TraversalPresentation
            // gives the hero: the climb-v4 start, climb loop and ledge clips (hands on the collider edge, enlarged about the
            // feet), and the body's wall kick (authored leaving a right wall).
            string wall = wallClips?[frame];
            if (climb != null && (wall == WallContactClip || wall == WallLedgeClip))
            {
                float t = wallTimes[frame];
                // The recordings end as the ghost stands on the top: finish standing up while the last pose holds.
                if (wall == WallLedgeClip && heldFor >= 0) t += heldFor;
                if (wall == WallContactClip)
                {
                    var start = human ? climb.humanStart : climb.inkStart;
                    var loop = human ? climb.humanClimb : climb.inkClimb;
                    pose.sprite = t < start.Duration ? start.At(t, false) : loop.At(t - start.Duration, true);
                    float handReach = (climb.contactX - pose.sprite.pivot.x) / pose.sprite.pixelsPerUnit * climb.bodyScale;
                    float contactOffset = climb.contactX > 0 && !float.IsNaN(reach) ? reach - handReach : -.4375f;
                    pose.x += contactOffset * track.face[frame];
                    pose.scale = climb.bodyScale;
                    pose.y += (1 - pose.scale) * climb.FootOffset(pose.sprite);
                }
                else
                {
                    var ledge = human ? climb.humanLedge : climb.inkLedge;
                    pose.sprite = ledge.At(t, false);
                    float blend = Mathf.SmoothStep(0, 1, t / (ledge.Duration - ledge.seconds[ledge.seconds.Length - 1]));
                    pose.scale = Mathf.Lerp(climb.bodyScale, climb.ledgeEndScale, blend);
                    float plant = GhostGrounded(track, frame) ? blend : 0;
                    pose.y += (1 - pose.scale - plant) * climb.FootOffset(pose.sprite);
                }
            }
            else
            {
                var wallClip = wall == null ? null : set?.Find(wall);
                if (wallClip != null)
                {
                    if (wallClip.sprites == null) wallClip.Load();
                    bool contact = wall == WallContactClip;
                    pose.sprite = wallClip.At(wallTimes[frame], contact);
                    pose.flip = contact ? track.face[frame] < 0 : track.face[frame] > 0;
                    if (contact) pose.x += -.4375f * track.face[frame];
                }
            }
            return pose;
        }

        // One frame of a ghost on its renderer, and the punch arc on a punch frame.
        void DrawGhostFrame(SpriteRenderer r, GhostTrack track, string[] wallClips, float[] wallTimes, int frame, float heldFor, float alpha, ref SpriteRenderer arc)
        {
            var hero = World.arena.Player;
            var pose = PoseAt(HeroSet(), ClimbArt, HeroBody == "opening_human", hero ? hero.offset.x + hero.size.x / 2 : float.NaN, track, wallClips, wallTimes, frame, heldFor);
            r.sprite = pose.sprite; r.flipX = pose.flip;
            r.transform.localScale = new Vector3(pose.scale, pose.scale, 1);
            r.transform.position = new Vector3(pose.x, GroundTop + pose.y, 0);
            r.color = new Color(1, 1, 1, alpha);
            r.enabled = r.sprite;
            if (track.punch[frame] != 0) DrawPunchArc(arc ??= ArcRenderer("Opening ghost punch"), track.x[frame], track.y[frame], track.face[frame],
                PunchProgress(track.punch, frame), alpha);
            else if (arc) arc.enabled = false;
        }

        SpriteRenderer ghostArc;

        // The recordings come from the HTML prototype, which had no wall art and drew the jump apex on a wall. A frame is
        // on a wall when the ghost is in the air and does not move sideways (it holds against the wall face); a kick starts
        // where it leaves a wall facing the other way and lasts the kick clip. Found from the track itself, so a new
        // recording keeps it.
        public const string WallContactClip = "wall_contact", WallKickClip = "wall_kick", WallLedgeClip = "wall_ledge";
        string[] ghostWallClip;
        float[] ghostWallTime;
        public string GhostWallClip => ghost == null || GhostFrame < 0 || ghostWallClip == null ? null : ghostWallClip[GhostFrame];
        public Sprite GhostSprite => ghostRenderer ? ghostRenderer.sprite : null;
        WallClimbVisuals ClimbArt => World ? World.GetComponent<Chapter1TraversalPresentation>()?.ClimbVisuals : null;
        static bool GhostGrounded(GhostTrack track, int f) => track.clip[f] == "run" || track.clip[f] == "idle" || track.clip[f] == "walk";

        static bool Has(string keys, string code) => !string.IsNullOrEmpty(keys) && Array.IndexOf(keys.Split('+'), code) >= 0;

        void FindGhostWallFrames(GhostTrack ghost, out string[] ghostWallClip, out float[] ghostWallTime) =>
            WallFrames(ghost, Ghosts.fps, HeroSet()?.Find(WallKickClip)?.Duration ?? 0, ClimbArt?.inkLedge?.Duration ?? 0, out ghostWallClip, out ghostWallTime);

        public static void WallFrames(GhostTrack ghost, float fps, float kickSeconds, float ledgeSeconds, out string[] ghostWallClip, out float[] ghostWallTime)
        {
            int n = ghost.x.Length; ghostWallClip = new string[n]; ghostWallTime = new float[n];
            float step = 1 / fps;
            bool Still(int a, int b) => b >= 0 && b < n && Mathf.Abs(ghost.x[a] - ghost.x[b]) < .002f;
            float since = 0, kickAt = -1, ledgeAt = -1; int lastWallFace = 0;
            for (int f = 0; f < n; f++)
            {
                bool air = ghost.y[f] > .05f && !GhostGrounded(ghost, f);
                // Climbing holds ↑; a jump that only bumps a wall (the air-jump lesson, before walls are taught) does not.
                if (air && (Still(f, f - 1) || Still(f, f + 1)) && Has(ghost.held[f], "ArrowUp"))
                {
                    since = f > 0 && ghostWallClip[f - 1] == WallContactClip ? since + step : 0;
                    ghostWallClip[f] = WallContactClip; ghostWallTime[f] = since; lastWallFace = ghost.face[f]; kickAt = ledgeAt = -1;
                    continue;
                }
                bool leftWall = f > 0 && ghostWallClip[f - 1] == WallContactClip;
                if (leftWall && air && ghost.face[f] == -lastWallFace) kickAt = f * step;
                // Leaving the wall still facing it with ↑ held is the motor's hop over the top: the ledge clip, as the hero.
                else if (leftWall && ghost.face[f] == lastWallFace && Has(ghost.held[f - 1], "ArrowUp")) ledgeAt = (f - 1) * step;
                if (kickAt >= 0 && air && f * step - kickAt < kickSeconds) { ghostWallClip[f] = WallKickClip; ghostWallTime[f] = f * step - kickAt; }
                else kickAt = -1;
                if (ledgeAt >= 0 && f * step - ledgeAt < ledgeSeconds) { ghostWallClip[f] = WallLedgeClip; ghostWallTime[f] = f * step - ledgeAt; }
                else ledgeAt = -1;
            }
        }

        // How far into its punch a frame of a recorded punch is (0..1).
        float PunchProgress(int[] punch, int frame)
        {
            int first = frame; while (first > 0 && punch[first - 1] != 0) first--;
            return Mathf.Clamp01((frame - first) / Ghosts.fps / PunchSeconds);
        }

        // A frame of the hero's current body as the prototype's heroFrame picks it (the dash clip by its own time, the
        // looping clips looped, a punch as the dash frame 45% in).
        Sprite HeroFrame(string clipName, float time, bool punching) => TrackFrame(HeroSet(), clipName, time, punching);
        public CharacterAnimationSet HeroAnimations => HeroSet();

        public static Sprite TrackFrame(CharacterAnimationSet set, string clipName, float time, bool punching)
        {
            if (set == null) return null;
            if (punching) clipName = "dash";
            var clip = set.Find(clipName) ?? set.Find("idle");
            if (clip == null) return null;
            if (clip.sprites == null) clip.Load();
            if (punching) return clip.At(clip.Duration * .45f, false);
            if (clipName == "dash") return clip.At(time / DashSeconds * clip.Duration, false);
            bool loop = clipName == "run" || clipName == "idle" || clipName == "fall_loop" || clipName == "walk";
            return clip.At(time, loop);
        }
        const float DashSeconds = .2f;

        // ---- The village broom (HTML BROOM): a chapter 1 broom stalker the sketch made out of the village broom. It walks
        // at the hero, raises its broom (windup), strikes (a hit within reach knocks the hero back; the opening never
        // kills), rests; three punches bring it down. Game time: the pause menu and the lesson hold it.
        public sealed class BroomTuning { public string art; public int hp; public float walk, near, windup, strike, cool, reach; }
        enum BroomState { Idle, Walk, Windup, Strike, Dying, Gone }
        BroomTuning broomTuning;
        SpriteRenderer broomRenderer;
        BroomState broomState = BroomState.Gone;
        float broomX, broomClock, broomStateAt, broomBorn, broomCool, broomHitUntil;
        int broomFace = -1, broomHp, broomLockFace;
        bool broomActive, broomStruck;
        // Down the moment the last punch lands, while it still collapses: the cut scene after the fight starts at once
        // instead of leaving the hero free to walk about while the broom falls.
        public bool BroomStanding => broomRenderer && broomState != BroomState.Gone && broomState != BroomState.Dying;
        public float BroomX => broomX;
        public int BroomHp => broomHp;
        const float PunchSeconds = .22f, PunchRest = .35f, PunchReach = 1.25f, PunchPush = .8f, PunchStep = 2.5f;

        public void ConfigureBroom(BroomTuning tuning) => broomTuning = tuning;

        public void AppearBroom(float x)
        {
            if (broomTuning == null) { Debug.LogWarning("Opening broom has no tuning"); return; }
            RemoveBroom();
            broomX = x; broomFace = -1; broomHp = broomTuning.hp; broomState = BroomState.Idle; broomActive = false;
            broomClock = 0; broomStateAt = broomBorn = 0; broomCool = 0; broomHitUntil = -1;
            broomRenderer = Renderer("Opening broom", BroomSprite("idle", 0), -9);
            broomRenderer.transform.position = new Vector3(x, GroundTop, 0);
            broomRenderer.color = new Color(1, 1, 1, 0);
        }

        // The fight starts: its first strike comes soon after.
        public void FightBroom() { if (!BroomStanding) return; broomActive = true; broomCool = broomClock + .3f; PunchUnlocked = true; }

        void RemoveBroom()
        {
            if (broomRenderer) Destroy(broomRenderer.gameObject);
            RemoveBroomOutline();
            broomRenderer = null; broomState = BroomState.Gone; broomActive = false;
        }

        Sprite BroomSprite(string clip, int frame) => Resources.Load<Sprite>($"{broomTuning.art}/{clip}/{frame:00}");

        void TickBroom(float dt)
        {
            if (!broomRenderer || broomState == BroomState.Gone || broomTuning == null) return;
            broomClock += dt;
            var b = broomTuning; var player = World.arena.Player;
            float age = broomClock - broomStateAt, alpha = Mathf.Min(1, (broomClock - broomBorn) / .6f);
            if (broomState == BroomState.Dying)
            {
                alpha = Mathf.Max(0, 1 - Mathf.Max(0, age - 1f) / .4f);
                if (age > 1.4f) { RemoveBroom(); return; }
            }
            else if (broomActive)
            {
                float dx = player.Position.x - broomX;
                // A punch landing: one hit per punch, pushed back, three bring it down.
                if (broomClock < punchUntil && !punchHit && Mathf.Abs(broomX - (player.Position.x + punchFace * 1f)) < PunchReach)
                {
                    punchHit = true; broomHp--; broomHitUntil = broomClock + .18f; broomX += punchFace * PunchPush; RequestShake(.1f, 2);
                    SpawnInkSpecks(broomX, punchFace);
                    // The punch was for the broom: once it falls the village has no Q (user 2026-11-10).
                    if (broomHp <= 0) { broomState = BroomState.Dying; broomStateAt = broomClock; broomActive = false; PunchUnlocked = false; PresentationSfx.Play("story-collapse"); }
                }
                if (broomState == BroomState.Windup)
                {
                    broomFace = broomLockFace;
                    if (age >= b.windup) { broomState = BroomState.Strike; broomStateAt = broomClock; broomStruck = false; }
                }
                else if (broomState == BroomState.Strike)
                {
                    // A strike only pushes the hero away (the opening never kills).
                    if (!broomStruck && Mathf.Abs(dx) < b.reach && dx * broomFace > 0 && player.Position.y - GroundTop < 2)
                    {
                        broomStruck = true;
                        player.velocity = new Vector2(broomFace * 9, 9); player.grounded = false;
                        player.moveLockUntil = World.arena.Now + .25f;
                        RequestShake(.18f, 3); PresentationSfx.Play("story-collapse");
                    }
                    if (age >= b.strike) { broomState = BroomState.Idle; broomStateAt = broomClock; broomCool = broomClock + b.cool; }
                }
                else if (broomState != BroomState.Dying)
                {
                    broomFace = dx >= 0 ? 1 : -1;
                    if (Mathf.Abs(dx) > b.near) { if (broomState != BroomState.Walk) { broomState = BroomState.Walk; broomStateAt = broomClock; } broomX += broomFace * b.walk * dt; }
                    else if (broomClock >= broomCool) { broomState = BroomState.Windup; broomStateAt = broomClock; broomLockFace = broomFace; }
                    else broomState = BroomState.Idle;
                }
            }
            age = broomClock - broomStateAt;
            Sprite sprite;
            if (broomState == BroomState.Dying) sprite = BroomSprite("death", Mathf.Min(13, Mathf.FloorToInt(age * 12)));
            else if (broomClock < broomHitUntil) sprite = BroomSprite("hit", 0);
            else if (broomState == BroomState.Windup) sprite = BroomSprite("attack", Mathf.Min(7, Mathf.FloorToInt(age / b.windup * 8)));
            else if (broomState == BroomState.Strike) sprite = BroomSprite("attack", 8 + Mathf.Min(7, Mathf.FloorToInt(age / b.strike * 8)));
            else if (broomState == BroomState.Walk) sprite = BroomSprite("walk", Mathf.FloorToInt(broomClock * 10) % 8);
            else sprite = BroomSprite("idle", Mathf.FloorToInt(broomClock * 10) % 12);
            if (sprite) broomRenderer.sprite = sprite;
            // The pictures face left.
            broomRenderer.flipX = broomFace > 0;
            broomRenderer.transform.position = new Vector3(Mathf.Round(broomX * 32) / 32, GroundTop, 0);
            broomRenderer.color = new Color(1, 1, 1, alpha);
            TickBroomOutline(alpha);
        }

        // The dark broom on the sketched village was hard to see: a white ring of the stations' outline width round it
        // (the interaction outline's silhouette shader, the same picture shifted in every direction behind it).
        SpriteRenderer[] broomOutline;
        void TickBroomOutline(float alpha)
        {
            if (broomOutline == null)
            {
                interactionOutlineMaterial ??= new Material(Resources.Load<Shader>("Hub/InteractionSilhouette"));
                broomOutline = new SpriteRenderer[OutlineSamples];
                for (int i = 0; i < OutlineSamples; i++)
                {
                    var edge = new GameObject("Opening broom outline " + i).AddComponent<SpriteRenderer>();
                    edge.transform.SetParent(transform, false); edge.sharedMaterial = interactionOutlineMaterial;
                    broomOutline[i] = edge;
                }
            }
            for (int i = 0; i < OutlineSamples; i++)
            {
                var edge = broomOutline[i]; float angle = i * Mathf.PI * 2 / OutlineSamples;
                edge.sprite = broomRenderer.sprite; edge.flipX = broomRenderer.flipX;
                edge.sortingLayerID = broomRenderer.sortingLayerID; edge.sortingOrder = broomRenderer.sortingOrder - 1;
                edge.transform.position = broomRenderer.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (OutlinePixels / 32f);
                edge.color = new Color(1, 1, 1, alpha); edge.enabled = true;
            }
        }

        void RemoveBroomOutline()
        {
            if (broomOutline != null) foreach (var edge in broomOutline) if (edge) Destroy(edge.gameObject);
            broomOutline = null;
        }

        // Ink specks off a punched broom (the prototype's ten dark flakes).
        void SpawnInkSpecks(float x, int face)
        {
            var random = new System.Random(Mathf.FloorToInt(Time.time * 1000));
            for (int i = 0; i < 10; i++)
            {
                var r = Renderer("Erase flake", FlakeSprite(), 4);
                r.transform.position = new Vector3(x, GroundTop + 1.1f + ((float)random.NextDouble() - .5f) * 30 / 32f, -.02f);
                r.transform.localScale = new Vector3(2, 2, 1); r.color = new Color32(0x24, 0x20, 0x1c, 255);
                frontFlakes.Add(new Flake { renderer = r, born = Time.time, life = .5f + (float)random.NextDouble() * .4f, wobble = (float)random.NextDouble() * 6,
                    velocity = new Vector2(face * (40 + (float)random.NextDouble() * 80) / 32f, (20 + (float)random.NextDouble() * 40) / 32f) });
            }
        }

        // ---- The bare-handed punch (Q): a short lunge with a white arc; only the broom can be hit. Opened by the fight.
        public bool PunchUnlocked { get; set; }
        float punchUntil = -1, punchReady;
        int punchFace = 1;
        bool punchHit;
        SpriteRenderer punchPose, punchArc;
        public bool Punching => broomClock < punchUntil;

        public bool TryPunch()
        {
            var player = World.arena.Player;
            if (!PunchUnlocked || !player || !player.grounded || broomClock < punchReady || World.arena.Controls.Current.SimulationPaused) return false;
            punchFace = player.facing == 0 ? 1 : player.facing;
            punchUntil = broomClock + PunchSeconds; punchReady = broomClock + PunchRest; punchHit = false;
            player.velocity = new Vector2(punchFace * PunchStep, player.velocity.y);
            return true;
        }

        void TickPunch()
        {
            var player = World.arena.Player; var visual = player ? player.visual : null;
            bool punching = PunchUnlocked && broomClock < punchUntil && visual;
            if (!punching)
            {
                if (punchPose && punchPose.enabled) { punchPose.enabled = false; if (visual && !heroPose && sketchStarted < 0) visual.forceRenderingOff = false; }
                if (punchArc) punchArc.enabled = false;
                return;
            }
            if (!punchPose)
            {
                punchPose = new GameObject("Opening punch pose").AddComponent<SpriteRenderer>();
                punchPose.transform.SetParent(visual.transform, false);
                punchPose.sharedMaterial = visual.sharedMaterial; punchPose.sortingLayerID = visual.sortingLayerID; punchPose.sortingOrder = visual.sortingOrder;
            }
            punchPose.sprite = HeroFrame("dash", 0, true); punchPose.enabled = punchPose.sprite; punchPose.color = visual.color;
            visual.forceRenderingOff = punchPose.enabled;
            float k = 1 - (punchUntil - broomClock) / PunchSeconds;
            DrawPunchArc(punchArc ??= ArcRenderer("Opening punch arc"), player.Position.x, player.Position.y - GroundTop, punchFace, k, 1);
        }

        SpriteRenderer ArcRenderer(string name) { var r = Renderer(name, null, 6); r.enabled = false; return r; }

        // The white arc in front of the fist (HTML drawPunchArc): grows from 12 to 22 px and fades to a fifth.
        static Sprite[] arcSprites;
        void DrawPunchArc(SpriteRenderer r, float x, float y, int face, float k, float alpha)
        {
            arcSprites ??= MakeArcs();
            int step = Mathf.Clamp(Mathf.FloorToInt(k * arcSprites.Length), 0, arcSprites.Length - 1);
            r.sprite = arcSprites[step]; r.flipX = face < 0; r.enabled = true;
            r.transform.position = new Vector3(Mathf.Round((x + face * (.7f + .5f * k)) * 32) / 32, Mathf.Round((GroundTop + y + 1.05f) * 32) / 32, -.03f);
            r.color = new Color(1, 1, 1, alpha * (1 - k * .8f));
        }

        static Sprite[] MakeArcs()
        {
            var sprites = new Sprite[6];
            for (int s = 0; s < sprites.Length; s++)
            {
                const int size = 64; float radius = 12 + 10 * (s / (sprites.Length - 1f));
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f, d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                        if (Mathf.Abs(a) > 1.1f) continue;
                        float off = Mathf.Abs(d - radius);
                        if (off <= .75f) px[y * size + x] = new Color32(0x1f, 0x1b, 0x19, 255);
                        else if (off <= 2f) px[y * size + x] = new Color32(0xff, 0xfa, 0xf0, 255);
                    }
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                t.SetPixels32(px); t.Apply(false, true);
                sprites[s] = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
                sprites[s].hideFlags = HideFlags.DontSave;
            }
            return sprites;
        }

        // ---- The ink running short before the blueprints (HTML inkFlicker): the hero blinks for .9 s.
        float flickerAt = -1;
        public void InkFlicker() => flickerAt = Time.unscaledTime;
        void TickFlicker()
        {
            if (flickerAt < 0) return;
            var visual = World.arena.Player ? World.arena.Player.visual : null;
            float t = Time.unscaledTime - flickerAt;
            if (!visual) { flickerAt = -1; return; }
            var c = visual.color;
            if (t >= .9f) { c.a = 1; visual.color = c; flickerAt = -1; return; }
            c.a = .45f + .55f * Mathf.Abs(Mathf.Cos(t * 14)); visual.color = c;
        }

        // The walking line (the opening's heights are measured from it).
        public float WalkingLine => GroundTop;

        // ---- A station the guide points at keeps its F keycap even from afar (HTML: the record desk before the first record).
        public string MarkedStation { get; set; }

        // ---- The way to the station the guide wants next (playtest 2026-11-06: players may not find the record desk or the
        // weapons). On screen: a 「!」 bubble bobbing over the station and the white interaction outline round it. Off
        // screen: the 「!」 at the screen's edge on its side, at its height, with an ink arrow pointing out towards it.
        public string GuideStation { get; set; }
        // Where the 「!」 stands (NaN while hidden) and whether it is at the screen edge.
        public Vector2 GuideMarkAt { get; private set; } = new Vector2(float.NaN, float.NaN);
        public bool GuideAtEdge { get; private set; }
        SpriteRenderer guideMark, guideArrow;
        SpriteRenderer[] guideOutline;
        static Sprite guideArrowSprite;
        const float GuideEdgeMargin = .9f;
        // Over the hero and the props, as the artisan's 「!」.
        const int GuideOrder = 8;

        void TickGuideMarker()
        {
            var station = string.IsNullOrEmpty(GuideStation) || StationsSuppressed || !InteractionAllowed ? null : Find(GuideStation);
            bool show = station != null && station.Art && World.View;
            GuideMarkAt = new Vector2(float.NaN, float.NaN); GuideAtEdge = false;
            if (!show)
            {
                if (guideMark) guideMark.enabled = false;
                if (guideArrow) guideArrow.enabled = false;
                if (guideOutline != null) foreach (var edge in guideOutline) edge.enabled = false;
                return;
            }
            guideMark ??= Renderer("Guide mark", ExclaimSprite(), GuideOrder);
            guideArrow ??= Renderer("Guide arrow", GuideArrowSprite(), GuideOrder);
            var view = World.View; float halfH = view.orthographicSize, halfW = halfH * view.aspect;
            Vector2 centre = view.transform.position;
            var bounds = station.Art.bounds;
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 4) * 2) / 32f;
            bool inside = bounds.max.x > centre.x - halfW && bounds.min.x < centre.x + halfW;
            Vector2 at;
            if (inside)
            {
                at = new Vector2(station.Position.x, station.Top + .3f + bob);
                guideArrow.enabled = false;
            }
            else
            {
                int side = station.Position.x > centre.x ? 1 : -1;
                float y = Mathf.Clamp(station.Top + .3f, centre.y - halfH + 1, centre.y + halfH - 1.4f);
                at = new Vector2(centre.x + side * (halfW - GuideEdgeMargin - .5f), y + bob);
                guideArrow.enabled = true; guideArrow.flipX = side < 0;
                guideArrow.transform.position = new Vector3(Mathf.Round((at.x + side * (.62f + Mathf.Abs(bob))) * 32) / 32, Mathf.Round((y + .45f) * 32) / 32, 0);
                GuideAtEdge = true;
            }
            // Close enough for its F keycap: the F takes over (the two stacked over the desk).
            guideMark.enabled = inside ? NearestStation != station : true;
            // At the screen edge the bubble drops its tail: the arrow points the way, nothing under it is meant (playtest 2026-11-06).
            guideMark.sprite = ExclaimSprite(inside);
            guideMark.transform.position = new Vector3(Mathf.Round(at.x * 32) / 32, Mathf.Round(at.y * 32) / 32, 0);
            GuideMarkAt = at;
            // The outline ring of the interaction highlight, on the guided station while it is in view.
            if (guideOutline == null)
            {
                interactionOutlineMaterial ??= new Material(Resources.Load<Shader>("Hub/InteractionSilhouette"));
                guideOutline = new SpriteRenderer[OutlineSamples];
                for (int i = 0; i < OutlineSamples; i++)
                {
                    var edge = new GameObject("Guide outline " + i).AddComponent<SpriteRenderer>();
                    edge.transform.SetParent(transform, false); edge.sharedMaterial = interactionOutlineMaterial;
                    guideOutline[i] = edge;
                }
            }
            var art = station.Art;
            for (int i = 0; i < OutlineSamples; i++)
            {
                var edge = guideOutline[i]; float angle = i * Mathf.PI * 2 / OutlineSamples;
                edge.sprite = art.sprite; edge.flipX = art.flipX; edge.flipY = art.flipY;
                edge.sortingLayerID = art.sortingLayerID; edge.sortingOrder = art.sortingOrder - 1;
                edge.transform.rotation = art.transform.rotation; edge.transform.localScale = art.transform.lossyScale;
                edge.transform.position = art.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (OutlinePixels / 32f);
                edge.enabled = inside && art.enabled;
            }
        }

        // A small ink arrow pointing right (flipped for the left edge), with a paper rim.
        static Sprite GuideArrowSprite()
        {
            if (guideArrowSprite) return guideArrowSprite;
            const int w = 12, h = 14;
            var px = new Color32[w * h];
            Color32 ink = new Color32(0x1f, 0x1b, 0x19, 255), paper = new Color32(0xff, 0xff, 0xff, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float half = (h - 1) / 2f, reach = (w - 1) * (1 - Mathf.Abs(y - half) / half);
                    if (x <= reach - 2) px[y * w + x] = ink;
                    else if (x <= reach) px[y * w + x] = paper;
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            t.SetPixels32(px); t.Apply(false, true);
            guideArrowSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            guideArrowSprite.hideFlags = HideFlags.DontSave;
            return guideArrowSprite;
        }

        // ---- The close-up at the well (HTML zoomTo/zoomView): eases to the asked zoom at 1.4 a second, centred on the hero
        // (once the helper is out, halfway between them), 1.6 units above its feet.
        float zoomTo = 1, zoom = 1, zoomX = float.NaN;
        public float CameraZoom => zoom;
        public void ZoomCamera(float to) => zoomTo = Mathf.Max(1, to);
        void TickZoom()
        {
            var player = World.arena.Player; if (!player) return;
            float dt = Time.unscaledDeltaTime, k = Mathf.Min(1, dt * 1.4f);
            zoom += (zoomTo - zoom) * k;
            if (Mathf.Abs(zoom - zoomTo) < .002f) zoom = zoomTo;
            float wantX = HelperShown ? (player.Position.x + helper.transform.position.x) / 2 : player.Position.x;
            zoomX = float.IsNaN(zoomX) ? wantX : zoomX + (wantX - zoomX) * k;
            World.CameraZoom = zoom; World.CameraZoomCenter = new Vector2(zoomX, player.Position.y + 1.6f);
        }

        // Every frame of the opening.
        void TickOpeningGuide()
        {
            float dt = World.arena.Controls.Current.SimulationPaused ? 0 : Time.deltaTime;
            TickGhost(); TickDemos(); TickBroom(dt); TickPunch(); TickFlicker(); TickZoom();
        }

        void EndOpeningGuide()
        {
            StopGhost(); ClearDemos(); RemoveBroom(); PunchUnlocked = false; punchUntil = -1; MarkedStation = null; GuideStation = null; flickerAt = -1;
            zoomTo = 1; FocusCamera(null);
            if (punchPose) Destroy(punchPose.gameObject); punchPose = null;
            if (punchArc) Destroy(punchArc.gameObject); punchArc = null;
            if (ghostArc) Destroy(ghostArc.gameObject); ghostArc = null;
            var visual = World && World.arena.Player ? World.arena.Player.visual : null;
            if (visual) { visual.forceRenderingOff = false; var c = visual.color; c.a = 1; visual.color = c; }
        }
    }
}
