---
doc_kind: task
owner_domain: delivery
task_code: 'K-02'
task_area: ''
task_type: '기획'
priority: 'P0'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/e2706c8781b0838eba7e01175ba895d1'
notion_id: 'e2706c87-81b0-838e-ba7e-01175ba895d1'
snapshot_date: '2026-08-28'
---

# K-02 공통 디자인 시스템·컴포넌트 정의

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

색상, 글자, 간격, 버튼, 입력, 탭, 카드, 모달, 아이콘과 상태 표현 규칙이 정의된다.

## 선행 작업

K-01

## 비고

증거 확인 전 완료 처리 금지

## 증거 링크

- [메인 HUD 컴포넌트·에셋 분리 명세](../../../../70-plans/stage-screen/component-asset-spec.md)
- [Cozy HUD v1 신규 아이콘과 재사용 목록](../../../../../apps/web/src/shared/assets/cozy-hud-v1/README.md)
- 신규 아이콘 24종, 공통 컨트롤 5종, 전용 종이 프레임 6종은 규격 PNG와 투명 alpha를 검사했다.
- 2026-09-10 메인 HUD 조립: `apps/web/src/features/battle/BattleHud.tsx`, `BattleHud.css`, `BattleScreen.tsx`, `apps/web/src/main.tsx`; 종이 프레임을 HTML 텍스트·서버 값과 분리해 프로필, 전투 기록, 스테이지, 관리 메뉴, 획득, HP·EXP, 성장 메뉴를 재사용 컴포넌트로 연결했다.
- 2026-09-10 검증: 웹 전체 Vitest 43개 파일·150개 테스트, TypeScript typecheck, production build 통과. `battle-hud-preview.html`을 1265×712에서 렌더해 전체 HUD 무잘림과 전투 기록 확장 패널을 확인했다. 실제 서버 세션 장시간 상태 전환과 추가 해상도 검증은 남아 있다.
- 2026-09-10 종이 프레임 합성 보정: `BattleHud.css`에서 9-slice PNG의 투명 외곽 아래에 깔리던 사각 CSS 배경을 제거했다. 전체 종이·버튼 PNG의 alpha 범위와 투명 모서리를 검사하고 `battle-hud-preview.html` 1265×712 재렌더에서 배경이 자글자글한 외곽을 따라 드러나는 것을 확인했다.
- 2026-09-10 HUD 정렬 보정: 좌상단 프로필을 축소하고 전투 기록과 왼쪽 축을 맞췄으며, HP·EXP 상태판과 성장 메뉴를 화면 중앙축에 정렬했다. 1265×712 미리보기에서 상·하단 HUD와 우측 획득 패널의 무겹침을 확인했다.
- 2026-09-10 HUD 종이 화풍 통일: 기존 획득 패널·하단 상태/성장 바의 반듯한 크림 종이와 얇은 갈색·금빛 이중 테두리를 기준으로 프로필·전투 기록 버튼용 3:1 셸을 표시 비율에 맞춰 별도 생성했다. 전투 기록 확장창은 화풍이 다른 생성 셸을 제외하고 획득 패널의 검증된 9-slice 종이 프레임을 재사용해 가변 높이와 동일한 테두리 표현을 함께 유지했다.
- 2026-09-10 PPT 색감·서체 통일: 사용자 제공 `젓키 레퍼런스.pptx` 5장을 렌더링해 밝은 아이보리 종이, 금빛 목재, 초콜릿 잉크, 코랄 강조, 시안 경험치, 초록 상태 팔레트를 공통 CSS 변수로 정의했다. 저장소의 `Jua-Regular.ttf`를 공통 게임 UI 서체로 연결하고 전투 HUD·거래소·랭킹의 회갈색·검은 표면과 배경 딤을 밝은 계열로 보정했다.
- 2026-09-10 색감·서체 검증: `battle-hud-preview.html`에서 밝아진 전투 배경, 아이보리 종이, 코랄 상태 배지와 `Hanjjak Jua` 렌더를 확인했다. 웹 전체 43개 파일·153개 테스트, typecheck, production build와 `git diff --check`가 통과했다. 로컬 API 서버 3000 미실행에 따른 테스트 로그의 `ECONNREFUSED`는 모의 API fallback 이후 전체 통과 결과에 영향을 주지 않았다.
- 2026-09-11 광장 채팅 UI 에셋 분리: `apps/web/src/features/chat/assets-cozy-pixel`에 투명 배경의 종이 패널, 빈 메시지 행, 코랄 종이비행기 전송 버튼을 추가했다. 닉네임·본문·시각은 이미지에 굽지 않고 실제 API 응답을 HTML로 조합한다.
- 2026-09-11 전역 서체 교체: 사용자 제공 `긱블말랑이체.zip`의 원본 `GeekbleMalang2WOFF2.woff2`를 공용 웹폰트로 연결했다. 전역 CSS 변수와 입력·버튼 상속을 기준으로 로그인, 재료 선택, 전투 HUD, 관리 화면, 랭킹, 캐릭터, 마이페이지, 치장, 채팅의 화면별 `Jua`·명조·Pretendard 덮어쓰기를 제거해 같은 서체를 사용한다. 사용 가이드가 허용한 웹 임베딩 범위 안에서 원본 파일을 수정하지 않고 포함했다.
- 2026-09-11 전역 서체 검증: 웹 전체 54개 파일·311개 테스트, TypeScript typecheck, production build와 `git diff --check`가 통과했다. 빌드 산출물에는 167.10KB `GeekbleMalang2WOFF2.woff2`만 포함되고 기존 2MB대 `Jua-Regular.ttf`는 제외됨을 확인했다. `battle-hud-preview.html` 1440×730에서 인벤토리·장비·랭킹의 제목, 긴 아이템명, 전투력 수치, 버튼·입력과 특수문자 닫기·페이지 화살표의 무잘림을 확인했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
