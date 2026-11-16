using System;
using System.Collections.Generic;

namespace Deviant.Presentation
{
    public readonly struct WindowShadowEdge
    {
        public readonly WindowPoint A, B;
        public readonly int Owner;
        public WindowShadowEdge(WindowPoint a, WindowPoint b, int owner) { A = a; B = b; Owner = owner; }
    }
    // Every receiver traces the SAME direction back to the aperture's nearest face.
    // There is deliberately no light origin/centroid parameter.
    public static class WindowDirectionalShadows
    {
        public static bool TrySourceDistance(WindowRect aperture, WindowPoint receiver, WindowPoint direction, float length, out float distance)
        {
            double enter = 0, exit = length;
            bool hit = Axis(receiver.X, direction.X, aperture.Left, aperture.Right, ref enter, ref exit) &&
                Axis(receiver.Y, direction.Y, aperture.Bottom, aperture.Top, ref enter, ref exit);
            distance = hit ? (float)enter : 0; return hit;
        }
        static bool Axis(double p, double direction, double low, double high, ref double enter, ref double exit)
        {
            if (Math.Abs(direction) < 1e-7) return p >= low && p <= high;
            double a = (p - high) / direction, b = (p - low) / direction;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b)); return exit >= enter;
        }
        public static bool IsBlocked(WindowRect aperture, WindowPoint receiver, WindowPoint direction, float length,
            IReadOnlyList<WindowShadowEdge> edges, int ignoreOwner = 0)
        {
            if (!TrySourceDistance(aperture, receiver, direction, length, out float reach) || reach <= 1e-4f) return false;
            foreach (var edge in edges)
            {
                if (ignoreOwner != 0 && edge.Owner == ignoreOwner) continue;
                double rx = -direction.X, ry = -direction.Y, vx = (double)edge.B.X - edge.A.X, vy = (double)edge.B.Y - edge.A.Y;
                double denominator = rx * vy - ry * vx; if (Math.Abs(denominator) < 1e-7) continue;
                double x = (double)edge.A.X - receiver.X, y = (double)edge.A.Y - receiver.Y;
                double t = (x * vy - y * vx) / denominator, u = (x * ry - y * rx) / denominator;
                if (t > 1e-4 && t < reach - 1e-4 && u >= 0 && u <= 1) return true;
            }
            return false;
        }
    }
}
