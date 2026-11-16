# 2스테이지 몬스터 디자인 비교

신호투척수 반환 걷기는 `outputs/waterway/thrower-walk-candidates-20261101/index.html`에서 A~D를 비교한다. `data/presentation/stage2_thrower_walk_candidates.json`과 `thrower_walk_candidates.py`가100개 셀을 무변형 분리하고 초반18장·중반17장·후반17장을 각각0.9초로 비교한다. D는 C와 같은17장을0.72초로 빠르게 재생한다. 기존 실제12색·384×224·공통 배율/오프셋을 사용하고 원본에 색이 있는 눈·소품 내부를 대조 복원하며 작은 잡점·빈 픽셀을 정리한다. 원본의 손과 다리 동작 전체를 유지하고, 추천은 C이며 사용자 선택은 미정이다.

다음은 신호투척수 걷기 입력이다. `outputs/waterway/walk-retry-white-20261101/R-selected/R-selected-white-input-4x.png`와 같은 폴더의 `prompt-white.txt`를 함께 사용한다. 현재 `stage2_walk_retry.json`과 `walk_retry_white.py`가 기존98×106px·384×224·실제12색을 유지한 순백1536×896 입력을 만든다. 약0.9초20자세의 짧은 보행을 요청하며 소품은 화면 오른쪽 뒤 골반의 원래 손·전완에 고정하고 양쪽 허벅지·무릎·부츠 전체를 보이도록 명시한다. M3 빠른 A540ms는 사용자 채택 상태다.

먹침필경사는 사용자가 A 동작을 선택했으며 현재 빠른 재생은 `outputs/waterway/m3-A-fast-20261101/index.html`에서 검토한다. `data/presentation/stage2_m3_fast_a.json`과 `m3_fast_a.py`가 A18장의 PNG·색·크기·순서를 무변형 복사하고 장당30ms·총540ms로 약1.48배 빠르게 재생한다. 기존800ms와 대화형 동시 비교하며 이540ms 속도도 사용자가 채택했다. 실제 게임 이동속도·공격·Unity는 변경하지 않는다.

먹침필경사 반환 걷기는 `outputs/waterway/m3-walk-candidates-20261101/index.html`에서 A~D를 비교한다. `data/presentation/stage2_m3_walk_candidates.json`과 `m3_walk_candidates.py`가100개 셀을 무변형 분리하고 초반18장·중반17장·후반15장을 각각0.8초로 재생한다. D는 A와 같은18장을1초로 늦춘 속도 비교다. 모든 후보에 기존9색·384×224·공통 배율/오프셋을 사용하고 작은 빈 픽셀과 침의1~2px 행 내부 끊김을 정리한다. 사용자 선택은 A이며 현재 재생 속도 보완은 위 빠른 A를 따른다. 이전 K4/B3 채택본은 유지한다.

다음은 먹침필경사다. `outputs/waterway/walk-retry-white-20261101/M3/M3-white-input-4x.png`와 같은 폴더의 `prompt-white.txt`를 함께 사용한다. 당시 입력 규격은 같은 폴더 metadata에 보존한다. 기존 몸체198×59px·논리384×224·기존 팔레트를 유지한 흰 배경4배 PNG이며, 빠르고 낮은 교차보행·긴 침 강체 유지·약0.8초20자세를 요청한다. 반환 걷기 후보는 위 A~D에서 검토한다.

쐐기보행기 반환 걷기는 `outputs/waterway/b3-walk-return-20261031/index.html`에서 검토한다. `data/presentation/stage2_b3_walk_return.json`과 `b3_walk_return.py`로 반환 시트100장을 무변형 분리하고 원본41~59번의 연속19장을 선택한다. 기존10색·384×224 캔버스·기본본 높이100px 기준 공통 배율에 맞춰 픽셀화하며 작은 내부 빈 픽셀을 보정한다. 1.3초·68/69ms는 시간 정보가 없는 PNG에 대한 검토 제안이다. 원본·선정 셀·투명 PNG·정수4배·마지막/처음 비교·맵 합성과 ZIP을 제공한다. 원본의 쐐기·뒤 관절 형태 변화는 검토 대상으로 남으며 사용자가 이19장을 쐐기보행기 걷기로 채택했으며 Unity 통합은 별도다.

등짐분사수 걷기는 사용자가 `outputs/waterway/k4-walk-D-pixel-clean-20261031/native`의 D 보정20장으로 채택했다. `data/presentation/stage2_k4_d_pixel_cleanup.json`과 `k4_d_pixel_cleanup.py`가 작은 내부 구멍·발목 틈과 제한적인 외곽 홈·색점·반복 연결부 뚜껑을 기존11색으로 보정한다. 20장 순서·1초 타이밍·크기를 유지하며 Unity 통합은 별도다.

쐐기보행기 입력은 `outputs/waterway/walk-retry-white-20261031/B3/B3-white-input-4x.png`와 같은 폴더의 `prompt-white.txt`다. `data/presentation/stage2_walk_retry.json`과 `walk_retry_white.py`가 기본본의 픽셀·10색·몸체180×100px을 유지한 흰 배경1536×896 PNG를 준비한다. 24자세·약1.3초는 입력 프롬프트의 제작 제안이며 반환 시트의 실제 선정은 위19장을 따른다. references에는 기본본과 팔레트를 무변형 보존한다.

사용자 선택 D의 픽셀 변환 입구는 `outputs/waterway/k4-walk-D-pixel-20261031/index.html`이다. `data/presentation/stage2_k4_d_pixel_walk.json`과 `k4_d_pixel_walk.py`가 선택한20장·30/55ms·총1초를 유지하며 기본본의 실제11색·384×224·몸 높이112px 기준으로 변환한다. 같은 배율과 위치 기준, 흰 배경 혼합 제거와16점 재질 투표를 사용한다. 투명 RGBA·indexed·흰 비교·정수4배·맵1배 합성과ZIP을 제공하며 픽셀 변환본 승인은 별도다.

등짐분사수 반복 접합의 사용자 선택 화면은 `outputs/waterway/k4-loop-candidates-20261031/index.html`이다. `data/presentation/stage2_k4_loop_candidates.json`과 `k4_loop_candidates.py`가 A/B 각20장·C39장·D20장(시간 변경)을 같은 공통 크롭으로 비교한다. 동시 시작·정지·0.5배속·접합부6장만 보기·개별 키 선택을 지원한다. 원본 PNG와 색·몸체는 그대로 보존하며 사용자가 D를 선택했으며 새 픽셀 변환본을 검토한다.

현재 등짐분사수 걷기 검토 입구는 `outputs/waterway/k4-walk-smooth21-20261031/index.html`이다. `data/presentation/stage2_k4_walk_smooth.json`과 `k4_walk_smooth.py`로 원본0기준7~27의 연속21장을 무변형 복사한다. 중간 자세를 복구하고 루프 경계가 가까운 구간을 사용한다. 50ms·20fps·총1.05초는 검토 제안이며 기존8장과 같은 크롭으로 비교한다. 원본·이전8장과 다른 몬스터는 변경하지 않는다.

