using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using Deviant.World.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Deviant.Integration
{
    // The source adapter creates the actual authored terrain in a local physics
    // scene. No player, live collider, world clock or HP is used by the proof.
    public sealed class UnityLayoutTraversalProof : IChapterTraversalProof
    {
        readonly Func<Scene, ChapterLayout, IDisposable> prepare;
        public LayoutProofResult LastResult { get; private set; }
        public UnityLayoutTraversalProof(Func<Scene, ChapterLayout, IDisposable> prepare)
        { this.prepare = prepare ?? throw new ArgumentNullException(nameof(prepare)); }

        public bool TryValidate(ChapterLayout layout, LayoutBodyProfile profile, IChapterModuleCatalog catalog, out string error)
        {
            error = null; LastResult = null;
            if (profile.Motion == null || layout.Witnesses.Count == 0) { error = "Explicit common motor capabilities and movement witnesses are required."; return false; }
            var scene = SceneManager.CreateScene("LayoutProof_" + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            IDisposable views = null;
            try
            {
                views = prepare(scene, layout) ?? throw new InvalidOperationException("The source supplied no isolated authored terrain.");
                var physics = scene.GetPhysicsScene2D();
                if (!physics.IsValid()) throw new InvalidOperationException("The proof scene has no local 2D physics world.");
                Physics2D.SyncTransforms(); physics.Simulate(0);
                var witnesses = new List<LayoutWitness>(layout.Witnesses);
                var results = new List<LayoutWitnessResult>();
                foreach (var anchor in layout.Anchors)
                {
                    if (anchor.Kind != LayoutAnchorKind.Traversal) continue;
                    LayoutModule module = null;
                    foreach (var candidate in layout.Modules) if (candidate.Id == anchor.ModuleId) { module = candidate; break; }
                    var point = module.Position.Add(anchor.LocalPosition);
                    foreach (int facing in new[] { 1, -1 })
                    {
                        using (var actor = new MotorActor(profile, physics, scene))
                        {
                            actor.Reset(point, facing);
                            bool clear = actor.ClearAt(point, facing), hazardous = actor.InHazard(layout.Hazards);
                            var inputs = new List<LayoutMotionInput>(); var trace = new List<int>();
                            int stable = 0;
                            int settleBudget = Mathf.CeilToInt(Mathf.Sqrt(.12f / profile.Motion.FallGravity) / profile.Motion.Step) + 3;
                            for (int tick = 0; clear && !hazardous && tick < settleBudget; tick++)
                            {
                                float previousY = actor.Y;
                                inputs.Add(default); actor.Step(default); actor.Record(trace);
                                if (actor.InHazard(layout.Hazards) || Math.Abs(actor.X - point.X) > .0001f ||
                                    actor.Y > point.Y + .0001f || actor.Y < point.Y - .06f) break;
                                stable = actor.Grounded && Math.Abs(actor.Y - previousY) <= .0001f ? stable + 1 : 0;
                                if (stable == 2) break;
                            }
                            bool supported = stable == 2;
                            string failure = !clear || !supported || hazardous ? "Traversal anchor failed: " + anchor.Id + " clear=" + clear + " settled=" + supported + " hazardous=" + hazardous + " " + actor.ContactDiagnostics() : null;
                            results.Add(new LayoutWitnessResult("anchor-settle/" + anchor.Id + "/" + facing, profile.ContentHash,
                                Hash(writer => { foreach (var input in inputs) { writer.Write(input.Axis); writer.Write(input.Jump); writer.Write(input.Dash); writer.Write(input.Drop); } }),
                                Hash(writer => { foreach (int bits in trace) writer.Write(bits); }), inputs.Count, failure == null, failure));
                            LastResult = new LayoutProofResult(layout.ContentHash, profile.ContentHash, results.ToArray());
                            if (failure != null) { error = failure; return false; }
                        }
                    }
                }
                foreach (var connection in layout.Connections)
                {
                    LayoutPoint Endpoint(string id)
                    {
                        foreach (var anchor in layout.Anchors)
                            if (anchor.Id == id)
                                foreach (var module in layout.Modules) if (module.Id == anchor.ModuleId) return module.Position.Add(anchor.LocalPosition);
                        throw new InvalidOperationException("A structurally validated connection endpoint disappeared.");
                    }
                    var from = Endpoint(connection.From); var to = Endpoint(connection.To);
                    var kind = connection.Traversal == LayoutTraversal.Jump ? LayoutWitnessKind.Jump : connection.Traversal == LayoutTraversal.Dash ? LayoutWitnessKind.Dash : LayoutWitnessKind.Walk;
                    foreach (bool reverse in new[] { false, true })
                    {
                        var start = reverse ? to : from; var finish = reverse ? from : to;
                        witnesses.Add(new LayoutWitness("connection/" + connection.Id + (reverse ? "/reverse" : "/forward"), start,
                            finish.X >= start.X ? 1 : -1, kind, finish, new LayoutRect(finish.X - .05f, finish.Y - .06f, .1f, .12f)));
                    }
                }
                foreach (var witness in witnesses)
                {
                    using (var actor = new MotorActor(profile, physics, scene))
                    {
                        actor.Reset(witness.Start, witness.Facing);
                        if (!actor.ClearAt(witness.Start, witness.Facing)) { error = "Witness starts inside authored solid terrain: " + witness.Id; return false; }
                        var inputs = new List<LayoutMotionInput>(); var trace = new List<int>();
                        float fallTime = Mathf.Sqrt(2 * Mathf.Abs(witness.Start.Y - witness.Destination.Y) / profile.Motion.FallGravity);
                        float jumpTime = 2 * (profile.Motion.FirstJump + profile.Motion.SecondJump) / profile.Motion.RiseGravity + fallTime;
                        float walkTime = Mathf.Abs(witness.Destination.X - witness.Start.X) / Math.Max(.0001f, profile.WalkSpeed);
                        float duration = Math.Max(walkTime, Math.Max(jumpTime, profile.Motion.DashDuration)) + .5f;
                        int budget = Math.Max(1, Mathf.CeilToInt(Math.Min(10, duration) / profile.Motion.Step));
                        bool succeeded = false;
                        int direction = witness.Destination.X > witness.Start.X ? 1 : witness.Destination.X < witness.Start.X ? -1 : 0;
                        bool crossed = direction == 0;
                        float firstJumpHeight = profile.Motion.FirstJump * profile.Motion.FirstJump / (2 * profile.Motion.RiseGravity);
                        bool needsSecondJump = witness.Kind == LayoutWitnessKind.Jump && witness.Destination.Y - witness.Start.Y > firstJumpHeight;
                        bool reachedEndpoint = false;
                        int settledAfterDash = 0;
                        float lastDeltaY = 0;
                        bool lastEndpointContact = false;
                        for (int tick = 0; tick < budget; tick++)
                        {
                            int axis = crossed ? 0 : direction;
                            if (witness.Kind == LayoutWitnessKind.Settle || witness.Kind == LayoutWitnessKind.Drop) axis = 0;
                            bool secondJump = needsSecondJump && actor.JumpCount == 1 && !actor.Grounded && actor.VerticalVelocity <= 0;
                            var input = new LayoutMotionInput(axis, witness.Kind == LayoutWitnessKind.Jump && (tick == 0 || secondJump),
                                tick == 0 && witness.Kind == LayoutWitnessKind.Dash, tick == 0 && witness.Kind == LayoutWitnessKind.Drop);
                            float previousY = actor.Y;
                            inputs.Add(input); actor.Step(input); actor.Record(trace);
                            lastDeltaY = actor.Y - previousY;
                            if (!crossed && (actor.X - witness.Destination.X) * direction >= 0) crossed = true;
                            if (actor.InHazard(layout.Hazards)) break;
                            var region = witness.FinishRegion;
                            bool endpointContact = actor.ContainsHorizontal(witness.Destination.X) && actor.Y >= region.Y && actor.Y <= region.Top;
                            lastEndpointContact = endpointContact;
                            if (witness.Kind == LayoutWitnessKind.Dash)
                            {
                                reachedEndpoint |= endpointContact;
                                bool settled = reachedEndpoint && !actor.Dashing && actor.Grounded && Math.Abs(actor.Y - previousY) <= .0001f &&
                                    actor.ClearAt(new LayoutPoint(actor.X, actor.Y), actor.Facing, allowSettledSupport: true);
                                settledAfterDash = settled ? settledAfterDash + 1 : 0;
                                succeeded = settledAfterDash == 2;
                            }
                            else succeeded = crossed && endpointContact && (!witness.RequireGrounded || actor.Grounded);
                            if (succeeded) break;
                        }
                        string failure = succeeded ? null : "Common motor witness failed: " + witness.Id + " kind=" + witness.Kind +
                            " from=(" + witness.Start.X.ToString("R") + "," + witness.Start.Y.ToString("R") + ") destination=(" +
                            witness.Destination.X.ToString("R") + "," + witness.Destination.Y.ToString("R") + ") budget=" + budget +
                            " actualTicks=" + inputs.Count + " at=(" + actor.X.ToString("R") + "," + actor.Y.ToString("R") + ") grounded=" + actor.Grounded +
                            " crossed=" + crossed + " reachedEndpoint=" + reachedEndpoint + " dashing=" + actor.Dashing +
                            " settledAfterDash=" + settledAfterDash + " lastDeltaY=" + lastDeltaY.ToString("R") + " lastEndpointContact=" + lastEndpointContact +
                            " finalClear=" + actor.ClearAt(new LayoutPoint(actor.X, actor.Y), actor.Facing) + " clearance=" + actor.LastClearance +
                            " traceTail=" + string.Join(",", trace.GetRange(Math.Max(0, trace.Count - 27), Math.Min(27, trace.Count)));
                        results.Add(new LayoutWitnessResult(witness.Id, profile.ContentHash,
                            Hash(writer => { foreach (var input in inputs) { writer.Write(input.Axis); writer.Write(input.Jump); writer.Write(input.Dash); writer.Write(input.Drop); } }),
                            Hash(writer => { foreach (int bits in trace) writer.Write(bits); }), inputs.Count, succeeded, failure));
                        LastResult = new LayoutProofResult(layout.ContentHash, profile.ContentHash, results.ToArray());
                        if (!succeeded) { error = failure; return false; }
                    }
                }
                return true;
            }
            catch (Exception exception) { error = "Isolated motor proof failed: " + exception.Message; return false; }
            finally
            {
                views?.Dispose();
                SceneManager.UnloadSceneAsync(scene);
            }
        }
        static string Hash(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        sealed class MotorActor : IDisposable
        {
            readonly LayoutBodyProfile profile;
            readonly LayoutMotionProfile motion;
            readonly GameObject root;
            readonly BoxCollider2D clearance;
            readonly ActorPlatformState platforms = new ActorPlatformState();
            readonly Dictionary<Collider2D, ActorPlatformKey> platformKeys = new Dictionary<Collider2D, ActorPlatformKey>();
            readonly Dictionary<ActorPlatformKey, Collider2D> platformColliders = new Dictionary<ActorPlatformKey, Collider2D>();
            readonly List<ActorPlatformKey> unusedPlatforms = new List<ActorPlatformKey>();
            readonly UnityActorSurfaceQuery query;
            readonly PhysicsScene2D physics;
            readonly PlayerJumpExecution jumps;
            readonly PlayerDashExecution dash = new PlayerDashExecution();
            readonly LocomotionProfile movement;
            readonly PlatformQuery platformQuery;
            float now, velocityX, velocityY;
            int facing;
            bool hadGround;
            public float X, Y;
            public bool Grounded;
            public float VerticalVelocity => velocityY;
            public int JumpCount => jumps.Used;
            public bool Dashing => dash.IsDashing(now);
            public int Facing => facing;
            public bool ContainsHorizontal(float x)
            {
                var body = Body;
                return x >= body.CenterX - body.Width / 2 - .005f && x <= body.CenterX + body.Width / 2 + .005f;
            }
            bool wall;
            public string LastClearance { get; private set; }
            public MotorActor(LayoutBodyProfile profile, PhysicsScene2D physics, Scene scene)
            {
                this.profile = profile; motion = profile.Motion; this.physics = physics;
                root = new GameObject("LayoutMotorProbe"); SceneManager.MoveGameObjectToScene(root, scene);
                clearance = root.AddComponent<BoxCollider2D>(); clearance.isTrigger = true;
                clearance.size = new Vector2(profile.Width - .015f, profile.Height - .015f);
                query = new UnityActorSurfaceQuery(IsIgnored, physics);
                platformQuery = new PlatformQuery(this);
                jumps = new PlayerJumpExecution();
                if (!LocomotionProfile.TryCreate(new LocomotionData {
                    schemaVersion = LocomotionProfile.SchemaVersion, id = "captured-layout-movement", runSpeed = profile.WalkSpeed,
                    dashSpeed = motion.DashSpeed, dashDuration = motion.DashDuration, dashRecovery = motion.DashRecovery,
                    dashInvulnerability = motion.DashInvulnerability, dashJumpCarrySpeed = motion.DashJumpCarrySpeed,
                    dashJumpCarryDuration = motion.DashJumpCarryDuration, firstJumpVelocity = motion.FirstJump, secondJumpVelocity = motion.SecondJump,
                    riseGravity = motion.RiseGravity, fallGravity = motion.FallGravity }, out movement, out var error)) throw new InvalidOperationException(error);
            }
            bool IsIgnored(Collider2D collider) => collider && platformKeys.TryGetValue(collider, out var key) && platforms.Contains(key);
            ActorBodyFrame Body => new ActorBodyFrame(X, Y, profile.Width, profile.Height, profile.OffsetX, profile.OffsetY, facing);
            public void Reset(LayoutPoint point, int direction)
            {
                X = point.X; Y = point.Y; facing = direction; velocityY = now = 0; Grounded = hadGround = wall = false;
                jumps.Reset(); dash.Reset(); platforms.Reset(); root.transform.position = new Vector3(X, Y, 0);
            }
            public bool ClearAt(LayoutPoint point, int direction, bool allowSettledSupport = false)
            {
                LastClearance = "no blocking exact-shape separation";
                root.transform.position = new Vector3(point.X, point.Y, 0);
                clearance.offset = new Vector2(profile.OffsetX * direction, profile.OffsetY);
                Physics2D.SyncTransforms();
                var contacts = new List<Collider2D>();
                physics.OverlapBox(new Vector2(point.X + profile.OffsetX * direction, point.Y + profile.OffsetY), clearance.size, 0,
                    new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers }, contacts);
                foreach (var collider in contacts)
                {
                    var surface = collider ? collider.GetComponent<ArenaSurface>() : null;
                    if (!surface || surface.oneWay) continue;
                    var separation = clearance.Distance(collider);
                    if (!separation.isValid || separation.isOverlapped && separation.distance < -.0001f)
                    {
                        if (allowSettledSupport && OrdinarySupportingContact(collider, separation)) continue;
                        LastClearance = collider.name + " valid=" + separation.isValid + " overlapped=" + separation.isOverlapped + " distance=" + separation.distance.ToString("R") +
                            " normal=" + separation.normal + " pointA=" + separation.pointA + " pointB=" + separation.pointB +
                            " probeSize=" + clearance.size + " probeOffset=" + clearance.offset + " probeEdge=" + clearance.edgeRadius.ToString("R") +
                            " probeBounds=" + clearance.bounds + " contactOffset=" + Physics2D.defaultContactOffset.ToString("R") +
                            " compositeEdge=" + (collider is CompositeCollider2D composite ? composite.edgeRadius.ToString("R") : "not-composite") + " " + ContactDiagnostics();
                        return false;
                    }
                }
                return true;
            }
            bool OrdinarySupportingContact(Collider2D support, ColliderDistance2D separation)
            {
                float contactOffset = Physics2D.defaultContactOffset;
                if (!Grounded || !separation.isValid || separation.normal.y > -.8f || Math.Abs(separation.normal.x) > .1f ||
                    -separation.distance > contactOffset) return false;
                var body = Body;
                var hits = new List<RaycastHit2D>();
                var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers };
                physics.BoxCast(new Vector2(body.X + body.OffsetX * body.Facing, body.Y + .025f), new Vector2(body.Width - .08f, .04f),
                    0, Vector2.down, .055f, filter, hits);
                bool sameSupport = false;
                foreach (var hit in hits)
                    if (hit.collider == support && hit.normal.y > .8f && hit.point.y <= Y + .005f && Y - hit.point.y <= .06f) sameSupport = true;
                if (!sameSupport) return false;
                float firstHeight = 0;
                for (int sample = 0; sample < 3; sample++)
                {
                    hits.Clear();
                    float x = body.CenterX + (sample - 1) * (body.Width / 2 - .025f);
                    physics.Raycast(new Vector2(x, Y + .04f), Vector2.down, .10f, filter, hits);
                    bool found = false;
                    foreach (var hit in hits)
                        if (hit.collider == support && hit.normal.y > .99f && hit.point.y <= Y + .005f && Y - hit.point.y <= .06f)
                        {
                            if (sample == 0) firstHeight = hit.point.y;
                            if (Math.Abs(firstHeight - hit.point.y) > .02f) return false;
                            found = true; break;
                        }
                    if (!found) return false;
                }
                // Exclude only the engine's ordinary floor contact band. A wall
                // or ceiling belonging to this same composite must still fail
                // the actual upper-body shape distance, never its AABB proxy.
                var size = clearance.size; var offset = clearance.offset;
                try
                {
                    clearance.size = new Vector2(size.x, size.y - 2 * contactOffset);
                    clearance.offset = new Vector2(offset.x, offset.y + contactOffset);
                    Physics2D.SyncTransforms();
                    var upper = clearance.Distance(support);
                    return upper.isValid && (!upper.isOverlapped || upper.distance >= -.0001f);
                }
                finally { clearance.size = size; clearance.offset = offset; Physics2D.SyncTransforms(); }
            }
            public string ContactDiagnostics()
            {
                var body = Body; var text = new StringBuilder();
                text.Append("foot=(").Append(X.ToString("R")).Append(',').Append(Y.ToString("R")).Append(") facing=").Append(facing)
                    .Append(" size=(").Append(profile.Width.ToString("R")).Append(',').Append(profile.Height.ToString("R"))
                    .Append(") offset=(").Append(profile.OffsetX.ToString("R")).Append(',').Append(profile.OffsetY.ToString("R")).Append(") ");
                var hits = new List<RaycastHit2D>();
                physics.BoxCast(new Vector2(body.X + body.OffsetX * body.Facing, body.Y + .025f), new Vector2(body.Width - .08f, .04f), 0,
                    Vector2.down, .055f, new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers }, hits);
                text.Append("groundHits=").Append(hits.Count);
                foreach (var hit in hits)
                {
                    var collider = hit.collider;
                    text.Append(" [").Append(collider ? collider.name : "null").Append(" point=").Append(hit.point)
                        .Append(" distance=").Append(hit.distance.ToString("R")).Append(" normal=").Append(hit.normal)
                        .Append(" bounds=").Append(collider ? collider.bounds.ToString() : "null")
                        .Append(" surface=").Append(collider && collider.GetComponent<ArenaSurface>());
                    if (collider is CompositeCollider2D composite) text.Append(" paths=").Append(composite.pathCount).Append(" points=").Append(composite.pointCount);
                    text.Append(']');
                }
                return text.ToString();
            }
            public bool InHazard(IReadOnlyList<LayoutRect> hazards)
            {
                var body = profile.At(new LayoutPoint(X, Y), facing);
                for (int index = 0; index < hazards.Count; index++) if (body.Overlaps(hazards[index])) return true;
                return false;
            }
            public void Record(List<int> trace)
            {
                trace.Add(BitConverter.SingleToInt32Bits(now)); trace.Add(BitConverter.SingleToInt32Bits(X)); trace.Add(BitConverter.SingleToInt32Bits(Y));
                trace.Add(BitConverter.SingleToInt32Bits(velocityX)); trace.Add(BitConverter.SingleToInt32Bits(velocityY));
                trace.Add(Grounded ? 1 : 0); trace.Add(wall ? 1 : 0); trace.Add(facing); trace.Add(jumps.Used);
            }
            public void Step(LayoutMotionInput input)
            {
                if (input.Jump && jumps.Press(now, movement, out float jump))
                {
                    bool carry = dash.IsDashing(now); dash.Cancel(now);
                    velocityY = jump; Grounded = hadGround = false;
                    if (carry) dash.BeginJumpCarry(now, movement);
                }
                else if (input.Dash && dash.CanStart(now))
                {
                    SyncFacing(input.Axis == 0 ? facing : input.Axis);
                    dash.Begin(now, facing, movement); velocityY = 0; jumps.ClearBuffer();
                }
                else if (input.Drop)
                {
                    var drop = platforms.TryDrop(Body, velocityY, now, query, platformQuery);
                    if (drop.Applied) { Y += drop.DeltaY; velocityY = drop.VelocityY; Grounded = false; }
                }
                bool wasDash = dash.IsDashing(now); float horizontal = input.Axis * profile.WalkSpeed; bool freeze = false;
                dash.ApplyMotion(now, movement, ref horizontal, ref freeze);
                if (!dash.IsDashing(now) && input.Axis != 0) SyncFacing(input.Axis);
                var host = new MotorHost(this); ActorMotorExecution.Step(motion.Step, horizontal, freeze, ref host);
                if (dash.EndAtWall(now, wall) || wasDash && !dash.IsDashing(now)) velocityY = 0;
                if (!hadGround && Grounded)
                {
                    dash.ClearCarry();
                    if (jumps.Land(now, movement, out float buffered)) { velocityY = buffered; Grounded = false; }
                }
                hadGround = Grounded;
                now += motion.Step;
            }
            void SyncFacing(int direction)
            {
                var host = new FacingHost(this); ActorFacingExecution.Sync(direction, ref host);
            }
            void PrunePlatforms()
            {
                unusedPlatforms.Clear();
                foreach (var key in platformColliders.Keys) if (!platforms.Contains(key)) unusedPlatforms.Add(key);
                foreach (var key in unusedPlatforms)
                {
                    var collider = platformColliders[key]; platformColliders.Remove(key); platformKeys.Remove(collider);
                }
            }
            public void Dispose() { if (root) { root.SetActive(false); Object.DestroyImmediate(root); } }

            struct MotorHost : IActorMotorHost
            {
                readonly MotorActor actor;
                public MotorHost(MotorActor actor) { this.actor = actor; }
                public float Now => actor.now;
                public void ExpireShield(float now) { }
                public void RefreshPlatforms(float now) => actor.platforms.Refresh(now, actor.Y, actor.profile.Height, actor.platformQuery);
                public void PruneUnusedPlatforms() => actor.PrunePlatforms();
                public ActorBodyFrame BodyFrame => actor.Body;
                public IActorSurfaceQuery SurfaceQuery => actor.query;
                public float VelocityY { get => actor.velocityY; set => actor.velocityY = value; }
                public bool Grounded { get => actor.Grounded; set => actor.Grounded = value; }
                public bool WallHit { set => actor.wall = value; }
                public ActorMotionInput ReadMotionInput(float now, float step, float desiredX, bool freezeVertical)
                    => new ActorMotionInput(now, step, desiredX, actor.velocityY, actor.Grounded, freezeVertical,
                        actor.dash.IsDashing(now), false, false, 0, 0, 0, 0, 0, 0, actor.motion.RiseGravity, actor.motion.FallGravity);
                public void SetVelocity(float x, float y) { actor.velocityX = x; actor.velocityY = y; }
                public void MovePosition(float x, float y) { actor.X += x; actor.Y += y; actor.root.transform.position = new Vector3(actor.X, actor.Y, 0); }
                public void SyncTransforms() { Physics2D.SyncTransforms(); }
            }
            struct FacingHost : IActorFacingHost
            {
                readonly MotorActor actor;
                public FacingHost(MotorActor actor) { this.actor = actor; }
                public int Facing { get => actor.facing; set => actor.facing = value; }
                public float OffsetX => actor.profile.OffsetX;
                public float OffsetY => actor.profile.OffsetY;
                public void ReadPosition(out float x, out float y) { x = actor.X; y = actor.Y; }
                public ActorBodyFrame BodyFrame => actor.Body;
                public IActorPlacementQuery SurfaceQuery => actor.query;
                public void SetPosition(float x, float y) { actor.X = x; actor.Y = y; actor.root.transform.position = new Vector3(x, y, 0); }
                public bool HasBody => false;
                public void SetBodyOffset(float x, float y) { }
                public bool HasHurt => false;
                public void SetHurtOffset(float x, float y) { }
                public bool HasVisual => false;
                public void SetVisualFlip(bool flip) { }
                public bool IsPlayer => true;
                public void SetVisualPosition(float x, float y) { }
            }
            sealed class PlatformQuery : IActorPlatformQuery
            {
                readonly MotorActor actor;
                public PlatformQuery(MotorActor actor) { this.actor = actor; }
                public Guid Scope => actor.platforms.Scope;
                public IReadOnlyList<ActorPlatformContact> CastOneWayPlatforms(ActorBoxCast request)
                {
                    var hits = new List<RaycastHit2D>();
                    actor.physics.BoxCast(new Vector2(request.X, request.Y), new Vector2(request.Width, request.Height), 0,
                        new Vector2(request.DirectionX, request.DirectionY), request.Distance,
                        new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers }, hits);
                    var contacts = new List<ActorPlatformContact>();
                    foreach (var hit in hits)
                    {
                        var surface = hit.collider ? hit.collider.GetComponent<ArenaSurface>() : null;
                        if (!surface || !surface.oneWay) continue;
                        if (!actor.platformKeys.TryGetValue(hit.collider, out var key))
                        { key = new ActorPlatformKey(Scope, Guid.NewGuid()); actor.platformKeys.Add(hit.collider, key); actor.platformColliders.Add(key, hit.collider); }
                        contacts.Add(new ActorPlatformContact(key, hit.normal.y, hit.point.y, !surface.blockDropThrough));
                    }
                    return contacts.AsReadOnly();
                }
                public ActorPlatformObservation Observe(ActorPlatformKey key)
                {
                    bool live = actor.platformColliders.TryGetValue(key, out var collider) && collider;
                    return new ActorPlatformObservation(key, live);
                }
            }
        }
    }
}
