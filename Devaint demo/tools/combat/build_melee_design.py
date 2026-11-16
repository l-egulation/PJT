"""Validate canonical combat JSON and render the melee specification."""
import json
import math
from pathlib import Path
from melee_source import load_melee

REPO = Path(__file__).resolve().parents[2]
ROOT = REPO / 'data/combat'
data = load_melee(ROOT)
forms = data['forms']
sword = next(f for f in forms if f['id'] == 'base_sword')

COMMON = """# 검·둔기 전투 설계 — 현행 Unity 전달 명세

검·둔기 7형태·28특수의 현행 설계다. [공통 전투 명세](../../../combat/rules.md)와 함께 사용한다.

이 문서는 [전투 데이터](../../../../../data/combat/melee_weapon_design.json)와 그 `hit_sources`가 참조하는 행동 원본에서 생성한다. 검 1타의 편집 원본은 [base_sword_basic_0.json](../../../../../data/combat/actions/base_sword_basic_0.json)이다. 수치는 JSON을 편집하고 `python tools/combat/build_melee_design.py`로 재생성한다. 생성기 안의 공통 설명·표 형식은 설명 변경 시에만 수정한다. [데이터 파이프라인](../../../../development/data/data-pipeline.md)을 따른다.

Combat V2 테스트 씬에 연결되어 있으며 전체 게임 통합·최종 밸런스는 후속 범위다. 보스의 기절 시간 부여와 패턴 지속은 새 기획이며 기존 보스 런타임 이행·검증은 미완료다.

## 범위와 설계 의도

검: 검·채찍·레이피어·한손 단검. 둔기: 망치·방패·대검. 첨부 초안에서 이름과 동작이 있는 7형태, 기본 공격 7세트, 형태별 기본 특수+3잉크 변환 **28기술**을 다룬다. 창은 제외하고, 이름이 비어 있는 둔기 네 번째 칸을 임의로 채우지 않는다. 원거리/미정 네 번째 무기 종류/개별 전용 잉크 효과는 이번 범위 밖이다.

검은 거리·속박·재배치·은신으로, 둔기는 느린 타격·밀기·방어·전방 광역 판정으로 구분한다. 긴 사거리/안전성에는 기본 DPS를 낮게 주고, 짧은 사거리 또는 긴 준비 동작에는 피해 보상을 준다. 검 잉크 기술의 강한 수치는 사용자 결정값이므로 비교를 위해 유지한다. 균형이 검증됐다는 뜻은 아니다.

채찍 후속타는 매듭 조이기, 레이피어 이동·후속타는360도 탐색 후 적 정면 순간이동, 대검 기본 공격은 가로베기·올려베기·내려베기 3타다. 방패는 보호막과 피해 감소를 함께 사용한다.

## 공통 구현 계약

- Unity unit/초 기준, 플레이어 scale1, 몸 size=(0.9375,2), 오른쪽 몸 중심x=0.1875/앞끝x=0.65625. 왼쪽은 x대칭. 무기 변경으로 몸/피격 박스는 변하지 않는다.
- 판정은 회전 호 또는 그림 외곽 대신 표에 정한 Box로 시작한다. 전방 근접 기본·특수의 박스는 기존 앞쪽 끝을 유지하면서 몸 전체와 몸 뒤 경계에서 0.25unit(8px)까지 포함한다. 오른쪽 기준 뒤쪽 끝은 root x=-0.53125이며 왼쪽은 대칭이다. 대검은 기본공격만 몸 앞끝(root x=±0.65625)부터 기존 전방 끝까지로 제한하며 몸 뒤 경계를 포함하지 않는다. 대검 특수 4종은 기존 범위를 유지한다. 모아내려찍기는 몸 겹침·뒤쪽 여유를 포함하고, 반월참과 회전 전진은 시전자 좌우를, 낙인 대검은 설치 중심 좌우를 타격한다. 그 외 `both`는 root 좌우 대칭이다. 활성 그림과 실제 판정을 Gizmo로 함께 표시한다. 투사체·설치 효과는 각 효과의 별도 형상을 사용한다.
- JSON 좌표 기준: `actor`는 현재 발/root 프레임, `saved_primary_world_pose`는 본타 시점의 발/root 프레임을 저장한 값이며 shape offset을 한 번만 적용한다. 완성된 월드 박스를 저장하는 구현도 가능하나 오프셋을 두 번 더하지 않는다. `projectile`과 `saved_spawn_world_pose`는 효과 중심 프레임으로 박스 offset=(0,0)이다. 발사체 생성offset과 판정offset을 혼동하지 않는다. `marked_target`의 `all_marked` 후속타는 본타에 적중해 저장한 모든 대상ID에 각자 적용한다.
- 모든 근접 특수의 직접 타격은 판정 범위 안 모든 대상에게 전체 피해를 주며 타격번호별 대상당1회 적용한다. 이동 기준 적은 몸 중심 거리순/동률 stable ID순으로 고르되 피해 대상을 제한하지 않는다. 지면·벽은 차폐, 일방향 발판은 공격 차폐하지 않는다. 이동 스윕에서도 벽 앞/뒤 사건 순서를 지킨다.
- 기본 공격은 타격마다 새 입력. 현재 타격 중 후속 입력1개 예약, 종료 후0.30초까지 다음 타격 가능. 마지막 타격 중 입력1개는 종료 후새1타. 공속M은 타격별 준비/활성/회복을1/M로 줄이고 연계 여유0.30초는 유지한다. 임의 공속 상한 없음. 프레임보다 짧은 활성도 시간 구간 교차/공간 스윕으로 누락하지 않는다.
- 기본 공격 방향은 모션 중 좌우 입력을 따라 그림·몸·판정이 함께 전환된다. 지상 이동형은 방향 입력이 있을 때 각 타격 기준 앞뒤0.04초, 총0.08초 동안0.65unit 전진하며 공속으로 시간창만 축소한다. 망치·대검은 지상 제자리, 공중 기본 공격은 모두 이동11.25·정상 중력이다. 유효 점프/기본 대시로 즉시 취소 가능. 일반 적은 기본 피격0.30초 경직/공격취소·0.25unit/0.10초 밀림(별도 밀기와 합산하지 않음), 보스는 기본 피격 경직0·기본 밀림 없음. 특수는 표에 명시된 제어만 적용한다. 일반 피격 밀림(손맛 시험 강베기 포함)은 사용 가능한 근접 무기들의 가장 짧은 기본 사거리 안에 대상 피격박스가0.25unit 남도록 제한한다. 원거리 슬롯의 비행거리는 계산에서 제외하며 원거리 타격·전용 밀기는 각 설정을 사용한다. 경계의 재피격은 기존 일반 밀림도 새 거리로 덮어쓰고 피해·경직을 유지한다.
- 특수는 기본 공속 영향을 받지 않는다. 별도 명시 없으면 모션 동안 수평 조작0, 준비 시작 방향고정, 일반 수직 운동 유지. 특수 중 점프 입력 무시. 유효 기본 대시로 즉시 취소 가능. 교체는 현재 특수 모션 종료 후 실행하며 기본 대시 취소는 교체 예약을 삭제한다.
- 피격은 공격 동작을 취소하지 않고 이동 조작만0.12초 잠근다. 강제 이동 중에는 밀림 생략, 다른 동작 중에는 기존 밀림 적용. 사망/낙사/리셋이 우선한다. 기술 무적도 낙사 면역을 주지 않는다.
- 피해=타격 기준값×(1+해당 피해 증가 합계). 기본/특수 증가율 분리. 내부소수 유지, 표시만 반올림. 스냅샷은 실제 효과 생성 때 저장하며 예약 독립 효과도 이 스냅샷을 사용한다. 새로 생긴 효과만 새 장비 수치를 받는다.
- 쿨타임=기본값×(1-min(감소합계,0.8)). 실제 효과 발동부터 시작, 준비 중 취소는 미소비. 빗나간 베기/벽에 소멸한 발사체는 이미 발동했으므로 소비. 무기별 독립, 비활성 무기도 진행한다.
- 무적/차단으로 무효화된 타격에는 피해·상태이상·피격 경직을 주지 않는다. 살아 있는 대상에만 후속 상태 적용. 속박은 이동만 금지, 기절은 공격취소/이동·새 공격 금지. 동일 상태는 남은 시간과 새 시간 중 긴 값. 보스도 속박/기절 시간을 부여받지만 보스 기절은 패턴을 멈추거나 취소하지 않으며 남은 시간은 잉크 조건 확인으로 소비하지 않는다(공통 전투 명세). 밀기/끌기/도발은 면역.
- 속박은 자발 이동을 막지만 기술의 강제 밀기/끌기를 막지는 않는다. 기절 중에도 중력/충돌/강제 이동은 처리한다. 이미 발사된 적 투사체는 기절로 삭제되지 않는다. 강제 위치이동은 고체를 넘지 않고 벽 추가 피해는 없다.
- 모든 스킬은 전방 대상이 없어도 입력하면 시작한다. 대상이 없으면 그 자리에서 헛스윙하거나 자기 효과를 발동하며 실제 발동부터 쿨타임을 쓴다. 순간이동은 이동할 적이 없으면 제자리에 남고, 판정 중 범위에 들어온 모든 적은 맞는다. 채찍 접근의 이동 기준은 처음 적중한 최근접 적 하나이며, 매듭 조임은 본타에 맞은 모든 적을 개별 기억해 각 생존 대상에게 예약 피해를 준다. 일반 적을 미는 망치 돌진도 본타에 맞은 각 적을 밀고 종료 피해를 각자 한 번 준다. 대상 없음은 발동 실패 사유가 아니지만 지상 전용·자원·쿨타임·설치 고체 겹침 조건은 유지한다.
- 지상 전용 기술은 입력과 실제 발동 모두 유효 바닥 접지를 요구한다. 공중 입력은 무시/자원 미소비, 준비 중 접지를 잃으면0.10초 실패 회복. 다른 기술은 공중 가능. 이동 특수의 높이 고정은 실제 이동 구간에만 적용하고 종료 시0부터 낙하한다. 준비 중 수직 운동은 유지한다.
- 순간이동은 몸 전체 스윕으로 고체 관통을 막고 실제 도착 위치의 공격 판정을 사용한다. 레이피어는360도 몸 중심 거리로 최근접 적을 탐색한 뒤 적이 바라보는 쪽 정면·몸 간격0.25에 배치하고 적을 마주본다. 탐색 반경과 도착 좌표 한도를 혼동하지 않는다. 채찍 매듭 접근은 첫 적중 뒤3초 안 새D 입력으로 시전자 쪽 표면까지 속도15/최대5unit 접근한다. 속박0.6초는 연장하지 않는다. 적과는 몸 충돌하지 않는다.
- 독립 투사체/예약 잔상/표식/장판/허수아비는 생성 후 일반 취소·교체에도 유지한다. 직접 다단/시전자 부착 공격 판정은 취소 즉시 남은 활성/타격 제거. 사망/런 종료/테스트 리셋은 양쪽 모두 제거한다. 효과는 시전자/원본무기/공격ID/타격ID를 갖는다.
- 동일 시전자·동일 종류 허수아비/연막은1개, 재시전 시 이전 것 교체. 보호/은신은 동종 중복 강화하지 않고 명시된 갱신만 한다. 독립 피해 효과는 별개 시전끼리 중첩 가능하되 각 수명 종료에 제거. 런타임 풀 한도에 도달했다고 피해를 조용히 생략하지 말고 진단 로그를 남긴다.
- 보호막·허수아비의 HP 및 제어 지속시간은 특수 피해 증가 잉크로 늘지 않는다. 피해 판정과 반사 피해에만 특수 피해 증가를 적용한다. 허수아비는 플레이어와 별도 피격 대상이며 같은 적 타격은 허수아비당1회, 무적/회피/피격경직 없이 HP를 감소시킨다.
- 채찍 접근/조임의 첫 타격을 회피·무적으로 막은 대상에는 표식/속박도 생성되지 않는다. 채찍 접근은 첫 적중에 성공해야 이후 이동을 실행한다. 망치 몰아치기는 이동 구간의 모든 유효 적중 대상을 기억한다. 이동 종료 추가41피해는 각 생존 대상에게 같은 공격의 별도 타격ID로 한 번 적용하며 취소·시전자 사망 시에는 생성하지 않는다.
- 연막으로 얻는 은신은 해당 영역이 제공하는 조건이며 공격 후 그 연막에서 재은신할 수 없다. 기본 은신과 겹치면 하나라도 유효한 공급원이 있으면 은신, 공격 시작은 모든 현재 은신 공급원을 해제한다. 은신 시 이미 추적 중인 적도 새 위치 추적을 중단하되 현재 확정된 공격은 취소하지 않는다.
- 도발 대상이 여럿이면 도달 가능한 가장 가까운 대상을 선택(동률 stable ID), 없어지면 일반 AI 재탐색. 이 효과가 적 공격 자체를 취소하지는 않는다. 보스 도발 면역.

### 충전·정비·취소의 예외

단검 이동 특수만3충전, 새 입력당1개를 실제 돌진 시작에 소비하고4초씩 순차 회복한다. 첫 회복 시작 뒤 추가 사용은 타이머 초기화 없음. 차지 공격(레이피어/대검의 버튼 유지)과 충전 횟수는 다른 개념이다. 차지 중 특수 버튼 유지 외 입력은 기존 취소 규칙을 따른다. 레이피어는 최대 유지에 자동 발동한다. 대검은 최대 피해에 도달한 뒤에도 입력을 유지할 수 있고 입력을 떼면 내려베기를 시작한다. 발동 전 대시 취소는 무료이며 이미 쏜 공격은 유지한다.

무기별 현재 특수 자원을 정비로 초기화하지 않는다. 1회형↔3충전형 전환은 잔여 회복 부담을 정규화한다. 최대충전C, 부족충전m, 진행 중 한 충전의 남은 비율r이면 부담q=(m-1+r)/C, 준비 완료는q=0. 새 C'에 대해m'=ceil(q×C'), 새 진행 비율r'=q×C'-(m'-1), 새 보유=C'-m'. 새 회복시간×r'부터 순차 회복한다. 예: 1회형 절반 회복→3회형은1개 보유/다음 충전0.5주기 남음. 동일 C=1이면 기존 '남은 비율 유지'와 정확히 같다. 부동소수 경계는0/정수에 epsilon 보정한다.

한 무기를 버리고 다시 줍는 경우에도 동일 아이템의 타이머/충전 상태를 보존한다. 회수 가능한 바닥 무기의 타이머는 게임 시간으로 계속 진행한다. 단순 정비/슬롯 이동은 쿨타임 세탁 수단이 되지 않는다. 전용 잉크 목록은 비워 두며 해당 내용을 이 명세가 추가하지 않는다.

## 기본 공격 비교

아래 기본 DPS는 모든 타격을 최단 간격으로 적중시키는 단일 대상 기준이다. 사거리·경직·이동·빗나감이 포함된 실전 성능 수치가 아니다.
"""


