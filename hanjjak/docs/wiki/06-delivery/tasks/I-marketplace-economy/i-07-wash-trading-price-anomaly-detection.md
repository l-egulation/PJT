---
doc_kind: task
owner_domain: delivery
task_code: 'I-07'
task_area: 'I 거래소·경제'
task_type: '검증'
priority: 'P1'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '미확인'
verification_status: '미확인'
deadline: ''
source_url: 'https://app.notion.com/p/e3a06c8781b083f58ddf01510016015d'
notion_id: 'e3a06c87-81b0-83f5-8ddf-01510016015d'
snapshot_date: '2026-08-28'
---

# I-07 주문 남용·자전거래·가격 이상 탐지

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [거래소](../../../../30-domain/economy/ssot.md)를 따른다.

## 완료 기준

계정별 상태 변경 성공 주문 mutation 최근 1분 30회 제한을 넘는 요청은 거절하고, 같은 계정·시장 품목·방향의 취소 후 재등록 또는 가격 변경이 10분 안에 20회 이상이면 차단 없이 반복 주문 후보·근거·규칙 버전을 기록한다. 비정상 가격 수치 기준은 30일 실제 분포와 오탐을 검토해 확정한다.

## 선행 작업

I-06

## 비고

증거 확인 전 완료 처리 금지

2026-09-11 후속 Office Hours에서 성공 mutation 이력 30일 보존, 반복 주문 후보 10분 20회와 운영 검토만 수행하는 조치를 확정했다. 비정상 가격의 기준 가격·배수·기간·저유동성 예외가 unresolved이므로 기획은 부분 완료로 유지한다.

## 증거 링크

없음

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 미확인 |
| 검증 | 미확인 |
