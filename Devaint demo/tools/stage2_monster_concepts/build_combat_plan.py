"""Publish the stage-two proposal. Does not export to Unity or change art."""
from pathlib import Path
from collections import Counter
from html import escape
import json

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/stage2_waterway_patterns.json'
DOC = ROOT / 'docs/design/enemies/chapter2/normal/stage2-waterway-monsters.md'
OUT = ROOT / 'outputs/waterway/user-sized-redraw-20261031'



def approved_render(data):
    stats = json.loads((ROOT / 'data/combat/stage2_monster_stats.json').read_text(encoding='utf-8'))['monsters']
    lines = ['# 2스테이지 지하수로 몬스터 전투', '', '[일반 몬스터 개요](README.md)', '',
             '> `python tools/stage2_monster_concepts/build_combat_plan.py`로 생성한다. 일반5종 편집 원본은 `data/combat/stage2_*.json`이다.', '',
             '일반5종은 확정 수치·판정·기존 프레임 시간표를 Chapter2 Unity 전투에 연결한다. 기존 이미지와 FX를 사용하며 새 스프라이트를 만들지 않는다.', '',
             '| 몬스터 | HP | 이동 u/s | 공격 시작 수평거리 u | 회복 뒤 대기 초 |', '|---|---:|---:|---:|---:|']
    for monster in data['monsters']:
        if monster['id'] not in stats:
            continue
        v = stats[monster['id']]
        lines.append(f"| {monster['name']} | {v['maxHP']:g} | {v['moveSpeed']:g} | {v['attackStartRange']:g} | {v['cooldownSeconds']:g} |")
    lines += ['', '## 일반5종 기준', '',
              '- [피격·공격 박스](stage2-monster-geometry.md)',
              '- [피해·연타·피격 반응과 공격 취소 예외](stage2-monster-damage-reaction.md)',
              '- [공격 프레임과 시간](stage2-monster-attack-timing.md)',
              '- [체력·이동·거리·대기](stage2-monster-stats.md)',
              '- [조준·투사체·장판](stage2-projectiles.md)',
              '- [첫 공격·화면·층간 접근](stage2-monster-engagement.md)',
              '- [Unity 연결과 검사 방법](../../../../development/testing/stage2-unity-validation.md)',
              '- [남은 결정](stage2-monster-pending.md)', '',
              '몸 접촉 피해는 없다. 본체 전조·회복과 이미 발사된 탄·장판의 시계는 분리한다. 분사는 최대3회, 찌르기는3연타, 돌진은 최초접촉1회 후 실제 정지 기준 내려찍기로 이어진다.', '',
              '일반5종은 배치된 바닥 구역에서만 이동한다. 원거리 탄은 발사 직전 조준한 바닥만 충돌 대상으로 고정하고 벽·천장·다른 바닥을 통과한다. 세부 예외는 해당 편집 원본과 위 명세를 따른다.', '',
              '## 엘리트와 보스', '',
              '일반5종 확정을 별도 콘텐츠의 미정 수치까지 확대하지 않는다. 현재 기준은 [A3 엘리트](../elite/chapter2-elite-three-patterns.md)와 [Rose/Blue 보스](../rose-blue/chapter2-rose-blue-boss.md)를 따른다.']
    return '\n'.join(lines) + '\n'


