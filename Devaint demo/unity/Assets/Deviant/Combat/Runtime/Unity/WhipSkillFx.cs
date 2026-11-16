using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat
{
    // 채찍 스킬 전용 표시(표시 전용, 2026-11-07 사용자 확정). 판정·피해·속박 시간은 바꾸지 않는다.
    //   줄: 휘감기·매듭 접근·매듭 조이기는 몸 그림에서 채찍을 지운 장(lines 목록)일 때 손잡이에서 줄을 코드로 그린다.
    //       타격 전에는 손 앞 0.6유닛에서 출발해 타격 순간 적 몸 가운데(빗나가면 그림 속 채찍 끝)에 닿도록 뻗고 처짐이 풀린다.
    //       매듭 접근 재입력 창 동안 줄이 0.25초마다 굵어졌다 돌아오고(맥박), 창이 끝나 놓으면 가운데서 끊겨 처지며 사라진다.
    //   감김: 휘감기 loosen(위 바퀴 45%·아래 75% 에 끊김), 가시 그물 thorn(가시), 나머지 bind(감기→유지→풀림).
    //   snap: 휘감기 고리가 감기는 순간 노란 섬광·먹 방울(시안 B). 같은 순간 적이 플레이어 쪽으로 3화소 당겨진다.
    //   knot_mark: 매듭 접근이 맞힌 적 머리 위 매듭 + 3초 줄어드는 노란 고리(다시 D 를 쓰거나 창이 끝나면 사라짐).
    //   knot_tighten: 매듭 조이기 맞은 적 몸의 매듭이 0.6초 조여들다 터짐(0.85초 조임 피해와 같은 순간), 적이 크게 흔들린다.
    //   net_mark: 가시 그물 타격 순간 판정 폭만큼 바닥 가시 자국.
    //   thorn_net: 가시 그물 부채(2026-11-13 사용자 확정). 채찍 위 낮은 뿌리에서 펴져 서고, 넘어오며 커져 적 너머까지 덮은 뒤
    //              땅에 내려앉는다. 시전 0.315초(몸 모션에서 채찍이 앞으로 내려온 장)부터 그 자리에서 재생한다. 몸 모션의 그물 장(15~19)은
    //              그물을 뺀 판이라 이 그림이 그물을 대신한다. PPU 88.22(몸 모션과 같은 화소 밀도), 피벗 = 발.
    // 원본: resources characters/ink/weapons/whip/runtime/skill-fx-20261107 (PPU32, 원점은 각 .meta 의 피벗).
    // 시계는 arena.Now 라 피격 멈춤·일시정지에 함께 멈춘다.
    public static class WhipSkillFx
    {
        const string CatalogPath = "WhipSkillFx/clips";
        // 가시 그물 부채(thorn_net)가 나오는 시전 시각 = 몸 모션 thorn-net 15번 장(그물을 뺀 판)이 시작하는 시각(0.03×7 + 0.035×3초).
        public const float NetFanStart = .315f;
        [Serializable] sealed class ClipData { public string name; public float seconds; public string[] frames; public int @in, hold, @out; public bool timed; }
        [Serializable] sealed class LineData { public string sprite; public float[] grip, end; public float width; }
        [Serializable] sealed class Catalog { public string format; public ClipData[] clips; public LineData[] lines; }
        public sealed class Clip
        {
            public string Name; public float Step; public Sprite[] Sprites; public int In, Hold, Out; public bool Timed;
            public float Duration => Step * Sprites.Length;
            public Sprite At(float age) { int i = Mathf.FloorToInt(age / Step); return age < 0 || i >= Sprites.Length ? null : Sprites[i]; }
        }
        public readonly struct Line { public readonly Vector2 Grip, End; public readonly float Width; public Line(Vector2 g, Vector2 e, float w) { Grip = g; End = e; Width = w; } }
        static Dictionary<string, Clip> clips;
        // Clips whose frames are read on first use (or by the equipped-whip warm-up), not with the catalog.
        static Dictionary<string, ClipData> pending;
        static Dictionary<string, Line> lines;

        static void Load()
        {
            if (clips != null) return;
            clips = new Dictionary<string, Clip>(); pending = new Dictionary<string, ClipData>(); lines = new Dictionary<string, Line>();
            var text = Resources.Load<TextAsset>(CatalogPath);
            if (!text) return;
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog?.clips == null || catalog.format != "deviant-whip-skill-fx-v1") return;
            foreach (var data in catalog.clips)
            {
                if (data?.frames == null || data.frames.Length == 0 || data.seconds <= 0) continue;
                pending[data.name] = data;
            }
            if (catalog.lines != null)
                foreach (var line in catalog.lines)
                    if (line?.sprite != null && line.grip?.Length == 2 && line.end?.Length == 2)
                        lines[line.sprite] = new Line(new Vector2(line.grip[0], line.grip[1]), new Vector2(line.end[0], line.end[1]), line.width > 0 ? line.width : .0625f);
        }
        static Clip Read(ClipData data)
        {
            pending.Remove(data.name);
            var sprites = new Sprite[data.frames.Length];
            bool complete = true;
            for (int i = 0; i < sprites.Length; i++) complete &= (sprites[i] = Resources.Load<Sprite>(data.frames[i])) != null;
            if (!complete) return null;
            return clips[data.name] = new Clip { Name = data.name, Step = data.seconds, Sprites = sprites, In = data.@in, Hold = data.hold, Out = data.@out, Timed = data.timed };
        }
        public static Clip Find(string name)
        {
            Load();
            if (name == null) return null;
            if (clips.TryGetValue(name, out var clip)) return clip;
            return pending.TryGetValue(name, out var data) ? Read(data) : null;
        }
        internal static IEnumerable<bool> WarmSteps()
        {
            Load();
            foreach (var data in new List<ClipData>(pending.Values)) { if (pending.ContainsKey(data.name)) Read(data); yield return true; }
        }
        public static bool TryLine(Sprite sprite, out Line line) { Load(); line = default; return sprite && lines.TryGetValue(sprite.name, out line); }
        internal static bool Enabled(CombatArena arena) { Load(); return arena && arena.loadCharacterArt && clips.Count + pending.Count > 0; }

        // 감김 장 고르기: 감기·유지(반복)·풀림 판은 묶인 지 age 초, 풀리기까지 remain 초. 시간 그대로 판(timed)은 age 로.
        public static Sprite BindFrame(Clip clip, float age, float remain)
        {
            if (clip == null) return null;
            if (clip.Timed) return clip.At(age);
            if (clip.In + clip.Hold + clip.Out != clip.Sprites.Length || clip.Hold <= 0) return null;
            int index;
            if (remain < clip.Out * clip.Step) index = clip.In + clip.Hold + Mathf.Clamp(clip.Out - 1 - Mathf.FloorToInt(remain / clip.Step), 0, clip.Out - 1);
            else if (age < clip.In * clip.Step) index = Mathf.Clamp(Mathf.FloorToInt(age / clip.Step), 0, clip.In - 1);
            else index = clip.In + Mathf.FloorToInt((age - clip.In * clip.Step) / clip.Step) % clip.Hold;
            return clip.Sprites[index];
        }

        // 채찍 스킬 전용 몸 그림이 깔렸는가(없으면 이전 카탈로그: 기존 코드 고리·궤적을 유지한다).
        internal static bool HasSkillArt(CombatArena arena)
        {
            if (!arena || arena.characterAnimations?.sets == null) return false;
            foreach (var set in arena.characterAnimations.sets)
                if (set.form == "whip" && set.Find("attack_wrap") != null) return true;
            return false;
        }
        public static string BindSet(string actionId) => actionId == "whip_default" ? "loosen" : actionId == "whip_range" ? "thorn" : "bind";
    }

    internal sealed class WhipSkillVisuals
    {
        const float LargeBodyWidth = 2, Px = 1 / 32f;
        static readonly Color Ink = new Color32(0x1b, 0x16, 0x16, 255);
        readonly CombatArena arena;
        readonly Dictionary<CombatActor, View> views = new Dictionary<CombatActor, View>();
        readonly List<CombatActor> gone = new List<CombatActor>();
        readonly HashSet<Guid> netMarked = new HashSet<Guid>(), netThrown = new HashSet<Guid>();
        const int NetFanOrder = 30;   // 가시 그물 부채는 캐릭터·몬스터보다 앞에 그린다
        static readonly List<WhipFxFlipbook> live = new List<WhipFxFlipbook>();
        GameObject lineRoot; LineRenderer line, snapA, snapB; Material lineMaterial;
        Guid lineExecution; float lineStarted; bool heldRecast; Vector3 lastGrip, lastTarget; float snapStarted = -10;
        WhipNudges nudges;

        sealed class View { public GameObject Root; public SpriteRenderer Front, Back, Cue, Mark; }

        public WhipSkillVisuals(CombatArena arena) { this.arena = arena; }

        internal static void Snap(CombatArena arena, CombatActor target)
        {
            if (!WhipSkillFx.Enabled(arena) || !target) return;
            var clip = WhipSkillFx.Find("snap");
            if (clip == null) return;
            bool playerOnRight = arena.Player && arena.Player.Position.x > target.Center.x;
            Spawn(arena, clip, new Vector3(target.Center.x, target.Center.y, target.transform.position.z - .03f), playerOnRight ? -1 : 1, target.visual, 6, "WhipSkillFx snap");
        }

        static void Spawn(CombatArena arena, WhipSkillFx.Clip clip, Vector3 at, int facing, SpriteRenderer over, int order, string name, float scale = 1, float lateBy = 0)
        {
            var node = new GameObject(name) { hideFlags = HideFlags.DontSave };
            node.transform.position = at; node.transform.localScale = new Vector3(facing < 0 ? -scale : scale, scale, 1);
            var renderer = node.AddComponent<SpriteRenderer>();
            if (over) { renderer.sortingLayerID = over.sortingLayerID; renderer.sortingOrder = over.sortingOrder + order; }
            if (arena.spriteMaterial) renderer.sharedMaterial = arena.spriteMaterial;
            var book = node.AddComponent<WhipFxFlipbook>();
            book.Begin(arena, clip, renderer, arena.Now - lateBy);
            live.RemoveAll(value => !value); live.Add(book);
        }

        public void Refresh()
        {
            bool enabled = WhipSkillFx.Enabled(arena);
            foreach (var pair in views) pair.Value.Root.SetActive(false);
            if (enabled)
            {
                if (!nudges) { var go = new GameObject("[Whip nudges]") { hideFlags = HideFlags.DontSave }; go.transform.SetParent(arena.transform, false); nudges = go.AddComponent<WhipNudges>(); }
                var recastCast = arena.FeelCharacterBinding?.WhipRecastCast;
                foreach (var pair in arena.WhipRoots)
                {
                    var actor = pair.Key;
                    if (!arena.TryReadWhipRecord(actor, out var record) || !actor.isActiveAndEnabled) continue;
                    float age = arena.Now - record.From;
                    bool rooted = arena.TryReadWhipRoot(actor, out _);
                    bool mark = record.ActionId == "whip_movement" && recastCast && recastCast.ExecutionId == record.Execution
                                && recastCast.RecastRemaining > 0 && !arena.Player.currentCast;
                    bool tighten = record.ActionId == "whip_follow_up" && age < (WhipSkillFx.Find("knot_tighten")?.Duration ?? 0);
                    // 당김(휘감기, 감기는 0.1초)·흔들림(조이기 터짐 0.12초)
                    if (record.ActionId == "whip_default" && age >= 0 && age < .1f && arena.Player)
                        nudges.Set(actor, new Vector3(Mathf.Sign(arena.Player.Position.x - actor.Center.x) * 3 * Px, 0, 0));
                    else if (record.ActionId == "whip_follow_up" && age >= .6f && age < .72f)
                        nudges.Set(actor, new Vector3((Mathf.FloorToInt((age - .6f) * 50) % 2 == 0 ? 3 : -3) * Px, 0, 0));
                    else nudges.Set(actor, Vector3.zero);
                    if (!rooted && !mark && !tighten) continue;
                    var view = Get(actor);
                    view.Root.SetActive(true);
                    view.Root.transform.position = new Vector3(actor.Center.x, actor.Center.y, actor.transform.position.z - .02f);
                    var scale = arena.transform.lossyScale;
                    view.Root.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
                    var visual = actor.visual;
                    if (visual)
                    {
                        view.Front.sortingLayerID = view.Back.sortingLayerID = view.Cue.sortingLayerID = view.Mark.sortingLayerID = visual.sortingLayerID;
                        view.Front.sortingOrder = visual.sortingOrder + 4; view.Back.sortingOrder = visual.sortingOrder - 2;
                        view.Cue.sortingOrder = visual.sortingOrder + 5; view.Mark.sortingOrder = visual.sortingOrder + 7;
                    }
                    string size = Mathf.Max(.5f, actor.size.x) >= LargeBodyWidth ? "large" : "small", set = WhipSkillFx.BindSet(record.ActionId);
                    float remain = Mathf.Min(record.Until, actor.rootUntil) - arena.Now;
                    view.Front.sprite = rooted ? WhipSkillFx.BindFrame(WhipSkillFx.Find(set + "_" + size + "_front"), age, remain) : null;
                    view.Back.sprite = rooted ? WhipSkillFx.BindFrame(WhipSkillFx.Find(set + "_" + size + "_back"), age, remain) : null;
                    view.Cue.sprite = tighten ? WhipSkillFx.Find("knot_tighten")?.At(age) : null;
                    view.Mark.sprite = mark ? WhipSkillFx.Find("knot_mark")?.At(age) : null;
                    view.Mark.transform.localPosition = new Vector3(0, Mathf.Max(.7f, actor.size.y) * .5f + .45f, 0);
                    view.Front.enabled = view.Front.sprite; view.Back.enabled = view.Back.sprite; view.Cue.enabled = view.Cue.sprite; view.Mark.enabled = view.Mark.sprite;
                }
                NetMark();
                NetFan();
            }
            DrawLine(enabled);
            gone.Clear();
            foreach (var pair in views) if (!pair.Key) gone.Add(pair.Key);
            foreach (var actor in gone) { if (views[actor].Root) Object.Destroy(views[actor].Root); views.Remove(actor); }
        }

        // 가시 그물: 플레이어의 그물 시전이 타격 시각을 지나면 한 번, 판정 폭 가운데 바닥에 자국.
        void NetMark()
        {
            var player = arena.Player; var cast = player ? player.currentCast : null;
            if (!cast || cast.isBasic || cast.Definition?.Id != "whip_range" || cast.Definition.Hits.Count == 0) return;
            if (cast.Age < cast.ImpactTime || !netMarked.Add(cast.ExecutionId)) return;
            if (netMarked.Count > 64) netMarked.Clear();
            var clip = WhipSkillFx.Find("net_mark"); var box = cast.Definition.Hits[0].Box;
            if (clip == null || box == null) return;
            int facing = player.facing < 0 ? -1 : 1;
            // 자국은 판정 폭만큼: 구울 때 폭(5.1875u) 대비 지금 판정 폭으로 균일 확대한다(SkillFxHitScale).
            Spawn(arena, clip, new Vector3(player.Position.x + box.X * facing, player.Position.y, player.transform.position.z - .01f), facing, player.visual, 2, "WhipSkillFx net_mark", SkillFxHitScale.WhipNetMark(box.Width));
        }

        // 가시 그물 부채: 시전이 0.315초를 지나면 한 번, 시전 자리 발밑에서 바라보는 쪽으로. 늦게 알아챈 만큼 앞당겨 시전 시계에 맞춘다.
        void NetFan()
        {
            var player = arena.Player; var cast = player ? player.currentCast : null;
            if (!cast || cast.isBasic || cast.Definition?.Id != "whip_range" || cast.PresentationCancelled) return;
            if (cast.Age < WhipSkillFx.NetFanStart || !netThrown.Add(cast.ExecutionId)) return;
            if (netThrown.Count > 64) netThrown.Clear();
            var clip = WhipSkillFx.Find("thorn_net");
            if (clip == null) return;
            int facing = cast.FeelFacing < 0 ? -1 : 1;
            var feet = player.visual ? player.visual.transform.position : (Vector3)player.Position;
            Spawn(arena, clip, new Vector3(feet.x, feet.y, player.transform.position.z - .02f), facing, player.visual, NetFanOrder, "WhipSkillFx thorn_net", 1, cast.Age - WhipSkillFx.NetFanStart);
        }

        void DrawLine(bool enabled)
        {
            var player = arena.Player; var visual = player ? player.visual : null;
            if (enabled && visual && WhipSkillFx.TryLine(visual.sprite, out var data))
            {
                var cast = player.currentCast && !player.currentCast.isBasic && player.currentCast.Definition?.Id?.StartsWith("whip_", StringComparison.Ordinal) == true ? player.currentCast : null;
                Guid execution = cast ? cast.ExecutionId : arena.FeelCharacterBinding?.WhipHoldExecution ?? Guid.Empty;
                if (execution != lineExecution) { lineExecution = execution; lineStarted = arena.Now; }
                int flip = visual.flipX ? -1 : 1;
                Vector3 grip = visual.transform.TransformPoint(new Vector3(data.Grip.x * flip, data.Grip.y, 0));
                var target = arena.WhipTarget(execution);
                if (!target && cast) target = Predict(cast);
                // 줄 끝은 적 몸 가운데가 아니라 손잡이에서 가장 가까운 몸 가장자리(2026-11-08 사용자 확정 「적에 닿게」).
                // 가운데로 가면 가까운 적에서는 줄이 몸에 가려 손 앞 토막만 보였다. 살짝(EdgeInset) 몸 안으로 넣어 닿아 보이게 한다.
                Vector3 goal = target ? TouchPoint(target, grip) : visual.transform.TransformPoint(new Vector3(data.End.x * flip, data.End.y, 0));
                float hitAt = cast ? cast.StartedAt + cast.ImpactTime : float.NegativeInfinity;
                float k = cast && arena.Now < hitAt ? Mathf.Clamp01((arena.Now - lineStarted) / Mathf.Max(.001f, hitAt - lineStarted)) : 1;
                float e = 1 - (1 - k) * (1 - k);
                Vector3 toward = goal - grip; float length = toward.magnitude; Vector3 dir = length > .001f ? toward / length : Vector3.right * flip;
                float start = Mathf.Min(length, .6f), current = start + (length - start) * e;
                Vector3 tip = grip + dir * current;
                var recastCast = arena.FeelCharacterBinding?.WhipRecastCast;
                bool recastReady = !cast && recastCast && recastCast.ExecutionId == execution && recastCast.RecastRemaining > 0;
                float width = recastReady && Mathf.FloorToInt(arena.Now * 4) % 2 == 0 ? data.Width * 1.5f : data.Width;
                Curve(EnsureLine(ref line, "Whip line", visual), grip, tip, (1 - e) * .3f, width);
                heldRecast = recastReady; lastGrip = grip; lastTarget = goal;
            }
            else
            {
                if (line) line.enabled = false;
                // 매듭 접근 재입력 창이 끝나 놓으면 줄이 가운데서 끊겨 처지며 사라진다(다시 D 로 끌려간 경우는 제외).
                if (heldRecast && enabled && player && !player.currentCast) snapStarted = arena.Now;
                heldRecast = false;
            }
            float v = (arena.Now - snapStarted) / .3f;
            if (enabled && visual && v >= 0 && v < 1 && Mathf.FloorToInt(v * 10) % 3 != 2)
            {
                Vector3 mid = (lastGrip + lastTarget) * .5f; float drop = 1.25f * v * v + .4f * v;
                Curve(EnsureLine(ref snapA, "Whip snap A", visual), lastGrip, mid + new Vector3(-.25f, -drop, 0), .3f + .6f * v, .0625f);
                Curve(EnsureLine(ref snapB, "Whip snap B", visual), lastTarget, mid + new Vector3(.25f, -drop, 0), .3f + .6f * v, .0625f);
            }
            else { if (snapA) snapA.enabled = false; if (snapB) snapB.enabled = false; }
        }

        internal const float EdgeInset = .15f;
        internal static Vector3 TouchPoint(CombatActor target, Vector3 grip)
        {
            Vector2 edge = target.ClosestHurtPoint(grip), center = target.Center, inward = center - edge;
            float depth = inward.magnitude;
            if (depth > .001f) edge += inward / depth * Mathf.Min(EdgeInset, depth);
            return new Vector3(edge.x, edge.y, grip.z);
        }

        // 타격 전 줄 끝이 향할 적: 첫 타격 상자 안에서 시전자에게 가장 가까운 적(게임 판정은 이것과 별개로 기존 경로가 정한다).
        CombatActor Predict(CombatCast cast)
        {
            if (cast.Definition.Hits.Count == 0 || cast.Definition.Hits[0].Box == null) return null;
            var box = cast.Definition.Hits[0].Box; var owner = cast.owner; int facing = owner.facing < 0 ? -1 : 1;
            CombatActor best = null; float bestDistance = float.MaxValue;
            foreach (var actor in arena.TargetsInBox(owner.Position + new Vector2(box.X * facing, box.Y), new Vector2(box.Width, box.Height)))
            {
                if (!actor || actor == owner || actor.isPlayer || actor.Dead) continue;
                float distance = Mathf.Abs(actor.Center.x - owner.Position.x);
                if (distance < bestDistance) { bestDistance = distance; best = actor; }
            }
            return best;
        }

        LineRenderer EnsureLine(ref LineRenderer renderer, string name, SpriteRenderer over)
        {
            if (!lineRoot) { lineRoot = new GameObject("[Whip line]") { hideFlags = HideFlags.DontSave }; lineRoot.transform.SetParent(arena.transform, false); }
            if (!renderer)
            {
                var go = new GameObject(name); go.transform.SetParent(lineRoot.transform, false);
                renderer = go.AddComponent<LineRenderer>(); renderer.useWorldSpace = true; renderer.positionCount = 17;
                renderer.numCapVertices = 0; renderer.textureMode = LineTextureMode.Stretch;
                if (!lineMaterial) lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
                renderer.sharedMaterial = lineMaterial; renderer.startColor = renderer.endColor = Ink;
            }
            renderer.sortingLayerID = over.sortingLayerID; renderer.sortingOrder = over.sortingOrder + 1;
            renderer.enabled = true;
            return renderer;
        }

        static void Curve(LineRenderer renderer, Vector3 a, Vector3 b, float sag, float width)
        {
            renderer.startWidth = renderer.endWidth = width;
            Vector3 control = (a + b) * .5f + Vector3.down * sag;
            for (int i = 0; i <= 16; i++)
            {
                float q = i / 16f;
                renderer.SetPosition(i, (1 - q) * (1 - q) * a + 2 * (1 - q) * q * control + q * q * b);
            }
        }

        View Get(CombatActor actor)
        {
            if (views.TryGetValue(actor, out var view) && view.Root) return view;
            var root = new GameObject("[Whip bind] " + actor.name) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(arena.transform, false);
            view = new View { Root = root, Back = Add(root, "Whip bind back"), Front = Add(root, "Whip bind front"), Cue = Add(root, "Whip knot tighten"), Mark = Add(root, "Whip knot mark") };
            views[actor] = view;
            return view;
        }

        SpriteRenderer Add(GameObject root, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.enabled = false;
            if (arena.spriteMaterial) renderer.sharedMaterial = arena.spriteMaterial;
            return renderer;
        }

        public void Clear()
        {
            foreach (var pair in views) if (pair.Value.Root) Object.Destroy(pair.Value.Root);
            views.Clear(); netMarked.Clear(); netThrown.Clear();
            if (nudges) nudges.Clear();
            if (line) line.enabled = false; if (snapA) snapA.enabled = false; if (snapB) snapB.enabled = false;
            heldRecast = false; snapStarted = -10; lineExecution = Guid.Empty;
            for (int i = live.Count - 1; i >= 0; i--)
                if (!live[i]) live.RemoveAt(i);
                else if (live[i].Arena == arena) { Object.Destroy(live[i].gameObject); live.RemoveAt(i); }
        }

        internal static int ActiveFlipbooks(CombatArena arena)
        {
            int n = 0;
            foreach (var book in live) if (book && book.Arena == arena) n++;
            return n;
        }
        internal int ActiveBinds { get { int n = 0; foreach (var pair in views) if (pair.Value.Root && pair.Value.Root.activeSelf && pair.Value.Front.enabled) n++; return n; } }
        internal bool LineVisible => line && line.enabled;
        internal Vector3 LineEnd => line && line.enabled ? line.GetPosition(16) : Vector3.zero;
    }

    // 한 번 재생하고 사라지는 장 묶음(arena 시계).
    internal sealed class WhipFxFlipbook : MonoBehaviour
    {
        CombatArena arena; WhipSkillFx.Clip clip; SpriteRenderer view; float started;
        internal CombatArena Arena => arena;
        internal void Begin(CombatArena value, WhipSkillFx.Clip source, SpriteRenderer renderer, float start)
        { arena = value; clip = source; view = renderer; started = start; Sample(); }
        void Sample()
        {
            if (!arena || clip == null || !view) return;
            float age = arena.Now - started;
            int index = Mathf.FloorToInt(age / clip.Step);
            if (index >= clip.Sprites.Length) { Destroy(gameObject); return; }
            view.sprite = index < 0 ? null : clip.Sprites[index];
            view.enabled = index >= 0;
        }
        void LateUpdate() => Sample();
    }

    // 적 그림 위치에 잠깐 덧씌우는 당김·흔들림. 몹 표시 코드가 매 프레임 위치를 다시 쓰므로 가장 늦게 실행해 그 위에 더한다.
    // 앞 프레임에 더한 값이 그대로 남아 있으면 빼고 새 값을 더해 누적되지 않게 한다.
    [DefaultExecutionOrder(32000)]
    internal sealed class WhipNudges : MonoBehaviour
    {
        sealed class Entry { public Vector3 Want, Applied, Written; }
        readonly Dictionary<CombatActor, Entry> entries = new Dictionary<CombatActor, Entry>();
        readonly List<CombatActor> drop = new List<CombatActor>();
        internal void Set(CombatActor actor, Vector3 offset)
        {
            if (!actor) return;
            if (!entries.TryGetValue(actor, out var entry)) { if (offset == Vector3.zero) return; entries[actor] = entry = new Entry(); }
            entry.Want = offset;
        }
        internal void Clear() { foreach (var pair in entries) Restore(pair.Key, pair.Value); entries.Clear(); }
        static void Restore(CombatActor actor, Entry entry)
        {
            if (!actor || !actor.visual) return;
            var t = actor.visual.transform;
            if (t.localPosition == entry.Written) t.localPosition -= entry.Applied;
        }
        void LateUpdate()
        {
            drop.Clear();
            foreach (var pair in entries)
            {
                var actor = pair.Key; var entry = pair.Value;
                if (!actor || !actor.visual) { drop.Add(actor); continue; }
                var t = actor.visual.transform;
                var basis = t.localPosition == entry.Written ? t.localPosition - entry.Applied : t.localPosition;
                t.localPosition = basis + entry.Want; entry.Applied = entry.Want; entry.Written = t.localPosition;
                if (entry.Want == Vector3.zero) drop.Add(actor);
                entry.Want = Vector3.zero;   // Refresh 가 매 프레임 다시 정한다
            }
            foreach (var actor in drop) entries.Remove(actor);
        }
    }
}