def shape_text(s):
    if s is None:
        return "표식 대상 직접 적용(공간 재검사 없음)"
    return f"size={s['size']}, 오른쪽offset={s['right_offset']}, 왼쪽offset={s['left_offset']}"


def basic_ground_text(b):
    return "타격별0.65unit/0.08초" if b['input_move_speed'] > 0 else "제자리"


def render():
    out=[COMMON, "| 형태 | 기본 피해 | 타격별 모션 | 타격 내 발동 | 최단 주기 | 이론 DPS | 지상 전진 |", "|---|---|---|---|---:|---:|---|"]
    for f in forms:
        b=f['basic']
        out.append(f"| {f['name']} | {b['damages']} | {b['motion_durations']} | {b['local_hit_times']} | {b['fastest_cycle']:.2f} | {sum(b['damages'])/b['fastest_cycle']:.2f} | {basic_ground_text(b)} |")
    trials={t['form']: t for t in json.loads((ROOT/'playtest_environment.json').read_text(encoding='utf-8-sig'))['basicSpeedTrials']}
    out += ["", "### 본 게임 공속 적용 기준", "",
            "기본 공격의 형태·타별 발동·모션·활성창·전진은 유지하고 피해를 배분한다. 대검은 기본공격만 뒤쪽 피해 판정을 제거하고 원래 전방 끝을 유지한다. 대검 특수 4종은 패치 이전의 범위·피해·타격 시간을 그대로 사용한다. 다른 무기의 범위는 유지한다. 위 표는 공속1인 원본이며 본 게임과 시험 환경은 다음 공속을 적용한다. 공속·추가 후딜의 편집 원본은 `data/combat/playtest_environment.json`이다. 추가 잉크·증강·F1 임시 변경은 제외한다. 검의 적용 DPS를100%로 두며 짧은 사거리·긴 준비에 피해 보상을 주되 광역·이동·은신 보상도 함께 비교한다.", "",
            "| 형태 | 적용 공속 | 첫 타 발동(초) | 콤보 주기(초) | 단일 대상 DPS |", "|---|---:|---:|---:|---:|"]
    for f in forms:
        b=f['basic']; trial=trials.get(f['id'],{}); speed=trial.get('scale',1)
        cycle=sum(b['motion_durations'])/speed+trial.get('lastHitExtraRecoverySeconds',0)
        out.append(f"| {f['name']} | ×{speed:g} | {b['local_hit_times'][0]/speed:.6f} | {cycle:.6f} | {sum(b['damages'])/cycle:.2f} |")
    out += ["", "특수의 타격 시각은 **기술 모션 시작을0초로 한 절대 시각**이다. 활성 구간은 시작 이상/끝 미만. `motion_end`는 캐릭터 조작 복귀 시점이며 독립 효과 수명과 다르다. 회복 구간 안에 직접 활성창이 포함될 수 있으므로 표의 타격 활성 시간을 모션에 다시 더하지 않는다.", ""]
    out += ["## 형태별 특수 목록", "", "| 형태 | 미장착 기본 특수 | 범위 잉크 | 이동 잉크 | 후속타 잉크 |", "|---|---|---|---|---|"]
    for f in forms:
        out.append("| "+f['name']+" | "+" | ".join(s['name']+f" ({s['cooldown']}초)" for s in f['specials'])+" |")
    out.append("")
    slots={"default":"기본 특수(미장착)","range":"범위 잉크","movement":"이동 잉크","follow_up":"후속타 잉크"}
    for f in forms:
        out += [f"## {f['name']} — {f['role']}", "", "### 기본 공격", ""]
        b=f['basic']; elapsed=0
        for i,(damage,duration,impact,active,shape) in enumerate(zip(b['damages'],b['motion_durations'],b['local_hit_times'],b['active_durations'],b['shapes']),1):
            out.append(f"- {i}타: 피해{damage}, 모션{duration:.3f}초, 모션 내{impact:.3f}초 발동/활성{active:.2f}초. 최단 연계의 절대 발동{elapsed+impact:.3f}초. {shape_text(shape)}.")
            elapsed+=duration
        out += ["",f"조작: 지상 {basic_ground_text(b)}(이동형은 방향 입력이 있을 때만), 모션 중 방향 입력 추종, 공중 이동11.25·정상 중력. 새 입력 연계/예약1개/종료후0.30초, 유효 점프·대시 취소. 일반 적 경직0.30초, 보스0. JSON 기본 공격의 input_move_speed는 지상 전진형 여부를 구분하는 기존 연결 필드이며 지속 이동속도로 해석하지 않는다. 기본 특수 및 잉크 변환은 아래와 같다.", ""]
        for s in f['specials']:
            out += [f"### {slots[s['slot']]} — {s['name']}", ""]
            if 'action_tracks' in s:
                out += ["- 실행 시간·형상의 편집 원본은 이 행동의 `action_tracks`다. 피해·돌진 speed/wall_recovery·검기 speed/distance/spawn·수호막 shield_hp/damage_reduction은 기존 수치 필드가 활성 원본이며 같은 `ActionTrackDefinition` 전체 후보로 검증한다. 아래의 옛 발동/종료·타격 시간/형상·돌진 distance·효과 lifetime/duration은 읽기 전용 이행 출처다. `FrameTimeline` C# 컴파일러만 마커를 해석하며 Python은 시간 그래프를 해석하지 않는다.",
                        "- 활성 트랙 원본: `" + json.dumps(s['action_tracks'], ensure_ascii=False) + "`."]
            out += [f"- 기본 쿨타임 **{s['cooldown']}초**, 최초 효과 발동 **{s['activation_time']:.2f}초**, 캐릭터 모션 종료 **{s['motion_end']:.2f}초**. 차지형은 아래 유지시간 규칙에 따라 늦어진다.",
                    f"- 수평 입력속도 {s['input_move_speed']}, 방향 `{s['facing']}`, 수직 `{s['vertical']}`, 무적 구간 {s['invulnerability'] or '없음'}."]
            for i,h in enumerate(s['hits'],1):
                out.append(f"- 타격{i}: {h['time']:.2f}초/피해{h['damage']}/활성{h['active']:.2f}초/대상`{h['targets']}`/기준`{h['anchor']}`. {shape_text(h['shape'])}. 제어: {json.dumps(h['control'],ensure_ascii=False) if h['control'] else '없음'}.")
            if not s['hits']:
                out.append("- 직접 공격 판정 없음. 보호·반사·은신·설치 효과는 아래 규칙으로 처리.")
            out += [f"- 동작: {s['rules']}"]
            if s['movement']:
                out.append("- 이동 데이터: `"+json.dumps(s['movement'],ensure_ascii=False)+"`.")
            if s['independent_effect']:
                out.append("- 효과 데이터: `"+json.dumps(s['independent_effect'],ensure_ascii=False)+"`.")
            if s['id'] == 'shield_follow_up':
                retaliation = json.loads((ROOT / 'shield_decoy_retaliation.json').read_text(encoding='utf-8'))['retaliation']
                out.append(f"- 반격 설계 원본은 `data/combat/shield_decoy_retaliation.json`이다. 피해로 파괴되거나 유지시간이 끝나면 반경{retaliation['radiusUnits']}unit의 적에게 고정{retaliation['fixedDamage']}피해를 한 번 준다. 미피격 만료도 폭발하며 초과 피해·추가 피해 증폭으로 증가하지 않는다. 재설치 제거에는 폭발이 없다. 도발의 보스 면역은 유지하며 충격파 피해는 보스도 대상이다.")
                out.append("- 구현 범위: 반격은 브라우저 시안의 계산·연출에 연결되어 있다. Unity는 ShieldDecoyRuntime으로 피해 파괴·만료 시 반경 안 적의 피격체와 거리를 검사해 한 번 폭발하며 교체·리셋에는 폭발하지 않는다. 브라우저 표적 중심점의 원형 범위 검사는 Unity 충돌·차폐·AI 검증을 대체하지 않는다.")
            out.append("")
    out += ["## Unity 검증·조정 기준", "",
            "이 절은 실행해야 할 검증 계획이다. 실제 Unity 실행 통과 결과가 아니다.", "",
            "| 검사 | 방법 | 기대 결과 |", "|---|---|---|",
            "| 검 참조·현재 원본 | 기존5개 JSON의 정책·자원과 활성 원본 비교 | 정책·자원 유지, 현재 피해·시간·박스는 활성 원본과 일치 |",
            "| 기본 연계 | 각 형태 빠른 연계/0.29초 지연/0.31초 지연 | 표 타격 시각 일치,0.29는 다음 타격/0.31은 새1타 |",
            "| 피해·중복 | 각 기술에 HP1000 적1명/5명/복수 콜라이더 | 이벤트별 표 피해, 같은 타격 중복0, 다단은 타격 수대로 |",
            "| 범위 전체 타격 | 모든 근접 기술 앞에 적3명과 범위 밖/벽 뒤 적 배치 | 각 범위 내 노출 적 전체 피해, 타격당1회; 이동 기준만 최근접 |",
            "| 박스·좌우 | 모든 기본/특수의 Gizmo와 발 좌표 비교 | size/offset 정확, 오른쪽과 왼쪽 x대칭 |",
            "| 고속·고공속 | 30/60/144fps, 물리0.02/0.01초, 공속1/2/5배 | 짧은 활성/스윕 타격 누락·중복 없음, 실제 공속 상한 추가 없음 |",
            "| 지형 | 얇은 벽·코너·일방향발판·좁은 통로 | 고체 너머 피해/순간이동 없음, 발판 공격차폐 없음 |",
            "| 공중 | 상승/정점/낙하에 각 기술 | normal은 수직 궤적 유지, freeze는 해당 구간만0, 지상전용은 미소비 거부 |",
            "| 취소 | 효과 발동 직전/직후 대시·자원부족 대시·점프 | 발동전 무료/발동후 소비, 불가 입력에 동작 취소 없음 |",
            "| 독립 효과 | 잔상/표식/여진/대검 설치 후 대시·교체 | 저장 위치/대상/스탯 유지, 직접 다단만 취소 |",
            "| 보스 | 기본타·속박·기절·밀기·도발 | 기본경직0, 속박/기절동일, 밀기/끌기/도발면역 |",
            "| 패링 |0.19초/0.20초·전후방·동시 다단·탄·레이저·최대쿨감 | 창 안 전방 첫1회만 성공, 근접26기준, 반사1회, 성공0.60초 재무장 하한·0.10초 회복, 실패/후방 정상피해 |",
            "| 보호 | 피해100, 보호막30/감소40% | 감소 후60→보호막30소모→HP30감소, 낙사는 보호 우회 |",
            "| 충전·정비 | 단검3회소모/회복 중 추가사용/1↔3회 전환 |4초 순차 회복, 타이머 리셋 없음, 정규화 부담 보존 |",
            "| 리셋 | 모든 효과 유지 중 전투 리셋 | 효과/예약/제어/중력 잠금 제거, 선택 장비 유지, 보상 복제 없음 |", "",
            "### 최소 검증 수치 예제", "",
            "- 기본 공격 피해·주기·DPS는 위 기본 공격 비교와 본 게임 공속 적용 기준 표를 따른다. 다른 고정 예제 수치를 중복 작성하지 않는다.",
            "- 채찍 조임25+40=65, 레이피어 연침5×22=110, 단검 난무6×21=126, 망치 여진41+3×15=86, 대검 회전8×24=192. 특수 피해+20% 하나면 각 합계78/132/151.2/103.2/230.4.",
            "- 단검 충전형을0.08/0.46/0.84초에 실제 발동하면 최초 회복4.08초, 다음8.08/12.08초. 추가 사용은 첫 회복시각을 밀지 않는다.",
            "- 최소/최대 차지: 레이피어51/90 및 전체0.53/1.13초, 대검70/120 및 최소 시전 전체0.88초. 준비 중 대시 취소는 미소비.", "",
            "### 밸런스 검증 우선순위", "",
            "1. 보스에게 기절/속박이 그대로 들어가므로 두 무기의 제어 연계와80%쿨감으로 장시간 행동 봉쇄가 가능한지 우선 기록한다. 현재 합의에 없는 보스 면역/점감은 몰래 추가하지 않는다. 심하면 지속시간/쿨타임 조정안을 별도 제시한다.",
            "2. 검 검기·돌진은 피해50/쿨타임6초, 잔상은35+35다. 다른 형태가 이보다 약해 보이더라도 이동 안전성/충전/다수 범위/제어를 포함해 30초 단일·다수 표적 기록으로 비교한다.",
            "3. 패링은 적 투사체 피해를 반사하므로 고위력 탄에서 성능이 급증할 수 있다. 근접 반사와 투사체 반사를 별도 로그로 기록하고 원본 피해/배율 중복 적용을 검사한다.",
            "4. 수치 조정은 첫 테스트 후 기록한다. 본 명세의 선택값을 최종 출시 밸런스로 표현하지 않는다. 실제 에셋 동작에 맞춘 판정 오차, 최종 적 AI·챕터 공간·원거리 연계 밸런스는 후속 범위다.", "",
            "## 산출물과 남은 범위", "",
            "- `melee_weapon_design.json`: 7형태 기본 공격과28특수의 구조화 설계값. `export_unity_catalog.py`를 통해 Combat V2 테스트 카탈로그에 연결한다. 전체 게임 이행과 구분한다.",
            "- `build_melee_design.py`: 문서/JSON 생성 및 타임라인·기하·개수 검증. 게임 동작 테스트를 대체하지 않는다.",
            "- 검 1타 수치는 actions/base_sword_basic_0.json만 편집하고 2타는 melee_weapon_design.json의 기존 배열을 편집한다. basic_sword_attack.json은 1타 경로·공통 규칙·2타 초기 회귀 참고값이다. 강베기(base_sword_default)의 활성 수치 원본은 melee_weapon_design.json이며 basic_sword_special.json은 numeric_source가 가리키는 최초 승인 회귀 출처다. 그 초기 피해/시간/박스는 편집값을 덮어쓰지 않는다. 다른 검 특수 참고 JSON의 정책·자원 및 비이행 비교 검사는 유지한다.",
            "- 원거리 상세는 `ranged.md`, 미정 종류/둔기 형태/창/전용 잉크 콘텐츠/보상 경제/전체 게임 이행은 별도 범위다.", ""]
    out[1:1] = ["", "[무기 형태 개요](README.md)"]
    return "\n".join(out)


