using System;

namespace Deviant.Presentation
{
    // Union of convex, non-degenerate openings. Holes/frames are the gaps between
    // pieces, not an opaque enclosing rectangle. Curves are authored as polygons.
    public sealed class WindowAperture
    {
        public const int MaxPieces = 8, MaxVertices = 64;
        readonly WindowPoint[][] pieces;
        public WindowRect Bounds { get; }
        public int PieceCount => pieces.Length;
        public int VertexCount { get; }
        public int GetVertexCount(int piece) => pieces[piece].Length;
        public WindowPoint GetVertex(int piece, int vertex) => pieces[piece][vertex];
        public static WindowAperture Rectangle(WindowRect rect) => new WindowAperture(new[] {
            new WindowPoint(rect.Left, rect.Bottom), new WindowPoint(rect.Right, rect.Bottom),
            new WindowPoint(rect.Right, rect.Top), new WindowPoint(rect.Left, rect.Top) });
        public WindowAperture(params WindowPoint[][] openings)
        {
            if (openings == null || openings.Length == 0 || openings.Length > MaxPieces)
                throw new ArgumentException("Provide one to eight convex aperture pieces.");
            pieces = new WindowPoint[openings.Length][];
            float left = float.PositiveInfinity, bottom = left, right = float.NegativeInfinity, top = right;
            int count = 0;
            for (int p = 0; p < openings.Length; p++)
            {
                var input = openings[p];
                if (input == null || input.Length < 3 || (count += input.Length) > MaxVertices)
                    throw new ArgumentException("Each aperture needs at least three vertices; the total budget is 64.");
                var polygon = (WindowPoint[])input.Clone(); double area = 0;
                for (int i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                    area += (double)a.X * b.Y - (double)b.X * a.Y;
                    left = Math.Min(left, a.X); bottom = Math.Min(bottom, a.Y); right = Math.Max(right, a.X); top = Math.Max(top, a.Y);
                }
                if (Math.Abs(area) < 1e-7) throw new ArgumentException("An aperture cannot have zero area.");
                if (area < 0) Array.Reverse(polygon);
                for (int i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                    double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
                    if (dx * dx + dy * dy < 1e-12) throw new ArgumentException("Duplicate aperture vertices are not allowed.");
                    for (int j = 0; j < polygon.Length; j++) if (j != i && j != (i + 1) % polygon.Length)
                    {
                        double cross = dx * (polygon[j].Y - a.Y) - dy * (polygon[j].X - a.X);
                        if (cross <= 1e-9) throw new ArgumentException("Use strictly convex, simple pieces; split concave outlines and holes into separate openings.");
                    }
                }
                pieces[p] = polygon;
            }
            VertexCount = count; Bounds = new WindowRect(left, bottom, right, top);
        }
        public bool TrySourceDistance(WindowPoint receiver, WindowPoint direction, float length, out float distance)
        {
            Deviant.Definitions.LightingValidation.Positive(length, nameof(length));
            distance = float.PositiveInfinity;
            foreach (var polygon in pieces)
            {
                double enter = 0, exit = length; bool hit = true;
                for (int i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                    double nx = a.Y - b.Y, ny = b.X - a.X;
                    double value = nx * (receiver.X - a.X) + ny * (receiver.Y - a.Y);
                    double rate = nx * direction.X + ny * direction.Y;
                    if (Math.Abs(rate) < 1e-7) { if (value < 0) { hit = false; break; } }
                    else if (rate > 0) exit = Math.Min(exit, value / rate);
                    else enter = Math.Max(enter, value / rate);
                    if (exit < enter) { hit = false; break; }
                }
                if (hit) distance = Math.Min(distance, (float)enter);
            }
            return !float.IsPositiveInfinity(distance);
        }
    }
}
