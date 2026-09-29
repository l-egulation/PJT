# 챕터 5-10 콘텐츠·밸런스 설계

---
doc_kind: design-spec
owner_domain: stages
authority_level: candidate
status: draft
date: 2026-09-14
worktree: .worktrees/chapter-5-10 (branch feat/stage-5-10-chapters)
---

## 1. 목표

챕터 1-4(40스테이지)로 상한이 박혀 있는 게임에 **챕터 5-10 (60스테이지, `stage.05-01`~`stage.10-10`)**을 추가한다. 이번 작업의 스코프는 **수치(밸런스)와 구조**만이다. 테마·보스명·배경·스프라이트 등 아트/프레젠테이션은 플레이스홀더로 두고 후속 작업으로 분리한다.

근거: `docs/70-plans/mvp-release/requirements.md:14`가 "장기 목표를 챕터 1~10, 총 100스테이지로 확장"을 사용자 합의(2026-09-10~11)로 명시하고 있으며, `docs/30-domain/world/ssot.md`가 챕터당 10스테이지 구조·글로벌 인덱스·성장벽 규칙을 정의한다. 미확정으로 남아 있던 "5~10장 적 수치·80레벨 이후 곡선"(`world/ssot.md:29`)을 이 스펙이 채운다.

## 2. 범위

### 포함
- 챕터 5-10의 스테이지 구조(챕터당 1-9 파밍 + 10 단독 보스, 4·7·10 성장벽)
- 챕터 5-10 적 수치 곡선(HP·공격·방어·보스 기제)
- **라이브 버전(`progression-rebalance-v1`) 챕터 1-4 일반·보스 공격 재설계** — 현행 2→22 램프(HP와 무관, atk/HP≈0.003)를 §3.2의 p×HP 모델로 교체해 챕터 1~10 공격 곡선을 통일 (2026-09-14 사용자 승인: "2-4 정도는 세게 때려도 금방 뚫는다")
- 레벨 상한 79 → 200
- 진행 보상(XP·쌀·최초 클리어·재료)의 기존 공식 자연 연장
- 위를 가능케 하는 전 계층 하드코딩 해제(§6)

### 제외 (후속)
- 챕터 5-10 테마/보스명/배경/몬스터 스프라이트 실 에셋 (플레이스홀더 ID로 진행)
- 미완인 챕터 4 보스/테마/배경 (별건)
- 챕터 화면(`GET /api/v1/chapters`)의 5-10 대응 — 현재 플랫 스테이지 목록으로도 5-10 노출 가능하므로 이번 스코프 밖
- 레이드/추가 콘텐츠의 5-1·7-1 해금 연동 (일정만 기존 유지)

## 3. 밸런스 모델 (핵심)

### 3.1 원칙 — 라이브 생성기 공식을 그대로 연장

라이브 기본 밸런스 버전은 `progression-rebalance-v1`(`apps/game-api/src/main/resources/application.yml:102`)이며, 이 버전의 적 수치는 손으로 저작한 표가 아니라 **`apps/balance-lab` 생성기가 계산**한다(`ProgressionRebalance.kt`의 `generatedStageRows()`, `Main.kt:55`의 `--write`). 챕터 5-10도 **같은 생성기를 스테이지 100까지 연장**해 만든다. 기하 배율은 적용하지 않는다(2026-09-14 사용자 결정: 기하급수적 증가는 부적절).

핵심 이점: 보스 HP·공격·강타는 상수로 적지 않고, 생성기가 **"기준 빌드가 1,000 seed 중 90% 이상 승리하는 최대값"을 이진탐색**으로 찾는다. 따라서 챕터 5-10 난이도가 챕터 1-4와 **같은 기준으로 자동 보정**된다.

> 폐기된 초안: 이 스펙의 이전 판은 챕터당 ~3배(기하) 곡선과 10-10 보스 ~6,700만을 제시했으나, 이는 **동결된 레거시 `v1`** 표(250→19,236, ×77)를 측정한 값이었다. 라이브 곡선은 선형(630→7,620, ×12)이라 성격이 완전히 다르다.

### 3.2 스탯별 곡선 (생성기 기준)

