"""Extract one returned M3 thrust cycle and reuse its exact pixels for a combo."""
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image

from b3_walk_return import dump, white
from k4_d_pixel_walk import ROOT, N, sha, sample, tidy
from k4_d_pixel_cleanup import fill_holes
from m3_walk_candidates import bounds, repair_needle, board
from walk_review import animate

SPEC = ROOT / 'data/presentation/stage2_m3_stab_reuse.json'


def build():
    s = json.loads(SPEC.read_text(encoding='utf-8'))
    out, base = ROOT / s['output'], ROOT / s['base']
    for folder in ('source', 'all-frames', 'native', 'native-indexed', 'white-native',
                   'enlarged-4x', 'masks', 'references', 'previews'):
        (out / folder).mkdir(parents=True, exist_ok=True)
    src = out / 'source' / Path(s['sourceFile']).name
    if not src.exists():
        shutil.copy2(s['sourceFile'], src)
    assert sha(src) == sha(Path(s['sourceFile']))
    Image.MAX_IMAGE_PIXELS = 100000000
    sheet = Image.open(src)
    w, h = s['cellSize']; cols, rows = s['grid']
    assert sheet.size == (w * cols, h * rows)
    all_bounds, hashes = [], []
    for i in range(cols * rows):
        x, y = i % cols * w, i // cols * h
        cell = sheet.crop((x, y, x + w, y + h)).convert('RGBA')
        p = out / 'all-frames' / f'source-{i+1:03}.png'
        cell.save(p); all_bounds.append(bounds(cell)); hashes.append(sha(p))
    basic = Image.open(base / 'native/base-frame.png').convert('RGBA')
    b = np.array(basic); palette = np.unique(b[b[:, :, 3] > 0, :3], axis=0)
    assert len(palette) == 9 and basic.size == tuple(s['canvas'])
    ref = all_bounds[s['scaleReferenceSourceOneBased'] - 1]
    scale = s['targetReferenceHeight'] / (ref[3] - ref[1])
    anchor = all_bounds[s['anchorReferenceSourceOneBased'] - 1]
    offset = [192 - (anchor[0] + anchor[2]) / 2 * scale,
              s['baseline'] - anchor[3] * scale]
    unique = sorted({i for name in ('windup', 'thrust', 'withdraw', 'recovery')
                     for i in s[name]['sourceOneBased']})
    images, mapping = {}, []
    indexed_palette = np.concatenate([np.zeros((1, 3), np.uint8), palette])
    for i in unique:
        raw = sample(Image.open(out / 'all-frames' / f'source-{i:03}.png').convert('RGBA'),
                     palette, scale, offset, s)
        cleaned = tidy(raw, basic)
        a, holes = fill_holes(np.array(cleaned), s['pinholeCleanup'])
        a, needle = repair_needle(a, s)
        a, more_holes = fill_holes(a, s['pinholeCleanup'])
        f = Image.fromarray(a); images[i] = f
        name = f'source-{i:03}.png'; f.save(out / 'native' / name)
        white(f).save(out / 'white-native' / name)
        f.resize((1536, 896), N).save(out / 'enlarged-4x' / name)
        ix = np.zeros(a.shape[:2], np.uint8)
        for ci, rgb in enumerate(palette, 1):
            ix[np.all(a[:, :, :3] == rgb, axis=2) & (a[:, :, 3] > 0)] = ci
        ind = Image.fromarray(ix)
        ind.putpalette(indexed_palette.ravel().tolist() + [0] * (768 - indexed_palette.size))
        ind.info['transparency'] = 0; ind.save(out / 'native-indexed' / name)
        assert ind.convert('RGBA').tobytes() == f.tobytes()
        changed = np.any(np.array(raw) != a, axis=2)
        mask = np.zeros_like(a); mask[changed] = [220, 30, 140, 255]
        Image.fromarray(mask).save(out / 'masks' / name)
        mapping.append({'sourceOneBased': i, 'sourceSha256': hashes[i-1],
                        'nativePath': f'native/{name}', 'nativeSha256': sha(out / 'native' / name),
                        'bbox': list(f.getbbox()), 'cleanupPixels': int(changed.sum()),
                        'holePixelsFilled': holes + more_holes, 'needleGapPixelsFilled': needle})
    references = {'base-frame.png': base / 'native/base-frame.png',
                  'palette.json': base / 'palette.json', 'prompt-en.txt': ROOT / s['inputPrompt'],
                  'handoff-spec.json': ROOT / s['handoffSpec'],
                  'combat-pattern-reference.json': ROOT / 'data/combat/stage2_waterway_patterns.json',
                  'C-game.png': ROOT / s['mapReference']}
    for name, p in references.items():
        shutil.copy2(p, out / 'references' / name)
    timeline = []

    def append(phase, hit=None):
        part = s[phase]; assert len(part['sourceOneBased']) == len(part['durationsMs'])
        for index, duration in zip(part['sourceOneBased'], part['durationsMs']):
            timeline.append({'phase': phase, 'hit': hit, 'sourceOneBased': index,
                             'path': f'native/source-{index:03}.png', 'durationMs': duration})

    append('windup')
    for hit in range(1, s['hitCountProposal'] + 1):
        append('thrust', hit)
        if hit < s['hitCountProposal']:
            append('withdraw', hit)
    append('recovery')
    elapsed = 0
    for entry in timeline:
        entry['startMs'] = elapsed; elapsed += entry['durationMs']; entry['endMs'] = elapsed
    assert elapsed == 2110
    cycle = [{'phase': phase, 'sourceOneBased': i, 'durationMs': d,
              'path': f'native/source-{i:03}.png'}
             for phase in ('thrust', 'withdraw')
             for i, d in zip(s[phase]['sourceOneBased'], s[phase]['durationsMs'])]
    union = Image.new('RGBA', tuple(s['canvas']))
    for f in images.values():
        union.alpha_composite(f)
    bb = union.getbbox(); box = [bb[0]-10, bb[1]-10, bb[2]+10, bb[3]+10]
    scene = Image.open(out / 'references/C-game.png').convert('RGBA')
    assert scene.size == (960, 540)
    for name, seq in [('cycle', cycle), ('combo', timeline)]:
        frames = [images[r['sourceOneBased']] for r in seq]
        ds = [r['durationMs'] for r in seq]
        if name == 'combo':
            frames.append(frames[-1]); ds.append(s['reviewGapMs'])
        animate(frames, ds, out / 'previews' / f'{name}-native.webp')
        animate(frames, ds, out / 'previews' / f'{name}-native.apng')
        white4 = [white(f.crop(box).resize(((box[2]-box[0])*4, (box[3]-box[1])*4), N)).convert('RGBA') for f in frames]
        animate(white4, ds, out / 'previews' / f'{name}-white-4x.webp')
        if name == 'combo':
            composite = []
            for f in frames:
                c = scene.copy(); c.alpha_composite(f, tuple(s['mapOffset'])); composite.append(c)
            animate(composite, ds, out / 'previews/combo-map.webp')
    keys = [images[i] for i in unique]
    board(keys, [i-1 for i in unique], list(range(len(unique))), box, 3, 3,
          out / 'previews/selected-keys.png')
    cycle_keys = [images[r['sourceOneBased']] for r in cycle]
    board(cycle_keys, [r['sourceOneBased']-1 for r in cycle], list(range(len(cycle))), box,
          3, 3, out / 'previews/cycle-keys.png')
    info = {**s, 'sourceSha256': sha(src), 'sheetSize': list(sheet.size),
            'sourceFrameHashes': hashes, 'sourceBounds': all_bounds,
            'baseSha256': sha(base / 'native/base-frame.png'), 'paletteRgb': palette.tolist(),
            'scale': scale, 'offset': offset, 'mapping': mapping, 'previewCrop': box,
            'uniqueFrameCount': len(unique), 'timelineEntryCount': len(timeline),
            'attackDurationMs': elapsed, 'singleCycleMs': sum(r['durationMs'] for r in cycle),
            'timeline': timeline, 'singleCycle': cycle, 'approval': 'pending-user-review',
            'runtimeIntegration': 'not-performed',
            'limitation': 'Source shape/needle width and foot movement remain; no rigid-needle or fixed-feet guarantee. Final neutral is source2, not a replacement with a different base pose.'}
    dump(out / 'manifest.json', info); dump(out / 'sequence.json', timeline)
    shutil.copy2(SPEC, out / 'pixel-spec.json')
    page(out, info)
    (out / 'README.md').write_text('''# 먹침필경사 찌르기 재사용

받은12800×7200 원본을 source에 보존하고100셀을 all-frames에 무변형 분리한다. 원본1번은 별도 참고 크기이므로 실제 중립2번 높이를59논리px로 맞추는 공통 배율과 위치 하나를 사용한다. 원본17·18·19번 찌르기와21·22·23번 회수를 한 사이클로 골라 같은 PNG를 반복 참조한다. 새 포즈·보간·역재생·프레임별 위치/크기 보정은 없다.

전조550ms 후 같은 찌르기100ms를4회, 회수120ms를3회 재사용하고 최종회복800ms를 붙여 기존 제안2110ms를 유지한다. 마지막 타격 뒤의 회수는 최종회복에 포함한다. 반복 사이600ms는 검토 간격이며 공격 시간에 포함하지 않는다. PNG 원본에는 시간이 없어 타이밍과4연타는 검토 제안이며 사용자 승인 전이다.

실제 기본본의9색·384×224·이진 알파·정수4배를 사용한다. 흰 매트 혼합을 분리하고 재질 투표로 픽셀화한 뒤 작은 잡점·4px 이하 내부 구멍·2px 이하 침 내부 행 틈을 정리한다. 마스크로 수정 픽셀을 보존한다. 원본의 침 두께/모양과 발 위치 변화는 남으며 완전한 강체나 발 고정 보정이라고 표현하지 않는다.

native/indexed/흰 배경/4배PNG·선정키판·한 사이클과4연타 무손실WebP/APNG·원본 대응 및 시간표를 제공한다. 맵은960×540 기존 배경과 플레이어를 바꾸지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. Unity·전투/효과 연결과 다른 채택 에셋은 변경하지 않는다.
''', encoding='utf-8')
    validate(out, info, images, palette)
    package(out)
    print(json.dumps({'output': s['output'], 'unique': len(unique), 'entries': len(timeline),
                      'attackMs': elapsed, 'scale': scale, 'crop': box}))


