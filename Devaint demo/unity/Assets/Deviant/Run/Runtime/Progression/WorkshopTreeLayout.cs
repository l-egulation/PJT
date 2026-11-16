using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.Run
{
    // The workshop tree of one branch tab (docs/design/progression/roguelite-progression.md#해금-트리), as grid cells.
    // A node holds the tiers of one effect that share an unlock's `node` ("공격력 I" = attack tiers 1-4); an unlock
    // without one is its own node. One row per tier line, and the column is the node's depth in the node graph, so
    // every prerequisite node stands left of what it opens. A prerequisite node from another branch is drawn as a
    // foreign node on rows below the branch's own lines. Presentation only reads this; purchase rules stay in
    // ProgressionService, which buys a node one tier at a time through the unlocks' own prerequisites.
    public sealed class WorkshopTreeLayout
    {
        public sealed class Node
        {
            // The node's tiers in buying order; the first tier's id names the node.
            public IReadOnlyList<ProgressionUnlock> Levels;
            public string Name;
            public int Row, Column;
            // A prerequisite from another branch: shown for reference, bought in its own tab.
            public bool Foreign;
            public string Id => Levels[0].id;
            public ProgressionUnlock Last => Levels[Levels.Count - 1];
            public bool Contains(string unlockId) => Levels.Any(l => l.id == unlockId);
            public int Owned(Func<string, bool> has) => Levels.Count(l => has(l.id));
            public bool Complete(Func<string, bool> has) => Levels.All(l => has(l.id));
            // The tier a purchase takes next: the lowest one not owned, or the last once the node is complete.
            public ProgressionUnlock Current(Func<string, bool> has) => Levels.FirstOrDefault(l => !has(l.id)) ?? Last;
        }
        public sealed class Row
        {
            // The line's first tier (the effect's lowest unlock); null for a foreign row.
            public ProgressionUnlock First;
            // Foreign rows: the branches their nodes come from.
            public string[] ForeignBranches = Array.Empty<string>();
            public bool Foreign => First == null;
        }
        public readonly struct Edge
        {
            public readonly Node From, To;
            public Edge(Node from, Node to) { From = from; To = to; }
        }

        public string Branch { get; private set; }
        public IReadOnlyList<Row> Rows { get; private set; }
        public IReadOnlyList<Node> Nodes { get; private set; }
        public IReadOnlyList<Edge> Edges { get; private set; }
        public int Columns { get; private set; }
        // The node holding this unlock.
        public Node Find(string unlockId) => Nodes.FirstOrDefault(n => n.Contains(unlockId));

        // Directional navigation follows the authored grid, including stair-shaped branches and prerequisites.
        public Node Neighbor(Node from,int horizontal,int vertical)
        {
            if(from==null)return null;
            if(horizontal!=0)return Nodes.Where(n=>n!=from&&(n.Column-from.Column)*horizontal>0)
                .OrderBy(n=>Math.Abs(n.Column-from.Column)).ThenBy(n=>Math.Abs(n.Row-from.Row)).ThenBy(n=>n.Row).FirstOrDefault();
            if(vertical!=0)return Nodes.Where(n=>n!=from&&(n.Row-from.Row)*vertical>0)
                .OrderBy(n=>Math.Abs(n.Row-from.Row)).ThenBy(n=>Math.Abs(n.Column-from.Column)).ThenBy(n=>n.Column).FirstOrDefault();
            return null;
        }

        // Tabs in data order; without branch data, one tab that holds every unlock.
        public static IReadOnlyList<ProgressionBranch> Branches(ProgressionCatalog catalog) =>
            catalog.Data.branches != null && catalog.Data.branches.Length > 0 ? catalog.Data.branches : new[] { new ProgressionBranch { id = "", name = "공방" } };
        public static string BranchOf(ProgressionCatalog catalog, ProgressionUnlock unlock) =>
            catalog.Data.branches != null && catalog.Data.branches.Length > 0 ? unlock.branch : "";

        // The node name shown on the tree and the detail page.
        public static string NameOf(ProgressionUnlock unlock) => string.IsNullOrEmpty(unlock.node) ? unlock.name : unlock.node;

        // The tiers of the node this unlock belongs to: its neighbours in the tier line that name the same node.
        public static IReadOnlyList<ProgressionUnlock> Levels(ProgressionCatalog catalog, ProgressionUnlock unlock)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (unlock == null) return Array.Empty<ProgressionUnlock>();
            var line = Line(catalog, catalog.FirstTier(unlock));
            int at = line.IndexOf(unlock); if (at < 0) return new[] { unlock };
            string key = Key(unlock); int from = at, to = at;
            while (from > 0 && Key(line[from - 1]) == key) from--;
            while (to + 1 < line.Count && Key(line[to + 1]) == key) to++;
            return line.GetRange(from, to - from + 1).AsReadOnly();
        }
        static string Key(ProgressionUnlock u) => string.IsNullOrEmpty(u.node) ? "#" + u.id : u.node;

        public static WorkshopTreeLayout For(ProgressionCatalog catalog, string branch)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            // Every node of the catalog, so depth and foreign prerequisites see the whole graph.
            var of = new Dictionary<string, Node>(); var all = new List<Node>();
            foreach (var u in catalog.Unlocks)
            {
                if (of.ContainsKey(u.id)) continue;
                var levels = Levels(catalog, u);
                var node = new Node { Levels = levels, Name = NameOf(levels[0]) };
                foreach (var l in levels) of[l.id] = node;
                all.Add(node);
            }
            IEnumerable<Node> Before(Node n) => n.Levels.SelectMany(l => l.prerequisites ?? Array.Empty<string>())
                .Where(of.ContainsKey).Select(id => of[id]).Where(p => p != n).Distinct();
            var depth = Depths(all, Before);

            var own = all.Where(n => BranchOf(catalog, n.Levels[0]) == branch).ToList();
            var rows = new List<Row>(); var nodes = new List<Node>();
            foreach (var node in own)
            {
                var first = catalog.FirstTier(node.Levels[0]);
                int row = rows.FindIndex(r => r.First == first);
                if (row < 0) { rows.Add(new Row { First = first }); row = rows.Count - 1; }
                node.Row = row; node.Column = depth[node]; nodes.Add(node);
            }
            // Foreign prerequisites take the first foreign row whose column is still free.
            var foreignRows = new List<Row>();
            foreach (var node in own)
                foreach (var pre in Before(node))
                {
                    if (nodes.Contains(pre)) continue;
                    int column = depth[pre], at = 0;
                    while (at < foreignRows.Count && nodes.Any(n => n.Foreign && n.Row == rows.Count + at && n.Column == column)) at++;
                    if (at == foreignRows.Count) foreignRows.Add(new Row());
                    pre.Row = rows.Count + at; pre.Column = column; pre.Foreign = true; nodes.Add(pre);
                    var from = BranchOf(catalog, pre.Levels[0]);
                    if (!foreignRows[at].ForeignBranches.Contains(from)) foreignRows[at].ForeignBranches = foreignRows[at].ForeignBranches.Append(from).ToArray();
                }
            rows.AddRange(foreignRows);
            var edges = new List<Edge>();
            foreach (var node in nodes.Where(n => !n.Foreign))
                foreach (var pre in Before(node))
                    if (nodes.Contains(pre)) edges.Add(new Edge(pre, node));
            return new WorkshopTreeLayout
            {
                Branch = branch, Rows = rows.AsReadOnly(), Nodes = nodes.AsReadOnly(), Edges = edges.AsReadOnly(),
                Columns = nodes.Count == 0 ? 0 : nodes.Max(n => n.Column) + 1,
            };
        }

        static List<ProgressionUnlock> Line(ProgressionCatalog catalog, ProgressionUnlock first)
        {
            var line = new List<ProgressionUnlock>();
            for (var u = first; u != null && !line.Contains(u); u = catalog.NextTier(u)) line.Add(u);
            return line;
        }

        // Longest prerequisite path from a root node (0).
        static Dictionary<Node, int> Depths(List<Node> all, Func<Node, IEnumerable<Node>> before)
        {
            var depth = new Dictionary<Node, int>();
            int Of(Node n, int guard)
            {
                if (depth.TryGetValue(n, out int d)) return d;
                if (guard > all.Count) throw new InvalidOperationException("prerequisite cycle at " + n.Id);
                d = 0;
                foreach (var pre in before(n)) d = Math.Max(d, Of(pre, guard + 1) + 1);
                return depth[n] = d;
            }
            foreach (var n in all) Of(n, 0);
            return depth;
        }
    }
}
