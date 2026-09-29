---
doc_kind: task
owner_domain: delivery
task_code: 'K-12'
task_area: ''
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/d2b06c8781b0828ea44e0159ec9ba7f3'
notion_id: 'd2b06c87-81b0-828e-a44e-0159ec9ba7f3'
snapshot_date: '2026-08-28'
---

# K-12 스킬 성장·장착·자동 사용 순서 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

성장 탭에서 등급·단계·효과·책과 쌀 비용·성공률·실패 보정을 확인하고 강화할 수 있으며, 장착 탭에서 액티브 4슬롯의 자동 사용 순서를 변경·저장할 수 있다. 부족한 스킬북은 해당 품목의 거래소 검색 결과로 이동한다.

## 선행 작업

H-03, H-04, K-02, K-13

## 비고

캐릭터·장비와 같은 밝은 종이 모달 안에 자동 사용 4슬롯과 2×3 성장 카드를 배치했다. 카드에서 등급·단계·효과·책과 쌀 비용·성공률을 확인하고 성장 명령과 액티브 자동 사용 순서를 변경·저장할 수 있다. 카드 본문에는 필요한 스킬북 수량만 표시하고 개별 보유량 팝업은 제거했다. 설명과 재화 행의 세로 여백을 압축하고 카드 높이를 콘텐츠 기준으로 맞춰 위아래 잔여 공간을 줄였으며, 자동 사용 영역과 성장 카드 사이의 간격도 좁혀 상단 정보가 한 덩어리로 이어지게 했다. 종이 배경은 고정한 채 제목부터 성장 카드까지 내부 콘텐츠 전체를 아래로 10px 이동했다. 카드의 자동 사용·상시 적용 배지는 제거하고 성장 성공률을 등급·강화 수치와 같은 정보 행으로 이동했으며, 성공률 글자와 성장 버튼 폭을 확대해 카드 안의 정보 위계와 조작 인지를 보강했다. 빈 자동 사용 슬롯을 누르면 카드 선택 모드로 전환되며 장착 가능한 액티브만 강조하고, 이미 장착된 스킬과 패시브는 선택 불가 상태를 표시한다. 장착된 슬롯을 누르면 해당 스킬을 해제한다. 사용자 제공 스킬 이미지 6종은 은색 프레임 바깥 배경을 투명 처리해 모두 반영했다. 부족 스킬북의 거래소 검색 이동은 아직 구현하지 않았다.

## 증거 링크

- UI: `apps/web/src/features/skills/SkillsScreen.tsx`, `apps/web/src/features/skills/api.ts`, `apps/web/src/main.tsx`, `apps/web/src/styles.css`
- 런타임 이미지와 추가 규칙: `apps/web/src/features/skills/assets-cozy-pixel/README.md`, `apps/web/src/features/skills/assets-cozy-pixel/skill-*.png`
- 미리보기: `apps/web/skills-preview.html`, `apps/web/src/features/skills/skills-preview.tsx`
- 검증: `pnpm --filter @hanjjak/web test` 175건 통과, `pnpm --filter @hanjjak/web typecheck` 통과, `pnpm --filter @hanjjak/web build` 통과. 스킬 화면 단위 테스트로 필요 스킬북 수량 표시, 6종 아이콘 등록, 빈 슬롯 선택, 장착 해제와 불확실 명령 차단을 확인했다. 브라우저 스모크에서 전투 화면 위 스킬 모달, 4슬롯 자동 사용, 2×3 카드와 스킬 이미지 6종을 확인했다. 1280×768 데스크톱 뷰에서는 종이 모달과 성장 카드 6종이 바깥 스크롤 없이 한 화면에 표시되는지 추가 확인했다.
- 2026-09-11 장착·재화 가독성 보정: 장착 여부를 카드의 지연된 스냅샷이 아니라 서버 `activeLoadout`에서 직접 파생해 해제 직후 다시 선택·장착할 수 있게 했다. 성장 카드에는 F~A 등급별 스킬북 `보유 / 필요` 수량과 부족 상태를 표시한다. 장착 해제 후 재장착 회귀 테스트를 포함해 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 production build를 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