새 등짐분사수 걷기 추출 입구는 `outputs/waterway/k4-walk-retry-frames-20261031/index.html`이다. `data/presentation/stage2_k4_retry_frames.json`과 `k4_retry_frames.py`로 흰 배경10×10 PNG 시트를100개 셀로 무변형 분리하고 선정8장만 재생·표시한다. 개별 PNG는1280×720 원본이며 재채색·배경 제거·파츠 합성·논리 픽셀 축소는 하지 않는다. 원본·전체 셀은 보존하며1초 루프는 PNG에 없는 검토용 시간 제안이다.

걷기는 한 마리씩 재시도하며 이전 전체 선정은 보류한다. 첫 등짐분사수 입력 `outputs/waterway/walk-retry-white-20261031/K4/K4-white-input-4x.png`와 당시 프롬프트·규격은 그대로 보존한다. 현재 입력 규격은 위 쐐기보행기를 가리킨다.

보류한 걷기 선정 입구는 `outputs/waterway/selected-walk-keys-20261031/index.html`이다. `data/presentation/stage2_selected_walk_keys.json`과 `selected_walk_keys.py`가 분사수·보행기·필경사8장씩, 수의6장만 제공한다. 원본 긴 영상을 재생하지 않고 선정 키로만 루프를 만든다. 투척수는 이전 합성의 허벅지 접합 손상으로 보류하고 선택 디자인 유지·걷기 재제작을 권장한다. 선정30장은 제한적인 외곽·내부 잡점 정리를 거치며 개체별 기본 팔레트만 사용한다. 선정과 사용자 최종 승인은 구분한다.

걷기 픽셀 보정 입구는 `outputs/waterway/walk-retouch-v2-20261031/index.html`이다. `data/presentation/stage2_walk_retouch.json`과 `walk_retouch.py`가 개체별 선택 기본본의 실제 색만 사용하고 작은 밝은 외곽 잡점을 정리한다. 투척수는 기본본 상체·든 팔·손·소품을 강체로 복원하며 반환된 다리 보행을 유지한다. 이전 원본·추출·정리본은 보존한다. 수정 전후 재생·개별 프레임·4배·ZIP은 아트 검토용이다.

받은 걷기 검토 입구는 `outputs/waterway/walk-review-20261031/index.html`이다. `data/presentation/stage2_walk_review.json`과 `walk_review.py`가 받은4종 GIF5개(보행기2버전)의 전체372장 원본 추출과 별도384×224 픽셀 정리본을 만든다. 원본ZIP·GIF·PNG를 보존하고 첫 입력 정지는 반복 검토에서만 제외한다. 개체별 고정 배율과 기존 팔레트를 사용하며 프레임마다 늘이거나 잘린 그림을 복원하지 않는다. 수의는 기존8키 픽셀 부유를 사용한다. 전체 프레임·8키·슬라이더·이동 합성·4배·ZIP을 제공하며 승인·Unity 적용 전 검토본이다.

잡몹5종의 걷기 전달 입구는 `outputs/waterway/normal-walk-handoff-20261031/index.html`이다. `data/presentation/stage2_normal_walk_handoff.json`이 원본 경로와 개체별 프롬프트를 소유하며 `python tools/stage2_monster_concepts/walk_handoff.py`로 투명1536×896 입력 PNG·원래384×224 PNG·팔레트·TXT·ZIP을 묶는다. 지상4종은 보행, 수의는 부유 이동이다. 기존 기본본을 변형하지 않으며8프레임·주기는 제작 제안이다. 반환된 이동 원본은 별도 걷기 검토 입구에서 확인하며 입력과 프롬프트를 보존한다.

현재 대기 입구는 `outputs/waterway/body-idles-8-v2-20261031/index.html`이다.8명의 상체와 장비가 함께 호흡하며 무릎·지지대에서 변위를 줄여 발은 고정한다. 수의만 전체가3px 부유한다. `data/presentation/stage2_body_idles.json`과 `body_idles.py`가 기존 국소 모션을 바탕으로 정수 행 이동·팔레트·고정 영역·강체 영역·복호화 일치를 검사한다. 이전/보정 동시 비교,8키 PNG,4배/8배 확대와 투명 모션을 제공한다. 기본본·이전 국소 대기는 보존하며 공격 패턴과 전투 구현은 변경하지 않는다.

현재 제작 입구는 `outputs/waterway/pixel-base-step1-20261031/index.html`이다. 사용자 요청의1단계로8명의 정지 픽셀 기본본만 제공한다. 분사수·남매의 사선 자세, 기존 크기, 제한 팔레트, 고정 캔버스1배/4배/8배 PNG를 사용한다. `data/presentation/stage2_pixel_base.json`이 규격 원본이며 `pixel_base_prepare.py`와 `pixel_base_build.py`가 입력 보존·픽셀 변환·전달을 맡는다. 대기는 기본본의 일부 픽셀을 움직이는 다음 단계다. 기존 생성 루프는 제작 기준에서 제외하고 원본만 보존한다. 자세한 방법은 [전달 기준](../../docs/development/presentation/visuals/stage2-monster-idle-study.md)을 따른다.

정지 대기 참고 입구는 `outputs/waterway/idle-reference-8-20261031/index.html`이다. 일반5·엘리트1·분리된 남매2의 정측면 기준 자세를 각1장 제공한다. `idle_references.py prepare`로 입력·프롬프트를 보존하고 기본 image_gen에서 개별 편집 후 `idle_references.py build`로 실제 크기·4배/8배 투명 PNG·384×224 고정 캔버스·종합판·ZIP을 만든다. 이동 프레임과 대기 루프는 사용자의 후속 제작 범위다. 투척수의106px는 기존154px 중 머리 위 물건을 내린 결과이며 몸을 키우지 않는다. 남매 개인 높이102/127px는 분리 작업값이다. 개별 파생본은 시안 상태이며 기존 아트와 사용자 크기 JSON을 보존한다.

현재 역할은 **근거리 K4 등짐분사수·B3 쐐기보행기·M3 먹침필경사 / 원거리 H3 늘어진수의·신호투척수 / 엘리트 A3**다. 공격 핵심은 사용자 지정이며 세부 보정·전조는 [기획 문서](../../docs/design/enemies/chapter2/normal/stage2-waterway-monsters.md)를 따른다. 원본 `data/combat/stage2_waterway_patterns.json`에서 `python tools/stage2_monster_concepts/build_combat_plan.py`로 문서와 `outputs/waterway/user-sized-redraw-20261031/combat-plan.html`을 생성한다. 남매 보스는 전방 석궁·후방 원거리 잉크술의 교대 방향이며 체력 공유와 세부 밸런스는 검토안이다. 시간은 시험 후보값이며 Unity 수치로 내보내지 않는다. 기존 그림·크기 JSON·생성 프롬프트 원본은 보존하고 현재 검토 화면과 조절기 역할 표시만 갱신한다. 과거 출력물과 프롬프트의 역할 표기는 제작 당시 기록이며 현재 역할 원본이 아니다.

현재 사용자 크기 기준 재생성 입구는 `outputs/waterway/user-sized-redraw-20261031/index.html`이다. 입력 원본은 사용자가 조절기에서 내보낸 `user-size-reference.json`이며 H3 151px, B3 100px, M3 59px, 신호투척수 154px, K4 112px, A3 155px, I4 127px의 **소품을 포함한 전체 유효 높이**를 적용한다. 이 값은 사용자 크기 검토값이며 제작 규격·전투 수치 확정과 구분한다.

