---
doc_kind: reference
source_system: Notion
source_collection: project-materials
notion_id: 'eef06c87-81b0-83b5-932c-81d57e62e946'
notion_title: 'DB병목 및 카프카 실험'
source_url: 'https://app.notion.com/p/eef06c8781b083b5932c81d57e62e946?pvs=204'
snapshot_date: '2026-08-28'
---

# DB병목 및 카프카 실험

> 실험 원문 보관본. 결론은 데이터·이벤트 SSOT와 배포·운영 SSOT에 반영된 범위만 현재 기준이다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/eef06c8781b083b5932c81d57e62e946 as of 2026-08-28T00:23:33.414Z:
<page url="https://app.notion.com/p/eef06c8781b083b5932c81d57e62e946" icon="icons/compressed-document_gray">
<ancestor-path>
<parent-data-source url="collection://4f806c87-81b0-8303-ad48-0755c71f61f1" name="프로젝트 자료실"/>
<ancestor-2-database url="https://app.notion.com/p/fe806c8781b08319832901d6a6520179" title=""/>
<ancestor-3-page url="https://app.notion.com/p/1dd06c8781b083669ab681e49cefffdd" title="자료실"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/eef06c8781b083b5932c81d57e62e946","링크":"","생성일":"2026-08-28T00:23:33.443Z","이름":"DB병목 및 카프카 실험"}
</properties>
<content>
<empty-block/>
## 1. 우리가 처음 해결하려던 문제는 뭐였나?
처음부터 문제를 **“Kafka를 어떻게 쓸까?”**로 잡은 게 아니야.
문제 정의는 이거였어.
> **게임 거래소에 거래 데이터가 계속 발생하도록 만들었을 때, 현재 Spring + MySQL 구조가 어디까지 버티고, 느려진다면 가장 먼저 어디가 병목이 되는가?**
그리고 그 병목을 하나씩 해결해 본 다음,
> **그래도 게임 요청과 거래 처리를 분리해야 하거나, 여러 기능이 같은 거래 이벤트를 사용해야 한다면 그때 Kafka를 쓰자.**
라는 방향이었어.
README에서도 처음 목표를 Kafka 도입이 아니라 **시장 데이터가 실제로 계속 발생하는지 확인하는 것**으로 잡고 있어.
---
# 2. 그 실험을 하기 위해 어떤 전제조건을 만들었나?
쉽게 말하면 **“거래가 억지로라도 계속 발생하는 게임 경제”**를 만든 거야.
### 전제 1. 유저를 A/B/C 직군으로 나눈다
예를 들어 A 직군은 A 재료를 잘 먹고, B/C 재료는 잘 못 먹게 했어.
```plain text
A 유저 → A 재료 80%
B 유저 → B 재료 80%
C 유저 → C 재료 80%
```
그런데 장비를 만들거나 강화하려면 **A/B/C 재료가 전부 필요해.**
그러니까 자연스럽게:
```plain text
나는 A는 많은데 B가 부족함
→ A를 팔고 B를 삼
```
이 구조가 되는 거지.
### 전제 2. 계속 성장하려면 계속 재료가 필요하다
장비 제작뿐만 아니라 강화도 계속 가능하도록 해서,
> "재료를 한 번 사고 끝"
이 아니라 계속 수요가 생기게 만들었어.
### 전제 3. 부족하면 사고, 남으면 판다
유저가 직접 버튼을 누르는 대신 봇이 자동으로 판단해.
```plain text
부족 → 구매 주문
남음 → 판매 주문
```
그래서 실제 유저가 많지 않아도 한 명이 계속 거래 데이터를 만들어낼 수 있게 했어.
### 전제 4. 거래소 규칙은 유지한다
그리고 매칭은 실제 거래소처럼:
```plain text
판매 → 가장 싼 것 우선
구매 → 가장 비싼 것 우선
같은 가격 → 먼저 등록한 것 우선
자기 자신과 거래 금지
```
라는 규칙을 유지했어.
이게 나중에 굉장히 중요해.
왜냐하면 **성능을 위해 마음대로 병렬 처리하면 가격 우선순위가 깨질 수 있기 때문**이야.
---
# 3. 그러면 병목은 몇 단계로 넘어갔나?
나는 크게 **6단계**로 보면 가장 이해하기 쉽다고 봐.
---
## STEP 1. 시장 처리를 너무 자주 했다
처음에는:
```plain text
재료 하나 획득
→ 시장 판단
→ 주문 생성
→ 매칭

재료 하나 획득
→ 시장 판단
→ 주문 생성
→ 매칭
```
이걸 계속 반복했어.
TPS를 올렸더니 실행 시간이 폭발적으로 증가했어.
그런데 DB 상세 저장을 하지 않는 `summary`에서도 느렸기 때문에:
> **DB 문제가 아니라 애플리케이션에서 시장 매칭을 너무 자주 돌리는 게 문제다.**
라고 판단했어.
### 해결
여러 이벤트를 모아서 한 번 시장 처리를 했어.
```plain text
이벤트
이벤트
이벤트
이벤트
이벤트
      ↓
한 번에 시장 처리
```
즉 **Batch 처리**.
결과적으로 약 346초 → 9.6초까지 줄어든 실험도 있었어.
---
# STEP 2. DB에 너무 비효율적으로 대량 저장했다
첫 번째를 해결하고 MySQL에 실제 주문/거래 데이터를 저장했더니 또 느려졌어.
그래서 시간을:
```plain text
계산 시간
DB 저장 시간
```
으로 나눠서 봤어.
그리고 동일한 약 12만 row를 저장했는데:
```plain text
JPA saveAll → 약 86초
JDBC Batch → 약 2초
```
였어.
즉:
> ❌ MySQL 자체가 느리다
	✅ 대량 데이터를 JPA 방식으로 저장하는 방법이 비효율적이었다
