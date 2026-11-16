# 확정 조합 미리보기(2026-11-12): 시간 배분 B(끊어치기) + 초승달 이펙트 「나」(가운데 두껍게 + 더 가파르게). 게임 속도.
import json, runpy
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent; ns = runpy.run_path(str(HERE / 'unarmed_fx_v3.py')); OUT = ns['OUT']; tr = ns['tr']['나']; view = ns['view']; vw, vh = ns['vw'], ns['vh']
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20)
def save(path, mul):
    runs = []
    for t, (im, key) in enumerate(tr):
        if runs and runs[-1][0] == key: runs[-1][2] += 1
        else: runs.append([key, t, 1])
    frs = []
    for _, t, _ in runs:
        p = Image.new('RGB', (2 * vw + 10, vh + 36), 'white'); p.paste(view(tr[t][0], (236, 228, 212, 255)), (0, 34)); p.paste(view(tr[t][0], (70, 78, 86, 255)), (vw + 10, 34))
        ImageDraw.Draw(p).text((6, 4), '맨손 기본 공격 확정안: 끊어치기 + 초승달', font=F, fill=(0, 0, 0)); frs.append(p)
    frs[0].save(path, save_all=True, append_images=frs[1:], duration=[max(20, c * 10 * mul) for _, _, c in runs], loop=0, disposal=1)
save(OUT / '맨손-확정안-게임속도.gif', 1); save(OUT / '맨손-확정안-0.5배.gif', 2)
(OUT.parent / '확정안.json').write_text(json.dumps({'decidedBy': '사용자 2026-11-12', 'timing': 'B 끊어치기', 'sequence': {k: [{'cell': c, 'seconds': s} for c, s in v] for k, v in ns['B'].items()},
    'frames': '4_정리본/<타>_<순서>_칸<번호>.png (640x360, 발밑 (220,327), 1유닛 106.67px)', 'effect': {'variant': '나', 'frames': {'1타': '5_이펙트/나_jab/00~04.png', '2타': '5_이펙트/나_jab_big/00~04.png'},
    'secondsPerFrame': .03, 'startsAt': .12, 'pixelsPerUnit': 32, 'size': [ns['W'], ns['H']], 'originTopLeftPx': [ns['OX'], ns['OY']], 'fistTipCellPx': {k: [v[0] + 2, round(v[1], 1)] for k, v in ns['TIP'].items()}},
    'gameNumbersUnchanged': 'data/combat/unarmed_basic.json: 1타 0.36초·타격 0.12, 2타 0.32초·타격 0.12'}, ensure_ascii=False, indent=1), encoding='utf-8')
print('ok')