def page(out, info):
    js = '''const DATA=REPLACE_DATA;let mode='combo',playing=true,rate=1,time=0,prev=null,images={};
const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');
function seq(){return mode==='combo'?DATA.timeline:DATA.singleCycle}
function duration(){return seq().reduce((n,r)=>n+r.durationMs,0)}
function render(key){let r=seq()[key],im=images[r.path],b=DATA.previewCrop,names={windup:'전조',thrust:'찌르기',withdraw:'회수',recovery:'최종 회복'};canvas.width=b[2]-b[0];canvas.height=b[3]-b[1];canvas.style.width=canvas.width*+document.querySelector('#zoom').value+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(im,b[0],b[1],canvas.width,canvas.height,0,0,canvas.width,canvas.height);document.querySelector('#step').max=seq().length-1;document.querySelector('#step').value=key;document.querySelector('#label').textContent=(r.hit?r.hit+'타 · ':'')+names[r.phase]+' · 원본'+r.sourceOneBased+' · '+r.durationMs+'ms';}
function tick(t){if(prev===null)prev=t;if(playing){time+=(t-prev)*rate;let total=duration(),rem=time%(total+(mode==='combo'?DATA.reviewGapMs:0)),key=seq().length-1;for(let i=0;i<seq().length;i++){if(rem<seq()[i].durationMs){key=i;break}rem-=seq()[i].durationMs}render(key)}prev=t;requestAnimationFrame(tick)}
function reset(){time=0;prev=null;render(0)}
Promise.all(DATA.mapping.map(r=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>{images[r.nativePath]=im;resolve()};im.onerror=reject;im.src=r.nativePath}))).then(()=>{reset();requestAnimationFrame(tick)});
document.querySelector('#mode').onchange=e=>{mode=e.target.value;reset()};document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#restart').onclick=reset;document.querySelector('#rate').onchange=e=>rate=+e.target.value;document.querySelector('#zoom').onchange=()=>render(+document.querySelector('#step').value);document.querySelector('#step').oninput=e=>{playing=false;time=seq().slice(0,+e.target.value).reduce((n,r)=>n+r.durationMs,0);document.querySelector('#play').textContent='재생';render(+e.target.value)};document.querySelector('#bg').onchange=e=>document.querySelector('.viewport').style.background=e.target.value;
'''.replace('REPLACE_DATA', json.dumps(info, ensure_ascii=False))
    (out / 'preview.js').write_text(js, encoding='utf-8')
    (out / 'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>먹침필경사 · 찌르기 재사용</title><style>body{margin:0;background:#eee;color:#28252c;font:16px/1.7 system-ui}main{max-width:1150px;padding:28px;margin:auto}section{background:white;padding:20px;margin:20px 0}h1{font-size:28px}h2{font-size:22px}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.viewport{overflow:auto;background:white;padding:12px}button,select{padding:8px;margin:4px}input{width:100%}a{color:#814d69}</style><main><h1>먹침필경사 · 한 번 찌르기를 재사용</h1><p>발을 버티고 긴 침을 빠르게 네 번 찌른 뒤 회복하는 공격입니다. 같은 찌르기·회수 PNG를 반복 연결한 검토안이며 아직 채택 전입니다.</p><p><a href="m3-stab-reuse-20261101.zip" download>PNG·시간표·원본·참조 ZIP</a> · <a href="manifest.json">규격과 원본 대응</a> · <a href="sequence.json">전체 재사용 순서</a></p><section><h2>4연타 / 한 사이클</h2><p>전조550ms → 찌르기100ms×4 + 사이 회수120ms×3 → 최종회복800ms = 2.11초. 반복 사이600ms는 확인용 간격입니다. 같은 사이클은220ms입니다.</p><select id="mode"><option value="combo">준비·4연타·회복</option><option value="cycle">한 번 찌르기·회수 반복</option></select><button id="play">정지</button><button id="restart">처음부터</button><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option><option value="0.25">0.25배속</option></select><select id="zoom"><option value="1">실제1배</option><option value="2">2배</option><option value="3" selected>3배</option><option value="4">4배</option></select><select id="bg"><option value="#fff">흰 배경</option><option value="#25252b">어두운 배경</option></select><div class="viewport"><canvas></canvas></div><p id="label"></p><input id="step" type="range" min="0" value="0"><p><a href="previews/combo-white-4x.webp">4연타 확대 재생</a> · <a href="previews/cycle-white-4x.webp">한 사이클 확대 재생</a> · <a href="previews/combo-native.apng">원배율 APNG</a></p></section><section><h2>재사용하는6장</h2><p>원본17·18·19번을 찌르기, 21·22·23번을 회수로 사용합니다. 4연타는 이 PNG를 그대로 참조하며 마지막 타격 뒤의 회수는 최종회복에 포함합니다.</p><img src="previews/cycle-keys.png" alt="찌르기와 회수6장"></section><section><h2>준비·회복을 포함한 고유11장</h2><img src="previews/selected-keys.png" alt="선정된 고유 프레임"></section><section><h2>기존 맵 합성 · 960×540</h2><img src="previews/combo-map.webp" alt="기존 맵과 플레이어 위 먹침필경사 공격 합성"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 배경과 플레이어는 원본 그대로입니다.</p></section><p>384×224·실제 기본본9색·이진 알파·정수4배. 실제 중립2번 높이를59px에 맞추는 한 배율과 위치를 사용합니다. 원본1번의 다른 크기는 제외합니다. 새 포즈 생성·보간·역재생은 없습니다. 원본의 침 두께와 발 움직임은 남는 검토 대상입니다. 시간·4연타는 기존 제작 제안이며 Unity/전투 수치는 변경하지 않았습니다.</p><script src="preview.js"></script></main></html>''', encoding='utf-8')


