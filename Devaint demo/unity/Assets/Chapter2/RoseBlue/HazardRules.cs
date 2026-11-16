using System;
using System.Collections.Generic;

namespace Deviant.Chapter2.RoseBlue
{
    public readonly struct Point
    {
        public readonly double X, Y;
        public Point(double x, double y) { X = x; Y = y; }
        public double Length => Math.Sqrt(X * X + Y * Y);
        public Point Unit => Length > 1e-12 ? this * (1 / Length) : new Point(1, 0);
        public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
        public static Point operator *(Point a, double b) => new Point(a.X * b, a.Y * b);
    }
    public readonly struct Box
    {
        public readonly double Left, Bottom, Right, Top;
        public Box(double left, double bottom, double right, double top)
        {
            if (!(right > left) || !(top > bottom) || double.IsInfinity(right - left) || double.IsInfinity(top - bottom))
                throw new ArgumentException("Invalid arena bounds");
            Left = left; Bottom = bottom; Right = right; Top = top;
        }
    }
    public readonly struct Segment
    {
        public readonly Point From, To;
        public Segment(Point from, Point to) { From = from; To = to; }
    }
    public static class HazardGeometry
    {
        public static double RainX(double left, double width, int index, bool fromRight)
        {
            int count = (int)Tuning.RainCount;
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            double span = count * Tuning.RainWidth + (count - 1) * Tuning.RainGap;
            if (width < span) throw new ArgumentException("Arena cannot fit the approved arrow gaps.");
            int i = fromRight ? count - 1 - index : index;
            return left + (width - span) / 2 + Tuning.RainWidth / 2 + i * (Tuning.RainWidth + Tuning.RainGap);
        }
        public static int NearestLayer(double centerY, double[] heights, bool preferLowerOnTie)
        {
            if (heights == null || heights.Length != 3) throw new ArgumentException("Three layer heights required");
            int best = 0; double distance = Math.Abs(heights[0] - centerY);
            for (int i = 1; i < heights.Length; i++)
            {
                double next = Math.Abs(heights[i] - centerY);
                if (next < distance || next == distance && !preferLowerOnTie) { best = i; distance = next; }
            }
            return best;
        }
        public static Point HomingVelocity(Point position, Point velocity, Point target, double dt)
        {
            Encounter.CheckAmount(dt);
            double angle = Math.Atan2(velocity.Y, velocity.X), desired = Math.Atan2(target.Y - position.Y, target.X - position.X);
            double delta = Math.Atan2(Math.Sin(desired - angle), Math.Cos(desired - angle));
            double limit = Tuning.HomingTurnDegrees * Math.PI / 180 * dt;
            angle += Math.Max(-limit, Math.Min(limit, delta));
            return new Point(Math.Cos(angle) * Tuning.HomingSpeed, Math.Sin(angle) * Tuning.HomingSpeed);
        }
        // Host supplies bounds inset for the chosen projectile shape, then performs physics sweeps
        // for each returned segment. Rendering/frame rate must not skip an intermediate wall hit.
        public static List<Segment> Bounce(ref Point position, ref Point velocity, Box bounds, double dt, Random random)
        {
            Encounter.CheckAmount(dt); if (random == null) throw new ArgumentNullException(nameof(random));
            if (position.X < bounds.Left - 1e-8 || position.X > bounds.Right + 1e-8 || position.Y < bounds.Bottom - 1e-8 || position.Y > bounds.Top + 1e-8)
                throw new ArgumentException("Projectile starts outside its reflection bounds");
            var segments = new List<Segment>();
            while (dt > 1e-10)
            {
                if (segments.Count >= 1024) throw new InvalidOperationException("Excessive physics step; subdivide simulation time.");
                double tx = velocity.X > 1e-12 ? (bounds.Right - position.X) / velocity.X : velocity.X < -1e-12 ? (bounds.Left - position.X) / velocity.X : double.PositiveInfinity;
                double ty = velocity.Y > 1e-12 ? (bounds.Top - position.Y) / velocity.Y : velocity.Y < -1e-12 ? (bounds.Bottom - position.Y) / velocity.Y : double.PositiveInfinity;
                double wallTime = Math.Max(0, Math.Min(tx, ty));
                double travel = Math.Min(dt, wallTime);
                var next = position + velocity * travel;
                segments.Add(new Segment(position, next)); position = next; dt -= travel;
                if (wallTime > travel + 1e-10 || double.IsInfinity(wallTime)) break;
                bool hitX = Math.Abs(tx - wallTime) < 1e-8, hitY = Math.Abs(ty - wallTime) < 1e-8;
                double speed = velocity.Length;
                if (speed < 1e-12) break;
                if (hitX && hitY) velocity = new Point(-velocity.X, -velocity.Y);
                else
                {
                    double tangent = hitX ? Math.Abs(velocity.Y) : Math.Abs(velocity.X);
                    bool normal = Math.Asin(Math.Min(1, tangent / speed)) <= Tuning.BounceNormalToleranceDegrees * Math.PI / 180;
                    if (!normal) velocity = new Point(hitX ? -velocity.X : velocity.X, hitY ? -velocity.Y : velocity.Y);
                    else
                    {
                        double angle = (Tuning.BounceRandomMinDegrees + random.NextDouble() * (Tuning.BounceRandomMaxDegrees - Tuning.BounceRandomMinDegrees)) * Math.PI / 180;
                        double sign = random.Next(2) == 0 ? -1 : 1;
                        velocity = hitX
                            ? new Point((velocity.X > 0 ? -1 : 1) * Math.Cos(angle) * speed, sign * Math.Sin(angle) * speed)
                            : new Point(sign * Math.Sin(angle) * speed, (velocity.Y > 0 ? -1 : 1) * Math.Cos(angle) * speed);
                    }
                }
            }
            return segments;
        }
    }

