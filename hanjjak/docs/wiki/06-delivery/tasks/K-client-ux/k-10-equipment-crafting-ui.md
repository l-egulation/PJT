---
doc_kind: task
owner_domain: delivery
task_code: 'K-10'
jira_key: 'S15P21B107-205'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/a3606c8781b082819222817b02dce688'
notion_id: 'a3606c87-81b0-8281-9222-817b02dce688'
snapshot_date: '2026-08-28'
---

# K-10 영구 장비 성장 카드 UI 구현

> 작업 상태와 완료 기준의 SSOT. 현재 UX 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

6개 부위의 미해금·강화·승급·최종 완료 상태와 서버 비용·보유량·결과 미리보기를 확인하고 실행할 수 있다.

## 선행 작업

G-01, G-04, K-09

## 비고

기존 제작식·보유 장비·장착 슬롯 화면을 제거하고 6개 영구 성장 카드로 교체했다. 클라이언트는 Q·비용·챕터 조건을 계산하지 않는다.

## 증거 링크

- UI: `apps/web/src/features/equipment/EquipmentScreen.tsx`, `apps/web/src/features/equipment/EquipmentScreen.test.tsx`
- API: `apps/web/src/features/equipment/api.ts`
- 스타일: `apps/web/src/styles.css`
- 검증: 웹 14개 테스트 파일/31개 테스트, typecheck, Vite build 성공. 실제 Chromium에서 6개 카드, 강화 결과 갱신, 캐릭터 창 장비 스탯 출처 갱신, 375px 1열 레이아웃을 확인했다.
- 2026-09-11 등급별 재화 표시: 장비 비용과 보유 재화에 거래소와 같은 F~A 감자·고구마·옥수수 이미지를 연결했다. 보유 재화 행의 등급 선택으로 세 재료의 표시 수량·이미지가 함께 바뀌며, 각 재료에 포인터·키보드 초점을 두면 모든 등급의 보유량을 확인할 수 있다. 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.
- 2026-09-11 장비 창 닫기 복구: 장비 종이 프레임 우측 상단의 닫기 버튼을 레이아웃 흐름과 분리해 고정하고 픽셀 닫기 아이콘을 사용해 화면 높이와 결과 안내 상태에 관계없이 항상 보이도록 했다. 브라우저 미리보기에서 아이콘 노출과 닫기 동작을 확인했고 웹 전체 59개 파일·338개 테스트, TypeScript typecheck와 production build가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