def render(data):
    if data.get('runtimeIntegration'):
        return approved_render(data)
    lines = ['# 2스테이지 지하수로 몬스터 공격·전조 기획', '',
             '> 이 문서는 `python tools/stage2_monster_concepts/build_combat_plan.py`로 생성한다. 편집 원본은 [stage2_waterway_patterns.json](../../../../../data/combat/stage2_waterway_patterns.json)이다.', '',
             '사용자가 정한 역할·핵심 공격을 기준으로 보정한 설계안이다. 일반·엘리트의 핵심 공격과 역할 교환은 사용자 지정이며, 아래 세부 보정과 시간은 제안이다. **보스는 석궁 전방·원거리 잉크술 후방의 교대 방향이며 세부 밸런스는 미정이다.**', '',
             '시간은 첫 플레이테스트 후보값이다. 대기 루프와 세 방향은 별도 시안으로 제작했으며 AI·공격 판정·공격 모션·Unity 연결·전투 검증은 수행하지 않았다. 기존 아트 원본과 사용자 크기 JSON은 보존한다. 이 문서 작성으로 전투 설계 체크리스트나 구현 완료율을 올리지 않는다.', '',
             '관련 기준: [몬스터 공통](../../chapter1/monsters.md) · [전투 규칙](../../../combat/rules.md) · [아트 기준](../../../presentation/art-and-presentation.md).', '',
             '## 구성과 공통 보정', '']
    lines.extend('- ' + s for s in data['common'])

    def pattern(p):
        lines.extend(['', '### ' + p['name'], '', p['correction'], '',
                      '| 구간 | 후보 길이 | 자세·전조·공격 |', '|---|---:|---|'])
        for step in p['phases']:
            lines.append(f'| {step["phase"]} | {step["seconds"]:g}초 | {step["action"]} |')
        if p.get('repeat'):
            r = p['repeat']
            lines.extend(['', f'타격 구간은 총 {r["hits"]}회, 타격 사이 회수·전조는 {r["gapCount"]}회다. 표의 구간 길이를 반복하며 한 번의 긴 상시 판정으로 합치지 않는다.'])
        for key, label in [('projectile', '투사체'), ('hazard', '잔류 장판 · 시험안'), ('counter', '플레이어 대응'), ('art', '아트·모션 보정')]:
            if key in p:
                lines.extend(['', '**' + label + '**: ' + p[key]])

    for m in data['monsters']:
        lines.extend(['', f'## {m["id"]} {m["name"]} / {m["role"]}', '', m['basis']])
        for p in m['patterns']:
            pattern(p)
    boss = data['boss']
    lines.extend(['', f'## {boss["id"]} {boss["name"]} / 보스 방향과 세부 제안', '',
                  '**' + boss['status'] + '**', '', boss['recommendation'], '', boss['reason'], '',
                  '**체력과 피격**: ' + boss['health'], '', '### 교대와 동시 공격 제한', ''])
    lines.extend('- ' + s for s in boss['coordination'])
    for p in boss['patterns']:
        pattern(p)
    lines.extend(['', '**후속 연계 후보**: ' + boss['laterCandidate'], '', '**아트 범위**: ' + boss['art'], '',
                  '## 남은 수치와 결정', ''])
    lines.extend('- ' + s for s in data['unresolved'])
    lines.extend(['', '## 후속 플레이테스트 관찰 기준', '', '아래는 검증 계획이며 통과 결과가 아니다.', ''])
    lines.extend('- ' + s for s in data['verificationPlan'])
    return '\n'.join(lines) + '\n'


