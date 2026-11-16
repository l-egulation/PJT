using System;
using Deviant.Control.Contracts;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Colour ink altar of the boss room (docs/design/equipment/rules.md, 공급원 표). It appears beside the
    // exit once the boss falls. F opens it (the chapter GUI owns the screen): pick one of the five colours, then one of up
    // to three candidates of that colour, which goes to the owned colour inks. The colour cannot be changed once its
    // candidates are shown. Leaving through the exit with candidates still unpicked discards them for good (user
    // decision 2026-10-31: the discarded candidates are not collectable).
    public sealed class Chapter1InkAltar : MonoBehaviour
    {
        public static readonly string[] Colors = { "red", "yellow", "green", "blue", "purple" };
        const float Reach = 1.6f;

        // The chapter that owns the altar: chapter 1's boss encounter, or chapter 2's boss exit (playtest 2026-11-06).
        Deviant.Combat.CombatArena arena;
        Func<bool> exitOpen;
        Func<WeaponGrowth> growth;
        Func<int> seed;
        SpriteRenderer body;
        IControlScope scope;
        System.Random random;
        public Vector2 Position { get; private set; }
        public KeycapMarker Key { get; private set; }
        public string Color { get; private set; }
        public string[] Candidates { get; private set; }
        public GrowthInk Claimed { get; private set; }
        public bool Discarded { get; private set; }
        public bool IsOpen => scope != null;
        // A chapter's first clear ends the run and goes home (run-structure.md), so there is nothing to equip the ink for:
        // the altar stays away then (user decision 2026-11-09). Decided once, when the exit opens: the clear settles the
        // profile at the exit, and the altar must not appear behind the result screen once the chapter counts as beaten.
        Func<bool> clearEndsRun;
        bool? skipped;
        public bool SkippedForFirstClear => skipped == true;
        public bool Available
        {
            get
            {
                if (arena && arena.Sketch != null) return false;
                if (!arena || exitOpen == null || !exitOpen()) return false;
                skipped ??= clearEndsRun != null && clearEndsRun();
                return !skipped.Value && Growth != null;
            }
        }
        public bool Finished => Claimed != null || Discarded;
        public WeaponGrowth Growth => growth?.Invoke();

        public void Initialize(ForestCombatWorld owner, Chapter1BossEncounter encounter, Vector2 position) =>
            Initialize(owner.arena, () => encounter && encounter.ExitOpen, () => owner ? owner.GetComponent<Chapter1RunBinding>()?.Run?.Growth : null,
                () => owner.GetComponent<Chapter1RunBinding>().Run.Seed, position, () => encounter && encounter.ClearEndsRun);

        public void Initialize(Deviant.Combat.CombatArena owner, Func<bool> open, Func<WeaponGrowth> runGrowth, Func<int> runSeed, Vector2 position, Func<bool> firstClear = null)
        {
            arena = owner; exitOpen = open; growth = runGrowth; seed = runSeed; Position = position; clearEndsRun = firstClear;
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = Before; body.sortingOrder = 5; body.enabled = false;
            if (arena.spriteMaterial) body.sharedMaterial = arena.spriteMaterial;
            transform.position = position;
            float top = body.sprite ? KeycapMarker.OpaqueTop(body) : position.y + 1.25f;
            Key = KeycapMarker.Create(transform.parent, 'F', new Vector2(position.x, top + .3f), arena.spriteMaterial, 6, Deviant.Combat.PlayerControls.For(arena.Player));
            Key.SetVisible(false);
        }

        public bool PlayerNear
        {
            get { var player = arena ? arena.Player : null; return player && !player.Dead && Vector2.Distance(player.Position, Position) <= Reach; }
        }

        public bool TryOpen()
        {
            if (IsOpen || !Available || Finished || !PlayerNear || arena.Controls.Current.SimulationPaused) return false;
            var next = arena.Controls.OpenScope("chapter ink altar");
            try { next.BlockInput(InputChannels.Combat | InputChannels.Interaction); next.PauseSimulation(); scope = next; }
            catch { next.Dispose(); throw; }
            return true;
        }

        public void Close() { scope?.Dispose(); scope = null; }

        public bool TryChooseColor(string color)
        {
            if (!IsOpen || Candidates != null || Array.IndexOf(Colors, color) < 0) return false;
            random ??= new System.Random(seed() ^ 0x51a7);
            Color = color; Candidates = Growth.AltarCandidates(color, random);
            return true;
        }

        public bool TryClaim(int index)
        {
            if (!IsOpen || Candidates == null || Finished || index < 0 || index >= Candidates.Length) return false;
            if(!Growth.TryGrantInk(Candidates[index],out var ink,out var error)){arena.GetComponent<Deviant.Inventory.Unity.InventoryRuntimeHost>()?.Presenter.NotifyPickupFailure(error);return false;}
            Claimed = ink;return true;
        }
        public bool TryReroll()
        {
            if (!IsOpen || Candidates == null || Finished || Growth == null) return false;
            if (!Growth.RerollAltar(Color, Candidates, random, out var next)) return false;
            Candidates = next; return true;
        }

        // Called when the chapter is left: candidates nobody picked are thrown away.
        public void DiscardUnclaimed() { if (Candidates != null && Claimed == null) Discarded = true; Close(); }

        void Update()
        {
            if (!body) return;
            Present();
            Key.SetVisible(Available && !Finished && !IsOpen && PlayerNear && !arena.Controls.Current.SimulationPaused);
        }

        void OnDestroy() { Close(); }

        // Approved-candidate altar art (Resources/Chapter1Art/InkAltar, 48x40 PPU32, bottom-centre): unused with the five
        // colour gems, used with dark gems; an 8-frame appearance when the boss falls and an 8-frame use once a candidate
        // is taken or the candidates are discarded. Animations only present; the rules above never wait for them.
        const string Art = "Chapter1Art/InkAltar/";
        static Sprite before, after;
        static Sprite Before => before ? before : before = Resources.Load<Sprite>(Art + "altar-before");
        static Sprite After => after ? after : after = Resources.Load<Sprite>(Art + "altar-after");
        SpriteFlipbook appear, use;
        readonly System.Collections.Generic.List<(SpriteRenderer, SpriteFlipbook)> discards = new System.Collections.Generic.List<(SpriteRenderer, SpriteFlipbook)>();
        public int DiscardEffects => discards.Count;
        void SpawnDiscard(Vector2 at)
        {
            var clip = new SpriteFlipbook(Art + "choice-discard"); if (!clip.Ready) return;
            var go = new GameObject("Altar candidate discard"); go.transform.SetParent(transform.parent, false); go.transform.position = at;
            var r = go.AddComponent<SpriteRenderer>(); r.sortingOrder = 7; if (arena.spriteMaterial) r.sharedMaterial = arena.spriteMaterial;
            clip.Play(); r.sprite = clip.Current; discards.Add((r, clip));
        }
        bool wasAvailable, wasFinished;

        void Present()
        {
            appear ??= new SpriteFlipbook(Art + "approved-appear"); use ??= new SpriteFlipbook(Art + "approved-use");
            bool available = Available;
            if (available && !wasAvailable) { appear.Play(); PresentationSfx.Play(PresentationSfx.AltarAppear); }
            if (Finished && !wasFinished)
            {
                use.Play();
                // Unpicked candidates sink and vanish above the altar, one per candidate (they were never owned inks).
                if (Discarded) PresentationSfx.Play(PresentationSfx.AltarDiscard);
                if (Discarded) for (int i = 0; i < Candidates.Length; i++) SpawnDiscard(new Vector2(Position.x + (i - (Candidates.Length - 1) / 2f) * .9f, Position.y + 1.8f));
            }
            for (int i = discards.Count - 1; i >= 0; i--)
            {
                var (renderer, clip) = discards[i];
                if (!clip.Playing) { if (renderer) Destroy(renderer.gameObject); discards.RemoveAt(i); continue; }
                renderer.sprite = clip.Current;
            }
            wasAvailable = available; wasFinished = Finished;
            body.enabled = available;
            body.sprite = use.Current ?? appear.Current ?? (Finished ? After : Before);
        }
    }
}
