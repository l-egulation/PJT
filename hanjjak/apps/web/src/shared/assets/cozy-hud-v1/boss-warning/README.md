# 보스 등장 경고 자산

보스 등장 경고는 고정 완성 이미지가 아니라 아래 투명 PNG와 런타임 텍스트·기존 몬스터 스프라이트를 조합한다.

- `regular-boss-aura.png`: 1~9 스테이지의 중간 보스 등장 오라
- `regular-boss-name-ribbon.png`: 동적 보스 이름을 올리는 공통 리본
- `final-boss-crest.png`: 10 스테이지 최종 보스 전용 강화 문장

## 조립 규칙

- 보스 이름과 `WARNING`·`FINAL BOSS` 제목은 이미지에 굽지 않고 HTML 텍스트로 표시한다.
- 보스 이미지는 `apps/web/public/assets/chapters/<chapter>/monsters/<monster-id>/<monster-id>-sprite-sheet-5x4.png`의 기존 스프라이트를 재사용한다.
- 1~9 스테이지는 공통 리본과 일반 오라를 사용한다.
- 10 스테이지는 공통 리본에 금색 CSS 테두리·텍스트를 적용하고 `final-boss-crest.png`를 추가한다.
- 화면 딤, 광선, 입자, 흔들림과 페이드 인·아웃은 CSS로 처리한다.
- 경고는 화면의 임의 처치 수가 아니라 서버 전투 사건 `BOSS_SPAWNED`를 기준으로 한 번만 재생한다.
- 자산은 실제 알파 채널을 포함하며 체크무늬 배경 이미지를 포함하지 않는다.

챕터 1~3의 스테이지별 일반·보스 편성과 프레젠테이션 ID는 `packages/game-content/versions/v1/stages/stages.json`이 소유한다. `apps/web/src/features/battle/battleVisuals.ts`는 해당 ID를 실제 배경·스프라이트 자산 경로와 시각 앵커로 변환한다.
