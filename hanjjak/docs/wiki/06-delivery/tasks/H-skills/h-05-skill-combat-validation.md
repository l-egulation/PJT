---
doc_kind: task
owner_domain: delivery
task_code: 'H-05'
task_area: 'H 스킬·자동 사용'
task_type: '검증'
priority: 'P2'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/fa306c8781b08293adf401ab27636dcf'
notion_id: 'fa306c87-81b0-8293-adf4-01ab27636dcf'
snapshot_date: '2026-08-28'
---

# H-05 액티브·버프·패시브 전투 검증

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [스킬](../../../../30-domain/character/skills/ssot.md)를 따른다.

## 완료 기준

강타·지속 피해가 기본공격을 대체하고 두 버프가 같은 행동의 기본공격과 함께 발동하며, 패시브와 등급별 효과·실패 보정이 서버 확정 스킬 상태대로 전투에 적용된다. 대상 사망·전환 시 지속 피해 잔여 틱이 [스킬 SSOT](../../../../30-domain/character/skills/ssot.md)의 대상 전환 규칙대로 처리되는지와 공통 모션·코드 효과를 포함한 실제 자동전투 흐름을 검증한다.

## 선행 작업

H-04, E-02

## 비고

전투 엔진 연결과 웹 보석 던전의 액티브 4종 시각 재생은 구현했지만 액티브 행동 점유율, 스킬별 DPS, 최적 순서 차이의 수치 검증은 수행하지 않았다. 시각 재생은 서버 사건 로그만 소비하며 전투 판정 수치를 변경하지 않는다.