    // One instance per projectile and victim. Re-entry during grace consumes that entry,
    // not a delayed hit after remaining continuously inside the projectile.
    public sealed class ReentryContact
    {
        bool inside;
        double lastDamage = double.NegativeInfinity;
        public bool Observe(bool overlaps, double now, bool damageAllowed, Func<bool> applyDamage)
        {
            Encounter.CheckAmount(now);
            bool entry = overlaps && !inside; inside = overlaps;
            if (!entry || !damageAllowed || now - lastDamage + 1e-9 < Tuning.BounceContactGrace) return false;
            if (applyDamage == null) throw new ArgumentNullException(nameof(applyDamage));
            if (!applyDamage()) return false;
            lastDamage = now;
            return true;
        }
    }
    public sealed class HitLedger
    {
        readonly HashSet<string> accepted = new HashSet<string>();
        // Group three bounce explosions under the same group ID; rain uses a separate ID per arrow.
        public bool TryHit(string attackGroup, int victim, Func<bool> applyDamage)
        {
            if (string.IsNullOrEmpty(attackGroup)) throw new ArgumentException(nameof(attackGroup));
            if (applyDamage == null) throw new ArgumentNullException(nameof(applyDamage));
            string key = attackGroup + ":" + victim;
            if (accepted.Contains(key) || !applyDamage()) return false;
            accepted.Add(key); return true;
        }
        public void Clear() => accepted.Clear();
    }
    public sealed class RootContact
    {
        readonly bool once;
        bool inside, applied;
        public RootContact(bool oncePerZone) { once = oncePerZone; }
        public bool Observe(bool overlaps, bool eligible)
        {
            bool entered = overlaps && !inside; inside = overlaps;
            if (!entered || !eligible || once && applied) return false;
            applied = true; return true;
        }
    }
    public sealed class PatternPicker
    {
        readonly Random random;
        Pattern? previous;
        public PatternPicker(int seed) { random = new Random(seed); }
        public Pattern? Next(bool homingActive)
        {
            if (homingActive) return null;
            var choices = new List<Pattern>();
            foreach (var p in new[] { Pattern.Homing, Pattern.Bounce, Pattern.ArrowRain, Pattern.ExpandingFan })
                if (p != previous) choices.Add(p);
            if (choices.Count == 0) return null;
            var chosen = choices[random.Next(choices.Count)]; previous = chosen; return chosen;
        }
    }
}
