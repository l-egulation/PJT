using System;
using Deviant.Combat;
using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // Tunable numbers for loot thrown out of a defeated monster. Source: data/presentation/loot_drop_toss.json.
    [Serializable]
    public sealed class LootTossSettings
    {
        public int schema = 1, seed = 7331, bounces = 2;
        public float launchLift = .8f, apexHeight = 1.1f, flightSeconds = .46f;
        public float minSpread = .35f, spreadJitter = .18f, heightJitter = .2f, timeJitter = .12f;
        public float bounceHeight = .22f, bounceDamping = .42f, bounceDrift = .14f;
        public float maxFallBelowSource = 2.5f, collectDelay = .05f;

        static LootTossSettings current;
        public static LootTossSettings Current
        {
            get
            {
                if (current != null) return current;
                var text = Resources.Load<TextAsset>("LootDropToss");
                current = text ? JsonUtility.FromJson<LootTossSettings>(text.text) : new LootTossSettings();
                current.Validate();
                return current;
            }
        }
        // Tests and tuning tools swap numbers here; null reloads the shipped file.
        public static void Override(LootTossSettings settings) { current = settings; current?.Validate(); }

        public void Validate()
        {
            if (schema != 1) throw new InvalidOperationException("LootDropToss schema must be 1.");
            bounces = Mathf.Clamp(bounces, 0, 4);
            flightSeconds = Mathf.Max(.1f, flightSeconds);
            apexHeight = Mathf.Max(.1f, apexHeight);
            bounceDamping = Mathf.Clamp01(bounceDamping);
            heightJitter = Mathf.Clamp(heightJitter, 0, .9f);
            timeJitter = Mathf.Clamp(timeJitter, 0, .9f);
            bounceHeight = Mathf.Max(0, bounceHeight); bounceDrift = Mathf.Max(0, bounceDrift);
            minSpread = Mathf.Max(0, minSpread); spreadJitter = Mathf.Max(0, spreadJitter);
            collectDelay = Mathf.Max(0, collectDelay);
        }
    }

    // One drop's path: out of the monster's body on a parabola, a few shrinking bounces, then rest.
    // The rest point is the only place the pickup can be collected, so the caller gates collection on Duration.
    public sealed class LootToss
    {
        public Vector2 Launch { get; private set; }
        public Vector2 Touchdown { get; private set; }
        public Vector2 Rest { get; private set; }
        public float Apex { get; private set; }
        public float FlightSeconds { get; private set; }
        public float Duration { get; private set; }
        public int Bounces => heights.Length;
        float gravity, launchSpeed;
        float[] heights = Array.Empty<float>(), durations = Array.Empty<float>();

        static int lastKey, sequence; static float lastTime = float.NaN;

        // Several drops from one kill share a source and a time; each gets the next index so the spread differs
        // between them but repeats exactly for the same kill.
        public static int NextIndex(Vector2 source, float now)
        {
            int key = Hash(Mathf.RoundToInt(source.x * 32), Mathf.RoundToInt(source.y * 32), 0);
            if (key == lastKey && Mathf.Approximately(now, lastTime)) return ++sequence;
            lastKey = key; lastTime = now; return sequence = 0;
        }

        public Vector2 Evaluate(float elapsed)
        {
            if (elapsed >= Duration) return Rest;
            if (elapsed < FlightSeconds)
            {
                float x = Mathf.Lerp(Launch.x, Touchdown.x, elapsed / FlightSeconds);
                return new Vector2(x, Launch.y + launchSpeed * elapsed - .5f * gravity * elapsed * elapsed);
            }
            float local = elapsed - FlightSeconds, bounceTotal = Duration - FlightSeconds;
            float roll = bounceTotal > 0 ? 1 - (1 - local / bounceTotal) * (1 - local / bounceTotal) : 1;
            float px = Mathf.Lerp(Touchdown.x, Rest.x, roll);
            for (int i = 0; i < heights.Length; i++)
            {
                if (local < durations[i])
                {
                    float speed = Mathf.Sqrt(2 * gravity * heights[i]);
                    return new Vector2(px, Rest.y + Mathf.Max(0, speed * local - .5f * gravity * local * local));
                }
                local -= durations[i];
            }
            return Rest;
        }

        public static Vector2 Snap(Vector2 at) => new Vector2(Mathf.Round(at.x * 32), Mathf.Round(at.y * 32)) / 32;

        // source: the monster's feet. rest: where the pickup's centre would lie on the floor. halfHeight: centre above floor.
        public static LootToss Plan(CombatArena arena, Vector2 source, Vector2 rest, float halfHeight, int index, LootTossSettings s = null)
        {
            s ??= LootTossSettings.Current;
            var random = new System.Random(Hash(s.seed, index, Hash(Mathf.RoundToInt(source.x * 32), Mathf.RoundToInt(source.y * 32), 1)));
            float Range(float half) => ((float)random.NextDouble() * 2 - 1) * half;

            float baseFloor = rest.y - halfHeight;
            float dx = rest.x - source.x;
            float dir = Mathf.Abs(dx) > .05f ? Mathf.Sign(dx) : (index % 2 == 0 ? 1 : -1) * (random.NextDouble() < .5 ? 1 : -1);
            float extra = dir * Mathf.Max(0, s.minSpread - Mathf.Abs(dx)) + Range(s.spreadJitter);
            var feet = new Vector2(rest.x, baseFloor);
            if (arena) feet = SafeFloor(arena, feet, arena.DropPoint(feet, extra, 2 * halfHeight), baseFloor, s.maxFallBelowSource);
            else feet.x += extra;
            dir = Mathf.Abs(feet.x - source.x) > .05f ? Mathf.Sign(feet.x - source.x) : dir;

            var toss = new LootToss { Rest = new Vector2(feet.x, feet.y + halfHeight) };
            var touchdownFeet = feet;
            if (s.bounces > 0 && s.bounceDrift > 0)
                touchdownFeet = arena ? SafeFloor(arena, feet, arena.DropPoint(feet, -dir * s.bounceDrift, 2 * halfHeight), feet.y, 0) : feet - Vector2.right * dir * s.bounceDrift;
            touchdownFeet.y = feet.y;
            toss.Touchdown = new Vector2(touchdownFeet.x, feet.y + halfHeight);
            toss.Launch = source + Vector2.up * s.launchLift;
            toss.FlightSeconds = s.flightSeconds * (1 + Range(s.timeJitter));
            float apex = s.apexHeight * (1 + Range(s.heightJitter));
            if (arena)
            {
                float room = Headroom(arena, toss.Launch, apex + halfHeight);
                apex = Mathf.Min(apex, room - halfHeight - .05f);
            }
            float drop = toss.Touchdown.y - toss.Launch.y;
            apex = Mathf.Max(apex, drop + .1f, .1f);
            toss.Shape(apex, s);
            // A ledge between body and floor: flatten the arc, and if it still clips, pop up in place instead.
            for (int tries = 0; arena && tries < 3 && toss.Clips(arena); tries++) toss.Shape(Mathf.Max(drop + .1f, .1f, toss.Apex * .5f), s);
            if (arena && toss.Clips(arena))
            {
                toss.Launch = toss.Touchdown; toss.Shape(Mathf.Max(.1f, s.apexHeight * .5f), s);
            }
            return toss;
        }

        void Shape(float apex, LootTossSettings s)
        {
            Apex = apex;
            float drop = Touchdown.y - Launch.y;
            float rise = Mathf.Sqrt(2 * apex), fall = Mathf.Sqrt(2 * (apex - drop));
            gravity = (rise + fall) * (rise + fall) / (FlightSeconds * FlightSeconds);
            launchSpeed = Mathf.Sqrt(2 * gravity * apex);
            int count = s.bounceHeight > 0 ? s.bounces : 0;
            heights = new float[count]; durations = new float[count];
            float total = FlightSeconds;
            for (int i = 0; i < count; i++)
            {
                heights[i] = s.bounceHeight * Mathf.Pow(s.bounceDamping, i);
                durations[i] = 2 * Mathf.Sqrt(2 * heights[i] / gravity);
                total += durations[i];
            }
            Duration = total;
        }

        bool Clips(CombatArena arena)
        {
            for (int i = 1; i < 12; i++)
                if (Solid(Evaluate(FlightSeconds * i / 12f))) return true;
            return false;
        }

        static bool Solid(Vector2 at)
        {
            foreach (var hit in Physics2D.OverlapPointAll(at))
                if (hit && hit.GetComponent<ArenaSurface>() is ArenaSurface surface && !surface.oneWay) return true;
            return false;
        }

        static float Headroom(CombatArena arena, Vector2 from, float distance)
        {
            float room = distance;
            foreach (var hit in Physics2D.RaycastAll(from, Vector2.up, distance))
                if (hit.collider.GetComponent<ArenaSurface>() is ArenaSurface surface && !surface.oneWay && hit.normal.y < -.5f)
                    room = Mathf.Min(room, hit.distance);
            return room;
        }

        // Walks back toward the safe point until the candidate stands on floor no deeper than the allowed fall.
        static Vector2 SafeFloor(CombatArena arena, Vector2 safe, Vector2 candidate, float referenceFloor, float maxFall)
        {
            for (int step = 0; step <= 8; step++)
            {
                var at = Vector2.Lerp(candidate, safe, step / 8f);
                var floor = arena.FindFloor(new Vector2(at.x, referenceFloor + .5f), 64);
                if (floor.HasValue && floor.Value >= referenceFloor - maxFall - .001f && floor.Value <= referenceFloor + .5f)
                    return new Vector2(at.x, floor.Value);
            }
            return safe;
        }

        static int Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)a) * 16777619; h = (h ^ (uint)b) * 16777619; h = (h ^ (uint)c) * 16777619;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
