using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Inventory.Unity;
using UnityEngine;

namespace ForestCombatV2
{
    // One walking monster beyond the chest carries the key and runs from the player through every room but the boss room.
    // Chapter 1 and chapter 2 share it through IStageGimmickHost.
    public sealed class Chapter1KeyChest : MonoBehaviour
    {
        public static float CarrierHealth => CombatArena.LoadRunInkCatalog().keyCarrierHp;
        public const float FleeSpeed = 7.1f;
        public const int MaxClimbTiles = 6;
        // A drop-through deck the carrier stepped on stays off its route this long (user 2026-11-08), so it does not
        // bounce between the same decks. The route is planned again every ReplanSeconds; a step that makes no
        // progress for ProgressSeconds is given up and avoided for a while.
        public const float SteppedDeckSeconds = 10, ReplanSeconds = .4f, ProgressSeconds = 1.25f;
        // The carrier's starting room and its neighbours. It may run farther (Chapter1KeyCarrierRoute).
        public Rect CarrierArea { get; private set; }
        // Verification only: confines the carrier to landings touching this area.
        public void SetCarrierAreaForVerification(Rect area)
        {
            CarrierArea = area; route?.Restrict(area); ResetRoute();
        }
        public const int GoldMin = 50, GoldMax = 100, HealMin = 5, HealMax = 10;
        const string Art = "Chapter1Gimmicks/";

        IStageGimmickHost host;
        string artFolder = Art;
        System.Random random, routeRandom;
        bool prepared;
        Chapter1KeyCarrierRoute route;
        Chapter1KeyCarrierRoute.Landing wasOn;
        bool wasGrounded, replan = true;
        int kickDirection, stepFrom = -1, stepTo = -1, lastRoom = -1;
        float nextPlan, progressAt, bestDistance, kickUntil, kickReady;
        public Chapter1KeyCarrierRoute Route => route;
        SpriteRenderer carrierKey;
        Chapter1KeyAcquisitionEffect keyEffect;

        public FieldMouseEnemy Carrier { get; private set; }
        public Chapter1ChestActor Chest { get; private set; }
        public bool HasKey { get; private set; }
        public bool Opened { get; private set; }
        public Chapter1KeyAcquisitionEffect KeyEffect => keyEffect;
        public string LastReward { get; private set; }
        public int LastAmount { get; private set; }
        public Chapter1RewardPickup LastStamp { get; private set; }

