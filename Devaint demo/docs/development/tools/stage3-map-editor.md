# 3챕터 HTML 맵 편집기

[분야 개요](README.md)

[3챕터 맵 초안](../../design/world/chapters/chapter3-layout.md)을 Unity에 적용하기 전에 수정하는 로컬 HTML 도구다. 이미지·폰트·외부 라이브러리 없이 Canvas 도형과 HTML 폼으로 구성한다.

## 열기와 편집

저장소 루트의 `3챕터_맵편집기_열기.cmd` 또는 [단일 HTML](../../../tools/map/stage3-editor/3챕터_맵편집기.html)을 Chrome·Edge에서 연다. 개발용 [index.html](../../../tools/map/stage3-editor/index.html)은 같은 폴더의 CSS·JS를 사용한다. 상세 조작은 [사용안내.txt](../../../tools/map/stage3-editor/사용안내.txt)와 페이지의 물음표 버튼에서 확인한다.

- 전체 동선에서 방을 16×12u 단위로 옮긴다. 더블클릭하면 방 내부로 들어간다.
- 방 안에서 발판을 1u씩 옮기고 양끝 손잡이로 너비를 바꾼다. 고체는 네 모서리로 조절한다. 수치 입력·추가·복제·삭제도 지원한다.
- 연결 도구로 방 둘을 고른다. 연결 선을 선택해 방향과 슬롯을 수정한다. 문과 출입구 디딤은 자동 생성된다.
- 적·시작 위치·정비·회복·보상·보스·출구는 위치 표식이다.
- 실행 취소·다시 실행, JSON 다운로드·불러오기, 지도 PNG 다운로드를 지원한다. JSON 불러오기와 기본 초안 복원도 실행 취소할 수 있다.
- 수정은 브라우저 로컬 저장소에 자동 저장하지만 HTML 파일을 바꾸지는 않는다. 중요한 배치는 JSON으로 보관한다. 브라우저나 파일 경로가 달라지면 자동 저장이 공유되지 않을 수 있다.

## 코드와 데이터

| 파일 | 역할 |
|---|---|
| `tools/map/stage3-editor/map-core.js` | 기본 12방, 연결·출입구·충돌 기하, JSON 검증, 실행 취소 모델 |
| `tools/map/stage3-editor/editor.js` | 편집 화면, 마우스·키보드 조작, 저장, 간이 이동 테스트 |
| `tools/map/stage3-editor/editor.css`, `index.html` | 화면 스타일과 개발용 진입점 |
| `tools/map/stage3-editor/build.cjs` | 외부 참조 없는 단일 HTML과 기본맵 JSON 생성 |
| `tools/map/stage3-editor/checks/core.test.cjs`, `checks/editor-state.test.cjs` | 초안 계약·JSON 왕복·잘못된 입력·실행 취소·연결·문턱·지지면과 자동 저장·드래그 취소 회귀 검사 |

## 빌드와 검사

저장소 루트에서 Node.js의 기본 테스트 러너를 사용한다. 추가 패키지 설치는 필요하지 않다.

```sh
node --check tools/map/stage3-editor/editor.js
node --check tools/map/stage3-editor/map-core.js
node --test tools/map/stage3-editor/checks/core.test.cjs tools/map/stage3-editor/checks/editor-state.test.cjs
node tools/map/stage3-editor/build.cjs
python -I tools/check_docs.py
```

`build.cjs`는 JavaScript·CSS를 HTML 안에 넣고 `seed()`를 기본맵 JSON에 기록한다. 생성물만 직접 고치지 않는다.

배치 검사는 방·통로 겹침, 연결 정렬·중복·도달 관계, 발판 겹침·범위, 시설·적 지지면과 고체 묻힘을 확인한다. 점프 경로 전체를 자동 증명하지 않는다.

이동 테스트는 몸 0.9375×2u, 속도 7.5u/s, 두 점프 초속 20u/s와 상승·하강 중력을 사용한다. 좌우 이동·2단 점프·하향 통과만 실행하며 실제 Unity 모터의 벽타기·대시·입력 보정·전투는 재현하지 않는다. 외부 의존성 없는 편집 도구이므로 Unity 씬과 리소스 배치 계약은 변경하지 않는다.
