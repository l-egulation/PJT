"""Extract and play the user's drawn jet; retain and disclose clipped source edges."""
import base64
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label

from attack_fx_polish import save_frame
from b3_walk_return import dump, white
from h3_orb_return import check_animation
from k4_d_pixel_walk import ROOT, N, sha
from walk_review import animate

SPEC = ROOT / 'data/presentation/stage2_k4_aether_spray_return.json'


def pixel_frame(cell, s, palette):
    a = np.array(cell.convert('RGB'))
    mask = a.min(2) < s['whiteCutoff']
    parts, _ = label(mask)
    counts = np.bincount(parts.ravel()); counts[0] = 0
    mask &= counts[parts] >= s['minimumSourceComponentArea']
    size = tuple(round(v * s['commonScale']) for v in cell.size)
    coverage = np.array(Image.fromarray(mask.astype(np.float32)).resize(size, Image.Resampling.BOX))
    channels = [np.array(Image.fromarray(a[:, :, c].astype(np.float32) * mask).resize(size, Image.Resampling.BOX)) for c in range(3)]
    rgb = np.stack(channels, axis=2) / np.maximum(coverage[:, :, None], 0.00001)
    solid = coverage >= s['nativeCoverageThreshold']
    nearest = ((rgb[:, :, None, :] - palette[None, None, :, :]) ** 2).sum(3).argmin(2)
    result = np.zeros((*solid.shape, 4), np.uint8)
    result[solid, :3] = palette[nearest[solid]]; result[solid, 3] = 255
    frame = Image.new('RGBA', tuple(s['nativeCanvas']))
    frame.alpha_composite(Image.fromarray(result), (0, 0))
    # Report source edge occupancy, not post-cleanup occupancy: it is evidence
    # of missing art even if a thin edge mark disappears at logical resolution.
    raw = a.min(2) < s['whiteCutoff']
    return frame, int(raw[:, 0].sum())


