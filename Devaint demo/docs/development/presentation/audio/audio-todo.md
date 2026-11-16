# 외부에서 구할 효과음 TODO

[분야 개요](README.md)

미사일 변신 `sfx_special_bomb_movement`은 사용자가 고른 번들 음원으로 실제 변신 발동에 연결한다. 변신, 비행 루프, 충돌 폭발은 서로 다른 사건이다. 현재 재생 위치와 배치 계약은 [선택 음원과 재생 지점](audio-production.md#선택-음원과-재생-지점)을 따른다.

채찍 기본공격·휘감기·가시 그물·매듭 접근·매듭 조이기는 사용자가 확정한 Minifantasy 검 3 편집 설정을 사용한다. [실제 전투 재생](audio-production.md#실제-전투-재생)은 기준 119종과 현재 런타임 선택을 구분한다.

이전 프롬프트와 제작 설정은 [외부 소싱 참고 YAML](../../../../data/audio/todo/external-source-reference.yaml)에 보관한다. 이 파일은 자동 생성 배치가 아니다. 포함/제외 ID는 [production-scope.json](../../../../data/audio/production-scope.json), 수치는 [기본공격 시간표](basic-attack-audio-timing.md)와 전투 원본을 따른다.

새 음원은 사용권과 출처를 확인하고 제작 원본을 별도 작업 공간에 보관한다. 청취 선택 뒤 [저장 규칙](audio-production.md)에 맞춰 WAV·`.meta`·선택 매핑과 제작 정보를 함께 갱신한다.

들쥐·두더지·멧돼지·허수아비·짚단·두레박의 `_move` 이동 접촉 6종은 제작 대상에서 제외하며 외부 소싱 TODO에도 포함하지 않는다. 플레이어 발걸음·점프·착지는 유지한다.
