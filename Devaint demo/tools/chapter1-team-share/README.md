# 1챕터 팀 공유 페이지

`build_share.py`는 실제 Unity 플레이 캡처를 이미지 데이터로 내장한 오프라인 HTML을 `outputs/team-share/chapter1-playtest-share.html`에 만듭니다. Python Pillow가 필요합니다.

- 방/길목/시설/배치/전투 캡처 입력: `outputs/room-replacement/captures/`
- 시작 도면 캡처 입력: `outputs/starting-blueprints/`
- 실행: `python tools/chapter1-team-share/build_share.py`
- 검증: Playwright를 설치한 Node 환경에서 `node tools/chapter1-team-share/verify.cjs` (필요하면 NODE_PATH 지정)

`LocalTeamShareCapture.cs`는 Unity 6000.6.5f1의 별도 검증 프로젝트 `Assets/Editor/`에 복사해 쓰는 캡처 도구입니다. 원본 프로젝트에 설치하지 않습니다. `CHAPTER1_SHARE_OUTPUT`에 캡처 절대 경로를 지정하고 Unity `-executeMethod LocalTeamShareCapture.Run`으로 실행합니다. 플레이 모드를 시작하고 종료하므로 사용 중인 에디터에서는 실행하지 않습니다. 시작 도면 캡처는 별도 준비해야 합니다.

HTML의 878,169,600은 방 선택·순서·ㄷ자 좌우·갈래 방향의 설계상 상한이며, 모든 시드를 전수 실행해 얻은 실제 고유 배치 수는 아닙니다. 생성 HTML과 캡처는 Git에 포함하지 않고 MR 첨부 파일로 전달합니다.

축소 맵 캡처를 사용하려면 `CHAPTER1_SHARE_OUTPUT`에 `outputs/compact-layout-preview/captures`의 절대 경로를 지정하고 생성기를 실행합니다. 캡처 도구와 생성기가 같은 환경변수를 사용합니다.
