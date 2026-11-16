using System;
using System.Collections;
using Deviant.Control.Contracts;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // Optional boss artwork supplied by scene content. Only base and hit frames are approved so far.
    public interface IChapter1BossArt
    {
        string BossName { get; }
        bool TryGetBossArt(out Sprite idle, out Sprite hit);
    }

    // Final boss room of the random stage: opens the chapter exit once the boss falls. The boss is the
    // stationary clock executioner of ClockExecutionerEncounter when the world has one; otherwise it
    // falls back to the combat boss dummy (HP 1000, 20 x 3 hits) with the village's approved frames.
    public sealed class Chapter1BossEncounter : MonoBehaviour
    {
        const float ExitReach = 1.6f, HitFrameSeconds = .15f;
        ForestCombatWorld world;
        Rect bossRoom;
        Sprite idle, hit;
        float lastHealth, hitUntil, introductionAt=-10;
        bool introductionShown;
        string bossName = "최종 보스";
        bool clockBoss; // adopted from ClockExecutionerEncounter, which owns its art, aggression and HUD
        public TestEnemy Boss { get; private set; }
        public Vector2 ExitPosition { get; private set; }
        public bool PlayerInBossRoom => world && world.arena.Player && bossRoom.Contains(world.arena.Player.Position);
        public bool ExitOpen => Binding && Binding.Run.BossDefeated;
        Chapter1RunBinding Binding => world ? world.GetComponent<Chapter1RunBinding>() : null;

        public void Initialize(ForestCombatWorld owner)
        {
            world = owner;
            var room = Array.Find(world.Data.rooms ?? Array.Empty<ForestRoom>(), r => r.kind == "boss");
            var marker = Array.Find(world.Data.landmarks ?? Array.Empty<ForestLandmark>(), l => l.kind == "boss");
            if (room == null || marker == null) { enabled = false; return; }
            bossRoom = new Rect(room.x, -(room.y + room.h), room.w, room.h);
            ExitPosition = new Vector2(room.x + room.w - 3, -(room.y + room.exitY));
            var clock = GetComponent<ClockExecutionerEncounter>();
            if (clock && clock.Boss) { Boss = clock.Boss; bossName = "시계탑 집행자"; clockBoss = true; }
            else
            {
                var art = GetComponent<IChapter1BossArt>();
                if (art != null) { bossName = art.BossName; art.TryGetBossArt(out idle, out hit); }
                Spawn(new Vector2(marker.x / 32, -marker.y / 32));
            }
            BuildExit();
            VillageInkPickup = gameObject.AddComponent<Chapter1VillageInkPickup>();
            VillageInkPickup.Initialize(owner, this);
        }

        SpriteRenderer exitView;
        bool exitIsArt;
        Sprite exitOpen, exitClosed;
        public bool ExitOpening { get; private set; }
        public bool ExitOpened { get; private set; }
        IControlScope exitScope;
        SpriteFlipbook exitOpening;
        public KeycapMarker ExitKey { get; private set; }
        public bool ExitVisible => exitView && exitView.enabled;
        // The exit uses its own arch art (Resources/Chapter1Art/Exit, 96x128 PPU32): hidden until the boss falls, then an
        // 8-frame opening ending on exit-open, with a bobbing F keycap above it. Without the art it falls back to a
        // plain panel. The sealed state is not shown (exit presentation is still open in the design).
        void BuildExit()
        {
            var go = new GameObject("Chapter1 exit"); go.transform.SetParent(transform, false);
            var floor = world.arena.FindFloor(ExitPosition + Vector2.up, 64);
            if (floor.HasValue) ExitPosition = new Vector2(ExitPosition.x, floor.Value);
            exitView = go.AddComponent<SpriteRenderer>();
            exitView.sortingOrder = 5;
            if (world.arena.spriteMaterial) exitView.sharedMaterial = world.arena.spriteMaterial;
            exitView.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            go.transform.position = ExitPosition + Vector2.up * 1.5f;
            go.transform.localScale = new Vector3(1.6f, 3f, 1);
            ExitKey = KeycapMarker.Create(transform, 'F', new Vector2(ExitPosition.x, ExitPosition.y + 3.25f), world.arena.spriteMaterial, 6, PlayerControls.For(world.arena.Player));
            ExitKey.SetVisible(false);
            exitOpen = Resources.Load<Sprite>("Chapter1Art/Exit/exit-open");
            exitClosed = Resources.Load<Sprite>("Chapter1Art/Exit/exit-closed");
            exitOpening = new SpriteFlipbook("Chapter1Art/Exit/approved-open");
            if (exitOpen)
            {
                exitIsArt = true; exitView.sprite = exitOpen; exitView.color = Color.white;
                go.transform.position = ExitPosition; go.transform.localScale = Vector3.one;
                ExitKey.MoveTo(new Vector2(ExitPosition.x, KeycapMarker.OpaqueTop(exitView) + .3f));
            }
            // The colour ink altar stands four units before the exit, on the same floor (user decision 2026-10-31).
            var at = ExitPosition + Vector2.left * 4;
            var altarFloor = world.arena.FindFloor(at + Vector2.up, 64);
            if (altarFloor.HasValue) at.y = altarFloor.Value;
            var altarObject = new GameObject("Chapter1 ink altar"); altarObject.transform.SetParent(transform, false);
            Altar = altarObject.AddComponent<Chapter1InkAltar>(); Altar.Initialize(world, this, at);
        }
        public Chapter1InkAltar Altar { get; private set; }
        // This exit ends the run and goes home (a chapter-1 first clear on a village-planned run); otherwise it carries on.
        public bool ClearEndsRun => Binding && Binding.ProfileRun && Chapter1ProfileHost.Service.ChapterOneClearEndsRun;
        public Chapter1VillageInkPickup VillageInkPickup { get; private set; }

        void Spawn(Vector2 at)
        {
            var floor = world.arena.FindFloor(at + Vector2.up, 64);
            if (floor.HasValue) at.y = floor.Value;
            Boss = world.arena.SpawnEnemy(2, at);
            Boss.name = bossName;
            Boss.aggressive = false;
            lastHealth = Boss.health;
            if (idle && Boss.visual)
            {
                // Approved frames use a foot pivot, so the sprite sits on the actor origin at native size.
                Boss.visual.transform.localPosition = Vector3.zero;
                Boss.visual.transform.localScale = Vector3.one;
                Boss.visual.sprite = idle; Boss.visual.color = Color.white;
            }
        }

        public bool TryUseExit()
        {
            var binding = Binding;
            if (!binding || !ExitOpen || ExitOpening || ExitOpened || binding.ShowingResult || binding.BossOfferPending || !world.arena.Player || world.arena.Player.Dead) return false;
            if (VillageInkPickup && VillageInkPickup.BlocksExit) return false;
            if (Vector2.Distance(world.arena.Player.Position, ExitPosition) > ExitReach) return false;
            // Banked sketch rewards are chosen before leaving the stage (rewards.md 받아 둔 보상).
            if (binding.Sketch && binding.Sketch.HoldExitForRewards()) return false;
            if (Altar) Altar.DiscardUnclaimed();
            ExitOpening=true;
            exitScope=world.arena.Controls.OpenScope("chapter exit portcullis");
            exitScope.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation);
            exitScope.PauseSimulation();
            StartCoroutine(OpenPortcullis());
            return true;
        }

        IEnumerator OpenPortcullis()
        {
            exitOpening.Play();PresentationSfx.Play(PresentationSfx.ExitOpen);
            while(exitOpening.Playing)yield return null;
            ExitOpening=false;ExitOpened=true;
            if(exitView)exitView.sprite=exitOpen;
            yield return new WaitForSecondsRealtime(.15f);
            exitScope?.Dispose();exitScope=null;
            // Chapter 1's exit always carries the run on to chapter 2 (ProgressionService.ChapterClearEndsRun).
            var binding=Binding;
            if(ClearEndsRun)binding.TryClearChapter();
            else Chapter1RunHost.ContinueChapter?.Invoke();
        }
        void OnDestroy(){exitScope?.Dispose();exitScope=null;}

        void Update()
        {
            if (!Boss || !world.arena.Player) return;
            // The walk-in and reveal own their cutscene. Start this four-second notice only when the fight actually begins.
            bool fightStarted=!clockBoss||Boss is ClockExecutionerBoss clock&&clock.Started;
            if(!fightStarted)introductionShown=false;
            if(!introductionShown&&fightStarted&&PlayerInBossRoom&&!Boss.Dead&&!world.arena.Controls.Current.SimulationPaused)
            {introductionShown=true;introductionAt=world.arena.Now;}
            // The boss engages only once the player is inside its room.
            if (!Boss.Dead && !clockBoss) Boss.aggressive = PlayerInBossRoom;
            if (Boss.health < lastHealth) hitUntil = Time.time + HitFrameSeconds;
            lastHealth = Boss.health;
            if (idle && Boss.visual && !Boss.Dead)
            {
                Boss.visual.sprite = hit && Time.time < hitUntil ? hit : idle;
                // Artwork faces left; mirror it toward the player.
                Boss.visual.flipX = world.arena.Player.Position.x > Boss.Position.x;
            }
            if (Boss.Dead && Boss.visual && !clockBoss) Boss.visual.enabled = false;
            if (exitView)
            {
                bool open = ExitOpen;
                if (exitIsArt) { exitView.enabled = open; exitView.sprite = ExitOpening ? (exitOpening.Current ?? exitOpen) : ExitOpened ? exitOpen : exitClosed ? exitClosed : exitOpen; }
                else exitView.color = ExitOpen ? new Color(.45f, 1f, .6f, .75f) : new Color(.35f, .35f, .4f, .75f);
            }
            if (ExitKey) ExitKey.SetVisible(ExitOpen && !ExitOpening && !ExitOpened && !(Binding && Binding.ShowingResult) && !(VillageInkPickup && VillageInkPickup.BlocksExit));
            if (!world.arena.Controls.Current.SimulationPaused)
                PlayerControls.TryPerform(world.arena.Player, Deviant.Input.Contracts.GameAction.Interact, TryUseExit);
        }

        public bool ExternalPresentation { get; set; }

        public string DisplayName => bossName;
        float IntroductionAge=>world.arena.Now-introductionAt;
        void OnGUI()
        {
            if(world&&world.Ready&&Boss&&!Boss.Dead&&!world.arena.Controls.Current.SimulationPaused&&introductionShown)
            {float age=IntroductionAge;if(age<4)ApprovedWorldNotice.Top(DisplayName,"전투 시작",Mathf.Clamp01((4-age)/.7f),50);}
            if (ExternalPresentation) return;
            if (!Boss || !world.Ready || world.arena.Controls.Current.SimulationPaused) return;
            var oldFont = GUI.skin.font;
            try
            {
                if (world.diagnostics && world.diagnostics.uiFont) GUI.skin.font = world.diagnostics.uiFont;
                if (PlayerInBossRoom && !Boss.Dead && !clockBoss)
                {
                    var bar = new Rect((Screen.width - 520) / 2f, 52, 520, 22);
                    GUI.Box(new Rect(bar.x - 6, bar.y - 24, bar.width + 12, bar.height + 30), bossName);
                    GUI.color = new Color(.25f, .05f, .05f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = new Color(.9f, .25f, .2f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(Boss.health / Mathf.Max(1, Boss.maxHealth)), bar.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                var point = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View, new Vector3(ExitPosition.x, ExitPosition.y + 2.6f, 0));
                if (point.z > 0)
                {
                    var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                    style.normal.textColor = ExitOpen ? new Color(.5f, 1f, .6f) : new Color(.75f, .75f, .75f);
                    GUI.Box(new Rect(point.x - 120, Screen.height - point.y - 16, 240, 32), ExitOpen ? "출구 / F 1챕터 클리어" : "출구 / 보스를 쓰러뜨려야 열림", style);
                }
            }
            finally { GUI.skin.font = oldFont; GUI.color = Color.white; }
        }
    }
}
