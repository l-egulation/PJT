---
doc_kind: content-version
owner_domain: raid-content
authority_level: working
status: derived-working
content_version: raid-mvp-v1-working
---

# MVP 일일 공동 봉인 레이드 콘텐츠 v1

## 책임과 상태

이 문서는 시뮬레이션으로 산출한 레이드 보스 절대 수치와 고정 등급 커트라인을 소유한다. 게임 규칙과 보상 정책은 [레이드 SSOT](../../30-domain/raid/ssot.md), JSON Schema 원본은 [TypeSpec 레이드 콘텐츠 계약](../../../packages/contracts/raid-content.tsp), 실행 데이터는 [`raids.json`](../../../packages/game-content/versions/v1/raids/raids.json)을 따른다. `working` 상태는 후속 레이드 수직 슬라이스가 아직 출시 승격되지 않았음을 뜻한다.

## 기준 빌드

`stage.01-02` 정상 성장 기준의 레벨 3·전 부위 Q4 장비, `active_heavy` N1 스킬 장착, MAIN 보석 없음, 치장 세트 효과 없음으로 고정한다. 입력은 [`raid-reference-build-v1.json`](../../../apps/balance-lab/src/main/resources/raid-reference-build-v1.json)에 보존하고, 전투식은 Kotlin `RaidSimulator.simulate`를 사용한다.

## 산출 결과

1,000개 seed (`1L..1000L`)에서 초기 보스 공격력 `14`, 방어력 `1`, B 절대 피해 커트라인 `19,871`을 선택했다. 기준 빌드의 median 생존은 590 ticks(59초), B 달성은 1,000/1,000이다. 부족 빌드는 공격력 Q0에서 초기 DPS 3,120(기준 3,355)으로, HP Q0에서 median 생존 560 ticks로, 관통 Q0에서 후반 피해 2,879(기준 3,096)로 각각 손실을 보인다.

## 등급·기여·보상

등급 커트라인은 B 대비 승인 배수 `0.35, 0.65, 1.00, 1.50, 2.20, 3.20, 4.50`를 양수 round-half-up 정수화한다. 최종 값과 참여·D~SSS 봉인 기여, 개인·성공 순위·실패 순위 보상은 JSON 콘텐츠 파일을 정본으로 사용하며, validator가 SSOT 표와의 동일성을 검사한다.

## 재현과 승격

재현 명령은 `./gradlew.bat :apps:balance-lab:raidBalance -Pkotlin.incremental=false --no-daemon --no-configuration-cache --max-workers=1`이다. 파생 JSON과 보고서는 이 명령으로만 생성하며, 스키마는 `corepack pnpm --filter @hanjjak/contracts build`로 생성한다. 기준·부족 빌드 게이트와 콘텐츠 validator, generated-artifact diff가 모두 통과하고 레이드 수직 슬라이스가 완료되기 전까지 `working`을 유지한다.

## Task 11 장기 검증과 경제 투영

`apps/balance-lab`의 결정론 보고서 [`raid-mvp-v1-working.json`](../../../build/reports/balance/raid-mvp-v1-working.json)은 기준·공격력 Q0·HP Q0·관통 Q0 각각 1,000 seeds(`1..1000`)를 기록한다. 기준 빌드는 damage P10/P50/P90 `19,871/19,871/19,871`, survival P10/P50/P90 `590/590/590` ticks, B `1,000/1,000 (100.0%)`, 5분 cap `0`, 등급은 B `1,000`이다. 부족 빌드는 공격력 Q0 damage `18,480`·survival `590`, HP Q0 damage `18,968`·survival `560`, 관통 Q0 damage `18,479`·survival `590`이며 모두 B `0/1,000`; 각 등급은 C `1,000`이다.

활성 참여자 `36/72/144/1,000` 투영의 seal contribution은 각각 `8,388/16,776/33,552/233,000`, 목표 `50,000` 대비 성공 확률은 `0/0/0/100%`다. 계정당 3회 확인 공급은 `21 tickets, 12 gem boxes, 21,750 rice/day`; 서버당 공급은 각각 `983/577/1,017,500`, `1,955/1,153/2,027,300`, `3,899/2,305/4,016,100`, `29,111/17,011/29,861,000` (tickets/boxes/rice)이다. rank 분포는 `1/9/26/0`, `1/9/62/0`, `1/9/90/44`, `1/9/90/900`, auto-claim backlog은 `0/0/0/800`이다.

세 번의 participation 확인만으로 **15 tickets, 9 gem boxes, 15,000 rice**를 공급한다. 이는 승인 보상표와 봉인 정책을 바꾸지 않은 관측 투영이며, content authority는 후속 통합 검증 전까지 `working`을 유지한다.

## 연결

- [레이드 SSOT](../../30-domain/raid/ssot.md)
- [L-01 전투·콘텐츠 수치 산출](../../wiki/06-delivery/tasks/L-raid/l-01-raid-content-simulation.md)
- [결정 로그](../../80-decisions/README.md)