| 스탯 | 생성기 모델 (챕터 1-4) | 챕터 5-10 처리 |
|---|---|---|
| 기준 레벨 | `level = 2i − 1` (i = 글로벌 인덱스) | 그대로 연장 → 스테이지 100은 레벨 199 |
| 기준 장비 Q | 구간선형 보간, 끝점 `(1,0) (10,25) (20,54) (30,64) (40,93)` | **끝점을 스테이지 100까지 연장** (§3.2.1) |
| 일반 HP | `600 + level×30 + Q×50` (선형) | 공식 그대로 연장 |
| 일반 공격 | ~~`2 + i/2`~~ — HP와 무관한 램프(결함) | **`round(p × 기준빌드 최대HP)`, p = 0.015~0.02** |
| 보스 공격 | STANDARD는 일반 공격과 동일(1.0배) | **보스:일반 = 2.0 고정** |
| 공통 방어 | `Q × 5` (관통벽은 `기준관통 + 90 + i×2`) | 공식 그대로 연장 |
| 보스 HP · 관통벽 보스 공격 · 강타 피해 | 이진탐색(기준 빌드 90% 승리 상한) | 자동 재보정 |

p의 확정값은 balance-lab 사이클 누적 목표(파밍 60~75%, 성장벽 85~95%)에서 역산한다. 성장벽 스테이지의 일반 공격은 챕터 1-4 패턴대로 파밍 대비 낮게 둔다.

#### 3.2.1 Q 램프 — 이번 구현의 유일한 밸런스 손잡이

장비 스탯은 `1.05^(Q/2)`로 **기하** 성장하는데 적 HP는 Q에 **선형**이므로, Q 램프 속도가 챕터 5-10의 체감 난이도와 진행 속도를 사실상 결정한다. 챕터 1-4 평균 램프는 약 **+2.3 Q/스테이지**다. 챕터 5-10 끝점은 시뮬레이션에서 **챕터당 도달 시간이 챕터 4 수준**이 되도록 역산해 확정한다. 초기안은 챕터 1-4 평균 램프를 그대로 이은 `(50,116) (60,139) (70,162) (80,185) (90,208) (100,231)`.

### 3.3 챕터 내부 구조 (챕터 1-4와 동일 보존)

각 챕터 c (5~10):
- **c-1 ~ c-9**: 일반 몬스터 20마리 + 보스 1마리 연속 사이클(파밍). 기본 보스 타입 `STANDARD`.
- **c-10**: 일반 몬스터 없이 최종 보스 1마리만 등장(`bossOnly`), 시작 즉시 `BOSS_SPAWNED`.
- **성장벽**: c-4(소벽), c-7(소벽), c-10(대벽)은 검사축 보스로 튜닝. 나머지는 기준 빌드가 90%+ 통과하는 파밍 난이도.
- **대벽 기대치(2026-09-14 사용자 의도)**: 챕터 5-10의 c-10 대벽은 최초 도달 시점에는 **거의 못 깨는 장기 정체 지점**으로 설계한다. 기준 빌드 90%+ 통과 요건은 파밍 스테이지(1~9)와 소벽에만 적용하고, 대벽은 해당 챕터에서 충분히 파밍·성장한 뒤에야 돌파 가능한 수준으로 튜닝한다.

### 3.4 성장벽 검사축 회전 (주기 3, 챕터 1-4 표 연장)

`world/ssot.md`의 회전표는 주기 3이다(챕터 4 = 챕터 1 패턴). 이를 그대로 연장:

| 챕터 | 4스테이지(소벽) | 7스테이지(소벽) | 10스테이지(대벽) | 동일 패턴 |
|---|---|---|---|---|
| 5 | 방어 관통 | 공격력 | 최대 HP | =챕터 2 |
| 6 | 최대 HP | 방어 관통 | 공격력 | =챕터 3 |
| 7 | 공격력 | 최대 HP | 방어 관통 | =챕터 1·4 |
| 8 | 방어 관통 | 공격력 | 최대 HP | =챕터 2 |
| 9 | 최대 HP | 방어 관통 | 공격력 | =챕터 3 |
| 10 | 공격력 | 최대 HP | 방어 관통 | =챕터 1·4 |

