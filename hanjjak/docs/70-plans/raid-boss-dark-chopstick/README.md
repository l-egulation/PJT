---
doc_kind: plan
authority_level: candidate
---

# 흑화 젓가락 레이드 보스 디자인 시안

## 상태와 기준

- 아래 10개 안은 선택 전 `candidate` 시안이며 현재 게임 정책이나 확정 콘텐츠가 아니다.
- 주인공 정체성과 세계관 기준은 [Story](../../10-game/story.md)를 따른다.
- 본체는 두 짝이 아니라 두 레일이 하나로 합쳐진 젓가락형 몸이며, 공격은 장비·마법·소환 효과로 수행한다.
- 각 시안은 전신 디자인 1개와 공격 패턴 3개로 구성한다. 피격·사망 모션은 범위에서 제외한다.
- 이미지의 공격 장면은 형태와 판독성 탐색용이며 실제 범위·수치·상태 전이를 확정하지 않는다. 전투 규칙 정본은 [전투 SSOT](../../30-domain/combat/ssot.md)다.

## 시안 10종

| 번호 | 시안 | 공격 패턴 1 | 공격 패턴 2 | 공격 패턴 3 |
| --- | --- | --- | --- | --- |
| 01 | [심연 불씨 군주](./concepts/01-abyss-ember-sovereign.png) | 거대 초승달 참격 | 지연 심연 기둥 | 흑염 파편 방사 |
| 02 | [균열 거울 도플갱어](./concepts/02-shattered-mirror-doppelganger.png) | 분신 교차 돌진 | 회전 수정 광선 | 표식 파편 내파 |
| 03 | [저주받은 옻칠 무사](./concepts/03-cursed-lacquer-shogun.png) | 발도 일섬 | 영체 갑주 내려찍기 | 원형 칼날 장벽 |
| 04 | [공허의 주방 집정관](./concepts/04-void-kitchen-archon.png) | 팬 회오리 | 삼열 단두 참격 | 솥 운석 낙하 |
| 05 | [혈월의 수확자](./concepts/05-blood-moon-reaver.png) | 반원 낫 베기 | 동심원 월광파 | 붉은 리본 속박 |
| 06 | [폭풍의 폭군](./concepts/06-storm-tyrant.png) | 낙뢰 망치 강타 | 추적 뇌전 구체 | 수축 번개 감옥 |
| 07 | [병든 대나무 리치](./concepts/07-blighted-bamboo-lich.png) | 부채꼴 망령 뿌리 | 음식 망령 돌진 | 안전 구역 독무 |
| 08 | [우주 일식 황제](./concepts/08-cosmic-eclipse-emperor.png) | 중력 특이점 | 순차 별창 광선 | 명암 부채꼴 전환 |
| 09 | [도깨비 잔치대왕](./concepts/09-dokkaebi-feastlord.png) | 철퇴 부채꼴 폭발 | 곡사 혼불 그릇 | 도깨비 전방 충격파 |
| 10 | [타락한 영웅 최종각성](./concepts/10-fallen-hero-final-ascension.png) | 삼단 대검 연계 | 추적 에너지 깃털 | 전장 낙하검과 안전 고리 |

## 생성 프롬프트 요약

기준 이미지의 합쳐진 젓가락 실루엣, 작은 검은 타원 눈, 짧은 팔다리를 유지하고 흑화한 레이드 보스로 확장했다. 각 안에는 하나의 큰 전신 디자인과 정확히 세 개의 공격 장면을 배치했으며, 텍스트·UI·피격·사망 모션은 제외했다. 시안별 차이는 심연, 거울, 옻칠 무사, 주방, 혈월, 뇌전, 고목, 일식, 도깨비, 최종 각성 테마와 대응 장비·오라·소환 효과다.

## 5초·64프레임 스프라이트 후보

사용자가 다시 첨부한 심연 불씨 군주, 병든 대나무 리치, 타락한 영웅 최종각성 이미지를 기준으로 후보 스프라이트를 제작했다. 각 보스는 대기와 공격 3종을 구분하며, 동작마다 8개의 실제 키포즈와 이징 중간 프레임을 합친 64프레임을 재생한다. GIF의 10ms 시간 단위에 맞춰 실제 프레임 시간은 70ms와 80ms를 배분해 합계가 정확히 5초가 되도록 했다. 256×256 논리 픽셀에서 합성한 뒤 정확히 2배 최근접 확대해 512×512 셀의 픽셀 크기를 균일하게 유지한다. 전체 아틀라스는 16열×16행의 8192×8192다. GIF는 모든 프레임에 같은 팔레트를 적용하고 디더링을 끄며, 미리보기는 한 번에 하나만 재생해 디코딩 부하를 제한한다. 이 규격과 애니메이션은 아직 `candidate`로 런타임이나 콘텐츠 버전에 반영하지 않았다.

