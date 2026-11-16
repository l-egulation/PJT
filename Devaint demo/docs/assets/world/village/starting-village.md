# 시작 마을 에셋과 적용 경계

[분야 개요](README.md)

## 저장 위치

새 이미지 원본·게임용 내보내기·미리보기는 메인의 `resources/` Git LFS 서브모듈에 있다. Drive에 같은 새 사본을 중복 게시하지 않는다. [리소스 저장 규칙](../../resource-repository.md)과 `resources/AGENTS.md`를 따른다. 기존 가져오기 원본 7,836개와 ZIP 대응은 보존하고 새 제작물은 별도 `generated-assets.json`으로 검사한다.

| 대상 | 리소스 경로 | 내용 |
|---|---|---|
| 건물·작업대·거점 소품 | `environments/village/`의 `starting-hub` 세트 | 건물 3종과 작업대·우물·출발 문 7종, 각 컬러/스케치 짝 20PNG |
| 도트 문양 | `ui/roguelite-hub/runtime/icons/` | 해금 6종·마법안료·시설/상태 문양, 32×32 단색 16PNG |
| 표현 효과 | `effects/hub-unlock/runtime/` | 해금·안료 획득 2클립, 각 8프레임, 총 16PNG |
| 이야기 배경 | `sequences/starting-village-intro/runtime/` | 기록 보관소·지워진 농가·같은 자리의 각성 배경, 960×540 3PNG |
| 규격·재사용 목록 | `ui/roguelite-hub/source/sprite-layout.json` | PPU·피벗·접촉선·알파·프레임 시간·기존 리소스 참조 |
| 전체 미리보기 | [에셋 갤러리](../../../../resources/ui/roguelite-hub/preview/index.html) | 컬러/스케치 전환, 독립 PNG, 실제 기존 아트와의 합성 비교 |

게임용 출력은 55PNG다. 생성 원화 13PNG와 요청문·가공 도구를 별도로 보존한다. 합성·비교·문양 시트 6PNG는 검토용이며 게임에 배경으로 그대로 삽입하지 않는다. 미리보기는 Unity 실행 화면이 아니다. 새 에셋은 사용자 최종 미술 승인 전 작업본이며, `runtime` 경로가 승인을 뜻하지 않는다.

## 재사용과 새 제작의 경계

기록 농가의 집, 마을 원경·하늘·지형·식생, 주인공·무기 모션, 기존 귀환/포탈 표현은 기존 리소스를 참조한다. 폰트·공통 프레임·버튼·슬롯·코인은 현재 메인의 `TemporaryGUI`와 Galmuri를 재사용한다. 새 상점 주민·초상·NPC 걷기 모션을 요구하는 구조로 바꾸지 않는다. 독립 작업대만으로 시설을 이용할 수 있다.

이야기 배경 3장은 정지 화면과 기존 캐릭터·대사를 조립하기 위한 자료다. 별도 원본이 없는 `StoryReference` 98PNG와 전체 잠입·이동 튜토리얼을 새로 제작한 결과가 아니다. 기존 초반 사건 순서를 보존하고, 전체 장면 재현을 선택하면 해당 원본의 실제 복원·추가 제작을 완료해야 한다. 미복원 자료를 받았거나 전체 오프닝을 검증했다고 보고하지 않는다.

## Unity 적용 기준

메인의 바인딩 원본은 [starting_hub.json](../../../../data/assets/starting_hub.json)이다. 소스·대상 구분과 개별 메타데이터를 읽고 필요한 게임용 출력만 `unity/Assets/Deviant/Hub/` 아래에 적용한다. 원본·생성 요청문·미리보기·가공용 PNG를 Resources에 함께 넣지 않는다.

월드 PNG는 PPU32, Point, 무압축, mipmap 없음, FullRect를 사용한다. 실제 피벗은 `sprite-layout.json`의 개별 값을 사용하며 모든 이미지를 바닥 중앙 `(0.5,0)`으로 가정하지 않는다. 투명 캔버스를 다시 자르지 않는다. 컬러/스케치는 크기·알파·피벗·접촉선이 동일하다.

문양은 32×32 흰색 알파 마스크이며 uGUI tint로 표시한다. 숫자·이름·가격·효과를 이미지에 굽지 않고 Galmuri 텍스트로 따로 표시한다. 기존 정적 글리프에 새 마을 문구가 없으면 기존 글리프를 보존해 확장한다. 표현 효과의 80ms는 아트 기본 시간이며 실제 기능·보상 확정 시점과 분리한다.

## 받기와 검증

```sh
GIT_LFS_SKIP_SMUDGE=1 git submodule update --init --recursive --depth 1 resources
git -C resources lfs install --local
git -C resources -c lfs.fetchrecentalways=false lfs pull
python resources/tools/configure_lfs.py
python resources/tools/verify_resources.py --check-links
python resources/tools/verify_starting_hub.py
python -m unittest discover -s resources/tools -p test_generated_resources.py
```

현재 체크아웃의 파일만 받는다. [환경 설정](../../../getting-started/setup.md#외부-리소스-서브모듈)의 얕은 받기·LFS 범위 제한을 따르며 PowerShell 환경 변수 문법도 그 안내를 사용한다. 과거 전체 이력이나 모든 버전의 LFS 원본을 자동으로 받지 않는다.

원화 재내보내기는 `resources/tools/export_starting_hub.py` → `build_starting_hub_preview.py` → `index_starting_hub.py` 순서다. 실제 원본 수정 의도가 있을 때만 색인 해시를 갱신한다. 기존 입력 ZIP과 이관 매핑의 해시를 새 결과에 맞춰 바꾸지 않는다.

기능 연결과 인수는 [A–Z 구현 프롬프트](../../../development/gameplay/village/starting-village-implementation.md)를 따른다. 이 에셋 전달은 프로필 저장·해금·보너스·마을 씬·빌드 플레이의 구현 완료가 아니다.
