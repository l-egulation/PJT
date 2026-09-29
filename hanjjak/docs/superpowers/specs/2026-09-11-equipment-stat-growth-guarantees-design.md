# 장비 능력치 성장 보장 설계

## 배경

현재 공격력·최대 HP 장비는 다음 공식을 사용한다.

```text
M(Q) = 1.05^(Q / 2)
Q = gradeIndex × 39 + enhancementLevel
```

부위별 기여 계수는 무기 공격력 36, 장갑 공격력 24, 갑옷 최대 HP 360, 투구 최대 HP 240이다. 장비 화면은 각 부위 기여값을 정수 반올림한 뒤 현재 값과 다음 값의 차이를 `statIncrease`로 표시하지만, 실제 캐릭터·전투 공격력과 최대 HP는 실수 기여값을 모두 합산한 뒤 반올림한다.

이 차이로 노말 장갑의 `+1→+2`, `+3→+4`, `+6→+7`, `+10→+11`, `+15→+16`에서 장비 화면이 `강화 시 +0`을 표시한다. 실제 최종 공격력도 다른 장비 구성과 반올림 경계에 따라 0 또는 1만큼 증가한다. 재화를 소비하는 강화에서 능력치가 오르지 않는 결과와 미리보기·실제 결과의 불일치가 모두 발생한다.

## 목표

- 모든 장비 강화는 해당 부위의 주 능력치를 정수 기준 최소 1 올린다.
- 공격력·최대 HP 장비는 동일한 강화 단계에서 상위 등급의 증가량이 바로 아래 등급보다 반드시 크다.
- 승급 직후의 주 능력치는 직전 등급 30강보다 반드시 크다.
- 장비 화면의 서버 계산 `statIncrease`와 실제 캐릭터·전투 능력치 변화를 같은 계산 정본에서 만든다.
- 기존 지수 성장 곡선과 승급 Q 점프를 가능한 한 유지한다.

## 비목표

- 장비 강화·승급 비용, 재료 세대, 성공률, 최대 강화 단계는 변경하지 않는다.
- 스킬, 보석, 치장 효과와 적 수치를 이 설계에서 직접 조정하지 않는다.
- 방어 관통 장비에 별도 등급 체감 배율을 추가하지 않는다. 다만 모든 강화에서 0 증가가 없어야 한다는 공통 불변식은 적용한다.
- 기존 계정의 장비 등급·강화 단계 저장값을 변경하지 않는다.

## 확정 규칙

### 용어

```text
gradeIndex = NORMAL 0, RARE 1, EPIC 2, LEGENDARY 3
level = 1..30
Q(grade, level) = gradeIndex × 39 + level
round(x) = 양의 값에 대한 round half-up
```

공격력·최대 HP 장비의 기본 계수 `B`는 현재 값을 유지한다.

| 부위 | 주 능력치 | B |
| --- | --- | ---: |
| 무기 | 공격력 | 36 |
| 장갑 | 공격력 | 24 |
| 갑옷 | 최대 HP | 360 |
| 투구 | 최대 HP | 240 |

### 등급 1강 기준값

각 등급 1강의 주 능력치 기여값은 현재 곡선을 유지한다.

```text
start(slot, grade) = round(B(slot) × M(Q(grade, 1)))
```

따라서 기존 등급별 시작점과 승급 Q 점프를 보존한다.

### 단계별 기준 증가량

각 등급의 `level→level+1` 기준 증가량은 현재 곡선의 정수 기여값 차이로 계산한다.

```text
rawDelta(slot, grade, level) =
    round(B(slot) × M(Q(grade, level + 1)))
  - round(B(slot) × M(Q(grade, level)))
```

### 단계별 보장 증가량

공격력·최대 HP 장비는 낮은 등급부터 다음 규칙으로 보정한다.

```text
guaranteedDelta(slot, NORMAL, level) =
    max(1, rawDelta(slot, NORMAL, level))

guaranteedDelta(slot, grade, level) =
    max(
        1,
        rawDelta(slot, grade, level),
        guaranteedDelta(slot, previousGrade, level) + 1
    )
```

이 규칙은 다음을 동시에 보장한다.

1. 모든 단계의 주 능력치 증가량이 최소 1이다.
2. 같은 `level→level+1`에서 `RARE > NORMAL`, `EPIC > RARE`, `LEGENDARY > EPIC`다.
3. 기존 곡선이 이미 조건을 만족하는 단계는 값을 바꾸지 않는다.

### 등급 내 누적 기여값

등급 1강 기준값에서 보장 증가량을 누적해 해당 단계의 정수 기여값을 계산한다.

```text
contribution(slot, grade, 1) = start(slot, grade)

contribution(slot, grade, level) =
    start(slot, grade)
  + sum(guaranteedDelta(slot, grade, step), step = 1..level-1)
```

장갑은 이 보정으로 노말 구간의 0 증가가 제거된다. 현재 곡선에서 이미 동일 단계 등급 증가 조건을 만족하는 무기·갑옷·투구는 원래 값에 최대한 수렴하며, 장갑의 희귀 일부 단계는 노말보다 크게 오르도록 보정된다.

### 승급 증가

승급 결과는 다음 등급 1강의 `start`를 사용한다. 현재 Q 간격은 등급 승급마다 10의 추가 점프를 두므로 공격력·최대 HP 네 부위 모두 다음을 만족한다.

```text
contribution(slot, nextGrade, 1) > contribution(slot, currentGrade, 30)
```

