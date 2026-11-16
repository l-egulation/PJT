# 전투 데이터 파이프라인

[분야 개요](README.md)

## 증강 데이터

`data/combat/maintenance_augments_proposal.json`은 [증강 기획](../../design/progression/maintenance-augments.md)의 후보·대가·카드별 적용 수치(`effect`)·공통 규칙(`runtimeRules`)·시험값(`trialValues`)과 미정을 기록한다. 저장소 루트에서 `python tools/combat/export_maintenance_augments.py`로 확정 수치와 카드 효과의 일치, 안정형의 무불이익, 제안 가능한 계열 수를 검사하고(획득처는 보스 처치·봉인 보상이며 가격 항목이 없어야 한다) 구현한 카드만 `unity/Assets/Deviant/Combat/Resources/MaintenanceAugments.json`으로 생성한다. `python tools/augment-workbench/build.py`는 같은 원본으로 독립 검토 HTML을 생성한다. 두 도구 모두 `--check`로 원본과의 일치를 검사한다. 런타임은 `Deviant.Run.MaintenanceAugments`가 무료 제안 추첨·보유·합산을, `CombatArena.MaintenanceAugments`가 전투 적용을, `ForestCombatV2.AugmentRewardChoice`가 다음 챕터가 있는 보스 처치(무기 보상 뒤 대기)와 봉인 보상 상자의 선택 화면·일시정지를 소유한다. 처음 만든 봉인 도전방 시험 증강의 원본(`chapter1_run_augments.json`)과 생성기는 폐기했다.

## 무기 도면·런 잉크 도감 기획 데이터

`data/combat/weapon_ink_run_design.json`은 [무기·잉크 명세](../../design/equipment/rules.md)의 확정 구조와 미정 값을 기록한다. `python3 tools/combat/export_run_inks.py`가 39종 이름·설명·기본/강화 수치와 무기·보상·상점 규칙(`rewards.gauge.openingRequirements`의 런 초반 필요량, `rewards.openingOffers`·`doubleOffers`·`mixedOffersFrom`의 회차별 구성 포함)을 `Combat/Resources/RunInks.json`으로 생성하며 `--check`로 일치를 검사한다. Windows에서는 `PYTHONUTF8=1`로 실행한다. `SketchEquipment`와 `CombatArena.RunInks`가 이를 읽는다. `null`은 미정이고 0이나 무료를 뜻하지 않는다. 기존 팔레트/스탬프용 JSON과 고정 파츠 수치는 현재 구현 입력이며 새 구조의 수치 원본이 아니다. `saveTransition`은 새 체계 통합 시 기존 런·영구 프로필·해금 이력을 삭제하고 새 규칙으로 초기화하는 정책이며 실제 파일 저장소가 `equipmentRevision`으로 구형 저장·백업을 함께 초기화한다. `inkApplication`은 확정 후 빈 슬롯을 포함한 전체 편집 금지, `inks.presentation`은 기능형 이름·설명 작성 기준, 39종 `entries`, 수치 자리표시자를 가진 `descriptionTemplates`를 기록한다. `python3 tools/combat/build_ink_display_text.py`는 조합·이름 중복을 검사하고 같은 원본의 효과 수치로 [확정 표시 문구](../../design/equipment/inks/names-and-descriptions.md)를 생성한다. `--check`로 생성 문서 일치를 검사한다. Unity 도감·보상·상점은 새 표시 문구와 공통 기본 아이콘을 사용한다. 생성기·Unity 카탈로그·소비 코드를 함께 갱신한다.

`inks.effectStacking`은 회복·방어막 생성을 제외한 효과별 실제 6칸 기여 상한과 원본 범위·초과 효과 선택/만료 정책을 소유한다. `export_run_inks.py`가 `maximumContributingInkCopies`를 내보내고, `CombatArena.RunInks`가 플레이어 지속 버프의 전 원본에서 효과량·최근 발동 처리 순으로 선택한다. 파랑·보라의 기존 원본 무기/스킬 귀속과 6칸 슬롯 제한은 유지한다. 강화 환산량 E와 수치 상한을 변경하지 않는다.

기획 데이터의 `inks.attackGranting`은 기본 공격으로만 발동해 기본 공격 피해 또는 공속만 높이는 빨강, `inks.skillGranting`은 스킬 공격으로 발동해 쿨타임 감소·스킬 피해량 증가를 제공하는 파랑이다. `inks.statusConditions`의 공격 대상 몬스터 기절·속박·슬로우 조건을 함께 확인한다. 비용 소모·상태 시간 소비·페널티는 없다. 복합 조건 예외형은 미정이다. 조건은 같은 공격의 상태 부여 후 확인하며 상태이상의 지속시간·중첩은 현재 효과 명세와 구조화 데이터의 시험값을 따른다.

현재 기본 목록의 개수는 `inks.contentCounts`에 총 39개(효과 부여 27·빨강 3·파랑 6)로 기록한다. 파랑의 `requiredCombinations`는 효과 2개×상태 3개의 6개 조합이며, 기본/강화 상태와 미채택 예외 후보는 별도 잉크로 세지 않는다. 생성기는 색별 합계·조건 조합의 완전성과 총수를 함께 검사한다.

`inks.numericValues`는 모든 잉크에 기본·강화 수치 한 쌍이 필요하다는 계약이다. 잉크 색과 슬롯 강화색이 같으면 강화값, 나머지는 기본값을 쓴다. 잉크 레벨은 없으며 기본 39종의 값은 현재 효과 명세와 수치표의 플레이 테스트 시작값을 따른다. `inks.rarity`는 노말·레어·에픽 드롭률 분류이며 효과 조합의 `rarity`, 빨강의 `conditionVariantRarities`, 파랑의 `effects.*.rarity`에 현재 노말을 기록한다. 기본/강화 상태와 독립이며 등급별 확률·가중치는 미정이다.

[잉크 주기·상태 조건 산정 기준](../../design/equipment/inks/balance-proposal.md)은 `weapon_ink_run_design.json`의 정책과 `ink_content_balance_proposal.json`의 검토 가정에서 생성한다. `python3 tools/combat/build_ink_balance_proposal.py`로 생성하고 `--check`로 일치를 검사한다. `--preview`는 Git 제외 `outputs/ink-balance/model.json`에 입력·SHA256을 기록한다. 새 데이터는 `RunInks.json`을 통해 Unity 런타임에 연결한다. 기존 팔레트 수치·등급·비용 데이터는 기존 구현 자료로 구분한다.

기존 Unity 색상 잉크의 짧은 게임 표시 문구는 `data/combat/ink_color_parts_proposal.json`의 `playerText`에 둔다. `export_weapon_growth.py`가 이 문구를 Unity 카탈로그의 `description`으로 내보낸다. 가방·정비소·판매대의 잉크 설명란은 이 문구를 그대로 표시한다. 강화 전후 비교에서는 레벨별 수치를 따로 보여준다. 가방에서 선택한 잉크 슬롯에 커서를 올리면 짧은 설명에 포함된 용어의 뜻을 표시한다.

잉크 충전소 수치 원본은 `data/combat/stage_recovery.json`이다. `python tools/combat/export_stage_recovery.py`로 `ForestCombatV2/Resources/StageRecovery.json`을 생성하고 `--check`로 일치를 확인한다. `Deviant.Run.StageRecovery`가 횟수·가격·비례 회복·전투 시계 간격을 관리하고 `StageRecoveryHold`가 길게 누르기의 시간·취소·단발 완료를 관리한다. `StageRecoveryStation`이 두 챕터의 안전 바닥 배치·상호작용·지도 표시와 공통 `RunEconomy.TrySpend` 결제를 연결한다. 생성 JSON만 직접 수정하지 않는다.

방패 패링의 창은 `melee_weapon_design.json`의 `shield_default.independent_effect.window`(0.20초)이며, 성공 재무장0.60초·성공 회복0.10초는 `shield_runtime_rules.json`의 `parrySuccessCooldownSeconds`·`parrySuccessRecoverySeconds`이며 공격 시간 강화·쿨감·환급으로 줄이지 않는다. 전방 투사체 차단 연장은 `shield_runtime_rules.json`의 `parryProjectileReach`(0.75unit)이다. `python tools/combat/export_shield_runtime.py`가 `ShieldRuntimeRules.json`을 생성하고 `--check`로 원본 일치와 미끼 반격 명세의 피해·반경 일치를 검사한다. 미끼는 HP60·3초·도발5unit을 유지하고 피해 파괴·만료 시 반경3unit에 고정265피해를 한 번 주며 교체·리셋에는 폭발하지 않는다. `ShieldSkillPresentation`은 승인된 투명 시트를 전투 시계로 재생하며 피해 판정 크기나 실제 이동 수치를 바꾸지 않는다. 원본은 리소스 ID `characters.ink.weapons.shield-reviewed`, 배치는 공통 실행기의 `--asset characters.ink.weapons.shield-reviewed` 선택을 사용한다.

전투 피해 변동 배율의 원본은 `data/combat/damage_variance.json`이다. `python tools/combat/export_damage_variance.py`로 `Combat/Resources/CombatDamageVariance.json`을 재생성하고 `--check`로 일치를 확인한다. `CombatDamageVariance`가 배율을 읽으며 수락된 타격에 한 번 적용한다. `varyPlayerReceivedDamage=false`이면 플레이어 피격에는 변동을 적용하지 않으며 적이 받는 피해의 90~110% 변동은 유지한다. 믹서의 기본 피해 7/8 추첨은 별도 패턴 규칙으로 유지한다. 낙사와 명시적 강제 사망은 변동하지 않는다.

보스 기절·속박 재부여 제한은 현재 0초로 해제되어 있으며 수치 원본은 `data/combat/boss_status_design.json`의 `controlInternalCooldown`이다. `python tools/combat/export_boss_control_cooldown.py`로 `Combat/Resources/BossControlCooldown.json`(초 단위 `stunSeconds`·`rootSeconds`)을 재생성하고 `--check`로 일치를 확인한다. 생성기는 유한한 0 이상의 값과 기존 게이트 계약을 검사한다. 0이면 같은 순간 재부여도 허용한다. `BossControlCooldownSettings`가 값을 읽는다.
무기 성장의 검토용 수치 원본은 `data/combat/weapon_growth_mvp_tuning_draft.json`이며 `draft_pending_user_review` 상태다. `node tools/ink-combination-lab/generate-growth-tuning.cjs`로 `docs/development/data/catalogs/weapon-growth-mvp-tuning.md`를 생성하고 `--check`로 확률 합계·20종 참조·고정 등급·강화표·확정 정책과의 분리를 검사한다. 이 초안은 브라우저와 Unity에 내보내지 않는다.

MVP 무기 성장 방향의 편집 원본은 `data/combat/weapon_growth_mvp_proposal.json`이다. 캐릭터 공통 공격 성능 강화, 시작 팔레트 무지급, 3~5칸·색상 랜덤, 유료 색상 변경 1회, 부분 교체 시 슬롯 강화 유지·재제작 시 초기화, 내부 등급별 드롭·수치 정책을 기록한다. 색상 변경은 팔레트마다 1개 슬롯·1회이며 점유 칸은 새 색의 새 잉크와 동시 교체·기존 잉크 소멸이다. 슬롯 강화는 장착 잉크의 수치를 높이며 슬롯에 귀속되고 +0부터 최대 +5까지다. 내부 등급은 1~4 숫자의 작은 UI 표기를 사용하며, 4단계로 기록하고 `fixed_by_part`로 파츠마다 고정 등급 하나와 기본 수치를 사용한다. 동일 파츠의 다른 등급 버전은 만들지 않는다. 등급 내 개체별 수치 랜덤은 사용하지 않는다. 슬롯 강화는 `effect_family_multiplier`로 파츠별 기본 수치에 효과 계열별·강화 단계별 배율을 적용한다. +0 배율은 1이며 실제 배율표·대상 수치는 미정이다. 등급 추첨은 `weights_by_acquisition_source`로 획득처별 확률표를 사용하며, 실제 확률표와 획득처별 등급 보장은 미정이다. 일반 몬스터의 낮은 확률 색상 잉크 드롭 방향은 `inkAcquisition.normalEnemy`에 기록한다. 일반 몬스터의 잉크 드롭 성공 수량은 1개다. `markSystem`에는 표식 최대 5개·8초·전량 소비·소비 표식 수 비례 기본 피해·격발 잉크 효과 1회를 기록한다. 기본 격발 피해 기준은 격발 무기의 기본 공격 기준값이며 추가타 적중·처치 트리거를 발동한다. 표식은 전체 8초 갱신하고 기존 표식 격발 후 새 무기 표식 1개를 부여한다. 팔레트 칸 수는 3·4·5 순서로 출현 빈도가 낮아진다. 최초 활성화는 잉크 1개부터 가능하며 정비소 제작비는 전체 칸 수 비례 골드다. 장착비·활성화 후 빈칸 추가비는 0이고 기존 잉크 교체는 유료다.

미정인 실제 드롭 확률·등급별 추첨 확률·수치·강화 배율·격발 피해 계수·비용은 null로 유지하고 런타임으로 내보내지 않는다.

브라우저 잉크 실험 50종은 `data/combat/ink_simulator_parts_proposal.json`(effect.params), `ink_simulator_builds_proposal.json`(조합), `ink_simulator_proposal.json`(미정 규칙 실험·간이 무기·AI)을 원본으로 사용한다. `node tools/ink-combination-lab/generate-simulator.cjs --preview`로 별도 카탈로그와 독립 HTML을 생성하고 `--check --preview`로 일치를 검사한다. `check-simulator-catalog.cjs`는 목록 무결성을, `simulator.test.cjs`는 계산을 검사한다. 기존 Unity 잉크 원본·내보내기와 분리하며 상세 범위는 [잉크 시뮬레이터](../tools/ink-simulator.md)를 따른다.

색상 잉크 파츠 개편안 54종의 원본은 `data/combat/ink_color_parts_proposal.json`이다. `node tools/ink-combination-lab/generate-color-parts.cjs`가 색상별 파츠 수(빨강 10·노랑 12·초록 10·파랑 11·보라 11종)·트리거·"색 표식이 터질 때" 파츠의 표식 순번(1~7)·회복과 보호막 규칙을 검사한 뒤 [카탈로그](catalogs/ink-color-parts.md)를 생성하고, `--check`로 일치를 검사한다. Unity 내보내기는 아래 `export_weapon_growth.py`가 맡고, 브라우저 시뮬레이터에는 연결하지 않는다.