- [GIF·시트 미리보기 HTML](./sprites/preview.html)
- [심연 불씨 군주 64프레임 시트](./sprites/01-abyss-ember-sovereign/01-abyss-ember-sovereign-sprite-sheet-64f-512.png)
- [병든 대나무 리치 64프레임 시트](./sprites/07-blighted-bamboo-lich/07-blighted-bamboo-lich-sprite-sheet-64f-512.png)
- [타락한 영웅 최종각성 64프레임 시트](./sprites/10-fallen-hero-final-ascension/10-fallen-hero-final-ascension-sprite-sheet-64f-512.png)

## 선택 시안 모션 v2

첨부 영상의 동작 길이와 프레임 전개를 기준으로 `타락한 영웅 최종각성`을 우선 모션 시안으로 정리했다. 5초 균등 재생 후보와 달리 v2는 모션마다 실제로 생성된 키포즈 10장을 사용하고, 준비 자세는 읽히게 유지하면서 가속·타격 프레임을 짧게 배치한다. 대기는 1.2초, 돌진 베기는 0.9초, 회전 베기는 1.0초, 낙하검 결계는 1.6초다. 합성 크로스페이드는 사용하지 않아 픽셀 외곽과 실루엣이 흐려지지 않는다.

시트는 256×256 셀의 10열×4행 RGBA PNG이며, 행 순서는 `idle`, `dash-slash`, `spin-slash`, `blade-rain`이다. GIF는 확인 편의를 위해 512×512 최근접 확대본으로 제공한다. 이 선택과 타이밍도 `candidate`이며 전투 범위·피해·상태 전이를 확정하지 않는다.

- [선택 시안 GIF 미리보기 HTML](./sprites/final-preview-v2.html)
- [선택 시안 10×4 RGBA 스프라이트 시트](./sprites/10-fallen-hero-final-motion-v2/fallen-hero-final-motion-sprite-sheet-10x4-v2.png)
- [프레임 좌표·가변 타이밍 JSON](./sprites/10-fallen-hero-final-motion-v2/fallen-hero-final-motion-sprite-sheet-10x4-v2.json)

## 고해상도 모션 v3

v2의 자글거림은 1983×793 한 장에 40개 키포즈를 배치한 원본을 512px 프레임으로 최근접 확대하면서 셀당 원화 해상도가 부족했던 것이 원인이었다. v3는 대기·돌진 베기·회전 베기·낙하검 결계를 각각 독립된 4×3 고해상도 원화로 생성해 동작당 12개, 총 48개의 실제 키포즈를 사용한다.

최종 RGBA 아틀라스는 셀당 768×768, 12열×4행의 9216×3072이며 v2보다 약 10.8배 많은 전체 픽셀을 사용한다. HTML 미리보기는 최근접 확대를 강제하지 않고 부드러운 브라우저 스케일링을 사용한다. 재생 속도는 0.1×~2.0×에서 조절하며 기본값은 0.4×이고, 일시정지와 처음부터 재생을 지원한다.

배경 제거는 시트 외곽과 연결된 중성색 체크 매트만 투명 처리해 보스 내부의 검은색·회색 디테일을 보존한다. 대표 네 프레임의 외곽을 5배 최근접 확대하고 녹색·자홍색 배경에 교차 합성한 검사본을 함께 둔다. 이 자산도 시각 `candidate`이며 전투 규칙·범위·수치를 확정하지 않는다.

- [고해상도 GIF 미리보기 HTML](./sprites/final-preview-v3-hd.html)
- [9216×3072 RGBA 스프라이트 시트](./sprites/10-fallen-hero-final-motion-v3-hd/fallen-hero-final-motion-sprite-sheet-12x4-768-v3.png)
- [고해상도 시트 축소 미리보기](./sprites/10-fallen-hero-final-motion-v3-hd/fallen-hero-final-motion-sprite-sheet-12x4-v3-preview.png)
- [48프레임 좌표·타이밍 JSON](./sprites/10-fallen-hero-final-motion-v3-hd/fallen-hero-final-motion-sprite-sheet-12x4-768-v3.json)
- [배경 제거 5배 확대 검사본](./sprites/10-fallen-hero-final-motion-v3-hd/background-removal-edge-check-5x.png)
