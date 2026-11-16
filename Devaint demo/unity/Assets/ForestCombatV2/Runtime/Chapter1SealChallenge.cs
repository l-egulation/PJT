using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // Seal challenge room (docs/design/world/stage-gimmicks.md). F at the tablet drops iron gates on the doors and
    // starts three pages of 8, 10 and 12 monsters (doubled, user decision 2026-11-13) that fall from the sky one by one, each after its shadow. Clearing
    // every page opens the gates, heals like a cleared combat room and raises a reward chest. There is no time limit
    // (user decision 2026-11-05). One try per stage. Chapter 1 and chapter 2 share it through IStageGimmickHost.
    public sealed class Chapter1SealChallenge : MonoBehaviour
    {
        public enum ChallengeState { Dormant, Active, Succeeded }
        public static readonly int[] PageSizes = { 8, 10, 12 };
        public const float FirstPageDelay = .7f, NextPageDelay = .9f, ShadowLead = .5f;
        public const float SpawnSpacing = .32f, SpawnJitter = .15f, PlayerClearance = 3.75f, MonsterClearance = 2.2f;
        public const float GateWidth = .75f;
        const float Reach = 1.6f, GateDropSeconds = .33f;

        sealed class Drop { public Vector2 floor; public float at; public string species; public SpriteRenderer shadow; public FieldMouseEnemy enemy; }

        IStageGimmickHost host;
        System.Random random;
        StageGimmickSite site;
        SpriteRenderer tablet;
        readonly List<(BoxCollider2D collider, Transform art)> gates = new List<(BoxCollider2D, Transform)>();
        readonly List<Drop> drops = new List<Drop>();
        readonly List<string> species = new List<string>();
        float gateShown = -10, pageAt, startedAt, completedNoticeAt=-10;
        Sprite dormant, active, broken, shadowSprite;

        public ChallengeState State { get; private set; }
        public int Page { get; private set; }
        public Vector2 TabletPosition { get; private set; }
        public KeycapMarker Key { get; private set; }
        public Chapter1ChestActor RewardChest { get; private set; }
        public bool GatesShut => gates.Count > 0 && gates[0].collider.enabled;
        public StageGimmickSite Site => site;
        // Chapter 1 only: the forest map room (verification and debug travel use its id and tiles).
        public ForestRoom Room => (host as Chapter1GimmickHost)?.SealRoom;
        public IStageGimmickHost Host => host;
        public event Action<Chapter1SealChallenge> RewardOpened;
        public IEnumerable<FieldMouseEnemy> Spawned { get { foreach (var d in drops) if (d.enemy) yield return d.enemy; } }

        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding) => Initialize(new Chapter1GimmickHost(owner, runBinding));
        public void Initialize(IStageGimmickHost stage) { host = stage; }
        Sprite Art(string name, Vector2 pivot) => Chapter1KeyChest.LoadSprite(host.ArtFolder, name, pivot);

        public bool Prepare()
        {
            if (site != null) return true;
            if (host == null || !host.Ready) return false;
            site = host.Seal;
            if (site == null) { enabled = false; return false; }
            random = new System.Random(unchecked(host.Seed * 6151 + 41));
            // Falling monsters are the normal species already placed in this stage's combat rooms.
            species.AddRange(host.SealSpecies);
            dormant = Art("seal-dormant", new Vector2(.5f, 0));
            active = Art("seal-active", new Vector2(.5f, 0));
            broken = Art("seal-broken", new Vector2(.5f, 0));
            shadowSprite = Art("fall-shadow-decal", new Vector2(.5f, .5f));
            TabletPosition = site.Marker;
            var go = new GameObject("Seal tablet"); go.transform.SetParent(transform, false); go.transform.position = TabletPosition;
            tablet = go.AddComponent<SpriteRenderer>(); tablet.sortingOrder = 5;
            tablet.sprite = dormant ? dormant : host.Arena.TechnicalSprite;
            if (!dormant) { tablet.color = new Color(.5f, .5f, .55f); go.transform.localScale = new Vector3(1.4f, 2, 1); }
            if (host.Arena.spriteMaterial) tablet.sharedMaterial = host.Arena.spriteMaterial;
            float top = dormant ? KeycapMarker.OpaqueTop(tablet) : TabletPosition.y + 2;
            Key = KeycapMarker.Create(transform, 'F', new Vector2(TabletPosition.x, top + .3f), host.Arena.spriteMaterial, 6, PlayerControls.For(host.Arena.Player));
            Key.SetVisible(false);
            foreach (var gate in site.Gates) BuildGate(gate);
            host.Arena.ResetCompleted += ResetChallenge;
            return true;
        }

        void BuildGate(Rect box)
        {
            float bottom = box.yMin, height = box.height;
            var go = new GameObject("Seal gate"); go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(box.center.x, bottom + height / 2);
            go.AddComponent<ArenaSurface>();
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = new Vector2(GateWidth, height); collider.enabled = false;
            var art = new GameObject("Seal gate art").transform; art.SetParent(go.transform, false);
            var body = Art("iron-gate-body", new Vector2(.5f, 0));
            var tip = Art("iron-gate-tip", new Vector2(.5f, 0));
            var bars = new GameObject("Bars").AddComponent<SpriteRenderer>(); bars.transform.SetParent(art, false);
            if (body)
            {
                var tiled = Sprite.Create(body.texture, body.rect, new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect);
                bars.sprite = tiled; bars.drawMode = SpriteDrawMode.Tiled; bars.size = new Vector2(GateWidth, height - .75f);
                bars.transform.localPosition = new Vector3(0, -height / 2 + .75f, 0);
            }
            else { bars.sprite = host.Arena.TechnicalSprite; bars.color = new Color(.25f, .25f, .28f); bars.transform.localScale = new Vector3(GateWidth * 4, height * 4, 1); }
            bars.sortingOrder = 9;
            if (tip)
            {
                var end = new GameObject("Tip").AddComponent<SpriteRenderer>(); end.transform.SetParent(art, false);
                end.sprite = tip; end.sortingOrder = 9; end.transform.localPosition = new Vector3(0, -height / 2, 0);
                if (host.Arena.spriteMaterial) end.sharedMaterial = host.Arena.spriteMaterial;
            }
            if (host.Arena.spriteMaterial) bars.sharedMaterial = host.Arena.spriteMaterial;
            art.gameObject.SetActive(false);
            gates.Add((collider, art));
            gateBoxes.Add(box);
        }
        readonly List<Rect> gateBoxes = new List<Rect>();

        // Verification captures show the gates fully down without waiting for the drop.
        public void SnapGatesForVerification() { gateShown = host.Arena.Now - GateDropSeconds; foreach (var (_, art) in gates) art.localPosition = Vector3.zero; }

        public bool PlayerNear
        {
            get { var p = host != null && host.Arena ? host.Arena.Player : null; return p && !p.Dead && Vector2.Distance(p.Position, TabletPosition) <= Reach; }
        }

        bool InteractionAllowed => host != null && host.Ready && host.Arena && host.Arena.Player &&
            !host.Arena.Player.Dead && !host.Arena.Player.InputBlocked &&
            !host.Arena.Controls.Current.SimulationPaused && !host.Arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;

        public bool TryStart()
        {
            if (!Prepare() || State != ChallengeState.Dormant || !PlayerNear || !InteractionAllowed) return false;
            State = ChallengeState.Active; startedAt = host.Arena.Now; Page = 0; pageAt = startedAt + FirstPageDelay;
            SetGates(true);
            return true;
        }

        void SetGates(bool shut)
        {
            foreach (var (collider, art) in gates) { collider.enabled = shut; art.gameObject.SetActive(shut); }
            gateShown = host.Arena.Now;
            // Anything standing in a doorway is pushed into the room.
            var player = host.Arena.Player;
            if (shut && player)
            {
                float middle = (site.DropLeft + site.DropRight) / 2;
                foreach (var box in gateBoxes)
                {
                    if (box.center.x < middle && player.Position.x < box.xMax + .5f) player.Position = new Vector2(box.xMax + .6f, player.Position.y);
                    if (box.center.x >= middle && player.Position.x > box.xMin - .5f) player.Position = new Vector2(box.xMin - .6f, player.Position.y);
                }
            }
            Physics2D.SyncTransforms();
        }

        void PlanPage()
        {
            int count = PageSizes[Page];
            var player = host.Arena.Player;
            var used = new List<float>();
            for (int i = 0; i < count; i++)
            {
                Vector2 floor = default; bool found = false;
                for (int tries = 0; tries < 30 && !found; tries++)
                {
                    float x = site.DropLeft + (float)random.NextDouble() * (site.DropRight - site.DropLeft);
                    if (Mathf.Abs(x - player.Position.x) < PlayerClearance && tries < 29) continue;
                    if (used.Exists(u => Mathf.Abs(u - x) < MonsterClearance) && tries < 29) continue;
                    // Block rooms have a solid frame above the interior: probe from below it.
                    var y = host.Arena.FindFloor(new Vector2(x, site.DropTop), site.DropTop - site.DropBottom + 1);
                    if (!y.HasValue || y.Value < site.DropBottom) continue;
                    floor = new Vector2(x, y.Value); found = true; used.Add(x);
                }
                if (!found) floor = new Vector2(TabletPosition.x + (i - count / 2f) * 1.5f, TabletPosition.y);
                drops.Add(new Drop { floor = floor, at = host.Arena.Now + .5f + i * SpawnSpacing + (float)random.NextDouble() * SpawnJitter,
                    species = species.Count > 0 ? species[random.Next(species.Count)] : "01_thorn-root" });
            }
        }

        bool PageAlive()
        {
            foreach (var d in drops) if (d.enemy == null || !d.enemy.Dead) return true;
            return false;
        }

        void Update()
        {
            if (!Prepare()) return;
            var arena = host.Arena;
            bool paused = arena.Controls.Current.SimulationPaused;
            Key.SetVisible(State == ChallengeState.Dormant && PlayerNear && InteractionAllowed);
            tablet.sprite = (State == ChallengeState.Dormant ? dormant : State == ChallengeState.Active ? active : broken) ?? tablet.sprite;
            float drop = Mathf.Clamp01((arena.Now - gateShown) / GateDropSeconds);
            foreach (var (collider, art) in gates) art.localPosition = new Vector3(0, (1 - drop) * collider.size.y, 0);
            if (State == ChallengeState.Dormant)
            {
                PlayerControls.TryPerform(arena.Player, GameAction.Interact, TryStart);
                return;
            }
            if (State == ChallengeState.Active && !paused) Step();
            if (ChestKey)
            {
                bool offer = RewardChest && !RewardChest.IsOpen && PlayerNearChest && InteractionAllowed;
                ChestKey.SetVisible(offer);
                if (offer) PlayerControls.TryPerform(arena.Player, GameAction.Interact, TryOpenReward);
            }
        }

        public void Step()
        {
            var arena = host.Arena;
            float now = arena.Now;
            if (drops.Count == 0 && now >= pageAt) PlanPage();
            foreach (var d in drops)
            {
                if (d.enemy) continue;
                if (!d.shadow && now >= d.at - ShadowLead) d.shadow = Shadow(d.floor);
                if (d.shadow) d.shadow.color = new Color(1, 1, 1, Mathf.Clamp01(1 - (d.at - now) / ShadowLead));
                if (now < d.at) continue;
                float sky = Mathf.Min(site.DropTop - .5f, d.floor.y + 9);
                d.enemy = host.SpawnSealMonster(d.species, new Vector2(d.floor.x, sky), d.floor.x < arena.Player.Position.x ? 1 : -1);
                if (!TryResolveDrop(arena, site, d.floor.x, d.enemy.size, d.enemy.offset, d.enemy.facing, out var floor, out var start))
                {
                    host.RemoveSealMonster(d.enemy); d.enemy = null;
                    continue;
                }
                if (Mathf.Abs(floor.x - d.floor.x) > .01f)
                {
                    // A relocated drop must announce its new column for the full warning time.
                    host.RemoveSealMonster(d.enemy); d.enemy = null;
                    d.floor = floor; d.at = now + ShadowLead;
                    if (d.shadow) Destroy(d.shadow.gameObject);
                    d.shadow = null;
                    continue;
                }
                d.floor = floor;
                d.enemy.Position = start;
                d.enemy.spawn = floor;
                d.enemy.velocity = Vector2.zero;
                d.enemy.grounded = false;
                Physics2D.SyncTransforms();
                d.enemy.distantIdleRange = Vector2.zero;
                // Like every chapter 1 monster, no per-enemy fixed ink table: rewards come from Chapter1Rewards only.
                if (!d.enemy.GetComponent<Deviant.Inventory.Unity.InkDropSource>()) d.enemy.gameObject.AddComponent<Deviant.Inventory.Unity.InkDropSource>().tableKey = "";
                // The seal room pays its reward chest only: page monsters drop no ink (user decision 2026-11-05).
                d.enemy.gameObject.AddComponent<Chapter1NoInkReward>();
                if (d.shadow) Destroy(d.shadow.gameObject);
            }
            if (drops.Count == 0 || PageAlive()) return;
            ClearDrops(false);
            Page++;
            if (Page >= PageSizes.Length) { Finish(); return; }
            pageAt = now + NextPageDelay;
        }

        // A centre ray can miss a wall that intersects the monster's shoulders. Validate
        // the entire body and descent column, lowering the entry below an overhang if needed.
        public static bool TryResolveDrop(CombatArena arena, StageGimmickSite site, float requestedX,
            Vector2 size, Vector2 offset, int facing, out Vector2 floor, out Vector2 start)
        {
            floor = start = default;
            float shift = offset.x * facing;
            int steps = Mathf.CeilToInt((site.DropRight - site.DropLeft) / .25f);
            for (int i = 0; i <= steps * 2; i++)
            {
                float x = requestedX + (i == 0 ? 0 : (i + 1) / 2 * .25f * (i % 2 == 1 ? 1 : -1));
                if (x < site.DropLeft || x > site.DropRight) continue;
                var ground = arena.FindFloor(new Vector2(x + shift, site.DropTop), site.DropTop - site.DropBottom + 1);
                if (!ground.HasValue || ground.Value < site.DropBottom) continue;
                float feet = ground.Value + size.y / 2 - offset.y + .02f;
                float sky = Mathf.Min(site.DropTop - offset.y - size.y / 2 - .02f, feet + 9);
                for (float y = sky; y >= feet; y = Mathf.Max(feet, y - .25f))
                {
                    float height = size.y + y - feet;
                    var center = new Vector2(x + shift, feet + offset.y + (y - feet) / 2);
                    if (!arena.SolidOverlap(center, new Vector2(size.x + .04f, height)))
                    {
                        floor = new Vector2(x, feet); start = new Vector2(x, y); return true;
                    }
                    if (y == feet) break;
                }
            }
            return false;
        }

        SpriteRenderer Shadow(Vector2 at)
        {
            var r = new GameObject("Fall shadow").AddComponent<SpriteRenderer>(); r.transform.SetParent(transform, false);
            r.transform.position = at + Vector2.up * .1f; r.sortingOrder = 4;
            r.sprite = shadowSprite ? shadowSprite : host.Arena.TechnicalSprite;
            if (!shadowSprite) { r.transform.localScale = new Vector3(8, 1.5f, 1); }
            if (host.Arena.spriteMaterial) r.sharedMaterial = host.Arena.spriteMaterial;
            return r;
        }

        // Clears the page bookkeeping; on failure the living monsters are removed from the arena.
        void ClearDrops(bool removeLiving)
        {
            foreach (var d in drops)
            {
                if (d.shadow) Destroy(d.shadow.gameObject);
                if (removeLiving && d.enemy && !d.enemy.Dead) host.RemoveSealMonster(d.enemy);
            }
            drops.Clear();
        }

        void Finish()
        {
            ClearDrops(false);
            State = ChallengeState.Succeeded; completedNoticeAt=host.Arena.Now;
            SetGates(false);
            var player = host.Arena.Player;
            float heal = host.SealClearHealth;
            if (player && !player.Dead) player.Heal(heal);
            var go = new GameObject("Seal reward chest"); go.transform.SetParent(transform, false);
            RewardChest = go.AddComponent<Chapter1ChestActor>();
            RewardChest.Initialize(host.Arena, TabletPosition + Vector2.right * 2.5f,
                Art("reward-chest-closed", new Vector2(.5f, 0)), Art("reward-chest-open", new Vector2(.5f, 0)));
            // The reward chest opens with F (user decision 2026-11-05), not with a hit: it takes no hits at all.
            if (RewardChest.hurtCollider) RewardChest.hurtCollider.enabled = false;
            float top = RewardChest.visual && RewardChest.visual.sprite ? RewardChest.visual.bounds.max.y : RewardChest.Position.y + 1.7f;
            ChestKey = KeycapMarker.Create(transform, 'F', new Vector2(RewardChest.Position.x, top + .3f), host.Arena.spriteMaterial, 6, PlayerControls.For(host.Arena.Player));
            ChestKey.SetVisible(false);
        }

        public KeycapMarker ChestKey { get; private set; }
        public bool PlayerNearChest
        {
            get { var p = host != null && host.Arena ? host.Arena.Player : null; return p && !p.Dead && RewardChest && Vector2.Distance(p.Position, RewardChest.Position) <= Reach; }
        }

        public bool TryOpenReward()
        {
            if (!RewardChest || RewardChest.IsOpen || !PlayerNearChest || !InteractionAllowed) return false;
            OpenReward();
            return true;
        }

        void OpenReward()
        {
            if (RewardChest.IsOpen) return;
            RewardChest.Open();
            RewardOpened?.Invoke(this);
        }

        // The development reset (F5) revives placed monsters; the challenge returns to its untouched state.
        void ResetChallenge()
        {
            ClearDrops(true);
            if (RewardChest) Destroy(RewardChest.gameObject);
            if (ChestKey) Destroy(ChestKey.gameObject);
            RewardChest = null; State = ChallengeState.Dormant; Page = 0;
            SetGates(false);
        }

        void OnDestroy() { if (host != null && host.Arena) host.Arena.ResetCompleted -= ResetChallenge; }

        // Opening the chest makes its instruction obsolete, even while the four-second notice timer remains.
        bool CompletionNoticeVisible => State==ChallengeState.Succeeded && RewardChest && !RewardChest.IsOpen &&
            host!=null && host.Arena && host.Arena.Now-completedNoticeAt<4;

        // Actual challenge page, completion and proximity states drive the approved world presentation.
        void OnGUI()
        {
            if(host==null||!host.Ready||!host.Arena||host.Arena.Controls.Current.SimulationPaused)return;
            // Speech-frame prompts went back to the old key caps over the tablet and the chest (2026-11-14).
            if(State==ChallengeState.Active)ApprovedWorldNotice.SealPages(Page,PageSizes.Length);
            if(State==ChallengeState.Succeeded)
            {
                float age=host.Arena.Now-completedNoticeAt;
                if(CompletionNoticeVisible)ApprovedWorldNotice.Top("봉인이 풀렸습니다","보상 상자를 열어 증강을 선택하세요.",Mathf.Clamp01((4-age)/.7f));
            }
        }

    }

    // Marks a monster whose defeat pays no ink (Chapter1Rewards still pays its gold).
    public sealed class Chapter1NoInkReward : MonoBehaviour { }

    // Marks a monster whose defeat pays nothing at all (the key carrier drops only its key).
    public sealed class Chapter1NoKillReward : MonoBehaviour { }
}