        public IStageGimmickHost Host => host;
        public static Sprite LoadSprite(string name, Vector2 pivot) => LoadSprite(Art, name, pivot);
        // folder: a Resources folder ending with '/'; chapter 2 recolours the same sprites (Chapter2Gimmicks/).
        public static Sprite LoadSprite(string folder, string name, Vector2 pivot)
        {
            var texture = Resources.Load<Texture2D>(folder + name);
            if (!texture) return null;
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 32);
        }

        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding) => Initialize(new Chapter1GimmickHost(owner, runBinding));
        public void Initialize(IStageGimmickHost stage)
        {
            host = stage; artFolder = host.ArtFolder;
            host.EnemyDefeated += Defeated;
            host.Arena.ResetCompleted += ResetRoute;
        }
        Sprite ArtSprite(string name, Vector2 pivot) => LoadSprite(artFolder, name, pivot);
        void OnDestroy()
        {
            if (host != null) host.EnemyDefeated -= Defeated;
            if (host != null && host.Arena) host.Arena.ResetCompleted -= ResetRoute;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= MatchGlowFrame;
        }

        // The monster's own player sets its frame and facing late in the frame; copying them right before each
        // camera renders keeps the light on the drawn pose (playtest video 2026-11-05: the light was mirrored).
        void MatchGlowFrame(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            var body = Carrier ? Carrier.visual : null;
            if (!body) return;
            foreach (var (piece, _) in edge) if (piece) { piece.sprite = body.sprite; piece.flipX = body.flipX; piece.flipY = body.flipY; }
        }

        void Update()
        {
            if (host == null || !host.Ready) return;
            if (!prepared) Prepare();
            if (carrierKey && Carrier)
            {
                carrierKey.enabled = !Carrier.Dead;
                // The hurt box follows the body; the sprite canvas carries transparent margin.
                float top = Carrier.hurtCollider ? Carrier.hurtCollider.bounds.max.y : Carrier.Position.y + Carrier.size.y;
                carrierKey.transform.position = new Vector3(Carrier.Position.x, top + .2f + Mathf.Sin(Time.time * 3) * .06f, 0);
            }
            if (ChestKey)
            {
                bool offer = !Opened && PlayerNearChest && InteractionAllowed;
                ChestKey.SetVisible(offer);
                if (offer) PlayerControls.TryPerform(host.Arena.Player, GameAction.Interact, () => TryOpenChest());
            }
            TickNoKey();
        }

        const float ChestReach = 1.6f;

        // ---- No key yet: the key icon crossed out with a red slash pops over the chest, holds and fades.
        SpriteRenderer noKey, noKeySlash;
        float noKeyAt = -10;
        public bool NoKeyShowing => noKey && noKey.enabled;
        const float NoKeyPop = .12f, NoKeyHold = 1.1f, NoKeyFade = .35f;

        void Locked()
        {
            Chest.Shake();
            if (!noKey)
            {
                noKey = new GameObject("No key").AddComponent<SpriteRenderer>();
                noKey.transform.SetParent(transform, false);
                noKey.sprite = ArtSprite("key-item", new Vector2(.5f, .5f));
                noKey.sortingOrder = 31;
                noKeySlash = new GameObject("Slash").AddComponent<SpriteRenderer>();
                noKeySlash.transform.SetParent(noKey.transform, false);
                noKeySlash.sprite = SlashSprite();
                noKeySlash.sortingOrder = 32;
                if (host.Arena.spriteMaterial) noKey.sharedMaterial = noKeySlash.sharedMaterial = host.Arena.spriteMaterial;
            }
            noKeyAt = Time.time;
            TickNoKey();
        }

        void TickNoKey()
        {
            if (!noKey) return;
            float age = Time.time - noKeyAt;
            bool show = !Opened && age < NoKeyHold + NoKeyFade;
            noKey.enabled = noKeySlash.enabled = show;
            if (!show) return;
            float top = Chest.visual && Chest.visual.sprite ? Chest.visual.bounds.max.y : Chest.Position.y + 1.7f;
            // Above the F keycap; pops up a little and overshoots, then fades.
            float pop = Mathf.Clamp01(age / NoKeyPop), scale = 1.5f * (pop < 1 ? Mathf.Lerp(.4f, 1.15f, pop) : 1);
            noKey.transform.position = new Vector3(Chest.Position.x, top + 1.15f + .15f * Mathf.Sqrt(pop), 0);
            noKey.transform.localScale = new Vector3(scale, scale, 1);
            float alpha = 1 - Mathf.Clamp01((age - NoKeyHold) / NoKeyFade);
            noKey.color = noKeySlash.color = new Color(1, 1, 1, alpha);
        }

        // A red slash from top left to bottom right (across the key, which lies bottom left to top right) over the 24 px key, with a 1 px ink edge, at the key's 32 px per unit.
        static Sprite slashSprite;
        static Sprite SlashSprite()
        {
            if (slashSprite) return slashSprite;
            const int size = 24;
            var red = new Color32(0xD8, 0x3A, 0x2C, 255); var ink = new Color32(0x2A, 0x1D, 0x17, 255);
            var pixels = new Color32[size * size];
            bool Core(int x, int y) { int d = x + y - (size - 1); return d >= -1 && d <= 1 && x >= 2 && x <= size - 3; }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (Core(x, y)) { pixels[y * size + x] = red; continue; }
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++) for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && x + dx < size && y + dy >= 0 && y + dy < size && Core(x + dx, y + dy)) { edge = true; break; }
                    if (edge) pixels[y * size + x] = ink;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "No key slash" };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            slashSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            return slashSprite;
        }
        public KeycapMarker ChestKey { get; private set; }
        public bool PlayerNearChest
        {
            get { var p = host != null && host.Arena ? host.Arena.Player : null; return p && !p.Dead && Chest && Vector2.Distance(p.Position, Chest.Position) <= ChestReach; }
        }

        bool InteractionAllowed => host != null && host.Ready && host.Arena && host.Arena.Player &&
            !host.Arena.Player.Dead && !host.Arena.Player.InputBlocked &&
            !host.Arena.Controls.Current.SimulationPaused && !host.Arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;

        // Interact at the locked chest: opens with the key, shakes without it. False when unavailable or out of reach.
        public bool TryOpenChest(bool ignoreReach = false)
        {
            if (!Chest || Opened || !InteractionAllowed || !ignoreReach && !PlayerNearChest) return false;
            Struck();
            return true;
        }

        public void Prepare()
        {
            if (prepared) return;
            prepared = true;
            random = new System.Random(unchecked(host.Seed * 4099 + 23));
            var site = host.Chest;
            if (site == null) return;
            var go = new GameObject("Locked chest");
            go.transform.SetParent(transform, false);
            Chest = go.AddComponent<Chapter1ChestActor>();
            Chest.Initialize(host.Arena, site.Marker, ArtSprite("locked-chest-closed", new Vector2(.5f, 0)), ArtSprite("locked-chest-open", new Vector2(.5f, 0)));
            // The locked chest opens with F (user decision 2026-11-05), never to a hit. A hit without the key still shakes it
            // and shows the crossed-out key, so a locked chest does not look broken (playtest 2026-11-06).
            Chest.Struck += () => { if (!Opened && !HasKey) Locked(); };
            float top = Chest.visual && Chest.visual.sprite ? Chest.visual.bounds.max.y : Chest.Position.y + 1.7f;
            ChestKey = KeycapMarker.Create(transform, 'F', new Vector2(Chest.Position.x, top + .3f), host.Arena.spriteMaterial, 6, PlayerControls.For(host.Arena.Player));
            ChestKey.SetVisible(false);
            ChooseCarrier();
        }

        // A main-route combat room beyond the locked chest room, so the chest is seen first (playtest 2026-11-05).
        public static bool CarrierRoom(ForestData data, ForestRoom room)
        {
            var chest = System.Array.Find(data.rooms, r => r.kind == "chest");
            return chest != null && room.kind == "combat" && room.mainPath && room.x > chest.x;
        }

        // Only plain walking monsters carry the key (user 2026-11-05: no fixed monsters). Species with their own
        // movement (the barrel gunner and rolling wheel are FieldMouseEnemy subclasses) or no walk never run away.
        public static bool CanCarry(FieldMouseEnemy mob) =>
            mob && !mob.Dead && mob.GetType() == typeof(FieldMouseEnemy) && Chapter1EnemyRole.Of(mob) == Chapter1EnemyRole.Normal &&
            mob.Definition != null && !mob.Definition.immovable && mob.Definition.walkSpeed > .1f;

        void ChooseCarrier()
        {
            var candidates = host.CarrierCandidates().ToList();
            if (candidates.Count == 0) return;
            Carrier = candidates[random.Next(candidates.Count)];
            host.BindCarrier(Carrier);
            Carrier.maxHealth = CarrierHealth; Carrier.health = CarrierHealth;
            Carrier.MovementOverride = SeekPlatform; Carrier.IgnoresKnockback = true; Carrier.IgnoresHitStagger = true;
            // Only the key is granted: no gold, ink or pigment, and no fixed ink table.
            Carrier.gameObject.AddComponent<Chapter1NoKillReward>();
            if (!Carrier.GetComponent<Deviant.Inventory.Unity.InkDropSource>()) Carrier.gameObject.AddComponent<Deviant.Inventory.Unity.InkDropSource>().tableKey = "";
            // Its room and the rooms next to it, with the passages between (playtest 2026-11-06: it may run into the next room).
            var home = host.CarrierHome(Carrier);
            if (home.width > 0) CarrierArea = home;
            route = host.BuildCarrierRoute(Carrier.size, FleeSpeed, host.Arena.Movement.FallGravity, MaxClimbTiles);
            routeRandom = new System.Random(unchecked(host.Seed * 7919 + 11));
            var icon = new GameObject("Carried key");
            icon.transform.SetParent(Carrier.transform, false);
            carrierKey = icon.AddComponent<SpriteRenderer>();
            carrierKey.sprite = ArtSprite("key-item", new Vector2(.5f, 0));
            carrierKey.sortingOrder = 30;
            if (host.Arena.spriteMaterial) carrierKey.sharedMaterial = host.Arena.spriteMaterial;
            BuildGlow();
        }

        // Carrier light (HTML prototype: key colour #d9a531, pulsing). A stepped pixel halo sits behind the body and
        // a few pale specks drift up and fade, so the carrier reads as glowing rather than outlined.
        public static readonly Color GlowColor = new Color32(217, 165, 49, 255);
        static readonly Color SparkColor = new Color32(255, 236, 168, 255);
        const int Sparks = 6;
        SpriteRenderer halo;
        readonly List<SpriteRenderer> sparks = new List<SpriteRenderer>();
        readonly List<(Vector2 offset, float born, float life)> sparkState = new List<(Vector2, float, float)>();
        System.Random sparkRandom;
        // Silhouette rings 1, 2 and 3 px out: bright pale gold at the edge fading to the key colour (playtest
        // 2026-11-05: the light should follow the outline).
        // Clear warm yellow, faint at the edge and fading out over four rings (playtest 2026-11-05: the first
        // version read as a muddy, heavy band).
        static readonly (float reach, float alpha, Color color)[] Rings =
        {
            (1, .5f, new Color32(255, 240, 150, 255)), (2, .3f, new Color32(255, 228, 112, 255)),
            (3, .16f, new Color32(255, 214, 90, 255)), (4, .07f, new Color32(255, 204, 80, 255)),
        };
        readonly List<(SpriteRenderer renderer, int ring)> edge = new List<(SpriteRenderer, int)>();
        public int GlowPieces => (halo ? 1 : 0) + sparks.Count + edge.Count;

        static Sprite haloSprite, speckSprite;
        // 64 x 80 px at PPU 32: three stepped rings, warm core, transparent edge, point-filtered like the art.
        static Sprite HaloSprite()
        {
            if (haloSprite) return haloSprite;
            const int w = 64, h = 80;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Key carrier halo" };
            var pixels = new Color32[w * h];
            Color core = new Color32(255, 244, 190, 255), rim = new Color32(255, 214, 90, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + .5f - w / 2f) / (w / 2f), dy = (y + .5f - h / 2f) / (h / 2f), d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d < .42f ? .34f : d < .64f ? .22f : d < .84f ? .12f : d < 1f ? .05f : 0;
                    var c = Color.Lerp(core, rim, Mathf.Clamp01(d * 1.2f)); c.a = a;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return haloSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32);
        }
        static Sprite SpeckSprite()
        {
            if (speckSprite) return speckSprite;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Key carrier speck" };
            texture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            texture.Apply(false, true);
            return speckSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 32);
        }

        void BuildGlow()
        {
            var body = Carrier.visual;
            // URP sprite shader: flipX and colours follow the normal sprite path (the legacy GUI/Text shader
            // ignored flipX and mixed the monster's dark colours into a muddy olive, playtest 2026-11-05).
            var shader = Resources.Load<Shader>("Chapter1GimmickShaders/KeyCarrierLight");
            if (!body || !shader) return;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += MatchGlowFrame;
            var material = new Material(shader) { name = "Key carrier light" }; material.SetFloat("_Solid", 0);
            halo = new GameObject("Key carrier halo").AddComponent<SpriteRenderer>();
            halo.transform.SetParent(Carrier.transform, false);
            halo.sprite = HaloSprite(); halo.sharedMaterial = material;
            halo.sortingLayerID = body.sortingLayerID; halo.sortingOrder = body.sortingOrder - 1;
            {
                var edgeMaterial = new Material(shader) { name = "Key carrier edge light" }; edgeMaterial.SetFloat("_Solid", 1);
                for (int ring = 0; ring < Rings.Length; ring++)
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4 + (ring % 2 == 1 ? Mathf.PI / 8 : 0);
                        var piece = new GameObject("Key carrier edge light").AddComponent<SpriteRenderer>();
                        piece.transform.SetParent(body.transform, false);
                        piece.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (Rings[ring].reach / 32f);
                        piece.sharedMaterial = edgeMaterial; piece.sortingLayerID = body.sortingLayerID;
                        edge.Add((piece, ring));
                    }
            }
            sparkRandom = new System.Random(unchecked(host.Seed * 31 + 7));
            for (int i = 0; i < Sparks; i++)
            {
                var speck = new GameObject("Key carrier speck").AddComponent<SpriteRenderer>();
                speck.transform.SetParent(Carrier.transform, false);
                speck.sprite = SpeckSprite(); speck.sharedMaterial = material;
                speck.sortingLayerID = body.sortingLayerID; speck.sortingOrder = body.sortingOrder + 1;
                sparks.Add(speck); sparkState.Add((Vector2.zero, -10, 1));
            }
        }

        void LateUpdate() => RefreshGlow();
        public void RefreshGlow()
        {
            if (!halo) return;
            bool shown = Carrier && Carrier.visual && Carrier.visual.enabled && !Carrier.Dead;
            halo.enabled = shown;
            foreach (var speck in sparks) if (speck) speck.enabled = shown;
            foreach (var (piece, _) in edge) if (piece) piece.enabled = shown;
            if (!shown) return;
            var bodyArt = Carrier.visual; float glowPulse = .75f + .25f * Mathf.Sin(Time.time * 6);
            foreach (var (piece, ring) in edge)
            {
                if (!piece) continue;
                piece.sprite = bodyArt.sprite; piece.flipX = bodyArt.flipX; piece.flipY = bodyArt.flipY;
                var tint = Rings[ring].color; tint.a = Rings[ring].alpha * glowPulse; piece.color = tint;
                piece.sortingOrder = bodyArt.sortingOrder - 1;
            }
            halo.sortingOrder = bodyArt.sortingOrder - 2;
            // Centre on the body (the hurt box), pulse brightness and breathe slightly in size.
            var box = Carrier.hurtCollider ? Carrier.hurtCollider.bounds : new Bounds(Carrier.Position + Vector2.up, Vector3.one * 2);
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 4);
            halo.transform.position = new Vector3(box.center.x, box.center.y, 0);
            float size = Mathf.Max(box.size.x * 1.6f, box.size.y * 1.25f) / 2.5f;
            halo.transform.localScale = Vector3.one * size * (1 + .05f * pulse);
            var c = Color.white; c.a = .7f + .3f * pulse; halo.color = c;
            float now = Time.time;
            for (int i = 0; i < sparks.Count; i++)
            {
                var (offset, born, life) = sparkState[i];
                float age = now - born;
                if (age > life)
                {
                    offset = new Vector2(((float)sparkRandom.NextDouble() - .5f) * box.size.x * 1.2f, ((float)sparkRandom.NextDouble() - .3f) * box.size.y);
                    born = now - (float)sparkRandom.NextDouble() * .2f; life = .8f + (float)sparkRandom.NextDouble() * .6f; age = 0;
                    sparkState[i] = (offset, born, life);
                }
                float t = age / life;
                var at = (Vector2)box.center + offset + Vector2.up * (t * .9f);
                sparks[i].transform.position = new Vector3(Mathf.Round(at.x * 32) / 32, Mathf.Round(at.y * 32) / 32, 0);
                var sc = SparkColor; sc.a = t < .2f ? t / .2f : 1 - (t - .2f) / .8f; sparks[i].color = sc;
            }
        }

        public string LastDecision { get; private set; } = "none";

        void ResetRoute()
        {
            route?.Reset(); kickUntil = kickReady = 0; wasOn = null; wasGrounded = false; replan = true;
            stepFrom = stepTo = lastRoom = -1;
        }

        // Verification: when the landing under this point may be chosen again (0 when it is free).
        public float LandingBlockedUntil(Vector2 at)
        {
            var l = route?.StandingOn(at);
            return l != null ? route.SteppedUntil(l.Index) : 0;
        }

        bool RouteUses(Chapter1KeyCarrierRoute.Landing l)
        {
            var path = route.Path;
            for (int k = 0; k < path.Count && k < 2; k++) if (route.Landings[path[k]].Deck == l.Deck) return true;
            return false;
        }

        float SeekPlatform(FieldMouseEnemy mob, float dt)
        {
            mob.MovementAllowsDrop = true;
            if (route == null) return 0;
            float now = host.Arena.Now, half = mob.size.x / 2;
            var feet = mob.Position;
            var on = mob.grounded ? route.StandingOn(feet) : null;
            // A drop-through deck counts as stepped on when the carrier leaves it.
            if (wasOn != null && wasOn.Droppable && (on == null || on.Deck != wasOn.Deck)) route.Stepped(wasOn, now + SteppedDeckSeconds);
            bool landed = on != null && !wasGrounded;
            wasGrounded = mob.grounded; wasOn = on;
            // Landing on a deck still resting from a step: fall straight through it unless the route needs it.
            if (landed && on.Droppable && route.SteppedUntil(on.Index) > now && !RouteUses(on) && mob.DropThrough())
            {
                wasOn = null; wasGrounded = false; LastDecision = "pass-through";
                return 0;
            }
            var under = on ?? route.Below(feet);
            if (under != null && under.Room >= 0 && under.Room != lastRoom) { lastRoom = under.Room; route.EnterRoom(under.Room); }
            if (now >= nextPlan || replan)
            {
                var player = host.Arena.Player;
                var threat = player && !player.Dead ? player.Position : feet + new Vector2(100000, 0);
                route.Plan(now, under, feet, threat, routeRandom);
                nextPlan = now + ReplanSeconds; replan = false;
            }
            if (now < kickUntil) return kickDirection * FleeSpeed;
            var path = route.Path;
            if (on != null && path.Count >= 2 && on.Index == path[1]) path.RemoveAt(0);
            else if (on != null && path.Count >= 1 && on.Index != path[0]) { replan = true; return 0; }
            if (path.Count < 2)
            {
                if (on != null && route.Goal == on.Index)
                {
                    // Fleeing runs straight on to the next room; wandering rests a moment.
                    if (!route.Fleeing) route.Pause(now + .3f + (float)routeRandom.NextDouble() * .6f);
                    route.ClearGoal(); replan = route.Fleeing;
                }
                LastDecision = "idle";
                return 0;
            }
            var from = route.Landings[path[0]]; var to = route.Landings[path[1]];
            var edge = route.EdgeBetween(from.Index, to.Index);
            if (edge == null) { replan = true; return 0; }
            if (from.Index != stepFrom || to.Index != stepTo) { stepFrom = from.Index; stepTo = to.Index; bestDistance = float.PositiveInfinity; progressAt = now; }
            float aim = to.Clamp(feet.x, half + .12f);
            float distance = Vector2.Distance(new Vector2(aim, to.Y), feet);
            if (distance < bestDistance - .1f) { bestDistance = distance; progressAt = now; }
            if (now - progressAt > ProgressSeconds)
            {
                route.Fail(from.Index, to.Index, now); replan = true; stepFrom = stepTo = -1; LastDecision = "give-up";
                return 0;
            }
            if (!mob.grounded)
            {
                if (edge.Kind == Chapter1KeyCarrierRoute.Move.Jump && mob.wallHit && to.Y > feet.y && now >= kickReady)
                {
                    kickDirection = -mob.facing; kickUntil = now + .12f; kickReady = now + .36f;
                    mob.VelocityY = Mathf.Sqrt(2 * host.Arena.Movement.RiseGravity * 3.6f);
                    LastDecision = "wall-jump";
                    return kickDirection * FleeSpeed;
                }
                LastDecision = "air";
                return Toward(aim, feet.x, dt);
            }
            float overlap0 = Mathf.Max(from.X0, to.X0) + half + .05f, overlap1 = Mathf.Min(from.X1, to.X1) - half - .05f;
            switch (edge.Kind)
            {
                case Chapter1KeyCarrierRoute.Move.Drop:
                {
                    float x = Mathf.Clamp(feet.x, overlap0, Mathf.Max(overlap0, overlap1));
                    if (Mathf.Abs(x - feet.x) > .08f) { LastDecision = "approach"; return Toward(x, feet.x, dt); }
                    if (mob.DropThrough()) { route.Stepped(from, now + SteppedDeckSeconds); wasOn = null; wasGrounded = false; LastDecision = "drop"; }
                    else { route.Fail(from.Index, to.Index, now); replan = true; }
                    return 0;
                }
                case Chapter1KeyCarrierRoute.Move.Fall:
                    LastDecision = "approach";
                    return Toward(edge.Side > 0 ? from.X1 + half + .3f : from.X0 - half - .3f, feet.x, dt);
                case Chapter1KeyCarrierRoute.Move.Jump:
                {
                    float x = to.Y > from.Y + .05f && overlap1 > overlap0 ? Mathf.Clamp(feet.x, overlap0, overlap1) : edge.TakeOff;
                    if (Mathf.Abs(x - feet.x) > .12f) { LastDecision = "approach"; return Toward(x, feet.x, dt); }
                    // Give the feet clearance above the target before descending onto it.
                    float height = Mathf.Clamp(to.Y - from.Y + .8f, 2, MaxClimbTiles);
                    mob.VelocityY = Mathf.Sqrt(2 * host.Arena.Movement.RiseGravity * height);
                    LastDecision = "jump";
                    return Toward(aim, feet.x, dt);
                }
                default:
                    LastDecision = route.Fleeing ? "flee" : "wander";
                    return Toward(aim, feet.x, dt);
            }
        }
        static float Toward(float x, float from, float dt) => Mathf.Clamp((x - from) / dt, -FleeSpeed, FleeSpeed);

        // Verification captures open the chest without replaying the chase.
        public void GrantKeyForVerification() { if (!Opened) HasKey = true; }
        // Verification only: takes the first card of the chest's ink-or-blueprint choice so captures can go on.
        public void TakeChoiceForVerification() { var sketch = host?.Run?.Sketch; if (sketch != null && sketch.PendingIsEvent) sketch.Choose(0); }

        void Defeated(TestEnemy enemy)
        {
            if (!Carrier || enemy != Carrier || HasKey || Opened) return;
            carrierKey.enabled = false;
            HasKey = true;
            var go = new GameObject("Key acquired");
            go.transform.SetParent(transform, false);
            keyEffect = go.AddComponent<Chapter1KeyAcquisitionEffect>();
            keyEffect.Initialize(host.Arena, ArtSprite("key-item", new Vector2(.5f, .5f)));
        }

        void Struck()
        {
            if (Opened) return;
            if (!HasKey) { Locked(); return; }
            if (ChestKey) ChestKey.SetVisible(false);
            HasKey = false; Opened = true;
            Chest.Open();
            Grant(Chest.Position);
        }

        void Grant(Vector2 at)
        {
            var run = host.Run;
            if (run == null || run.Ended) return;
            // Sketch-weapon runs (user decision 2026-11-13): the key opens a choice of an unregistered ink or a blueprint
            // weapon instead of gold, health and a stamp. The choice pauses the game until one is taken.
            if (run.Sketch != null)
            {
                LastReward = "choice"; LastAmount = 0; LastStamp = null;
                run.Sketch.OpenChestOffer();
                return;
            }
            // The development blueprint path without sketch weapons keeps the earlier gold-or-health and stamp reward
            // (chapter 1 only; other stages restore health).
            var world = (host as Chapter1GimmickHost)?.World;
            if (!world)
            {
                LastReward = "health"; LastAmount = HealMin + random.Next(HealMax - HealMin + 1); LastStamp = null;
                host.Arena.Player.Heal(LastAmount);
                return;
            }
            if (random.Next(2) == 0)
            {
                LastReward = "gold"; LastAmount = GoldMin + random.Next(GoldMax - GoldMin + 1);
                var goldObject = new GameObject("Reward · Gold");
                goldObject.transform.SetParent(transform, false);
                var gold = goldObject.AddComponent<Chapter1RewardPickup>();
                gold.Initialize(world, run.Stash, Chapter1RewardKind.Gold, null, world.arena.DropPoint(at, .9f, .75f));
                gold.SetGold(run, LastAmount);
            }
            else
            {
                LastReward = "health"; LastAmount = HealMin + random.Next(HealMax - HealMin + 1);
                var player = world.arena.Player;
                player.Heal(LastAmount);
            }
            // The chest's stamp is guaranteed only while the stage stamp limit still has room.
            LastStamp = null;
            if (run.Economy != null && !run.Economy.TryTakeStageStamp(Deviant.Run.StampStages.Chapter1)) return;
            var rewards = world.GetComponent<Chapter1Rewards>();
            var stamp = ChooseChestStamp(rewards ? rewards.ChosenBlueprintKinds() : new List<string>(), random);
            var stampObject = new GameObject("Reward · Stamp");
            stampObject.transform.SetParent(transform, false);
            LastStamp = stampObject.AddComponent<Chapter1RewardPickup>();
            var kind = stamp.Kind == Deviant.Run.StampKind.Weapon ? Chapter1RewardKind.WeaponStamp : Chapter1RewardKind.SkillStamp;
            LastStamp.Initialize(world, run.Stash, kind, null, world.arena.DropPoint(at, .4f, 1f), at + Vector2.up * .6f);
            LastStamp.ConfigureStamp(stamp);
        }

        // Locked chest stamp (playtest 2026-11-06, replacing the colored ink part): a form stamp for one of the blueprints
        // the player carries this run, or a skill stamp, with even odds; only the skill stamp when no blueprint is carried.
        public static Deviant.Run.RunStamp ChooseChestStamp(IList<string> blueprintKinds, System.Random random) =>
            Chapter1Rewards.RollStageStamp(blueprintKinds != null && blueprintKinds.Count > 0 && random.Next(2) == 0, blueprintKinds, random);
    }

    // The locked chest takes hits like a monster but never takes damage: every swing reaches Struck once.
    public sealed class Chapter1ChestActor : TestEnemy
    {
        public event System.Action Struck;
        Sprite closed, open;
        float lastStruck = -10, shakeUntil;
        Vector3 home;
        public bool IsOpen { get; private set; }
        protected override bool ReceivesAutomaticHitReaction => false;
        protected override bool AcceptsIncomingControl => false;
        protected override bool AcceptsHitOrigin(CombatHitOrigin origin)
        {
            if (!IsOpen && arena.Now - lastStruck > .25f) { lastStruck = arena.Now; Struck?.Invoke(); }
            return false;
        }
        public void Initialize(CombatArena owner, Vector2 at, Sprite closedSprite, Sprite openSprite)
        {
            closed = closedSprite; open = openSprite;
            var dimensions = closed ? (Vector2)closed.bounds.size : new Vector2(1.8f, 1.6f);
            Configure(owner, at, dimensions, new Vector2(0, dimensions.y / 2), false);
            spawn = at; maxHealth = 1; health = 1; isBoss = true; bodyCollider.enabled = false; aggressive = false;
            var view = new GameObject("Presentation"); view.transform.SetParent(transform, false);
            visual = view.AddComponent<SpriteRenderer>(); visual.sortingOrder = 8;
            visual.sprite = closed ? closed : owner.TechnicalSprite;
            if (!closed) { visual.color = new Color(.55f, .4f, .25f); visual.transform.localScale = new Vector3(dimensions.x, dimensions.y, 1); visual.transform.localPosition = new Vector3(0, dimensions.y / 2, 0); }
            if (owner.spriteMaterial) visual.sharedMaterial = owner.spriteMaterial;
            home = visual.transform.localPosition;
        }
        public void Shake() => shakeUntil = arena.Now + .35f;
        public void Open()
        {
            IsOpen = true;
            if (open) visual.sprite = open;
            if (hurtCollider) hurtCollider.enabled = false;
        }
        public override void Step(float dt) { }
        public override void ResetEnemy() { }
        void Update()
        {
            if (!visual || !arena) return;
            float left = shakeUntil - arena.Now;
            visual.transform.localPosition = home + (left > 0 ? new Vector3(Mathf.Sin(left * 90) * .06f * left / .35f, 0, 0) : Vector3.zero);
        }
    }

    // Visual feedback only: ownership is granted immediately when the carrier is defeated.
    public sealed class Chapter1KeyAcquisitionEffect : MonoBehaviour
    {
        const float RiseSeconds = .8f;
        CombatArena arena;
        SpriteRenderer body;
        float acquiredAt;

        public void Initialize(CombatArena owner, Sprite sprite)
        {
            arena = owner;
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = sprite ? sprite : owner.TechnicalSprite; body.sortingOrder = 50;
            if (!sprite) { body.color = new Color(1, .82f, .3f); transform.localScale = Vector3.one * .5f; }
            if (owner.spriteMaterial) body.sharedMaterial = owner.spriteMaterial;
            acquiredAt = owner.Now;
            Update();
        }

        void Update()
        {
            if (!arena || !arena.Player) return;
            // Above the player's head, easing up and fading in the last 40%.
            float u = Mathf.Clamp01((arena.Now - acquiredAt) / RiseSeconds);
            transform.position = arena.Player.Position + Vector2.up * (arena.Player.size.y + .3f + 1.06f * (1 - (1 - u) * (1 - u)));
            var c = body.color; c.a = u < .6f ? 1 : 1 - (u - .6f) / .4f; body.color = c;
            if (u >= 1) Destroy(gameObject);
        }
    }
}