def page(data):
    def pcard(p):
        rows = ''.join(f'<tr><td>{escape(s["phase"])}</td><td>{s["seconds"]:g}초</td><td>{escape(s["action"])}</td></tr>' for s in p['phases'])
        details = ''.join(f'<p><strong>{label}</strong> {escape(p[k])}</p>' for k, label in [('projectile', '투사체'), ('hazard', '잔류 장판 · 시험안'), ('counter', '대응'), ('art', '아트 보정')] if k in p)
        repeat = f'<p>타격 {p["repeat"]["hits"]}회, 사이 회수·전조 {p["repeat"]["gapCount"]}회. 표의 타격과 회수 구간을 반복합니다.</p>' if p.get('repeat') else ''
        return f'<article><h3>{escape(p["name"])}</h3><p>{escape(p["correction"])}</p><table><tr><th>구간</th><th>후보 길이</th><th>전조·행동</th></tr>{rows}</table>{repeat}{details}</article>'
    sections = ''
    for m in data['monsters']:
        sections += f'<section id="{m["id"]}"><h2>{m["id"]} {escape(m["name"])} · {escape(m["role"])}</h2><img class="monster" src="{m["id"]}/cutouts/style-scale-study.png"><p>{escape(m["basis"])}</p>' + ''.join(pcard(p) for p in m['patterns']) + '</section>'
    b = data['boss']
    sections += '<section id="I4"><h2>남매 보스 · 방향 채택 / 세부 밸런스 미정</h2><img class="monster" src="../pixel-base-step1-20261031/I4-blue/native/base-cropped.png"><img class="monster" src="../pixel-base-step1-20261031/I4-rose/native/base-cropped.png">' + ''.join(f'<p>{escape(b[k])}</p>' for k in ['recommendation', 'reason', 'health'])
    sections += '<ul>' + ''.join('<li>' + escape(s) + '</li>' for s in b['coordination']) + '</ul>'
    sections += ''.join(pcard(p) for p in b['patterns']) + f'<p><strong>후속 후보</strong> {escape(b["laterCandidate"])}</p><p>{escape(b["art"])}</p></section>'
    common = ''.join('<li>' + escape(s) + '</li>' for s in data['common'])
    checks = ''.join('<li>' + escape(s) + '</li>' for s in data['verificationPlan'])
    unresolved = ''.join('<li>' + escape(s) + '</li>' for s in data['unresolved'])
    return '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>지하수로 몬스터 공격·전조 기획</title><style>body{margin:0;background:#17212a;color:#e8e9e3;font:17px/1.8 "Malgun Gothic",sans-serif}main{max-width:1120px;margin:auto;padding:28px}a{color:#b4d9e7}h1{font-size:29px}h2{font-size:24px;color:#eac087}h3{font-size:20px}section{clear:both;border-top:1px solid #56616a;padding:24px 0}.monster{float:right;max-width:40%;image-rendering:pixelated;margin:10px 10px 20px 28px}article{clear:both;padding:10px 0}table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #56616a;text-align:left;padding:12px;vertical-align:top}td:nth-child(1),td:nth-child(2){white-space:nowrap}.note{border-left:4px solid #eac087;padding-left:18px}li{margin-bottom:12px}@media(max-width:650px){main{padding:16px}table{font-size:14px}td,th{padding:6px}.monster{max-width:45%}}</style><main><h1>지하수로 몬스터 · 공격과 전조</h1><p><a href="index.html">크기·배경 비교</a> · <a href="size-editor.html">크기 조절기</a> · <a href="combat-patterns.json">기획 원본 JSON</a> · <a href="../pixel-base-step1-20261031/index.html">정지 픽셀 기본본8명</a></p><p class="note">사용자 지정: 근거리 등짐분사수·쐐기보행기·먹침필경사 / 원거리 늘어진수의·신호투척수 / 엘리트 편측거구.<br>세부 보정·시간은 첫 플레이테스트 제안이며 보스는 석궁 전방·잉크술 후방의 교대 방향이며 세부 밸런스는 미정입니다. 전투 구현이나 검증 완료를 뜻하지 않습니다. 그림은 정지 참고본이며 몸통 호흡 대기는 별도 시안으로 제작했습니다. 공격 모션과 전투 구현은 후속입니다.</p><h2>공통 보정</h2><ul>''' + common + '</ul>' + sections + '<h2>남은 결정</h2><ul>' + unresolved + '</ul><h2>후속 검증 계획 · 미실행</h2><ul>' + checks + '</ul></main></html>'


def build():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    assert data['schema'] == 'stage2-waterway-pattern-proposal-v1'
    by_id = {m['id']: m for m in data['monsters']}
    assert set(by_id) == {'H3', 'B3', 'M3', 'K4', 'R-selected', 'A3'}
    assert Counter(m['role'].split('·')[0] for m in by_id.values()) == {'근거리': 2, '큰 근거리': 1, '원거리': 2, '엘리트': 1}
    assert data['boss']['status'] == '석궁 전방·원거리 잉크술 후방의 교대 방향 / 세부 밸런스 미정'
    for m in data['monsters'] + [data['boss']]:
        for p in m['patterns']:
            assert p['phases'] and all(type(s['seconds']) in [int, float] and s['seconds'] > 0 for s in p['phases'])
    DOC.write_text(render(data), encoding='utf-8')
    if data.get('runtimeIntegration'):
        print('Approved stage2 overview generated from current rules; legacy art proposal preview skipped')
        return
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'combat-plan.html').write_text(page(data), encoding='utf-8')
    (OUT / 'combat-patterns.json').write_bytes(SOURCE.read_bytes())
    print(json.dumps({'designDoc': str(DOC), 'reviewPage': str(OUT / 'combat-plan.html'), 'normalRoles': '3 melee / 2 ranged', 'boss': data['boss']['status']}, ensure_ascii=False))


if __name__ == '__main__':
    build()