`user_sized_redraw.py`가 이전 화풍 그림·승인 플레이어·배경·JSON과 7개 편집 프롬프트를 보존한다. 연결된 기본 image_gen으로 채택 형태·역할·개체별 색과 테두리 없는 표현을 유지해 개별 재생성한다. `user_sized_review.py`는 RGB 재채색 없이 연결 알파 핵심 영역을 추출하고 비율 유지 최근접 변환으로 요청한 유효 높이가 정확히 일치하는지 확인한다. 이전 그림을 같은 높이로 확대한 비교, 모두 같은 1배율 줄맞춤, 개체별 배경 합성 14장과 저장 장면 재현 1장, 시인성 표·HTML·ZIP을 제공한다.

새 `size-editor.html`은 JSON의 D 장면·A3 선택·표시 상태·좌표를 초기값으로 읽고 별도 저장 공간을 사용한다. 다른 개체의 `visible:false`도 높이는 모두 재생성에 반영한다. 개별 합성은 바닥을 맞춘 비교용 배치이며 JSON 위치를 확정 맵 배치로 취급하지 않는다. 원래 조절기·기존 그림·사용자 JSON은 보존한다.

`outputs/waterway/style-unified-20261031/size-editor.html`은 7종의 높이(px)·비율 유지 너비·드래그 위치를 직접 조절하는 도구다. `size_editor.py`가 배경과 몬스터를 포함한 단일 HTML을 만들며 별도 서버나 이미지 요청 없이 사용할 수 있다. 플레이어 유효 높이 64px와 화면 960×540은 고정이다. 표시 개체·배경·이전/조정 그림을 바꾸고, 초기화·브라우저 자동 저장·JSON 저장/불러오기·선택틀 없는 합성 PNG 저장을 지원한다. `style_review.py` 재빌드에도 조절 화면과 입구를 함께 만든다. JSON의 높이는 소품을 포함한 전체 그림 높이다.

현재 화풍·색 조정의 입구는 `outputs/waterway/style-unified-20261031/index.html`이다. `style_unification.py`가 채택 7건의 무변형 원본·편집 입력·프롬프트를 준비하고 연결된 기본 image_gen으로 개체별 화풍 편집을 수행한다. `style_review.py`는 수정 전후, 실제 크기 줄맞춤, 960×540 개별·역할별 합성 22장, 어두운 벽·푸른 원경·검정 근경 비교와 ZIP을 만든다. 기존 디자인 원본과 역할은 유지하며 조정본은 검토용 파생본이다. 애니메이션·스프라이트 시트·Unity 적용과 최종 양산 픽셀 정리는 포함하지 않는다.

화풍 기준은 외곽선 없는 재질색 면, 2~3단계의 큰 명암 면, 회청색 계열의 공통 그림자와 자주빛 먹 내부 덩어리다. 회백·뼈색·자주·주황·구리·청색 등 개체별 주요 색은 유지한다. 손발과 발사부는 배경 위에서 따로 검토하며 작은 눈만 밝히는 방식으로 해결하지 않는다. R의 작은 명암을 정리하고 K4의 호스 끝은 중간 명도 금속색으로 조정한다. B3는 원본 안쪽 다리를 포함한 다섯 접점을 유지한 최초 편집본을 사용하며, 다리를 잘못 제거한 재편집은 제외한다. I4는 기존 연결체 형태를 유지한다.

일반 몬스터의 비교 크기 상한은 기존 채택값을 유지한다. A3는 144×112, I4는 216×128 상한의 별도 크기 검토이며 최종 제작 규격이 아니다. 원본의 해시·배경 참조 일치, 삽입 밖 배경과 플레이어 일치, 근경 가림과 링크를 검사한다. 편집 생성의 외곽은 픽셀 단위로 원본과 동일하다고 주장하지 않으며 정규화 실루엣 중첩은 검토 보조값으로 남긴다.

현재 채택 정리의 입구는 `outputs/waterway/adopted-monsters-v2-20261031/index.html`이다. `current-choices.json`에 현재 선택을 기록하며 `adopted_summary.py`는 이를 읽어 기존 그림 7건의 모음 보드·선택표·원본·프롬프트 ZIP을 만든다. 탐색 도구의 재실행은 현재 사용자 선택을 덮어쓰지 않으며 K/L/M 비교는 생성 당시 선택 스냅샷을 사용한다.

K/L/M 탐색 자료의 입구는 `outputs/waterway/final-slots-candidates-20261031/index.html`이다. `final_slots_catalog.py`는 K/L/M 각 근거리 2·원거리 2, 총 12개의 생성 당시 후보와 참조·프롬프트를 준비한다. 현재 이 중 M3와 K4는 채택됐으며 나머지 10종은 미채택이다. 신규 생성에는 승인 플레이어의 픽셀 밀도와 배경만 입력하고, 채택 5건은 제외할 체형을 문자로 지정한다. 굽은 몸·초승달·갈비판·등짐 호스·뿌리·긴 전단팔·부채·집게·회전 고리·빈 가슴·낮은 좌식·엇갈린 판몸을 비교한다. 새 엘리트·보스는 생성하지 않는다.

`final_slots_review.py`는 원본 보존, RGB를 바꾸지 않는 연결 실루엣 분리, 5배 색 보드·동일 알파 단색 보드, 채택 일반 몬스터와의 줄맞춤, 960×540 합성 6장, 벽·원경·검정 근경 비교 및 ZIP을 만든다. 채택 5건 원본·분리 그림과 배경 참조의 해시 일치, 플레이어 유효 영역 66×64, 삽입 밖 배경·플레이어 일치, 원본 근경 가림 순서를 검사한다. 빌드 도구는 현재 선택 파일을 쓰지 않으며 신규 후보를 자동 채택하지 않는다.

**채택: H3 늘어진수의는 근거리, B3 쐐기보행기는 큰 근거리, 신호투척수는 원거리, A3 편측거구는 엘리트, I4는 수로를 지키는 남매 2인조 보스 콘셉트(가칭)다.** H3의 이전 원거리 설명은 현재 역할에서 제외하며 근접 공격 방식은 미정이다. I4의 현재 그림은 연결된 두 인물의 참고 원형이다. 독립된 남매 두 명의 디자인·무기 분담·최종 크기·이름·패턴은 아직 제작하거나 확정하지 않았다. 기존 원본은 변경하지 않는다.

**M3 먹침필경사와 K4 등짐분사수도 디자인 채택 상태다.** 각각 첫 첨부 M 보드의 세 번째, 두 번째 첨부 K 보드의 네 번째 개체다. M3는 사용자 지정에 따라 근거리·찌르기 역할로 채택했으며, 낮은 좌식 몸과 긴 주둥이판을 쓰는 근접 공격 방향이다. 기존 먹침 직사 설명은 현재 역할에서 제외한다. K4는 세로 등짐통과 짧은 호스의 원거리 분사형이다. 원본 그림은 유지하며 공격 세부와 수치는 미정이다.

