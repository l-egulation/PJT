---
doc_kind: task
owner_domain: delivery
task_code: 'K-11'
task_area: ''
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/66f06c8781b0828eba4201d0e6c1cb87'
notion_id: '66f06c87-81b0-828e-ba42-01d0e6c1cb87'
snapshot_date: '2026-08-28'
---

# K-11 영구 장비 강화·승급 UI 구현

> 작업 상태와 완료 기준의 SSOT. 현재 UX 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

강화·승급 비용, 다음 결과, 재료 부족과 챕터·30강 게이트를 확인하고 서버 명령을 실행할 수 있다.

## 선행 작업

K-10

## 비고

강화 확률·판매 UI·장비 인스턴스 비교는 현재 영구 성장 모델의 범위가 아니다. 결과 불확실 시 같은 멱등 명령을 다시 확인하고, 확정된 4xx는 상태를 새로 조회한다.

부족한 강화 재료의 단일 품목 거래소 구매, 사용자 지정 가격 상한, 부분 구매 결과와 구매 후 별도 강화 확인은 [K-24](./k-24-equipment-shortage-market-purchase-ui.md)가 소유한다.

## 증거 링크

- UI: `apps/web/src/features/equipment/EquipmentScreen.tsx`
- API: `POST /api/v1/equipment/{slot}/enhance`, `POST /api/v1/equipment/{slot}/promote`
- 검증: 웹 테스트·typecheck·build 성공. 실제 Chromium에서 강화 카드의 서버 `statIncrease`, `30강 필요`·챕터 게이트, 승급 후 Q40 결과와 오류 비활성 상태를 확인했다.
- 2026-09-11 강화 결과 피드백: 강화·승급 명령 전후의 서버 전투력을 조회해 증가량이 양수일 때 화면 중앙에 약 5초간 `전투력 +N` 토스트를 표시한다. 랭킹 화면·파일은 변경하지 않았으며 기존 서버 전투력 조회 계약만 읽기 전용으로 사용한다. 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