였던 거지.
### 해결
```plain text
JPA saveAll
↓
JDBC batch insert
```
---
# STEP 3. 주문 검색 알고리즘이 느렸다
DB 저장을 빨리 만들었더니 이제 계산 시간이 보였어.
```plain text
compute 약 39초
그중 match 약 38.5초
```
거의 매칭이 전부였던 거야.
왜?
매 거래마다 List 전체를 뒤져서:
```plain text
가장 싼 판매 주문 찾아
가장 비싼 구매 주문 찾아
```
를 반복하고 있었어.
### 해결
```plain text
List
↓
PriorityQueue
```
로 바꿨어.
그러면 항상 맨 앞에 최저가/최고가가 있게 돼.
결과:
```plain text
match
38.5초 → 약 0.1초

전체
41초 → 약 2.2초
```
까지 내려갔어.
---
# STEP 4. 거래 체결 트랜잭션이 너무 많은 일을 했다
이제 실제 동시 거래를 보니까 체결할 때 한 트랜잭션에서:
```plain text
주문 잠금
주문 수량 변경
판매자 User 잠금
구매자 User 잠금
골드 변경
재료 변경
거래 기록
```
전부 처리하고 있었어.
즉 **체결 하나가 너무 무거웠던 것**이야.
### 해결
체결과 정산을 분리했어.
```plain text
체결
→ 주문만 변경
→ 거래 기록
→ 정산 이벤트 생성

정산
→ 나중에 판매자 골드 지급
→ 구매자 재료 지급
```
그래서 체결에서는 User row lock을 제거했어.
실제로 체결 구간에서 `userLock = 0ms`가 됐고 체결 자체는 30\~50% 빨라졌어.
그런데 여기서 재미있는 일이 생겨.
---
# STEP 5. 체결을 빠르게 만들었더니 정산이 느려졌다
체결/정산을 분리했더니 전체 성능이 오히려 떨어졌어.
왜?
정산 이벤트 120개를:
```plain text
이벤트 1개
→ 트랜잭션
→ commit

이벤트 1개
→ 트랜잭션
→ commit
```
이렇게 처리했거든.
즉:
```plain text
120 이벤트
=
120 transaction
=
120 commit
=
120 fsync
```
문제는 정산 계산이 아니고 **commit 횟수**였어.
### 해결
정산도 Batch.
그리고 동일 유저의 여러 이벤트는 합쳤어.
```plain text
유저 A +10 gold
유저 A +20 gold
유저 A +5 gold

↓ 합산

유저 A +35 gold
```
결과:
```plain text
정산 시간
2098ms → 80ms

transaction
120 → 1

commit
1711ms → 54ms
```
약 **26배 개선**됐어.
---
# STEP 6. 이제 DB 쿼리와 락 자체를 봤다
정산까지 줄이고 나니까 다시 체결이 병목이 됐어.
이번에는 SQL까지 내려갔어.
원래:
```sql
WHERE material = ?
AND quantity > 0
ORDER BY price, id
LIMIT 1
FOR UPDATE
```
였어.
문제는 `quantity > 0`이 **range 조건**이라서 인덱스가 뒤의 `price, id` 정렬을 제대로 못 써.
그래서 MySQL이:
```plain text
여러 row 읽음
↓
정렬(filesort)
↓
최상위 주문 1개 선택
```
을 하고 있었고,
거기에 `FOR UPDATE`까지 붙어서 불필요하게 많은 row를 잠그고 있었어.
### 해결
```plain text
quantity > 0
↓
status = OPEN
```
으로 바꾸고 인덱스를 맞췄어.
그러니까:
```plain text
OPEN인 주문
+
가격 순
+
LIMIT 1
```
을 인덱스로 바로 찾게 됐어.
결과:
> **rows_read 9\~16배 감소 + filesort 제거**
까지 갔어.
---
# 그래서 지금 어디까지 온 거야?
전체를 한 줄로 연결하면:
```plain text
① 시장 처리 횟수
   ↓ batch

② DB 대량 INSERT
   ↓ JDBC batch

③ 주문 검색 알고리즘
   ↓ PriorityQueue

④ 체결 트랜잭션이 너무 무거움
   ↓ 체결 / 정산 분리

⑤ 정산 commit 너무 많음
   ↓ 정산 batch + 유저별 합산

⑥ 후보 주문 SQL이 너무 많이 읽고 잠금
   ↓ status=OPEN + index

⑦ 현재
   주문장 맨 앞 row를 여러 요청이 동시에 잡으려는 락 경합
```
여기까지야.
---
# 4. 그러면 DB 병목은 어느 정도 해결한 거야?
꽤 많이 해결했다고 봐도 돼.
README에서도 현재 잡은 걸:
```plain text
정산 commit 병목       해결
후보 조회 filesort     해결
불필요한 rows_read     해결
Self Trade 안정성      해결
deadlock/retry 안정화  해결
```
로 정리하고 있어.
현재 남은 핵심 문제는 단순히 DB가 구려서가 아니야.
## 거래소 자체의 구조적 문제야.
예를 들어 판매 주문이:
```plain text
9원
10원
11원
```
있다면 무조건 9원부터 체결해야 하잖아.
그러면 워커가 10개 있어도:
```plain text
Worker1 ─┐
Worker2 ─┤
Worker3 ─┤→ 전부 9원 주문을 노림
Worker4 ─┤
Worker5 ─┘
```
이렇게 돼.
그래서 지금 남은 병목은:
> **“후보를 못 찾는 문제”가 아니라 “후보는 바로 찾는데 모두 같은 후보를 기다리는 문제”**
야.
이건 Kafka를 넣는다고 자동으로 해결되지 않아.
---
# 5. 이제 Kafka를 왜 쓰면 되는가?
여기서 생각을 바꿔야 해.
> ❌ Kafka로 DB lock 병목을 해결한다.
가 아니라
> ✅ **거래가 체결된 뒤 그 이벤트를 여러 기능이 사용하도록 한다.**
이게 훨씬 자연스러워.
README도 이 방향이야.
예를 들어 거래 하나가 체결돼.
```plain text
거래 체결
   ↓
TradeCompletedEvent
```
그러면 여러 기능이 필요할 수 있어.
```plain text
                    ┌→ 정산
                    │
거래 체결 → Kafka ──┼→ 랭킹
                    │
                    ├→ 통계
                    │
                    ├→ 알림
                    │
                    └→ 실시간 로그
```
여기서 Kafka가 의미 있어지는 거야.
---
# 내가 추천하는 Kafka 기능은 3개
처음부터 5개 다 만들 필요 없어.
### 1순위: 거래 통계
```plain text
TradeCompleted
↓
Kafka
↓
통계 Consumer
↓
오늘 총 거래량
재료별 거래량
유저별 거래량
실시간 거래 TPS
```
가장 만들기 쉽고 **빅데이터 프로젝트 느낌도 좋아.**
---
### 2순위: 거래 랭킹
예를 들어:
```plain text
가장 많이 거래한 유저
가장 많이 판매한 유저
가장 비싼 거래
재료별 인기 순위
```
를 Kafka Consumer가 계산하게 하는 거야.
그러면:
```plain text
Matching Server
```
는 랭킹 계산을 전혀 신경 쓰지 않아.
---
### 3순위: 실시간 알림
예:
```plain text
내 아이템 판매 완료
내 구매 주문 체결
희귀 아이템 거래 발생
```
거래 서버가 직접 알림까지 보내지 않고:
```plain text
체결
↓
Kafka
↓
Notification Consumer
```
로 보내면 돼.
---
# 그러면 우리가 만들 최종 그림은 이렇게 보면 돼
지금은:
```plain text
사냥
 ↓
이벤트 처리
 ↓
주문
 ↓
거래 체결
 ↓
MySQL
```
여기까지 DB 병목을 많이 잡아놓은 상태야.
다음에는:
```plain text
                 ┌→ 통계 Consumer
                 │
거래 체결 → Kafka ┼→ 랭킹 Consumer
                 │
                 └→ 알림 Consumer
```
를 붙이는 거야.
그러면 프로젝트 설명도 굉장히 자연스러워져.
> **처음부터 Kafka를 사용하지 않고 단일 Spring/MySQL 구조에서 부하를 발생시켰습니다. 시장 처리 빈도, 대량 INSERT, 매칭 알고리즘, 트랜잭션, 커밋, 인덱스 순으로 병목을 측정하고 개선했습니다. DB 내부에서 해결할 수 있는 병목을 먼저 제거한 뒤, 거래 체결 이벤트를 통계·랭킹·알림 등 여러 기능으로 분배해야 하는 요구가 생겼을 때 Kafka를 도입했습니다.**
이게 지금 프로젝트에서 우리가 가져가면 좋은 **전체 문제해결 스토리**야.
</content>
</page>