def animation_duration(path):
    im = Image.open(path); total = 0
    for i in range(im.n_frames):
        im.seek(i); im.load(); total += im.info['duration']
    return total


def validate(out, info, images, palette):
    allowed = {tuple(rgb) for rgb in palette.tolist()}
    for i, f in images.items():
        a = np.array(f); name = f'source-{i:03}.png'
        assert f.size == (384, 224) and set(np.unique(a[:, :, 3])) <= {0, 255}
        assert {tuple(c) for c in np.unique(a[a[:, :, 3] > 0, :3], axis=0)} <= allowed
        assert Image.open(out / 'native-indexed' / name).convert('RGBA').tobytes() == f.tobytes()
        assert Image.open(out / 'enlarged-4x' / name).tobytes() == f.resize((1536,896), N).tobytes()
        x0,y0,x1,y1 = f.getbbox(); assert 0 < x0 < x1 < 384 and 0 < y0 < y1 < 224
    for hit in range(1,5):
        part = [r for r in info['timeline'] if r['phase']=='thrust' and r['hit']==hit]
        assert [r['sourceOneBased'] for r in part] == [17,18,19]
        assert sum(r['durationMs'] for r in part) == 100
        if hit < 4:
            part = [r for r in info['timeline'] if r['phase']=='withdraw' and r['hit']==hit]
            assert [r['sourceOneBased'] for r in part] == [21,22,23]
            assert sum(r['durationMs'] for r in part) == 120
    assert info['timeline'][0]['path'] == info['timeline'][-1]['path']
    assert animation_duration(out/'previews/cycle-native.webp') == 220
    assert animation_duration(out/'previews/combo-native.webp') == 2710
    assert round(animation_duration(out/'previews/combo-native.apng')) == 2710
    for p in (out/'previews').glob('*'):
        if p.suffix in {'.webp','.apng'}:
            im = Image.open(p)
            for k in range(im.n_frames): im.seek(k); im.load()
    handoff = json.loads((ROOT/info['handoffSpec']).read_text(encoding='utf-8'))
    assert round(sum(p['seconds'] for p in handoff['phaseProposal'])*1000) == info['attackDurationMs']
    dump(out/'validation.json', {'result':'pass', 'sourceCells':100, 'uniquePixelFrames':len(images),
         'paletteColorsAllowed':9, 'canvas':[384,224], 'alpha':'binary', 'indexedRgba':'exact',
         'enlarged4x':'exact-nearest', 'thrustReuse':'same-paths-and-pixels-four-times',
         'attackDurationMs':2110, 'reviewGapMs':600, 'webpAndApngDecoded':True,
         'browserQA':'not-run', 'Unity':'not-run'})


def package(out):
    target = out/'m3-stab-reuse-20261101.zip'
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p != target and p.name not in {'source-overview.png','diagnostic-bounds.json'}:
                z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(target) as z: assert z.testzip() is None


if __name__ == '__main__':
    build()
