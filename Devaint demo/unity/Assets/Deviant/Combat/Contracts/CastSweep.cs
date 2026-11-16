namespace Deviant.Combat.Contracts
{
    public readonly struct CastSweepBox
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public CastSweepBox(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    // Query indices and collected indices belong only to this entered call.
    // Native refs, ordered overlap results and the existing per-call list stay
    // with the host; nested Receive callbacks get a separate host/list.
    public interface ICastSweepHost
    {
        void Begin();
        void DebugBox(float x, float y, float width, float height);
        int QueryBox(float x, float y, float width, float height);
        bool IsOwner(int queryIndex);
        bool IsPlayer(int queryIndex);
        bool IsDecoy(int queryIndex);
        bool IsDead(int queryIndex);
        bool ContainsCandidate(int queryIndex);
        bool Exposed(int queryIndex, float originX, float originY, float x, float y, float width, float height);
        void AddCandidate(int queryIndex);
        // Keep List.Sort and its comparison order. Each comparison reads the
        // live owner/target centers and uses CastSweepExecution.Compare.
        void SortCandidates();
        int CandidateCount { get; }
        bool MarkedExists { get; }
        bool IsMarkedCandidate(int candidateIndex);
        void ApplyTarget(int candidateIndex);
    }
}
