"""Timing candidates for ArrowRain using the cleaned (mouthless) frames; independent players."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'preview/rain-candidates'
TEMPLATE = GROUP / 'preview/bounce-cleanup'

FRAMES = list(range(1, 36))
SUMMON, DETONATE = 5, 28
CUES = [400, 2450]
CUE_LABELS = ['소환 ↑ (첫 화살 0.5초)', '기폭 ↙ (첫 폭발 2.45초)']
ROLES = {1: 'idle', 2: 'idle', 3: 'coil', 4: 'coil', 5: 'hit', 6: 'follow', 7: 'follow',
         **{f: 'lock' for f in range(8, 25)}, 25: 'coil', 26: 'coil', 27: 'strike', 28: 'hit',
         **{f: 'lock' for f in range(29, 35)}, 35: 'hold'}

def sped_opening(factor):
    """A with 02-07 played `factor` times faster; 01 absorbs the time before 05 (0.40 s) and the
    time saved after 05 is spread over the raised hold 08-24, so 05 and 28 keep their cues."""
    a = [160, 80, 80, 80, 100, 90, 90] + [90] * 17 + [90, 90, 60, 90, 70, 70, 60, 60, 60, 60, 150]
    pre = [round(x / factor) for x in a[1:4]]
    post = [round(x / factor) for x in a[4:7]]
    hold_total = sum(a[7:24]) + (sum(a[4:7]) - sum(post))
    hold = [hold_total // 17 + (1 if i < hold_total % 17 else 0) for i in range(17)]
    return [400 - sum(pre)] + pre + post + hold + a[24:]


def trimmed_sped_opening(drop, factor):
    """A without the `drop` frames (chosen by least added jump) and with the kept 02-07 frames played
    `factor` times faster. 01 absorbs the time before 05 (0.40 s); the raised hold 08-24 absorbs the
    rest so 28 stays at 2.45 s. Returns (sourceFrames, durationsMs)."""
    base = dict(zip(range(1, 8), [160, 80, 80, 80, 100, 90, 90]))
    pre = [f for f in (2, 3, 4) if f not in drop]
    post = [f for f in (5, 6, 7) if f not in drop]
    pre_d = [round(base[f] / factor) for f in pre]
    post_d = [round(base[f] / factor) for f in post]
    hold_total = 2050 - sum(post_d) - (90 + 90 + 60)
    hold = [hold_total // 17 + (1 if i < hold_total % 17 else 0) for i in range(17)]
    frames = [1] + pre + post + list(range(8, 36))
    return frames, [400 - sum(pre_d)] + pre_d + post_d + hold + [90, 90, 60, 90, 70, 70, 60, 60, 60, 60, 150]


DROP_MEASURE = {'02': 1.02, '03': 1.34, '04': 1.20, '05': 1.10, '06': 0.98, '07': 1.23,
                'note': 'jump between the neighbours when the frame is removed / larger existing neighbour step, on the cleaned frames (silhouette + colour change below the crossbow). <=1.02 means removal adds no visible jump.'}


CANDIDATES = [
    {'id': 'A', 'name': '원본 흐름', 'durationsMs': [160, 80, 80, 80,
                                                 100, 90, 90] + [90] * 17 + [90, 90, 60,
                                                 90, 70, 70, 60, 60, 60, 60, 150],
     'note': '01~35 전부. 유지 장을 90ms씩 고르게 이어 Aether가 그린 흐름 그대로. 0.40초 팔 올림, 2.45초 내리찍음.'},
    {'id': 'B', 'name': '힘 모으기 · 강한 스냅', 'durationsMs': [200, 60, 50, 90,
                                                        160, 70, 70] + [84] * 16 + [86] + [120, 160, 40,
                                                        150, 60, 60, 60, 60, 60, 60, 150],
     'note': 'A와 같은 35장. 감기(04)와 주먹 쥔 장(25·26)에 힘을 모으고, 내리찍는 27은 40ms로 빠르게. 두 정점(05·28)을 150~160ms 세움.'},
    # User: "앞부분덜고배속까지 해봐 그리고 덜 때 자연스럽게 덜어봐 최대한". 02 and 06 are the only opening frames whose removal adds no jump.
    {'id': 'A-trim-x1.5', 'name': 'A · 02·06 덜기 + 1.5배속', 'sourceFrames': trimmed_sped_opening({2, 6}, 1.5)[0], 'durationsMs': trimmed_sped_opening({2, 6}, 1.5)[1],
     'note': '빼도 튀지 않는 두 장(02 기본 둘째 장, 06 소매 흔들림)만 덜고 남은 03·04·05·07을 1.5배 빠르게. 03~07이 0.23초 안에 지나감. 08부터는 A와 같은 흐름.'},
    {'id': 'A-trim-x2', 'name': 'A · 02·06 덜기 + 2배속', 'sourceFrames': trimmed_sped_opening({2, 6}, 2)[0], 'durationsMs': trimmed_sped_opening({2, 6}, 2)[1],
     'note': '같은 두 장을 덜고 2배 빠르게. 기본 자세 320ms 뒤 감기→팔 올림→정리가 0.18초 안에 끝나 가장 순간적으로 치켜 올라감.'},
    # User: "흠 자연스러운건 A긴한데 뭔가 확이 없는데 배속은 어때?" Same 35 frames as A, opening sped up.
    {'id': 'A-x1.5', 'name': 'A · 앞부분 1.5배속', 'durationsMs': sped_opening(1.5),
     'note': 'A와 같은 35장 그대로. 감기(02~04)와 팔 올림 뒤 소매 정리(05~07)만 1.5배 빠르게. 기본 자세가 그만큼 길게 버티고, 아낀 시간은 위로 올린 유지 장에 고르게 나눔.'},
    {'id': 'A-x2', 'name': 'A · 앞부분 2배속', 'durationsMs': sped_opening(2),
     'note': 'A와 같은 35장 그대로. 02~07을 2배 빠르게(40~50ms). 가장 순간적으로 치켜 올라감. 기본 자세 280ms 뒤 0.12초 만에 팔이 올라감.'},
    # User: "A가 맞는거 같고 처음에 ... 확! 이런게 없어서 아쉽네 프레임 좀만 줄여봐 앞부분". Only the opening changes; 08 onward equals A.
    {'id': 'A2', 'name': 'A · 앞부분 덜기', 'sourceFrames': [1, 3, 4, 5] + list(range(7, 36)),
     'durationsMs': [240, 80, 80,
                     180, 100] + [90] * 17 + [90, 90, 60,
                     90, 70, 70, 60, 60, 60, 60, 150],
     'note': 'A에서 02(기본 둘째 장)와 06(소매 정리 첫 장)을 뺌. 기본 자세가 조금 길게 버티다 감기 두 장 뒤 튕겨 오르고, 올린 05에서 180ms 멈춤. 08부터는 A와 같음.'},
    {'id': 'A3', 'name': 'A · 앞부분 크게 덜기', 'sourceFrames': [1, 4, 5] + list(range(7, 36)),
     'durationsMs': [320, 80,
                     180, 100] + [90] * 17 + [90, 90, 60,
                     90, 70, 70, 60, 60, 60, 60, 150],
     'note': 'A2에서 03까지 뺌. 기본 자세 → 감기 한 장(04) → 바로 튕겨 올림. 가장 갑작스럽고, 감기가 80ms만 보임. 08부터는 A와 같음.'},
]

ORDER = ['A-x2', 'A', 'A-trim-x1.5', 'A-trim-x2', 'A-x1.5', 'A2', 'A3', 'B']
CANDIDATES.sort(key=lambda c: ORDER.index(c['id']))


def main():
    OUT.mkdir(exist_ok=True)
    clean = json.loads((OUT / 'clean-all.json').read_text(encoding='utf-8'))
    for c in CANDIDATES:
        d, frames = c['durationsMs'], c.get('sourceFrames', FRAMES)
        assert len(d) == len(frames) and frames == sorted(frames), (c['id'], len(d), len(frames))
        starts = [sum(d[:i]) for i in range(len(d))]
        assert [starts[frames.index(SUMMON)], starts[frames.index(DETONATE)]] == CUES, (c['id'], starts[frames.index(SUMMON)], starts[frames.index(DETONATE)])
        c.update(sourceFrames=frames, roles=[ROLES[f] for f in frames], frameCount=len(frames), totalMs=sum(d),
                 releaseCuesMs=CUES, repeat=False, holdLastFrame=True, clean=True,
                 frameUrls=[f'clean/{f:02}.png' for f in frames])
    data = {
        'format': 'stage2-rain-timing-candidates-v1', 'status': 'user-selected-A-x2', 'selected': 'A-x2', 'runtime': '../../runtime/rain-a-x2/motion.json', 'openingDropMeasure': DROP_MEASURE,
        'pattern': 'ArrowRain', 'source': '../../source/rain-aether-01/aether-original.png', 'cleanFrames': 'clean-all.json',
        'frameBase': '../../source/rain-aether-01/frames/', 'logicalCanvas': [384, 256],
        'commonTransform': {'destinationSize': clean['transform']['destinationSize'], 'rasterOffset': clean['transform']['rasterOffset']},
        'previewMatte': {'whiteMinimum': 250, 'maximumChannelDifference': 4},
        'cueFrames': [SUMMON, DETONATE], 'releaseCuesMs': CUES, 'cueLabels': CUE_LABELS, 'visualDirections': ['up', 'down-left'],
        'gameTimeline': {'tell': 0, 'firstArrowSpawn': 0.5, 'landings': [0.75, 2.25], 'explosions': [2.45, 3.95]},
        'candidates': CANDIDATES,
    }
    (OUT / 'candidates.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    js = (TEMPLATE / 'candidates.js').read_text(encoding='utf-8')
    old = re.search(r"function shotText\(c\)\{.*?\}\n", js, re.S)
    assert old
    js = js.replace(old.group(0), "function shotText(c){const t=states[c.id].time,n=c.releaseCuesMs.filter(x=>t>=x).length;return n?D.cueLabels[n-1]:'준비';}\n")
    js = js.replace("text($('#status'),`원본 ${ids.length}장과 비교 이미지 3장 로딩 완료. 정리본은 투명 PNG를 그대로 재생합니다.`);",
                    "text($('#status'),`정리본 ${ids.length}장과 비교 이미지 3장 로딩 완료. 입을 지운 가면과 승인 석궁이 적용된 투명 PNG입니다.`);")
    assert 'D.cueLabels' in js and '입을 지운' in js
    (OUT / 'candidates.js').write_text(js, encoding='utf-8')

    tpl = (TEMPLATE / 'index.html').read_text(encoding='utf-8')
    style = tpl[tpl.index('<style>'):tpl.index('</style>') + 8]
    controls = tpl[tpl.index('<div class="controls"><button id="play">'):tpl.index('<div id="choices"')].replace('max="2950"', 'max="3110"')
    sections = tpl[tpl.index('<section><h2>승인 기본과 비교'):tpl.index('<section><h2>외곽 확대 확인')].replace('5연사 A · 같은 축척', '화살비 · 같은 축척')
    html = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>화살비 타이밍 비교</title><link rel="stylesheet" href="../review.css">{style}
<main><p class="eyebrow">STAGE 02 / PHASE 03 / ARROW RAIN / TIMING REVIEW</p><h1>순차 폭발 화살비 · 타이밍 비교</h1><p class="notice">사용자 확정: <strong>A-x2 (앞부분 2배속)</strong>. 게임용 결과는 <a href="../../runtime/rain-a-x2/sheet.png" target="_blank">runtime/rain-a-x2</a>. Unity 적용 전입니다.</p>
<p>Aether 원본 3840×2160 · 6×6. 00(입력 장)을 뺀 01~35를 씁니다. 05에서 팔이 위로 올라가 0.40초에 멈추고, 28에서 주먹을 왼쪽 아래로 내리찍어 2.45초 첫 폭발과 맞춥니다. 끝은 내리찍은 자세 그대로이며 기본 자세로 돌아가지 않습니다.</p>
<p class="notice">배경제거·외곽 정리·승인 석궁·발 정리를 마친 투명 PNG로 재생합니다. 생성된 입은 지웠습니다. 12번 가면에서 입 줄 하나만 위아래 가면색으로 메운 뒤, 같은 가면을 모든 장의 후드 자리에 붙여 깜빡이지 않게 했습니다. 손이 가면 앞을 지나는 04번은 손을 살렸습니다. 고른 뒤 확정본으로 내보냅니다.</p>
<nav><a href="../rain-sim/">실제 화살비와 함께 보기</a><a href="../rain-input/">입력·프롬프트</a><a href="../../source/rain-aether-01/aether-original.png" target="_blank">원본 시트</a><a href="../../source/rain-aether-01/inspection-contact.png" target="_blank">36장 한눈에</a><a href="all-clean-black.png" target="_blank">정리본 35장 · 검정</a><a href="candidates.json" target="_blank">프레임·시간표</a></nav>
{controls}<p class="legend">막대 색:<i class="idle"></i>기본<i class="coil"></i>감기·주먹<i class="strike"></i>내리는 중<i class="hit"></i>정점<i class="follow"></i>소매 정리<i class="lock"></i>유지<i class="hold"></i>끝. 주황 세로선은 0.40초 소환과 2.45초 기폭입니다.</p>
<div id="choices" class="choices"></div>
{sections}<p id="status" role="status">프레임 로딩 중…</p></main><script type="module" src="candidates.js"></script></html>'''
    if '.lock{' not in html:
        html = html.replace('</style>', '.lock{background:#b8925a}.legend i{display:inline-block;width:12px;height:10px;margin:0 4px 0 10px;vertical-align:middle}.idle{background:#666}.coil{background:#8d78b5}.strike{background:#c98fd8}.hit{background:#f0b36c}.follow{background:#74aab9}.hold{background:#7c9f77}</style>', 1)
    (OUT / 'index.html').write_text(html, encoding='utf-8')
    for c in CANDIDATES:
        print(c['id'], c['totalMs'])


if __name__ == '__main__':
    main()