구현은 이 관계를 네 부위와 세 승급 경계 전체에서 검증한다. 관계가 깨지는 계산 변경은 허용하지 않는다.

### 방어 관통

망토와 신발은 현재 선형 공식을 유지한다.

```text
망토 = round(3.6 × Q)
신발 = round(2.4 × Q)
```

현재 모든 강화의 정수 증가량은 1 이상이다. 공통 전 구간 검증으로 이 불변식을 고정하되 공격력·최대 HP용 등급 간 `+1` 차이 보정은 적용하지 않는다.

## 단일 계산 경계

`EquipmentRules`가 등급·강화 단계에서 정수 장비 기여값을 계산하는 유일한 정본이 된다.

- 장비 상태 API의 `current.stats`, `enhance.result.stats`, `enhance.statIncrease`, 승급 결과는 이 계산을 사용한다.
- `CharacterStatsCalculator`는 장비별 실수 지수식을 다시 계산하지 않고 같은 정수 기여값을 사용한다.
- 캐릭터 화면, 전투 입력, 공개 전투력 계산은 `CharacterStatsCalculator`가 만든 동일 영구 스탯 스냅샷을 계속 사용한다.
- 웹 클라이언트는 서버 `statIncrease`를 그대로 표시하며 로컬 보정이나 `+0` 숨김을 하지 않는다.

이 경계로 장비 화면에서 안내한 증가량이 장비 기여값과 실제 캐릭터 기본 능력치에 동일하게 반영된다. 보석·치장 퍼센트 효과가 있는 경우 최종 능력치는 기존 순서대로 정수 장비 기여값과 고정 보너스를 합산한 뒤 퍼센트 효과를 적용하고 최종 반올림한다.

## 데이터와 호환성

장비 능력치는 저장하지 않고 `(slot, grade, enhancementLevel)`에서 파생한다. 따라서 DB migration과 기존 계정 데이터 변환은 없다. 배포 후 기존 계정도 현재 저장된 등급·강화 단계에 대응하는 새 파생값을 즉시 사용한다.

멱등 command 결과 JSON은 저장된 당시 응답을 재생할 수 있으므로, 이미 완료된 과거 command 재생 결과는 변경하지 않는다. 새 조회와 새 command 결과는 새 계산 규칙을 사용한다. 영구 상태와 재화 차감은 재실행하지 않는다.

## 문서 반영

구현과 같은 변경에서 다음 정본과 실행 상태를 갱신한다.

- `docs/30-domain/items/equipment/ssot.md`: 단계별 최소 증가, 동일 단계 등급 증가 보장, 서버 계산 정본
- `docs/30-domain/combat/ssot.md`: 장비 정수 기여값을 사용하는 최종 공격력·최대 HP 합산 공식
- `docs/80-decisions/README.md`: 사용자 확정과 기존 장비 성장 결정에 대한 전파 상태
- `docs/wiki/06-delivery/tasks/G-equipment-crafting/g-08-enhancement-slots-stat-table.md`: 구현·검증 증거
- `docs/wiki/06-delivery/tasks/G-equipment-crafting/_index.md`와 `docs/wiki/06-delivery/tasks/_index.md`: G-08 상태 동기화

## 검증

### 도메인 전 구간

공격력·최대 HP 네 부위, 네 등급, 등급당 29개 강화 전이를 전수 검사한다.

- 모든 `next - current >= 1`
- 같은 단계에서 `RARE > NORMAL`, `EPIC > RARE`, `LEGENDARY > EPIC`
- 세 승급 경계에서 `nextGrade +1 > currentGrade +30`
- Q와 강화 비용은 기존 값 유지
- 망토·신발도 모든 강화에서 `next - current >= 1`

### 서비스·API

- 노말 장갑의 기존 0 증가 다섯 구간이 모두 최소 `+1`을 반환한다.
- 강화 command 이후 `current.stats - previous.current.stats == previous.enhance.statIncrease`다.
- 재화 차감, 단계 증가, 멱등 replay는 기존 계약을 유지한다.
- API의 `statIncrease`와 캐릭터 기본 공격력·최대 HP 변화가 일치한다.

### 웹

- 장비 화면의 강화 가능한 행에 `강화 시 +0`이 나타나지 않는다.
- 서버가 반환한 양의 증가량을 변경 없이 표시한다.
- 강화 완료 후 장비 행과 캐릭터 능력치 조회가 갱신된 값을 표시한다.

### 밸런스 회귀

- `apps:balance-lab`의 장비 성장·전투 시뮬레이션을 새 계산 정본으로 실행한다.
- 기존 확정 적 수치나 성장벽 인수 조건이 깨지면 결과를 근거로 별도 밸런스 결정 대상으로 보고한다. 이 장비 수정에 적 수치 변경을 숨겨 포함하지 않는다.

## 인수 기준

1. 어떤 부위·등급·강화 단계에서도 주 능력치 `+0` 강화가 없다.
2. 무기·장갑·갑옷·투구의 같은 강화 단계 증가량은 등급이 오를수록 엄격히 커진다.
3. 세 승급 모두 다음 등급 1강의 주 능력치가 이전 등급 30강보다 크다.
4. 장비 API 미리보기와 강화 후 장비 기여값 차이가 일치한다.
5. 같은 장비 상태로 만든 캐릭터 화면과 전투 입력이 동일 공격력·최대 HP를 사용한다.
6. 강화·승급 비용, 재료, 성공 결과와 저장 상태는 기존 계약을 유지한다.