검사축 → 보스 타입/기제 매핑(기존 유지, 크기는 §3.1 곡선으로 스케일):
- **공격력** → `ATTACK_CHECK`: 제한시간(`timeLimitTicks`) 내 처치 요구
- **최대 HP** → `HP_CHECK`: 예약 강타(`scheduledStrikes`)로 큰 피해 — 충분한 HP 요구
- **방어 관통** → `PENETRATION_CHECK`: 높은 공통 방어력 + 누적 피격 — 관통 요구

### 3.5 대표 수치 (라이브 생성기 기준, 일반 HP)

Q는 §3.2.1 초기안 기준. `일반 HP = 600 + level×30 + Q×50`.

| 스테이지 | 레벨 | Q | 일반 HP |
|---|--:|--:|--:|
| 1-01 | 1 | 0 | 630 |
| 4-10 (현행 끝) | 79 | 93 | 7,620 |
| 5-10 | 99 | 116 | 9,370 |
| 7-10 | 139 | 162 | 12,870 |
| 10-10 | 199 | 231 | 18,120 |

보스 HP·공격·강타는 이 표에 적지 않는다 — 생성기가 기준 빌드 대비 이진탐색으로 산출하며, 확정값은 `--write` 실행 결과와 `docs/70-plans/mvp-release/verification/progression-rebalance-v1.csv`에 기록된다.

## 4. 진행 · 보상 · 레벨

### 4.1 레벨 상한 79 → 200
- 도메인 `ProgressionRules.MAX_LEVEL`은 이미 500이라 코드 변경 불필요. 레벨업 총경험치 공식 `totalExperienceForLevel(L) = 500·L·(L−1)`은 임의 레벨에서 성립.
- **변경 필요**: 콘텐츠 `progression.json`의 `levelRange.max` 79 → 200, 그리고 **스키마 `progression.schema.json`의 `max: {const: 79}` → 200**.
- 정합성: 스테이지 100(10-10)의 기준 레벨 = `2×100−1 = 199` ≤ 200. 100스테이지 = 최대 레벨 199 도달.

### 4.2 보상은 기존 공식으로 자연 연장
- **결정: 보상(XP·쌀·재료)도 적 수치와 마찬가지로 기존 곡선을 그대로 잇는다.** 별도 배율 없음. 적과 보상이 같은 페이스로 연장되므로 챕터 1-4의 플레이 감각이 5-10장에서도 유지된다.
- XP: `experienceForNormal`의 기존 공식을 스테이지 41-100으로 확장(기준 레벨 `2I−1`, 보스 XP = 일반×5). 쌀 = 일반 `globalIndex`, 보스 ×10.
- 최초 클리어 보상: `buildProgressionRebalance.ts`의 챕터별 스케줄 패턴을 5-10장으로 연장(재료 세대·수량·쌀). 세부 표는 구현 시 확정.
- 재료 드롭: 챕터별 세대 가중치(`chapterGenerationWeights`)를 5-10장으로 연장. 현재 규칙(챕터 c는 M1~Mc 누적)을 유지하되 M4가 최대 세대이므로 5장 이상은 M1~M4 유지(신규 세대 도입은 이번 스코프 밖).

## 5. 검증

- **content-validator** (`tools/content-validator`): 스키마·참조 무결성·매니페스트 체크섬. 40/4챕터 하드코딩(§6) 해제 후 100스테이지/10챕터 기준으로 통과해야 함.
- **balance-lab** (`apps/balance-lab`): 각 챕터 성장벽(c-4/c-7/c-10)에서 기준 빌드 90%+ 통과, 부족 빌드는 해당 검사축(공격/HP/관통) 부족으로 `TIME_LIMIT`/`PLAYER_DIED` 재현. 파밍 스테이지 통과 확인.
- **회귀**: 챕터 1-4 기존 수치·검증 결과는 불변(이번 변경은 5-10장 신규 + 상한/범위 확장만).

## 6. 변경 서피스 (전 계층 체크리스트)

검증 워크플로우(2026-09-14)로 확인된 하드코딩 지점. 챕터 5-10을 위해 아래를 함께 푼다.

