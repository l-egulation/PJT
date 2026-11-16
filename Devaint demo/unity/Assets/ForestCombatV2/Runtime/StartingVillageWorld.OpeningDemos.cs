using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // The learning road's demonstrations (user request 2026-11-10): instead of a lesson that stops the game, a recorded
    // ghost plays its move over and over where it was recorded (the wall climb, the dash through a shot) while the player
    // walks the road freely; the player finds it already playing. The GUI puts its keys over it.
    public sealed partial class StartingVillageWorld
    {
        public sealed class GhostDemo
        {
            public GhostTrack Track { get; internal set; }
            public string[] Keys { get; internal set; }
            public string Text { get; internal set; }
            // The frame on screen (-1 before the move starts) and the ghost's alpha (0..1 of its full alpha).
            public int Frame { get; internal set; } = -1;
            public float Visibility { get; internal set; }
            // True after the move's last frame, while the last pose holds and fades.
            public bool Holding { get; internal set; }
            // How many times the move has played through.
            public int Plays { get; internal set; }
            public Vector2 Feet { get; internal set; }
            public string WallClip => Frame < 0 || wallClip == null ? null : wallClip[Frame];
            public bool ShotShown => shot && shot.enabled;
            public Vector2 ShotAt => shot ? (Vector2)shot.transform.position : Vector2.zero;
            public Sprite Sprite => renderer ? renderer.sprite : null;
            internal SpriteRenderer renderer, shot, arc, ring;
            public bool RingShown => ring && ring.enabled;
            internal string[] wallClip;
            internal float[] wallTime;
            internal float clock;
        }

        readonly List<GhostDemo> demos = new List<GhostDemo>();
        public IReadOnlyList<GhostDemo> Demos => demos;
        // Seconds of one play: fade in on the first frame, the move, the last pose held while it fades, a moment unseen.
        const float DemoGap = .35f;

        public GhostDemo PlayDemo(string id, string[] keys, string text)
        {
            var track = FindGhost(id);
            if (track == null || track.x == null || track.x.Length == 0) { Debug.LogWarning("Opening demonstration ghost missing: " + id); return null; }
            var visual = World.arena.Player.visual;
            var demo = new GhostDemo { Track = track, Keys = keys ?? new string[0], Text = text };
            FindGhostWallFrames(track, out demo.wallClip, out demo.wallTime);
            demo.renderer = new GameObject("Opening demonstration ghost " + id).AddComponent<SpriteRenderer>();
            demo.renderer.transform.SetParent(transform, false);
            demo.renderer.sharedMaterial = visual.sharedMaterial; demo.renderer.sortingLayerID = visual.sortingLayerID;
            demo.renderer.sortingOrder = visual.sortingOrder - 1; demo.renderer.enabled = false;
            if (track.shotTo > track.shotFrom && !string.IsNullOrEmpty(track.shotArt))
            {
                var art = ShotSprite(track.shotArt);
                if (!art) Debug.LogWarning("Opening demonstration shot art missing: " + track.shotArt);
                else
                {
                    demo.shot = new GameObject("Opening demonstration shot " + id).AddComponent<SpriteRenderer>();
                    demo.shot.transform.SetParent(transform, false);
                    // A plain sprite material: the hero's own draws the hero's picture whatever the sprite (the shot came out as the hero).
                    demo.shot.sprite = art; demo.shot.sharedMaterial = World.arena.spriteMaterial; demo.shot.sortingLayerID = visual.sortingLayerID;
                    demo.shot.sortingOrder = visual.sortingOrder + 1; demo.shot.enabled = false;
                    demo.shot.transform.localScale = new Vector3(track.shotScale, track.shotScale, 1);
                }
            }
            demos.Add(demo);
            TickDemo(demo, 0);
            return demo;
        }

        public void ClearDemos()
        {
            foreach (var demo in demos)
            {
                if (demo.renderer) Destroy(demo.renderer.gameObject);
                if (demo.shot) Destroy(demo.shot.gameObject);
                if (demo.arc) Destroy(demo.arc.gameObject);
                if (demo.ring) Destroy(demo.ring.gameObject);
            }
            demos.Clear();
            OpenDodgeGate(); // A deliberate opening skip must also release the route for the chase.
            ClearPracticeShots();
        }

        // Real time, held while the game is paused (the pause menu).
        void TickDemos()
        {
            float dt = World.arena.Controls.Current.SimulationPaused ? 0 : Time.unscaledDeltaTime;
            foreach (var demo in demos) TickDemo(demo, dt);
            TickPracticeShots(dt);
        }

        // ---- Practice shots at the dodge demonstration (user 2026-11-10: the player should try it, not only watch): while
        // the hero stands on the dodge's stretch of road, a real ink ball flies at it from where the ghost's shot starts,
        // every PracticeEvery seconds (the first a moment after it walks in). It knocks the hero back as the broom does
        // unless the hero dashes through it (the dash is invulnerable); the opening never kills.
        sealed class PracticeShot { public SpriteRenderer renderer; public float x, y; public bool passed; }
        readonly List<PracticeShot> practiceShots = new List<PracticeShot>();
        float practiceClock, practiceNext;
        const float PracticeEvery = 1.8f, PracticeFirst = .6f, PracticeSpeed = 9, PracticeRadius = .25f, PracticeZone = 24;
        public int PracticeShotsFlying => practiceShots.Count;
        public int PracticeHits { get; private set; }
        public int PracticeDodges { get; private set; }

        void TickPracticeShots(float dt)
        {
            var dodge = demos.Find(d => d.Track.shotTo > d.Track.shotFrom);
            var player = World.arena.Player;
            if (dodge == null || !player || dodgeGateOpen) { ClearPracticeShots(); return; }
            var track = dodge.Track;
            float right = track.shotX, left = right - PracticeZone, feet = player.Position.y - GroundTop;
            bool inZone = player.Position.x > left && player.Position.x < right - .5f && feet < 3 && !player.Dead;
            practiceClock += dt;
            if (!inZone) practiceNext = Mathf.Max(practiceNext, practiceClock + PracticeFirst);
            else if (practiceClock >= practiceNext && dt > 0)
            {
                practiceNext = practiceClock + PracticeEvery;
                var r = Renderer("Practice shot", ShotSprite(InkBallArt), player.visual ? player.visual.sortingOrder + 1 : 6);
                practiceShots.Add(new PracticeShot { renderer = r, x = right, y = GroundTop + track.shotY });
            }
            for (int i = practiceShots.Count - 1; i >= 0; i--)
            {
                var shot = practiceShots[i];
                float previousX = shot.x;
                shot.x -= PracticeSpeed * dt;
                shot.renderer.transform.position = new Vector3(Mathf.Round(shot.x * 32) / 32, Mathf.Round(shot.y * 32) / 32, 0);
                var c = player.Center;
                bool touching = shot.x < c.x + player.size.x / 2 + PracticeRadius && previousX > c.x - player.size.x / 2 - PracticeRadius && Mathf.Abs(shot.y - c.y) < player.size.y / 2 + PracticeRadius;
                if (touching && !shot.passed)
                {
                    if (player.Dashing && player.Invulnerable) { shot.passed = true; PracticeDodges++; OpenDodgeGate(); }
                    else if (player.Invulnerable) continue;
                    else
                    {
                        PracticeHits++;
                        player.velocity = new Vector2(-6, 7); player.grounded = false; player.moveLockUntil = World.arena.Now + .25f;
                        RequestShake(.12f, 2); PresentationSfx.Play("story-collapse"); SpawnInkSpecks(shot.x, -1);
                        Destroy(shot.renderer.gameObject); practiceShots.RemoveAt(i); continue;
                    }
                }
                if (shot.x < left - 4) { Destroy(shot.renderer.gameObject); practiceShots.RemoveAt(i); }
            }
        }

        readonly List<GameObject> dodgeGateParts = new List<GameObject>();
        bool dodgeGateBuilt, dodgeGateOpen;
        float dodgeGateX, dodgeGateWidth, dodgeGateTop;
        public bool DodgeGateOpen => dodgeGateOpen;
        public bool InDodgePractice => !dodgeGateOpen && demos.Exists(d => d.Track.shotTo > d.Track.shotFrom
            && World.arena.Player.Position.x > d.Track.shotX - PracticeZone
            && World.arena.Player.Position.x < d.Track.shotX && World.arena.Player.Position.y < GroundTop + 3);

        void BuildDodgeGate(float x, float width, float top)
        {
            dodgeGateX = x; dodgeGateWidth = width; dodgeGateTop = top; dodgeGateBuilt = true; dodgeGateOpen = false;
            int objects = courseObjects.Count, art = courseArt.Count;
            Wall(x, width, 0, top);
            dodgeGateParts.AddRange(courseObjects.GetRange(objects, courseObjects.Count - objects));
            foreach (var renderer in courseArt.GetRange(art, courseArt.Count - art)) dodgeGateParts.Add(renderer.gameObject);
        }

        void OpenDodgeGate()
        {
            if (!dodgeGateBuilt || dodgeGateOpen) return;
            dodgeGateOpen = true;
            foreach (var part in dodgeGateParts) if (part) part.SetActive(false);
            paired.RemoveAll(p => p.renderer && dodgeGateParts.Contains(p.renderer.gameObject));
            Wall(dodgeGateX, dodgeGateWidth, 4, dodgeGateTop);
            Apply(); Physics2D.SyncTransforms();
        }

        void ClearPracticeShots()
        {
            foreach (var shot in practiceShots) if (shot.renderer) Destroy(shot.renderer.gameObject);
            practiceShots.Clear();
        }

        // On the wall the ghost's stamina ring empties over its head as the hero's does (WallStaminaRing).
        void TickDemoRing(GhostDemo demo, int frame, float alpha)
        {
            float stamina = World.arena.Movement.WallStaminaDuration;
            bool on = stamina > 0 && demo.wallClip?[frame] == WallContactClip && alpha > 0;
            if (!on) { if (demo.ring) demo.ring.enabled = false; return; }
            if (!demo.ring)
            {
                demo.ring = new GameObject("Opening demonstration stamina ring").AddComponent<SpriteRenderer>();
                demo.ring.transform.SetParent(transform, false);
                demo.ring.sharedMaterial = World.arena.spriteMaterial; demo.ring.sortingLayerID = demo.renderer.sortingLayerID;
                demo.ring.sortingOrder = demo.renderer.sortingOrder + 3;
            }
            var hero = World.arena.Player;
            float top = hero ? hero.offset.y + hero.size.y / 2 : 2;
            demo.ring.sprite = Deviant.Combat.StaminaRing.For(1 - demo.wallTime[frame] / stamina); demo.ring.enabled = true;
            demo.ring.color = new Color(1, 1, 1, Mathf.Min(1, alpha / .7f));
            demo.ring.transform.position = new Vector3(Mathf.Round(demo.Track.x[frame] * 32) / 32, Mathf.Round((GroundTop + demo.Track.y[frame] + top + Deviant.Combat.WallStaminaRing.Above) * 32) / 32, 0);
        }

        void TickDemo(GhostDemo demo, float dt)
        {
            var file = Ghosts; var track = demo.Track;
            // The dodge is shown in the top demonstration card, never over the real practice shot.
            if (track.shotTo > track.shotFrom) { demo.renderer.enabled = false; if (demo.shot) demo.shot.enabled = false; return; }
            float move = (track.x.Length - 1) / file.fps, cycle = file.fade + move + file.endHold + DemoGap;
            demo.clock += dt;
            if (demo.clock >= cycle) { demo.clock -= cycle; demo.Plays++; }
            float t = demo.clock - file.fade;
            int frame = Mathf.Clamp(Mathf.FloorToInt(t * file.fps), 0, track.x.Length - 1);
            float alpha = t < 0 ? Mathf.Clamp01(demo.clock / file.fade) : t <= move ? 1 : Mathf.Max(0, 1 - (t - move) / file.endFade);
            demo.Frame = t < 0 ? -1 : frame; demo.Holding = t > move; demo.Visibility = alpha;
            demo.Feet = new Vector2(track.x[frame], GroundTop + track.y[frame]);
            DrawGhostFrame(demo.renderer, track, demo.wallClip, demo.wallTime, frame, demo.Holding ? t - move : -1, file.alpha * alpha, ref demo.arc);
            if (alpha <= 0) demo.renderer.enabled = false;
            TickDemoRing(demo, frame, file.alpha * alpha);
            if (demo.shot)
            {
                // The shot flies from shotFrom to shotTo, appearing over 6 frames and fading over its last 10.
                int f = Mathf.FloorToInt(t * file.fps);
                bool flying = t >= 0 && f >= track.shotFrom && f <= track.shotTo;
                demo.shot.enabled = flying;
                if (flying)
                {
                    float shotAlpha = Mathf.Min(1, (f - track.shotFrom + 1) / 6f, (track.shotTo - f + 1) / 10f);
                    float x = track.shotX + track.shotSpeed * (f - track.shotFrom) / file.fps;
                    demo.shot.transform.position = new Vector3(Mathf.Round(x * 32) / 32, Mathf.Round((GroundTop + track.shotY) * 32) / 32, 0);
                    demo.shot.color = new Color(1, 1, 1, Mathf.Clamp01(shotAlpha) * .9f);
                }
            }
        }
    }
}
