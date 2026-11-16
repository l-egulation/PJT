# 둥근 지형의 게임 연결

지형 제작 원본·렌더러·굽기·픽셀 비교 도구는 `resources/environments/forest/rounded-terrain/`와 `resources/tools/chapter1-rounded-terrain/`가 소유한다. 메인은 게임 기하·배치 JSON·Unity 어댑터·GUID를 유지한다.

일반 이미지 준비는 저장소 루트의 `에셋 업데이트.bat` 또는 `python tools/assets/update_assets.py`를 사용한다. 아틀라스·숲 배경과 필요한 타일 원본 3개는 같은 배치 계약에 포함한다. 별도 입력 폴더·ZIP·포장 도구를 사용하지 않는다.

제작 검토가 필요한 경우에만 Node.js와 `sharp`를 준비하고 새 빈 출력에 굽는다.

```sh
node resources/tools/chapter1-rounded-terrain/bake.cjs --main-repo . --destination outputs/terrain-review
```

`--ver2`는 기존 MapVer2 기하를 읽는 선택이며 구형 씬을 새로 생성하지 않는다. 결과는 검토용 `Art/`, `Data/`, `reference/`이며 메인 파일·메타데이터·배치 계약을 자동으로 갱신하지 않는다. `SHARP_MODULE`로 이미 설치된 모듈 경로를 지정할 수 있다.

렌더러는 5×5 점유 이웃의 모서리·천장 끝과 원본 타일을 조립한다. 32px 논리 격자, 잔디 윗장식9px·통과 발판 착지선11px와 현재 접촉선·피벗을 유지한다. 그림의 투명 가장자리와 물리 사각형을 구분하며 이미지 배치로 충돌·이동·발판 규칙을 바꾸지 않는다.

실제 Unity 카메라와의 픽셀 비교는 Unity **6000.6.5f1** 및 그래픽 장치에서 별도로 수행한다. `resources/tools/chapter1-rounded-terrain/compare.cjs --input <reference와 Unity 캡처 폴더>`는 검사 결과를 표준 출력으로 내며 원본을 수정하지 않는다. 전체 이동 경로·미술 승인·빌드 검증을 대신하지 않는다.
