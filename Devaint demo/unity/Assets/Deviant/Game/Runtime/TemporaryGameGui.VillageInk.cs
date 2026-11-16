using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Narrative.Opening;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        VillageInkRevealView villageInkView;
        VillageInkPourView villageInkPour;
        int approachingInk;
        float inkApproachAt;
        bool inkWalkStarted;
        Text inkConfirm;
        IControlScope inkRewardLock;
        int inkRewardPiece;
        float inkRewardAt;
        const string VillageInkStoryPrefix = "village-ink-return-";
        public bool VillageInkShowing => approachingInk != 0 || villageInkPour && villageInkPour.Playing;
        public bool VillageInkRewardShowing => villageInkView && villageInkView.Playing;
        public bool VillageInkModalShowing => inkRewardLock != null;
        public string VillageInkPhase => approachingInk != 0 ? "approach" : villageInkPour ? villageInkPour.Phase : "idle";
        public static string VillageInkStory(int piece) => VillageInkStoryPrefix + piece;
        static string InkRewardStory(int piece) => "village-ink-reward-" + piece;

        // Older saves recorded the completed artisan visit before separate ink-use flags existed.
        bool InkReturned(int piece) => Profile.Snapshot.HasSeen(VillageInkStory(piece)) || Profile.Snapshot.HasSeen(VisitStory(piece));
        int ReturnedInkPieces
        {
            get { int count = 0; while (count < Profile.PalettePieces && InkReturned(count + 1)) count++; return count; }
        }
        int PendingWellInk => ReturnedInkPieces < Profile.PalettePieces ? ReturnedInkPieces + 1 : 0;
        int pocketInk;
        bool wellInkReturn;

        void RefreshCarriedVillageInk()
        {
            int piece = PendingWellInk;
            if (piece == pocketInk) return;
            pocketInk = piece;
            var player = village.World.arena.Player;
            if (piece > 0) VillageInkPocketGlow.Carry(player, piece);
            else player.GetComponent<VillageInkPocketGlow>()?.Release();
        }

        // A later ink (the first plays with the artisan's first visit) waits at the well until the player pours it.
        bool WellInkReady => PendingWellInk >= 2 && !openingActive && !VillageInkModalShowing && !storyActive && !StoryRequired && !ChapterSceneFade.Busy;

        bool TryPourWellInk()
        {
            if (!WellInkReady) return false;
            StartWellInkReturn(PendingWellInk); return true;
        }

        void StartWellInkReturn(int piece)
        {
            wellInkReturn = true;
            var script = new OpeningScript { schemaVersion = 1, id = "well-ink-" + piece, beats = new[] {
                new OpeningBeat { id = "pour", part = 1, trigger = new OpeningTrigger { type = "start" }, actions = new[] {
                    new OpeningAction { type = "lockInput" }, new OpeningAction { type = "bloom", piece = piece },
                    new OpeningAction { type = "waitAnimation" }, new OpeningAction { type = "unlockInput" }
                } }
            } };
            openingActive = true; escHeldSince = -1; visitPiece = piece; visitBeats = 1; leaveAt = -1;
            village.SceneCamera = true;
            opening = new OpeningDirector(script, new OpeningStage(this), 1);
        }

        void BuildVillageInk()
        {
            villageInkView = Rect("마을 잉크 획득", stage, 0, 0, 960, 540).gameObject.AddComponent<VillageInkRevealView>();
            villageInkView.Configure(boldFont ? boldFont : font);
            var go = new GameObject("마을 잉크 흘려보내기"); go.transform.SetParent(village.transform, false);
            villageInkPour = go.AddComponent<VillageInkPourView>(); villageInkPour.Configure();
            villageInkPour.Poured += piece =>
            {
                village.BloomPiece(piece); Profile.MarkStorySeen(VillageInkStory(piece));
                PresentationSfx.Play("story-ink-awaken");
            };
            inkConfirm = BindPrompt(Label("", stage, 360, 502, 240, MenuHintHeight, MenuHintFontSize, KeyFace), "[{Interact}] 확인");
            Deviant.UI.Theme.ApprovedTheme.ControlHint(inkConfirm);
            inkConfirm.alignment = TextAnchor.MiddleCenter;
            var shadow = inkConfirm.gameObject.AddComponent<Shadow>(); shadow.effectColor = InkColour; shadow.effectDistance = new Vector2(1,-1);
            inkConfirm.gameObject.SetActive(false);
        }
        void BeginVillageInk(int piece)
        {
            if (Profile.Snapshot.HasSeen(VillageInkStory(piece))) { village.ColourPieces = piece; return; }
            var well = village.Find(StartingVillageWorld.ReturnWell);
            var player = village.World.arena.Player;
            player.facing = well.Position.x < player.Position.x ? -1 : 1;
            approachingInk = piece; inkApproachAt = village.SceneTime; inkWalkStarted = false;
        }
        void TickVillageInkPour(float dt)
        {
            if (approachingInk == 0) { if(villageInkPour) villageInkPour.Step(dt); return; }
            var player = village.World.arena.Player; var well = village.Find(StartingVillageWorld.ReturnWell);
            if(!inkWalkStarted && village.SceneTime-inkApproachAt >= .18f)
            {
                float distance=Mathf.Abs(well.Position.x-player.Position.x);
                float step=Mathf.Min(1.5f,Mathf.Max(0,distance-1.8f));
                inkWalkStarted=true;
                if(step>.05f) village.StartScriptedWalk(float.NaN,player.Position.x+player.facing*step);
            }
            if(!inkWalkStarted || village.ScriptedWalking || village.SceneTime-inkApproachAt < .35f) return;
            int piece=approachingInk; approachingInk=0;
            villageInkPour.Begin(piece,player.Position,well.Position);
            player.GetComponent<VillageInkPocketGlow>()?.Release();
        }
        void FinishVillageInk(bool remember)
        {
            if(approachingInk!=0)
            {
                int piece=approachingInk; approachingInk=0; village.StopScriptedWalk();
                if(remember) { Profile.MarkStorySeen(VillageInkStory(piece)); village.ColourPieces=piece; }
            }
            if (villageInkPour && villageInkPour.Playing)
            {
                int piece = villageInkPour.Piece; villageInkPour.Cancel();
                if (remember) { Profile.MarkStorySeen(VillageInkStory(piece)); village.ColourPieces = piece; }
            }
            if (!remember) CloseVillageInkModal();
            var player = village && village.World && village.World.arena ? village.World.arena.Player : null;
            if(player) player.GetComponent<VillageInkPocketGlow>()?.Release();
        }
        void BeginVillageInkReward(int piece)
        {
            if (VillageInkModalShowing) return;
            if (Profile.Snapshot.HasSeen(InkRewardStory(piece))) return;
            inkRewardPiece = piece;
            SetInkConfirmPrompt("[{Interact}] 확인");
            inkRewardLock = village.World.arena.Controls.OpenScope("village ink reward and notebook");
            inkRewardLock.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            inkRewardAt = Time.unscaledTime + .4f;
        }
        void SetInkConfirmPrompt(string template)
        {
            promptLabels.RemoveAll(entry => entry.view == inkConfirm);
            BindPrompt(inkConfirm, template);
        }
        bool TickVillageInkModal()
        {
            if (!VillageInkModalShowing) return false;
            Controls?.SuppressGameplay();
            if (!VillageInkRewardShowing && Time.unscaledTime >= inkRewardAt)
            {
                villageInkView.Begin(inkRewardPiece, Vector2.zero);
                inkConfirm.gameObject.SetActive(true); inkConfirm.transform.SetAsLastSibling(); inkRewardAt = Time.unscaledTime;
            }
            if (TakeAdvance() && Time.unscaledTime - inkRewardAt > .65f) ConfirmVillageInkModal();
            return true;
        }
        void ConfirmVillageInkModal()
        {
            if (!VillageInkRewardShowing) return;
            villageInkView.Cancel(); Profile.MarkStorySeen(InkRewardStory(inkRewardPiece));
            CloseVillageInkModal(); openingStationReadyAt = Time.unscaledTime + OpeningStationDelay;
        }
        void CloseVillageInkModal()
        {
            if (villageInkView) villageInkView.Cancel();
            if (inkConfirm) inkConfirm.gameObject.SetActive(false);
            inkRewardLock?.Dispose(); inkRewardLock = null;
        }
    }
}
