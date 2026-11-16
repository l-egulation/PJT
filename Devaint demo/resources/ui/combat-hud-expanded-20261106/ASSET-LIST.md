# 전투 HUD · 출발 팔레트 에셋 목록

선택 무기는 검 4종·둔기 3종·원거리 3종으로 총 10종입니다. 발사기는 차징 후 발사·레이저·샷건·유탄포 스킬을 장착하며, 스킬에 맞춰 큰 무기 원과 출발 카드의 외형을 바꿉니다. 외형 13종, 특수공격 40종, 잉크 54종을 포함합니다. Unity 연결·실행 검증과 새 원화의 미술 승인은 별도입니다.

| 묶음 | 제공 내용 | 상태 |
|---|---|---|
| 체력 | 프레임, 초상, 실제 빨간 HP 채움 | 완료 |
| 오른쪽 위 | 골드, 새 안료 아이콘, 가방, 일시정지, 공용 버튼 | 완료 |
| 지도 | 얇은 테두리, 표시 비율 313:201, 상단 버튼 아래 배치 | 완료 |
| 무기 | 검 4 + 둔기 3 + 원거리 3, 발사기 스킬 외형 3개 추가 | 10무기 · 13외형 |
| 특수공격 | 동작 원화 + 무기 식별 레이어 조합 | 발사기 포함 40종 |
| 발사기 | 차징 후 발사·레이저·샷건·유탄포를 장착 스킬로 표시, 큰 무기 외형도 연동 | 4스킬 · 게임 바인딩 별도 |
| 팔레트 | 빨강·노랑·초록·파랑·보라, 별도 무색 범용 칸 | 5색 + 무색 완료 |
| 팔레트 크기 | 3·4·5칸 받침, 9-slice 규격, 출발 카드 내 자동 간격 | 완료 |
| 잉크 파츠 | 실제 ID·이름·색·효과·트리거·등급 연결 및 조합 PNG | 54종 완료 |
| 상태 | 선택/대기, 빈 무기, 빈/잠긴 칸, 쿨타임, 사용 횟수, 차징/완료, 스킬 없음 | 완료 |
| 런 시작 | 카드 2장, 무기 교체, 3~5색, 3.5초 표시 | 조립 미리보기 완료 |
| Unity 적용 | 프리팹·실제 게임 이벤트 바인딩·실행 검증 | 미적용 |

총 runtime PNG **169개**. 다음 제작 구분을 섞어 세지 않습니다.

| 제작 구분 | 파일 수 |
|---|---|
| 이전 단계의 새 독립 원화 유지 | 18 |
| 직접 작성한 UI 도형 | 20 |
| 이번 단계 새 독립 원화 | 37 |
| 독립 원화의 레이어 조합 | 94 |

54종 잉크와 40종 특수공격은 공용 원화를 조합한 결과입니다. 공용 원화와 색·효과·무기·발동 조건을 조합한 에셋입니다. `runtime/composition-recipes.json`에서 레이어를 수정할 수 있습니다. 출발 팔레트에는 파츠 문양 대신 순수 색 구성만 표시합니다. 무색은 잉크 종류가 아닌 범용 슬롯입니다.

## 무기와 특수공격

| ID | 이름 | 유형 | 특수공격 |
|---|---|---|
| `base_sword` | 기본검 | sword | 강베기 · 검기 · 돌진 찌르기 · 잔상 |
| `rapier` | 레이피어 | sword | 모아찌르기 · 장침 · 흘려찌르기 · 섬광 연침 |
| `whip` | 채찍 | sword | 휘감기 · 가시 그물 · 매듭 접근 · 매듭 조이기 |
| `dagger` | 한손 단검 | sword | 숨죽이기 · 먹안개 · 삼연 도약 · 그림자 난무 |
| `hammer` | 기본망치 | blunt | 횡강타 · 지축 강타 · 몰아치기 · 여진 |
| `greatsword` | 대검 | blunt | 모아내려찍기 · 반월참 · 회전 전진 · 낙인 대검 |
| `shield` | 방패 | blunt | 받아치기 · 수호막 · 방패 밀기 · 미끼 방패 |
| `launcher` | 기본 발사기 | ranged | 차징 후 발사 · 레이저 · 샷건 · 유탄포 |
| `shuriken` | 수리검 | ranged | 관통 수리검 · 폭발 수리검 · 뒤로 이동 · 복귀 · 전진 회전 수리검 |
| `bomb` | 폭탄 | ranged | 3연속 투척 · 대형 폭탄 · 미사일 변신 · 착지 시한폭탄 |

