using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // 잉크 파츠 이펙트 시험판: CombatArena.MvpInkCued 신호를 받아 pixelfx 스프라이트 시트를 튼다(보여 주기만, 규칙·수치는 안 건드린다).
    // 시트는 Resources/InkPartFx/<이름>.png + .json(Aseprite 형식, meta.pixelfx 에 장 크기·피벗·열·fps). 오른쪽 기준이라 왼쪽이면 좌우 반전한다.
    // 시트가 없으면(그림 미배치·테스트) 아무것도 하지 않는다. 시간은 전투 시계(arena.Now)를 따라 피격 멈춤에 같이 멈춘다.
    // 파츠 → 시트 연결은 전투테스트/이펙트실험실 에서 고른 것과 같다. 시트 원본과 고른 이유는 전투테스트/<이펙트>/README.md.
    public sealed class Chapter1InkPartFx : MonoBehaviour
    {
        const float PPU = 32, Floor = 28 / PPU, ThornDelay = .375f, FadeSeconds = .2f;

        [Serializable] public sealed class SheetJson { public SheetMeta meta; }
        [Serializable] public sealed class SheetMeta { public SheetInfo pixelfx; }
        // fieldRadius: 바닥 장판 시트에서 반경 1배에 해당하는 픽셀(전기장 반경에 맞춰 크기를 조절한다)
        [Serializable] public sealed class SheetInfo { public int[] frameSize, pivot; public int columns, frames, holdFrame = -1; public float fps, fieldRadius; public bool loop; }
        sealed class Sheet { public Sprite[] Frames; public float Fps, FieldRadius; public bool Loop; public int Hold; }
        sealed class Play
        {
            public SpriteRenderer Body; public Sheet Sheet; public float Age, Delay, FadeLeft; public bool Fading; public int Start;
            public Func<Vector2> At; public Func<int> Face; public Func<bool> Alive; public float Stretch = 1, Scale = 1; public Action OnEnd;
            // 전용 그림이 없는 효과는 기존 시트에 색을 입혀 임시로 쓴다(번개·에너지 기)
            public Color Tint = Color.white;
        }

        readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>();
        readonly List<Play> plays = new List<Play>();
        readonly Dictionary<string, Play> held = new Dictionary<string, Play>();
        CombatArena arena;
        float last, shieldUntil;
        bool chargeShown;
        public int Shown { get; private set; }

        public void Initialize(CombatArena owner)
        {
            arena = owner;
            if (!arena) return;
            arena.MvpInkCued += Cue;
            last = arena.Now;
        }

        void OnDestroy() { if (arena) arena.MvpInkCued -= Cue; }

        Sheet Load(string name)
        {
            if (sheets.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("InkPartFx/" + name);
            var json = Resources.Load<TextAsset>("InkPartFx/" + name);
            if (tex && json)
            {
                var info = JsonUtility.FromJson<SheetJson>(json.text)?.meta?.pixelfx;
                if (info != null && info.frameSize != null && info.frameSize.Length == 2)
                {
                    tex.filterMode = FilterMode.Point;
                    int fw = info.frameSize[0], fh = info.frameSize[1];
                    var pivot = new Vector2(info.pivot[0] / (float)fw, 1 - info.pivot[1] / (float)fh);
                    var frames = new Sprite[info.frames];
                    for (int i = 0; i < info.frames; i++)
                    {
                        int col = i % info.columns, row = i / info.columns;
                        frames[i] = Sprite.Create(tex, new Rect(col * fw, tex.height - (row + 1) * fh, fw, fh), pivot, PPU, 0, SpriteMeshType.FullRect);
                    }
                    s = new Sheet { Frames = frames, Fps = info.fps > 0 ? info.fps : 25, Loop = info.loop, Hold = Mathf.Max(0, info.holdFrame), FieldRadius = info.fieldRadius };
                }
            }
            sheets[name] = s;
            return s;
        }

        Play Spawn(string name, Func<Vector2> at, Func<int> face, float delay = 0, Func<bool> alive = null, int start = 0, float stretch = 1)
        {
            var sheet = Load(name);
            if (sheet == null) return null;
            var go = new GameObject("InkFx " + name);
            go.transform.SetParent(transform, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.enabled = false;
            var actor = arena.Player ? arena.Player.visual : null;
            if (actor) { body.sortingLayerID = actor.sortingLayerID; body.sortingOrder = actor.sortingOrder + 20; body.sharedMaterial = actor.sharedMaterial; }
            else body.sortingOrder = 60;
            var play = new Play { Body = body, Sheet = sheet, Delay = delay, At = at, Face = face, Alive = alive, Start = start, Stretch = stretch };
            plays.Add(play); Shown++;
            return play;
        }
        Play Spawn(string name, Vector2 at, int face, float delay = 0, int start = 0, float stretch = 1) => Spawn(name, () => at, () => face, delay, null, start, stretch);
        static Play Tinted(Play play, Color tint) { if (play != null) play.Tint = tint; return play; }
        static readonly Color Yellow = new Color32(253, 216, 53, 255);

        // 파츠마다 하나만 떠 있어야 하는 반복 시트(이속 줄기·보호막·둔화 물결·기운)
        void Hold(string key, Func<Play> make)
        {
            if (held.TryGetValue(key, out var p) && plays.Contains(p) && !p.Fading) return;
            var made = make();
            if (made != null) held[key] = made;
        }

        // 방 섬멸 회복도 회복 파츠와 같은 초록 회복 시트를 몸에 띄운다. 시트가 없으면 false
        public bool PlayHeal() => arena && Spawn("heal_green", PlayerFeet, PlayerFace) != null;

        // 몬스터에게 터지는 효과의 자리: 맞은(쓰러진) 몬스터 발밑에서 시트의 바닥 높이만큼 올린 몸통 가운데.
        // 신호의 Origin 은 처치·속박 해제·전기장일 때만 대상 발밑이고 그 밖에는 플레이어 발밑이라 대상을 우선한다
        static Vector2 TargetBody(MvpInkCue cue) => (cue.Target ? cue.Target.Position : cue.Origin) + Vector2.up * Floor;

        Vector2 PlayerFeet() => arena.Player ? arena.Player.Position : Vector2.zero;
        int PlayerFace() => arena.Player ? arena.Player.facing : 1;

        void Cue(MvpInkCue cue)
        {
            switch (cue.Kind)
            {
                // 표식이 하나씩 터지는 순간(mark_pop)과 독·속박·기절 상태는 몬스터 상태이상 표시(InkStatusVisuals,
                // resources effects/ink-status 확정 시트)가 그린다. 여기서 같은 순간에 임시 시트를 겹쳐 띄우지 않는다.
                case "chain_lightning_hit":
                    Tinted(Spawn("emp-hit_red", cue.Point, cue.Facing), Yellow);
                    break;
                case "move_charge":
                    // 질주 충전: 움직이는 동안 몸에 에너지 기가 쌓이는 모습. 기가 사라지거나 방출되면 꺼진다
                    Hold("energy", () => Tinted(Spawn("charge-rise_red", PlayerFeet, PlayerFace, 0, () => arena.ReadMvpCharge() > 0), Yellow));
                    break;
                case "move_charge_release":
                    Tinted(Spawn("charge-release_red", PlayerFeet, PlayerFace), Yellow);
                    Tinted(Spawn("emp-hit_red", cue.Point, cue.Facing), Yellow);
                    break;
                case "poison_burst":
                    // 독폭탄은 타격 시 발동이라 신호의 발밑 기준점(Origin)이 플레이어다. 독이 터진 몬스터 몸통에서 반경 3.2로 펑 터진다
                    Spawn("ink-splash_purple", TargetBody(cue), cue.Facing);
                    break;
                case "mark_burst":
                    {
                        var order = new[] { "red", "yellow", "green", "blue", "purple" };
                        var colors = order.Where(c => cue.Colors != null && cue.Colors.Contains(c)).ToArray();
                        string name = $"mark-burst_{(colors.Length == 0 ? "base" : string.Join("-", colors))}_m{Mathf.Clamp(cue.Count, 1, 5)}";
                        var sheet = Load(name);
                        if (sheet != null) Spawn(name, cue.Origin + Vector2.up * Floor, cue.Facing, 0, sheet.Hold);
                        break;
                    }
                case "extra_hit":
                case "homing_counter":
                    Spawn("follow-cut_red_v" + UnityEngine.Random.Range(1, 4), cue.Point, cue.Facing);
                    break;
                case "kill_area_hit":
                    // 처치 폭발: 쓰러진 자리에서 피해 반경만큼 잉크가 둥글게 터진다
                    // (시트는 피해 반경 6 = 192px 에 맞춰 그렸다. 반경은 모든 레벨에서 6이다)
                    Spawn("ink-splash_red", TargetBody(cue), cue.Facing);
                    break;
                case "burst_extra_hit":
                case "area_hit":
                    Spawn("burst-thorn_red", TargetBody(cue), cue.Facing, ThornDelay);
                    break;
                case "dash_hit":
                case "cc_count_hit":
                case "underline_mark":
                case "retaliation_release":
                    Spawn("emp-hit_red", cue.Point, cue.Facing);
                    break;
                case "switch_slide":
                    // 옮겨 쓰기 전용 그림 전까지 출발 자리에 노랑 이속 폭발 시트를 임시로 쓴다
                    Spawn("speed-burst_yellow", cue.Origin, cue.Facing);
                    break;
                case "next_attack_power":
                    // 기세를 몰아·처치 강화만 기다리는 동안 기운을 띄운다. 힘 실어 건네기는 바꾸자마자 공격이라 강한 한 방만 보인다
                    if (cue.Trigger == "kill") { chargeShown = true; Hold("charge", () => Spawn("charge-rise_red", PlayerFeet, PlayerFace, 0, () => arena.ReadMvpBuff("power") > 0)); }
                    break;
                case "power_release":
                    if (chargeShown) Spawn("charge-release_red", PlayerFeet, PlayerFace);
                    chargeShown = false;
                    break;
                case "power_hit":
                    Spawn("emp-hit_red", cue.Point, cue.Facing);
                    break;
                case "attack_and_move_speed":
                case "moving_attack_power":
                    if (!held.TryGetValue("speed", out var trail) || !plays.Contains(trail)) Spawn("speed-burst_yellow", PlayerFeet, PlayerFace);
                    Hold("speed", () =>
                    {
                        var p = Spawn("speed-trail_yellow", PlayerFeet, PlayerFace, 0, () => arena.ReadMvpBuff("move") > 0);
                        if (p != null) p.OnEnd = () => Spawn("speed-end_yellow", PlayerFeet, PlayerFace);
                        return p;
                    });
                    break;
                case "electric_field":
                    // 전기장은 신호가 아니라 실제 전기장 목록(SyncFields)을 따라 깔리고 지워진다
                    break;
                case "heal":
                    PlayHeal();
                    break;
                case "shield":
                case "hit_guard_shield":
                    {
                        // 단단한 껍질은 시간 제한이 없어 꺼지면 언제나 맞아서 깨진 것이다
                        shieldUntil = cue.Kind == "hit_guard_shield" ? float.PositiveInfinity : arena.Now + cue.Duration;
                        var up = Spawn("shield-up_green", PlayerFeet, () => 1);
                        if (up != null) up.OnEnd = () => Hold("shield", () =>
                        {
                            var p = Spawn("shield_green", PlayerFeet, () => 1, 0, () => arena.ReadInkShield() > 0);
                            // 시간보다 일찍 꺼지면 맞아서 깨진 것
                            if (p != null) p.OnEnd = () => { if (arena.Now < shieldUntil - .05f) Spawn("shield-break_green", PlayerFeet, () => 1); };
                            return p;
                        });
                        break;
                    }
            }
        }

        // 전기장: 깔린 동안 반복 시트를 바닥에 두고, 전기장이 사라지면 흐려지며 지운다.
        // 전기 장판은 반경에 맞춘 번개 장판 시트 하나, 전기 발자국은 작은 번개 파티클 시트를 반경 안에 나란히 깐다.
        const string FieldSheet = "electric-field_yellow", FootstepSheet = "footstep-electric-micro-spark", FootstepPart = "ink3_yellow_01";
        // 발자국 파티클 하나는 폭이 약 0.6unit이라 반경의 -0.6·0·+0.6 배 자리에 셋을 두면 전기장 지름을 채운다
        static readonly float[] FootstepSpread = { -.6f, 0, .6f };
        readonly Dictionary<(Vector2, float, string), List<Play>> fields = new Dictionary<(Vector2, float, string), List<Play>>();
        readonly HashSet<(Vector2, float, string)> liveFields = new HashSet<(Vector2, float, string)>();
        readonly List<(Vector2, float, string)> staleFields = new List<(Vector2, float, string)>();
        int fieldSerial;
        public int VisibleFields { get; private set; }

        void SyncFields()
        {
            liveFields.Clear();
            foreach (var f in arena.ReadMvpFields())
            {
                var key = (f.feet, f.until, f.partId);
                liveFields.Add(key);
                if (fields.ContainsKey(key)) continue;
                var made = f.partId == FootstepPart ? SpawnFootstep(key, f.feet, f.radius) : SpawnFloor(key, f.feet, f.radius);
                if (made.Count == 0) continue;
                // 바닥 효과라 캐릭터 뒤에 깐다
                var actor = arena.Player ? arena.Player.visual : null;
                foreach (var play in made) play.Body.sortingOrder = actor ? actor.sortingOrder - 1 : 9;
                fields[key] = made;
            }
            staleFields.Clear();
            int visible = 0;
            foreach (var pair in fields) { if (!pair.Value.Any(p => p.Body && plays.Contains(p))) staleFields.Add(pair.Key); else visible++; }
            foreach (var key in staleFields) fields.Remove(key);
            VisibleFields = visible;
        }

        List<Play> SpawnFloor((Vector2, float, string) key, Vector2 at, float radius)
        {
            var made = new List<Play>();
            var sheet = Load(FieldSheet);
            if (sheet == null) return made;
            var play = Spawn(FieldSheet, () => at, () => 1, 0, () => liveFields.Contains(key), fieldSerial++ % sheet.Frames.Length);
            if (play == null) return made;
            play.Scale = sheet.FieldRadius > 0 ? radius * PPU / sheet.FieldRadius : 1;
            made.Add(play);
            return made;
        }

        // 원본 크기(1배) 그대로 깔고, 파티클마다 재생 시작 장과 좌우를 엇갈려 같은 번개가 동시에 번쩍이지 않게 한다
        List<Play> SpawnFootstep((Vector2, float, string) key, Vector2 feet, float radius)
        {
            var made = new List<Play>();
            var sheet = Load(FootstepSheet);
            if (sheet == null) return made;
            foreach (var spread in FootstepSpread)
            {
                int serial = fieldSerial++;
                Vector2 at = feet + Vector2.right * (spread * radius);
                int face = serial % 2 == 0 ? 1 : -1;
                var play = Spawn(FootstepSheet, () => at, () => face, 0, () => liveFields.Contains(key), serial * 3 % sheet.Frames.Length);
                if (play != null) made.Add(play);
            }
            return made;
        }

        void LateUpdate()
        {
            if (!arena) return;
            SyncFields();
            float now = arena.Now, dt = Mathf.Clamp(now - last, 0, .1f);
            last = now;
            for (int i = plays.Count - 1; i >= 0; i--)
            {
                var p = plays[i];
                if (!p.Body) { plays.RemoveAt(i); continue; }
                if (p.Delay > 0) { p.Delay -= dt; continue; }
                p.Age += dt;
                // 반복 시트는 조건이 끝나면 0.2초 동안 흐려지며 사라진다
                if (p.Alive != null && !p.Fading && !p.Alive()) { p.Fading = true; p.FadeLeft = FadeSeconds; }
                if (p.Fading && (p.FadeLeft -= dt) <= 0) { Finish(i, p); continue; }
                int n = p.Sheet.Frames.Length, frame = p.Start + (int)(p.Age * p.Sheet.Fps + 1e-4f);
                if (!p.Sheet.Loop && frame >= n) { Finish(i, p); continue; }
                p.Body.enabled = true;
                p.Body.sprite = p.Sheet.Frames[p.Sheet.Loop ? frame % n : frame];
                var tint = p.Tint; tint.a *= p.Fading ? Mathf.Clamp01(p.FadeLeft / FadeSeconds) : 1;
                p.Body.color = tint;
                p.Body.transform.position = p.At();
                p.Body.transform.localScale = new Vector3(p.Face() * p.Stretch * p.Scale, p.Scale, 1);
            }
        }

        void Finish(int index, Play p)
        {
            plays.RemoveAt(index);
            if (p.Body) Destroy(p.Body.gameObject);
            p.OnEnd?.Invoke();
        }
    }
}