### 도메인 (Kotlin)
- `modules/stage/.../domain/Stage.kt:4` — `require(chapter in 1..4 && number in 1..10)` → 1..10
- `modules/stage/.../domain/Stage.kt:24` — `bossOnly = (chapter==1 && number==10)` → 모든 챕터의 10스테이지로 일반화
- `modules/stage/.../infrastructure/JsonStageCatalog.kt:28` — `require(size == 40)` → 100
- `modules/progression/.../domain/Progression.kt:81` — `experienceForNormal` `require(globalIndex in 1..40)` → 1..100
- `modules/progression/.../domain/Progression.kt:96` — `globalIndex()` `require(chapter in 1..4 && number in 1..10)` → 1..10
- 관련 테스트: `StageTest.kt`(globalIndex 단언), `ProgressionRulesTest.kt` 등 픽스처 갱신

### 콘텐츠 스키마 (`packages/game-content/schema`)
- `stages.schema.json` — `minItems/maxItems: 40` → 100, `chapter.maximum: 4` → 10, `backgroundId` enum에 5-10장 배경 추가(플레이스홀더)
- `material-drops.schema.json` — `chapterGenerationWeights` required 키 `1..4` → `1..10`, `additionalProperties` 정책 확장
- `progression.schema.json` — `levelRange.max: {const: 79}` → 200

### 밸리데이터 / 생성기 (`tools/content-validator/src`)
- `buildProgressionRebalance.ts` — `Array.from({length:40})` → 100, `firstClearFor` 챕터 1-4 스케줄 → 1-10, `levelRange {min:1,max:79}` → 200, `chapterGenerationWeights` 1-4 → 1-10
- `validate.ts` — `progression.stages.length !== 40` → 100, `for index<40` 루프 → 100, material-drops `for chapter=1..4` → 1..10, 스테이지 ID 생성/검증 범위, 프로모션 표(EPIC→LEGENDARY의 `stage.05-10` 게이트는 이제 실재 스테이지가 됨)

### 콘텐츠 데이터 (`packages/game-content/versions`)
- `chapters/chapters.json` — chapter.05~10 (각 10 stageId) 추가
- `stages/stages.json` — stage.05-01~10-10 60행 추가(적 수치 §3, 플레이스홀더 프레젠테이션 ID). `v1`과 `progression-rebalance-v1` 양쪽
- `progression/progression.json`·`drops/material-drops.json`·`equipment/equipment.json` — 생성기 재실행으로 재생성, 매니페스트 갱신

### API 계약 / 클라이언트 (필요 시)
- `packages/contracts/main.tsp` `StageSummary` — 클라가 소비하는 `normalMonsterIds/bossMonsterId/backgroundId` 동기화(선택)
- `apps/web/.../features/battle/battleVisuals.ts` — 몬스터/배경 유니온·레지스트리·스프라이트 라우팅에 5-10장 플레이스홀더 추가(없으면 챕터 1로 무음 폴백 — 아트 후속까지 허용)

## 7. 미결 · 후속

- **일반 공격 결함 처리(2026-09-14 감사·결정)**: 라이브 기본 버전은 `progression-rebalance-v1`(`apps/game-api/src/main/resources/application.yml:102`)이며, 그 일반 공격은 HP와 무관한 2→22 램프(체감 "매우 낮음"의 실제 원인) → **이번 스코프에서 §3.2 p×HP 모델로 재설계(§2 포함)**. 레거시 `v1/stages/stages.json`의 챕터 2-4 일반 공격 `round(0.55×문서값)` 결함(30행, 보스 미스케일로 비율 2.0→3.6 갈라짐, 원인 커밋 `213a23e5`)은 v1이 재현 기록용 동결 버전이므로 **수정하지 않고 기록만 유지**. 설계 문서(`docs/60-content/enemies/mvp-v1.md`)와 v1 JSON의 불일치는 문서에 각주로 후속 반영.
- 챕터 5-10 실 테마/보스명/배경/몬스터 스프라이트 (아트 작업)
- 미완 챕터 4 보스/테마/배경
- 챕터 화면의 5-10 대응(현재 "네 챕터 단일 월드맵" 가정) 및 페이지네이션
- M5 이상 재료 세대 도입 여부(현재 M1~M4 유지)
- 문서 승격: 이 스펙 확정 시 `docs/30-domain/world/ssot.md`·`docs/80-decisions/README.md`에 5-10장 수치 확정 반영
