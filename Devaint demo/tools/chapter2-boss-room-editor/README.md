# Rose · Blue 보스방 편집기

`index.html`을 브라우저에서 직접 연다. 외부 라이브러리나 서버가 필요 없다.

- 1스테이지 `data/combat/chapter1_selected_rooms.json`의 `special:boss`와 같은 30×18unit이다. 초기 바닥 두께는 3unit, 바닥 위 빈 높이는 15unit이다. 좌표는 좌하단 원점·Y 위쪽이다.
- 벽·바닥과 통과형 발판을 드래그해 그린다. 발판 두께는 0.5unit이다.
- 선택 도구로 요소를 드래그하거나 오른쪽 X/Y/너비/높이를 입력한다. 방향키는 현재 격자만큼 이동한다. Delete로 삭제하며 Ctrl+Z/Y로 취소·다시 실행한다.
- Rose/Blue/플레이어 원본 이미지를 표시한다. X는 가로 중심, Y는 발밑이다. 너비·높이, 비율 잠금, 좌우 반전을 지원한다. 플레이어 가시 높이는 2u, 보스 초기 크기는 비교용 초안이다. 피격 판정을 확정하지 않는다.
- 선택한 캐릭터·지형은 Ctrl+C/V로 복제한다. 새 ID로 만들고 0.5u 옆·위로 옮기되 방 경계 안으로 제한한다. 입력칸에서는 일반 텍스트 복사·붙여넣기를 유지한다.
- 미디어 없는 편집기 원본은 `template.html`, 이미지 내장 실행본은 `resources/monsters/stage-02/elite-boss-pattern-drafts/preview/boss-room/index.html`이다. 이 폴더의 `index.html`은 실행본 안내다.
- 재생성: `python resources/monsters/stage-02/elite-boss-pattern-drafts/source/build_boss_room.py` (메인 루트에서 실행).
- **편집한 HTML 저장**으로 도구와 현재 데이터를 함께 내려받는다. 내려받은 HTML을 다시 열어 편집할 수 있다. HTML/JSON 파일 열기도 지원한다. 브라우저 자동 저장은 사용하지 않는다.
- 이 파일은 독립 공간 편집 데이터다. 기존 전체 맵 편집기용 JSON과 스키마가 다르며 기존 import 도구에 바로 전달하지 않는다. 승인 데이터는 `data/combat/chapter2_rose_blue_arena.json`을 거쳐 RoseBlueStage가 실행 중 적용한다. 기존 전체 맵 JSON이나 씬을 덮어쓰지 않는다.

## 페이즈별 추천 배치

- 편집 기본 화면은 1페이즈다. 화면 선택에서 첫 조우·2페이즈·3페이즈로 전환한다. 1페이즈에서만 발판 7개와 기계를 표시하고 다른 단계에서는 바닥을 표시한다.
- 추천 좌표 원본은 `recommended-room.json`이다. 발판 윗면 Y=5/9.5/14u, 층 간격 4.5u이며 2층 중앙 기계는 (14, 9.5), 2×3u다. 현재 기본값은 사용자가 제출한 `rose-blue-boss-room (1).html`의 승인 좌표다. 1·3층 X=6/17, 2층 X=2/12/22이며 실제 점프·회피 난도는 Unity에서 별도 확인한다.
- 기계도 선택·이동·크기 조절·복사가 가능하다. 복사/붙여넣기 버튼과 Ctrl+C/V는 같은 동작을 호출하며 현재 화면에 복제한다.
- `surfaces`·`markers`·`devices`는 각 요소의 `phases`로 표시 단계를 구분한다. 예전 파일의 단계 정보가 없는 요소는 모든 화면에 표시하고, `devices`가 없으면 빈 배열로 읽는다.
- 파일 저장은 모든 단계의 데이터를 함께 보존한다. 화면별 캐릭터 위치는 독립적인 배치 참고이며 피격 박스·실제 등장 시간은 의미하지 않는다.