일반 근거리는 H3·B3·M3 3종, 원거리는 신호투척수·K4 2종으로 사용자 지정 구성을 충족한다. 엘리트 A3 1종과 남매 보스 I4 콘셉트 1건을 포함해 채택 7건이다. 역할 구성 선택은 완료했으며, 최종 크기·공격 세부·양산 및 구현 완료와는 구분한다. 조형 탐색 A/B/C의 다른 10종과 D~G 16종은 탈락이며, H/I/J의 미채택 10종과 K/L/M의 나머지 10종은 후보로 유지한다. 디자인 채택은 모션·최종 크기·전투 수치·Unity 적용과 구분한다.

`topology_reset.py prepare/build`는 신규 일반 근거리 6종·원거리 대안 6종을 H/I/J 보드로 비교한다. 납작한 바닥형·옷과 무기를 든 인물·세로 부유형·낮은 삼각 기계·속이 빈 사슬·방패와 창·늘어진 막과 낭·쌍두 연결체·연속 S몸·단일 다리·부유 가면·장대 인물처럼 **전체 몸의 구조, 접점 수, 빈 공간, 가로세로 비율부터 다르게 잡는다.** 같은 굵은 어깨·짧은 다리·큰 주먹 몸체에 장비를 교체하는 접근을 피한다. 옷과 휴대 무기를 허용한다. 새 엘리트·보스는 생성하지 않는다.

