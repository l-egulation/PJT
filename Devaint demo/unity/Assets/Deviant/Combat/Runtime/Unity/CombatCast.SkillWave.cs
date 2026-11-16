using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatCast
    {
        SkillWaveExecution skillWave;
        SpriteRenderer projectileVisual;
        bool reviewedWave;
        float waveVisualStarted;
        CombatHitOrigin waveHitOrigin;
        Vector2 ProjectileSize => Definition.Hits.Count > 0 && Definition.Hits[0].Box != null
            ? new Vector2(Definition.Hits[0].Box.Width, Definition.Hits[0].Box.Height) : new Vector2(1, 2);
        // 생성점부터 끝까지 막힘 없이 날아가는 시간(이동 거리 ÷ 속도). 검기 그림의 사라지는 장을 이 끝에 맞춘다.
        float WaveFlight => Definition.Effect != null && Definition.Effect.Speed > 0 ? Definition.Effect.Distance / Definition.Effect.Speed : 0;
        void ActivateSkillWave()
        {
            waveHitOrigin = HitOrigin(0, CombatHitKind.Projectile).WithEffect(System.Guid.NewGuid(), CombatHitKind.Projectile);
            Vector2 at = owner.Position; var host = new SkillWaveHost(this);
            skillWave.Activate(at.x, at.y, Definition.Effect, ref host);
        }
        void StepSkillWave(float step)
        {
            var host = new SkillWaveHost(this); skillWave.Step(step, Definition.Effect, ref host);
        }
        struct SkillWaveHost : ISkillWaveExecutionHost
        {
            readonly CombatCast cast;
            public SkillWaveHost(CombatCast cast) { this.cast = cast; }
            public int Direction => cast.direction;
            public bool Blocked(float x, float y) => cast.arena.SkillProjectileBlocked(new Vector2(x, y), cast.ProjectileSize);
            public bool Occluded(float x, float y) => cast.arena.Occluded(cast.owner.Center, new Vector2(x, y));
            public void CreateVisual(float x, float y)
            {
                Vector2 size = cast.ProjectileSize;
                cast.projectileVisual = new GameObject("SwordWave").AddComponent<SpriteRenderer>();
                cast.projectileVisual.sprite = cast.arena.CircleSprite; cast.projectileVisual.color = new Color(.15f, .55f, 1, .85f); cast.projectileVisual.sortingOrder = 22;
                if (cast.arena.spriteMaterial) cast.projectileVisual.sharedMaterial = cast.arena.spriteMaterial;
                cast.waveVisualStarted = cast.Age;
                Sprite reviewed = cast.Definition.Id == "base_sword_range" ? BaseSwordSkillVisual.WaveAt(0, cast.WaveFlight) : null;
                cast.reviewedWave = reviewed != null;
                if (cast.reviewedWave)
                {
                    cast.projectileVisual.sprite = reviewed;
                    cast.projectileVisual.color = Color.white;
                    cast.projectileVisual.flipX = cast.direction < 0;
                    float scale = SkillFxHitScale.SwordWave(size.y);
                    cast.projectileVisual.transform.localScale = new Vector3(scale, scale, 1);
                }
                else cast.projectileVisual.transform.localScale = new Vector3(size.x, size.y, 1);
                cast.projectileVisual.transform.position = new Vector2(x, y);
            }
            public float Travel(float x, float y, int direction, float requested)
                => cast.arena.SkillProjectileTravel(new Vector2(x, y), cast.ProjectileSize, Vector2.right * direction, requested);
            public void ApplyBox(float previousX, float previousY, float x, float y)
                => cast.ApplyBox(0, cast.Definition.Hits[0], new Vector2(previousX, previousY), new Vector2(x, y), true, cast.waveHitOrigin);
            public void UpdateVisual(float x, float y)
            {
                if (!cast.projectileVisual) return;
                cast.projectileVisual.transform.position = new Vector2(x, y);
                if (cast.reviewedWave)
                    cast.projectileVisual.sprite = BaseSwordSkillVisual.WaveAt(cast.Age - cast.waveVisualStarted, cast.WaveFlight);
            }
            public void DestroyVisual() { if (cast.projectileVisual) { Destroy(cast.projectileVisual.gameObject); cast.projectileVisual = null; } }
        }
    }
}