def build():
    s = json.loads(SPEC.read_text(encoding='utf-8'))
    out = ROOT / s['output']; (out / 'references').mkdir(parents=True, exist_ok=True)
    src = Path(s['source']); copied = out / 'references/aether-original.png'
    shutil.copy2(src, copied); assert sha(src) == sha(copied)
    shutil.copy2(SPEC, out / 'references/presentation-spec.json')
    shutil.copy2(ROOT / s['palette'], out / 'references/approved-palette.json')
    colors = json.loads((ROOT / s['palette']).read_text(encoding='utf-8'))['colors']
    palette = np.array([tuple(bytes.fromhex(c[1:])) for c in colors], np.uint8)
    source = Image.open(copied).convert('RGBA'); cols, rows = s['grid']
    assert source.size == (7680, 4320)
    native = []; originals = []; records = []
    for i in range(cols * rows):
        x = i % cols * 1280; y = i // cols * 720
        cell = source.crop((x, y, x + 1280, y + 720))
        crop = out / 'source-cells' / f'source-{i+1:02}.png'; crop.parent.mkdir(exist_ok=True); cell.save(crop)
        assert Image.open(crop).convert('RGBA').tobytes() == cell.tobytes()
        f, edge = pixel_frame(cell, s, palette)
        rec = save_frame(f, out / 'all-native' / 'rgba' / f'attack-{i+1:02}.png', palette)
        # Source comparison uses the same scale/placement, with all source colors.
        original = Image.new('RGBA', tuple(s['nativeCanvas']), 'white')
        resized = cell.resize(tuple(round(v * s['commonScale']) for v in cell.size), Image.Resampling.LANCZOS)
        original.alpha_composite(resized)
        native.append(f); originals.append(original)
        rec.update(sourceOneBased=i+1, sourceBox=[x,y,x+1280,y+720], sourceCrop=str(crop.relative_to(out).as_posix()), sourceLeftEdgePixels=edge, leftClipped=edge > 0, bbox=f.getbbox())
        records.append(rec)
    chosen = s['selectedOneBased']; ds = s['durationsMs']; assert len(ds) == len(chosen) == 30
    selected = [native[i-1] for i in chosen]; raw = [originals[i-1] for i in chosen]
    paths = []; original_paths = []
    for k, (source_no, f, r) in enumerate(zip(chosen, selected, raw), 1):
        p = f'native/rgba/attack-{k:02}.png'; save_frame(f, out / p, palette); paths.append(p)
        rp = f'comparison/source-{k:02}.png'; (out / 'comparison').mkdir(exist_ok=True); r.save(out / rp); original_paths.append(rp)
    play_ds = ds.copy(); play_ds[-1] += s['reviewGapMs']
    whites = [white(f).convert('RGBA') for f in selected]
    animate(whites, play_ds, out / 'review-native.webp'); check_animation(out / 'review-native.webp', whites, play_ds)
    animate([f.resize((800,448), N) for f in whites], play_ds, out / 'review-white-2x.webp')
    animate(raw, play_ds, out / 'source-selected.webp')
    animate(originals, [80]*36, out / 'source-all36.webp')
    sheet = Image.new('RGBA', (400*6, 250*5), 'white'); draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
    for k, (n, f) in enumerate(zip(chosen, whites)):
        x = k % 6 * 400; y = k // 6 * 250
        sheet.alpha_composite(f, (x,y+26))
        draw.text((x+5,y+2), f'{k+1:02} · 원본 {n:02}' + (' · 왼쪽 잘림' if records[n-1]['leftClipped'] else ''), fill='#7c505f', font=font)
    sheet.save(out / 'selected-keys.png')
    inspect = Image.new('RGBA', (800*3,448*2), 'white')
    for k, n in enumerate([4,7,10,18,24,28]):
        inspect.alpha_composite(whites[n].resize((800,448),N), (k%3*800,k//3*448))
    inspect.save(out / 'sequence-review.png')
    clipped = [r['sourceOneBased'] for r in records if r['leftClipped']]
    info = {**s, 'sourceSha256':sha(copied), 'sourceCount':36, 'selectedCount':30, 'motionMs':sum(ds), 'cycleMs':sum(play_ds), 'clippedSourceOneBased':clipped, 'clippingRepaired':False, 'selectedPaths':paths, 'comparisonPaths':original_paths, 'records':records, 'paletteHex':colors, 'sourceBodyReused':True, 'interpolatedFrames':0, 'gameIntegration':'not-performed'}
    dump(out / 'manifest.json', info)
    page(info, out)
    (out / 'README.md').write_text('''# 등짐분사수 · 에테르 반환 연결 검토

사용자가 가져온 6×6 원본36칸을 그대로 보존하고 원본2~31번30장을 순서대로 연결한다. 한 배율0.31/고정400×224 캔버스이며 개별 프레임 확대·위치 재맞춤이나 중간 형태 생성은 없다. 승인된 등짐분사수11색과 이진 알파로 환원하고, 흰 테두리 혼입을 막기 위해 전경 점유로 가중한 색을 사용한다. 본체 높이는 약112 논리 픽셀이다.

중간 원본의 왼쪽 잉크 끝이 잘려 있다. 없는 그림을 복원한 결과가 아니며, 여백을 추가해도 누락된 형태는 생기지 않는다. 프레임별 원본 경계 점유를 manifest에 기록하고 화면과 키판에 표시한다. 원본36장/선정30장/픽셀30장 비교를 제공하며 게임용 확정본이나 실제 게임 캡처가 아니다. H3 및 기존 승인 공격 원본·전투·Unity는 변경하지 않는다.
''', encoding='utf-8')
    with zipfile.ZipFile(out / 'k4-aether-spray-review.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix in {'.png','.json','.md','.py'}: z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out / 'k4-aether-spray-review.zip') as z: assert z.testzip() is None
    dump(out / 'validation.json', {'sourceCopyExact':True,'sourceCellsExact':36,'selectedFrames':30,'nativeWebpPixelTimingExact':True,'paletteColorsAllowed':11,'binaryAlpha':True,'sourceClipping':clipped,'clippingRepaired':False,'browserQA':'not-run','Unity':'not-run'})
    print(json.dumps({'selected':30,'motionMs':sum(ds),'cycleMs':sum(play_ds),'clipped':clipped,'entry':str(out / 'index.html')},ensure_ascii=False))


def page(info, out):
    paths = info['selectedPaths'] + info['comparisonPaths']
    uris = {p:'data:image/png;base64,' + base64.b64encode((out / p).read_bytes()).decode('ascii') for p in paths}
    info = {**info, 'images':uris}
    payload = json.dumps(info,ensure_ascii=False).replace('</','<\\/')
    html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 · 에테르 분사 연결</title>
<style>*{box-sizing:border-box}body{font:16px/1.6 system-ui,'Malgun Gothic',sans-serif;background:#eeeae3;color:#29252f;margin:0}main{max-width:1000px;margin:auto;padding:24px}section{padding:18px;background:white;border:1px solid #d3c9bf;border-radius:8px;margin:20px 0}button,select{font:inherit;padding:7px 12px;border:1px solid #bfb5ad;border-radius:5px;background:white}.controls{display:flex;gap:12px;flex-wrap:wrap;align-items:center}.viewport{overflow:auto;margin-top:16px}canvas,img{display:block;image-rendering:pixelated}input[type=range]{width:100%}.note{color:#80586e}.small{font-size:14px;color:#6f6669}a{color:#80586e}</style>
<main><h1>등짐분사수 · 에테르 분사 연결</h1><p>준비 → 굵게 분출 → 흐름 유지 → 끝물 분리 → 노즐 회수</p><p class="small">모션·합성 검토용 · 실제 게임 캡처 아님</p>
<section><div class="controls"><button id="play">정지</button><button id="restart">처음부터</button><select id="mode"><option value="pixel">기존11색 픽셀본</option><option value="source">선정 원본 비교</option></select><select id="speed"><option value="1">1배</option><option value="0.5">0.5배</option><option value="0.25">0.25배</option></select><select id="bg"><option value="white">흰 배경</option><option value="dark">어두운 배경</option></select></div><p id="loading"></p><div class="viewport"><canvas id="scene" width="400" height="224" style="width:800px;height:448px" aria-label="등짐분사수 분사 모션"></canvas></div><p id="phase"></p><p id="clipping" class="note"></p><input id="scrub" type="range" min="0" max="29" value="0" step="1" aria-label="선정 프레임"><div class="controls"><button id="prev">이전 프레임</button><button id="next">다음 프레임</button></div></section>
<p class="note">중간 프레임은 원본 왼쪽 잉크 끝이 잘려 있습니다. 이번 연결본에는 누락된 끝을 임의로 그려 넣지 않았습니다.</p><p>원본36칸 중2~31번30장을 연결했습니다. 크기와 위치는 모든 프레임에 같은 기준을 적용하고, 픽셀본은 기존 등짐분사수11색만 사용합니다.</p><details><summary>가져온 원본36장 전체 재생</summary><div class="viewport"><img src="source-all36.webp" width="800" alt="원본36장 전체 순서"></div></details><p><a href="selected-keys.png">선정30장 보기</a> · <a href="k4-aether-spray-review.zip" download>프레임·원본 ZIP</a></p></main>
<script>window.SPRAY_RETURN=__DATA__;</script><script>
const D=window.SPRAY_RETURN,$=id=>document.getElementById(id),images=new Map();let time=0,last=null,playing=true,rate=1;
const durations=D.durationsMs.slice();durations[durations.length-1]+=D.reviewGapMs;
function at(t){let end=0;for(let i=0;i<durations.length;i++){end+=durations[i];if(t<end)return i}return durations.length-1}
function draw(){const k=at(time%D.cycleMs),n=D.selectedOneBased[k],c=$('scene'),ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.fillStyle=$('bg').value==='dark'?'#25252b':'white';ctx.fillRect(0,0,c.width,c.height);const p=($('mode').value==='source'?D.comparisonPaths:D.selectedPaths)[k];if(images.has(p))ctx.drawImage(images.get(p),0,0);$('scrub').value=k;$('phase').textContent=`${k+1}/30 · 원본${n}번 · ${n<=4?'준비':n<=24?'분출 · 흐름 유지':n<=27?'끝물 분리':'노즐 회수'} · ${(time%D.cycleMs/1000).toFixed(2)}초`;$('clipping').textContent=D.records[n-1].leftClipped?'이 프레임: 원본의 왼쪽 잉크 끝 잘림':''}
function seek(k){playing=false;$('play').textContent='재생';k=(k+30)%30;time=durations.slice(0,k).reduce((a,b)=>a+b,0);draw()}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'정지':'재생'};$('restart').onclick=()=>{time=0;last=null;draw()};$('mode').onchange=$('bg').onchange=draw;$('speed').onchange=e=>rate=+e.target.value;$('scrub').oninput=e=>seek(+e.target.value);$('prev').onclick=()=>seek(+$('scrub').value-1);$('next').onclick=()=>seek(+$('scrub').value+1);
let count=0;for(const [p,uri] of Object.entries(D.images)){const im=new Image();im.onload=()=>{images.set(p,im);$('loading').textContent=`${++count}/60 이미지 준비`;draw()};im.onerror=()=>{$('loading').textContent='읽기 실패: '+p};im.src=uri}
function tick(now){if(last!==null&&playing)time+=Math.min(now-last,250)*rate;last=now;draw();requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
</script></html>'''
    (out / 'index.html').write_text(html.replace('__DATA__',payload),encoding='utf-8')


if __name__ == '__main__':
    build()