형태 연구에는 [스컬 공식 스크린샷](https://store.steampowered.com/app/1147560/Skul_The_Hero_Slayer/)과 [공식 개발 블로그](https://steamcommunity.com/games/1147560/announcements/detail/2805137023068596466)를 사용한다. 참고 자료의 플레이어 로스터와 적 이미지는 구분한다. 외부 캐릭터·의상·테두리를 복제하지 않으며, 생성 입력은 승인 플레이어의 묘사 밀도와 기존 지하수로 배경이다. 보존 A3/B3를 신규 몸체 템플릿으로 넣지 않는다. 실제 참고 이미지와 출처는 결과 폴더 `references/research/`에 보존한다.

색 보드와 동일한 알파·배율의 단색 실루엣 보드, 플레이어 유효 영역 66×64와의 크기 비교, 960×540 배경 합성, 벽·푸른 원경·검정 근경 검토를 함께 제공한다. H/J의 명도·외곽 보정은 기본 image_gen 편집으로 수행하고 최초 원본·편집 원본·정확한 편집 프롬프트를 `raw/`에 모두 남긴다. `topology-reset-20261031`은 생성 당시 역할의 탐색 자료이며, 현재 채택 역할은 위 정리와 `current-choices.json`을 따른다.

`next_forms.py prepare/build`는 보존한 세 개체의 원본·사본·해시와 역할을 유지하고, 신규 일반 근거리 8종·원거리 대안 8종을 D/E/F/G 보드로 비교한다. 각 보드는 윗줄 접근형·기본 근거리, 아랫줄 원거리 대안 2종이며 검토 크기의 5배 확대다. 보존 A3의 높이 112px 예시는 역할별 크기 비교용이며 제작 규격 확정이 아니다. 새 엘리트·보스는 생성하지 않는다. 보존 선택을 새 변형으로 덮어쓰지 않고 원본·참조·프롬프트·합성·ZIP을 결과 폴더에 남긴다.

**동물·일상 사물의 변형에 한정하지 않고 사람형·기계형·불안정한 잉크 신체를 폭넓게 섞어 체형·자세·관절·공격 부위로 캐릭터를 만든다.** 잉크 도시 아래 관리되지 않은 수로의 공간감은 유지한다. 사람형과 기계형을 허용하며, 시안 묶음을 새 세력이나 직업 설정으로 확정하지 않는다.

**몬스터 그림에는 외곽선이 없다.** 검정 테두리뿐 아니라 진한 재질색으로 실루엣을 따라 두르는 1픽셀 테두리, 밝은 림과 베벨 테두리도 사용하지 않는다. 재질의 색 면이 투명 배경과 직접 닿고, 제한된 큰 명암 면으로 형태를 잡는다. 내부 관절의 잉크 덩어리와 내부 구조선은 외곽 테두리와 구분한다. 넓은 밝은 몸체 면을 남겨 실제 배경과 구분한다.

`form_exploration.py prepare/build`는 접근형·기본형·압박형·원거리를 A/B/C 각 4종, 신규 일반 후보 총 12종으로 비교한다. 신규 그림은 승인 플레이어와 실제 배경만 참고하며 기존 채택 원거리의 테두리를 스타일 참고로 가져오지 않는다. 메인 보드는 신규 4종을 2×2로, 검토 크기의 5배로 표시한다. 원본·참조·프롬프트·합성·ZIP을 결과 폴더에 보존한다. 채택 원거리 원본은 별도 참고와 실제 크기·배경 합성에서 유지하고, 엘리트·보스는 후속 단계로 남긴다.

`ink_contamination.py prepare/build`는 일반 몬스터 합성 도구를 사용해 이 방향의 신규 후보 12종과 비교 자료를 만든다. A 퇴적물·잔해 / B 뿌리·균사 / C 수로 생물은 같은 공간의 소재를 나눠 본 미채택 디자인 시안이다. 원본·참조·프롬프트·합성·ZIP을 결과 폴더에 보존한다. 엘리트·보스는 후속 단계로 남긴다.

`selected-ranged.json`의 **신호투척수**는 사용자가 첨부한 원본 보드의 5번째 개체이며 원거리 디자인 채택 상태다. 주황 앞치마·크림색 머리 덮개·보라 팔·황토색 신호통을 유지한다. 이번 오염 시안에서도 채택 그림을 변경하지 않고 동일 사본을 비교 기준으로 사용한다. 디자인 채택은 모션·스프라이트 시트·Unity 연결·공격 수치 확정과 구분한다.

`normal_round2.py prepare`는 채택 원본과 정확한 직사각형 사본을 보존하고, 합성용 실루엣 사본·참조·프롬프트를 준비한다. 기존 배경 폴더의 브랜치를 전환하지 않고 이 몬스터 전용 worktree에서 실행한다. 이미지 생성은 연결된 기본 image_gen으로 A/B/C 각 근거리 3종·직사 원거리 1종을 요청하며, 채택 신호투척수는 생성 대상에서 제외한다. `normal_round2.py build`는 신규 12종과 채택 원거리의 동일 사본을 조합해 일반 몬스터 보드 3장, 실제 크기 줄맞춤, 960×540 근거리·원거리 합성 6장 및 시인성 비교를 만든다. 채택 원거리 원본의 RGB와 출처·해시·프롬프트를 보존하며 엘리트·보스는 현재 제작 범위에 포함하지 않는다.

미채택 정지 디자인 시안 3세트, 총 21종을 위한 보존 프롬프트·소재 목록·합성 도구다. 배경 기준은 `outputs/waterway/parallax-kit-v3-20261031/`이며 결과 입구는 `outputs/waterway/monster-concepts-v3-20261031/index.html`이다. 그림은 저장소의 이미지 제외 정책에 따라 결과 ZIP에 포함한다.

`catalog.py`가 디자인 설명·생성 지시·검토용 크기 상한의 편집 원본이다. 사물·식물·동물을 세트별 비중으로 혼합하고 개체마다 소재에 맞는 색을 사용한다. 역할은 근거리 3·원거리 2·엘리트 1·보스 1이다. 선택과 승인, 애니메이션과 전투 수치, 최종 픽셀 정리는 별도다.

`python tools/stage2_monster_concepts/prepare.py`로 참조 이미지의 무변형 사본·해시·세트별 프롬프트를 만든다. 이 도구는 네트워크나 이미지 서버를 호출하지 않는다. 이미지는 대화에 연결된 기본 image_gen 도구로 생성하고 결과를 각 세트 `raw/generated.png`로 복사한다. 원본 경로는 `raw/source.json`에 보존한다. sub2api 서버를 호출하거나 기존 환경변수를 바꾸지 않는다.

`python tools/stage2_monster_concepts/build_review.py`는 생성 원본을 수정하지 않고 알파 임계값 180에서 분리된 7개 개체를 추출한다. 사본의 알파만 정리하며 RGB를 재채색하지 않는다. 비율 유지·최근접 축소로 실제 크기 합성, 전 종 줄맞춤, 벽·원경·검정 근경 비교와 HTML·ZIP을 만든다. Pillow·NumPy·SciPy가 필요하다.

승인 플레이어는 192×160 투명 캔버스 안 유효 영역 66×64다. 미리보기는 논리 화면 960×540이며 기존 배경 PNG의 밝기·색·선명도를 바꾸지 않는다. 원본 근경의 알파를 합성 마스크에서 제외해 가림 순서를 지킨다. 원본 플레이어 영역과 삽입 영역 밖 배경 픽셀이 일치하는지 검사한다. 결과와 제한은 Git 제외 결과 폴더의 `validation.json`에 둔다.

생성 결과는 디자인 시안과 합성 미리보기다. 실제 게임 캡처, 최종 양산 픽셀, 애니메이션·스프라이트 시트·Unity 적용으로 표현하지 않는다. 세트별 `previous-directions/`에는 수정 전 생성 원본과 당시 프롬프트를 보존한다.

## 일반 5종 이동 채택본 전체 검토

`python tools/stage2_monster_concepts/normal_locomotion_approved.py`는 `data/presentation/stage2_normal_locomotion_approved.json`을 읽어 채택본90개 native/indexed/4배 PNG를 무변형 복사하고 전체·개별·원본 맵 비교와 ZIP을 만든다. 출력 입구는 `outputs/waterway/normal-locomotion-approved-20261101/index.html`이다. H3는 B16장/1600ms 채택이며 각 개체의 원래 크기·시간·색을 유지한다. Unity 및 전투는 변경하지 않는다.

## 일반 몬스터 공격 입력

`python tools/stage2_monster_concepts/normal_attack_handoff.py`는 `data/presentation/stage2_normal_attack_handoff.json`을 읽어 첫 등짐분사수의 기존 픽셀 기본본을 무변형 흰 배경1536×896 입력으로 준비하고, 공격 소개·단계·영문 프롬프트·출처·ZIP과 검토 페이지를 만든다. 현재 공격 생성이나 채택은 없고 사용자 반환을 기다린다.

## 등짐분사수 반환 공격 · 분사 유지

`python tools/stage2_monster_concepts/k4_attack_return.py`는 `data/presentation/stage2_k4_attack_return.json`에 따라 원본36셀을 보존하고24개 선정 논리 프레임을 기존11색/같은 크기로 변환한다. 전조550ms·단일 전체 프레임 분사 유지650ms·회복700ms이며 분사 중 노즐과 발은 고정이다. 검토 페이지는 `outputs/waterway/k4-attack-hold-20261101/index.html`이다. 실제 잉크 효과·전투/Unity 연결은 없다.

## 등짐분사수 공격 프레임 축소 비교

`python tools/stage2_monster_concepts/k4_attack_reduced.py`는 `data/presentation/stage2_k4_attack_reduced.json`에 따라 기존 픽셀24장 중 A19/B15/C11장을 무변형 복사한다. 전조550ms·분사 고정650ms·회복700ms를 유지하며 `outputs/waterway/k4-attack-reduced-20261101/index.html`에서 이전24장과 동기 비교한다. 사용자가 A19장을 채택했다.

## 쐐기보행기 공격 입력

`python tools/stage2_monster_concepts/normal_attack_handoff.py --spec data/presentation/stage2_b3_attack_handoff.json`은 기존B3 픽셀 기본본을10색·180×100px 그대로 흰 배경 전체1536×896 입력과 프롬프트로 준비한다. 출력은 `outputs/waterway/normal-attack-handoff-20261101/B3/index.html`이다. 전조/제자리돌진/앞몸들기/찍기/회복의2.45초는 검토값이며 공격 생성·전투/Unity 연결은 아직 없다. 기존K4 규격과출력은 보존한다.

## 쐐기보행기 큰 들기 재요청

`python tools/stage2_monster_concepts/normal_attack_handoff.py --spec data/presentation/stage2_b3_high_lift_retry.json`은 크게 앞몸 세우기/찍기/회복1.5초 입력을 준비한다. `outputs/waterway/b3-high-lift-retry-20261101/index.html`에서 기본 입력과 기본 도구로 생성한 최대 들기 자세 가이드를 구분한다. 가이드 원본·생성 프롬프트/해시와 낮은 들기로 반환된 시트는 보존한다. 가이드는 최종 논리 픽셀 프레임이 아니며 전체 돌진-찍기 기획과 기존 채택 에셋은 유지한다.

## 쐐기보행기 위 여백 확장 입력

`python tools/stage2_monster_concepts/b3_headroom_handoff.py`는 기존1536×896 입력 위에640px만 흰색으로 추가한1536×1536 입력과 수정프롬프트를 만든다. `data/presentation/stage2_b3_headroom_handoff.json`이 기준이며 출력은 `outputs/waterway/b3-high-lift-headroom-20261101/index.html`이다. 몸180×100 논리/720×400 입력픽셀과10색은 그대로이며 작업캔버스만384×384로 늘어난다.

## 쐐기보행기 제자리 앞발 찍기 보정

`python tools/stage2_monster_concepts/b3_foreleg_slam_handoff.py`는 `data/presentation/stage2_b3_foreleg_slam_handoff.json`에 따라 확장 기본이미지를 바이트 그대로 복사하고 뒷발접점 고정/앞발만 크게들기/한번찍기의 수정프롬프트를 준비한다. 현재 입구는 `outputs/waterway/b3-foreleg-slam-20261101/index.html`이며 새 반환 모션은 아직 없다.

## 쐐기보행기 반환 찍기19장

`python tools/stage2_monster_concepts/b3_slam_return.py`는 `data/presentation/stage2_b3_slam_return.json`에 따라 원본36셀을 보존하고 들기8/찍기3/회복8의19장·1.5초를 기존10색/공통배율로 변환한다. 기본은 무보충 original이며 기존기본본 다리픽셀 추가시도는 큰들기에서 어색해 repair-trial로 분리한다. 입구는 `outputs/waterway/b3-slam-return-20261101/index.html`이다.

## 먹침필경사 공격 입력

`python tools/stage2_monster_concepts/normal_attack_handoff.py --spec data/presentation/stage2_m3_attack_handoff.json`은 기존198×59px/실제9색 기본본의 흰 배경1536×896 입력과 제자리 긴침4연타 제작 제안 프롬프트를 준비한다. 입구는 `outputs/waterway/normal-attack-handoff-20261101/M3/index.html`이다. B3는 original 무보충19장 채택이며 이미지/시간은 변경하지 않는다.

## 먹침필경사 반환 찌르기 재사용

`python tools/stage2_monster_concepts/m3_stab_reuse.py`는 `data/presentation/stage2_m3_stab_reuse.json`을 읽어 받은100셀을 분리하고 찌르기3장·회수3장의 같은 픽셀을4연타로 재사용한다. 전조/회복 포함 고유11장·31개 시간표 참조·기존9색·384×224·정수4배·한 사이클220ms·전체2110ms 검토와 맵합성/ZIP을 `outputs/waterway/m3-stab-reuse-20261101/index.html`에 제공한다. 원본/프롬프트/팔레트/수정마스크를 보존하며 새 포즈와 보간, 전투/Unity 변경은 없다. 이 재사용 구조의 빠른4연타는 아래 채택 규격을 따른다.

## 먹침필경사 빠른 찌르기

`python tools/stage2_monster_concepts/m3_stab_fast.py`는 `data/presentation/stage2_m3_stab_fast.json`을 읽어 기존 픽셀11장과 순서를 그대로 복사하고 찌르기70ms·회수80ms로 재생한다. 한 사이클150ms·전조/회복 포함4연타1870ms이며 이전 속도 비교와 맵합성/ZIP을 `outputs/waterway/m3-stab-fast-20261101/index.html`에 제공한다. PNG 파일 동일성과 WebP/APNG 시간을 확인하며 사용자가 빠른4연타를 채택했다. 전투/Unity 수치는 변경하지 않는다.

## 신호투척수 공격 입력

`python tools/stage2_monster_concepts/normal_attack_handoff.py --spec data/presentation/stage2_thrower_attack_handoff.json`은 기존98×106px·실제12색 기본본의 순백1536×896 입력과 소개/단계·프롬프트·참조/ZIP을 `outputs/waterway/thrower-held-hand-input-20261101/index.html`에 만든다. 현재 물체를 쥔 화면 오른쪽 팔로 조준400ms·고정200ms·투척150ms·회복/같은 손 재보급700ms를 요청한다. 화면 왼쪽 빈손은 균형만 잡으며 던지지 않는다. 물체 전달과 손 교체/캐릭터 반전은 금지한다. 실제 모션 생성과 투사체 비행은 별도다.

## 신호투척수 주머니 재보급 공격19장

`python tools/stage2_monster_concepts/thrower_pocket_attack_return.py`는 `data/presentation/stage2_thrower_pocket_attack_return.json`을 읽어 반환100셀을 보존하고 준비/고정/투척/빈손·주머니 재보급19장1450ms를 선정한다. 기존12색·384×224·한 배율/위치·정수4배, 원본 옷 점유 회복과 작은 구멍 정리, 비행체 분리 참고/수정마스크·선정키·WebP/APNG·맵합성·ZIP을 `outputs/waterway/thrower-pocket-attack-return-20261101/index.html`에 제공한다. 사용자가 본체19장의 픽셀/1450ms 박자를 채택했다. 투사체·피해 판정·Unity 적용은 별도다.

## 늘어진 수의 구 분리 입력

`python tools/stage2_monster_concepts/h3_orb_attack_handoff.py`는 `data/presentation/stage2_h3_orb_attack_handoff.json`을 읽어 승인 B 본체 부유16장을 바이트 그대로 복사하고 기본 image_gen 구 원본을40px·기존4색·64×64·이진 알파로 환원한다. 구 단독512×512 흰 입력/프롬프트·본체 앞 위치/맵 합성·원본/참조/ZIP을 `outputs/waterway/h3-orb-attack-handoff-20261101/index.html`에 제공한다. 본체에 손발/공격 자세를 추가하지 않는다. 실제 구 모션은 아직 생성하지 않았으며 이후 비행/착탄/장판·Unity 적용은 별도다.

## 늘어진 수의 반환 구 모션

`python tools/stage2_monster_concepts/h3_orb_return.py`는 `data/presentation/stage2_h3_orb_return.json`으로36셀을 분리하고 생성/발사13장+빈 회복14장1950ms를 기존4색·완성 높이40px·128×96 여백·정수8배로 내보낸다. 참고1번과 잘린15/16번은 제외/보존한다. 본체 B16장/100ms는 변경 없이 두 시간표 경계를 합쳐 재생하며1250ms 확인 대기로3200ms/부유2주기를 맞춘다. 원본·픽셀/시간표·키판·WebP/APNG·합성/ZIP은 `outputs/waterway/h3-orb-return-20261101/index.html`에 제공한다. 모션 채택과 이후 비행/착탄/장판/Unity 적용은 별도다.

## 늘어진 수의 느린 생성과 포물선 착탄

`python tools/stage2_monster_concepts/h3_slow_arc_impact.py`는 `data/presentation/stage2_h3_slow_arc_impact.json`을 읽어 기존 생성8장을1600ms(완성형300ms)로 늘리고 완성 구를1000ms 포물선으로 예시 바닥에 떨어뜨린다. 기본 image_gen 착탄 정지 원본을 기존3색 폭80px로 환원/최근접 변형한6키 파열과3초 장판 제안을 연결한다. 사용자3~4유닛 요청의 중간값인 전체 폭3.5유닛(PPU32·112px)을 사용하며,12장/1200ms의 직접 픽셀 조정으로 표면 세 곳에서 부풀고 터지는 잉크 방울을 번갈아 보여준다. 거품은 피해 범위 확장을 뜻하지 않는다. 본체 B16장/100ms·구 픽셀은 유지하며 전체 합성 반복6400ms는 부유4주기에 맞춘다. 결과는 `outputs/waterway/h3-slow-arc-impact-20261101/index.html`이며 착탄 원본/프롬프트·독립 픽셀·시간표·키판·흰/맵 합성·ZIP을 보존한다. 느린 생성·곡사 착탄·부글 장판 시각 시안은 사용자 채택이며 실제 게임 탄도/피해/장판 판정·Unity 연결은 하지 않는다.

## 수의 점성 비행과 투척수 실제 물체 분리

`python tools/stage2_monster_concepts/projectile_material_review.py`는 `data/presentation/stage2_projectile_material_review.json`을 읽는다. 기존 수의4색 구를 발사 변형의 감쇠 진동/속도 부하와 면적 보존 UV 역변환으로21키(실제 비행20샘플) 보정한다. 중력 궤적과 모양 변형을 분리하며 정밀 유체/실물 점성계수로 표현하지 않는다. 생성1600ms·본체 부유·착탄·전체 폭3.5유닛 장판 픽셀은 유지한다. 투척수 원본26/27/29번의 실제 비행 물체를 기존 배율/12색으로 분리해 참고3개를 남기고26번25×21px를 독립64×64/피벗32,32로 사용한다. 원본이 채운 내부 재질만 복구하고 정확한90도 회전4키를 만든다. 본체19장과1450ms 박자를 유지하고 키11 시작680ms의 마지막 손 위 위치에서 생성한다. 투척800ms 궤적은 합성 예시다. 결과는 `outputs/waterway/projectile-material-review-20261101/index.html`의 두 모션/맵/기존대비/키판과 독립 PNG·원본/해시·모델/시간표·ZIP이다. 새 보정은 검토 전이고 기존 채택본과 Unity/피해 판정은 바꾸지 않는다.

## 신호투척수 원형 물체 그대로 분리

`python tools/stage2_monster_concepts/thrower_original_prop.py`는 `data/presentation/stage2_thrower_original_prop.json`의 명시 마스크로 승인 기본본의 원통형 물체/짙은 마개를 native35×25px 그대로 분리한다. 원본26번의 둥근 비행체는 이전 검토로 보존한다. 손에 가려져 홈처럼 남은 윗면4행만 사용자 요청으로 기존 황토색으로 채우며, 수정 마스크 바깥은 픽셀 그대로다. 보정 전 분리본과 마스크를 남기고 크기 변형/새 색 없이64×64·피벗[32,32]에 담고, 정확한 반시계90도 회전25×35px 한 장을 비행 전체에 사용한다. 마지막 쥔 키9/10도 같은 물체로 맞추며 보라색 손/손목과 명시 영역 바깥 픽셀을 보존한다. 승인 본체19장 원본은 따로 보존하고 새 display 수정2장은 검토 전이다.1450ms/놓기680ms는 유지한다. 입구는 `outputs/waterway/thrower-prop-original-shape-20261101/index.html`이며 원본/마스크/형태 비교·독립 PNG/indexed/8배·놓기/맵 합성·ZIP을 제공한다. 수의 보정이나 게임/Unity 판정은 바꾸지 않는다.

## 폐잉크통 파손과 짧은 잉크 분출

`python tools/stage2_monster_concepts/thrower_can_break.py`는 `data/presentation/stage2_thrower_can_break.json`으로 현재 통/본체19장을 바이트 그대로 복사하고 통 픽셀을 마개1개/몸체3조각으로 겹침/누락 없이 분할한다. 접촉1480ms부터8키560ms의 정수 이동/90도 회전과 기존 캐릭터2색 잉크 분출/방울을 보여준다. 마지막 빈 키와600ms 확인 대기를 포함한 전체 반복은2640ms다. 잔류 웅덩이·추가 폭발/파편/잉크 피해는 없으며 직격1회 규칙을 유지한다. 결과는 `outputs/waterway/thrower-can-break-20261101/index.html`의 파손 확대/전체 투척/원본 맵 합성/키판과 독립 PNG/indexed/6배·원형/조각·참조/시간표·ZIP이다. 파손 시안은 검토 전이며 게임 탄도/충돌/피해/Unity 적용은 없다.

## 일반5종 대기/이동/공격 종합 검토

`python tools/stage2_monster_concepts/normal_motion_review.py`는 `data/presentation/stage2_normal_motion_review.json`으로 몸통 호흡 대기8키/각 채택 이동/최신 공격15클립을 모은다. K4 A19 본체+픽셀 보충 잉크(2.1초 유지)·B3 무보충19·M3 빠른4연타·투척수 원형 통/파손·수의27키 생성/점성 구/부글 장판을 사용한다. 원본 파일163개를 바이트 그대로 참조 사본/해시로 보존하며 PNG 픽셀과 복합 공격의 무손실 원배율 WebP 전경/시간을 유지한다. 합성 검토본의 정확한 흰 배경/알려진 바닥선만 제거하고 프레임361항목을 개체별3동작 공통 여백으로 자른다. 원래 크기를 유지하며 정수 확대만 한다.

`normal_motion_review.html`/`normal_motion_review.js` 템플릿은15칸 반복, 개별 대기→이동→공격/모드 선택, 정지/재생/속도/재시작, 프레임 이전/다음/슬라이더, 흰/어두운 배경과 원본960×540 맵을 제공한다. 생성된 HTML에는 데이터/재생 코드/PNG/맵/플레이어를 모두 포함해 상대 이미지 경로에 의존하지 않으며 별도 파일도 내보내기용으로 보존한다. 이미지는 독립 로딩하며 실패/지연은 해당 칸에 표시하고 다른 동작을 막지 않는다. 입구는 `outputs/waterway/normal-motion-review-v2-20261101/index.html`이다. K4의 최신 잉크 보충 합성과 수의의 채택 생성 보정을 포함한다. B3 돌진 연결은 별도임을 표시한다. 공격 확인 대기는 동작 시간과 나누며 전체 채택이나 Unity 검증으로 표현하지 않는다. 새 아트/시간/전투 규칙은 만들지 않는다.

`node tools/stage2_monster_concepts/tests/normal_motion_review_check.cjs`는 생성 후361개 시간 경계/컨트롤과 일부 이미지 지연·맵 실패·한 프레임 실패·전체 실패 표시를 오프라인 VM으로 검증한다. 브라우저 실행 검증과는 구분한다.

## 등짐분사수 · 잉크 분리와 잘린 끝 픽셀 보충

현재 검토 입구는 `outputs/waterway/k4-ink-only-repaired-20261101/index.html`이다. `python tools/stage2_monster_concepts/k4_ink_only_repair.py`가 `data/presentation/stage2_k4_ink_only_repair.json`을 읽는다. 반환 시트에서 잉크만 사용하며 캐릭터/총은 마스크로 제외한다. 기존 검정·회색2색, 이진 알파로 만들고9~25번17장의 잘린 왼쪽 끝을 픽셀로 보충한다. 원본에서 보이는 영역은 그대로 유지하며 추가 부분을 `repair-layers/`의 투명 PNG로 분리한다. 프레임별 잘린 윤곽별 연장 길이는 JSON에서 편집할 수 있다.

본체는 기존 승인 A19장 PNG를 바이트 동일하게 복사한다. 준비550ms/회수700ms를 유지하고 효과2100ms가 끝날 때까지 버팀 키만 연장한다. 반환 시트의 본체나 회수는 사용하지 않는다. 효과28장(종료 빈 프레임 포함)/투명 PNG·indexed·원본·보정 레이어·ZIP·전후 비교·기존 본체 합성을 제공한다. 없는 끝은 재구성 시안이며 사용자 승인/실제 전투 판정/Unity 적용과 구분한다. 생성 시 팔레트·이진 알파·캔버스 잘림·원본 영역 보존·본체 SHA·WebP 픽셀/시간을 검증한다.

반환 전체 본체30장 연결본은 사용자의 잉크 전용 요구와 달라 현재 채택 대상으로 사용하지 않는다.

## 등짐분사수 에테르 반환 분사 연결

`python tools/stage2_monster_concepts/k4_aether_spray_return.py`는 `data/presentation/stage2_k4_aether_spray_return.json`으로 사용자 반환6×6 원본36칸을 보존하고2~31번30장을 순서대로 연결한다. 한 배율0.31·고정400×224·기존11색·이진 알파를 적용하며 점유로 가중한 색 환원으로 흰 테두리 혼입을 줄인다. 전경/흰 배경 WebP의 픽셀과 시간, 원본 복사·36칸 분리, 팔레트/알파/indexed 동등성과 ZIP을 검증한다. 원본 비교·원본36장 전체·선정30장 키판·프레임 이동·속도·흰/어두운 배경·다운로드 입구는 `outputs/waterway/k4-aether-spray-return-20261101/index.html`이다. 원본9~25번의 왼쪽 잉크 끝 잘림은 복원하지 않고 경계 점유와 화면에 표시한다. 준비/흐름/끝물/회수2420ms와 확인 대기500ms는 시각 검토 박자다. 이전 코드 연결본은 총구에 붙는 느낌으로 탈락이며 이번 반환의 재질 방향만 사용자 호평, 연결본 채택은 미정이다. H3·기존 승인 공격·전투/Unity는 변경하지 않는다.

## 등짐분사수 분사 / 수의 생성 연결 보정

`python tools/stage2_monster_concepts/attack_fx_polish.py`와 `data/presentation/stage2_attack_fx_polish.json`은 `outputs/waterway/attack-fx-polish-20261101/index.html`에 독립 이미지 내장 검토 화면을 만든다. 기본 image_gen의 검정 잉크 정지 원본/프롬프트를 보존하고 기존 캐릭터2색·이진 알파·정수 픽셀 변형으로 분사8키를 만든다. K4 A19장/박자는 그대로며550~1200ms 분사 자세에서만 효과가 나온다. 사용자가 미리보기에서 고르기로 한 분사 길이/높이/노즐 위치와 임시 사각 범위를 함께 조절하고 JSON으로 저장한다. 기본128×40px는 시험값이며 전투 데이터/Unity 히트박스에는 적용하지 않는다.

수의는8장/기존 시점/4색을 유지하며 재질 경계의 픽셀 변화를19장에 나눠27키로 연결한다. 각50ms, 완성 구300ms 유지/발사1600ms/전체6400ms를 보존한다. 단순 SDF 문턱값에서1픽셀 띠가 한 번에 바뀌는 현상을 피하도록 경계의 변경 픽셀 수를 균등하게 배정한다. 최대 단계 변화는350→88픽셀이며 블러/알파 혼합을 쓰지 않는다. 발사 뒤 점성 비행/착탄/장판은 최신 합성과 픽셀/시간이 같다. 원본·독립 native/indexed·시간표·ZIP을 보존한다. 수의27키 보정은 사용자 채택이며 K4 분사는 아래 압력 분사 검토로 대체한다.

생성 후 `python -m unittest discover -s tools/stage2_monster_concepts/tests -v`와 `node tools/stage2_monster_concepts/tests/attack_fx_polish_check.cjs`로 기존 키/발사 뒤 정확성, 분사650ms, 팔레트/알파/본체19장 보존, 내장 이미지와 크기 조절/JSON 모델을 오프라인 검증한다. 브라우저/게임 판정 검증과 구분한다.

## 압력 분출 · 끝물 낙하 후 노즐 회수

`python tools/stage2_monster_concepts/k4_pressure_spray.py`는 `data/presentation/stage2_k4_pressure_spray.json`으로 공급 압력 감소→출구 속도 감소→중력 낙하를1/480초 입자로 계산한다. 출구 속도는 상대 압력 제곱근, 방출2D 질량은 노즐 폭×속도×dt로 정한다. 중력32×9.81px/s²·2차 항력·같은 크기/반대 방향 응집/점성 힘을 적용하며 실제 입자 상태/압력·속도/분출량을 NPZ/JSON으로 내보낸다. 정규화된 화면용 근사로서 측정 잉크 물성/비압축성 Navier-Stokes 전체 해석은 아니다. 픽셀은 기존 검정/이진 알파다.

`outputs/waterway/k4-pressure-spray-20261101/index.html`에서35키 흐름과 노즐 버팀을 확인한다. 공급650ms 뒤 낙하한 잉크가 사라지는 동안 버팀 키를 연장하고2250ms에 기존700ms 회수를 시작한다. 원본19장 PNG/다른 키 시간은 그대로다. 새 전체2950ms/확인 간격 포함3450ms는 시각 검토 박자이며 전투 판정 시간은 확정하지 않는다. 매 프레임 시각 범위 마스크/AABB와 프레임 이동/0.5·0.25배/원본 맵/ZIP을 제공한다. 수의 채택27장과 전체 클립은 바이트 복사한다. `tests/test_k4_pressure_spray.py`와 `node tools/stage2_monster_concepts/tests/k4_pressure_spray_check.cjs`가 힘/압력, 회수 경계, 팔레트/알파/마스크/원본 보존과 오프라인 뷰어를 검증한다.

## 등짐분사수 · 무거운 기름 재질 방향

입자 분사 시안은 무게감이 부족하다는 사용자 피드백으로 채택하지 않는다. 최신 재질 방향은 `data/presentation/stage2_k4_oil_material.json`과 `python tools/stage2_monster_concepts/k4_oil_material.py`를 사용하며, 입구는 `outputs/waterway/k4-oil-material-direction-20261101/index.html`이다. 대화 기본 image_gen으로 그린3행 효과 원본/프롬프트/캐릭터 참고를 보존하고, 코드로는 행 분리·공통 픽셀 배율·기존2색 환원·그대로인 몸체와의 합성만 수행한다.

큰 덩어리의 초기 분출/아래로 굽는 두꺼운 붓기/큰 끝방울의 정지3키 재질 시안이다. 기름을 붓는 농도와 무게감을 검토하며 완성 모션이나 물리 해석·실제 석유 점도·히트박스로 표현하지 않는다. 첫 흐름 전체 폭140px는 재질 비교 배율이며 판정값이 아니다. 캐릭터와 수의 채택27장은 바꾸지 않는다. 사용자가 재질 방향을 확인했으며 이를 바탕으로 아래 모션 시안을 검토한다.

## 무거운 기름 모션 연결

`python tools/stage2_monster_concepts/k4_oil_motion.py`와 `data/presentation/stage2_k4_oil_motion.json`은 `outputs/waterway/k4-oil-motion-20261101/index.html`을 만든다. 기본 image_gen의9칸 기름 그림/프롬프트와 원형3장을 보존하고, 기존 원형3개를 같은 픽셀/노즐 위치로 넣는다. 나머지 그림을 같은 배율/기존2색·이진 알파로 맞춘 뒤 큰 끝방울을 정수 픽셀로 바닥354px까지 이동하고60ms의 짧은 눌림과100ms 빈 버팀을 연결한다. 입자 시뮬레이션으로 기름 실루엣을 대체하지 않는다.

기름16키/1380ms, 버팀 종료1930ms, 기존700ms 회수, 전체 시각 동작2630ms/확인 포함3130ms다. 준비550ms와 본체19장/나머지 키 박자는 보존한다. 원본 맵/흰·어두운 배경/속도/프레임 이동/시각 범위와 독립 PNG·indexed/원본/시간표 ZIP을 제공한다. 수의 승인 전체 클립은 바이트 동일 복사다. 이 모션은 총구에 붙는 느낌으로 사용자 미채택이며, 새 에테르 반환 연결본을 위 항목에서 검토한다. 실제 전투 시간·히트박스/피해/Unity 연결과 구분한다. 생성 후 Python 테스트와 `node tools/stage2_monster_concepts/tests/k4_oil_motion_check.cjs`가 원형3개/팔레트·알파·빈 픽셀/끝방울 착지/회수 경계/원본 보존과 내장 이미지/컨트롤을 검증한다.
