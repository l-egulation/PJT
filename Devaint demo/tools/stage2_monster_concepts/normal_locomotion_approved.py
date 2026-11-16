"""Package the five user-approved normal locomotion loops without changing pixels."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / 'data/presentation/stage2_normal_locomotion_approved.json'
N = Image.Resampling.NEAREST


def dump(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def frame_at(durations, elapsed):
    cursor = elapsed % sum(durations)
    for i, duration in enumerate(durations):
        if cursor < duration:
            return i
        cursor -= duration
    raise AssertionError('Invalid duration table')


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    out = ROOT / spec['output']
    (out / 'references').mkdir(parents=True, exist_ok=True)
    (out / 'previews').mkdir(exist_ok=True)
    refs = ROOT / spec['references']
    for src, name in [(refs/'C-game.png', 'C-game.png'), (refs/'assets/approved-player.png', 'approved-player.png')]:
        shutil.copy2(src, out/'references'/name)
    player = Image.open(out/'references/approved-player.png').convert('RGBA')
    scene = Image.open(out/'references/C-game.png').convert('RGBA')
    assert scene.size == (960, 540)
    data, all_frames = [], []
    for item in spec['monsters']:
        src = ROOT / item['source']
        manifest = json.loads((src/'manifest.json').read_text(encoding='utf-8'))
        paths = sorted((src/'native').glob('*.png'))
        ds = manifest['durationsMs']
        assert len(paths) == len(ds) == item['frameCount']
        assert all(d > 0 for d in ds) and sum(ds) == item['cycleMs']
        folder = out/item['id']
        folder.mkdir(exist_ok=True)
        shutil.copy2(src/'manifest.json', folder/'source-manifest.json')
        frames, hashes, palette = [], [], set()
        union = Image.new('RGBA', tuple(spec['canvas']))
        for path in paths:
            for sub in ('native', 'native-indexed', 'enlarged-4x'):
                (folder/sub).mkdir(exist_ok=True)
                target = folder/sub/path.name
                shutil.copy2(src/sub/path.name, target)
                assert sha(target) == sha(src/sub/path.name)
            im = Image.open(path).convert('RGBA')
            indexed = Image.open(folder/'native-indexed'/path.name).convert('RGBA')
            enlarged = Image.open(folder/'enlarged-4x'/path.name).convert('RGBA')
            assert im.size == tuple(spec['canvas']) and im.tobytes() == indexed.tobytes()
            assert enlarged.tobytes() == im.resize((1536,896),N).tobytes()
            arr = np.asarray(im)
            assert set(np.unique(arr[:,:,3])).issubset({0,255})
            palette.update(map(tuple, np.unique(arr[arr[:,:,3]>0,:3], axis=0).tolist()))
            frames.append(im); union.alpha_composite(im); hashes.append(sha(path))
        declared = manifest.get('paletteRgb')
        if declared is None:
            declared = json.loads((src.parent/'manifest.json').read_text(encoding='utf-8'))['paletteRgb']
        assert palette == set(map(tuple, declared))
        bb = union.getbbox()
        offset = [item['left']-bb[0], spec['baseline']-192]
        assert item['left'] >= 0 and item['left']+bb[2]-bb[0] <= 960
        crop = [bb[0]-8, bb[1]-8, bb[2]+8, bb[3]+8]
        # A lossless independent loop keeps the exact accepted per-key durations.
        previews = []
        for im in frames:
            im = im.crop(crop)
            white = Image.new('RGBA',im.size,'white'); white.alpha_composite(im)
            previews.append(white.resize((im.width*4,im.height*4),N))
        previews[0].save(folder/'loop-white-4x.webp', save_all=True, append_images=previews[1:], duration=ds, loop=0, lossless=True, exact=True)
        decoded = Image.open(folder/'loop-white-4x.webp')
        assert decoded.n_frames == len(ds)
        for i, (expected, duration) in enumerate(zip(previews, ds)):
            decoded.seek(i); decoded.load()
            assert decoded.convert('RGBA').tobytes() == expected.tobytes()
            assert decoded.info['duration'] == duration
        row = {**item, 'durationsMs':ds, 'paletteRgb':[list(v) for v in sorted(palette)], 'bounds':list(bb), 'offset':offset, 'crop':crop, 'paths':[f'{item["id"]}/native/{p.name}' for p in paths], 'sha256':hashes, 'approval':'approved-by-user'}
        dump(folder/'manifest.json',row)
        data.append(row); all_frames.append(frames)
    assert sum(len(fs) for fs in all_frames) == 90
    # The entire union of every pose must stay separate at actual native scale.
    for left, right in zip(data, data[1:]):
        assert left['left']+left['bounds'][2]-left['bounds'][0]+24 <= right['left']
    info = {**spec, 'monsters':data, 'totalNativeFrames':90, 'playerCanvas':list(player.size), 'playerBounds':list(player.getbbox()), 'referenceHashes':{p.name:sha(p) for p in (out/'references').glob('*.png')}, 'overviewClip':{'durationMs':6400,'sampleMs':40,'note':'Finite overview clip. Its outer reset is not an individual gait seam; the HTML plays each exact timing continuously.'}}
    dump(out/'manifest.json', info)
    fontpath = 'C:/Windows/Fonts/malgun.ttf'
    font = ImageFont.truetype(fontpath,16)
    small = ImageFont.truetype(fontpath,13)
    title = ImageFont.truetype(fontpath,24)

    def roster(elapsed):
        canvas = Image.new('RGBA',(960,540),'white'); draw = ImageDraw.Draw(canvas)
        draw.text((32,26),'2스테이지 · 일반 몬스터 5종 이동 채택본',font=title,fill='#202635')
        draw.text((32,66),'근거리 3종 / 원거리 2종 · 승인 크기 그대로 · 디자인 및 합성 미리보기',font=small,fill='#586174')
        draw.line((24,341,940,341),fill='#dedfe3')
        pb=player.getbbox(); canvas.alpha_composite(player.crop(pb),(32,spec['baseline']-(pb[3]-pb[1])))
        draw.text((32,365),'플레이어',font=font,fill='#202635')
        for row, frames in zip(data,all_frames):
            canvas.alpha_composite(frames[frame_at(row['durationsMs'],elapsed)],tuple(row['offset']))
            center=row['left']+(row['bounds'][2]-row['bounds'][0])/2
            for y,text in [(365,row['name']),(393,row['role']),(419,f'{row["frameCount"]}장 / {row["cycleMs"]/1000:g}초')]:
                ft=font if y==365 else small; width=draw.textlength(text,font=ft)
                draw.text((center-width/2,y),text,font=ft,fill='#202635' if y==365 else '#586174')
        draw.text((32,489),'표시 크기 960×540 · 엘리트/보스 이동 및 공격 모션은 별도 작업 · 실제 게임 캡처 아님',font=small,fill='#586174')
        return canvas

    roster(0).save(out/'previews/roster-native.png')
    clip=[roster(t) for t in range(0,6400,40)]
    clip[0].save(out/'previews/roster-overview.webp',save_all=True,append_images=clip[1:],duration=40,loop=0,lossless=True,exact=True)
    # Contact image shows the original scene and original player for each monster.
    board=Image.new('RGBA',(960*2,580*3),'white'); draw=ImageDraw.Draw(board)
    for i,(row,frames) in enumerate(zip(data,all_frames)):
        panel=scene.copy();panel.alpha_composite(frames[0],(238,94))
        x=(i%2)*960;y=(i//2)*580
        board.alpha_composite(panel,(x,y));draw.text((x+16,y+548),row['name']+' · 원본 맵 합성 / 1배',font=font,fill='#202635')
    board.save(out/'previews/maps-contact.png')
    page(out, info)
    shutil.copy2(SPEC, out/'presentation-spec.json')
    (out/'README.md').write_text('''# 일반 몬스터 5종 이동 채택본

근거리: 등짐분사수 D 정리본20장/1000ms, 쐐기보행기19장/1300ms, 먹침필경사 빠른 A18장/540ms.
원거리: 신호투척수 바지 보정 C17장/900ms, 늘어진수의 B16장/1600ms.

총90개 native PNG와 indexed 및 정확한4배 PNG를 채택본에서 바이트 그대로 복사한다. 각 manifest.json에 원본 경로·프레임 순서·장별 시간·색·해시를 보존한다. 원본 생성 그림과 원본 반환 시트는 source-manifest.json이 가리키는 기존 출력에 보존되어 있다.

index.html은 각자의 정확한 시간표로 계속 재생한다. 모든 확대는 최근접 정수 배율이다. 전체 비교는 원래 크기이며 플레이어 투명 캔버스192×160과 실제66×64 영역을 구분한다. 맵 비교는 기존 C-game.png 위 합성이다. 배경 밝기/색/선명도를 수정하지 않는다. 실제 게임 캡처가 아니다.

roster-overview.webp는40ms 간격의6.4초 전체 확인 클립이다. 클립 끝의 재시작은 각 이동 루프의 접합이 아니다. 개별 loop-white-4x.webp는 원래 시간표를 정확하게 보존한다. Unity 통합과 전투 이동속도/공격/엘리트/보스 작업은 포함하지 않는다.
''',encoding='utf-8')
    archive=out/'stage2-normal-locomotion-approved-20261101.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(out))
    print(json.dumps({'output':str(out),'nativeFrames':90,'counts':[r['frameCount'] for r in data],'cycles':[r['cycleMs'] for r in data],'paletteCounts':[len(r['paletteRgb']) for r in data],'byteExactCopies':270,'loopTimingVerified':True,'zipEntries':len(zipfile.ZipFile(archive).namelist())},ensure_ascii=False))


def page(out, info):
    html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>일반 몬스터 5종 · 이동 채택본</title>
<style>*{box-sizing:border-box}body{margin:0;background:#f3f2ef;color:#202635;font:16px/1.6 system-ui,sans-serif}main{max-width:1320px;margin:auto;padding:32px}h1{font-size:30px;margin:0}h2{font-size:22px;margin:28px 0 12px}p{margin:8px 0 16px;color:#586174}button,select,a.download{font:inherit;padding:8px 14px;background:white;border:1px solid #ccc;border-radius:8px;color:inherit}button{cursor:pointer}.controls{display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin:16px 0}canvas,img{image-rendering:pixelated;image-rendering:crisp-edges}.view{overflow:auto;background:white;border:1px solid #d9d9df;border-radius:10px}.view canvas{display:block}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:18px}.card{background:#fff;padding:20px;border:1px solid #ddd;border-radius:12px}.card h3{margin:0}.card .view{margin:16px 0}.tag{font-size:13px;color:#586174}a{color:#304c8c}.foot{font-size:14px}</style>
<main><h1>일반 몬스터 5종 · 이동 채택본</h1><p>근거리 3종 + 원거리 2종 선택 완료. 승인된 크기·픽셀·재생 속도를 그대로 모았습니다.<br>디자인 및 합성 미리보기이며 실제 게임 캡처가 아닙니다.</p>
<div class="controls"><button id="play">일시정지</button><button id="reset">처음부터</button><label>배경 <select id="bg"><option value="#ffffff">흰색</option><option value="#243138">어두운 벽색</option><option value="#486578">푸른 원경색</option><option value="#111316">검정 근경색</option></select></label><label>전체 확대 <select id="zoom"><option value="1">1배 · 실제 크기</option><option value="2">2배</option></select></label><span id="time"></span></div>
<h2>전체 · 승인 플레이어와 같은 배율</h2><div class="view"><canvas id="roster" width="960" height="540"></canvas></div><p class="foot">캐릭터마다 원래 주기를 유지하며 제자리에서 재생합니다. 플레이어 실제 영역 66×64px / 투명 캔버스 192×160px. 아래 키 수와 시간은 아트 재생 규격입니다.</p>
<h2>각 몬스터 확대</h2><div class="controls"><label>개별 확대 <select id="detailzoom"><option value="2">2배</option><option value="4" selected>4배</option><option value="8">8배</option></select></label></div><div class="grid" id="cards"></div>
<h2>기존 지하수로 위 크기·시인성</h2><div class="controls"><select id="mapselect"></select></div><div class="view"><canvas id="map" width="960" height="540"></canvas></div><p class="foot">원본 960×540 배경과 승인 플레이어를 보존한 합성입니다. 각 개체를 같은 위치에서 비교하며 배경은 밝히거나 흐리지 않았습니다.</p>
<h2>파일</h2><div class="controls"><a class="download" href="stage2-normal-locomotion-approved-20261101.zip">채택 5종 전체 ZIP</a><a href="manifest.json">순서·시간·색·원본 경로</a><a href="previews/maps-contact.png">맵 비교 5장 모아보기</a></div><p class="foot">native 90장 + indexed 90장 + 정수 4배 90장. 원본과 이전 후보는 각 출처 폴더에 보존되어 있습니다. 현재 완료 범위는 일반 5종 이동 선택이며 엘리트·보스 이동 및 공격·피격·사망 제작, Unity 연결은 별도입니다.</p><p id="loading">이미지 준비 중…</p></main>
<script>const DATA=__DATA__;
const load=p=>new Promise((ok,no)=>{let im=new Image;im.onload=()=>ok(im);im.onerror=()=>no(Error(p));im.src=p});
const at=(r,t)=>{t%=r.cycleMs;for(let i=0;i<r.durationsMs.length;i++){if(t<r.durationsMs[i])return i;t-=r.durationsMs[i]}return 0};
const ctx=c=>{let x=c.getContext('2d');x.imageSmoothingEnabled=false;return x};
let running=true,elapsed=0,last=performance.now();
async function init(){try{const rows=DATA.monsters;const images=await Promise.all(rows.map(r=>Promise.all(r.paths.map(load))));const [player,scene]=await Promise.all([load('references/approved-player.png'),load('references/C-game.png')]);let cards=[];const get=id=>document.getElementById(id);const roster=get('roster'),map=get('map'),rctx=ctx(roster),mctx=ctx(map);
rows.forEach((r,i)=>{const card=document.createElement('article');card.className='card';card.innerHTML=`<h3>${r.name} <span class="tag">${r.role}</span></h3><div>${r.choice}</div><div class="tag">${r.frameCount}장 · ${(r.cycleMs/1000).toFixed(2)}초 · ${r.paletteRgb.length}색</div><div class="view"><canvas></canvas></div><div class="tag" data-key></div><div class="controls"><button>이전 키</button><button>다음 키</button><a href="${r.id}/loop-white-4x.webp">개별 루프</a><a href="${r.paths[0]}">첫 PNG</a></div>`;get('cards').append(card);const c=card.querySelector('canvas');cards.push({c,key:card.querySelector('[data-key]'),r,i});let b=card.querySelectorAll('button');b.forEach((button,j)=>button.onclick=()=>{let key=(at(r,elapsed)+(j?1:-1)+r.frameCount)%r.frameCount;elapsed=r.durationsMs.slice(0,key).reduce((a,b)=>a+b,0);running=false;get('play').textContent='재생';draw()});let o=document.createElement('option');o.value=i;o.textContent=r.name;get('mapselect').append(o)});
function label(x,text,y,center,color='#202635',size=16){x.font=`${size}px system-ui,sans-serif`;x.fillStyle=color;x.textAlign='center';x.fillText(text,center,y)}
function draw(){const bg=get('bg').value;const ink=bg==='#ffffff'?'#202635':'#f1ede4';rctx.fillStyle=bg;rctx.fillRect(0,0,960,540);rctx.textAlign='left';rctx.fillStyle=ink;rctx.font='24px system-ui,sans-serif';rctx.fillText('일반 몬스터 5종 · 이동 채택본',32,52);rctx.font='14px system-ui,sans-serif';rctx.fillText('승인 크기 그대로 / 근거리 3 · 원거리 2 / 디자인 및 합성 미리보기',32,82);rctx.strokeStyle=bg==='#ffffff'?'#ddd':'#7b858c';rctx.beginPath();rctx.moveTo(24,341);rctx.lineTo(940,341);rctx.stroke();let p=DATA.playerBounds;rctx.drawImage(player,p[0],p[1],p[2]-p[0],p[3]-p[1],32,340-(p[3]-p[1]),p[2]-p[0],p[3]-p[1]);label(rctx,'플레이어',382,65,ink);rows.forEach((r,i)=>{let k=at(r,elapsed);rctx.drawImage(images[i][k],...r.offset);let center=r.left+(r.bounds[2]-r.bounds[0])/2;label(rctx,r.name,382,center,ink);label(rctx,r.role,408,center,ink,13);label(rctx,`${r.frameCount}장 / ${r.cycleMs/1000}초`,433,center,ink,13)});let z=Number(get('zoom').value);roster.style.width=960*z+'px';roster.style.height=540*z+'px';let dz=Number(get('detailzoom').value);cards.forEach(({c,key,r,i})=>{let b=r.crop,w=b[2]-b[0],h=b[3]-b[1];if(c.width!==w||c.height!==h){c.width=w;c.height=h}c.style.width=w*dz+'px';c.style.height=h*dz+'px';let x=ctx(c);x.fillStyle=bg;x.fillRect(0,0,w,h);let k=at(r,elapsed);x.drawImage(images[i][k],b[0],b[1],w,h,0,0,w,h);key.textContent=`키 ${k+1} / ${r.frameCount} · ${r.durationsMs[k]}ms`});let i=Number(get('mapselect').value),r=rows[i];mctx.clearRect(0,0,960,540);mctx.drawImage(scene,0,0);mctx.drawImage(images[i][at(r,elapsed)],238,94);get('time').textContent=(elapsed/1000).toFixed(2)+'초'}
get('play').onclick=()=>{running=!running;last=performance.now();get('play').textContent=running?'일시정지':'재생'};get('reset').onclick=()=>{elapsed=0;draw()};['bg','zoom','detailzoom','mapselect'].forEach(id=>get(id).onchange=draw);document.addEventListener('visibilitychange',()=>last=performance.now());get('loading').textContent='90개 채택 프레임 준비 완료 · 각자의 원래 시간표로 재생';function tick(t){if(running)elapsed+=t-last;last=t;draw();requestAnimationFrame(tick)}last=performance.now();requestAnimationFrame(tick)}catch(e){document.getElementById('loading').textContent='파일을 불러오지 못했습니다: '+e.message}}
init();</script></html>'''
    (out/'index.html').write_text(html.replace('__DATA__',json.dumps(info,ensure_ascii=False)),encoding='utf-8')


if __name__ == '__main__':
    build()