def validate():
    assert len(forms)==7
    assert sum(len(f['specials']) for f in forms)==28
    assert len({s['id'] for f in forms for s in f['specials']})==28
    for f in forms:
        b=f['basic']
        assert len(b['damages'])==len(b['motion_durations'])==len(b['local_hit_times'])==len(b['shapes'])
        assert {s['slot'] for s in f['specials']}=={'default','range','movement','follow_up'}
        for impact,duration,active in zip(b['local_hit_times'],b['motion_durations'],b['active_durations']):
            assert 0 <= impact < impact+active <= duration+1e-8
        for shape in b['shapes']:
            assert all(v>0 for v in shape['size'])
        for s in f['specials']:
            assert s['cooldown']>0 and 0<=s['activation_time']<s['motion_end']
            for h in s['hits']:
                allowed_targets = {'all_marked'} if h['anchor']=='marked_target' else {'all', 'screen'}
                assert h['targets'] in allowed_targets, (s['id'], h['anchor'], h['targets'])
                assert h['time']>=s['activation_time']-1e-8 and h['damage']>=0 and h['active']>0
                if h['anchor']=='actor':
                    assert h['time']+h['active']<=s['motion_end']+1e-8, s['id']
                if h['shape']:
                    assert all(x>0 for x in h['shape']['size'])
            m=s['movement']
            if 'action_tracks' in s:
                # Scalar transport checks only. The C# compiler alone resolves
                # track timing, intervals, geometry and complete policy graphs.
                assert all(math.isfinite(h['damage']) and h['damage']>=0 for h in s['hits'])
                if s['id']=='base_sword_movement':
                    assert math.isfinite(m['speed']) and m['speed']>0
                    assert math.isfinite(m['wall_recovery']) and m['wall_recovery']>=0
                if s['id']=='base_sword_range':
                    effect=s['independent_effect']
                    assert math.isfinite(effect['speed']) and effect['speed']>0
                    assert math.isfinite(effect['distance']) and effect['distance']>0
                    assert len(effect['right_spawn'])==len(effect['left_spawn'])==2
                    assert all(math.isfinite(value) for value in effect['right_spawn']+effect['left_spawn'])
                    assert effect['left_spawn']==[-effect['right_spawn'][0],effect['right_spawn'][1]]
                if s['id']=='shield_range':
                    effect=s['independent_effect']
                    assert math.isfinite(effect['shield_hp']) and effect['shield_hp']>=0
                    assert math.isfinite(effect['damage_reduction']) and 0<=effect['damage_reduction']<=1
            if m and 'speed' in m and m.get('type','dash')=='dash' and 'action_tracks' not in s:
                assert abs(m['speed']*m['duration']-m['distance'])<1e-6
            if m and m.get('type')=='target_dash_recast':
                assert math.isfinite(m['speed']) and m['speed']>0 and m['max_distance']>=0 and m['recast_window']>0
    # First-hit source identity and approved special references; basic numerics remain editable SSOT values.
    read=lambda name: json.loads((ROOT/name).read_text(encoding='utf-8-sig'))
    basic=read('basic_sword_attack.json')
    assert sword['basic']['hit_sources'][0]==basic['first_hit_source']
    for s,filename in zip(sword['specials'],['basic_sword_special.json','basic_sword_range_special.json','basic_sword_movement_special.json','basic_sword_followup_special.json']):
        old=read(filename)
        if 'action_tracks' not in s:
            assert s['cooldown']==old['base_cooldown_seconds']
        if s['id']=='base_sword_default':
            # Explicit one-source cutover: preserve the approved policy/resource
            # reference, never reinterpret its initial numerics as active edits.
            assert old['numeric_source']=='melee_weapon_design.json#base_sword_default'
            assert old['numeric_values_role']=='initial_approved_regression_provenance'
            assert old['weapon']=='basic_sword' and old['special_ink'] is None
            assert old['target_count']=='all_valid_targets' and old['hits_per_target_per_attack']==1
            assert old['horizontal_input_during_motion']=='disabled' and old['automatic_forward_movement'] is False
            assert old['vertical_physics']=='unchanged' and old['facing_lock']=='motion_start'
            assert old['additional_control_effect']=='none' and old['applies_basic_hit_stagger'] is False
            assert s['facing']=='windup_start' and s['vertical']=='normal' and s['input_move_speed']==0
            assert len(s['hits'])==1 and s['hits'][0]['anchor']=='actor' and s['hits'][0]['targets']=='all' and not s['hits'][0]['control']
            continue
        assert s['activation_time']==old['windup_seconds']
        assert s['motion_end']==old['total_motion_seconds']
        # Active finite damage belongs to this canonical action, not the initial
        # regression JSON. Old timing/geometry above/below remain provenance.
        if 'action_tracks' not in s:
            if 'base_damage' in old:
                assert s['hits'][0]['damage']==old['base_damage']
            else:
                assert [h['damage'] for h in s['hits']]==[old['primary_base_damage'],old['echo_base_damage']]
        shape=s['hits'][0]['shape']
        if 'hitbox' in old:
            assert shape['size']==old['hitbox']['size']
            assert shape['right_offset']==old['hitbox']['right_offset']
        if 'hitbox_size' in old:
            assert shape['size']==old['hitbox_size']
    print('PASS: 7 forms, 28 specials, timing/geometry/movement consistency, base sword source mapping and special references checked')


if __name__=='__main__':
    validate()
    destination = REPO / 'docs/design/equipment/weapons/forms/melee.md'
    destination.write_text(render(), encoding='utf-8')
    print('Rendered docs/design/equipment/weapons/forms/melee.md from data/combat/melee_weapon_design.json')
