using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Presentation
{
    public readonly struct WindowPoint
    {
        public readonly float X, Y;
        public WindowPoint(float x, float y)
        { if (!Finite(x) || !Finite(y)) throw new ArgumentException("Finite window coordinates are required."); X = x; Y = y; }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public readonly struct WindowRect
    {
        public readonly float Left, Bottom, Right, Top;
        public WindowRect(float left, float bottom, float right, float top)
        {
            if (!WindowPoint.Finite(left) || !WindowPoint.Finite(bottom) || !WindowPoint.Finite(right) || !WindowPoint.Finite(top) || right <= left || top <= bottom)
                throw new ArgumentException("A finite, positive window/receiver rectangle is required.");
            Left = left; Bottom = bottom; Right = right; Top = top;
        }
        public WindowPoint Center => new WindowPoint((Left + Right) / 2, (Bottom + Top) / 2);
        public bool Contains(WindowPoint point, float epsilon = 0) => point.X >= Left - epsilon && point.X <= Right + epsilon && point.Y >= Bottom - epsilon && point.Y <= Top + epsilon;
    }
    // Sweep the entire aperture, not a point at its center. The convex footprint is
    // clipped to the explicit receiving room. No world collision or combat queries.
    public static class WindowLightGeometry
    {
        public static WindowPoint Direction(float angle)
        {
            WindowLightDefinition.ValidateAngle(angle); double radians = angle * Math.PI / 180;
            return new WindowPoint((float)Math.Cos(radians), (float)Math.Sin(radians));
        }
        public static WindowPoint[] Build(WindowRect aperture, WindowRect room, float angle, float length)
        {
            LightingValidation.Positive(length, nameof(length)); var direction = Direction(angle);
            var candidates = new List<WindowPoint>(8);
            foreach (float x in new[] { aperture.Left, aperture.Right }) foreach (float y in new[] { aperture.Bottom, aperture.Top })
            {
                candidates.Add(new WindowPoint(x, y)); candidates.Add(new WindowPoint(x + direction.X * length, y + direction.Y * length));
            }
            candidates.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var hull = new List<WindowPoint>();
            foreach (var p in candidates) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 1e-7) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            for (int i = candidates.Count - 2; i >= 0; i--)
            {
                var p = candidates[i]; while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 1e-7) hull.RemoveAt(hull.Count - 1); hull.Add(p);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            hull = Clip(hull, 0, room.Left, true); hull = Clip(hull, 0, room.Right, false);
            hull = Clip(hull, 1, room.Bottom, true); hull = Clip(hull, 1, room.Top, false);
            for (int i = hull.Count - 1; i >= 0 && hull.Count > 1; i--)
            {
                var a = hull[i]; var b = hull[(i + 1) % hull.Count];
                if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 1e-6f) hull.RemoveAt(i);
            }
            return hull.Count >= 3 ? hull.ToArray() : Array.Empty<WindowPoint>();
        }
        static double Cross(WindowPoint a, WindowPoint b, WindowPoint c) => ((double)b.X - a.X) * (c.Y - a.Y) - ((double)b.Y - a.Y) * (c.X - a.X);
        static List<WindowPoint> Clip(List<WindowPoint> input, int axis, float limit, bool greater)
        {
            var output = new List<WindowPoint>(); if (input.Count == 0) return output;
            var previous = input[input.Count - 1]; float prior = axis == 0 ? previous.X : previous.Y;
            bool wasInside = greater ? prior >= limit : prior <= limit;
            foreach (var current in input)
            {
                float value = axis == 0 ? current.X : current.Y; bool inside = greater ? value >= limit : value <= limit;
                if (inside != wasInside)
                {
                    float t = (limit - prior) / (value - prior);
                    output.Add(new WindowPoint(previous.X + (current.X - previous.X) * t, previous.Y + (current.Y - previous.Y) * t));
                }
                if (inside) output.Add(current);
                previous = current; prior = value; wasInside = inside;
            }
            return output;
        }
        public static bool Contains(WindowPoint[] polygon, WindowPoint point, float epsilon = 1e-5f)
        {
            if (polygon == null || polygon.Length < 3) return false;
            for (int i = 0; i < polygon.Length; i++) if (Cross(polygon[i], polygon[(i + 1) % polygon.Length], point) < -epsilon) return false;
            return true;
        }
    }
}
