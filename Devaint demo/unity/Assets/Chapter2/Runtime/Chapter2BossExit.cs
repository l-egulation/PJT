using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Control.Contracts;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Chapter2
{
    // The Rose·Blue room's exit and colour ink altar (playtest 2026-11-06: clearing chapter 2 showed neither). Like
    // chapter 1's boss room (Chapter1BossEncounter): once the boss falls the exit appears at Layout.exit with an F keycap,
    // and the colour ink altar stands four units before it on the same floor. F at the door discards unpicked altar
    // candidates, plays the opening, then clears the chapter (Chapter2World).
    // The door is the chapter 3 great-library entrance (resources environments/stage2-library-exit-20261113, user
    // decision 2026-11-13): a 6x7u bookcase double door, bottom-centre pivot, eight opening frames at 80 ms.
    public sealed class Chapter2BossExit : MonoBehaviour
    {
        public const float Reach = 1.6f;
        public const string DoorRoot = "Chapter2LibraryExit/";
        Chapter2World world;
        SpriteRenderer view;
        Sprite open, closed;
        SpriteFlipbook opening;
        IControlScope scope;
        Action passed;
        public Vector2 Position { get; private set; }
        public KeycapMarker Key { get; private set; }
        public Chapter1InkAltar Altar { get; private set; }
        public VillageInkPickup VillageInkPickup { get; private set; }
        public bool Opening { get; private set; }
        public bool Opened { get; private set; }
        public bool Unlocked => world && world.Boss && world.Boss.Dead;
        public bool Visible => view && view.enabled;
        bool sketchBossReward;

        public void Initialize(Chapter2World owner, Vector2 at, int seed, Action onPassed)
        {
            world = owner; passed = onPassed; Position = at;
            var arena = owner.arena;
            var arch = new GameObject("Chapter2 exit"); arch.transform.SetParent(transform, false); arch.transform.position = at;
            view = arch.AddComponent<SpriteRenderer>(); view.sortingOrder = 5; view.enabled = false;
            if (arena.spriteMaterial) view.sharedMaterial = arena.spriteMaterial;
            open = Resources.Load<Sprite>(DoorRoot + "door-open");
            closed = Resources.Load<Sprite>(DoorRoot + "door-closed");
            opening = new SpriteFlipbook(DoorRoot + "opening");
            view.sprite = closed ? closed : open;
            float top = view.sprite ? KeycapMarker.OpaqueTop(view) : at.y + 3;
            Key = KeycapMarker.Create(transform, 'F', new Vector2(at.x, top + .3f), arena.spriteMaterial, 6, PlayerControls.For(arena.Player));
            Key.SetVisible(false);
            var altarObject = new GameObject("Chapter2 ink altar"); altarObject.transform.SetParent(transform, false);
            Altar = altarObject.AddComponent<Chapter1InkAltar>();
            // Chapter 2 is the last chapter: its clear goes home, so no altar (user decisions 2026-11-09, 2026-11-13).
            Altar.Initialize(arena, () => Unlocked, () => arena.MvpGrowth, () => seed, at + Vector2.left * 4,
                () => world.SourceRun?.Plan != null && !world.SourceRun.Plan.Training && Chapter1ProfileHost.Service.ChapterClearEndsRun(2));
            VillageInkPickup = gameObject.AddComponent<VillageInkPickup>();
            VillageInkPickup.Initialize(arena, () => world && world.Ready && world.Boss,
                () => world.Boss.Dead, () => world.Boss.Blue.Position, () => Position.x,
                () => world.SourceRun?.Plan != null && !world.SourceRun.Plan.Training && Chapter1ProfileHost.Service.PalettePieces < 2, 2);
        }

        public bool TryUse()
        {
            var arena = world ? world.arena : null;
            if (!arena || !Unlocked || Opening || Opened || world.Complete || world.ShowingResult || world.BossOfferPending || world.Boss.DefeatPresentationPlaying || !arena.Player || arena.Player.Dead) return false;
            if (Vector2.Distance(arena.Player.Position, Position) > Reach) return false;
            if (VillageInkPickup && VillageInkPickup.BlocksExit) return false;
            // Banked sketch rewards are chosen before leaving the stage (rewards.md 받아 둔 보상).
            var sketch = world.GetComponent<ForestCombatV2.SketchRunBinding>();
            if (sketch && sketch.HoldExitForRewards()) return false;
            if (Altar) Altar.DiscardUnclaimed();
            Opening = true;
            scope = arena.Controls.OpenScope("chapter2 exit portcullis");
            scope.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            scope.PauseSimulation();
            StartCoroutine(OpenPortcullis());
            return true;
        }

        IEnumerator OpenPortcullis()
        {
            opening.Play(); PresentationSfx.Play(PresentationSfx.ExitOpen);
            while (opening.Playing) yield return null;
            Opening = false; Opened = true;
            yield return new WaitForSecondsRealtime(.15f);
            scope?.Dispose(); scope = null;
            passed?.Invoke();
        }

        void Update()
        {
            if (!view || !world) return;
            bool unlocked = Unlocked;
            if (unlocked && !sketchBossReward && world.arena.Sketch != null &&
                !world.Boss.DefeatPresentationPlaying && !world.BossOfferPending)
            {
                // A run-bound world owns the boss reward and its outro wait. Only a standalone scene
                // needs this fallback; opening either reward early would pause the defeat pose's clock.
                if (world.SourceRun == null) world.arena.Sketch.RecordKill(true);
                sketchBossReward = true;
            }
            view.enabled = unlocked;
            view.sprite = Opening ? (opening.Current ?? open) : Opened ? open : closed ? closed : open;
            if (Key) Key.SetVisible(unlocked && !Opening && !Opened && !world.ShowingResult && !(VillageInkPickup && VillageInkPickup.BlocksExit));
        }

        void OnDestroy() { scope?.Dispose(); scope = null; }
    }
}