원거리 특수공격의 ID는 원본 JSON의 필드에 연결하는 UI 별칭입니다. 게임 내부 enum/저장 ID를 새로 확정하지 않습니다. 신규 무기는 catalog 항목과 아이콘을 추가하면 같은 슬롯에 표시되며, 미등록 ID는 빈 슬롯 표시를 사용합니다. 미확정 원거리 4번째 형태는 임의로 만들지 않았습니다.

## 잉크 54종

| ID | 이름 | 색 | 효과 종류 | 발동 조건 | 내부 등급 |
|---|---|---|---|---|---|
| `ink3_red_01` | 덧칠 | red | extra_hit | hit | 1 |
| `ink3_red_02` | 집중 공격 | red | focus_stack_damage | hit | 3 |
| `ink3_red_03` | 힘 실어 건네기 | red | next_attack_power | switch | 2 |
| `ink3_red_04` | 터지는 잉크 | red | burst_extra_hit | mark_burst | 4 |
| `ink3_red_05` | 사방으로 튀는 잉크 | red | kill_area_hit | kill | 1 |
| `ink3_red_06` | 기세를 몰아 | red | kill_stack_attack_power | kill | 2 |
| `ink3_red_07` | 붉은 낙관 | red | mark_damage_bonus | passive | 3 |
| `ink3_red_08` | 결정타 | red | execute_damage | hit | 2 |
| `ink3_red_09` | 농묵 | red | risky_attack_power | passive | 3 |
| `ink3_red_10` | 밑줄 긋기 | red | underline_mark | hit | 2 |
| `ink3_yellow_01` | 전기 발자국 | yellow | electric_field | move | 3 |
| `ink3_yellow_02` | 뛰어난 발걸음 | yellow | attack_and_move_speed | passive | 1 |
| `ink3_yellow_03` | 몸통 박치기 | yellow | dash_hit | dash | 2 |
| `ink3_yellow_04` | 휘몰아치기 | yellow | attack_and_move_speed | switch | 2 |
| `ink3_yellow_05` | 전기 장판 | yellow | electric_field | mark_burst | 4 |
| `ink3_yellow_06` | 신난다 | yellow | moving_attack_power | kill | 1 |
| `ink3_yellow_07` | 끊어 치기 | yellow | hit_recovery_cancel | hit | 3 |
| `ink3_yellow_08` | 번개 표식 | yellow | chain_lightning | mark_pop | 2 |
| `ink3_yellow_09` | 번지는 표식 | yellow | mark_chain_spread | mark_burst | 4 |
| `ink3_yellow_10` | 질주 충전 | yellow | move_charge | move | 1 |
| `ink3_yellow_11` | 내달린 획 | yellow | dash_followup | passive | 2 |
| `ink3_yellow_12` | 옮겨 쓰기 | yellow | switch_slide | switch | 3 |
| `ink3_green_01` | 든든한 배리어 | green | shielded_attack_power | passive | 3 |
| `ink3_green_02` | 보호막 교체 | green | shield | switch | 2 |
| `ink3_green_03` | 가시갑옷 | green | homing_counter | damaged | 4 |
| `ink3_green_04` | 전투 식량 | green | heal | kill | 1 |
| `ink3_green_05` | 티끌 모아 태산 | green | heal | hit | 1 |
| `ink3_green_06` | 풀컨디션 | green | high_health_attack_power | passive | 2 |
| `ink3_green_07` | 초록 수액 | green | heal | mark_pop | 3 |
| `ink3_green_08` | 단단한 껍질 | green | hit_guard_shield | damaged | 2 |
| `ink3_green_09` | 되갚는 잉크 | green | stored_retaliation | damaged | 2 |
| `ink3_green_10` | 갑옷 깁기 | green | shield | kill | 2 |
| `ink3_blue_01` | 범위 증폭 | blue | area_radius | passive | 2 |
| `ink3_blue_02` | 교대 타격 | blue | cooldown_reduce | switch | 1 |
| `ink3_blue_03` | 여의봉 | blue | attack_reach | passive | 2 |
| `ink3_blue_04` | 난무 | blue | cooldown_reduce | hit | 3 |
| `ink3_blue_05` | 공명 | blue | cooldown_reduce | mark_burst | 4 |
| `ink3_blue_06` | 학살 | blue | area_radius | kill | 1 |
| `ink3_blue_07` | 푸른 순환 | blue | cooldown_reduce | mark_pop | 3 |
| `ink3_blue_08` | 색 번짐 | blue | mark_spread | hit | 4 |
| `ink3_blue_09` | 겹쳐 쓰기 | blue | extra_skill_charge | passive | 4 |
| `ink3_blue_10` | 정리 정돈 | blue | cooldown_reduce | kill | 1 |
| `ink3_blue_11` | 앞당긴 획 | blue | next_attack_geometry | switch | 2 |
| `ink3_purple_01` | 먹물 늪 | purple | bind | hit | 2 |
| `ink3_purple_02` | 잔류 폭발 | purple | area_hit | bind_end | 3 |
| `ink3_purple_03` | 선제 제압 | purple | stun | hit | 2 |
| `ink3_purple_04` | 뇌진탕 | purple | stunned_dot | passive | 3 |
| `ink3_purple_05` | 사약 | purple | poison | hit | 1 |
| `ink3_purple_06` | 종합 병원 | purple | cc_count_hit | mark_burst | 4 |
| `ink3_purple_07` | 쪽빛 족쇄 | purple | bind | mark_pop | 3 |
| `ink3_purple_08` | 보랏빛 낙인 | purple | poison | mark_pop | 4 |
| `ink3_purple_09` | 독폭탄 | purple | poison_burst | hit | 2 |
| `ink3_purple_10` | 넘어뜨리기 | purple | stun | dash | 3 |
| `ink3_purple_11` | 약점 찌르기 | purple | controlled_damage | passive | 2 |