기본 대시의 속도 모양은 `data/combat/locomotion/player.json`의 선택적 `dashCurve`(`holdFraction` 최고속 유지 시간 비율 0~0.9, `easeOutPower` 감속 거듭제곱 1~8)다. `dashSpeed`는 평균 속도라 거리(`dashSpeed × dashDuration`)는 곡선과 관계없이 같고, 출발 속도는 평균의 1/(유지 + (1-유지)/거듭제곱)배다. 현재 0.25·3(세제곱)으로 출발 45unit/s에서 끝에 멈추듯 줄어든다(플레이 테스트 2026-11-03: 일정 속도는 「에스컬레이터처럼 미끄러진다」). 블록이 없으면 일정 속도다. 고정 스텝마다 그 구간의 평균 속도를 써서 합이 정확히 같은 거리가 된다. 정점 체공 보정의 수치 원본은 `data/combat/locomotion/player.json`의 `jumpApexSeconds`(현재 0.06초)다. `LocomotionProfile`이 유효 범위와 내용 해시를 소유하며 `export_unity_catalog.py`로 `Resources/PlayerLocomotion.json`을 재생성한다. 기존 상승·하강 중력과 점프 초기속도를 바꾸지 않는다. 보정은 자발적인 플레이어 점프에만 적용하며 적·투사체의 중력을 바꾸지 않는다.

같은 파일의 선택적 `jumpAssist`(`groundBufferDuration` 0.12, `wallCoyoteDuration` 0.10, `releaseVelocityScale` 1.0)와 `horizontalControl`(`groundAccelerationSeconds` 0.05, `groundDecelerationSeconds` 0.03, `airAccelerationSeconds` 0.10, `airDecelerationSeconds` 0.08)은 이동 보정 1차 시험값이다. 검증·해시·`ToData()` 왕복에 포함한다. 현재 해제 배율 1.0은 키를 일찍 놓아도 상승속도를 줄이지 않아 첫 점프·재점프·착지 예약 점프가 모두 최대 높이로 뛰게 한다. 두 객체가 없는 이전 schema 2 프로필은 0.08초 착지 예약·벽 코요테 없음·조기 해제 무시·즉시 이동을 유지한다. 시간은 0~0.5초(가감속은 0~1초), 해제 배율은 0 초과 1 이하만 허용한다.

색상 잉크 후보의 편집 원본은 `data/combat/ink_parts_proposal.json`, 대표 조합 원본은 `data/combat/ink_part_builds_proposal.json`이다. `node tools/ink-combination-lab/generate-doc.cjs`로 `docs/development/data/catalogs/ink-part-catalog.md`를 생성하고 `--check`로 일치를 검사한다. 두 JSON의 개별 효과·수치·중복 배정·상한은 실험 제안이다. `python tools/combat/export_training_parts.py`는 60종 표시 메타데이터와 `unityImplementation`으로 명시된 59종의 공통 전투 시험 DTO(`Combat/Resources/RuntimeInkParts.json`)를 함께 생성한다. `--check`는 두 생성물의 원본 일치를 검사한다. 원본의 `unityExperiment.implementedParts`와 파츠별 상태가 일치해야 한다. 런타임 효과 DTO를 직접 고치지 않는다. 제품의 마지막 공격 무기 지속 효과 규칙과 실험 데이터의 유지 규칙을 구분한다.

