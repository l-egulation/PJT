using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Plays the approved dagger skill effect frames (Resources/DaggerSkillFx, pixelfx bakes at PPU32, approved 2026-11-07).
    // Presentation only: stealth, smoke area, dash and hits stay with the executions.
    //   hold_breath   dagger_default    shadow cover at the feet (b), with the body inked for a moment (Presenter)
    //   ink_smoke     dagger_range      ink band curtain at the smoke spot (b, no border), behind the body, as long as the smoke
    //   triple_leap   dagger_movement   one-flash streak from where the leap starts (b)
    //   flurry        dagger_follow_up  crescent storm over the target in front (e)
    // Every clip starts at the skill's activation, back-dated to the cast start, so a cast cancelled before activation shows nothing.
    // A cast that is cancelled early (dash, replaced smoke) takes its effect with it.
    public static class DaggerSkillFx
    {
        const string CatalogPath = "DaggerSkillFx/clips";
        // Where the flurry storm sits in front of the dagger user (the preview's target spot: 1.75 units ahead, body centre).
        internal static readonly Vector2 FlurryOffset = new Vector2(1.75f, 30 / 32f);
        // Body ink of hold breath: full ink from 0.08s, fading out from 0.12s to 0.26s of the cast.
        internal const float InkFrom = .08f, InkHold = .12f, InkTo = .26f;
        static Dictionary<string, BombSkillFx.Clip> clips;

        static Dictionary<string, BombSkillFx.Clip> Load() => clips ??= BombSkillFx.LoadCatalog(CatalogPath, "deviant-dagger-skill-fx-v1");
        internal static BombSkillFx.Clip Find(string name) => BombSkillFx.Resolved(Load(), name);
        internal static IEnumerable<bool> WarmSteps() => BombSkillFx.WarmCatalog(Load());

        public static string ClipFor(string skillId)
        {
            switch (skillId)
            {
                case "dagger_default": return "hold_breath";
                case "dagger_range": return "ink_smoke";
                case "dagger_movement": return "triple_leap";
                case "dagger_follow_up": return "flurry";
            }
            return null;
        }

        internal const string FogClip = "ink_smoke_fog", FogInkClip = "ink_smoke_fog_ink";
        // 밝기 문턱: 1스테이지 종이 배경(약 0.88)과 수로(약 0.2) 사이.
        public const float BrightBackdrop = .55f;
        static RenderTexture probe;
        static Texture2D probeRead;

        // 지금 화면 배경이 밝은가: 게임 카메라를 16×9 로 한 번 그려 평균 밝기를 본다(시전마다 한 번). 그래픽이 없으면 밝다고 본다.
        public static bool BackdropIsBright(CombatArena arena)
        {
            var view = arena ? arena.ViewCamera : null;
            if (!view || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
            probe ??= new RenderTexture(16, 9, 16) { hideFlags = HideFlags.DontSave };
            probeRead ??= new Texture2D(16, 9, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var target = view.targetTexture; var active = RenderTexture.active;
            try
            {
                view.targetTexture = probe; view.Render();
                RenderTexture.active = probe; probeRead.ReadPixels(new Rect(0, 0, 16, 9), 0, 0); probeRead.Apply(false);
            }
            finally { view.targetTexture = target; RenderTexture.active = active; }
            return Luminance(probeRead.GetPixels32()) >= BrightBackdrop;
        }

        public static float Luminance(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0) return 1;
            double sum = 0;
            foreach (var c in pixels) sum += (.2126 * c.r + .7152 * c.g + .0722 * c.b) / 255.0;
            return (float)(sum / pixels.Length);
        }

        // Ink weight of the body during hold breath at cast age t (1 = solid ink).
        public static float BodyInk(float age)
            => age < InkFrom || age >= InkTo ? 0 : age < InkHold ? 1 : 1 - (age - InkHold) / (InkTo - InkHold);

        // Called once when a player dagger special activates. at = feet where the cast started, activation = seconds since cast start.
        // end = cast end (seconds from start), smokeSeconds = how long the smoke area lasts,
        // smokeWidth = the smoke area's width; the curtain is drawn to it (SkillFxHitScale).
        internal static void Activated(CombatArena arena, CombatCast cast, CombatActor owner, string skillId, Vector2 at, int facing, float activation, float end, float smokeSeconds, float smokeWidth = 0)
        {
            if (!arena || !arena.loadCharacterArt || !owner || !owner.isPlayer) return;
            bool smoke = skillId == "dagger_range";
            // 먹안개(2026-11-11 확정): 뭉게 연막. 시전 순간 화면 배경이 밝으면 옅은 먹색, 어두우면 밝은 회색을 쓴다.
            // 새 프레임이 배치되지 않았으면 예전 띠 장막(ink_smoke)으로 돌아간다.
            var fog = smoke ? Find(BackdropIsBright(arena) ? FogInkClip : FogClip) : null;
            var clip = fog ?? Find(ClipFor(skillId));
            if (clip == null) return;
            int body = owner.visual ? owner.visual.sortingOrder : 20;
            Vector2 spot = skillId == "dagger_follow_up" ? owner.Position + new Vector2(FlurryOffset.x * facing, FlurryOffset.y) : at;
            float scale = fog != null ? SkillFxHitScale.DaggerFog(smokeWidth) : smoke ? SkillFxHitScale.DaggerSmoke(smokeWidth) : 1;
            var book = BombSkillFx.Play(arena, clip, spot, facing, smoke ? body - 1 : body + 2, null, scale);
            book.gameObject.name = "DaggerSkillFx " + clip.data.name;
            // The smoke picture starts with the smoke; the others were drawn from the cast start.
            if (!smoke) book.Backdate(activation);
            // Removed only if the cast goes before its normal end (the smoke: before the smoke runs out); otherwise the clip plays out.
            float until = arena.Now + Mathf.Max(0, smoke ? smokeSeconds : end - activation) - .05f;
            book.gameObject.AddComponent<DaggerFxCancel>().Bind(arena, cast, until);
        }
    }

    // Removes a dagger effect when its cast disappears before it should (dash cancel, a newer smoke, reset).
    internal sealed class DaggerFxCancel : MonoBehaviour
    {
        CombatArena arena;
        CombatCast cast;
        float until;
        internal void Bind(CombatArena value, CombatCast source, float before) { arena = value; cast = source; until = before; }
        void LateUpdate()
        {
            if (!arena) { Destroy(gameObject); return; }
            if (!cast && arena.Now < until) Destroy(gameObject);
        }
    }
}