## 독립 PNG 파일

| ID | 용도 | 제작 |
|---|---|---|
| [health-plaque](runtime/sprites/health-plaque.png) | 체력 프레임 | 이전 단계의 새 독립 원화 유지 |
| [weapon-disc](runtime/sprites/weapon-disc.png) | 무기·스킬 공용 원형 슬롯 | 이전 단계의 새 독립 원화 유지 |
| [run-card](runtime/sprites/run-card.png) | 출발 팔레트 카드 | 이전 단계의 새 독립 원화 유지 |
| [heading-plaque](runtime/sprites/heading-plaque.png) | 출발 팔레트 제목판 | 이전 단계의 새 독립 원화 유지 |
| [button-frame](runtime/sprites/button-frame.png) | 버튼·키캡 바탕 | 이전 단계의 새 독립 원화 유지 |
| [weapon-sword](runtime/sprites/weapon-sword.png) | 검 아이콘 | 이전 단계의 새 독립 원화 유지 |
| [weapon-hammer](runtime/sprites/weapon-hammer.png) | 망치 아이콘 | 이전 단계의 새 독립 원화 유지 |
| [skill-slash](runtime/sprites/skill-slash.png) | 검 기본 공격 | 이전 단계의 새 독립 원화 유지 |
| [skill-swirl](runtime/sprites/skill-swirl.png) | 회전 스킬 | 이전 단계의 새 독립 원화 유지 |
| [drop-blue](runtime/sprites/drop-blue.png) | 파랑 물방울 | 이전 단계의 새 독립 원화 유지 |
| [drop-yellow](runtime/sprites/drop-yellow.png) | 노랑 물방울 | 이전 단계의 새 독립 원화 유지 |
| [drop-red](runtime/sprites/drop-red.png) | 빨강 물방울 | 이전 단계의 새 독립 원화 유지 |
| [drop-green](runtime/sprites/drop-green.png) | 초록 물방울 | 이전 단계의 새 독립 원화 유지 |
| [skill-impact](runtime/sprites/skill-impact.png) | 망치 기본 공격 | 이전 단계의 새 독립 원화 유지 |
| [hero-portrait](runtime/sprites/hero-portrait.png) | 캐릭터 초상 | 이전 단계의 새 독립 원화 유지 |
| [gold-coin](runtime/sprites/gold-coin.png) | 골드 | 이전 단계의 새 독립 원화 유지 |
| [bag-glyph](runtime/sprites/bag-glyph.png) | 인벤토리 | 이전 단계의 새 독립 원화 유지 |
| [radial-ring-clean](runtime/sprites/radial-ring-clean.png) | 얇은 원형 링 | 이전 단계의 새 독립 원화 유지 |
| [progress-ring-mask](runtime/sprites/progress-ring-mask.png) | 진행률 원형 마스크 | 직접 작성한 UI 도형 |
| [pause-glyph](runtime/sprites/pause-glyph.png) | 일시정지 | 직접 작성한 UI 도형 |
| [corner-accent](runtime/sprites/corner-accent.png) | 카드 모서리 강조 | 직접 작성한 UI 도형 |
| [chevron](runtime/sprites/chevron.png) | 제목 화살표 | 직접 작성한 UI 도형 |
| [divider](runtime/sprites/divider.png) | 카드 구분선 | 직접 작성한 UI 도형 |
| [weapon-whip](runtime/sprites/weapon-whip.png) | 채찍 | 이번 단계 새 독립 원화 |
| [weapon-rapier](runtime/sprites/weapon-rapier.png) | 레이피어 | 이번 단계 새 독립 원화 |
| [weapon-dagger](runtime/sprites/weapon-dagger.png) | 한손 단검 | 이번 단계 새 독립 원화 |
| [weapon-shield](runtime/sprites/weapon-shield.png) | 방패 | 이번 단계 새 독립 원화 |
| [weapon-greatsword](runtime/sprites/weapon-greatsword.png) | 대검 | 이번 단계 새 독립 원화 |
| [weapon-shuriken](runtime/sprites/weapon-shuriken.png) | 수리검 | 이번 단계 새 독립 원화 |
| [weapon-bomb](runtime/sprites/weapon-bomb.png) | 폭탄 | 이번 단계 새 독립 원화 |
| [minimap-frame-compact](runtime/sprites/minimap-frame-compact.png) | 미니맵 · 출발 카드와 같은 비율 | 이번 단계 새 독립 원화 |
| [pigment-currency](runtime/sprites/pigment-currency.png) | 안료 재화 · 새 원화 | 이번 단계 새 독립 원화 |
| [drop-purple](runtime/sprites/drop-purple.png) | 보라 잉크 | 이번 단계 새 독립 원화 |
| [drop-colorless](runtime/sprites/drop-colorless.png) | 무색 범용 칸 | 이번 단계 새 독립 원화 |
| [action-wave](runtime/sprites/action-wave.png) | action-wave | 이번 단계 새 독립 원화 |
| [action-dash](runtime/sprites/action-dash.png) | action-dash | 이번 단계 새 독립 원화 |
| [action-echo](runtime/sprites/action-echo.png) | action-echo | 이번 단계 새 독립 원화 |
| [action-bind](runtime/sprites/action-bind.png) | action-bind | 이번 단계 새 독립 원화 |
| [action-net](runtime/sprites/action-net.png) | action-net | 이번 단계 새 독립 원화 |
| [action-thrust](runtime/sprites/action-thrust.png) | action-thrust | 이번 단계 새 독립 원화 |
| [action-stealth](runtime/sprites/action-stealth.png) | action-stealth | 이번 단계 새 독립 원화 |
| [action-mist](runtime/sprites/action-mist.png) | action-mist | 이번 단계 새 독립 원화 |
| [action-shockwave](runtime/sprites/action-shockwave.png) | action-shockwave | 이번 단계 새 독립 원화 |
| [action-guard](runtime/sprites/action-guard.png) | action-guard | 이번 단계 새 독립 원화 |
| [action-decoy](runtime/sprites/action-decoy.png) | action-decoy | 이번 단계 새 독립 원화 |
| [action-mark](runtime/sprites/action-mark.png) | action-mark | 이번 단계 새 독립 원화 |
| [action-timer](runtime/sprites/action-timer.png) | action-timer | 이번 단계 새 독립 원화 |
| [action-ball](runtime/sprites/action-ball.png) | action-ball | 이번 단계 새 독립 원화 |
| [action-pellets](runtime/sprites/action-pellets.png) | action-pellets | 이번 단계 새 독립 원화 |
| [action-grenade](runtime/sprites/action-grenade.png) | action-grenade | 이번 단계 새 독립 원화 |
| [action-missile](runtime/sprites/action-missile.png) | action-missile | 이번 단계 새 독립 원화 |
| [action-teleport](runtime/sprites/action-teleport.png) | action-teleport | 이번 단계 새 독립 원화 |
| [action-heal](runtime/sprites/action-heal.png) | action-heal | 이번 단계 새 독립 원화 |
| [action-lightning](runtime/sprites/action-lightning.png) | action-lightning | 이번 단계 새 독립 원화 |
| [action-poison](runtime/sprites/action-poison.png) | action-poison | 이번 단계 새 독립 원화 |
| [action-beam-clean](runtime/sprites/action-beam-clean.png) | action-beam-clean | 이번 단계 새 독립 원화 |
| [weapon-launcher](runtime/sprites/weapon-launcher.png) | 발사기 | 이번 단계 새 독립 원화 |
| [weapon-launcher-laser](runtime/sprites/weapon-launcher-laser.png) | 레이저 | 이번 단계 새 독립 원화 |
| [weapon-launcher-shotgun](runtime/sprites/weapon-launcher-shotgun.png) | 샷건 | 이번 단계 새 독립 원화 |
| [weapon-launcher-grenade](runtime/sprites/weapon-launcher-grenade.png) | 유탄발사기 | 이번 단계 새 독립 원화 |
| [health-fill-red](runtime/sprites/health-fill-red.png) | 빨간 체력 채움 | 직접 작성한 UI 도형 |
| [ring-selected](runtime/sprites/ring-selected.png) | 선택 무기 링 | 직접 작성한 UI 도형 |
| [ring-idle](runtime/sprites/ring-idle.png) | 대기 무기 링 | 직접 작성한 UI 도형 |
| [ring-cooldown](runtime/sprites/ring-cooldown.png) | 쿨타임 링 | 직접 작성한 UI 도형 |
| [ring-charge](runtime/sprites/ring-charge.png) | 차징 진행 링 | 직접 작성한 UI 도형 |
| [palette-tray-3](runtime/sprites/palette-tray-3.png) | 3칸 팔레트 받침 | 직접 작성한 UI 도형 |
| [palette-tray-4](runtime/sprites/palette-tray-4.png) | 4칸 팔레트 받침 | 직접 작성한 UI 도형 |
| [palette-tray-5](runtime/sprites/palette-tray-5.png) | 5칸 팔레트 받침 | 직접 작성한 UI 도형 |
| [charge-pip-ready](runtime/sprites/charge-pip-ready.png) | 사용 가능 스킬 횟수 | 직접 작성한 UI 도형 |
| [charge-pip-empty](runtime/sprites/charge-pip-empty.png) | 회복 중 스킬 횟수 | 직접 작성한 UI 도형 |
| [slot-empty](runtime/sprites/slot-empty.png) | 빈 잉크 칸 | 직접 작성한 UI 도형 |
| [slot-locked](runtime/sprites/slot-locked.png) | 잠긴 잉크 칸 | 직접 작성한 UI 도형 |
| [state-unavailable](runtime/sprites/state-unavailable.png) | 사용 불가 | 직접 작성한 UI 도형 |
| [state-empty-weapon](runtime/sprites/state-empty-weapon.png) | 빈 무기 칸 | 직접 작성한 UI 도형 |
| [state-ready](runtime/sprites/state-ready.png) | 차징 완료 | 직접 작성한 UI 도형 |
| [special-base_sword_default](runtime/sprites/special-base_sword_default.png) | 강베기 | 독립 원화의 레이어 조합 |
| [special-base_sword_range](runtime/sprites/special-base_sword_range.png) | 검기 | 독립 원화의 레이어 조합 |
| [special-base_sword_movement](runtime/sprites/special-base_sword_movement.png) | 돌진 찌르기 | 독립 원화의 레이어 조합 |
| [special-base_sword_follow_up](runtime/sprites/special-base_sword_follow_up.png) | 잔상 | 독립 원화의 레이어 조합 |
| [special-whip_default](runtime/sprites/special-whip_default.png) | 휘감기 | 독립 원화의 레이어 조합 |
| [special-whip_range](runtime/sprites/special-whip_range.png) | 가시 그물 | 독립 원화의 레이어 조합 |
| [special-whip_movement](runtime/sprites/special-whip_movement.png) | 매듭 접근 | 독립 원화의 레이어 조합 |
| [special-whip_follow_up](runtime/sprites/special-whip_follow_up.png) | 매듭 조이기 | 독립 원화의 레이어 조합 |
| [special-rapier_default](runtime/sprites/special-rapier_default.png) | 모아찌르기 | 독립 원화의 레이어 조합 |
| [special-rapier_range](runtime/sprites/special-rapier_range.png) | 장침 | 독립 원화의 레이어 조합 |
| [special-rapier_movement](runtime/sprites/special-rapier_movement.png) | 흘려찌르기 | 독립 원화의 레이어 조합 |
| [special-rapier_follow_up](runtime/sprites/special-rapier_follow_up.png) | 섬광 연침 | 독립 원화의 레이어 조합 |
| [special-dagger_default](runtime/sprites/special-dagger_default.png) | 숨죽이기 | 독립 원화의 레이어 조합 |
| [special-dagger_range](runtime/sprites/special-dagger_range.png) | 먹안개 | 독립 원화의 레이어 조합 |
| [special-dagger_movement](runtime/sprites/special-dagger_movement.png) | 삼연 도약 | 독립 원화의 레이어 조합 |
| [special-dagger_follow_up](runtime/sprites/special-dagger_follow_up.png) | 그림자 난무 | 독립 원화의 레이어 조합 |
| [special-hammer_default](runtime/sprites/special-hammer_default.png) | 횡강타 | 독립 원화의 레이어 조합 |
| [special-hammer_range](runtime/sprites/special-hammer_range.png) | 지축 강타 | 독립 원화의 레이어 조합 |
| [special-hammer_movement](runtime/sprites/special-hammer_movement.png) | 몰아치기 | 독립 원화의 레이어 조합 |
| [special-hammer_follow_up](runtime/sprites/special-hammer_follow_up.png) | 여진 | 독립 원화의 레이어 조합 |
| [special-shield_default](runtime/sprites/special-shield_default.png) | 받아치기 | 독립 원화의 레이어 조합 |
| [special-shield_range](runtime/sprites/special-shield_range.png) | 수호막 | 독립 원화의 레이어 조합 |
| [special-shield_movement](runtime/sprites/special-shield_movement.png) | 방패 밀기 | 독립 원화의 레이어 조합 |
| [special-shield_follow_up](runtime/sprites/special-shield_follow_up.png) | 미끼 방패 | 독립 원화의 레이어 조합 |
| [special-greatsword_default](runtime/sprites/special-greatsword_default.png) | 모아내려찍기 | 독립 원화의 레이어 조합 |
| [special-greatsword_range](runtime/sprites/special-greatsword_range.png) | 반월참 | 독립 원화의 레이어 조합 |
| [special-greatsword_movement](runtime/sprites/special-greatsword_movement.png) | 회전 전진 | 독립 원화의 레이어 조합 |
| [special-greatsword_follow_up](runtime/sprites/special-greatsword_follow_up.png) | 낙인 대검 | 독립 원화의 레이어 조합 |
| [launcher-ball](runtime/sprites/launcher-ball.png) | 차징 후 발사 | 독립 원화의 레이어 조합 |
| [launcher-laser](runtime/sprites/launcher-laser.png) | 레이저 | 독립 원화의 레이어 조합 |
| [launcher-shotgun](runtime/sprites/launcher-shotgun.png) | 샷건 | 독립 원화의 레이어 조합 |
| [launcher-grenade](runtime/sprites/launcher-grenade.png) | 유탄포 | 독립 원화의 레이어 조합 |
| [special-shuriken_default](runtime/sprites/special-shuriken_default.png) | 관통 수리검 | 독립 원화의 레이어 조합 |
| [special-shuriken_range](runtime/sprites/special-shuriken_range.png) | 폭발 수리검 | 독립 원화의 레이어 조합 |
| [special-shuriken_movement](runtime/sprites/special-shuriken_movement.png) | 뒤로 이동 · 복귀 | 독립 원화의 레이어 조합 |
| [special-shuriken_follow_up](runtime/sprites/special-shuriken_follow_up.png) | 전진 회전 수리검 | 독립 원화의 레이어 조합 |
| [special-bomb_default](runtime/sprites/special-bomb_default.png) | 3연속 투척 | 독립 원화의 레이어 조합 |
| [special-bomb_range](runtime/sprites/special-bomb_range.png) | 대형 폭탄 | 독립 원화의 레이어 조합 |
| [special-bomb_movement](runtime/sprites/special-bomb_movement.png) | 미사일 변신 | 독립 원화의 레이어 조합 |
| [special-bomb_follow_up](runtime/sprites/special-bomb_follow_up.png) | 착지 시한폭탄 | 독립 원화의 레이어 조합 |
| [part-ink3_red_01](runtime/sprites/part-ink3_red_01.png) | 덧칠 | 독립 원화의 레이어 조합 |
| [part-ink3_red_02](runtime/sprites/part-ink3_red_02.png) | 집중 공격 | 독립 원화의 레이어 조합 |
| [part-ink3_red_03](runtime/sprites/part-ink3_red_03.png) | 힘 실어 건네기 | 독립 원화의 레이어 조합 |
| [part-ink3_red_04](runtime/sprites/part-ink3_red_04.png) | 터지는 잉크 | 독립 원화의 레이어 조합 |
| [part-ink3_red_05](runtime/sprites/part-ink3_red_05.png) | 사방으로 튀는 잉크 | 독립 원화의 레이어 조합 |
| [part-ink3_red_06](runtime/sprites/part-ink3_red_06.png) | 기세를 몰아 | 독립 원화의 레이어 조합 |
| [part-ink3_red_07](runtime/sprites/part-ink3_red_07.png) | 붉은 낙관 | 독립 원화의 레이어 조합 |
| [part-ink3_red_08](runtime/sprites/part-ink3_red_08.png) | 결정타 | 독립 원화의 레이어 조합 |
| [part-ink3_red_09](runtime/sprites/part-ink3_red_09.png) | 농묵 | 독립 원화의 레이어 조합 |
| [part-ink3_red_10](runtime/sprites/part-ink3_red_10.png) | 밑줄 긋기 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_01](runtime/sprites/part-ink3_yellow_01.png) | 전기 발자국 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_02](runtime/sprites/part-ink3_yellow_02.png) | 뛰어난 발걸음 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_03](runtime/sprites/part-ink3_yellow_03.png) | 몸통 박치기 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_04](runtime/sprites/part-ink3_yellow_04.png) | 휘몰아치기 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_05](runtime/sprites/part-ink3_yellow_05.png) | 전기 장판 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_06](runtime/sprites/part-ink3_yellow_06.png) | 신난다 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_07](runtime/sprites/part-ink3_yellow_07.png) | 끊어 치기 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_08](runtime/sprites/part-ink3_yellow_08.png) | 번개 표식 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_09](runtime/sprites/part-ink3_yellow_09.png) | 번지는 표식 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_10](runtime/sprites/part-ink3_yellow_10.png) | 질주 충전 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_11](runtime/sprites/part-ink3_yellow_11.png) | 내달린 획 | 독립 원화의 레이어 조합 |
| [part-ink3_yellow_12](runtime/sprites/part-ink3_yellow_12.png) | 옮겨 쓰기 | 독립 원화의 레이어 조합 |
| [part-ink3_green_01](runtime/sprites/part-ink3_green_01.png) | 든든한 배리어 | 독립 원화의 레이어 조합 |
| [part-ink3_green_02](runtime/sprites/part-ink3_green_02.png) | 보호막 교체 | 독립 원화의 레이어 조합 |
| [part-ink3_green_03](runtime/sprites/part-ink3_green_03.png) | 가시갑옷 | 독립 원화의 레이어 조합 |
| [part-ink3_green_04](runtime/sprites/part-ink3_green_04.png) | 전투 식량 | 독립 원화의 레이어 조합 |
| [part-ink3_green_05](runtime/sprites/part-ink3_green_05.png) | 티끌 모아 태산 | 독립 원화의 레이어 조합 |
| [part-ink3_green_06](runtime/sprites/part-ink3_green_06.png) | 풀컨디션 | 독립 원화의 레이어 조합 |
| [part-ink3_green_07](runtime/sprites/part-ink3_green_07.png) | 초록 수액 | 독립 원화의 레이어 조합 |
| [part-ink3_green_08](runtime/sprites/part-ink3_green_08.png) | 단단한 껍질 | 독립 원화의 레이어 조합 |
| [part-ink3_green_09](runtime/sprites/part-ink3_green_09.png) | 되갚는 잉크 | 독립 원화의 레이어 조합 |
| [part-ink3_green_10](runtime/sprites/part-ink3_green_10.png) | 갑옷 깁기 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_01](runtime/sprites/part-ink3_blue_01.png) | 범위 증폭 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_02](runtime/sprites/part-ink3_blue_02.png) | 교대 타격 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_03](runtime/sprites/part-ink3_blue_03.png) | 여의봉 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_04](runtime/sprites/part-ink3_blue_04.png) | 난무 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_05](runtime/sprites/part-ink3_blue_05.png) | 공명 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_06](runtime/sprites/part-ink3_blue_06.png) | 학살 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_07](runtime/sprites/part-ink3_blue_07.png) | 푸른 순환 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_08](runtime/sprites/part-ink3_blue_08.png) | 색 번짐 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_09](runtime/sprites/part-ink3_blue_09.png) | 겹쳐 쓰기 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_10](runtime/sprites/part-ink3_blue_10.png) | 정리 정돈 | 독립 원화의 레이어 조합 |
| [part-ink3_blue_11](runtime/sprites/part-ink3_blue_11.png) | 앞당긴 획 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_01](runtime/sprites/part-ink3_purple_01.png) | 먹물 늪 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_02](runtime/sprites/part-ink3_purple_02.png) | 잔류 폭발 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_03](runtime/sprites/part-ink3_purple_03.png) | 선제 제압 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_04](runtime/sprites/part-ink3_purple_04.png) | 뇌진탕 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_05](runtime/sprites/part-ink3_purple_05.png) | 사약 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_06](runtime/sprites/part-ink3_purple_06.png) | 종합 병원 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_07](runtime/sprites/part-ink3_purple_07.png) | 쪽빛 족쇄 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_08](runtime/sprites/part-ink3_purple_08.png) | 보랏빛 낙인 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_09](runtime/sprites/part-ink3_purple_09.png) | 독폭탄 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_10](runtime/sprites/part-ink3_purple_10.png) | 넘어뜨리기 | 독립 원화의 레이어 조합 |
| [part-ink3_purple_11](runtime/sprites/part-ink3_purple_11.png) | 약점 찌르기 | 독립 원화의 레이어 조합 |
