# 공통 픽셀 편집기

고정 진입점은 `index.html`이다. 무기/모션마다 새로운 HTML을 복제하지 않는다.

## 작업 데이터 추가

```powershell
python set_project.py "C:/Downloads/사용자수정.json" --title "표창 · 최신 수정본"
```

이 명령은 원본 JSON을 `projects/`에 바이트 그대로 보관하고 `project-data.js`만 갱신한다. `index.html`, `editor.css`, `editor.js`, `editor-core.js`는 변경하지 않는다. 기존 작업을 활성 상태로 유지하려면 `--keep-active`를 사용한다.

편집 화면의 작업 선택 또는 JSON 불러오기도 동일한 편집기를 사용한다. 처음 체크아웃하면 `node build-projects.cjs`를 실행해 `../approved/manifest.json`에 등록한 확정 모션을 구성한다. 프로젝트 목록과 묶음은 재생성 파일이므로 Git에서 제외한다. 재생성 전에 추가 편집본은 JSON으로 저장한다.

## 지원 데이터

- `bodyImages`, `weaponImage`, `placements`, `sourceFrames`를 가진 몸체·무기 분리 JSON
- `images`, `sourceFrames`를 가진 여러 프레임 JSON
- `image`를 가진 단일 프레임 JSON

새 형식이 필요하면 데이터 변환이나 `editor-core.js`의 어댑터를 보완한다. 개별 전용 편집기를 새로 만들지 않는다.

## 유지할 조작

진회색/회색/흰색/체크무늬 배경, 확대, 격자, 이전 프레임 겹치기, 재생 속도, 몸체/무기 보기, 펜/지우개/영역 채우기/색 추출, 무기 배치, Ctrl+Z와 다시 실행, 전체 JSON 저장, 투명 PNG, 에테르용 흰 배경 8배 PNG.

화면 설정은 공통으로 저장하고, 임시 수정본은 작업 ID별로 분리한다. 새 원본 파일은 내용 해시로 다른 ID를 받아 이전 임시 저장이 덮어쓰지 않는다. 동일한 공유 대기 프레임은 내용과 배치가 같을 때만 연결하며 다르면 양쪽 수정본을 보존한다.

미리보기 배경과 격자는 PNG에 섞지 않는다. 8배 내보내기만 흰 배경으로 확대한다. 사용자 JSON의 다른 메타데이터는 내보내기에 유지한다.

## 마스터 팔레트 잠금

`paletteLock: true`인 작업은 JSON의 `palette`에 있는 정확한 RGB 색상과 완전 투명 RGBA(0,0,0,0)만 허용한다. 불러오기·펜·영역 채우기·내보내기에서 팔레트 밖 색과 반투명 픽셀을 거부한다. 잠금 없는 기존 작업의 색 편집 동작은 유지한다. `node --test palette-lock.test.cjs`로 잠금과 편집·취소·다시 실행의 동작을 검증한다.
