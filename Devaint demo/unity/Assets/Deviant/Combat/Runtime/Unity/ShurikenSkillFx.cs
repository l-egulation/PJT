using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the approved shuriken skill effect frames (Resources/ShurikenSkillFx, pixelfx bakes at PPU32, 2026-11-08).
    // Presentation only: hit shapes, damage, explosion radius and the teleport stay with the executions.
    //   star_basic   basic throws (shuriken)                   small spinning star with yellow wind lines
    //   star         pierce, explosive and step throws          the same star at full size
    //   roll         followup (shuriken_followup)               fast spinning giant star with floor sparks
    //   sweep        shuriken_range_special cast start          throwing swing at the feet
    //   blast        shuriken_range explosion                   replaces the placeholder burst picture
    //   step_out/in  teleport behind the target and the return  ink puffs where the player left and arrived
    // Without the deployed frames everything keeps the placeholder look.
    public static class ShurikenSkillFx
    {
        const string CatalogPath = "ShurikenSkillFx/clips";
        internal const int ShotOrder = 26, BlastOrder = 30, SweepOrder = 29, StepOrder = 29;
        static Dictionary<string, BombSkillFx.Clip> clips;

        static Dictionary<string, BombSkillFx.Clip> Load() => clips ??= BombSkillFx.LoadCatalog(CatalogPath, "deviant-shuriken-skill-fx-v1");
        internal static BombSkillFx.Clip Find(string name) => BombSkillFx.Resolved(Load(), name);
        internal static IEnumerable<bool> WarmSteps() => BombSkillFx.WarmCatalog(Load());
        static bool Enabled(CombatArena arena) => arena && arena.loadCharacterArt;

        // Which flying picture a shuriken projectile uses.
        public static string ShotClip(string projectileType)
        {
            switch (projectileType)
            {
                case "shuriken": return "star_basic";
                case "shuriken_special": case "shuriken_range": case "shuriken_move": return "star";
                case "shuriken_followup": return "roll";
            }
            return null;
        }

        // Puts the flying star on a player shuriken. Returns false (placeholder stays) when nothing approved is deployed.
        internal static bool TryAttachShot(PlayerProjectile shot, string projectileType)
        {
            if (!shot || !Enabled(shot.arena) || !shot.owner || !shot.owner.isPlayer) return false;
            if (projectileType == "shuriken_special" && TryAttachFastStar(shot)) return true;
            var clip = Find(ShotClip(projectileType));
            if (clip == null) return false;
            int facing = shot.velocity.x < 0 ? -1 : 1;
            var book = BombSkillFx.Play(shot.arena, clip, shot.Position, facing, ShotOrder, shot.transform);
            book.gameObject.name = "ShurikenSkillFx " + clip.data.name;
            // The big pierce star (radius 1) is drawn to its hit size; the others keep the baked size (SkillFxHitScale).
            book.gameObject.AddComponent<ShurikenShotUpright>().Bind(facing, SkillFxHitScale.ShurikenShot(clip.data.name, shot.Definition.Radius));
            return true;
        }

        // 큰 관통 수리검(2026-11-11 확정): 지름 2u 로 구운 뾰족한 네 날 수리검이 2배 빨리 돈다(0.24초에 한 바퀴).
        // 대시와 같은 방식의 먹청색 잔상이 짧게 남고, 탄 아래 땅이 가까울 때만 칼끝이 긁히는 스파크가 튄다.
        internal const string FastStarClip = "star_fast", FastSparksClip = "star_fast_sparks";
        internal static readonly Color EchoInk = new Color32(0x2b, 0x54, 0x6a, 255);   // ApprovedDashPresentation.json afterimageColor
        internal const float EchoInterval = .04f, EchoLife = .1f, EchoOpacity = .4f;

        static bool TryAttachFastStar(PlayerProjectile shot)
        {
            var body = Find(FastStarClip);
            if (body == null) return false;
            int facing = shot.velocity.x < 0 ? -1 : 1;
            var book = BombSkillFx.Play(shot.arena, body, shot.Position, facing, ShotOrder, shot.transform);
            book.gameObject.name = "ShurikenSkillFx " + body.data.name;
            book.gameObject.AddComponent<ShurikenShotUpright>().Bind(facing);
            book.gameObject.AddComponent<ShurikenEchoTrail>().Bind(shot.arena, book.View);
            var sparks = Find(FastSparksClip);
            if (sparks != null)
            {
                var spark = BombSkillFx.Play(shot.arena, sparks, shot.Position, facing, ShotOrder + 1, shot.transform);
                spark.gameObject.name = "ShurikenSkillFx " + sparks.data.name;
                spark.gameObject.AddComponent<ShurikenShotUpright>().Bind(facing);
                spark.gameObject.AddComponent<ShurikenGroundSparks>().Bind(shot, spark.View);
            }
            return true;
        }

        // 바닥이 탄 아래 (반지름 + 0.3u) 안에 있을 때만 스파크를 보인다(공중 투척은 잔상만).
        public static bool SparksTouchGround(float centerY, float radius, float? floorY) => floorY.HasValue && centerY - floorY.Value <= radius + .3f;

        internal static bool TryBlast(CombatArena arena, Vector2 at, int facing)
            => Named(BombSkillFx.Play(arena, "blast", at, facing, BlastOrder));

        // Cast start of a player shuriken special: the explosive throw swings at the feet.
        internal static void CastStarted(CombatArena arena, CombatActor owner, string skillId, int facing)
        {
            if (!owner || !owner.isPlayer || skillId != "shuriken_range_special") return;
            Named(BombSkillFx.Play(arena, "sweep", owner.Position, facing, SweepOrder));
        }

        // Teleport behind the target and the automatic return: a puff where the body left and one where it arrived.
        internal static void Step(CombatArena arena, Vector2 from, int fromFacing, Vector2 to, int toFacing)
        {
            Named(BombSkillFx.Play(arena, "step_out", from, fromFacing, StepOrder));
            Named(BombSkillFx.Play(arena, "step_in", to, toFacing, StepOrder));
        }

        static bool Named(BombFxFlipbook book)
        {
            if (!book) return false;
            book.gameObject.name = book.gameObject.name.Replace("BombSkillFx", "ShurikenSkillFx");
            return true;
        }
    }

    // A star rides its projectile, whose transform carries the hit size (scale) and the rolling spin (rotation).
    // The baked frames already spin, so the picture cancels that and stays upright at its own display size
    // (the baked PPU32 size times the hit-size ratio from SkillFxHitScale).
    internal sealed class ShurikenShotUpright : MonoBehaviour
    {
        int facing = 1;
        float size = 1;
        internal void Bind(int value, float scale = 1) { facing = value; size = scale > 0 ? scale : 1; Apply(); }
        void Apply()
        {
            var parent = transform.parent;
            transform.rotation = Quaternion.identity;
            if (!parent) return;
            var scale = parent.lossyScale;
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(facing * size / Mathf.Max(.0001f, Mathf.Abs(scale.x)), size / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1);
        }
        void LateUpdate() => Apply();
    }

    // 대시 잔상(ApprovedDashPresentation)과 같은 방식: 일정 간격으로 지금 그림의 복사본을 먹청색으로 남기고 짧게 지운다. 아레나 시계를 따른다.
    internal sealed class ShurikenEchoTrail : MonoBehaviour
    {
        sealed class Echo { public SpriteRenderer view; public float born; }
        readonly System.Collections.Generic.List<Echo> echoes = new System.Collections.Generic.List<Echo>();
        CombatArena arena; SpriteRenderer source; float next;
        internal void Bind(CombatArena value, SpriteRenderer body) { arena = value; source = body; next = value ? value.Now : 0; }
        void LateUpdate()
        {
            if (!arena) return;
            float now = arena.Now;
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var e = echoes[i]; float age = now - e.born;
                if (!e.view || age >= ShurikenSkillFx.EchoLife || age < 0) { if (e.view) Destroy(e.view.gameObject); echoes.RemoveAt(i); continue; }
                var c = ShurikenSkillFx.EchoInk; c.a = ShurikenSkillFx.EchoOpacity * Mathf.Pow(1 - age / ShurikenSkillFx.EchoLife, 1.15f); e.view.color = c;
            }
            if (!source || !source.sprite || now < next) return;
            next = now + ShurikenSkillFx.EchoInterval;
            var node = new GameObject("Shuriken echo") { hideFlags = HideFlags.DontSave };
            node.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            node.transform.localScale = source.transform.lossyScale;
            var view = node.AddComponent<SpriteRenderer>();
            view.sprite = source.sprite; view.sharedMaterial = source.sharedMaterial; view.flipX = source.flipX;
            view.sortingLayerID = source.sortingLayerID; view.sortingOrder = source.sortingOrder - 1;
            var tint = ShurikenSkillFx.EchoInk; tint.a = ShurikenSkillFx.EchoOpacity; view.color = tint;
            echoes.Add(new Echo { view = view, born = now });
        }
        void OnDestroy() { foreach (var e in echoes) if (e.view) Destroy(e.view.gameObject); echoes.Clear(); }
    }

    // 바닥 스파크는 땅이 가까울 때만 보인다.
    internal sealed class ShurikenGroundSparks : MonoBehaviour
    {
        PlayerProjectile shot; SpriteRenderer view;
        internal void Bind(PlayerProjectile value, SpriteRenderer renderer) { shot = value; view = renderer; Refresh(); }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (!view) return;
            if (!shot || !shot.arena) { view.enabled = false; return; }
            float r = shot.Definition.Radius;
            view.enabled = ShurikenSkillFx.SparksTouchGround(shot.Position.y, r, shot.arena.FindFloor(shot.Position, r + .3f));
        }
    }
}
