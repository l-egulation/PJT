using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // 채찍 스킬 속박의 출처 기록(표시 전용, 2026-11-07). 속박 자체(rootUntil)·피해·판정은 기존 경로가 그대로 소유한다.
    // 채찍 스킬이 건 속박이 그 적의 가장 긴 속박이면 파츠용 보라 밧줄 대신 채찍 감김을 그리고,
    // 플레이어는 같은 시전이 건 속박이 끝날 때까지 땡기는 장을 붙잡는다(CharacterPresentationBinding.TryWhipHold).
    internal readonly struct WhipRootRecord
    {
        public readonly float From, Until;
        public readonly Guid Execution;
        public readonly string ActionId;
        public readonly int Life;
        public WhipRootRecord(float from, float until, Guid execution, string actionId, int life)
        { From = from; Until = until; Execution = execution; ActionId = actionId; Life = life; }
    }

    public partial class CombatArena
    {
        readonly Dictionary<CombatActor, WhipRootRecord> whipRoots = new Dictionary<CombatActor, WhipRootRecord>();
        readonly List<CombatActor> staleWhipRoots = new List<CombatActor>();
        WhipSkillVisuals whipSkillVisuals;

        // CastHitResponseHost 가 채찍 스킬의 속박 타격이 받아들여진 직후 부른다.
        internal void RecordWhipRoot(CombatActor target, CombatCast cast)
        {
            if (!target || target.isPlayer || !cast || cast.isBasic || cast.Definition?.Id == null
                || !cast.Definition.Id.StartsWith("whip_", StringComparison.Ordinal) || target.rootUntil <= Now) return;
            whipRoots[target] = new WhipRootRecord(Now, target.rootUntil, cast.ExecutionId, cast.Definition.Id, target.LifeVersion);
            if (cast.owner == Player && cast.Definition.Id == "whip_default") WhipSkillVisuals.Snap(this, target);
        }

        // 지금 이 적을 묶고 있는 가장 긴 속박이 채찍 스킬의 것인가.
        internal bool TryReadWhipRoot(CombatActor actor, out WhipRootRecord root)
        {
            root = default;
            if (!actor || actor.Dead || !whipRoots.TryGetValue(actor, out root)) return false;
            return root.Life == actor.LifeVersion && actor.rootUntil > Now && root.Until > Now && root.Until >= actor.rootUntil - .001f;
        }

        // 한 시전이 건 채찍 속박 가운데 가장 늦게 끝나는 시각(없으면 음의 무한대). 살아 있는 적만 센다.
        internal float WhipRootUntil(Guid execution)
        {
            float until = float.NegativeInfinity;
            foreach (var pair in whipRoots)
                if (pair.Value.Execution == execution && pair.Key && !pair.Key.Dead && pair.Value.Life == pair.Key.LifeVersion)
                    until = Mathf.Max(until, Mathf.Min(pair.Value.Until, pair.Key.rootUntil));
            return until;
        }

        internal IEnumerable<KeyValuePair<CombatActor, WhipRootRecord>> WhipRoots => whipRoots;

        // 한 시전이 붙잡은 적 가운데 시전자에게 가장 가까운 산 적(줄 끝). 속박이 끝났어도 기록이 남아 있으면 쓴다(매듭 접근 재입력 창).
        internal CombatActor WhipTarget(Guid execution)
        {
            CombatActor best = null; float bestDistance = float.MaxValue;
            foreach (var pair in whipRoots)
            {
                var actor = pair.Key;
                if (pair.Value.Execution != execution || !actor || actor.Dead || pair.Value.Life != actor.LifeVersion) continue;
                float distance = Player ? Mathf.Abs(actor.Center.x - Player.Position.x) : 0;
                if (distance < bestDistance) { bestDistance = distance; best = actor; }
            }
            return best;
        }
        internal bool TryReadWhipRecord(CombatActor actor, out WhipRootRecord record)
        {
            record = default;
            return actor && !actor.Dead && whipRoots.TryGetValue(actor, out record) && record.Life == actor.LifeVersion;
        }

        void RefreshWhipSkillVisuals()
        {
            staleWhipRoots.Clear();
            // 매듭 접근은 속박이 끝나도 재입력 창(3초) 동안 표식·줄이 그 적을 쓴다 → 넉넉히 4초 뒤에 정리
            foreach (var pair in whipRoots) if (!pair.Key || pair.Value.Until < Now - 4) staleWhipRoots.Add(pair.Key);
            foreach (var actor in staleWhipRoots) whipRoots.Remove(actor);
            if (whipSkillVisuals == null)
            {
                whipSkillVisuals = new WhipSkillVisuals(this);
                Initialized += ClearWhipSkillState; ResetCompleted += ClearWhipSkillState;
            }
            whipSkillVisuals.Refresh();
        }

        void ClearWhipSkillState()
        {
            whipRoots.Clear();
            whipSkillVisuals?.Clear();
        }
    }
}
