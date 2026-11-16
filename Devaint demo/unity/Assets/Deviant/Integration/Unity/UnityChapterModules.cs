using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.World.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Integration
{
    // First module binding: static, unrotated solid BoxCollider2D modules and
    // collision-free marker views. Read the authoring objects; never rewrite them.
    public sealed class UnityChapterModules : IChapterModuleCatalog, IChapterViewFactory, IDisposable
    {
        readonly Transform root;
        readonly Dictionary<string, GameObject> modules, placements;
        readonly HashSet<View> views = new HashSet<View>();
        GameObject staging, collisionRoot;
        CompositeCollider2D composite;
        bool disposed;
        public int LiveViewCount => views.Count;
        public UnityChapterModules(Transform root, IDictionary<string, GameObject> modules, IDictionary<string, GameObject> placements)
        {
            this.root = root ? root : throw new ArgumentNullException(nameof(root));
            this.modules = new Dictionary<string, GameObject>(modules ?? throw new ArgumentNullException(nameof(modules)), StringComparer.Ordinal);
            this.placements = new Dictionary<string, GameObject>(placements ?? throw new ArgumentNullException(nameof(placements)), StringComparer.Ordinal);
            CheckRoot();
        }
        public static LayoutBodyProfile ProfileFor(CombatPlayer player) => ProfileFor(player, player ? player.arena?.Movement : null);
        public static LayoutBodyProfile ProfileFor(CombatPlayer player, Deviant.Definitions.LocomotionProfile movement)
        {
            if (!player || !player.arena || movement == null) throw new ArgumentException("Initialized common player and explicit candidate movement required.", nameof(player));
            var motion = new LayoutMotionProfile(Time.fixedDeltaTime, movement.FirstJumpVelocity, movement.SecondJumpVelocity,
                movement.RiseGravity, movement.FallGravity, movement.DashSpeed, movement.DashDuration, movement.DashRecovery,
                movement.DashInvulnerability, movement.DashJumpCarrySpeed, movement.DashJumpCarryDuration, movement.ContentHash);
            return new LayoutBodyProfile(player.size.x, player.size.y, player.offset.x, player.offset.y, movement.RunSpeed, movement.ContentHash, motion);
        }
        public bool TryGetGeometry(string contentId, out ModuleGeometry geometry, out string error)
        {
            geometry = null; error = null;
            try
            {
                CheckRoot();
                if (!modules.TryGetValue(contentId, out var source) || !source) { error = "Module source missing."; return false; }
                if (!Identity(source.transform.localRotation, source.transform.localScale) || source.GetComponentsInChildren<Rigidbody2D>(true).Length != 0 ||
                    source.GetComponentsInChildren<MonoBehaviour>(true).Any(s => !s || !(s is ArenaSurface)) ||
                    source.GetComponentsInChildren<Behaviour>(true).Any(b => !(b is Collider2D) && !(b is ArenaSurface)))
                { error = "Only static identity-root modules with ArenaSurface metadata are supported."; return false; }
                var solids = new List<LayoutRect>();
                foreach (var collider in source.GetComponentsInChildren<Collider2D>(true))
                {
                    if (!collider.enabled || !ChildEnabled(collider.transform, source.transform)) continue;
                    var box = collider as BoxCollider2D;
                    var surface = collider.GetComponent<ArenaSurface>();
                    var matrix = source.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                    if (!box || box.isTrigger || box.usedByEffector || box.compositeOperation != Collider2D.CompositeOperation.None || box.autoTiling || box.edgeRadius != 0 ||
                        surface && surface.oneWay || !IdentityMatrix(matrix))
                    { error = "Unsupported collider, trigger, effector, tiling, rounded edge, rotation or scale."; return false; }
                    var center = matrix.MultiplyPoint3x4(box.offset);
                    solids.Add(new LayoutRect(center.x - box.size.x / 2, center.y - box.size.y / 2, box.size.x, box.size.y));
                }
                geometry = new ModuleGeometry(solids.ToArray()); return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public bool HasPlacementContent(string contentId)
        {
            if (disposed || !placements.TryGetValue(contentId, out var source) || !source) return false;
            return Identity(source.transform.localRotation, source.transform.localScale) && source.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                source.GetComponentsInChildren<Rigidbody2D>(true).Length == 0 && source.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
        }
        public IChapterView PrepareModule(LayoutModule module, string expectedGeometryHash)
        {
            if (!TryGetGeometry(module.ContentId, out var shape, out var error) || shape.ContentHash != expectedGeometryHash)
                throw new InvalidOperationException("Module geometry changed or is unavailable: " + module.Id + " / " + error);
            var instance = Prepare(modules[module.ContentId], module.Position, "Module_" + module.Id);
            try
            {
                EnsureCollisionRoot(); instance.transform.SetParent(collisionRoot.transform, false);
                foreach (var collider in instance.GetComponentsInChildren<BoxCollider2D>(true))
                    collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                return Register(instance);
            }
            catch { Remove(instance); throw; }
        }
        public IChapterView PreparePlacement(LayoutPlacement placement, LayoutPoint resolvedPosition)
        {
            CheckRoot();
            if (!HasPlacementContent(placement.ContentId)) throw new InvalidOperationException("Unsupported or missing marker content: " + placement.ContentId);
            return Register(Prepare(placements[placement.ContentId], resolvedPosition, "Placement_" + placement.Id));
        }
        GameObject Prepare(GameObject source, LayoutPoint at, string name)
        {
            CheckRoot();
            if (!staging)
            {
                staging = new GameObject("ChapterViewPreparation"); staging.SetActive(false); staging.transform.SetParent(root, false);
            }
            var instance = Object.Instantiate(source, staging.transform, false);
            instance.SetActive(false); instance.name = name; instance.transform.SetParent(root, false);
            instance.transform.localPosition = new Vector3(at.X, at.Y, 0);
            return instance;
        }
        void EnsureCollisionRoot()
        {
            if (collisionRoot) return;
            collisionRoot = new GameObject("ChapterSolidGeometry"); collisionRoot.transform.SetParent(root, false);
            var body = collisionRoot.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
            composite = collisionRoot.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            collisionRoot.AddComponent<ArenaSurface>();
        }
        void RefreshCollision()
        {
            if (!composite || !composite.gameObject.activeInHierarchy) return;
            Physics2D.SyncTransforms(); composite.GenerateGeometry();
        }
        View Register(GameObject instance) { var view = new View(this, instance); views.Add(view); return view; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var view in views.ToArray()) view.Dispose();
            if (staging) Remove(staging);
            if (collisionRoot) Remove(collisionRoot);
        }
        void CheckRoot()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityChapterModules));
            if (!root || !IdentityMatrix(root.localToWorldMatrix) || root.GetComponentInParent<Rigidbody2D>(true)) throw new InvalidOperationException("Chapter root must have identity rotation/scale and no Rigidbody2D ancestor.");
        }
        static bool ChildEnabled(Transform child, Transform source)
        {
            for (var at = child; at != source; at = at.parent) if (!at || !at.gameObject.activeSelf) return false;
            return true;
        }
        static bool Identity(Quaternion rotation, Vector3 scale) => Quaternion.Angle(rotation, Quaternion.identity) < .0001f && (scale - Vector3.one).sqrMagnitude < .00000001f;
        static bool IdentityMatrix(Matrix4x4 matrix)
        {
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                if (float.IsNaN(matrix[r, c]) || float.IsInfinity(matrix[r, c])) return false;
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                if (Mathf.Abs(matrix[r, c] - (r == c ? 1 : 0)) > .0001f) return false;
            return true;
        }
        static void Remove(GameObject instance)
        {
            instance.SetActive(false);
            if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
        }
        sealed class View : IChapterView
        {
            readonly UnityChapterModules owner;
            GameObject instance;
            public View(UnityChapterModules owner, GameObject instance) { this.owner = owner; this.instance = instance; }
            public void Activate()
            {
                owner.CheckRoot();
                if (!instance) throw new ObjectDisposedException(nameof(IChapterView));
                instance.SetActive(true); owner.RefreshCollision();
            }
            public void Dispose()
            {
                if (instance) { Remove(instance); owner.RefreshCollision(); }
                instance = null; owner.views.Remove(this);
            }
        }
    }
}
