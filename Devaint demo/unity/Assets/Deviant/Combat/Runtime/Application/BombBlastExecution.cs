using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class BombBlastExecution
    {
        // The caller has already claimed the existing TimedBomb stop. An
        // exception keeps the entered blast's partial effects; it must not
        // force removal or retry a candidate/fragment.
        public static void ExplodeStopped<T>(ref T host) where T : struct, IBombBlastHost
        {
            host.BeginBlast();
            int count = host.QueryCandidates();
            for (int i = 0; i < count; i++)
            {
                host.SelectCandidate(i);
                if (!host.RegionExists || !host.TargetExists || host.TargetIsOwner || host.TargetIsPlayer ||
                    host.TargetIsDecoy || host.TargetDead || !host.TryClaimTarget()) continue;
                host.FindClosestPoint();
                if (host.ContactDistance > host.BlastRadius + .0001f || host.Occluded()) continue;
                // Direct contact has no extra event; each claimed target gets
                // one basic Receive regardless of its acceptance result.
                host.ReceiveBasic();
            }
            host.BombExploded();
            for (int i = 0; i < host.FragmentCount; i++)
            {
                float angle = host.FragmentAngleAt(i);
                host.SpawnFragment(angle);
                host.SweepFragmentAtCurrentPosition();
            }
            host.Remove();
        }
    }
}