2026-09-10 액티브 4종의 최종 8프레임 시각 시안은 [MVP 스킬 표시 콘텐츠 v1](../../../../60-content/abilities/mvp-v1.md#액티브-전투-vfx-확정-시안)로 확정됐고 메인 전투와 보석 던전의 공통 런타임 매핑에 반영했다. 아래 2026-09-09 및 같은 날짜의 이전 VFX 증거는 교체 전 이력이며, 현재 표시 기준은 마지막 확정 시안 반영 증거를 따른다.

## 증거 링크

- 전투 연결: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/AccountBattleSkillProvider.kt`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`
- 시뮬레이터: `packages/sim-core/src/main/kotlin/com/hanjjak/sim/CombatSimulator.kt`
- 액티브 VFX 재생(2026-09-09): `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/features/gems/AngelDungeonCharacter.tsx`, `apps/web/src/features/gems/GemsScreen.tsx`, `apps/web/public/assets/effects/skills-v1/`. `active_heavy`는 `strike1`, `active_dot`은 `thrust1`, `active_haste`와 `active_basic_amp`는 `rest` 모션에 연결했다. 지속 피해 표식은 시전 후 50틱 유지되고 `DOT_HIT`마다 밝은 피격 프레임을 재생하며, 두 버프 문양은 시전 후 50틱 유지된다. 무기 레이어에는 이펙트를 합성하지 않는다.
- 마! 쫄이나 대상 VFX 교체(2026-09-09): 특정 몬스터의 외곽선·표면에 의존하던 액체 구체 대신 중심점과 바운딩 박스만 사용하는 `졸임 낙인` 6프레임 스프라이트로 교체했다. 1~3프레임은 부착 상태, 4~6프레임은 `DOT_HIT` 피해 펄스이며 기존 50틱/10틱 재생 계약은 유지한다. 교체 후 웹 typecheck, Vitest 39개 파일·132개 테스트와 Vite 프로덕션 빌드를 통과했다.
- 액티브 VFX 검증(2026-09-09): 웹 TypeScript typecheck, Vitest 30개 파일·81개 테스트, Vite 프로덕션 빌드 통과. 4프레임 단발 이펙트, 5초 지속 표식·버프, 지속 피해 피격 펄스의 사건 타임라인과 실제 전투 HUD 오버레이 렌더 회귀 테스트를 포함한다.
- 메인 전투 VFX 검증(2026-09-09): 보석 던전과 같은 `skills-v1` 이펙트를 메인 전투의 `PLAYER_SKILL_CAST_STARTED`, `BUFF_STARTED`, `PLAYER_SKILL_IMPACT`, `DOT_TICK` 사건에 연결했다. `active_heavy`는 `strike1`, `active_dot`은 `thrust1`, 두 버프는 `rest`로 재생하며 버프 문양은 캐릭터 머리 위 좌우에 분리했다. 대상이 바뀌면 이전 적의 지속 피해 표식을 새 적에 넘기지 않는다. 브라우저 1-4 실제 전투에서 6개 VFX 종류, 5초 버프·표식, 눈 불꽃, 황금 링·X를 확인했고 콘솔 오류와 에셋 404가 없었다. H-05 수치 검증 미완료 상태는 유지한다.
- 메인 전투 버프 정렬 후속(2026-09-10): 기본 나무검까지 포함한 전체 캐릭터 박스의 중앙 대신 928×672 합성 프레임에서 젓가락 두 짝의 정수리 중앙인 X 39.3%를 별도 `hero-crown-vfx` 기준점으로 정의했다. 가속·기본 공격 증폭의 반복·종료 전 점멸 동작은 유지하면서 두 문양 묶음의 중심을 이 기준점에 맞췄다.
- 정수리 앵커 검증(2026-09-10): `battle-hud-preview.test.tsx`에서 두 반복 버프가 `hero-crown-vfx` 아래에 렌더되는 회귀 검사를 추가했고, 웹 Vitest 44파일·184테스트, TypeScript typecheck, production build를 통과했다. 로컬 버프 미리보기에서도 무기 방향으로 치우치지 않은 정수리 중앙 정렬을 확인했다.
- 보석 던전 VFX 후속 검증(2026-09-09): 로컬 PostgreSQL `55432`, game-api `8080`, web `5173`과 테스트 계정으로 폭주형 1단계를 실제 재생했다. 렌더 구간을 50틱 미만으로 보정해 표식·버프를 정확히 5초 동안 표시하고, 마지막 `DOT_HIT`의 3프레임 펄스는 별도로 완주하게 했다. 전투 종료 후 재생 시계가 계속 진행되어 두 번째 시전의 표식·버프가 결과 화면에 남지 않으며, 던전의 두 버프 문양은 캐릭터 머리 위 좌우로 분리했다. `thrust1` 네 프레임과 눈 오버레이 프레임 대응, DOT 펄스 약 1초 간격, 375×812 무가로넘침, 콘솔 오류·에셋 404 없음, 웹 typecheck·39파일 133테스트·프로덕션 빌드 통과를 확인했다. H-05의 기획·개발·검증 상태는 기존 체계를 유지한다.
- 액티브 VFX 8프레임 확장(2026-09-10): 강타 시전자·피격, 지속 피해 눈, 공속·기본공격 피해 버프의 승인된 4개 키프레임을 유지하면서 투명 알파 기반 중간 프레임을 추가해 8프레임 시트로 확장했다. 강타와 지속 피해 눈은 350ms 동안 8단계를 단발 재생하고, `prefers-reduced-motion`에서는 사건 나이에 대응하는 정적 키프레임을 사용한다. 기존 4프레임 시트와 6프레임 졸임 낙인은 보존한다. 5개 시트의 RGBA·완전 투명/불투명 범위와 1·3·6·8번 원본 키프레임 픽셀 일치를 확인했고, 웹 TypeScript typecheck, Vitest 44개 파일·175개 테스트, Vite 프로덕션 빌드를 통과했다. 테스트 중 로컬 API `localhost:3000` 미기동에 따른 연결 거부 로그는 있었으나 테스트 실패는 없었다. 증거: `apps/web/public/assets/effects/skills-v1/*-8f*.png`, `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/styles.css`, `apps/web/src/features/gems/skillVfx.test.ts`.
- 지속형 버프 VFX 반복·만료 예고(2026-09-10): `active_haste`와 `active_basic_amp`는 5초 유지 중 8프레임 가로 시트를 800ms 주기로 반복하고, 남은 시간이 3초 이하가 되는 논리 tick부터 600ms 주기로 점멸한다. 모션 감소 환경에서는 반복·점멸을 정지하고 해당 논리 tick의 정적 프레임을 표시한다. 강타·지속 피해 눈의 단발 8프레임과 기존 `dot-target-6f.png` 6프레임 매핑은 유지한다. 다섯 8프레임 파일의 `800% 100%` 매핑과 6프레임 파일의 `600% 100%` 유지, 반복·만료 경계와 전투 미리보기 회귀 테스트를 포함해 웹 Vitest 44개 파일·183개 테스트, TypeScript typecheck, Vite 프로덕션 빌드를 통과했다. 테스트 중 로컬 API `localhost:3000` 미기동에 따른 연결 거부 로그는 있었으나 테스트 실패는 없었다. 증거: `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/styles.css`, `apps/web/src/features/gems/skillVfx.test.ts`, `apps/web/src/features/battle/battle-hud-preview.test.tsx`.
- 액티브 VFX 전투 화면 검수 경로(2026-09-10): `battle-hud-preview.html?skill-vfx=heavy|dot|buffs|all`로 실제 메인 전투 캐릭터·몬스터 좌표에서 각 연출을 확인하고, `&expiring=1`로 지속형 두 버프의 만료 점멸 상태를 확인할 수 있게 했다. 사용자 조정 전투 배경 `morning.png`, `lunch.png`, `evening.png`, `predawn.png`는 기존 `battleVisuals.ts`의 챕터 1 스테이지 매핑을 통해 수정본을 직접 사용한다. 증거: `apps/web/src/features/battle/battle-hud-preview.tsx`, `apps/web/src/features/battle/battle-hud-preview.test.tsx`, `apps/web/src/features/battle/battleVisuals.ts`.
- 확정 액티브 VFX 런타임 교체(2026-09-10): `active_heavy`는 `active-heavy-target-8f.png`를 현재 공격 대상 중심에서 350ms 단발 재생하고, `active_dot`은 기존 눈·6프레임 낙인 대신 `active-dot-target-8f.png`를 최초 시전의 50틱 수명 안에서 현재 `DOT_TICK` 대상에 반복 재생한다. 대상이 바뀌어도 수명을 다시 시작하지 않고 이전 몬스터 DOM에는 남지 않는다. `active_haste`와 `active_basic_amp`는 메인 전투와 보석 던전 모두 `hero-crown-vfx`에 배치해 8프레임 반복, 남은 3초부터 600ms 점멸, 50틱 종료 즉시 제거를 공유한다. 세 확정 `outputs/skill-vfx-concepts-v2` PNG를 `apps/web/public/assets/effects/skills-v1` 런타임 경로로 복사했고 네 매핑 모두 `800% 100%`를 사용한다. 전투 판정·ID·피해량·지속시간·재사용시간은 변경하지 않았다. 증거: `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/features/gems/AngelDungeonCharacter.tsx`, `apps/web/src/features/gems/GemsScreen.tsx`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/styles.css`.
- 확정 VFX 검증(2026-09-10): 웹 Vitest 44개 파일·227개 테스트, TypeScript typecheck, Vite production build를 통과했다. 로컬 미리보기에서 강타 8프레임 단발의 대상 중심, 마! 쫄이나 8프레임 반복과 눈 이펙트 DOM 0개, 두 버프의 정수리 묶음, `800% 100%`, 800ms 반복·600ms 점멸을 확인했다. 새 브라우저 세션의 warning/error 콘솔 항목은 0개였고 네 런타임 PNG의 HTTP HEAD가 모두 200 `image/png`이었다. 몬스터 원본 캔버스가 컨테이너보다 큰 경우에도 HP·그림자·대상 VFX가 같은 `monster-visual-anchor`의 실제 몸 중심 X 좌표를 사용하도록 회귀 검사를 추가했다. 테스트 중 로컬 API `localhost:3000` 미기동 연결 거부 로그는 있었으나 테스트 실패는 없었다.
- `화력 최대로!` 비율 보정(2026-09-10): 확정 원본 `active-basic-amp-8f-concept-v2.png`가 2172×724의 좁은 합본인데 런타임 정사각 셀로 직접 늘어나던 원인을 확인했다. 투명 원본 픽셀의 종횡비와 프레임별 중심을 유지해 512×512 셀 8개인 4096×512 런타임 시트로 재패킹했고 `800% 100%` 재생 계약은 유지했다. PNG 헤더의 `width = height × 8`을 검사하는 회귀 테스트를 추가했고 웹 Vitest 44개 파일·230개 테스트, TypeScript typecheck와 production build를 통과했다. `battle-hud-preview.html?stage=stage.02-04&skill-vfx=all`에서 작은 화구부터 큰 불꽃까지 원형 화구가 찌그러지지 않고 캐릭터 정수리 앵커에서 반복되는 것을 확인했다. 전투 수치·서버 판정은 변경하지 않았다.
- 지속 버프 시각 외곽 정렬(2026-09-10): 두 버프의 CSS 셀은 동일했지만 불투명 픽셀 기준 `화력 최대로!`의 최대 외곽이 약 11% 작고 아래 끝이 약 6% 낮았다. 메인 전투와 보석 던전에서 기본 공격 증폭 셀을 56%로 확대하고 `bottom: 6%`, `left: 47%`로 보정해 가속 이펙트와 보이는 크기·아래 기준선을 맞췄다. 관련 Vitest 3개 파일·46개 테스트, TypeScript typecheck와 production build를 통과했고 2-4 미리보기에서 두 이펙트의 정수리 기준 정렬을 재검수했다.
- 한짝의 일격 런타임 시트 보정(2026-09-11): 비정사각 원본 합본을 CSS 정사각 8셀로 늘리며 발생한 왜곡을 제거하기 위해 투명 512×512 프레임 8개를 4096×512 시트로 재패킹했다. `800% 100%`와 350ms 단발 재생, 서버 사건·피해 판정은 유지하고 캐시 무효화 버전 키를 연결했다. PNG 투명 알파와 `width = height × 8`을 회귀 검사하고 실제 대상 중심 미리보기에서 검은 배경·프레임 잘림·찌그러짐이 없음을 확인했다. 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.
- 한짝의 일격 모션 완주 보정(2026-09-11): `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/styles.css`에서 대상 이펙트의 표시 수명을 3틱에서 12틱으로 늘리고 8프레임 단발 재생을 1.2초로 조정했다. 다음 전투 사건이 빠르게 도착해도 마지막 프레임까지 재생한 뒤 제거되며, 스킬 ID·피해량·재사용시간과 서버 전투 판정은 변경하지 않았다. `skillVfx.test.ts`의 시작·마지막 표시·제거 경계와 CSS 재생시간 회귀 검사를 포함해 웹 Vitest 50개 파일·291개 테스트, TypeScript typecheck와 Vite production build를 통과했다. `battle-hud-preview.html?stage=stage.02-04&skill-vfx=heavy`에서 초반·후반 프레임이 대상 중심에서 외곽 잘림 없이 이어지는 것을 확인했다.
- 마! 쫄이나 장판 전환(2026-09-12): `CombatSimulator`가 일반 몬스터 처치 시 남은 DOT 예약 틱을 지우지 않도록 변경했다. 따라서 5초 동안 1초 간격의 기존 5회·총 피해·10초 재사용시간은 유지하면서 각 틱이 그 시점의 현재 전투 대상을 공격한다. 스킬 행동 점유 시간은 공통 행동 점유 시간의 3분의 1로 줄였고 `CombatSimulatorTest`와 `DungeonCombatSimulatorTest`에 대상 전환·행동 잠금 회귀 시나리오를 추가했다. 웹 전체 61개 파일·350개 테스트, TypeScript typecheck, production build와 `:packages:sim-core:compileTestKotlin`을 통과했다.
- 장판 시각 수명·일반 몬스터 즉시 교체(2026-09-12): `active_dot` 장판 DOM을 개별 몬스터 렌더 키 밖의 전투 필드 레이어로 이동해 대상 사망·교체로 재마운트되지 않게 했다. 일반 몬스터 사이 서버 논리 공백과 두 번째 이후의 클라이언트 접근 연출 누적을 제거하고, 다음 몬스터 시트를 현재 전투 중 미리 로드한다. 처치 사건과 다음 일반 몬스터 등장은 같은 재생 시각에 처리하며 보스 진입의 0.5초 논리 지연과 보스 경고 연출은 유지한다. 필드 레이어 독립성·같은 프레임 교체·대상 전환 VFX·서버 spawn tick 회귀 테스트를 추가했고 웹 전체 62개 파일·352개 테스트, TypeScript typecheck, production build와 `:packages:sim-core:compileTestKotlin`을 통과했다.
- 장판 발밑 앵커 보정(2026-09-12): 독립 전투 필드 레이어는 유지하면서 현재 몬스터 스프라이트의 실제 콘텐츠 하단, 표시 배율, 지면 오프셋과 일반·보스 배치 높이로 장판 중심 Y를 계산한다. 장판은 화면 컨테이너 바닥이 아니라 현재 몬스터 그림자 중심, 즉 발 바로 아래에 표시된다.
- 장판 대상 중심·화염 타격 펄스 보정(2026-09-12): 사용자 플레이 화면 피드백에 따라 `active_dot` 전체 이펙트 중심을 `active_heavy`와 같은 현재 몬스터 몸 중심으로 올렸다. 기존 투명 8프레임 루프의 부드러움은 유지하고 각 `DOT_TICK` 직후 0.3초 동안 중앙의 황백색 불꽃과 주황 불티가 위로 솟았다 사라지는 별도 펄스를 합성한다. 메인 전투와 보석 던전이 같은 `skill-vfx-impact` 표현을 사용한다. 타격 펄스 활성·해제 경계와 렌더 클래스 회귀 검사를 추가했고 웹 전체 62개 파일·353개 테스트, TypeScript typecheck와 production build를 통과했다.
- 장판 위치·타격 불꽃 에셋 재보정(2026-09-12): 실제 플레이 캡처에서 장판의 가시 중심이 몬스터보다 왼쪽 위로 치우친 피드백을 반영해 메인 전투 장판을 자체 크기 비례로 오른쪽 18%, 아래 12% 이동했다. 임시 CSS 도형 불꽃은 제거하고 실제 투명 알파를 검증한 신규 픽셀 아트 `active-dot-impact-fire-v2.png`를 피해 틱 전용 0.36초 상승·확대·페이드 펄스로 연결했다. 기존 8프레임 장판 루프와 전투 판정은 변경하지 않았다. 신규 PNG는 1,254×1,254 RGBA와 네 모서리 알파 0을 확인했고 웹 전체 62개 파일·353개 테스트, TypeScript typecheck와 production build를 통과했다.
- 장판 중심·피해 틱 재생 동기화(2026-09-12): 후속 플레이 캡처를 기준으로 장판의 가시 중심을 자체 높이의 18%만큼 더 내려 `active_heavy`의 하단 황금 타격 중심에 맞췄다. 타격 불꽃이 `dot-target` 장판 DOM에 종속되어 다음 피해에서도 같은 React 인스턴스를 재사용하던 원인을 제거하고, `DOT_TICK`마다 사건 ID가 포함된 `dot-impact` 독립 인스턴스를 생성한다. 장판 루프는 재시작하지 않으며 불꽃만 피해 숫자 발생 시점에 새로 솟아 0.36초 뒤 완전히 꺼진다. 피해 사건별 인스턴스 키 회귀 검사를 추가했고 웹 전체 62개 파일·354개 테스트, TypeScript typecheck와 production build를 통과했다.
- 타격 불꽃 철회(2026-09-12): 최종 사용자 피드백에 따라 `active_dot`의 피해 틱 불꽃 인스턴스, 재생 로직, CSS 애니메이션과 생성 PNG 에셋을 제거했다. 아래로 보정한 장판 위치와 기존 8프레임 지속 루프만 유지하며 전투 판정은 변경하지 않는다.
- 피해 숫자 고정 스택 전환(2026-09-12): 메인 전투의 피해 숫자를 아래에서 위로 이동하는 단일 표시에서 현재 몬스터 기준 기존 애니메이션의 최고 지점에 고정되는 독립 스택으로 변경했다. 각 숫자는 0.9초의 수명을 따로 가지며, 사라지기 전에 다음 피해가 발생하면 새 숫자가 기존 숫자 위에 추가된다. 숫자 레이어를 교체되는 몬스터 DOM 밖에 배치해 일반 몬스터가 즉시 교체되어도 남은 숫자의 수명과 스택이 유지된다. 전투 판정과 피해량은 변경하지 않았다. 웹 전체 62개 파일·354개 테스트, TypeScript typecheck와 production build를 통과했고 로컬 웹 `http://127.0.0.1:5173` 응답 200을 확인했다. 증거: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/features/battle/BattleScreen.persistentField.test.tsx`.
- 피해 숫자 스택 깊이 표현(2026-09-12): 첫 피해는 기준 위치에 생성되고 다음 피해는 바로 위의 새 높이 슬롯에 생성된다. 생성된 숫자의 높이 슬롯은 아래 숫자가 먼저 사라져도 재배치하지 않아 각 숫자가 0.9초 수명 동안 같은 좌표에 머문다. 가장 최근 숫자는 불투명도 100%로 유지하고, 새 피해에 의해 아래쪽이 된 숫자는 한 단계마다 24%씩 흐려지며 최소 38%에서 유지되도록 했다. 높이 슬롯은 5개를 순환하여 6번째 피해부터 기존 표시가 남아 있어도 다시 맨 아래 슬롯에 겹쳐 표시한다. 모든 피해 숫자가 사라져 스택이 비어도 다음 피해는 맨 아래 슬롯에서 다시 시작한다. 고정 슬롯·5칸 순환·빈 스택 초기화·스택 깊이별 불투명도 회귀 검사를 포함해 웹 전체 62개 파일·354개 테스트와 TypeScript typecheck를 통과했다.
- 마! 쫄이나 장판 크기·Y축 고정(2026-09-12): 몬스터별 콘텐츠 높이와 일반·보스 배치 높이를 장판 Y축에 더하던 계산을 제거했다. 후속 플레이 피드백에 따라 장판 Y축은 전투 필드 바닥 기준 138px, 크기는 200px로 모든 몬스터에 고정했다. X축은 `left: 50%`와 `translateX(-50%)`를 함께 사용해 전투 대상 영역의 정중앙을 유지한다. 몬스터 크기나 일반·보스 전환으로 장판이 위아래로 뛰거나 확대·축소되지 않는다. 고정 Y축·고정 크기·X축 중앙 정렬 회귀 검사를 추가했고 웹 전체 62개 파일·356개 테스트, TypeScript typecheck와 production build를 통과했다. 전투 판정은 변경하지 않았다.
- 피해 숫자 3칸 순환·장판 X축 미세 보정(2026-09-12): 메인 전투 피해 숫자의 고정 높이 슬롯을 5개에서 3개로 줄여 네 번째 피해부터 다시 맨 아래 슬롯에 표시한다. 각 숫자의 0.9초 수명과 기존 깊이별 불투명도는 유지한다. `마! 쫄이나` 장판은 200px 크기와 바닥 기준 138px Y축을 유지하고 전투 대상 영역 중심에서 오른쪽으로 총 35px 이동했다. `BattleScreen.persistentField.test.tsx`에 3칸 순환과 CSS 오프셋 회귀 검사를 반영했다.
- 피해 숫자 고정 2레인 전환(2026-09-12): 큰 보스의 스프라이트 높이를 피해 숫자 Y축에 더해 1-10·2-10에서 화면 위로 밀려나던 계산을 제거하고 전투 필드 바닥 기준 170px로 고정했다. 일반 공격과 `마! 쫄이나` 외 스킬 피해는 중앙의 단일 슬롯에서 새 피해가 기존 표시를 즉시 교체한다. `마! 쫄이나`의 `DOT_TICK` 피해는 중앙 기준 오른쪽 80px의 별도 단일 슬롯에서 독립적으로 교체된다. 두 레인은 각 표시의 0.9초 수명을 유지하며 몬스터 크기와 교체 여부에 영향을 받지 않는다. 고정 Y축·레인별 단일 슬롯·일반 피해 교체·DOT 우측 분리 회귀 검사를 추가했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
