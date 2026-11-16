using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // The erasure of the playable opening (docs/design/story/opening/opening-playable.md part 2): a front comes in from the left and
    // turns what it passes into sketch, piece by piece (the ground a little ahead of it, objects each a little behind), then
    // chases the hero down the road. It surges and stutters instead of gliding, stops for a moment whenever it swallows an
    // object, and is pulled faster when the hero gets far ahead, so it always stays on screen. If it reaches the hero (or the
    // hero falls into a pit of the chase road) the GUI fades out and RespawnAtCheckpoint starts the chase again. At the well
    // a second front comes in from the right and both close on the hero. EndOpening clears all of it.
    public sealed partial class StartingVillageWorld
    {
        // Tuning from the opening's beat file (its "chase" block); speeds are (fromX, speed), surge steps (seconds, rate).
        public sealed class ChaseTuning
        {
            public float checkpointX, checkpointBack, catchMargin, groundLead, objectLagMin, objectLagMax, slow;
            public float leashFar, leashK, leashMax, biteHold, biteShake, biteAmp;
            public Vector2[] speeds, surge;
        }

        enum Front { None, Chase, Close, Met }

        ChaseTuning chase;
        Front front;
        float erasureLeft = float.NaN, erasureRight = float.NaN;
        bool frontPaused, erasureSky;
        float holdAt = float.NaN, slowFromX = float.NaN, slowRate = 1, graceLeft, biteLeft, frontClock;
        float closeLeftSpeed, closeRightSpeed, meetX;
        bool biteShakePending;
        readonly List<float> biteAt = new List<float>();
        int nextBite;
        string caught;
        bool caughtReported;

        public bool ErasureActive => front != Front.None;
        public bool ErasureMet { get; private set; }
        public float ErasureLeft => erasureLeft;
        // The front chases (and can catch) the hero: after the release, before the fronts close in at the well.
        public bool ErasureChasing => front == Front.Chase && float.IsNaN(holdAt);
        // "erase" or "pit" from the moment the hero is caught until RespawnAtCheckpoint.
        public string Caught => caught;

        public void ConfigureChase(ChaseTuning tuning) => chase = tuning;

        // The front appears `behind` units left of the hero (not left of the road), still, with the hero turned to look.
        // The chase music starts with the colour drain, not with the run that follows it.
        // With rollIn seconds the front sweeps in from RollInDistance further left over that time instead of appearing at
        // once (playtest 2026-11-06: it popped in); the hero turns only when the script says so.
        public void StartErasureChase(float behind, float hold, float slowFrom, float rollIn = 0)
        {
            OpeningMusicCue = "opening_erase";
            var player = World.arena.Player;
            front = Front.Chase; ErasureMet = false; erasureSky = false;
            erasureLeft = Mathf.Max(courseLeftX, player.Position.x - behind); erasureRight = float.NaN;
            rollInTarget = float.NaN;
            if (rollIn > 0)
            {
                rollInTarget = erasureLeft; erasureLeft = Mathf.Max(courseLeftX, erasureLeft - RollInDistance);
                rollInSpeed = (rollInTarget - erasureLeft) / rollIn;
                if (rollInSpeed <= 0) rollInTarget = float.NaN;
            }
            frontPaused = true; holdAt = hold; slowFromX = slowFrom; slowRate = 1; graceLeft = biteLeft = frontClock = 0;
            caught = null; caughtReported = false;
            if (rollIn <= 0) player.facing = -1;
            CollectBites();
            foreach (var piece in paired) piece.erased = -1;
            ApplyErasure();
        }

        public void GoErasure() { frontPaused = false; if (!float.IsNaN(rollInTarget)) { erasureLeft = Mathf.Max(erasureLeft, rollInTarget); rollInTarget = float.NaN; } }
        const float RollInDistance = 12;
        float rollInTarget = float.NaN, rollInSpeed;
        public bool ErasureRollingIn => !float.IsNaN(rollInTarget);

        // The front stops waiting at its hold and chases after a grace time; the hero turns to run.
        public void ReleaseErasure(float graceSeconds)
        {
            holdAt = float.NaN; frontPaused = false; graceLeft = graceSeconds; slowRate = 1;
            World.arena.Player.facing = 1;
        }

        // At the well: a second front comes in rightAhead units right of the hero and both drift in; nothing catches.
        public void CloseErasure(float rightAhead, float rightSpeed, float leftSpeed)
        {
            if (front == Front.None) return;
            front = Front.Close; meetX = World.arena.Player.Position.x;
            erasureRight = meetX + rightAhead; closeRightSpeed = rightSpeed; closeLeftSpeed = leftSpeed;
            ApplyErasure();
        }

        // Both fronts reach the hero within seconds.
        public void MeetErasure(float seconds)
        {
            if (front != Front.Close) return;
            closeRightSpeed = (erasureRight - meetX) / seconds;
            closeLeftSpeed = Mathf.Max(2, (meetX - erasureLeft) / seconds);
        }

        // The GUI reads a catch once and plays it out before calling RespawnAtCheckpoint.
        public string TakeCaught()
        {
            if (caught == null || caughtReported) return null;
            caughtReported = true; return caught;
        }

        // True once after a bite: the front lets go of what it swallowed with a short, strong shake.
        public bool TakeBite(out float seconds, out float amplitude)
        {
            seconds = chase?.biteShake ?? 0; amplitude = chase?.biteAmp ?? 0;
            if (!biteShakePending) return false;
            biteShakePending = false; return true;
        }

        // The only checkpoint of the chase: the hero stands at checkpointX and the front starts checkpointBack behind.
        public void RespawnAtCheckpoint()
        {
            var player = World.arena.Player;
            player.Position = new Vector2(chase.checkpointX, GroundTop); player.velocity = Vector2.zero; player.facing = 1;
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            World.RefreshCamera(0, true);
            erasureLeft = chase.checkpointX - chase.checkpointBack; frontClock = biteLeft = graceLeft = 0; biteShakePending = false;
            nextBite = FirstBiteAfter(erasureLeft);
            caught = null; caughtReported = false;
            heroTint = Color.white; tintStarted = -1;
            if (player.visual) player.visual.color = Color.white;
            ApplyErasure();
        }

        // Game time: the pause menu and the guide cards hold the fronts.
        void TickErasure(float dt)
        {
            if (front == Front.None || front == Front.Met || caught != null) return;
            var player = World.arena.Player; float x = player.Position.x;
            if (front == Front.Close)
            {
                erasureLeft += closeLeftSpeed * dt; erasureRight -= closeRightSpeed * dt;
                if (erasureRight - erasureLeft <= .05f)
                {
                    erasureLeft = erasureRight = meetX; ErasureMet = erasureSky = true; front = Front.Met;
                    // The colour twins leave with the met fronts: draw every piece again.
                    foreach (var piece in paired) piece.erased = -1;
                }
                ApplyErasure();
                return;
            }
            if (frontPaused && !float.IsNaN(rollInTarget))
            {
                erasureLeft = Mathf.Min(rollInTarget, erasureLeft + rollInSpeed * dt);
                if (erasureLeft >= rollInTarget) rollInTarget = float.NaN;
                ApplyErasure();
                return;
            }
            if (frontPaused || chase == null) return;
            if (graceLeft > 0) { graceLeft -= dt; return; }
            if (biteLeft > 0) { biteLeft -= dt; if (biteLeft <= 0) biteShakePending = true; return; }
            bool held = !float.IsNaN(holdAt);
            // Slower while it covers the bystanders it is held for, so their fright can be read.
            float target = held && !float.IsNaN(slowFromX) && erasureLeft > slowFromX ? chase.slow : 1;
            slowRate += (target - slowRate) * Mathf.Min(1, dt * 8);
            float step = dt * slowRate;
            frontClock += step;
            float pull = held ? 0 : Mathf.Max(0, x - erasureLeft - chase.leashFar) * chase.leashK;
            erasureLeft += Mathf.Min(chase.leashMax, SpeedAt(x) * SurgeAt(frontClock) + pull) * step;
            if (held) erasureLeft = Mathf.Min(erasureLeft, holdAt);
            if (nextBite < biteAt.Count && erasureLeft >= biteAt[nextBite]) { nextBite++; biteLeft = chase.biteHold; }
            ApplyErasure();
            if (!held && x < erasureLeft + chase.catchMargin) Catch("erase");
        }

        void Catch(string kind)
        {
            if (caught != null) return;
            caught = kind; caughtReported = false;
            var player = World.arena.Player; player.velocity = new Vector2(0, Mathf.Min(0, player.velocity.y));
        }

        float SpeedAt(float x)
        {
            float speed = chase.speeds[0].y;
            foreach (var zone in chase.speeds) if (x >= zone.x) speed = zone.y;
            return speed;
        }

        // The surge pattern's time average is 1, so the zone speed stays the average speed.
        float SurgeAt(float clock)
        {
            float period = 0; foreach (var s in chase.surge) period += s.x;
            float t = Mathf.Repeat(clock, period);
            foreach (var s in chase.surge) { if (t < s.x) return s.y; t -= s.x; }
            return 1;
        }

        // Where the front stands when each object turns: walls, platforms, props, buildings and the bystanders.
        void CollectBites()
        {
            biteAt.Clear();
            if (chase != null)
            {
                foreach (var piece in paired)
                    if (piece.renderer && !Backdrop(piece) && !Ground(piece)) biteAt.Add(piece.renderer.transform.position.x - Offset(piece));
                foreach (var centre in courseBlocks) biteAt.Add(centre - chase.groundLead);
                foreach (var resident in residents) biteAt.Add(resident.x + resident.lag);
                biteAt.Sort();
            }
            nextBite = FirstBiteAfter(erasureLeft);
        }

        int FirstBiteAfter(float x)
        {
            int i = 0; while (i < biteAt.Count && biteAt[i] <= x) i++;
            return i;
        }

        static bool Backdrop(Piece piece) { string name = piece.renderer.name; return name.StartsWith("Hub sky") || name.StartsWith("Hub background"); }
        static bool Ground(Piece piece) { string name = piece.renderer.name; return name == "Ground" || name == "Ground body" || name == "Opening course platform"; }

        // How far ahead of the front (positive) or behind it (negative) a piece turns: the ground leads, each object lags
        // by its own amount, so the village does not turn as one screen.
        float Offset(Piece piece)
        {
            if (chase == null) return 0;
            if (!float.IsNaN(piece.erasureOffset)) return piece.erasureOffset;
            return piece.erasureOffset = Ground(piece) ? chase.groundLead : -Lag(piece.renderer.name, piece.renderer.transform.position.x);
        }

        float Lag(string id, float x)
        {
            uint h = 7;
            foreach (char c in id + Mathf.Round(x * 100)) h = (h * 31 + c) % 9973;
            return Mathf.Lerp(chase.objectLagMin, chase.objectLagMax, h / 9973f);
        }

        // Overrides a piece's colour while a front is out; the sky and far layers keep their colour until the fronts meet.
        bool? ErasedOverride(Piece piece)
        {
            if (float.IsNaN(erasureLeft)) return null;
            if (ErasureMet) return true;
            // Drawn per pixel by the colour twin over the sketch, the sky and far rows too (StartingVillageWorld.EraseColour;
            // playtest 2026-11-04: the whole screen drains, not just the street).
            if (EraseMaterial()) return true;
            if (Backdrop(piece)) return erasureSky;
            float x = piece.colorSprite && !piece.keepTransform ? piece.column + .5f : piece.renderer.transform.position.x, off = Offset(piece);
            return x <= erasureLeft + off || (!float.IsNaN(erasureRight) && x >= erasureRight - off);
        }

        // Only the pieces that changed since the last frame get a new sprite.
        void ApplyErasure()
        {
            foreach (var piece in paired)
            {
                if (!piece.renderer) continue;
                bool sketch = ErasedOverride(piece) ?? Preview ?? !IsColoured(piece.region);
                if (piece.erased == (sketch ? 1 : 0)) continue;
                piece.erased = sketch ? 1 : 0;
                ApplyPiece(piece, sketch);
            }
        }

        // Whether the village is drawn in sketch at x right now (erasure fronts, previews, then the owned unlocks).
        public bool SketchAt(float x)
        {
            if (!float.IsNaN(erasureLeft)) return ErasureMet || x <= erasureLeft || (!float.IsNaN(erasureRight) && x >= erasureRight);
            if (Preview.HasValue) return Preview.Value;
            if (BlotColoured(x)) return false;
            foreach (var region in coloured)
                if (IsColoured(region) && regionSpans.TryGetValue(region, out var span) && x >= span.x && x <= span.y) return false;
            return true;
        }

        void ClearErasure()
        {
            front = Front.None; erasureLeft = erasureRight = float.NaN; erasureSky = false; ErasureMet = false;
            foreach (var piece in paired) RestorePieceMaterial(piece);
            frontPaused = false; holdAt = slowFromX = float.NaN; caught = null; caughtReported = false; biteShakePending = false;
            biteAt.Clear();
            foreach (var piece in paired) { piece.erased = -1; piece.erasureOffset = float.NaN; }
            ClearFrontFlakes();
        }
    }
}
