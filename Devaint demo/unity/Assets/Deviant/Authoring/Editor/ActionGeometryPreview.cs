using System;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    // Shared by numeric, frame and motion editors. Only explicit Apply/Save
    // buttons in the owning window may touch the runtime or source files.
    public sealed class ActionGeometryPreview : VisualElement
    {
        readonly Slider time, zoom;
        readonly Toggle left, body, hurtbox;
        readonly DropdownField direction;
        readonly Label state, media, sample, legend;
        readonly IMGUIContainer canvas;
        readonly Vector3[] pathPoints = new Vector3[2], boxPoints = new Vector3[5];
        Catalog assetForms, injectedForms;
        CharacterAnimationCatalog assetMedia, injectedMedia;
        Func<string, Sprite> injectedResolver;
        string injectedProvenance;
        bool assetsLoaded, fitPending = true;
        Vector2 viewCenter = new Vector2(0, 1.5f);
        string lastAction, lastFixed, lastFrame;
        Catalog lastForms;
        CharacterAnimationCatalog lastMedia;
        BasicMeleeDefinition pendingAction;
        FixedAttackMotion pendingFixed;
        MeleeFrameDefinition pendingFrame;
        ActionTrackDefinition pendingTracks;
        int pendingHit;
        string lastTracks;
        DirectMeleeSpecialDefinition pendingDirect;
        string lastDirect;
        ChargedMeleeDefinition pendingCharged;
        float pendingRelease;
        string lastCharged;
        AuthoredSkillDefinition pendingAuthored;
        string lastAuthored;
        CombatArena pendingArena;
        FrameAttackMotionSource lastSource;
        string lastSourceAction;
        MeleeFrameDefinition sourceFrame;
        public ActionGeometryPreviewModel Model { get; private set; }
        public ActionGeometrySample CurrentSample => Model == null ? default : Model.Sample(time.value, left.value ? -1 : 1, InputDirection);
        int PreviewDirection => Model?.Tracks != null || Model?.Authored != null ? left.value ? -1 : 1 : InputDirection;
        int InputDirection => direction.index == 1 ? 1 : direction.index == 2 ? -1 : 0;

        public ActionGeometryPreview()
        {
            name = "action-geometry-preview";
            style.marginTop = 8; style.marginBottom = 12;
            var heading = new Label("장면 미리보기 · 읽기 전용") { name = "geometry-heading" };
            heading.style.unityFontStyleAndWeight = FontStyle.Bold; Add(heading);
            state = Wrapped("geometry-state"); Add(state);
            time = new Slider("행동 시간 (s)", 0, 1) { name = "geometry-time", showInputField = true };
            time.RegisterValueChangedCallback(_ => RefreshSample()); Add(time);
            var controls = new VisualElement(); controls.style.flexDirection = FlexDirection.Row; controls.style.flexWrap = Wrap.Wrap;
            left = new Toggle("왼쪽 보기") { name = "geometry-facing-left" };
            left.RegisterValueChangedCallback(_ => {
                if (InputDirection != 0) direction.SetValueWithoutNotify(direction.choices[left.value ? 2 : 1]);
                fitPending = true; RefreshSample();
            }); controls.Add(left);
            direction = new DropdownField("지상 입력", new System.Collections.Generic.List<string> { "입력 없음", "오른쪽 (+1)", "왼쪽 (-1)" }, 1) { name = "geometry-input" };
            direction.RegisterValueChangedCallback(_ => {
                if (InputDirection != 0) left.SetValueWithoutNotify(InputDirection < 0);
                fitPending = true; RefreshSample();
            }); controls.Add(direction);
            body = new Toggle("몸") { name = "geometry-body", value = true };
            hurtbox = new Toggle("피격") { name = "geometry-hurtbox", value = true };
            body.RegisterValueChangedCallback(_ => canvas.MarkDirtyRepaint()); hurtbox.RegisterValueChangedCallback(_ => canvas.MarkDirtyRepaint());
            controls.Add(body); controls.Add(hurtbox); Add(controls);
            zoom = new Slider("배율 (1× = 32px/unit)", .25f, 6) { name = "geometry-zoom", value = 1.5f, showInputField = true };
            zoom.RegisterValueChangedCallback(_ => { fitPending = false; canvas.MarkDirtyRepaint(); }); Add(zoom);
            var fit = new Button(() => { fitPending = true; canvas.MarkDirtyRepaint(); }) { name = "geometry-fit", text = "전체 형상 / 바인딩 크기에 맞춤" }; Add(fit);
            canvas = new IMGUIContainer(Draw) { name = "geometry-canvas" };
            canvas.style.height = 300; canvas.style.minHeight = 300; canvas.style.flexShrink = 0; Add(canvas);
            sample = Wrapped("geometry-sample"); Add(sample);
            legend = Wrapped("geometry-legend");
            legend.text = "1unit 격자 · PPU 32 기준 · Sprite.rect/pivot/실제 PPU로 그림 배치\n청록 실선: 지형 몸 · 분홍 점선: 별도 피격 영역 (같은 고정 기하) · 주황: 현재 선택 타격의 활성 박스 · 파랑: 지상 전진 경로";
            Add(legend);
            media = Wrapped("geometry-media"); Add(media);
            var notice = Wrapped("geometry-read-only");
            notice.text = "공속 M=1 · 발 원점/scale 1 · 일정한 방향 입력의 평지 예상 경로 (지형·중력·충돌 해석 아님). 스크럽/반전/배율은 전투 시작·tick·리셋·피해·자원·Cue·적용·저장을 실행하지 않습니다.";
            Add(notice);
            RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += ProjectChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= ProjectChanged);
            SetInvalid("미리볼 정의를 불러오세요.");
        }
        static Label Wrapped(string name)
        {
            var label = new Label { name = name }; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 5; return label;
        }

        // Allows a caller/capture to supply actual bound Sprites without creating
        // gameplay objects or importing invented art. The model captures a copy.
        public void SetMedia(Catalog forms, CharacterAnimationCatalog catalog, Func<string, Sprite> resolveSprite = null, string provenance = null)
        {
            injectedForms = forms; injectedMedia = catalog; injectedResolver = resolveSprite; injectedProvenance = provenance;
            InvalidateBinding();
            if (pendingAction != null) SetCandidate(pendingAction, pendingFixed, pendingFrame, pendingArena);
            else if (pendingTracks != null) SetCandidate(pendingTracks, pendingHit, pendingArena);
            else if (pendingDirect != null) SetCandidate(pendingDirect, pendingArena, pendingFrame);
            else if (pendingCharged != null) SetCandidate(pendingCharged, pendingRelease, pendingArena);
            else if (pendingAuthored != null) SetCandidate(pendingAuthored, pendingHit, pendingArena);
        }
        void ProjectChanged()
        {
            assetsLoaded = false; assetForms = null; assetMedia = null; InvalidateBinding();
            if (pendingAction != null) SetCandidate(pendingAction, pendingFixed, pendingFrame, pendingArena);
            else if (pendingTracks != null) SetCandidate(pendingTracks, pendingHit, pendingArena);
            else if (pendingDirect != null) SetCandidate(pendingDirect, pendingArena, pendingFrame);
            else if (pendingCharged != null) SetCandidate(pendingCharged, pendingRelease, pendingArena);
            else if (pendingAuthored != null) SetCandidate(pendingAuthored, pendingHit, pendingArena);
        }
        void InvalidateBinding() { lastAction = lastFixed = lastFrame = lastTracks = lastDirect = lastCharged = lastAuthored = null; lastForms = null; lastMedia = null; }
        void LoadAssets()
        {
            if (assetsLoaded) return;
            assetsLoaded = true;
            var forms = Resources.Load<TextAsset>("MeleeCatalog");
            if (forms) assetForms = JsonUtility.FromJson<Catalog>(forms.text);
            var animations = Resources.Load<TextAsset>("CharacterV2/AnimationCatalog");
            if (animations) assetMedia = JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text);
        }
        public void SetFirstSwordCandidate(BasicMeleeDefinition action, FrameAttackMotionSource source, CombatArena arena = null)
        {
            if (source?.UsesFrameTime == true)
            {
                if (lastSource != source || lastSourceAction != action?.ContentHash)
                {
                    LoadAssets(); var context = (arena ? arena.catalog : null) ?? injectedForms ?? assetForms;
                    float inputSpeed = Array.Find(context.forms, form => form.id == "base_sword").inputSpeed;
                    sourceFrame = source.Compile(action, inputSpeed); lastSource = source; lastSourceAction = action.ContentHash;
                }
                SetCandidate(sourceFrame.Action, frame: sourceFrame, arena: arena);
            }
            else SetCandidate(action, source?.FixedBinding, arena: arena);
        }
        public void SetCandidate(BasicMeleeDefinition action, FixedAttackMotion fixedMotion = null, MeleeFrameDefinition frame = null, CombatArena arena = null)
        {
            if (action == null) { SetInvalid("유효한 선택 정의가 없습니다."); return; }
            pendingTracks = null; lastTracks = null; pendingDirect = null; lastDirect = null; pendingCharged = null; lastCharged = null; pendingAuthored = null; lastAuthored = null;
            pendingAction = action; pendingFixed = fixedMotion; pendingFrame = frame; pendingArena = arena;
            LoadAssets();
            var forms = (arena ? arena.catalog : null) ?? injectedForms ?? assetForms;
            var animations = injectedMedia ?? (arena ? arena.characterAnimations : null) ?? assetMedia;
            if (Model != null && lastAction == action.ContentHash && lastFixed == fixedMotion?.ContentHash && lastFrame == frame?.ContentHash && lastForms == forms && lastMedia == animations) return;
            string previousId = Model?.ActionId;
            try
            {
                Model = ActionGeometryPreviewModel.Compile(action, forms, animations, fixedMotion, frame, injectedResolver);
                lastAction = action.ContentHash; lastFixed = fixedMotion?.ContentHash; lastFrame = frame?.ContentHash; lastForms = forms; lastMedia = animations;
                time.highValue = Model.Action.End; time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, Model.Action.End));
                time.SetEnabled(true); left.SetEnabled(true); direction.SetEnabled(true);
                if (previousId != action.Id) fitPending = true;
                state.text = $"{action.Id} · {(frame != null ? "FrameTime 후보" : "전투 시간 고정 후보")} · 실효 접촉 {Model.Action.Impact:F5}s · 활성 [{Model.Action.Impact:F5}, {Model.Action.Impact + Model.Action.Active:F5})s · 종료 {Model.Action.End:F5}s" +
                    $"\n지상 전진 구간 [{Model.Action.AdvanceStart:F5}, {Model.Action.AdvanceStart + Model.Action.AdvanceDuration:F5})s · 정의 거리 {Model.Action.AdvanceDistance:F5}unit (방향 입력 시, 행동 종료로 잘림)";
                string origin = injectedProvenance ?? (injectedMedia != null ? "호출자가 주입한 실제 미디어 참조 (스냅샷)" : arena && arena.characterAnimations != null ? "선택 전투의 현재 미디어 참조 (읽기 전용 스냅샷)" : "Resources CharacterV2/AnimationCatalog 메타데이터 + 실제 Sprite 에셋");
                media.text = "요청 " + Model.RequestedBinding + " · 본체 바인딩 " + Model.BoundBinding + "\n" + origin +
                    $" · 로드 {Model.LoadedSprites}/{Model.SpriteCount}" + (string.IsNullOrEmpty(Model.MediaDiagnostic) ? "" : "\n" + Model.MediaDiagnostic);
                RefreshSample();
            }
            catch (ArgumentException error) { SetInvalid(error.Message); }
        }
        public void SetCandidate(ActionTrackDefinition tracks, int selectedHit, CombatArena arena = null)
        {
            if (tracks == null) { SetInvalid("유효한 finite track 정의가 없습니다."); return; }
            pendingAction = null; pendingFixed = null; pendingFrame = null; pendingDirect = null; lastDirect = null; pendingCharged = null; lastCharged = null; pendingAuthored = null; lastAuthored = null; pendingTracks = tracks; pendingHit = selectedHit; pendingArena = arena;
            LoadAssets();
            var animations = injectedMedia ?? (arena ? arena.characterAnimations : null) ?? assetMedia;
            string key = tracks.ContentHash + "/" + selectedHit;
            if (Model != null && lastTracks == key && lastMedia == animations) return;
            try
            {
                string previous = Model?.ActionId;
                Model = ActionGeometryPreviewModel.Compile(tracks, selectedHit, animations, injectedResolver); lastTracks = key; lastMedia = animations;
                time.highValue = Model.End; time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, Model.End));
                time.SetEnabled(true); left.SetEnabled(true); direction.SetEnabled(false);
                if (previous != tracks.Id) fitPending = true;
                var hit = tracks.Hits.Count == 0 ? null : tracks.Hits[selectedHit];
                state.text = $"{tracks.Id} · CombatTimeFixed 유한 트랙 · 본체 종료 {tracks.BodyEnd:F5}s" +
                    (hit == null ? $"\n방어 {tracks.Defense.Id}: [{tracks.Defense.Start:F5}, {tracks.Defense.End:F5})s · 실제 activation clock 기준 지속 {tracks.Defense.Duration:F5}s · 공격 박스 없음" : $"\n타격 {hit.Id} [{hit.Start:F5}, {hit.End:F5})s · 독립 판정 보관 종료 {tracks.TailEnd:F5}s") +
                    (tracks.Motion == null ? "\n강제 actor 이동 없음" : $"\n강제 이동 [{tracks.Motion.Start:F5}, {tracks.Motion.End:F5})s · 속도 {tracks.Motion.Speed:F5}unit/s") +
                    (tracks.Effect == null ? "" : $"\n검기: 속도/거리/고체 충돌의 기존 수명 · span은 최소 보관창 (타이머로 피해를 절단하지 않음)");
                media.text = "요청 " + Model.RequestedBinding + " · 본체 바인딩 " + Model.BoundBinding + $" · 로드 {Model.LoadedSprites}/{Model.SpriteCount}\n" + Model.MediaDiagnostic;
                RefreshSample();
            }
            catch (ArgumentException error) { SetInvalid(error.Message); }
        }
        public void SetCandidate(DirectMeleeSpecialDefinition direct, CombatArena arena = null, MeleeFrameDefinition frame = null)
        {
            if (direct == null) { SetInvalid("유효한 direct numeric 정의가 없습니다."); return; }
            pendingAction = null; pendingFixed = null; pendingFrame = frame; pendingTracks = null; pendingCharged = null; lastCharged = null; pendingAuthored = null; lastAuthored = null; pendingDirect = direct; pendingArena = arena;
            LoadAssets(); var animations = injectedMedia ?? (arena ? arena.characterAnimations : null) ?? assetMedia;
            string version = direct.ContentHash + "/" + frame?.ContentHash;
            if (Model != null && lastDirect == version && lastMedia == animations) return;
            try
            {
                string previous = Model?.ActionId; Model = ActionGeometryPreviewModel.Compile(direct, animations, injectedResolver, frame); lastDirect = version; lastMedia = animations;
                time.highValue = Model.Action.End; time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, Model.Action.End)); time.SetEnabled(true); left.SetEnabled(true); direction.SetEnabled(false);
                if (previous != direct.Id) fitPending = true;
                state.text = $"{direct.Id} · {(frame == null ? "CombatTimeFixed" : "FrameTime")} direct 정책 · 접촉 {Model.Action.Impact:F5}s · 활성 [{Model.Action.Impact:F5}, {Model.Action.Impact + Model.Action.Active:F5})s · 종료 {Model.Action.End:F5}s\n수평 입력 잠금·전진 없음·기본 공속 미적용·Cue 없음";
                media.text = "요청 " + Model.RequestedBinding + " · 본체 바인딩 " + Model.BoundBinding + $" · 로드 {Model.LoadedSprites}/{Model.SpriteCount}\n" + Model.MediaDiagnostic;
                RefreshSample();
            }
            catch (ArgumentException error) { SetInvalid(error.Message); }
        }
        public void SetCandidate(ChargedMeleeDefinition charged, float releaseAt, CombatArena arena = null)
        {
            if (charged == null) { SetInvalid("유효한 charged numeric 정의가 없습니다."); return; }
            pendingAction = null; pendingFixed = null; pendingFrame = null; pendingTracks = null; pendingDirect = null; pendingAuthored = null; lastAuthored = null; pendingCharged = charged; pendingRelease = releaseAt; pendingArena = arena;
            LoadAssets(); var animations = injectedMedia ?? (arena ? arena.characterAnimations : null) ?? assetMedia;
            string key = charged.ContentHash + "/" + releaseAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (Model != null && lastCharged == key && lastMedia == animations) return;
            try
            {
                string previous = Model?.ActionId; Model = ActionGeometryPreviewModel.Compile(charged, releaseAt, animations, injectedResolver); lastCharged = key; lastMedia = animations;
                var release = Model.Release.Value; time.highValue = Model.End; time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, Model.End)); time.SetEnabled(true); left.SetEnabled(true); direction.SetEnabled(false);
                if (previous != charged.Id) fitPending = true;
                state.text = $"{charged.Id} · 기존 ChargeDefinition.Resolve 읽기 전용 · 해제 {release.ReleaseTime:F5}s · 피해 {release.Damage:F5} · 종료 {release.MotionEnd:F5}s\n실제 타격/자원/해제 실행 없이 형상·기존 매체만 샘플링합니다.";
                media.text = "요청 " + Model.RequestedBinding + " · 본체 바인딩 " + Model.BoundBinding + $" · 로드 {Model.LoadedSprites}/{Model.SpriteCount}\n" + Model.MediaDiagnostic; RefreshSample();
            }
            catch (ArgumentException error) { SetInvalid(error.Message); }
        }
        public void SetCandidate(AuthoredSkillDefinition authored, int selectedHit = 0, CombatArena arena = null)
        {
            if (authored == null) { SetInvalid("유효한 authored skill 정의가 없습니다."); return; }
            pendingAction = null; pendingFixed = null; pendingFrame = null; pendingTracks = null; pendingDirect = null; pendingCharged = null; lastCharged = null; pendingAuthored = authored; pendingHit = selectedHit; pendingArena = arena;
            LoadAssets(); var animations = injectedMedia ?? (arena ? arena.characterAnimations : null) ?? assetMedia; string key = authored.ContentHash + "/" + selectedHit;
            if (Model != null && lastAuthored == key && lastMedia == animations) return;
            try {
                string previous = Model?.ActionId; Model = ActionGeometryPreviewModel.Compile(authored, selectedHit, animations, injectedResolver); lastAuthored = key; lastMedia = animations;
                time.highValue = Model.End; time.SetValueWithoutNotify(Mathf.Clamp(time.value, 0, Model.End)); time.SetEnabled(true); left.SetEnabled(true); direction.SetEnabled(false); if (previous != authored.Id) fitPending = true;
                state.text = $"{authored.Id} · {Model.SelectedHitId} 읽기 전용 · 본체 {authored.Execution.End:F5}s · 독립 표본 끝 {Model.End:F5}s\n대상/순간이동/접근/지형·접지·발동 성공은 시뮬레이션하지 않습니다.";
                media.text = "요청 " + Model.RequestedBinding + " · 본체 바인딩 " + Model.BoundBinding + $" · 로드 {Model.LoadedSprites}/{Model.SpriteCount}\n" + Model.MediaDiagnostic; RefreshSample();
            }
            catch (ArgumentException error) { SetInvalid(error.Message); }
        }
        public void SetInvalid(string error)
        {
            Model = null; pendingAction = null; pendingFixed = null; pendingFrame = null; pendingTracks = null; pendingDirect = null; pendingCharged = null; pendingAuthored = null; pendingArena = null; InvalidateBinding();
            state.text = "무효 후보 — 형상 표시 중지: " + error + "\n마지막 유효 후보를 현재 편집값으로 표시하지 않습니다.";
            state.style.color = new Color(1, .55f, .4f);
            sample.text = ""; media.text = ""; time.SetEnabled(false); left.SetEnabled(false); direction.SetEnabled(false); canvas.MarkDirtyRepaint();
        }
        void RefreshSample()
        {
            if (Model == null) return;
            state.style.color = StyleKeyword.Null;
            var pose = CurrentSample;
            float distance = Model.AdvanceAt(Model.End, PreviewDirection);
            sample.text = $"t={pose.Seconds:F5}s · root=({pose.Root.x:F5}, {pose.Root.y:F5})unit · 지상 전진 {pose.Root.x:F5}/{distance:F5}unit" +
                $"\n몸/피격 size=({pose.Body.width:F5}, {pose.Body.height:F5})unit · 중심=({pose.Body.center.x:F5}, {pose.Body.center.y:F5})unit" +
                $"\n선택 판정 {(pose.HitActive ? "활성" : "비활성 (그리지 않음)")} · size=({pose.Hitbox.width:F5}, {pose.Hitbox.height:F5})unit · 프레임 {pose.FrameIndex + 1}/{Model.SpriteCount}: {Model.FrameReference(pose.FrameIndex)}" +
                (pose.Sprite ? $"\nSprite.rect={pose.Sprite.rect.width:R}×{pose.Sprite.rect.height:R}px · pivot=({pose.Sprite.pivot.x:R}, {pose.Sprite.pivot.y:R})px · PPU={pose.Sprite.pixelsPerUnit:R} · 실제 크기={pose.SpriteBounds.width:F5}×{pose.SpriteBounds.height:F5}unit" : "\n현재 바인딩 이미지 없음 — 형상만 표시 (실제 아트 미확인)");
            canvas.MarkDirtyRepaint();
        }
        void Draw()
        {
            var rect = new Rect(0, 0, canvas.contentRect.width, canvas.contentRect.height);
            if (rect.width <= 0 || rect.height <= 0) return;
            EditorGUI.DrawRect(rect, new Color(.075f, .085f, .11f));
            if (Model == null) { GUI.Label(new Rect(12, 12, rect.width - 24, 60), "유효한 후보 없음 · 이전 형상 숨김", EditorStyles.wordWrappedLabel); return; }
            if (fitPending) { Fit(rect); fitPending = false; }
            GUI.BeginClip(rect);
            try
            {
                var pose = CurrentSample; float scale = ActionGeometryPreviewModel.ReferencePixelsPerUnit * zoom.value;
                Vector2 Screen(Vector2 point) => new Vector2(rect.width / 2 + (point.x - viewCenter.x) * scale, rect.height / 2 - (point.y - viewCenter.y) * scale);
                Rect ScreenBox(Rect box)
                {
                    var topLeft = Screen(new Vector2(box.xMin, box.yMax)); return new Rect(topLeft, box.size * scale);
                }
                Handles.BeginGUI();
                float xMin = viewCenter.x - rect.width / (2 * scale), xMax = viewCenter.x + rect.width / (2 * scale);
                float yMin = viewCenter.y - rect.height / (2 * scale), yMax = viewCenter.y + rect.height / (2 * scale);
                for (int x = Mathf.CeilToInt(xMin); x <= Mathf.FloorToInt(xMax); x++)
                {
                    Handles.color = x == 0 ? new Color(.4f, .45f, .55f) : new Color(.16f, .19f, .24f);
                    Handles.DrawLine(Screen(new Vector2(x, yMin)), Screen(new Vector2(x, yMax)));
                    GUI.Label(new Rect(Screen(new Vector2(x, yMin)).x + 2, rect.height - 19, 45, 18), x.ToString(), EditorStyles.miniLabel);
                }
                for (int y = Mathf.CeilToInt(yMin); y <= Mathf.FloorToInt(yMax); y++)
                {
                    Handles.color = y == 0 ? new Color(.4f, .45f, .55f) : new Color(.16f, .19f, .24f);
                    Handles.DrawLine(Screen(new Vector2(xMin, y)), Screen(new Vector2(xMax, y)));
                    GUI.Label(new Rect(3, Screen(new Vector2(xMin, y)).y, 35, 18), y.ToString(), EditorStyles.miniLabel);
                }
                Handles.EndGUI();
                if (pose.Sprite) DrawSprite(pose, ScreenBox(pose.SpriteBounds), scale);
                Handles.BeginGUI();
                Handles.color = new Color(.25f, .6f, 1);
                var start = Screen(Vector2.zero); var end = Screen(new Vector2(Model.AdvanceAt(Model.End, PreviewDirection), 0));
                pathPoints[0] = start; pathPoints[1] = end; Handles.DrawAAPolyLine(3, pathPoints);
                Handles.DrawLine(end + Vector2.up * 5, end - Vector2.up * 5);
                if (pose.HitActive) DrawBox(ScreenBox(pose.Hitbox), new Color(1, .55f, .12f), false, .16f);
                if (body.value) DrawBox(ScreenBox(pose.Body), new Color(.2f, 1, .85f), false, .04f);
                if (hurtbox.value) DrawBox(ScreenBox(pose.Hurtbox), new Color(1, .4f, .8f), true, 0);
                Handles.color = Color.white; var root = Screen(pose.Root);
                Handles.DrawLine(root + Vector2.left * 6, root + Vector2.right * 6); Handles.DrawLine(root + Vector2.up * 6, root + Vector2.down * 6);
                Handles.EndGUI();
                GUI.Label(new Rect(12, 9, rect.width - 24, 22), $"{pose.Seconds:F5}s  ·  {zoom.value:F2}×  ·  1unit = {scale:F1}px  ·  {(pose.HitActive ? "판정 활성" : "판정 비활성")}", EditorStyles.whiteMiniLabel);
                if (!pose.Sprite) GUI.Label(new Rect(12, 30, rect.width - 24, 32), "미디어 없음 · 실제 아트가 아닌 형상 미리보기", EditorStyles.whiteMiniLabel);
            }
            finally { GUI.EndClip(); }
        }
        static void DrawSprite(ActionGeometrySample pose, Rect bounds, float scale)
        {
            var sprite = pose.Sprite;
            // A rectangular crop is placed relative to the original Sprite pivot,
            // never fitted to the fixed body or the transparent canvas dimensions.
            try
            {
                var tex = sprite.textureRect; var offset = sprite.textureRectOffset; float ppu = sprite.pixelsPerUnit;
                float x = pose.Facing > 0 ? offset.x - sprite.pivot.x : sprite.pivot.x - offset.x - tex.width;
                var local = new Rect(pose.Root.x + x / ppu, pose.Root.y + (offset.y - sprite.pivot.y) / ppu, tex.width / ppu, tex.height / ppu);
                var uv = new Rect(tex.x / sprite.texture.width, tex.y / sprite.texture.height, tex.width / sprite.texture.width, tex.height / sprite.texture.height);
                bool flipX = pose.Facing < 0;
                if (sprite.packed)
                {
                    flipX ^= sprite.packingRotation == SpritePackingRotation.FlipHorizontal || sprite.packingRotation == SpritePackingRotation.Rotate180;
                    if (sprite.packingRotation == SpritePackingRotation.FlipVertical || sprite.packingRotation == SpritePackingRotation.Rotate180) { uv.y += uv.height; uv.height = -uv.height; }
                }
                if (flipX) { uv.x += uv.width; uv.width = -uv.width; }
                var crop = new Rect(bounds.x + (local.xMin - pose.SpriteBounds.xMin) * scale,
                    bounds.y + (pose.SpriteBounds.yMax - local.yMax) * scale, local.width * scale, local.height * scale);
                GUI.DrawTextureWithTexCoords(crop, sprite.texture, uv, true);
            }
            catch (UnityException)
            {
                GUI.Label(new Rect(12, 50, 420, 32), "패킹된 비직사각 Sprite는 이 뷰에서 이미지 표시 불가 · 형상만 표시", EditorStyles.whiteMiniLabel);
            }
        }
        void DrawBox(Rect box, Color color, bool dashed, float fill)
        {
            if (fill > 0) { var shade = color; shade.a = fill; EditorGUI.DrawRect(box, shade); }
            Handles.color = color;
            Vector3 a = new Vector2(box.xMin, box.yMin), b = new Vector2(box.xMax, box.yMin), c = new Vector2(box.xMax, box.yMax), d = new Vector2(box.xMin, box.yMax);
            if (dashed) { Handles.DrawDottedLine(a, b, 4); Handles.DrawDottedLine(b, c, 4); Handles.DrawDottedLine(c, d, 4); Handles.DrawDottedLine(d, a, 4); }
            else
            {
                boxPoints[0] = a; boxPoints[1] = b; boxPoints[2] = c; boxPoints[3] = d; boxPoints[4] = a;
                Handles.DrawAAPolyLine(2, boxPoints);
            }
        }
        void Fit(Rect rect)
        {
            var initial = Model.Sample(0, left.value ? -1 : 1, InputDirection);
            var final = Model.Sample(Model.End, left.value ? -1 : 1, InputDirection);
            var bounds = Union(Union(initial.Body, final.Body), Union(initial.Hitbox, final.Hitbox));
            if (Model.Motion != null)
                for (int i = 0; i < Model.Motion.Frames.Count; i++)
                {
                    var sprite = Model.BoundSprite(i);
                    if (sprite) bounds = Union(bounds, Union(ActionGeometryPreviewModel.SpriteRect(sprite, initial.Root, initial.Facing), ActionGeometryPreviewModel.SpriteRect(sprite, final.Root, initial.Facing)));
                }
            bounds = Union(bounds, new Rect(-.1f, -.1f, .2f, .2f)); viewCenter = bounds.center;
            float scale = Mathf.Min((rect.width - 72) / Mathf.Max(1, bounds.width), (rect.height - 58) / Mathf.Max(1, bounds.height));
            zoom.SetValueWithoutNotify(Mathf.Clamp(scale / ActionGeometryPreviewModel.ReferencePixelsPerUnit, .25f, 6));
        }
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }
}
