# 전문 대항전 종합전투력 랭킹

2026-09-09 office-hours 설계와 구현 기록.

## 범위

- 감자·고구마·옥수수 전문별 TOP 랭킹
- 로그인한 사용자의 전문 내 순위
- 닉네임, 전문, 서버 계산 종합전투력, 갱신 시각 표시
- 시즌·보상·실시간 스트리밍·Kafka 집계 제외

## 종합전투력 v1

`docs/30-domain/combat/ssot.md`가 공식의 정본이다.

```text
combatPower = round(1000 × (attack / 40 + maxHp / 400 + penetration / 20))
```

서버가 계산하며 웹은 재계산하지 않는다. 현재 전투의 임시 버프와 현재 HP는 제외한다.

## 기술 경계

`ranking_entry`는 공개 조회용 파생 읽기 모델이다. 원본 전투 스탯은 서버 도메인 상태에 있다. 구현은 PostgreSQL/Flyway 기반으로 시작하며, 상위 조회는 전문·전투력·갱신시각·캐릭터 ID 순의 복합 인덱스와 결정적 정렬을 사용한다.

현재 1차 구현은 랭킹 조회 transaction 안에서 현재 서버 상태를 읽어 `ranking_entry`를 보정한다. 이후 성장 command의 same-transaction projection 호출로 바꾸는 것은 별도 후속 작업이다. Kafka는 사용하지 않는다.

## 화면

기존 `apps/web/src/features/market/MarketScreen.tsx`와 `apps/web/src/styles.css`의 장부형 리스트, 종이 톤, 재료 액센트를 재사용한다. 세 전문 카드를 나란히 배치하고 TOP 3를 강조하며, 하단에 내 순위를 둔다. 작은 화면에서는 전문 카드를 세로로 쌓는다.

## 변경 경로

- DB: `apps/game-api/src/main/resources/db/migration/V29__combat_power_ranking.sql`
- 서버: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/ranking/`
- 계약: `packages/contracts/ranking.tsp`
- 웹: `apps/web/src/features/ranking/`
- 내비게이션: `apps/web/src/main.tsx`, `apps/web/src/navigationState.ts`
- 작업 상태: `docs/wiki/06-delivery/tasks/J-delivery-operations/j-15-combat-power-ranking.md`

2026-09-09 office-hours 설계와 구현 기록.

## 범위

- 감자·고구마·옥수수 전문별 TOP 랭킹
- 로그인한 사용자의 전문 내 순위
- 닉네임, 전문, 서버 계산 종합전투력, 갱신 시각 표시
- 시즌·보상·실시간 스트리밍·Kafka 집계 제외

## 종합전투력 v1

`docs/30-domain/combat/ssot.md`가 공식의 정본이다.

```text
combatPower = round(1000 × (attack / 40 + maxHp / 400 + penetration / 20))
```

서버가 계산하며 웹은 재계산하지 않는다. 현재 전투의 임시 버프와 현재 HP는 제외한다.

## 기술 경계

`ranking_entry`는 공개 조회용 파생 읽기 모델이다. 원본 전투 스탯은 서버 도메인 상태에 있다. 구현은 PostgreSQL/Flyway 기반으로 시작하며, 상위 조회는 전문·전투력·갱신시각·캐릭터 ID 순의 복합 인덱스와 결정적 정렬을 사용한다.

초기 구현은 로그인한 랭킹 조회 시 현재 서버 상태를 읽어 projection을 보정한다. 이후 전투력 변경 command의 same-transaction projection 호출로 고정한다. Kafka는 사용하지 않는다.

## 화면

기존 `apps/web/src/features/market/MarketScreen.tsx`와 `apps/web/src/styles.css`의 장부형 리스트, 종이 톤, 재료 액센트를 재사용한다. 세 전문 카드를 나란히 배치하고 TOP 3를 강조하며, 하단에 내 순위를 둔다. 작은 화면에서는 전문 카드를 세로로 쌓는다.

## 변경 경로

- DB: `apps/game-api/src/main/resources/db/migration/V29__combat_power_ranking.sql`
- 서버: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/ranking/`
- 계약: `packages/contracts/ranking.tsp`
- 웹: `apps/web/src/features/ranking/`
- 내비게이션: `apps/web/src/main.tsx`, `apps/web/src/navigationState.ts`
- 작업 상태: `docs/wiki/06-delivery/tasks/J-delivery-operations/j-15-combat-power-ranking.md`
