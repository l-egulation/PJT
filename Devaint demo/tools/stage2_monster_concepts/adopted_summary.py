"""Arrange the user's seven adopted design directions without generating or repainting art."""
from pathlib import Path
import hashlib, html, json, re, shutil, zipfile
from PIL import Image, ImageDraw, ImageFont
import normal_round2 as shared

ROOT = shared.ROOT
SOURCE = Path(__file__).parent
OUT = ROOT / 'outputs/waterway/adopted-monsters-v2-20261031'
PRIOR = ROOT / 'outputs/waterway/topology-reset-20261031'
LATEST = ROOT / 'outputs/waterway/final-slots-candidates-20261031'
N = Image.Resampling.NEAREST


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def dump(p, value):
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def build():
    choices = json.loads((SOURCE / 'current-choices.json').read_text(encoding='utf-8'))
    retained = {m['id']: m for m in choices['retained']}
    signal = json.loads((SOURCE / 'selected-ranged.json').read_text(encoding='utf-8'))
    records = []
    assert choices['adoptedIds'] == ['H3', 'B3', 'M3', 'R-selected', 'K4', 'A3', 'I4']
    OUT.mkdir(parents=True, exist_ok=True)
    for ident in choices['adoptedIds']:
        dst = OUT / 'selected' / ident
        dst.mkdir(parents=True, exist_ok=True)
        m = dict(signal if ident == 'R-selected' else retained[ident])
        if ident in ['H3', 'I4', 'M3', 'K4']:
            k = ident[0]
            source = LATEST if ident in ['M3', 'K4'] else PRIOR
            original = source / k / 'raw/generated.png'
            cut = source / k / 'cutouts' / f'{ident}-enlarged.png'
            scale = source / k / 'cutouts' / f'{ident}-scale-study.png'
            prompt = source / k / 'prompt.txt'
            shutil.copytree(source / k / 'raw', dst / 'generation-record', dirs_exist_ok=True)
            if (source / k / 'references').is_dir():
                shutil.copytree(source / k / 'references', dst / 'generation-inputs', dirs_exist_ok=True)
        else:
            old = PRIOR / 'retained' / ident
            original, cut = old / 'original-board.png', old / 'design-cutout.png'
            scale = old / ('scale-study.png' if ident == 'R-selected' else 'role-scale-study.png')
            prompt = old / 'original-prompt.txt'
        assert sha(original) == m['sourceSha256'], ident
        if 'cutoutSha256' in m:
            assert sha(cut) == m['cutoutSha256'], ident
        for p, name in [(original, 'original-board.png'), (cut, 'design-cutout.png'),
                        (scale, 'earlier-scale-study.png'), (prompt, 'original-prompt.txt')]:
            shutil.copy2(p, dst / name)
        m['copiedOriginalSha256'] = sha(dst / 'original-board.png')
        m['copiedCutoutSha256'] = sha(dst / 'design-cutout.png')
        assert m['copiedCutoutSha256'] == sha(cut)
        m['originalRGBAndAlphaPreserved'] = True
        m['displayNote'] = 'Original design copied unchanged. Per-card enlargement is not final game scale.'
        dump(dst / 'selection.json', m)
        records.append(m)

    role_counts = {'melee': sum(m['role'].startswith(('근거리','큰 근거리')) for m in records), 'ranged': sum(m['role'].startswith('원거리') for m in records), 'elite': sum(m['role'].startswith('엘리트') for m in records), 'bossConcept': sum(m['role'].startswith('보스') for m in records)}
    assert role_counts == {'melee':3,'ranged':2,'elite':1,'bossConcept':1}, role_counts

    refs = OUT / 'references'
    refs.mkdir(exist_ok=True)
    for name in ['assets/approved-player.png', 'C-game.png']:
        target = refs / Path(name).name
        shutil.copy2(PRIOR / 'references' / name, target)
    assert sha(refs / 'approved-player.png') == sha(PRIOR / 'references/assets/approved-player.png')
    dump(OUT / 'choices.json', choices)
    dump(OUT / 'selections.json', records)

    font = lambda size: ImageFont.truetype(shared.FONT, size)
    board = Image.new('RGB', (1600, 1560), '#eeeee8')
    d = ImageDraw.Draw(board)
    d.text((32, 22), '2스테이지 · 현재 채택 디자인', font=font(34), fill='#24323c')
    d.text((34, 72), '일반 근거리 3 · 일반 원거리 2 · 엘리트 1 · 남매 2인조 보스 콘셉트 1', font=font(21), fill='#4f626d')
    slots = [(24, 120, 504, 410), (548, 120, 504, 410), (1072, 120, 504, 410),
             (24, 550, 764, 410), (812, 550, 764, 410),
             (24, 980, 764, 470), (812, 980, 764, 470)]
    for m, (x, y, w, h) in zip(records, slots):
        ident = m['id']
        d.rounded_rectangle((x, y, x+w, y+h), 12, fill='#f9f8f2', outline='#c5ccc9', width=2)
        d.text((x+20, y+16), f'{ident} · {m["role"]}', font=font(23), fill='#5a6d77')
        if ident in ['M3', 'K4']:
            d.text((x+w-115, y+20), '채택', font=font(18), fill='#a45137')
        cut = Image.open(OUT / 'selected' / ident / 'design-cutout.png').convert('RGBA')
        factor = min((w-64)/cut.width, (h-128)/cut.height)
        shown = cut.resize((round(cut.width*factor), round(cut.height*factor)), N)
        board.paste(shown, (x+(w-shown.width)//2, y+64+(h-128-shown.height)//2), shown)
        d.text((x+20, y+h-48), m['name'], font=font(25), fill='#263640')
    d.text((30, 1475), '채택된 기존 그림 모음 · 개체별 확대 배율 다름 · 최종 크기 비교 아님', font=font(20), fill='#526570')
    d.text((30, 1510), 'H3 근접 공격 방식 미정 / I4 현재 연결체는 참고 원형, 독립된 남매 두 인물은 후속 디자인', font=font(20), fill='#526570')
    board.save(OUT / 'adopted-board.png')

    descriptions = {
        'H3': ('발 없는 회백색 수의와 탁한 자주색 안감', '미정·근접 공격으로 재검토'),
        'B3': ('낮은 뼈색 쐐기몸과 구리색 지지다리', '묵직하게 접근하는 압박형·세부 미정'),
        'R-selected': (signal['identity'], signal['attackCharacter']),
        'M3': ('낮게 앉은 몸과 길게 뻗은 주둥이판', '긴 주둥이판으로 가까운 적을 찌르기·세부 미정'),
        'K4': ('세로 등짐통과 짧은 호스를 든 사람형', '두 손으로 호스를 겨누고 먹물 분사'),
        'A3': ('비대칭 거구와 한쪽에 치우친 거대한 팔', '거대한 팔을 쓰는 근접 압박·세부 미정'),
        'I4': ('탁한 장미색·청색 두 인물과 잉크 연결부', '미정·남매 2인조 보스 방향'),
    }
    rows = []
    lines = []
    for m in records:
        ident = m['id']; core, attack = descriptions[ident]
        line = f'{m["name"]} / {m["role"]} / {core} / {attack}'
        lines.append(line)
        rows.append(f'<tr><td>{ident}</td><td>{html.escape(m["name"])}</td><td>{m["role"]}</td><td>{core}</td><td>{attack}</td><td><a href="selected/{ident}/design-cutout.png">채택 그림</a> · <a href="selected/{ident}/original-board.png">원본 보드</a></td></tr>')
    (OUT / 'descriptions.md').write_text('\n\n'.join(lines)+'\n', encoding='utf-8')
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>2스테이지 현재 채택 7건</title><style>body{margin:0;background:#17212a;color:#e7e9e3;font:17px/1.75 "Malgun Gothic",sans-serif}main{max-width:1600px;margin:auto;padding:28px}h1{font-size:30px}a{color:#addbea}.board{display:block;width:100%;height:auto}table{border-collapse:collapse;width:100%;min-width:1100px}th,td{text-align:left;padding:15px 10px;border-bottom:1px solid #52606a;vertical-align:top}.scroll{overflow-x:auto}strong{color:#edc68b}p{max-width:1200px}</style><main><h1>2스테이지 · 지금까지 채택한 디자인 7건</h1><p>근거리 3종 · 원거리 2종 · 엘리트 1종 · 보스 콘셉트 1건입니다. M3 먹침필경사는 근거리·찌르기로 반영했습니다. 보스는 남매 2인조 방향을 채택했으며, 최종 캐릭터 두 명의 디자인은 후속 작업입니다.</p><img class="board" src="adopted-board.png" alt="현재 채택된 일곱 디자인 모음"><div class="scroll"><table><thead><tr><th>ID</th><th>가칭</th><th>현재 역할</th><th>외형 핵심</th><th>공격 성격</th><th>보존 그림</th></tr></thead><tbody>'''+''.join(rows)+'''</tbody></table></div><p><strong>H3 늘어진수의:</strong> 기존 원거리 후보에서 근거리로 채택했습니다. 기존 손끝 먹줄기 발사 설명은 현재 역할에서 제외하고, 근접 공격 방식은 미정으로 둡니다. 그림 자체는 변경하지 않았습니다.</p><p><strong>I4 수로를 지키는 남매(가칭):</strong> 쌍두엮임 그림에서 2인조 보스 방향을 채택했습니다. 현재 이미지는 잉크로 연결된 두 인물이며, 독립된 남매 두 명으로 분리·재설계한 결과가 아닙니다. 관계와 성격, 이름, 무기 분담, 최종 크기와 패턴은 미정입니다. 기존 일반 원거리의 석궁 발사를 보스 시스템으로 확정하지 않습니다.</p><p><strong>M3 먹침필경사:</strong> 사용자 지정에 따라 근거리로 채택했습니다. 긴 주둥이판을 이용한 근접 찌르기 방향이며, 기존 먹침 발사 설명은 현재 역할에서 제외합니다. 공격의 세부 동작과 수치는 미정입니다. 그림은 그대로 유지했습니다.</p><p><strong>일반 몬스터 역할 구성:</strong> H3·B3·M3 근거리 3종, 신호투척수·K4 원거리 2종으로 맞췄습니다. 일반 몬스터 역할 선택은 완료했으며, 최종 크기·공격 세부·제작 적용은 후속 작업입니다. H/I/J의 미채택 10종과 K/L/M의 나머지 10종도 후보로 유지합니다.</p><p>이 자료는 기존 디자인 시안의 채택 정리입니다. 원본을 재생성하거나 재채색하지 않았습니다. 개체별 보기 좋은 크기로 확대했으므로 이 보드는 최종 체급 비교가 아닙니다. 애니메이션·스프라이트 시트·Unity 적용·전투 수치 확정은 포함하지 않습니다.</p><p><a href="adopted-monsters-v2-20261031.zip">원본·선택 정보·프롬프트 ZIP</a> · <a href="choices.json">현재 선택 정보</a> · <a href="descriptions.md">한 줄 설명</a></p></main></html>'''
    (OUT / 'index.html').write_text(page, encoding='utf-8')
    for name in ['adopted_summary.py', 'current-choices.json', 'selected-ranged.json', 'README.md']:
        dst = OUT / 'source' / name; dst.parent.mkdir(exist_ok=True); shutil.copy2(SOURCE / name, dst)
    archive = OUT / (OUT.name + '.zip')
    missing = [link for link in re.findall(r'(?:href|src)="([^"]+)"', page)
               if link != archive.name and not (OUT/link).is_file()]
    assert not missing, missing
    dump(OUT / 'validation.json', {'selectedDesignDirections': 7, 'roles': role_counts, 'originalsAndCutoutsHashMatch': True, 'newArtGenerated': False, 'twoSeparateBossBodiesDesigned': False, 'missingLocalLinks': missing})
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p != archive:
                z.write(p, p.relative_to(OUT))
    print(json.dumps({'selected': choices['adoptedIds'], 'board': str(OUT/'adopted-board.png'), 'zipBytes': archive.stat().st_size}, ensure_ascii=False))


if __name__ == '__main__':
    build()
