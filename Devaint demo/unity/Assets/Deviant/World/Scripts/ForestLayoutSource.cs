using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Integration;
using Deviant.World.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Deviant.World
{
    // Reads approved source cells/platforms once. Runtime views and isolated
    // movement proof populate the same actual Tilemap/effector configuration.
    public sealed class ForestLayoutSource : IChapterLayoutProvider, IChapterModuleCatalog
    {
        public const string LayoutId = "prototype-forest-strip";
        public const string LayoutVersion = "1";
        const int Width = 64, Templates = 8;
        readonly InkForestData source;
        readonly InkForestChunk prefab;
        readonly GroundCell[] cells;
        readonly AirPlatform[] platforms;
        readonly Dictionary<string, ModuleGeometry> geometry = new Dictionary<string, ModuleGeometry>(StringComparer.Ordinal);
        readonly string[] tileIds;
        readonly string sourceHash;
        readonly ChapterLayout layout;
        readonly RepeatingLayoutProvider provider;
        readonly LayoutBodyProfile profile;
        readonly float vertexDistance, offsetDistance;
        readonly CompositeCollider2D.GenerationType generationType;
        readonly Vector3 tileAnchor;
        public ChapterLayout Layout => layout;
        public string ContentHash => sourceHash;
        public ForestLayoutSource(InkForestData source, InkForestChunk prefab, LayoutBodyProfile profile, LayoutPoint entry)
        {
            this.source = source ? source : throw new ArgumentNullException(nameof(source));
            this.prefab = prefab ? prefab : throw new ArgumentNullException(nameof(prefab));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (profile.Motion == null) throw new ArgumentException("The actual common motor capabilities must be captured before opening the forest layout.");
            if (source.cells == null || source.platforms == null || source.cells.Any(cell => cell == null) || source.platforms.Any(platform => platform == null))
                throw new InvalidOperationException("The authored forest cells/platforms are incomplete.");
            cells = source.cells.Select(cell => new GroundCell { id = cell.id, x = cell.x, y = cell.y }).ToArray();
            platforms = source.platforms.Select(platform => new AirPlatform { x = platform.x, top = platform.top, width = platform.width }).ToArray();
            var composite = prefab.ground.GetComponent<CompositeCollider2D>();
            if (!composite) throw new InvalidOperationException("The authored forest composite is required.");
            vertexDistance = composite.vertexDistance; offsetDistance = composite.offsetDistance; generationType = composite.generationType;
            tileAnchor = prefab.ground.tileAnchor;
            tileIds = cells.Select(cell => cell.id).Concat(new[] { "ledge" }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            ValidateConfiguration(); sourceHash = HashSource();
            var modules = new LayoutModule[Templates]; var templateIds = new string[Templates];
            var anchors = new List<LayoutAnchor>();
            for (int index = 0; index < Templates; index++)
            {
                string id = "forest-template-" + index;
                templateIds[index] = id; modules[index] = new LayoutModule(id, id, new LayoutPoint(index * Width, 0));
                var solids = cells.Where(cell => cell.x >= index * Width && cell.x < (index + 1) * Width)
                    .Select(cell => new LayoutRect(cell.x - index * Width, cell.y, 1, 1)).ToArray();
                var oneWay = platforms.Where(platform => platform.x >= index * Width && platform.x < (index + 1) * Width)
                    .Select(platform => new LayoutRect(platform.x - index * Width, platform.top - .24f, platform.width, .24f)).ToArray();
                geometry.Add(id, new ModuleGeometry(solids, oneWay));
                anchors.Add(new LayoutAnchor(id + "/origin", id, default, LayoutAnchorKind.Binding));
                for (int platform = 0; platform < oneWay.Length; platform++)
                    anchors.Add(new LayoutAnchor(id + "/platform-" + platform, id,
                        new LayoutPoint(oneWay[platform].X + oneWay[platform].Width / 2, oneWay[platform].Top), LayoutAnchorKind.Binding));
            }
            if (entry.X < 0 || entry.X >= Width * Templates) throw new ArgumentException("The existing prototype entry must belong to the supplied first period.");
            int entryTemplate = Mathf.FloorToInt(entry.X / Width);
            anchors.Add(new LayoutAnchor("prototype-player-entry", templateIds[entryTemplate], new LayoutPoint(entry.X - entryTemplate * Width, entry.Y)));
            layout = new ChapterLayout(LayoutId, LayoutVersion, new LayoutOrigin(LayoutOriginKind.Authored, "existing-forest-source", "1", sourceHash),
                "prototype-player-entry", modules, anchors.ToArray(), Array.Empty<LayoutConnection>(),
                repetition: new LayoutRepetition(Width, "forest-strip/", templateIds), witnesses: Witnesses(entry));
            provider = new RepeatingLayoutProvider(layout);
        }
        public LayoutSupply Provide(LayoutRequest request)
        {
            if (!SourceUnchanged(out var error)) return LayoutSupply.Failure(error);
            return provider.Provide(request);
        }
        public bool TryGetGeometry(string contentId, out ModuleGeometry result, out string error)
        {
            result = null;
            if (!SourceUnchanged(out error)) return false;
            if (!geometry.TryGetValue(contentId, out result)) { error = "Unknown authored forest template: " + contentId; return false; }
            return true;
        }
        public bool HasPlacementContent(string contentId) => false;
        public bool SourceUnchanged(out string error)
        {
            try
            {
                if (!source || source.cells == null || source.platforms == null || source.cells.Length != cells.Length || source.platforms.Length != platforms.Length)
                { error = "Forest collision source was replaced; explicitly reopen its layout."; return false; }
                for (int index = 0; index < cells.Length; index++)
                {
                    var current = source.cells[index]; var frozen = cells[index];
                    if (current == null || current.id != frozen.id || current.x != frozen.x || current.y != frozen.y)
                    { error = "Forest collision cells changed; explicitly reopen its layout."; return false; }
                }
                for (int index = 0; index < platforms.Length; index++)
                {
                    var current = source.platforms[index]; var frozen = platforms[index];
                    if (current == null || current.x != frozen.x || current.top != frozen.top || current.width != frozen.width)
                    { error = "Forest one-way source changed; explicitly reopen its layout."; return false; }
                }
                ValidateConfiguration(false);
                error = null; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        void ValidateConfiguration(bool inspectCells = true)
        {
            if (!source || source.cells == null || source.platforms == null || source.themes == null || source.themes.Length == 0 || !prefab ||
                !prefab.ground || !prefab.ledges || !prefab.platformRoot) throw new InvalidOperationException("The existing forest source/prefab is incomplete.");
            var grid = prefab.GetComponent<Grid>(); var tileCollider = prefab.ground.GetComponent<TilemapCollider2D>();
            var composite = prefab.ground.GetComponent<CompositeCollider2D>(); var body = prefab.ground.GetComponent<Rigidbody2D>();
            if (!grid || grid.cellSize != Vector3.one || grid.cellGap != Vector3.zero || grid.cellLayout != GridLayout.CellLayout.Rectangle ||
                !tileCollider || !composite || !body || body.bodyType != RigidbodyType2D.Static ||
                tileCollider.compositeOperation != Collider2D.CompositeOperation.Merge || tileCollider.extrusionFactor != 0 ||
                composite.geometryType != CompositeCollider2D.GeometryType.Polygons || composite.edgeRadius != 0 ||
                composite.vertexDistance != vertexDistance || composite.offsetDistance != offsetDistance || composite.generationType != generationType ||
                prefab.ground.tileAnchor != tileAnchor)
                throw new InvalidOperationException("The authored forest requires its existing rectangular Grid/Tilemap/composite collision configuration.");
            if (prefab.transform.localScale != Vector3.one || prefab.transform.localRotation != Quaternion.identity ||
                prefab.ground.transform.localScale != Vector3.one || prefab.ground.transform.localRotation != Quaternion.identity || prefab.ground.transform.localPosition != Vector3.zero)
                throw new InvalidOperationException("Forest grid transforms changed; an explicit new geometry adapter is required.");
            if (prefab.platformRoot.localPosition != Vector3.zero || prefab.platformRoot.localScale != Vector3.one || prefab.platformRoot.localRotation != Quaternion.identity)
                throw new InvalidOperationException("The authored one-way platform root transform changed.");
            foreach (var theme in source.themes)
                foreach (string id in tileIds)
                {
                    var tile = theme.Get(id);
                    if (!tile || tile.transform != Matrix4x4.identity || tile.colliderType != (id == "ledge" ? Tile.ColliderType.None : Tile.ColliderType.Grid))
                        throw new InvalidOperationException("Forest tile collision differs from the frozen source: " + theme.id + "/" + id);
                }
            if (!inspectCells) return;
            if (source.cells.Any(cell => cell == null || cell.x < 0 || cell.x >= Width * Templates || string.IsNullOrWhiteSpace(cell.id)) ||
                source.cells.GroupBy(cell => (cell.x, cell.y)).Any(group => group.Count() != 1) ||
                source.cells.Select(cell => cell.x).Distinct().Count() != Width * Templates)
                throw new InvalidOperationException("The approved forest period requires distinct authored cells in every existing source column.");
            for (int index = 0; index < Templates; index++)
                if (source.platforms.Count(platform => platform.x >= index * Width && platform.x < (index + 1) * Width) != 2)
                    throw new InvalidOperationException("The existing chunk prefab has exactly two authored one-way platforms per template.");
            foreach (var platform in source.platforms)
                if (platform == null || float.IsNaN(platform.x) || float.IsInfinity(platform.x) || float.IsNaN(platform.top) || float.IsInfinity(platform.top) ||
                    platform.width <= 0 || platform.x < 0 || platform.x + platform.width > (Mathf.FloorToInt(platform.x / Width) + 1) * Width)
                    throw new InvalidOperationException("Invalid authored one-way platform source.");
        }
        string HashSource()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write("existing-forest-grid-v1");
                    foreach (var cell in source.cells) { writer.Write(cell.id); writer.Write(cell.x); writer.Write(cell.y); }
                    foreach (var platform in source.platforms) { writer.Write(platform.x); writer.Write(platform.top); writer.Write(platform.width); }
                    var composite = prefab.ground.GetComponent<CompositeCollider2D>();
                    writer.Write(composite.vertexDistance); writer.Write(composite.offsetDistance); writer.Write((int)composite.generationType);
                    writer.Write(prefab.ground.tileAnchor.x); writer.Write(prefab.ground.tileAnchor.y);
                }
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public void Populate(InkForestChunk chunk, int index, int themeIndex, Action<InkForestChunk> prepared)
        {
            if (!SourceUnchanged(out var error)) throw new InvalidOperationException(error);
            Fill(chunk, index, source.themes[themeIndex], prepared);
        }
        void Fill(InkForestChunk chunk, int index, ForestTheme theme, Action<InkForestChunk> prepared)
        {
            int template = index % Templates, start = template * Width;
            chunk.index = index; chunk.name = "Chunk_" + index; chunk.transform.localPosition = new Vector3(index * Width, 0, 0);
            chunk.ground.ClearAllTiles(); chunk.ledges.ClearAllTiles();
            while (chunk.platformRoot.childCount < 2)
            {
                var go = new GameObject("OneWay"); go.transform.SetParent(chunk.platformRoot, false);
                var box = go.AddComponent<BoxCollider2D>(); box.usedByEffector = true; box.sharedMaterial = source.frictionless;
                var effector = go.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.useOneWayGrouping = true;
                effector.surfaceArc = 120; effector.useSideBounce = false; effector.useSideFriction = false;
            }
            foreach (var cell in cells) if (cell.x >= start && cell.x < start + Width)
                chunk.ground.SetTile(new Vector3Int(cell.x - start, cell.y, 0), theme.Get(cell.id));
            int platformIndex = 0;
            foreach (var platform in platforms) if (platform.x >= start && platform.x < start + Width)
            {
                for (int tile = 0; tile < platform.width; tile++)
                    chunk.ledges.SetTile(new Vector3Int(Mathf.RoundToInt(platform.x - start) + tile, Mathf.RoundToInt(platform.top) - 1, 0), theme.Get("ledge"));
                var target = chunk.platformRoot.GetChild(platformIndex++);
                target.localPosition = new Vector3(platform.x - start + platform.width * .5f, platform.top - .12f, 0);
                target.GetComponent<BoxCollider2D>().size = new Vector2(platform.width, .24f);
            }
            prepared?.Invoke(chunk);
            chunk.ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        }
        public IChapterTraversalProof CreateProof() => new UnityLayoutTraversalProof(PrepareProof);
        IDisposable PrepareProof(Scene scene, ChapterLayout requested)
        {
            if (!ReferenceEquals(requested, layout)) throw new InvalidOperationException("Proof must use this exact frozen authored forest layout.");
            var root = new GameObject("IsolatedAuthoredForest"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                // One period plus its repeated neighbor proves actual template
                // seams, including 7->0; this is not a finite chapter extent.
                for (int index = 0; index <= Templates; index++)
                {
                    var chunk = Object.Instantiate(prefab, root.transform, false); chunk.gameObject.SetActive(false);
                    Fill(chunk, index, source.themes[0], PrepareSurfaces); chunk.gameObject.SetActive(true);
                    chunk.ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                }
                Physics2D.SyncTransforms();
                return new ProofViews(root);
            }
            catch { Object.DestroyImmediate(root); throw; }
        }
        static void PrepareSurfaces(InkForestChunk chunk)
        {
            foreach (var collider in chunk.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider.isTrigger) continue;
                var surface = collider.GetComponent<Deviant.Combat.ArenaSurface>();
                if (!surface) surface = collider.gameObject.AddComponent<Deviant.Combat.ArenaSurface>();
                surface.oneWay = collider.GetComponent<PlatformEffector2D>() != null;
            }
        }
        sealed class ProofViews : IDisposable
        {
            GameObject root;
            public ProofViews(GameObject root) { this.root = root; }
            public void Dispose() { if (root) { root.SetActive(false); Object.DestroyImmediate(root); root = null; } }
        }
        LayoutWitness[] Witnesses(LayoutPoint entry)
        {
            var results = new List<LayoutWitness>();
            Add("existing-player-entry", entry, new LayoutPoint(entry.X, SurfaceTop(entry.X)), 1, LayoutWitnessKind.Settle);
            foreach (var platform in platforms)
            {
                float center = platform.x + platform.width / 2;
                string key = platform.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                Add("platform-" + key + "-land", new LayoutPoint(center, SurfaceTop(center) + .03f), new LayoutPoint(center, platform.top), 1, LayoutWitnessKind.Jump);
                Add("platform-" + key + "-drop", new LayoutPoint(center, platform.top + .03f), new LayoutPoint(center, SurfaceTop(center)), 1, LayoutWitnessKind.Drop);
            }
            var changes = new List<int>();
            for (int column = 1; column < Width * Templates; column++) if (SurfaceTop(column) != SurfaceTop(column - 1)) changes.Add(column);
            for (int seam = Width; seam <= Width * Templates; seam += Width) if (!changes.Contains(seam)) changes.Add(seam);
            foreach (int at in changes)
                foreach (int direction in new[] { 1, -1 })
                {
                    // One unit either side is the authored tile-cell center
                    // corridor, independent of the current movement profile.
                    float from = at - direction, to = at + direction;
                    float startY = SurfaceTop(from), endY = SurfaceTop(to);
                    Add("surface-transition-" + at + "/" + direction, new LayoutPoint(from, startY + .03f),
                        new LayoutPoint(to, endY), direction, endY > startY ? LayoutWitnessKind.Jump : LayoutWitnessKind.Walk);
                }
            Add("existing-flat-dash", new LayoutPoint(25, SurfaceTop(25) + .03f), new LayoutPoint(28, SurfaceTop(28)), 1, LayoutWitnessKind.Dash);
            return results.ToArray();

            void Add(string id, LayoutPoint start, LayoutPoint destination, int facing, LayoutWitnessKind kind)
                => results.Add(new LayoutWitness(id, start, facing, kind, destination,
                    new LayoutRect(destination.X - .05f, destination.Y - .06f, .1f, .12f)));
        }
        float SurfaceTop(float x)
        {
            int column = Mathf.FloorToInt(Mathf.Repeat(x, Width * Templates));
            // This only locates authored witness endpoints. Collision proof and
            // gameplay both use actual cell/platform colliders, never this top.
            return cells.Where(cell => cell.x == column).Max(cell => cell.y + 1);
        }
    }
}
