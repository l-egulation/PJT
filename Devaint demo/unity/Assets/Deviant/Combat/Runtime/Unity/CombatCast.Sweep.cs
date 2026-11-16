using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        struct SweepHost : ICastSweepHost
        {
            readonly CombatCast cast;
            readonly int index;
            readonly SkillHitDefinition hit;
            readonly CombatHitOrigin hitOrigin;
            readonly float originOffsetX;
            List<CombatActor> query, candidates;
            Dictionary<int, CombatHitContact> contacts;
            CombatHitContact selectedContact;
            public SweepHost(CombatCast cast, int index, SkillHitDefinition hit, CombatHitOrigin hitOrigin, float originOffsetX)
            { this.cast = cast; this.index = index; this.hit = hit; this.hitOrigin = hitOrigin; this.originOffsetX = originOffsetX; query = candidates = null; contacts = null; selectedContact = default; }
            public void Begin() { candidates = new List<CombatActor>(); contacts = new Dictionary<int, CombatHitContact>(); }
            public void DebugBox(float x, float y, float width, float height)
                => cast.debugBoxes.Add(new Bounds(new Vector2(x, y), new Vector3(width, height, 1)));
            public int QueryBox(float x, float y, float width, float height)
            {
                if (cast.meleeAction != null)
                    cast.arena.RecordEnvironmentAttack(new EnvironmentAttackSample(hitOrigin, x, y, width, height, x - originOffsetX, y, cast.direction));
                query = cast.arena.TargetsInBox(new Vector2(x, y), new Vector2(width, height));
                if (cast.owner && cast.owner.isPlayer) cast.arena.AugmentReflectShots(cast, new Vector2(x, y), new Vector2(width, height));
                return query.Count;
            }
            public bool IsOwner(int i) => query[i] == cast.owner;
            public bool IsPlayer(int i) => query[i].isPlayer;
            public bool IsDecoy(int i) => query[i].isDecoy;
            public bool IsDead(int i) => query[i].Dead;
            public bool ContainsCandidate(int i) => candidates.Contains(query[i]);
            public bool Exposed(int i, float originX, float originY, float x, float y, float width, float height)
                => hit.Targets == ScreenTargets ? ScreenContact(query[i], out selectedContact) : cast.arena.Exposed(new Vector2(originX, originY), query[i], new Vector2(x, y), new Vector2(width, height), out selectedContact, cast.direction);
            public void AddCandidate(int i) { candidates.Add(query[i]); contacts[query[i].ActorId] = selectedContact; }
            public void SortCandidates()
            {
                candidates.Sort(cast.CompareSweepCandidates);
                var marked = cast.marked; var activeCast = cast; int hitIndex = index;
                var eligible = candidates.Where(t => activeCast.chargedMeleeExecution != null ? !activeCast.chargedMeleeExecution.HasClaimedTarget(t.ActorId) :
                    activeCast.meleeAction != null ? !activeCast.meleeAction.HasClaimedTarget(t.ActorId) : !activeCast.ledger.Contains(hitIndex, t.ActorId));
                if (cast.owner == cast.arena.Player)
                    cast.arena.PrepareSketchHit(hitOrigin, hit.Targets == "nearest" ? eligible.Where(t => !marked || t == marked).Take(1) : eligible);
            }
            public int CandidateCount => candidates.Count;
            public bool MarkedExists => cast.marked;
            public bool IsMarkedCandidate(int i) => candidates[i] == cast.marked;
            public void ApplyTarget(int i) => cast.ApplyTarget(index, hit, candidates[i], hitOrigin, contacts[candidates[i].ActorId]);
        }

        // 화면 대상 판정은 벽·바닥 차폐 없이 피격 몸 중심을 접촉점으로 쓴다.
        static bool ScreenContact(CombatActor target, out CombatHitContact contact)
        {
            Vector2 center = target.Center;
            contact = new CombatHitContact(center.x, center.y, 0, 0, HitAnchorKind.OverlapRepresentative);
            return true;
        }

        int CompareSweepCandidates(CombatActor first, CombatActor second)
        {
            Vector2 origin = owner.Center, center = first.Center;
            float firstDistance = CastSweepExecution.Distance(origin.x, origin.y, center.x, center.y);
            origin = owner.Center; center = second.Center;
            float secondDistance = CastSweepExecution.Distance(origin.x, origin.y, center.x, center.y);
            return CastSweepExecution.Compare(firstDistance, first.ActorId, secondDistance, second.ActorId);
        }
    }
}