패드 피드백의 편집 원본은 `data/combat/presentation/gamepad_feedback.json`이다. `python tools/combat/export_gamepad_feedback.py`로 `unity/Assets/Deviant/Combat/Resources/GamepadFeedback.json`을 재생성하고 `--check`로 일치를 확인한다. 스키마 1의 `low`·`high`·`duration` 단발은 원래 선형 감쇠를 유지한다. 스키마 2의 이벤트는 `segments` 배열을 사용하며, 각 구간의 `low`/`high`는 0~1 세기, `duration`은 0 초과 1초 이하, `curve`는 `hold`(일정)·`decay`(시작 세기→0)·`rise`(0→설정 세기)다. 두 세기가 0이면 쉼이다. 이벤트당 1~16구간·전체 2초 이하를 검증한다. 스키마 2에서도 구형 단발 이벤트를 읽을 수 있다. `GamepadFeedbackProfile.Validate`와 공통 exporter가 이 형식과 트리거 위치/세기/주파수·오래된 이벤트의 재생 제한을 검사한다. `readyStrength`·`chargingStrength`는 0~8이며 0은 트리거 저항 해제다. 킥 세기 1~8·주파수 1~255·지속시간은 독립적으로 유지해 무저항 상태에서도 짧은 킥을 사용할 수 있다. 이 값은 이벤트 진동과 DualSense 트리거의 표현 시험값이며 입력 임계값·차지/쿨타임·피해를 변경하지 않는다. [`gamepad_feedback_tuner.py`](../../../tools/combat/gamepad_feedback_tuner.py)의 HTML 편집기도 같은 exporter의 검증·직렬화를 사용해 원본과 Unity 카탈로그를 저장하며, 원본 해시가 바뀌면 덮어쓰지 않는다. 사용 방법은 [HTML 피드백 튜너](../presentation/ui/input-devices.md#html-피드백-튜너), 연결과 지원 범위는 [입력 장치 구조](../presentation/ui/input-devices.md#진동과-dualsense-적응형-트리거)를 따른다.

테스트 손맛 설정의 편집 원본은 `data/combat/presentation/hit_feedback.json`이다. `python tools/combat/export_hit_feedback.py`가 `unity/Assets/Deviant/Combat/Resources/CombatFeelFeedback.json`으로 내보내며 `--check`는 원본과 생성물 일치를 검사한다. 타격 피해나 공격 시간표를 바꾸는 데이터가 아니며 C# `CombatFeelSettings.Validate`가 범위를 검증한다. 같은 도구가 공격별 적 밀림·플레이어 기본 공격 전진 원본 `data/combat/attack_feel_motion.json`을 `Resources/CombatFeelMotion.json`으로 내보내며 `CombatFeelMotion.Validate`가 검증한다. 이 파일은 게임 규칙이다. 적용 범위와 조작은 [전투 표현](../presentation/visuals/combat-presentation.md#손맛-테스트)을 따른다.

전방 근접 기본·특수의 X 판정은 기존 앞쪽 끝을 유지하면서 몸 뒤 경계에서 0.25unit까지 포함한다. 오른쪽 기준 뒤쪽 끝은 root x=-0.53125이며 왼쪽은 대칭이다. 일반·차지·돌진 타격과 저장 위치 잔상의 활성 형상을 원본에서 조정하고, 양방향 공격·투사체·설치 효과의 형상은 유지한다. 대검은 기본공격만 몸 앞끝부터 전방으로 제한하며, 특수 4종은 기존 형상을 유지한다. 유한 행동의 형상은 `action_tracks.hits[].box`를 편집하며 옛 `hits[].shape`는 이행 출처로 보존한다.

수치 원본은 data/combat/다. 일반 이동·기본 대시·대시 점프 취소·점프 속도/중력은 locomotion/player.json 스키마 2를 편집한다. 검 1타는 actions/base_sword_basic_0.json, 나머지 검·둔기는 melee_weapon_design.json, 원거리는 ranged_basic_design.json, 발사기 스킬은 launcher_skills.json을 사용한다. 가방에 보이는 스킬 설명 글은 skill_descriptions.json(특수 id → 문장)이며 export_unity_catalog.py가 MeleeCatalog.json 각 특수의 `summary`로 옮긴다. 사용 가능한 특수마다 글이 있어야 하고 쓰이지 않는 id가 있으면 내보내기가 멈춘다. 새 글자가 생기면 tools/temporary-gui-preview/update-font-glyphs.py로 정적 글꼴 목록을 갱신한다. 검 기존 JSON은 명시한 초기/회귀 참조이며 현재 원본을 중복하지 않는다.

시계탑 집행자의 HP 1,800·광폭화 누적 피해 257(남은 HP 1,543)·최종 단계 HP 360·추가 속도 1.2배·최종 회복 및 대기 0초와 사슬 고정 유지 시간은 `data/combat/clock_executioner_timing.json`의 `chainHoldSeconds`(현재 1초)가 소유한다. `python tools/combat/export_clock_boss_timing.py`로 `Assets/ForestCombatV2/Resources/ClockExecutioner/Timing.json`을 내보내고 `--check`로 일치를 검사한다. `ClockExecutionerBoss`가 시작 전에 스키마 2·유한 양수·체력 기준의 순서·최종 회복 및 대기 0초를 검증한다. 페이즈는 다른 구간의 속도를 바꾸지만 꽂힌 사슬의 유지 구간은 전투 시간 기준 고정 길이이며 일시정지에는 함께 멈춘다.

명중 표현의 역할·배율·표시 한도·카메라/왜곡 시험값은 스키마 1 `data/presentation/hit_feedback.json`, 게임 시간에 영향을 주는 슬로우 배율·실제 시간 TTL은 스키마 2 `data/combat/hit_feedback_time.json`이 각각 소유한다. 표현 원본의 `distortAllHits=true`는 모든 수락된 플레이어 명중에 왜곡을 요청하는 시험 설정이며 큰 카메라 킥 자격 `strongActions`와 독립적이다. 필드가 없거나 `false`이면 강타 자격만 사용한다. `paperInk=true`는 프리팹 명중 VFX를 원래 발광색 대신 흰 몸·1게임픽셀 안쪽 외곽선·어두운 곳 빗금으로 그린다. `paperOutlineColor`는 `#RRGGBB` 형식의 외곽선색(현재 짙은 중성 회색 `#1A1A1A`, RGB 26·26·26)이며 C#에서 형식을 검증한다. 960×540 격자의 이진 경계를 사용하고 안티앨리어싱은 적용하지 않는다. 실행 중 F1에서 변경한 값은 재질 사본에 즉시 적용하고 F5에 유지하며 원본 JSON에는 저장하지 않는다. 프리팹·재질 원본은 바꾸지 않고 실행 중 재질 사본(`Presentation/Unity/Resources/HitFeedbackPaperInk.shader`)으로 바꿔 끼운다. 필드가 없거나 `false`이면 원래 색이다. `debrisAlpha`(0 초과 1 이하, 없으면 1)와 `debrisDesaturate`(0~1, 없으면 0)는 시체 파편의 투명도·회색 섞기 비율이고, `debrisStrikeSpeed`·`debrisStrikeUpward`(0 이상, 없으면 0)는 플레이어 근접 공격 판정 상자가 파편에 겹칠 때 주는 수평·상향 속도(unit/s)다. 현재 시험값은 0.72·0.45·6.5·5.0이다. `prefabScale`(기본 1, 현재 0.625)은 모든 레시피의 `scale`에 곱하는 명중 VFX 전체 크기다. 레시피가 이미 원래의 0.8배라 원래 크기의 0.5배로 보인다. 몬스터보다 2~3배 큰 그림을 몬스터 크기에 맞추려고 넣었다. 에디터·개발 빌드의 F1 패널에서 20~150%로 바꿔 볼 수 있으며 저장하지 않는다. 시간 원본은 `trigger=player-death`만 허용하며 슬로우는 플레이어 본인 사망 때 생명당 한 번 적용한다. 몬스터 처치와 이전 중요 처치 플래그에는 적용하지 않는다. `python3 tools/combat/export_hit_feedback.py`가 두 DTO를 `Resources/HitFeedback`으로 운반하고 `--check`로 생성물 최신성을 확인한다. C# `HitFeedbackSettings`·`HitFeedbackTimeData`가 의미를 검증한다. 현재 수치는 trial이며 큰 강타 목록은 비어 있다. 라이선스 에셋 복원과 Unity Variant/잔해 생성은 [타격감 계획](../presentation/visuals/combat-hit-feedback-plan.md)의 별도 절차다.

`Integration/HitFeedbackBridge`의 프리팹 VFX·물리 잔해·카메라/왜곡·사망 슬로우와 `CombatFeelFeedback`의 검·채찍 궤적·피격/처치 연출은 독립된 출력 층이다. F1에서 전자의 여섯 출력을 각각 비교하고 F6/F7/F8에서 후자의 기존 비교 설정을 사용한다. 편집 원본도 서로 덮어쓰지 않는다. `data/presentation/hit_feedback.json`과 `data/combat/hit_feedback_time.json`은 `Resources/HitFeedback`으로, `data/combat/presentation/hit_feedback.json`은 `Combat/Resources/CombatFeelFeedback.json`으로 투영한다. `python tools/combat/export_hit_feedback.py`와 `--check`는 세 생성물을 함께 처리한다.

몬스터 명중 프리팹 후보 44개는 표현 원본의 `recipes`에 원본 이름·고유 키·배율·회전으로 명시한다. 평소 명중은 `criticalRecipe`를 뺀 43개 중 하나를 명중 사건의 seed로 고르고, 치명타 명중은 언제나 `criticalRecipe`(`hit_blunt`, `VFX_ImpactOrange (1)` 큰 둥근 고리)를 쓴다. 치명타 여부는 수락된 명중 기록(`CombatAcceptedHit.Critical`)에서 읽으며 피해 계산은 바꾸지 않는다. `forms`는 공격 형태의 자격 검사용 매핑이며 매핑에 없는 형태의 명중은 프리팹을 재생하지 않는다. 실행 중에 종류를 고르거나 바꾸는 경로는 없고 편집기·개발 빌드·배포 빌드가 같은 규칙을 쓴다. Variant 생성기는 원본 JSON 해시를 바인딩에 저장해 낡은 로컬 캐시를 자동 재생성하고, `recipes`에서 빠진 키의 프리팹·재질을 `Generated`에서 지워 Player 빌드에 남기지 않는다.

이동 스키마 2의 firstJumpVelocity/secondJumpVelocity는 20, riseGravity/fallGravity는 66.6666667/104.1666667의 승인값으로 시작한다. 네 값은 유한 양수로 검증·해시하며 스키마 1을 기본값으로 보완하지 않는다. 생성기는 같은 DTO를 운반하고 C# LocomotionProfile이 의미를 소유한다. Editor 열한 수치의 저장/재로드·원자 해시 충돌 경로를 유지한다. 적용은 전체 프로필+테스트 리셋뿐이므로 진행 중 공중/반동/미사일 상태를 재해석하지 않는다. 점프 2회는 고정 정책이며 착지 예약 시간은 `jumpAssist.groundBufferDuration`을 따른다. 모든 액터 중력과 시작 반동/미사일은 같은 프로필의 중력을 사용한다.

벽점프 수치는 `data/combat/locomotion/player.json`의 `wallJump`와 `wallSlide`가 소유하고 `export_unity_catalog.py`가 `PlayerLocomotion.json`으로 복사한다. 불변 프로필의 검증·해시·왕복 저장을 유지한다. 현재 `clingDuration`·`detachDuration`·`arrivalClingDuration`은 0이며, 실행은 이전 프로필에 양수가 있어도 정지 매달림이나 붙기 지연을 만들지 않는다. `CombatInput.ClimbAxis > 0`이면 기어오르고, 아니면 미끄러진다. 미끄러짐 수치가 없는 이전 프로필은 일반 중력으로 내려간다. 벽 차기·예약 접촉·코요테는 모두 현재 `ClimbAxis > 0`을 요구하고 ↑를 놓으면 예약을 제거한다. 범위는 `jumpCheckDistance`와 기존 접촉 허용 거리 0.08unit 중 큰 값을 사용한다. 맞은편 도착도 ↑가 있으면 즉시 오르고 아니면 즉시 내려오며, 별도 잡기 입력이나 필드를 사용하지 않는다. 벽 차기 진단은 `[CombatV2]`의 `WALL JUMP NEAR`·`WALL JUMP MISS`에서 현재 거리·방향·수직속도를 확인한다. 벽 꼭대기 올라서기는 `wallJump.ledgeHopHeight`·`ledgeClimbSeconds`·`ledgeClimbForward`가 소유한다(`ledgeClimbSeconds` 0~2, `ledgeClimbForward` 0~3, 둘 다 내용 해시에 들어간다). 올라가기 그림을 벽 모서리에 붙이는 자리(`ledgeCorner`, 원본 그림판 왼쪽 위 기준 화소)와 한 크기(`ledgePixelsPerUnit`)는 표현 데이터 `Resources/ApprovedTraversal/ClimbV4.json`이 소유한다.

강베기 base_sword_default의 현재 숫자는 melee_weapon_design.json만 편집한다. basic_sword_special.json은 numeric_source로 이 원본을 가리키고 초기 승인 숫자/정책/자원 참조를 보존하며 현재 숫자의 두 번째 권한이 아니다.

무기 스위칭 준비 단축률은 `data/combat/weapon_switch.json`의 `afterHitWindupReduction`(현재 0, 0 이상 1 미만)을 편집하며 `export_unity_catalog.py`가 `MeleeCatalog.json`의 `weaponSwitch`로 내보낸다. C# `WeaponSwitchDefinition`이 범위를 검증하고, 값이 없거나 범위를 벗어나면 전투 런타임을 시작하지 않는다. 휘몰아치기의 잉크 단축률은 `ink_color_parts_proposal.json`의 `switchWindupReduction`이며 `export_weapon_growth.py`가 `WeaponGrowthMvp.json`의 슬롯 단계 값으로 내보내고 0 이상 1 미만을 검사한다. 규칙은 [무기 스위칭 취소](../../design/combat/rules.md#무기-스위칭-취소)를 따른다.

기습 추가 피해는 `data/combat/stealth_ambush.json`에서 편집한다. `useSourceBonuses=true`일 때 `stealthFirstHitBonusDamage`(숨죽이기, 현재 0)·`smokeFirstHitBonusDamage`(연막, 현재 0)를 사용하고 동시에 유효하면 큰 값을 쓴다. `useSourceBonuses`가 없거나 false이면 이전 공통값 `firstHitBonusDamage`(현재 0)를 두 출처에 적용한다. 같은 파일의 `stealthExitBasicSpeedBonus`(0.5)·`stealthExitSeconds`(4)는 숨죽이기 은신 해제 후 기본 공격 공속 보너스와 지속 시간이며 0이면 효과가 없다. 모든 피해는 0 이상이며 `export_unity_catalog.py`가 `MeleeCatalog.json`의 `stealthAmbush`로 내보낸다. C# `StealthAmbushDefinition`이 유한·비음수를 검증하고, 값이 없는 이전 카탈로그는 기습 없음(0)으로 읽는다. 규칙은 [전투 공통 명세](../../design/combat/rules.md)의 은신·기습 항목을 따른다.

일반 적 기본 피격 경직·밀림은 `data/combat/enemy_hit_reaction.json`을 편집하며 `export_unity_catalog.py`가 `MeleeCatalog.json`의 `enemyHitReaction`으로 내보낸다. 현재 기본 경직 0.30초, 기본 밀림 0.25unit/0.10초, 근접 일반 밀림의 사거리 안쪽 여유 `basicPushOverlap` 0.25unit이다. `EnemyHitReactionDefinition`이 유한·비음수 여유를 검증하며, 값이 없거나 0인 이전 데이터는 사거리 상한을 적용하지 않는다. `attack_feel_motion.json`에 있는 공격은 그 밀림이 기본 밀림을 대신하되 같은 사거리 상한을 사용한다. 실제 경계·무기 조합·전용 밀기 예외는 [전투 공통 명세](../../design/combat/rules.md#특수정비상태이상)를 따른다.

```text
data/combat/ (기존 JSON · actions/ · locomotion/ · presentation/)
  ├─ build_melee_design.py → docs/design/equipment/weapons/forms/melee.md
  ├─ export_unity_catalog.py → Assets/Deviant/Combat/Resources/{MeleeCatalog,PlayerLocomotion}.json
  └─ export_attack_motion.py → Assets/Deviant/Combat/Resources/CharacterV2/AnimationCatalog.json (전체 본체 registry·첫검·근접/특수 프레임 원본)
```

저장소 루트에서 실행한다.

```sh
python tools/combat/build_melee_design.py
python tools/combat/export_unity_catalog.py
python tools/combat/export_attack_motion.py
```

효과음 제작용 [기본공격 시간표](../presentation/audio/basic-attack-audio-timing.md)는 `python tools/combat/build_basic_attack_audio_reference.py`로 생성하고 `--check`로 원본과의 일치를 확인한다. 근접 타격·원거리 발사와 로컬 시험 공속/추가 후딜을 구분한다. 프레임 시간 모드는 Python에서 해석하지 않으며 해당 모드가 저장되어 있으면 생성을 중단한다. 이 도구는 문서만 생성하고 수치 원본과 Unity 리소스는 변경하지 않는다.

근접 문서의 수치 표는 생성물이므로 직접 편집하지 않는다. 공통 설명과 표 형식은 생성기의 `COMMON`·`render()`에서 관리한다. 원거리 설명 문서는 수치 원본을 참조해 함께 갱신한다. 생성 후 diff를 확인하고 [검사 절차](../testing/testing.md)를 수행한다.

`PlayerLocomotion.json`은 원본과 같은 스키마의 생성물이며 Python 내보내기는 원본의 직렬화 형식을 그대로 유지한다. `LocomotionProfile.TryCreate`가 런타임·편집 적용에서 공통으로 스키마, 유한·비음수 수치, 양수 대시 시간, 대시 안의 무적창을 검증하고 읽기 전용 값과 문화권 비의존 내용 해시를 만든다. Python은 이를 복사·직렬화하며 시간 해석이나 별도 게임 규칙을 소유하지 않는다. 내보내기 성공만으로 유효한 프로필이라고 판단하지 않고 Unity의 `LocomotionProfileTests`와 전투 회귀를 실행한다.

## 1챕터 처치 보상·정비소 가격 시험값

[성장·경제](../../design/run/progression-and-economy.md)와 [콘텐츠 비용](../../design/progression/content-costs.md)의 1차 시험값 원본은 `data/combat/chapter1_economy.json`이다. 역할(normal·elite·boss)별 골드·잉크 파츠·팔레트·스탬프 확률과 수량, 가방 잉크 수(`inkCount`)·특수 공격 잉크 수(`specialInkCount`), 캐릭터 강화 가격 트랙(`statUpgrade`)의 시작가·증가폭, 슬롯별 강화 비용(`slotUpgradeCosts`: 50/100/180/350/600), 2스테이지 별도 보상(`chapter2Rewards`: 일반 13~16G, 엘리트 450~550G; `rewards`는 1스테이지), 잉크 판매대(`inkShop`: 진열 등급 `grades` [3,4]·진열 수 `offerCount` 2~3·등급별 고정 가격 `gradePrices` 420/600G·색상 잉크 판매가 `saleGold` 8G), 무기 스탬프 비율·스탬프 교체 비용·팔레트 제시 수, 스테이지당 스탬프 드롭 상한(`stageStampLimit`, 1 이상 정수)을 담는다. 저장소 루트에서 `python tools/combat/export_chapter1_economy.py`로 범위·확률·항목과 판매대 등급 가격이 공격 강화 4~5회 누적 비용(420~600G) 안에 있는지를 검사하고 `unity/Assets/ForestCombatV2/Resources/Chapter1Economy.json`을 생성하며 `--check`로 일치를 확인한다. 생성물은 직접 편집하지 않는다. 런타임 추첨과 가격 계산은 `Deviant.Run`의 `RewardRoller`·`RunEconomy`가 소유한다.

## 들판 테스트의 6종 몬스터

행동 원본은 `data/monsters/chapter1_approved.json`이다. `python tools/combat/export_chapter1_enemies.py`로 `unity/Assets/Deviant/Combat/Resources/Chapter1Enemies.json`을 재생성한다. 들판 씬은 콘텐츠 ID로 이 정의를 읽으며 맵 JSON의 과거 `ranged` 분류와 임시 체력 대신 승인된 종별 정의를 사용한다. 공격 프레임 시간도 이 정의가 소유한다. 원본의 `unityImplemented`·`unityVerified`는 전체 전달 명세의 상태이므로 제한된 평지 AI 연결만으로 올리지 않는다.

Unity EditMode의 `ForestCombatV2.Tests.Chapter1SpeciesBehaviorTests`가 실제 들판 씬과 수동 공통 시뮬레이션에서 6종 피해·선딜, 멧돼지 5칸/제동·중단·근접 정지, 짚단의 자세 유지·피격 취소를 검사한다. 기존 `Chapter1HitFeedback`·`FieldMouseBrainTests`도 회귀 대상으로 사용한다.

## 승인된 몬스터 공격 아트 가져오기

승인 아트의 원본·선택·검증은 `resources`가 소유한다. 현재 선택한 프레임은 메인의 배치 계약으로 적용한다.

```powershell
python tools/assets/update_assets.py --check
python tools/assets/update_assets.py
```

이 명령은 선택된 PNG 바이트만 운반한다. `Data/ApprovedAttackEffects.json`의 기존 아트 프레임 시간·피벗·연결과 메인의 `.meta`를 유지한다. 멧돼지 잔상과 들쥐의 깨끗한 공격 포즈도 기존 출력 버전을 사용하며 전투 수치·AI 시간 원본을 변경하지 않는다. 제작 재생성과 배치·Unity 연결을 구분한다.

Unity에서 `Tools > Deviant > Meadow Dusk > Apply Approved Attack Effects and Placement`를 실행하면 Point/PPU32/비압축/커스텀 발 피벗으로 가져오고 기존 들판 씬에 이펙트 카탈로그와 배치 설정만 연결한다. 씬 지형·카메라·무기·기존 모션 카탈로그는 재생성하지 않는다. 테스트는 EditMode의 `ForestCombatV2.Tests.Chapter1AttackEffectTests`와 기존 종별 행동·피격 테스트를 사용한다.

## 행동 원본과 검 1타 편집

`melee_source.py`가 통합 데이터의 `hit_sources`를 해석한다. 이행한 타격의 기존 피해·모션·발동·형상 배열 칸은 `null`이어야 하며 별도 숫자가 남아 있으면 중복 원본 오류로 거부한다. `active_seconds`는 미이행 타격에만 사용한다. 이행한 타격은 행동 원본의 `hit.active`를 사용하고 전체 연계 시간은 해석된 모션 길이의 합으로 계산한다. ID·스키마·경로 이탈·중복 원본 검사는 `python tools/combat/test_melee_source.py`로 확인한다.

`Tools > deviant > Experiments > Basic Sword First Hit`에서 1타의 피해·모션 길이·타격 시각·활성 시간·박스 크기/오프셋을 편집한다. **다음 공격부터 적용**은 진행 중 공격의 정의를 보존하고 새 1타에만 반영한다. **리셋 후 적용**은 동일 API의 리셋 경로를 사용한다. 두 버튼은 원본을 저장하지 않는다.

**원본 저장 · 문서/카탈로그 재생성**은 파일 해시 충돌 검사와 원자적 원본 저장 후 기존 Python 생성기 두 개를 실행한다. Python 실행 파일은 창에서 지정할 수 있다. 원본은 저장됐지만 생성기가 실패하면 그 상태를 구분해 표시하며 원본을 보존한다. 원인을 해결한 뒤 재시도하거나 CLI로 재생성한다. 저장은 현재 전투에 자동 적용하지 않고, 생성 카탈로그를 직접 편집하지 않는다. Undo는 임시 값만 되돌리며 파일 해시·실제 저장·전투 리셋은 되돌리지 않는다.

검·둔기 7형태의 기본 17타는 초기 `MeleeCatalog.json`을 `BasicMeleeCatalog`/`BasicMeleeDefinition`으로 컴파일한다. 각 실행의 시작 공속·타격/전진 시간·형상은 불변 `MeleeActionDefinition`으로 해석하며 `MeleeActionRunner`가 같은 전투 시계를 읽어 실행한다. 검 2타와 나머지 형태도 기존 `melee_weapon_design.json` 원본을 유지하며 별도 수치 원본을 추가하지 않는다. `GetBasicMelee`로 형태/타격별 정의를 조회하고 기존 `BasicSwordFirst`/`BasicSwordSecond` 조회와 1타 적용 API도 유지한다. `MotionDuration`/`ImpactTime`/`Age`는 시작 실행을 읽는다. 진행 중 Unity 호환 DTO 편집으로 타격과 본체/Cue가 서로 다른 시간을 채택하지 않는다. 기존 예약/연계/취소 순서와 0.30초 연계 여유를 보존하고 검 1타의 프레임 후보를 다른 타격/무기에 재사용하지 않는다. 원거리·폭탄·특수·전체 Sprite/Hit/Motion/Control 트랙은 기존 경로/후속 범위다.

### 나머지 근접 기본 16타 수치 편집

`Tools > deviant > Experiments > Melee Basic Hits`는 기존 7형태 기본 17타 중 검 1타를 제외한 16타의 피해·모션·타격·박스를 편집한다. 원본은 기존 `melee_weapon_design.json` 배열이며 검 1타 `hit_sources`·전용 창·프레임 검사 경로는 유지한다. `active_seconds`는 해당 형태의 미이행 타격 전체에 공유되며 창이 영향을 받는 ID와 전체 diff를 표시한다. 기존 양옆 대검 박스는 X=0/root 대칭을 유지한다. 전체 후보 검증 뒤 다음 시작/리셋 적용하고 진행 중 캐스트는 시작 버전을 보존한다.

저장은 바이트 해시 충돌 검사 후 `SourceFiles` 한 번의 원자적 교체로 기존 필드/미편집 내용/특수를 보존하고 기존 `build_melee_design.py`·`export_unity_catalog.py`로 재생성한다. 원본 저장 성공/생성 실패를 구분하고 저장과 현재 전투 적용은 별도다. `basic_sword_attack.json`의 2타 값은 회귀 참고값이며 편집 원본이 아니다. 특수·원거리·프레임/아트 편집은 제공하지 않는다.

네 저작 창의 장면 미리보기는 편집 후보를 즉시 읽되 자동 적용/저장하지 않는다. 숫자·프레임 변경 뒤 실효 접촉/활성/종료와 world-unit 형상을 갱신한다. FrameTime 수치 창의 시간 필드는 고정 기준값 읽기 전용이며 장면은 마커가 컴파일한 실효 시간을 표시한다. Melee Basic Hits의 검 1타 선택도 원본을 읽기 전용 미리보고 실제 편집은 전용 창을 유지한다. 이미지가 없으면 형상만 남고 요청/실제 본체 바인딩 및 미디어 누락을 표시한다. 주입된 임시 Sprite 증거와 실제 아트 검증은 구분한다.

근접 기본 17타의 표현은 시작 시점의 본체/Cue 메타데이터·Sprite를 고정하고 같은 AttackMotionTiming을 읽는다. 전체 20개 가족의 FrameTime은 아래 전용 경로를 사용하며 첫검 전용 창은 같은 위임 원본의 제한된 뷰다. 전용 세트가 없는 형태의 기존 대체도 시작에 고정하고 다른 형태 Cue를 생성하지 않는다.

검 강베기·레이피어 장침·대검 반월참도 같은 MeleeActionRunner와 불변 DirectMeleeSpecialDefinition을 사용한다. 기본 공속 비적용·고정 방향·수평 입력 0·정상 중력·기존 자원 소비를 유지한다. 수치 저작과 FrameTime은 아래 전용 경로를 사용하며 진행 중 본체/타격은 시작 버전을 유지하고 기본 공격 Cue를 추가하지 않는다.

레이피어/대검 기본 특수의 편집 원본은 기존 `melee_weapon_design.json` → `MeleeCatalog.json`을 유지한다. `ChargedMeleeDefinition`은 초기 카탈로그의 단일 nearest 기하/정책·메타데이터와 `ChargeDefinition`을 함께 검증/복사하며 조회/실행은 같은 내용 해시를 보관한다. 시작/예약/발동 대상 검사와 현재 타격은 이 불변 정의를 읽고, 호환 DTO/원본의 실행 중 편집을 현재 판정에 채택하지 않는다. `ChargeExecution`은 같은 전투 시각/유지 표본을 계속 해석한다. 본체 매체/대체/최초 실패는 시작 스냅샷으로 고정하고 현재 해제 시각으로 샘플링하며 매체 편집을 현재 실행에 채택하지 않는다. 별도 차지 원본/저작/명시적 임시 적용/저장 API는 아직 없다.

기존 `Basic Sword First Hit` 창은 1타만 편집하며 2타·특수의 수치나 공통 규칙을 바꾸지 않는다. 나머지 근접 기본은 위 별도 창을 사용한다. 수치 저장 후 기획 설명의 고정 예시와 수동 검사 기준도 검토하며, 생성 문서 변경만으로 모션·손맛 검증을 완료 처리하지 않는다. 전체 프레임 시간표와 범용 입력 녹화는 후속 범위이며, 아래 고정 시나리오에서 적용한 수치를 비교할 수 있다.

### 직접 근접 특수 3개 수치 저작

Tools > deviant > Experiments > Direct Melee Specials는 기존 base_sword_default/rapier_range/greatsword_range의 피해·접촉·활성·본체 시간·박스 폭/높이/X/Y를 편집한다. 대검 X=0을 유지하고 쿨타임/용량/정책/표시 메타데이터는 읽기 전용이다. Equipment가 자원을 소유한다. 같은 C# 정의로 전체 선택 부분집합을 검증한 뒤 다음 시작/리셋에 원자 게시하며 진행 cast/매체와 미선택 행동은 유지한다. SourceVersion은 정책/자원/메타데이터, ContentHash는 전체 수치를 포함한다. SourceFiles 해시·원자 저장으로 선택한 통합 원본만 바꾸고 기존 생성기를 실행하며 초기 회귀 JSON을 덮어쓰지 않는다. 생성 일치 검사는 시작/빌드의 같은 컴파일러를 사용한다. nearest/접지/제어가 다른 기술·차지/재입력/미사일·전체 Sprite 트랙 저작을 완료한 것은 아니다.

### 모든 기존 행동의 본체 모션 저작

망치의 기본 특수는 전용 `hammer/attack_default_slam` 매체가 있으면 해당 모션을 사용한다. 해당 매체가 없는 이전 카탈로그는 기존 `hammer/attack1` 대체를 유지한다. 평타는 `hammer/attack1`·`hammer/attack2`이며, 전용 특수 매체 연결로 피해·판정·쿨타임은 바꾸지 않는다. 고해상도 망치 시험 그림은 896×768, PPU128, 피벗 (384,164; 아래 기준)으로 기존 224×192·PPU32의 월드 크기와 발 기준을 유지한다.

검 1타의 마무리 자세는 `base_sword_attack1.json`의 마지막 프레임을 편집한다. 현재는 `F003`을 유지해 2타의 같은 시작 자세에 이어진다. 기본 공격 이펙트의 크기와 발 기준 위치는 [`basic_attack_effects.json`](../../../data/combat/presentation/basic_attack_effects.json)을 편집하고 `python tools/combat/export_attack_motion.py`로 재생성한다. `scale`은 양수 배율이며 `x`/`y` 배열은 기존 이펙트 프레임 수와 일치해야 한다. 확정 패키지의 아트와 시간표를 수정하지 않는다.

data/combat/presentation/action_motion_registry.json은 제공된 본체 clip 팔레트와 완전한 행동/변환 바인딩을 관리한다. 각 clip의 안정 step ID·프레임 참조·유지 비중·원본 접촉/Cue 시각을 소유하고 base_sword/attack1은 기존 첫검 원본에 위임해 시간을 복제하지 않는다. 논리 ID 61개는 잉크별 발사기 그림 구분을 포함한 64 typed 바인딩으로 확장한다. 발사기 기본 공격은 장착 잉크의 총 그림(변형 0~3)을, 발사기 스킬은 그 잉크 총의 `attack_single`을 쓴다. 같은 clip을 공유한 행동은 영향 목록을 표시하고 전용 매체가 없는 행동은 기존 대체를 명시하며 새 아트를 만들지 않는다. 확정 11종 아트 가져오기는 발사기 4종 총 그림의 실제 팔레트와 바인딩을 연결한다.

Attack Body Motions는 전체 선택·공유 영향·알려진 프레임 삽입/삭제/순서/참조·Undo·diff·실효 ms·원자 저장/재로드·다음 시작/리셋을 제공한다. 공유 CombatTimeFixed 비중과 행동별 FrameTime override는 다른 원본이다. 기본/직접 20개 gameplay FrameTime은 아래 가족 편집기를 사용하고 다른 정책은 별도 작업이다. 생성기는 구조만 운반하고 같은 C# 컴파일러가 의미를 검증한다.

레지스트리의 선택적 `frameTimingSource`가 없거나 `null` 또는 빈 문자열이면 C# 컴파일러에서 같은 미지정 상태다. Python 운반 단계는 Unity 직렬화의 빈 문자열을 오류로 취급하지 않고 원래 표현을 보존한다. 비어 있지 않은 값의 경로·행동 대응은 C#이 검증하며, 생성·저장 검증용 임시 저장소도 필요한 근접 프레임 원본과 전투 카탈로그를 함께 복사한다.
### 원거리 승인 16개 수치 저작

Tools > deviant > Experiments > Ranged Action Numerics는 발사기·폭탄·수리검의 기본/특수 16개만 편집한다. 발사기 볼 탄환은 ranged_basic_design.json, 발사기 스킬 4개는 launcher_skills.json이 소유한다. 모아쏘기의 최소 충전 시간·피해는 첫 발사 시각·피해와 같은 값이며 최대 충전·최대 피해·최대 지름은 원본 파일에서 편집한다. 실제 피해/방출/본체 시간·직선/레이저/산탄/폭탄/파편/신관/복귀/주기/미사일/반동 수치만 노출한다. 자원/충돌/경직/관통/one-way/종료/방향 정책은 고정이고 미소비 값은 편집하지 않는다. 반동 시간·레이저 반경·상속 폭탄/수리검 공통 수치는 기존 단일 소유 관계를 유지한다.

RangedActionDefinition/Catalog가 전체 16개·공유 그룹/alias를 같은 기존 실행/발사 snapshot으로 컴파일한다. 전체 후보 검증 뒤 한 번 게시하고 기존 cast/탄/파편/착지 신관/복귀 예약/미사일은 시작 값을 유지한다. canonical ranged_basic_design.json과 launcher_skills.json의 두 읽기 해시를 검사하고 선택한 소유 파일만 원자 저장하며 다른 파일이 그대로인지 확인한다. 파일 간 원자 transaction을 주장하지 않는다. SourceVersion은 정책/자원, ContentHash는 실제 수치이며 생성/빌드/신규 시작도 같은 C# 의미를 검사한다. 저장·생성·전투 적용은 별개다.

### 근접 기본·직접 특수 20개 FrameTime

Melee Frame Timing은 전체 20개를 같은 선택/변환/로컬 프레임·마커/diff/Undo·다음 시작/리셋·저장/재로드로 편집한다. 첫검은 기존 base_sword_attack1.json만 저장하고 나머지 19개는 melee_frame_bindings.json만 저장한다. shared registry는 알려진 프레임 identity와 고정 기본 비중을 유지한다. Fixed canonical 기록은 시간 override/hash를 복제하지 않고 FrameTime 기록은 정확한 정책/구조적 media hash·timeline을 요구한다. ToSourceDocument는 상속 원본, ToDocument는 분리된 적용 버전 snapshot이며 의미를 혼동하지 않는다.

변환은 기존 EffectiveSeconds/ActionTime을 재사용해 sourceImpact 0의 첫 자세 유지도 처리하고, 양수 span/IntervalDuration·실제 전후 diff를 검증한다. 공유 비중 변경은 다른 활성 로컬 override를 재시간화하지 않으며 step 삽입/삭제/참조/대체 변경은 명시 rebase 전 실패한다. 숫자 창/API/save는 활성 FrameTime의 시간 편집을 막고 피해/박스는 기존 숫자 원본에 남긴다. 공유 active_seconds가 활성 override를 건드려도 거부한다. 첫검 수치 저장은 명시 repository와 현재 mode를 검증한다. 없는 모드 원본을 고정 기본값으로 숨기지 않는다.

TryApplyMeleeFrameState는 17 basic/3 direct·registry/첫검/전체 source를 모두 precompile/media 검사한 뒤 한 번 게시한다. 전체 가족의 초기 시나리오 복구는 아래 완전 근접/특수 batch를 사용하고 reset 관측자는 일관된 전체 집합을 읽는다. reset 요청은 idle 예약을 게시 전에 검사하며 거부된 reset이 후보만 바꾸지 않는다. 완전 가족 snapshot과 첫검 위임·기존 비활성 knock speed baseline·raw 사실 비교는 유지한다. 제품 아트 완료와 구분한다.

### 유한·일반 특수 23개 FrameTime

`Tools > deviant > Experiments > Special Frame Timing`은 기존 finite 4개와 authored 19개를 선택해 고정/로컬 모드·정책·실효 시간 diff·안정 step/마커·Undo/Redo·읽기 전용 Sprite 미리보기·다음 시작/리셋 적용·원자 저장/재로드/생성을 제공한다. `special_frame_bindings.json`이 모드 원본이며 공유 registry는 이미지/step identity를 소유한다. 손실 없는 자동 투영이 불가능하면 거부 이유를 표시하고 모든 step 지속시간과 정책 마커를 포함한 완전한 JSON을 명시적으로 저작한다. 없는 시간/마커를 자동 채우지 않는다.

고정 원본은 상속 시간 override를 복제하지 않고 적용 snapshot은 실제 정책/매체 바인딩·전체 시간표와 해시를 보관한다. Python은 구조만 운반하고 C# 공통 컴파일러가 의미·빌드 최신성을 검증한다. 공유 모션의 구조 변경과 활성 모드의 수치 시간 우회는 거부하며 피해/형상·event-relative 수치는 기존 허용 목록을 따른다. 재사용 시나리오는 전체 23개 그래프와 코드/질의 provenance를 필수 캡처·검증·복구한다. 현재 스키마 9는 이전 1~8과 누락을 거부하고 기본값으로 보완하지 않는다. 제품 아트·차지/원거리 FrameTime 완료를 뜻하지 않는다.

시나리오는 엔진/플랫폼/CPU/비트수, 정렬된 실제 로드 모듈 이름·MVID 벡터와 코드 해시, `queriesHitTriggers`·`queriesStartInColliders`·`autoSyncTransforms`·`defaultContactOffset` 및 설정/결합 해시를 함께 저장한다. 모듈 식별은 로드 세대별 캐시를 사용하되 설정은 매번 새로 검사한다. MVID를 얻지 못하면 명시적으로 실패하며 현재 코드/설정으로 채우지 않는다. 지원 범위는 관리형 Editor 실험장이다.

### 차지 근접 특수 2개 수치 저작

Tools > deviant > Experiments > Charged Melee Specials는 rapier_default/greatsword_default의 최소/최대 유지·피해, 활성창·기본 종료, 박스 폭/높이/X/Y 열 값을 편집한다. 자원/방향/nearest/수직/제어 정책은 읽기 전용이다. 같은 ChargedMeleeDefinition/ChargeDefinition.Resolve로 전체 선택 부분집합을 검증해 다음 시작/리셋에 원자 게시하며 진행 중 유지/해제/매체는 시작 정의를 보존한다. SourceVersion은 고정 정책/자원을, ContentHash는 전체 후보를 식별한다.

단일 melee_weapon_design.json 저장은 min_hold/activation_time/hit.time·min_damage/hit.damage·active/hit.active를 함께 갱신한다. 선택적 `charge.hold_until_release`·`charge.release_windup`도 원본 로더와 생성 카탈로그가 같은 정책으로 읽는다. 미지정은 자동 해제·해제 준비 0이며, 대검의 해제까지 유지·해제 후 준비 정책은 수치 편집/저장에서도 보존한다. charge.recovery/max_motion은 읽기 전용 파생 주석으로 관련 숫자 편집 때만 같은 C# Resolve(Maximum)으로 갱신한다. Python은 구조를 운반하며 별도 차지 계산기를 만들지 않는다. SourceFiles 해시·원자 교체·생성/시작/빌드 일치 검사와 오류 후 재시도는 기존 경로를 따른다. 미리보기의 선택 해제 시각도 같은 Resolve·기존 모션 시간 매핑을 샘플링할 뿐 실제 charge/cast·대상 선택·자원/피해/Cue를 실행하지 않는다. 다른 열아홉 특수·원거리 전체 저작 완료를 뜻하지 않는다.

### 일반 근접 특수 19개 수치 저작

Tools > deviant > Experiments > Generic Melee Specials는 기존 열아홉 ID의 실제 소비 수치만 descriptor별로 편집한다. 행동/타격 시간·피해/박스/제어, 전용 이동/재입력·효과 지속/형상/소환 HP/범위, 패링 창/피해·무적/명시 수직 고정을 지원한다. 종류·개수·앵커/대상·방향/수직 분류·자원은 읽기 전용이다. 방패 패링의 시작 0은 정책이고 돌진 거리는 속도×시간 파생 주석이며 대상 순간이동 max_distance는 실제 미소비 메타데이터라 편집하지 않는다. 채찍 접근의 max_distance=0은 거리 상한 없음을 뜻하며, 속도와 벽 충돌은 계속 적용한다. 흘려찌르기는 teleport_endpoint_clear 정책으로 벽 너머 적을 선택하고 경로 차폐를 생략하되 도착 몸 전체의 고체 겹침은 거부한다.

AuthoredSkillDefinition은 기존 불변 SkillExecutionDefinition을 감싸는 닫힌 콘텐츠 컴파일러다. SourceVersion은 고정 정책/자원/미소비 주석, ContentHash는 실제 수치 그래프를 식별한다. 단일 melee 원본의 선택 부분집합을 SourceFiles 해시·원자 교체로 저장하고 전체 후보 검증 뒤 다음 시작/리셋에 게시한다. 진행 cast/독립 효과/매체는 시작 버전을 유지한다. 패링 window/반사 피해·연막 Box·분신 Box/tauntRadius·rapier_follow_up의 명시 vertical_freeze는 이제 캡처한 원본 값을 실제 소비하며 기존 승인값을 유지한다. 새 runner/시계/JSON 언어를 만들지 않는다.

### 검·방패 유한 행동 트랙

Finite Sword Action Tracks는 기존 네 행동의 action_tracks 스키마 1 시간/형상과 실제 canonical 숫자를 함께 편집한다. FrameTimeline이 발동/본체/타격/이동/효과/방어 시간·박스를 단독 소유하고 stable-hit 피해·돌진 속도/벽 회복·검기 속도/거리/대칭 spawn·방어 HP/감소율은 기존 숫자 노드가 활성 원본이다. 종류/개수/ID/앵커/제어/자원은 읽기 전용이며 0-hit 방패에 판정을 만들지 않는다. 이전 시간/형상·돌진 거리·검기 lifetime/방어 duration은 이행 출처다. Save는 선택 숫자+tracks만 해시 검사/원자 교체하고 미선택·비활성 provenance를 보존한다. 전체 후보 검증 뒤 다음 시작/리셋에 게시하고 진행 실행은 시작 버전을 유지한다. 생성/시작/빌드는 같은 C# 정의를 사용하며 Python은 시간 의미를 복제하지 않는다.


## 검 1타 모션 편집

게임용 타이밍 원본은 [presentation/base_sword_attack1.json](../../../data/combat/presentation/base_sword_attack1.json) 한 곳이다. 기본값은 스키마 1 `base_sword/attack1`·행동 `base_sword_basic_0`의 `CombatTimeFixed`이며 기존 수치/아트 참조를 유지한다. 스텝 ID는 같은 Sprite의 반복과 별도로 유지하고 프레임 참조·초·원본 접촉/Cue 시작을 보관한다. `FixedAttackMotion`은 방어 복사·내용 해시·검증을 제공하며 실행과 미리보기가 같은 `AttackMotionTiming`을 읽는다. 내부 접촉 마커·1..256개 양수 프레임과 `-1` 또는 클립 안 Cue 시작을 지원한다. 아래 명시적 저장으로 같은 파일을 스키마 2 `FrameTime`으로 전환할 수 있다. 스텝 삽입·참조 교체·다단 저작은 아직 지원하지 않는다.

`Tools > deviant > Experiments > Basic Sword Motion`은 원본 프레임 유지 시간과 접촉/Cue 시작, 원본 대비 diff·미저장/실행 버전·기준 공속 전 실제 환산 시간을 표시한다. 고정한 실제 타격/종료 시각은 선택한 전투 정의 또는 재로드한 행동 원본에서 읽는다. 프레임을 늘려도 실제 타격 시각은 이동하지 않으며 같은 구간의 다른 프레임이 짧아질 수 있다. Undo/Redo와 Play Mode 전환은 임시 편집을 보존하며 자동 저장하지 않는다.

영구 `FrameTime` 원본이 활성화되면 이 고정 모션 창의 편집/저장/적용을 막고 `Frame Timing Preview`로 안내한다. `Basic Sword First Hit`의 타격·활성·종료 값은 고정 모드 기준값으로만 표시하며 읽기 전용이다. 피해·형상 편집은 계속 사용할 수 있고 프레임 마커를 덮어쓰지 않는다. 외부에서 모드를 바꾼 뒤 남은 고정 Draft도 저장 전에 현재 원본을 다시 검사해 거부한다.

**다음 1타부터 적용**과 **리셋 후 적용**은 공통 `TryApplyBasicSwordMotion`을 사용한다. 현재 아트 바인딩과 프레임 참조/개수가 일치하는 유효한 전체 정의만 교체한다. 잘못된 후보·없는 바인딩은 교체/리셋 전에 거부한다. 진행 중 본체와 Cue의 시작 버전은 유지한다. **모션 원본 저장 · 카탈로그 재생성**은 파일 해시 충돌 검사·원자적 원본 저장 뒤 `export_attack_motion.py`를 실행하며 런타임 적용과 별도다. 생성 실패 시 저장한 원본을 보존하고 오류를 해결한 뒤 재시도한다. 생성기는 이미지/Pillow 없이 게임용 모션 원본과 기본 공격 이펙트 배치를 카탈로그에 투영하고 원본 아트 참조를 보존한다.

제작 패키지의 `재생설정.json`은 최초 이식 입력이며 이후 게임용 타이밍은 위 원본이 소유한다. 리소스 배치는 이미지 바이트만 운반하므로 모션 원본·생성 카탈로그나 실험 시간을 되돌리지 않는다. 이미지·피벗·보존 패키지를 게임 시간 저작의 저장 대상으로 삼지 않는다. 실제 이미지 로딩과 아트 가독성 검사는 별도 수행한다. 현재 A/B 창은 고정/프레임 후보를 보관해 타격/범위/대시 지표를 비교하며 실제 모션 영상/캡처 저장은 아직 없다.

## 프레임 기준 시간과 임시 적용

Frame Timing Preview는 첫검 고정 모션/행동 원본을 독립 Draft로 읽고 MeleeFrameCompiler.ProjectFirst로 접촉·활성 종료·행동 종료·Cue를 안정 스텝 기준에 연결한다. 전체 가족 창도 같은 compiler의 ProjectBasic/ProjectDirect를 사용한다. 실제 전후 시각/diff·공속 전 로컬 프레임·후보 버전을 표시하고 연결된 준비 프레임 편집은 해당 마커를 이동시킨다. Undo/Redo와 Play 전환은 편집을 유지하며 적용/저장은 명시 동작이다.

순수 `FrameTimeline`은 프레임 순서와 안정 ID, 마커 의존성을 복사하고 초 단위 시간을 해석한다. 절대 시각과 참조를 동시에 지정하거나 중복/누락/순환 참조·비유한/음수 결과·진행하지 않는 프레임 누적을 거부한다. 일반 마커는 독립 효과 등을 위해 모션 끝 뒤 시각도 표현할 수 있고, 미리보기 어댑터는 양수 종료와 접촉<종료를 추가 검사한다. 현재 고정 모드에서 실제 유지 시간이 0으로 접힌 프레임은 임의 삭제/ID 변경/최소 시간 부여로 바꾸지 않고 가져오기를 거부한다.

MeleeFrameDefinition은 수치/정책/매체와 행동별 프레임 시간표를 캡처한다. contact < hit-end <= action-end와 전체 로컬 프레임 종료를 검증하고 기본 공속을 한 번 적용한다. Cue는 같은 행동 마커를 읽고 기존 매체 곡선의 실제 두 값만 CapturedCueMediaMapping으로 보존한다. 직접/대체의 기존 Cue 제외는 유지한다.

**다음 1타부터 임시 적용**과 **리셋 후 임시 적용**은 공통 `TryApplySwordFrameTiming`에서 고정 수치/모션 버전·아트 바인딩·공속 해석을 검증한 뒤 행동/모션 후보 하나를 함께 게시한다. 진행 중 공격과 Cue는 기존 시작 버전을 유지한다. 잘못된/오래된 후보는 적용/리셋 전에 거부한다. **다음 1타부터 고정 실행 복귀**는 프레임 후보만 반환하며 진행 중 프레임 공격을 취소하지 않는다. 액터 리셋·사망/취소는 실험 정의 자체를 자동 삭제하지 않는다. 별도 수치/모션 창은 프레임 후보 적용 중에는 복귀 후 사용하며 해당 API도 충돌 적용을 거부한다. A/B는 아래의 전체 후보 캡처/복원 경로로 고정/프레임 모드를 함께 비교한다.

프레임 원본 저장은 유효 후보를 명시 저장한다. 첫검 변환은 MeleeFrameCompiler.ProjectFirst, 나머지는 같은 가족 ProjectBasic/ProjectDirect를 사용하고 실제 전후 diff를 확인한다. 활성 프레임/마커가 유일한 시간 원본이고 fixedBinding/공유 registry는 기존 매체 출처다. 피해/형상은 기존 수치 원본에 남는다. 영구 고정 복귀는 가족 창의 명시 ReturnFixedSource 경로이며 임시 런타임 복귀와 구분한다.

FrameMotionSource는 첫검 위임 파일의 1MiB/바이트 해시/원자 저장 계약을 유지한다. 생성 실패는 저장 원본을 보존하며 재시도할 수 있다. 생성기는 motionSources와 meleeFrames DTO를 운반할 뿐 마커/전진 의미를 복제하지 않는다. Game/Lab 시작과 빌드는 같은 MeleeFrameCompiler/Catalog로 전체 20개와 첫검을 검증하며 무효/누락/바인딩 불일치는 고정 실행으로 대체하지 않는다.

FrameMotionBuildValidation은 같은 C# 컴파일러로 canonical 기본/직접 수치·모션 registry·첫검 위임·전체 20개 가족의 생성 일치를 검사한다. 오래되거나 누락된 원본은 Player 빌드 전에 거부하며 생성기를 자동 실행하지 않는다. 원본 저장/생성과 현재 전투 적용을 구분하고 창 종료가 진행 후보를 임의 반환하지 않는다. 다른 정책의 FrameTime·제품 아트·패키징/프로세스 재시작 검증은 별도 범위다.

## 프레임 실험 파일

창의 **프레임 실험 파일 저장**은 현재 유효한 후보의 프레임 ID/초·마커 기준/참조/오프셋, 고정 수치/모션 원본의 ID·내용 해시와 전체 후보 해시를 버전 1 `FirstSwordFrameExperiment` JSON으로 기록한다. 피해·형상·아트 경로·장비·월드/입력/난수 상태를 파일에 복제하지 않는다. 이 파일은 재사용할 편집 프리셋이며 전체 시나리오·제품 세이브·두 번째 전투 수치 원본이 아니다. 공유 프리셋은 `data/experiments/`, 일회성 실험 산출물은 Git 제외 `outputs/`를 사용한다. 기본 파일 선택 위치는 `outputs/`이며 저장소에 자동 파일을 추가하지 않는다.

**프레임 실험 파일 불러오기**는 현재 저장소 원본의 고정 또는 영구 프레임 모드를 읽고 수치/읽기 전용 매체 버전·종류·ID·프레임/마커/후보 해시를 검증한 뒤 편집 Draft를 한 번에 바꾼다. 다른 원본 버전·누락/순환/무효 그래프·변조 파일은 기존 Draft와 실행 중 후보를 유지한다. 불러오기만으로 전투를 적용/리셋하거나 영구 원본을 저장하지 않는다. 새 창·새 Play Mode에서 기존 명시적 적용 버튼으로 같은 후보를 재실행할 수 있으며 실험 파일 자체를 프로젝트 시작에 자동 로드하지 않는다.

`FramePresetSource`는 최대 1MiB를 읽기/해석 전에 제한하고 BOM 없는 결정적 직렬화와 기존 `SourceFiles`의 덮어쓰기 없는 원자적 생성·읽었던 바이트 해시를 확인한 원자적 교체를 사용한다. 기존 경로는 호환되는 실험 파일이어야 하며 외부 수정/삭제를 덮어쓰지 않는다. 마커 선언 순서가 달라도 파일/후보 의미가 같으면 같은 직렬화/해시를 사용한다. 원본 버전이 달라지면 실험 파일을 조용히 새 수치에 재기준화하지 않는다.

창은 실험 파일 경로와 저장값 대비 미저장을 표시하고 파일 해시/실제 저장/전투 적용은 Undo 대상과 구분한다. Undo/Redo는 편집값만 바꾸며 Play Mode 전환은 파일 연결과 Draft를 유지한다. **현재 원본에서 다시 가져오기**는 실험 파일 연결을 해제하고 현재 고정/영구 프레임 원본의 후보로 돌아간다. 실험 파일 저장·불러오기 실패는 기존 파일 연결/후보를 유지하며 빌드용 원본·생성 카탈로그·공유 아트를 변경하지 않는다. 영구 원본 저장은 위의 별도 버튼이다.

## 이동·대시 Editor 편집

`Tools > deviant > Experiments > Locomotion`에서 일반 이동·기본 대시·대시 점프 취소 수치를 편집한다. 창은 원본 대비 변경값, 검증 실패 이유, 미저장 상태, 편집/실행 내용 해시와 예상 대시 거리를 표시한다.

- **전투 리셋 후 적용**: Play Mode에서 선택한 공통 전투에 임시 프로필을 적용한다. 파일은 쓰지 않는다. 일시정지 상태에서도 사용할 수 있다.
- **원본 저장 · 생성물 갱신**: 읽었던 원본 파일의 바이트 해시를 확인한 후 같은 디렉터리의 임시 파일을 원자적으로 교체하고 생성물에 복사한다. 외부 수정·삭제를 발견하면 원본을 덮어쓰지 않는다. 두 파일을 한 트랜잭션으로 교체하지 않으므로 원본 저장 후 생성물 갱신이 실패하면 이를 구분해 표시한다. 원본을 보존한 채 재시도하거나 CLI로 재생성한다.
- **원본 재로드 (편집 폐기)**: 현재 원본과 해시를 다시 읽는다. 실행 중 프로필은 자동 변경하지 않는다.

Undo/Redo는 임시 편집 값만 다룬다. 파일 저장·실행 리셋·충돌 검사 해시는 되돌리지 않는다. Play Mode 진입의 도메인 재로드에서도 창의 임시 값을 유지하며 종료만으로 파일을 저장하지 않는다. JSON 저장과 적용은 C# 재컴파일 없이 수행한다. 실제 원본 경로는 창에 표시하고 생성 카탈로그를 편집 원본으로 사용하지 않는다.

이 창은 이동 프로필의 편집 경로이며 검 1타와 고정 전투 시간의 모션은 위의 별도 창에서 편집한다. 전체 스프라이트 시간표와 범용 입력 녹화는 후속 범위다. 임의 대시 지속시간의 tick 내부 구간 분할도 아직 기존 물리 스텝 제한을 따른다.

## 검·대시 공통 A/B

1. `CombatV2Arena`를 Play Mode로 실행하고 `Tools > deviant > Experiments > Sword and Dash A-B`를 연다.
2. 현재 적용 정의를 A로 기록한다. 이동/검/프레임 시간 창에서 후보를 편집하고 런타임에 적용한 뒤 B로 기록한다. 미적용 임시 편집값을 자동으로 읽지 않는다. `SwordDashVersion`은 유효 수치/이동과 선택적인 전체 프레임 후보·그 기반 고정 정의를 함께 보관한다. 창의 버전 표시는 고정/프레임 모드와 후보 해시를 구분하고, 프레임 캡처는 Play Mode 전환에도 유지한다. 프레임 캡처가 유실/무효하면 수치 모드로 조용히 대체하지 않는다.
3. **같은 입력으로 A/B 실행 · 전투 리셋**을 누른다. 기본 입력 모드의 고정 시나리오 `lab-sword-dash-wall-v1`이 같은 기존 지형·빈 능력치 슬롯·오른쪽 방향을 사용한다. 플레이어 x=0/허수아비 x=3에서 0초 기본 공격·0.6초 대시를 1.2초까지 실행하고, x=11.8에서 기존 x=13 벽을 향한 대시를 별도로 0.4초 실행한다. 다른 적은 x=35로 옮기고 AI를 끈다.
4. HP 감소·첫 적중이 관측된 0.02초 구간·평지와 벽 앞 이동 거리·내용 해시를 비교한다. 변경한 공격이 0.6초 대시에 취소되거나 범위가 닿지 않으면 적중 없음도 유효한 결과다.
5. 비교 후 기존 장비·이동·고정/프레임 모드와 기반 고정 정의·적 공격 설정으로 전투를 리셋한다. 원래 프레임 모드였으면 같은 원본 버전의 프레임 후보를 복원하고, 고정 모드였으면 임시 프레임 후보를 남기지 않는다. 중간 버전 적용/리셋 실패도 같은 복원 경로를 시도한다. 기존 HP·진행 중 행동은 복원하지 않는다. 결과를 채택할 때 각 편집 창에서 명시적으로 저장한다.

A/B 기록은 창의 불변 정의 복사본이며 프레임 후보를 포함한 모드와 기반 소스도 보관한다. 실행 전 A/B와 원래 모드의 프레임 아트/스텝 바인딩·Sprite 개수를 검증해 잘못된 후보는 전투 리셋 전에 거부한다. 기본 모드에서는 성공한 수동 틱 뒤 A의 제출 표본을 불변 입력 기록으로 만들고 B가 같은 기록을 재생한다. 불러온 입력 모드에서는 두 버전 모두 선택한 표본을 사용한다. 비교는 전투 수치 원본 파일을 쓰지 않고 전투 규칙을 별도로 계산하지 않는다. 수동 실행권을 중복 획득하거나 기존 입력 차단을 해제하지 않는다. 본 게임에서도 같은 수동 틱 경계를 검사하지만 이 고정 시나리오 도구는 전투가 하나만 존재하는 기존 시험장만 받는다. 기존 전역 2D 질의가 다른 전투에 영향을 주지 않도록 비활성 전투도 중복으로 거부한다. 범용 레이아웃 저장·입력 녹화·시나리오 편집·모션 재생 캡처는 후속 범위다. 수치 결과만으로 손맛·아트 검증을 완료 처리하지 않는다.

## 고정 시나리오 입력 파일

비교 창의 **입력 기록 내보내기**는 마지막 성공한 비교 또는 불러온 기록의 공격/벽 두 구간을 하나의 JSON 파일로 저장한다. **입력 기록 불러오기**는 파일 버전·구간 ID·0.02초 간격·60/20틱 표본 수·입력 값·각 내용 해시를 검증한 뒤 재생 선택을 바꾼다. 잘못된 파일은 기존 선택을 유지한다. A/B 수치·장비·초기 배치·지형은 입력 파일에 들어 있지 않으므로 각 실행의 기존 시나리오 초기화를 사용한다. **기본 입력 사용**은 파일 선택을 해제하고 위의 기본 스크립트로 돌아간다. 파일 불러오기만으로 전투를 리셋하거나 원본 수치를 바꾸지 않는다.

형식은 `SwordDashInputSource`의 스키마 **3** Editor 데이터이며 제품 저장 스키마나 전투 편집 원본이 아니다. 각 틱의 axis·specialHeld·Pressed·Released·climbAxis·heldSpecials를 내용 해시에 포함한다. 스키마 1~2 파일은 자동 호환하지 않으며 새 비교/내보내기로 교체한다. RGB/모션·보상·런 진행도 저장하지 않는다. 최대 1MiB를 넘는 파일은 읽기/해석 전에 거부한다. 새 경로는 같은 디렉터리 임시 파일의 덮어쓰기 없는 원자적 생성, 기존 경로는 읽었던 바이트 해시를 확인한 원자적 교체를 사용한다. 외부 수정·삭제는 덮어쓰지 않는다. 창에서 다른 기존 경로를 선택해 내보낼 때도 유효한 입력 파일인지 확인한다. 파일 쓰기/재생성은 비교와 별도의 명시적 동작이며 일반 녹화/진단 산출물은 Git 제외 `outputs/`에 둔다.

## 재사용 Lab 시나리오

Tools > deviant > Experiments > Reusable Lab Scenario는 기존 CombatV2Arena 한 개만 받는다. Capture Initial은 실제 정적 ArenaSurface/ArenaPit 박스 서명·레이어/one-way, 플레이어/세 적의 명시 fixture key·프로필·위치·방향·spawn·초기 HP/실드/남은 방어, 두 무기/능력치 슬롯·선택, 카탈로그/매체 해시·이동·근접 17타·첫검 모션/프레임과 ActionTrackDefinition.SupportedIds 전체 그래프를 캡처하며 전투를 바꾸지 않는다. 지원 개수를 복제하지 않고 실제 ActionTrackCount·모든 ID/SourceVersion/ContentHash·현재 원본 메타데이터를 검사한다. 미지원 지형/Native 바인딩·누락 그래프는 캡처/선택/reset 전에 거부한다.

DirectMeleeSpecialDefinition.SupportedIds의 전체 직접 특수도 FormId/SourceVersion/ContentHash와 완전 ToData로 캡처한다. 실제 DirectMeleeSpecialCount와 대조하고 현재 생성 카탈로그의 정책/자원 불일치는 reset 전에 거부한다. 수치 여덟 필드는 저장된 시작 후보로 재생하고 종료 시 실행 전 전체 직접 정의·기존 기본/유한/모션/이동/장비를 복구한다.

ChargedMeleeDefinition.SupportedIds 전체도 완전 ToData·FormId/SourceVersion/ContentHash로 캡처하고 실제 ChargedMeleeCount·현재 생성 정책을 같은 TryCreate로 검증한다. 최소/최대 유지/피해·활성·기본 종료·박스를 저장하지만 진행 중 해제/유지 실행 상태를 저장하지 않는다. 재생/복구는 차지와 기존 모든 정의/이동/장비를 함께 적용한다. 내장 이동 스키마도 독립적으로 검증한다.

전체 AuthoredSkillDefinition 일반 특수·RangedActionCatalog 원거리 공유 source group·MotionSourceRegistry 팔레트/clip/typed 변환/대체 바인딩도 완전 ToData/ToSnapshot/graph와 정책/내용 증거로 캡처한다. 로드/저장/Deserialize는 명시 현재 canonical Catalog를 요구하고 CatalogHash를 컴파일 전에 확인한다. 실제 선택 arena 또는 offline 생성 카탈로그를 명시 공급하며 별도 inventory/게임플레이 재구성기를 만들지 않는다. 모션 전체 hash는 캡처한 실제 첫검 Motion과 registry BindingHash를 결합하고 첫검 시간을 graph에 복제하지 않는다.

수동 입력의 axis/Pressed/Released/held와 반복을 실제 공통 tick에 제출해 성공한 1..4096 tick만 기록한다. 간격은 0.0001..1초다. 현재 canonical JSON 버전과 필수 코드/질의 provenance는 위 스키마 계약을 따르며 이전 형식을 자동 채우지 않는다. 외부 수정/현재 catalog·정책·known palette/중복 원본 오류는 선택/reset 전에 거부한다. 새 창/Play에서 저장한 전체 정의·모션 graph·첫검 위임·초기 조건을 재생하고 finally에 이전 모든 family를 복구한다. 현재 비행/차지/월드 실행 상태 저장은 아니며 부분 A/B를 완전 저장으로 오인하지 않는다.

Replay는 임시 ManualSimulation 하나만 얻고 기존 lease·입력 차단·정지를 빼앗지 않는다. Lab Execution 도구는 먼저 Release해야 한다. 성공/실패 후 기술 초기 설정/정의 복구를 시도하고 자기 lease를 반환하며 진행 중 행동/속도/상태/발판 이력/충전 부채를 저장하지 않는다. seed는 명시 provenance일 뿐 RNG를 만들거나 전역 난수를 바꾸지 않는다. 조회 지표와 성공 tick 동안 실제 CombatV2 DiagnosticEvents 순서를 비교하지만 권위 있는 행동 결과 버스나 일반 Game/Run 저장·플랫폼 간 결정성을 뜻하지 않는다.

관측 결과는 모든 실제 명령 상태/이유·tick별 두 슬롯 충전 값·수락 타격을 구분한다. UI의 자원/명령/결합 trace와 별도 인과 관계 `IdentityTraceHash`는 변경 전후 차이를 비교하고 기존 `HitTraceHash`는 수락 타격만 비교한다. 원시 ID는 보존하되 교차 실행 비교는 fixture와 최초 관측 관계로 정규화한다. 커서 유실/overflow·세션 변경은 명시 실패다. 실패한 시도는 `FAILED`로 표시하고 원래 예외·완료 수·이미 발생한 부분 명령/타격을 노출하며 이전 성공 결과를 덮어쓰지 않는다. 자원 용량/회복 정책은 읽기 전용이며 새 수치 편집 기능이 아니다.

## Game / Lab 라이브 입력 관측

같은 `Sword and Dash A-B` 창에서 초기화된 Game 또는 Lab 전투를 선택하고 **라이브 입력 관측 시작**을 누른다. `CombatInputRecorder`는 실제 공통 `CombatInputIngress`의 완료 경계를 poll한다. 호출자가 제출한 값을 가짜 전달하거나 콜백으로 전투를 재진입하지 않는다. 관측 시작 때만 원시 ring 저장소를 확보하고 전투 중 기록/소비/완료는 할당·사용자 콜백 없이 값만 복사한다. 불변 배치 생성은 관측자 poll에서만 수행한다. 완료 경계는 256개만 보관하며 미수집/보관 한도를 넘은 오래된 관측은 버리고 유실 수를 표시한다. 사용자 지정 수집 용량은 `경계 수 × ingress 표본 용량 ≤ 131072`를 시작 전에 검사한다. 창에는 최근 8개 경계를 보여 주고 `LiveInputBatches`는 보관 중인 불변 스냅샷을 제공한다. **관측 중지 · 기록 유지** 또는 창 닫기는 수집만 종료하며 게임 입력/시계를 바꾸지 않는다.

각 표본은 액터·세대·epoch 토큰, 단조 증가 순번, 장치 ID/입력원, 명시적인 시계 종류와 시각을 가진다. 키보드·패드는 [공통 액션 소스](../presentation/ui/input-devices.md)에서 native 액션 콜백 시각을 `Realtime`으로 보존하며, `LocalInput`의 단일 수집과 전투 변환기를 거쳐 같은 ingress에 접수한다. 한 Update 이전의 반복 press/release도 합치지 않는다. 한 native 이벤트의 동시 액션만 이동 → 무기 1/2 기본 → 무기 1/2 스킬 → 대시 → 점프 순서를 사용한다. ↑/↓·스틱 Y는 기존 `climbAxis`, 좌우·스틱 X는 기존 정수 axis, Q/W·trigger는 슬롯별 스킬/유지 입력으로 변환한다. 점프/내려가기 해제는 누를 때의 버튼에 대응한다. `Keyboard=1`을 유지하고 `Gamepad`·`OtherDevice`는 양수 장치 ID와 realtime 시각으로 접수한다. 콜백에서 전투를 실행하지 않고 기존 Update에서 캡처 소유권·제한·세대를 확인하며 오래된 차단·수동 소유·재바인딩 입력을 새 공격으로 바꾸지 않는다. 수동 `SubmitInput(CombatInput)`/재생은 같은 ingress의 `Simulation`/Now 경로를 사용한다. 연속 axis/held는 최신 값이고 같은 tick의 Pressed/Released는 기존 bit union이다. 이력 순서로 공격을 추가 실행하거나 중재 우선순위를 바꾸지 않는다.

`Admitted`는 접수만 뜻한다. `Consumed`는 기존 행동 중재 뒤 edge-clear 경계를 지난 뜻이며 공격 성공을 뜻하지 않는다. 시뮬레이션 진입 `SimulationTickCount`/`Now`와 별도로 전체 tick의 `Completed`/`Faulted` 및 관측 종료 `ObservedThroughNow`를 표시한다. 전체 tick이 끝나기 전에는 소비 배치를 게시하지 않으므로 뒤 단계 예외를 성공한 tick으로 오인하지 않는다. 직접 플레이어만 갱신한 경계·차단/정지/사망/초기화 폐기는 `NotObserved`이며, 폐기 표본은 다시 실행하지 않는다. 모든 reset은 옛 세대 토큰을 무효화하고 NewEpoch만 epoch·게임 시계를 새로 시작하며 axis/held도 비운다. 일반 KeepTime은 기존 axis/held 유지 정책을 따른다. ingress 자체는 액터당 최대 512개 미소비 개별 표본만 받고, 새 공개 표본 경로는 한도 초과를 변경 없는 `RejectedCapacity`로 반환한다. 기존 값 제출 경로는 분해할 전체 표본의 공간을 먼저 검사해 일부만 적용하지 않고 거부한다.

라이브 기록은 메모리 진단이며 고정 A/B 파일로 내보내거나 실패한 tick을 재생 입력으로 승격하지 않는다. 개별 edge 파일 저장/자유 시나리오 재생·패드/장치 이벤트 원시 기록·초기 월드/장비/정의 저장·행동별 성공 결과와 플랫폼 간 결정성은 이 기능의 범위 밖이다.

## 에셋 준비

Unity `.meta`와 현재 진입 씬은 메인에서 받는다. 이미지 준비를 위해 구형 시험 씬을 생성하지 않는다.

이미지는 고정 리소스와 [공통 배치 계약](../../assets/README.md)으로 준비한다. 일반 배치는 Pillow나 원본 ZIP 입력을 요구하지 않는다.

```sh
python tools/assets/update_assets.py --check
python tools/assets/update_assets.py
```

확정 무기의 원본 바이트·다른 세트·이미지 내장 편집 데이터는 `resources`의 색인으로 찾는다. 메인의 기존 피벗·고정 GUID·모션 registry·첫검 위임·FrameTime 시간표와 시각물 카탈로그를 유지한다. `ApprovedWeaponCatalog.json`은 아트 바인딩이며 게임용 시간표를 우회하는 원본이 아니다. 게임 원본을 편집한 경우에만 기존 `export_attack_motion.py` 등 게임 생성기로 카탈로그를 재생성한다.

원본/PNG SHA256과 출처는 외부 색인이, 배치 목적지·GUID는 메인이 소유한다. 생성 결과를 편집 원본으로 사용하지 않으며 진단 출력은 Git 제외 `outputs/`에 둔다.

## 마을 확정 몬스터의 임시 공격

`data/combat/stage1_village_monsters.json`은 새 네 종의 마을 시험용 배치 대응·몸통/공격 박스·예고/타격 시점·돌진·탄환·갈고리 수치다. 최종 밸런스 원본과 구분하는 `playtest-prototype-not-balanced` 상태를 유지한다. `python tools/combat/export_stage1_village_monsters.py`가 기존 `data/monsters/chapter1_approved.json`의 공통 감지·경직과 종별 초기 체력/피해를 가져와(종별 `senseX`가 있으면 가로 감지 거리만 덮어씀) `Assets/Deviant/Stage1Monsters/Game/Data/VillageMonsterContent.json`을 만든다. 모션 길이는 `resources/authoring/workbench/source/MonsterReference/approved`에서 읽고, 그림·프레임을 수정하지 않는다. 생성 JSON만 직접 수정하지 않는다.

공격 전조(준비 자세)를 늘리려면 종별 `prepareHoldFrame`(공격 모션의 유지 프레임 번호)과 `prepareHoldExtra`(더할 초, 0 이상)를 넣는다. 실행 중에는 그 프레임을 `prepareHoldExtra`만큼 더 보여 주고, 그 뒤의 승인 시간(나머지 프레임·`damageStart`/`damageEnd` 판정·공격 이펙트)과 뇌의 `prepare`를 같은 양만큼 민다. 승인 모션 원본의 프레임 시간은 바꾸지 않는다. 유지 프레임은 `prepare` 이전에 끝나야 하며(내보내기 도구가 검사), 필드가 없는 종은 늘리지 않는다. 에디터·개발 빌드의 F12는 이 값을 +0/+0.2/+0.4/+0.6초로 덮어 비교한다(저장하지 않음, 배포 빌드에는 없음). 종별 `attackEffectScale`(기본 1)은 승인 공격 이펙트(일반 공격 그림 한 줄, 예: 먹빗자루 먼지)를 게임에서만 키우는 배율이다. 그림의 바닥 피벗에서 커져 바닥에 붙은 채 앞쪽으로 늘어난다. 먹빗자루는 먼지가 공격 판정 앞끝(4.25유닛)에 닿도록 1.5배다(그림만 2.9유닛). 승인 프레임은 바꾸지 않는다.

전조 효과(떨림·번쩍임)는 종별 `windupCueFrame`(효과가 시작되는 공격 프레임 번호, 없으면 효과 없음)과 파일 상단의 공통 `windupCue`를 따른다. 공통 값은 `shakeStepSeconds`(떨림 간격), `shakeWidenFrom`(좌우로 넓어지는 구간 비율), `flashSeconds`(피해 직전 번쩍임 길이), `flashColor`(RGB), `tintKinds`(전조 동안 붉어지는 근접 공격 종류), `tintColor`(RGB), `tintMax`(붉은 덧그림 최대 불투명도)다. 내보내기 도구는 효과 시작 프레임이 `damageStart`보다 앞서는지와 공통 값의 범위를 검사한다. 효과는 피해 시작(`damageStart` + 준비 자세 유지 시간)에서 끝난다.
## 근접 이동형 끝까지 추격

`data/combat/enemy_pursuit.json`의 `pursuit`가 감지 후 끝까지 추격하는 근접 이동형 목록(`appliesTo`)과 추격 속도 배율·점프 최대 높이(상한)·점프 중 가로 속도·웅크림·최대 낙하 높이·경로 재계산 주기·감지 거리 배율·하향점프/절벽 낙하 사용 여부, 추격 중 무작위 점프(`randomHop`, 간격·높이 범위)와 경로 점프·하향점프 전 대기 범위(`pathJumpDelayMin`/`Max`)를 담는다. `screenLeash`는 화면 이탈 후 포기 시간·화면 밖 감지 개체의 미진입 포기 시간·복귀 순간이동 시간·경로 막힘 순간이동 시간이다(판정 화면은 아레나의 `ViewCamera`). `pursuit.targetFacingDeadzone`은 일반 몬스터가 플레이어의 root X를 바라볼 때 현재 방향을 유지하는 폭(0.125u=4도트)이며 실제 경로 이동 방향에는 적용하지 않는다. `individuality.individualVariance`는 개체 편차 폭, `alert`는 발견 반응 시간·연쇄 경보 반경·거리당 지연·무작위 지연·"!" 표시 시간·경보 수신 종이다. 개체 무작위 값은 월드가 `EnemyRandom.SetSeed`로 넘긴 스테이지 생성 시드(1챕터 `generationSeed`, 2챕터 레이아웃 시드)에서 정해진다. `python tools/combat/export_enemy_pursuit.py`가 검사 후 `unity/Assets/Deviant/Combat/Resources/EnemyPursuit.json`을 만들고 `--check`로 최신 여부를 확인한다. `rangedRange`는 원거리·고정형 사정범위의 조정 기록이며 Unity로 내보내지 않는다. 실제 값은 각 종의 원본(`stage1_village_monsters.json`의 `startGap`, `chapter1_new_monsters.json`의 `activationDistance`, `stage2_monster_stats.json`의 `attackStartRange`)에 반영하고 해당 내보내기 도구로 생성한다. 술통 포격수의 포격 시작 거리는 `activationDistance`를 따른다. 수치 시험은 `tools/combat/monster_pursuit_tuner/index.html`을 브라우저로 열어 같은 경로 규칙으로 하고, 패널의 JSON을 원본에 옮긴다. 런타임은 `Deviant/Combat/Runtime/EnemyPursuit.cs`다.

## 수동 체크포인트 모션 연결

승인 모션 이미지는 [벽·포탈 배치 절차](../../assets/world/approved-traversal-and-village.md)를 사용한다. `ApprovedTraversal/Catalog.json`과 메인 소유 단계별 모션·시간 원본은 이미지 배치에서 다시 생성하지 않는다. 게임 시간 변경이 있을 때만 아래 공통 내보내기로 Unity 수치 카탈로그를 갱신한다.

원본은 `data/combat/manual_checkpoint.json`이며 `python tools/combat/export_unity_catalog.py`가 `unity/Assets/Deviant/Combat/Resources/ManualCheckpoint.json`을 생성한다. `installSeconds`(2.0), `enterSeconds`(0.64), `appearSeconds`(0.8)는 각 단계의 전투 시간이다. 모션과 포탈 그림은 이 시간에 맞춰 같은 비율로 빨라진다. 양수·유한값과 schemaVersion 1을 검증하고 씬 초기화 때 불변 프로필로 읽는다. 편집 후 생성기를 실행하고 Play를 다시 시작한다.

`installMotion`, `enterMotion`, `appearMotion`에 각각 `frames`(Resources 기준 확장자 없는 Sprite 경로 배열)와 `seconds`(프레임별 양수 시간 배열)를 같은 길이로 넣는다. 빈 배열은 해당 단계의 임시 연출을 사용한다. 세 모션은 별개이며 기존 공격 카탈로그를 변경할 필요가 없다. 캐릭터 Sprite는 기존 발 원점·PPU 32 규격을 사용한다. 원본 프레임 시간 비율을 유지하며 해당 단계의 전체 시간에 맞춰 재생하므로, 완성 모션 길이를 그대로 사용하려면 대응하는 단계 시간도 프레임 시간 합으로 설정한다. 연출은 상태를 읽기만 하며 이동·무적 확정은 CombatPlayer의 전투 시계가 처리한다.

`Chapter1ManualCheckpointTests`는 실제 플레이어·충돌을 사용해 설치/교체/반복 귀환, 원웨이 발판, 장애물 재검사, 피격 취소/출현 무적, 사망/리셋 삭제, 키 입력과 카메라 이동을 검증한다. 본 게임의 해금·챕터 전환 저장 기능으로 확대 적용하지 않는다.

## MVP 색상 잉크·팔레트 성장

`weapon_growth_mvp_proposal.json`의 정책과 `weapon_growth_mvp_tuning_draft.json`의 획득처 확률·가격·강화 배율표, `ink_color_parts_proposal.json`의 파츠 정의, `ink_color_parts_tuning.json`의 파츠별 고정 등급·강화 대상·적용 대상·임시 아이콘을 사용한다. `python tools/combat/export_weapon_growth.py`는 색상 잉크 54종의 고정 등급·표식 순번(`markIndex`)·슬롯 +0..+5 값(사약의 최대 중첩은 단계별 표)·쿨타임·획득처 확률·가격·무색 확률을 `unity/Assets/Deviant/Combat/Resources/WeaponGrowthMvp.json`으로 투영한다. 파츠마다 강화 대상 수치의 단계별 표시 문구 `stats`(카탈로그 생성기와 같은 이름·형식, 예: `비율` `20%`…`35%`)도 함께 내보내며 팔레트 관리 화면이 선택 칸의 단계 값을 보여 줄 때 쓴다. 무지개 표식의 최대 개수·유지시간·하나씩 터지는 간격은 `weapon_growth_mvp_tuning_draft.json`의 `mark.cap`·`durationSeconds`·`popIntervalSeconds`를 `markCap`·`markDuration`·`markPopInterval`로 함께 내보내며, C# `GrowthData.Validate`가 1~7개와 양수 시간을 검사한다. `--check`는 생성물 일치를 확인한다. 기존 구형 `RuntimeInkParts.json`과 `TrainingInkParts.json`을 덮어쓰지 않는다. 새 런의 `WeaponGrowth`가 색상 잉크와 팔레트 인스턴스를 보유하며 기존 형태·특수 잉크 가방과 구분한다.

## 신규 일반 몬스터와 선택 방 프로토타입

신규 포격수·돌진체 수치·판정·행동의 원본은 `data/combat/chapter1_new_monsters.json`이다. `python tools/combat/export_chapter1_new_monsters.py`는 `Chapter1NewMonsters.json`만 생성하고, 개별 PNG 두 장은 공통 리소스 배치가 운반한다. `Chapter1NewMonsterImporter`는 Single·PPU32·피벗 (0.5, 0.05)·Point·무압축·밉맵 없음·FullRect를 사용한다. 선택 원본 PNG 바이트를 보존하고 스프라이트 시트는 생성하지 않는다.

1·2챕터 방 원형의 원본은 `data/map/block_rooms.json`이다. 1챕터 봉인 도전방·잠긴 상자방의 형태 A·B·C(지형, 지워지는 발판 표시, 석판·상자 자리)도 이 파일의 원형이다. `node tools/map/export_block_rooms.cjs`가 `ForestCombatV2/Resources/BlockRooms.json`과 배치기 일치 시험 기준을 생성하고 `--check`로 일치를 확인한다([블록 맵](../gameplay/world/block-map.md)). 이전 방 편집본 `data/combat/chapter1_selected_rooms.json`과 `python tools/combat/export_chapter1_selected_rooms.py`가 만드는 `Chapter1Rooms.json`·`Chapter1SelectedRooms.json`은 현재 맵 생성에 쓰지 않으며, `Chapter1Rooms.json`의 나무 발판 타일 번호만 둥근 지형 표시에서 참조한다. 기존 일반 몬스터의 HTML 콘텐츠 ID는 런타임 카탈로그의 과거 배치 ID로 컴파일한 후 기존 마을 매핑을 거쳐 동일 종으로 연결한다.

`Chapter1PrototypeEnemyTests`는 실제 `Chapter1Procedural` 씬과 공통 시뮬레이션에서 새 몬스터를 검증한다. 사용자 실행 확인은 Unity 6000.6.5f1의 `Tools → Lost Words → Chapter1 → Open Random Stage`를 사용한다. 기본 자세와 코드 연출은 애니메이션 승인 완료와 구별한다.

`data/combat/roguelite_progression.json`은 시작 마을의 영구 해금·마법안료 원본이다. `python tools/combat/export_roguelite_progression.py`가 ID·선행·가격·묶음 구성·환급 목록과 공방 트리 줄기(`branches`의 ID, 해금마다 알려진 `branch`, 줄기마다 뿌리 하나 이상, 같은 효과 단계의 같은 줄기)와 노드(`node`: 단계가 여럿인 효과는 4단계씩 `<효과> I`·`II`, 한 번 사는 항목·묶음은 숫자 없는 단독 노드, 노드 안은 앞 단계만 선행, 다른 노드로의 연결은 노드 첫 단계에서 앞 노드의 마지막 단계로)를 검증해 `unity/Assets/Deviant/Hub/Resources/RogueliteProgression.json`을 만든다. `--check`로 최신 여부를 확인한다. 미정 가격은 `costStatus: "pending"`과 `cost: null`을 함께 유지한다. Unity의 null 숫자는 0으로 읽히므로 런타임은 `costStatus`로 구매 가능 여부를 판단한다. `trial-1`·`trial-2`·`trial-3`·`confirmed`는 결정된 가격 상태다.

현재 `unlocks`에는 유지 능력치·지원금·대시, 공용 리롤·충전소 추가 이용·50% 부활·무기 파괴 골드·진열 확장·출발 추가 보상·출발 잉크 보상·처치 골드와 잉크·증강 묶음이 있다. 묶음은 `effect.parts`의 실제 콘텐츠 ID를 검증하며 내구도 대기 증강은 넣지 않는다. 잉크 묶음은 `weapon_ink_run_design.json`의 런 잉크 ID만 담고 기본 공격·스킬 발동 잉크는 넣지 않는다. 한 잉크·증강은 한 묶음에만 속한다. 공방은 선행 해금 트리이므로 각 항목은 `prerequisites`의 모든 ID보다 뒤에 나열해야 하며, 내보내기 도구가 순서를 검사한다. 건물의 `openAtPieces`는 마을 작업대 열림·연출에만 쓰고 그 건물 항목의 구매 조건에 더하지 않는다. 항목 자체의 `openAtPieces`만 구매를 기다리게 한다. 단계 효과는 해당 단계의 합계다. `revision`과 `retiredUnlocks`는 기존 프로필의 제거 해금 일회 환급에 사용한다. 같은 ID를 현행과 제거 목록에 함께 두지 않으며, 무료 디버그 강제 해금에는 구매가 환급을 지급하지 않는다. 이전 `targetRevision` 트리는 현행 입력에서 제거했다.

마을 대사만 바꿀 때는 `python tools/assets/build_hub_bindings.py --visits-only`를 사용한다. 같은 명령에 `--check`를 붙이면 `data/story/village_visits.json`의 생성 사본만 확인하며 미디어 배치나 원본 픽셀을 변경하지 않는다.

시작 마을의 이미지 55장(건물·작업대·우물·출발 문·문양·효과·이야기 배경)은 `data/assets/starting_hub.json`의 선택과 공통 `unity-deployment.json` 계약으로 준비한다. `tools/assets/update_assets.py`만 PNG를 운반한다. 메인의 `tools/assets/build_hub_bindings.py`는 고정 리소스의 크기·해시를 확인하고 피벗·PPU 색인 `Hub/Data/hub-sprites.json`, 게임 배치 `Hub/Resources/HubLayout.json`, 이야기 `Hub/Resources/HubStory.json`만 생성한다. `--check`는 기존 이미지와 세 JSON의 일치를 확인하며 이미지나 배치 계약을 변경하지 않는다. `HubArtImporter`의 기존 Unity 설정·GUID와 마을 씬 연결은 유지한다. 마을 씬 생성은 기존 `Tools/deviant/Hub/Build Starting Village Scene`의 별도 명시적 작업이며 일반 이미지 준비에서 실행하지 않는다.

`stage1_village_monsters.json`의 선택적 `noPlayerHitReaction`(현재 우물 도르래 수문장만 true)은 그 몬스터의 근접 피해에서 플레이어 피격 입력 잠금·밀림을 되돌리고 피해만 남긴다. 선택적 `damage`·`maxHP`는 현재 마을 4종의 피해·최대 체력을 명시하며 구 들판 몬스터 원본 수치는 바꾸지 않는다. `python tools/combat/export_stage1_village_monsters.py`로 생성 콘텐츠를 갱신한다.

맷돌 돌진 종료 후 3초 회복은 `chapter1_new_monsters.json`의 `cooldownAfterChargeSeconds`가 정한다.

## 2챕터 A3 전용 엘리트

`data/combat/chapter2_elite.json`이 편집 원본이다. `python tools/combat/export_chapter2_elite.py`로 `unity/Assets/Chapter2/Resources/Chapter2Elite.json`을 생성하고 `--check`로 일치를 검사한다. 레이아웃 데이터는 이 도구가 편집하지 않는다. [본 게임 연결](../gameplay/enemies/chapter2-elite.md)을 따른다.

가방의 보관 한도 원본은 `data/inventory/temporary-inks.json`의 `capacity`(5)이며 모든 종류의 잉크가 이 한도 하나를 공유한다. `export_inventory_content.py`는 가방 콘텐츠에, `export_weapon_growth.py`는 같은 한도를 `WeaponGrowthMvp.json.inkCapacity`에 내보낸다. 두 생성기와 각각의 `--check`를 함께 실행한다.

몬스터 처치 드롭이 몸에서 튀어나와 떨어지는 궤적 수치(솟는 높이·비행 시간·튕김 횟수와 높이·흩뜨림·시드)는 `data/presentation/loot_drop_toss.json`이 원본이다. `python tools/inventory/export_loot_toss.py`가 `unity/Assets/Deviant/Inventory/Resources/LootDropToss.json`으로 복사하고 `--check`로 최신성을 확인한다. 의미 검증과 범위 제한은 C# `LootTossSettings`가 한다.

2챕터 일반5종 확정 수치·판정·기존 프레임 시간과 선택 FX는 `python tools/combat/export_chapter2_approved_normals.py` (`--check` 지원, 기존 `export_chapter2_normal_attacks.py` 진입점도 같은 생성기로 연결), A3 승인 아트는 `python tools/combat/export_chapter2_elite_art.py`로 생성한다. 픽셀 원본은 리소스 저장소에 보존하고 메인의 배치 계약으로 복원한다.

A3 승인 모션 바인딩은 `data/combat/presentation/chapter2_elite_art.json`이다. `python tools/combat/export_chapter2_elite_art.py`가 `Resources/Chapter2EliteArt.json`을 생성하고 `--check`는 전투 데이터의 순간이동·연타·돌 파쇄 접점과의 일치도 검사한다. 원본 PNG는 리소스 저장소 `monsters/stage-02/elite/stone-fist/`에 보존하고 메인의 `tools/assets/register_chapter2_elite_art.py`로 선택·가져오기 메타데이터·배치 계약을 등록한다. 실제 PNG 운반은 공통 `tools/assets/update_assets.py`를 사용한다.

2단 점프의 공중 자세 원본은 `data/assets/air_jump_visual.json`이고 런타임 사본은 `ApprovedTraversal/AirJump.json`이다. 각 무기·인간·맨손의 상승·정점 그림 4장을 0.18초로 재생하며, `CharacterPresentationBinding.TryAirJump`가 `CharacterPresenter`에서 해당 그림을 선택한다. 일반 실행에서는 `JumpMotionPresentation`의 몸 늘리기·몸 잔상을 끄고 원래 배율과 이동 보간을 유지한다. `DoubleJumpPuff`의 발밑 효과는 별도로 유지한다. 인간·맨손은 main의 기존 PPU32·발 피벗·GUID를 유지한다. HTML 비교 화면은 같은 그림·수치로 재현한 미리보기이며 Unity 실행 검증을 대신하지 않는다.

발사기 `launcher_skills.json`의 레이저는 `ranged.unlimitedRange=true`, `distance=0`으로 고정 사거리 없는 수평 관통을 지정한다. Unity는 무한 방향 고체 질의 후 유한한 첫 벽 길이로 피해·표시를 제한하며 벽 없는 방향의 적도 거리 상한 없이 포함한다. 모아쏘기의 `ranged.impactBoxSize=2`는 적 충돌점 중심 정사각형 범위의 한 변 길이다. 충전 탄 반지름과 독립이며 적 명중 때만 발사 시점 피해를 대상마다 한 번 적용하고 고체 지형이 차폐한다. 이 필드는 정의 복사·스냅샷·해시·카탈로그 내보내기에 포함한다. 모아쏘기 `charge.steps=3`은 카탈로그 `chargeSteps`로 내보내며, `ChargeDefinition`이 0.20~0.80초 충전 구간을 같은 폭 세 단계로 나눠 충전량을 0·0.5·1로 끊는다. 피해(20·30·40)와 탄 반지름(0.15·0.225·0.3)이 같은 충전량을 쓴다. 0이면 이전처럼 연속으로 오른다. 단계 수는 원거리 행동 검증·해시·복사와 충전 정책 해시에 포함한다.

맨손 출발의 전용 2타 기본 공격은 `data/combat/unarmed_basic.json`이 소유한다. [승인 초기 공격 규격](../../design/equipment/weapons/rules.md#맨손-기본-공격)은 검 피해를 상속하지 않는다. `python tools/combat/export_unity_catalog.py`가 두 타격과 선택적 `advance` 지상 전진 정보를 `MeleeCatalog.json`에 내보내며 기존 전투 시전·충돌·피해·취소 처리를 사용한다. `python tools/combat/export_attack_motion.py`는 마을 맨손의 기존 이동 클립에 전용 `attack1`·`attack2`를 더한 `unarmed` 세트를 만들고 `data/combat/presentation/action_motion_registry.json`의 두 행동 바인딩을 함께 내보낸다.

아트 원본은 리소스 ID `characters.ink.weapons.unarmed`이며 곧은 잽 두 번 세트(`source/jab-combo.json`, 몸 11장)와 흰 초승달 타격 효과(`runtime/jab-fx/`, 1타·2타 각 5장)를 공간 픽셀화 없이 그대로 배치한다. `python tools/assets/register_unarmed_art.py`는 선택·배치 계약과 새 경로의 가져오기 메타데이터를 등록하고, 실제 바이트 운반은 공통 `tools/assets/update_assets.py`가 담당한다. 몸 그림은 640×360·PPU 106.6667·발 피벗 (220,327)으로 기본 대기 그림(128px·PPU32)을 10/3배로 그린 것과 같은 크기다. 기존 이동 그림의 PPU32·발 피벗·GUID는 유지한다. 시간 배분은 자세 0.10초 → 중간 0.02초 → 타격 순간(0.12초)에 다 뻗은 장이며 `unarmed_basic.json`의 `presentation`이 소유한다. 초승달 효과는 같은 파일의 `effects`(PPU32, 주먹 끝 기준 발밑 상대 위치)이며 `fxStart`=타격 순간부터 0.03초씩 재생한다. 이전 `approved-combo` 세트는 리소스 저장소에만 보존한다. 이 런 전투 공격과 [오프닝 먹빗자루 튜토리얼](../../design/story/opening/opening-playable.md)의 전용 주먹 처리·수치는 별개다.

## 망치·레이피어 기본공격 원본

리소스 `characters.ink.weapons.basic-outline`의 PNG를 원래 256×144 바이트로 사용한다. 망치는 사용자가 지정한 현재 모션 `runtime/hammer-current-motion-20261112/hammer-a/00..08`·`09..20`을 1·2타로 사용하고, 기존 긴 선딜 수치를 유지한다. 레이피어는 사용자 지정 폴더에서 보존한 `runtime/rapier-blade-revised-20261112/rapier/00..08`·`09..15`·`16..23`의 24장을 1·2·3타로 연결한다. `tools/assets/register_basic_outline.py`가 선택 계약·메인 소유 가져오기 메타데이터와 `action_motion_registry.json`의 본체 프레임을 연결하고, `export_attack_motion.py`가 게임 카탈로그로 투영한다. 실제 PNG 배치는 공통 실행기 `python tools/assets/update_assets.py --asset characters.ink.weapons.basic-outline`를 사용한다.

Point, 무압축, FullRect를 사용한다. 망치는 PPU32와 발 피벗 (104,16)px, 레이피어는 PPU35와 발 피벗 (72,16)px을 사용한다. 레이피어의 70px 준비 자세를 64px 대기 자세(PPU32)와 같은 2unit 높이로 표시한다. 타격 그림은 망치 `07`·`15`, 레이피어 `03`·`11`·`17`이며 기존 sourceImpact와 전체 본체 시간을 유지해 접점 전후를 나누어 재생한다. 공격 판정·피해·특수 모션의 원본은 그대로 사용한다.

레이피어 기본공격 이펙트의 배치는 `data/combat/presentation/basic_attack_effects.json`에서 편집한다. `rotation`은 오른쪽을 볼 때의 Z축 각도(도)이며, 1·2·3타는 각각 +15°·-6°·0°다. `x`·`y`는 발 원점 기준의 프레임별 위치이며 원본 이펙트의 PPU64·피벗·재생 시간은 유지한다. 본체 전진 중에는 현재 캐릭터 위치를 따라가고, 왼쪽에서는 위치와 각도를 함께 반전한다. 시작한 이펙트는 시작 시점의 배율·각도를 유지한다. 이 배치는 피해 판정이나 긴 선딜 망치의 수치를 변경하지 않는다.

레이피어 모아찌르기의 전용 준비·유지 본체는 `data/combat/presentation/rapier_charge_presentation.json`을 편집하고 `python tools/combat/export_attack_motion.py`로 재생성한다. `charge_prepare`는 한 번 재생하고 `charge_hold`는 준비가 끝난 뒤 반복한다. 그림은 `resources/characters/ink/weapons/rapier/runtime/charge-hold-direct-20261112/`가 소유하며 메인의 배치 계약으로 운반한다. 유지 자세는 실행 시작에 캡처하고 해제 전에는 공격 그림을 선택하지 않는다. 최소 준비·최대 자동 발동·피해·쿨타임은 `melee_weapon_design.json`을 계속 따른다. 해제 후 본체의 종료 시각은 실제 차지 해제의 `MotionEnd`를 사용한다. `RapierChargeHoldTests`는 실제 전투 시뮬레이션과 본체 선택 경로에서 조기 해제·중간 해제·최대 자동 발동·좌우 반전·차징 중 아트 편집·대시 취소를 검사한다.

레이피어 차징 유지의 기준은 기존 모아찌르기 `charge/015.png`다. 새 포즈를 생성하지 않고 원본 640×360·6색·PPU106.6667·발 피벗을 보존한 채 로브와 목도리만 정수 화소 단위로 움직인다. 머리·얼굴·눈·양팔·레이피어·발은 기준 컷을 보존하며, 왼손만 손가락 없는 둥근 검은 주먹으로 보정한다. 목도리/망토 끝의 검은 테두리 화소는 원본 천 색으로 정리한다. 준비 4장도 원본 `002/004/009/015`에서 같은 두 부분만 보정한다. 보정한 신체·무기 화소는 유지 8장 내내 동일하다.

근접 특수는 `melee_weapon_design.json`의 타격 대상 정책을 따른다. 직접 판정은 `all`, 화면 판정은 `screen`, 본타의 모든 적중 대상에 예약하는 매듭 조임은 `marked_target` 앵커와 `all_marked`를 사용한다. 레이피어·대검의 차지 정의도 `all`을 검증하며 이동용 최근접 대상 선택은 피해 대상 수와 분리한다. `build_melee_design.py`와 `export_unity_catalog.py`로 문서와 카탈로그를 재생성한다. `TargetlessSpecialWhiffTests`는 구현된 스킬 40종의 무대상 발동을, `MeleeSkillAreaTargetTests`는 다중 타격·표식 후속타·늦게 진입한 적·차폐와 좌우를 실제 공통 런타임에서 검사한다.

## 피해·적 HP 단위

검 기본 피해 65에 맞춘 무기별 기본·스킬 수치는 각 무기 원본에서 편집한다. `weapon_ink_run_design.json`의 `combatBalanceTrial.damageScale.factor`는 원본 적 투사체가 반사되어 적에게 주는 피해의 단위 환산(5)이며, 이미 환산된 무기 피해에 다시 곱하지 않는다. `export_run_inks.py`가 `damageUnitScale`과 열쇠 보유 몬스터의 `keyCarrierHp`를 내보낸다. 일반·엘리트·보스 HP와 보스의 파괴 가능한 패턴·페이즈 HP 기준·보스 대상 패턴 피해/회복은 각 몬스터·보스 원본을 따른다. 플레이어 HP·적의 플레이어 대상 피해·힐/방어막은 별도 단위다. 무기 내구도와 처치 게이지 HP 분류 기준은 `weapon_ink_run_design.json`에서 같은 비율로 관리한다.
