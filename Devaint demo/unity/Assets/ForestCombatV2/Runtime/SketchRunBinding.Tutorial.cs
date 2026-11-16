using Deviant.Run;
using Deviant.Tutorial.Unity;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class SketchRunBinding
    {
        public const string AttackTutorialRecord = "first-run-attack-v1";
        public const string RewardTutorialRecord = "first-run-reward-v1";
        FirstRunTutorialWindow firstRunTutorial;
        bool attackTutorialShown, rewardTutorialShown;
        public bool FirstRunTutorialShowing => firstRunTutorial && firstRunTutorial.IsOpen;

        public static bool TutorialEligible(RunStartPlan plan, ProfileSnapshot profile, string record) =>
            plan != null && !plan.Training && profile != null &&
            !string.IsNullOrEmpty(plan.ProfileId) && plan.ProfileId == profile.ProfileId &&
            profile.RunsStarted == 1 && !profile.FirstRunSettled && !profile.HasSeen(record);

        FirstRunTutorialWindow TutorialWindow()
        {
            if (!firstRunTutorial)
            {
                var root = new GameObject("First-run automatic tutorial", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                firstRunTutorial = root.AddComponent<FirstRunTutorialWindow>();
            }
            return firstRunTutorial;
        }

        void InitializeFirstRunTutorials(int stage)
        {
            if (stage != 1 || attackTutorialShown || run.Plan == null || run.Plan.Training || !Arena.Player || Arena.Player.Dead ||
                !TutorialEligible(run.Plan, Chapter1ProfileHost.Service.Snapshot, AttackTutorialRecord)) return;
            TutorialWindow().ShowAttack(Deviant.Combat.PlayerControls.For(Arena.Player), Arena.Controls);
            attackTutorialShown = true;
            RecordTutorial(AttackTutorialRecord);
        }

        bool TryShowFirstRewardTutorial()
        {
            if (FirstRunTutorialShowing) return true;
            if (rewardTutorialShown || run.Plan == null || run.Plan.Training || State?.PendingOffer == null || justPicked != null || justRegistered != null ||
                !Arena || !Arena.Player || Arena.Player.Dead ||
                !TutorialEligible(run.Plan, Chapter1ProfileHost.Service.Snapshot, RewardTutorialRecord)) return false;
            // Keep the reward's own pause lease while the tutorial opens and closes above it.
            UpdatePause();
            TutorialWindow().ShowRewards(Deviant.Combat.PlayerControls.For(Arena.Player), Arena.Controls);
            rewardTutorialShown = true;
            navigationDirection = 0;
            RecordTutorial(RewardTutorialRecord);
            return true;
        }

        static void RecordTutorial(string record)
        {
            var profile = Chapter1ProfileHost.Service;
            if (!profile.MarkStorySeen(record) && !profile.Snapshot.HasSeen(record))
                Debug.LogWarning("Tutorial record could not be saved: " + profile.LastSaveError);
        }

        void OnDisable() => CloseFirstRunTutorial();

        void CloseFirstRunTutorial()
        {
            if (firstRunTutorial) firstRunTutorial.Dismiss();
        }
    }
}
