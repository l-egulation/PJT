namespace Deviant.Combat.Contracts
{
    // Every entered sweep owns its native query/candidate lists. Actor refs,
    // List.Sort comparisons and Collider/Physics queries stay with this host;
    // eligibility and contact facts are read only when that candidate is visited.
    public interface IEnemyProjectileSweepHost
    {
        void ReadPosition(out float x, out float y);
        float VelocityMagnitude { get; }
        void ReadDirection(out float x, out float y);
        float SolidTravel(float x, float y, float width, float height, float directionX, float directionY, float distance);
        void BeginCandidates();
        int QueryBox(float x, float y, float width, float height);
        bool ContainsCandidate(int queryIndex);
        void AddCandidate(int queryIndex);
        void SortCandidates(float originX, float originY);
        int CandidateCount { get; }
        bool AcceptsTarget(int candidateIndex);
        bool Occluded(int candidateIndex, float originX, float originY);
        bool TryContact(int candidateIndex, float originX, float originY);
        void SetPosition(float x, float y);
        void Release();
    }
}
