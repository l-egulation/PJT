"""Extract returned Aether GIFs losslessly and prepare separate pixel review copies."""
from pathlib import Path
import hashlib
from html import escape
import json
import shutil
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / 'data/presentation/stage2_walk_review.json'
N = Image.Resampling.NEAREST


def dump(p, d):
    p.write_text(json.dumps(d, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def clean_mask(a):
    rgb = a[:, :, :3].astype(int)
    # White video background and neutral bright edge contamination are not art colors.
    mask = (rgb.min(2) < 240) & ~((rgb.min(2) > 165) & (np.ptp(rgb, axis=2) < 18))
    components, _ = label(mask)
    sizes = np.bincount(components.ravel())
    sizes[0] = 0
    return sizes[components] >= 256


def bounds(mask):
    y, x = np.where(mask)
    return [int(x.min()), int(y.min()), int(x.max() + 1), int(y.max() + 1)]


def animate(frames, durations, path):
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=durations,
                   loop=0, lossless=True, exact=True)


def review_board(frames, indices, box, cols, scale, path):
    w, h = box[2]-box[0], box[3]-box[1]
    board = Image.new('RGBA', (cols*w*scale, ((len(indices)+cols-1)//cols)*(h*scale+26)), '#26313b')
    draw = ImageDraw.Draw(board)
    font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 16)
    for cell, index in enumerate(indices):
        x, y = cell % cols * w*scale, cell // cols * (h*scale+26)
        draw.text((x+4,y+3), f'{index:03}', fill='#e8c48d', font=font)
        board.alpha_composite(frames[index].crop(box).resize((w*scale,h*scale),N),(x,y+26))
    board.save(path)


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    out, base = ROOT/spec['output'], ROOT/spec['base']
    sources = out/'sources'
    out.mkdir(exist_ok=True, parents=True)
    rows = []
    for clip in spec['clips']:
        ident, base_id = clip['id'], clip.get('baseId',clip['id'])
        folder = out/ident
        for sub in ('raw-frames','native','enlarged-4x','previews','references'):
            (folder/sub).mkdir(parents=True,exist_ok=True)
        basic = Image.open(base/base_id/'native/base-frame.png').convert('RGBA')
        shutil.copy2(base/base_id/'native/base-frame.png',folder/'references/base-frame.png')
        palette = np.unique(np.array(basic).reshape(-1,4),axis=0)
        palette = palette[palette[:,3]==255,:3]
        if clip.get('pixelFloat'):
            prior = ROOT/'outputs/waterway/body-idles-8-v2-20261031/H3'
            meta = json.loads((prior/'metadata.json').read_text(encoding='utf-8'))
            frames = [Image.open(prior/'frames'/f'idle-{i:02}.png').convert('RGBA') for i in range(8)]
            durations = meta['durations']
            selected = list(range(8))
            provenance = {'method':'existing exact pixel floating frames','sourceHashes':[sha(prior/'frames'/f'idle-{i:02}.png') for i in range(8)]}
        else:
            source = sources/clip['source']
            gif = Image.open(source)
            boxes, durations = [], []
            for i in range(gif.n_frames):
                gif.seek(i)
                raw = gif.convert('RGBA')
                raw.save(folder/'raw-frames'/f'walk-{i:03}.png')
                boxes.append(bounds(clean_mask(np.array(raw))))
                durations.append(gif.info.get('duration',100))
            boxes = np.array(boxes)
            # A single scale and translation retain changes and drift present in the source.
            dims = boxes[1:,2:]-boxes[1:,:2]
            bb = basic.getbbox()
            axis = 0 if clip['scaleBy']=='width' else 1
            target = (bb[2]-bb[0],bb[3]-bb[1])[axis]
            scale = target/float(np.median(dims[:,axis]))
            anchor = [float(np.median((boxes[1:,0]+boxes[1:,2])/2)),float(np.median(boxes[1:,3]))]
            offset = [192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
            frames = []
            for i in range(gif.n_frames):
                raw = np.array(Image.open(folder/'raw-frames'/f'walk-{i:03}.png').convert('RGBA'))
                mask = clean_mask(raw)
                # Inverse nearest-neighbor sampling of the same source canvas each frame.
                sx = np.floor((np.arange(384)+0.5-offset[0])/scale).astype(int)
                sy = np.floor((np.arange(224)+0.5-offset[1])/scale).astype(int)
                valid = (sy[:,None]>=0)&(sy[:,None]<raw.shape[0])&(sx[None,:]>=0)&(sx[None,:]<raw.shape[1])
                yy, xx = np.clip(sy,0,raw.shape[0]-1), np.clip(sx,0,raw.shape[1]-1)
                sampled = raw[yy[:,None],xx[None,:]]
                alpha = valid & mask[yy[:,None],xx[None,:]]
                rgb = sampled[:,:,:3].astype(np.int32)
                distance = ((rgb[:,:,None,:]-palette.astype(np.int32))**2).sum(3)
                result = np.zeros((224,384,4),dtype=np.uint8)
                result[:,:,:3] = palette[distance.argmin(2)]
                result[:,:,3] = alpha.astype(np.uint8)*255
                result[~alpha] = 0
                frames.append(Image.fromarray(result))
            selected = list(range(1,len(frames)))
            if frames[selected[-1]].tobytes()==frames[selected[0]].tobytes():
                # Omit an exact duplicated loop endpoint; keep the full source above.
                selected.pop()
            provenance = {'source':clip['source'],'sha256':sha(source),'sourceCanvas':list(gif.size),
                          'sourceBoxes':boxes.tolist(),'scale':scale,'offset':offset,'targetScaleBy':clip['scaleBy'],
                          'normalization':'white cleanup + existing palette + fixed inverse nearest sampling'}
        for i,frame in enumerate(frames):
            assert frame.size==(384,224)
            assert set(np.unique(np.array(frame)[:,:,3]))=={0,255}
            frame.save(folder/'native'/f'walk-{i:03}.png')
            frame.resize((1536,896),N).save(folder/'enlarged-4x'/f'walk-{i:03}.png')
        active = [frames[i] for i in selected]
        ds = [durations[i] for i in selected]
        union = Image.new('RGBA',(384,224))
        for frame in active:union.alpha_composite(frame)
        b = union.getbbox()
        box = (max(0,b[0]-8),max(0,b[1]-8),min(384,b[2]+8),min(224,b[3]+8))
        animate(active,ds,folder/'previews/walk-native.webp')
        enlarged = [f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N) for f in active]
        animate(enlarged,ds,folder/'previews/walk-4x.webp')
        gifs=[]
        for f in enlarged:
            bg=Image.new('RGBA',f.size,'#26313b');bg.alpha_composite(f);gifs.append(bg.convert('RGB'))
        gifs[0].save(folder/'previews/walk-4x.gif',save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2,optimize=False)
        keys = [selected[round(i*(len(selected)-1)/7)] for i in range(8)]
        review_board(frames,keys,box,4,2,folder/'previews/key-frames.png')
        review_board(frames,list(range(len(frames))),box,8,2,folder/'previews/all-frames.png')
        row = {**clip,**provenance,'frameCount':len(frames),'reviewIndices':selected,
               'durations':durations,'reviewDurationMs':sum(ds),'reviewBox':list(box),
               'baseHash':sha(base/base_id/'native/base-frame.png')}
        dump(folder/'metadata.json',row);rows.append(row)
        print(f'{ident}: {len(frames)} frames / {len(selected)} review frames',flush=True)
    dump(out/'manifest.json',rows)
    shutil.copy2(SPEC,out/'motion-spec.json')
    page(out,rows)
    (out/'README.md').write_text('# 잡몹 걷기 추출·픽셀 부유 검토\n\nsources에 받은 ZIP·GIF·PNG를 그대로 보존한다. sheet-frames는 받은 PNG 시트의 셀을 행 우선으로 그대로 분리한1280×720 PNG다. raw-frames는 GIF 전체를 원래1280×720으로 복호화한 RGBA PNG다. native는 흰 배경·작은 잡점 정리, 기존 팔레트와 클립별 고정 배율을 적용한384×224 검토용 사본이다. enlarged-4x는 정확한4배다. previews에 전체·8키 비교와 GIF/WebP를 둔다. 첫 정지 입력은 raw/native에 보존하며 반복 미리보기에서만 제외한다. sourceBoxes·scale·offset·시간은 metadata에 기록한다. 프레임별 크기·접지 보정이나 잘린 부분 복원은 하지 않는다. H3는 기존 픽셀8키를 사용한다. 모션은 아트 검토용이며 맵은 이동 합성, 속도는 연출값이다. 게임 구현·승인 완료가 아니다.\n',encoding='utf-8')
    package(out,rows)


def package(out,rows):
    (out/'references').mkdir(exist_ok=True)
    shutil.copy2(ROOT/'outputs/waterway/pixel-base-step1-20261031/references/C-game.png',out/'references/C-game.png')
    for r in rows:
        if r.get('sheet'):
            # Also split the supplied PNG contact sheet without GIF palette conversion.
            Image.MAX_IMAGE_PIXELS=100000000
            sheet_path=out/'sources'/r['sheet']
            with Image.open(sheet_path) as sheet:
                cols, row_count=sheet.width//1280,sheet.height//720
                assert cols*row_count==r['frameCount']
                raw=out/r['id']/'sheet-frames';raw.mkdir(exist_ok=True)
                for i in range(r['frameCount']):
                    box=((i%cols)*1280,(i//cols)*720,(i%cols+1)*1280,(i//cols+1)*720)
                    sheet.crop(box).save(raw/f'walk-{i:03}.png')
                r['sheetCanvas']=list(sheet.size)
                r['sheetGrid']=[cols,row_count]
                r['sheetSha256']=sha(sheet_path)
            dump(out/r['id']/'metadata.json',r)
        folder=out/r['id']/'previews/frames'
        folder.mkdir(exist_ok=True)
        box=r['reviewBox']
        for i in range(r['frameCount']):
            im=Image.open(out/r['id']/'native'/f'walk-{i:03}.png').crop(box)
            im.resize((im.width*4,im.height*4),N).save(folder/f'walk-{i:03}.png')
    page(out,rows)
    dump(out/'manifest.json',rows)
    with zipfile.ZipFile(out/'walk-review-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and ('sources' not in p.relative_to(out).parts or p.suffix=='.gif'):
                z.write(p,p.relative_to(out).as_posix())


def page(out,rows):
    cards=[]
    for r in rows:
        i=r['id'];original=(f'<a href="sources/{r["source"]}">원본 GIF</a> · <a href="{i}/sheet-frames/walk-000.png">PNG 시트 원본 첫 장</a> · ' if r.get('source') else '');cards.append(f'''<section id="{i}"><h2>{escape(r['name'])} · {r['frameCount']}장</h2><p>{escape(r['note'])}</p><div class="scroll"><img class="animation" data-id="{i}" src="{i}/previews/walk-4x.webp"></div><button data-play="{i}">정지</button><input data-step="{i}" type="range" min="0" max="{r['frameCount']-1}" value="{r['reviewIndices'][0]}"><span data-label="{i}">재생 중</span><p>{original}<a href="{i}/previews/all-frames.png">전체 프레임판</a> · <a href="{i}/previews/key-frames.png">8키 확대</a> · <a href="{i}/metadata.json">원본·프레임 정보</a> · <a href="{i}/native/walk-000.png">PNG 첫 장</a></p><img class="board" src="{i}/previews/key-frames.png"><details><summary>실제 크기 이동 합성 · 속도는 연출값</summary><div class="map"><img class="background" src="references/C-game.png"><img class="actor" src="{i}/previews/walk-native.webp"></div></details></section>''')
    js='''const rows=DATA;document.querySelectorAll('[data-play]').forEach(b=>b.onclick=()=>{const im=document.querySelector('[data-id="'+b.dataset.play+'"]');if(b.textContent==='정지'){const r=rows.find(r=>r.id===b.dataset.play);im.src=r.id+'/previews/frames/walk-'+String(r.reviewIndices[0]).padStart(3,'0')+'.png';b.textContent='재생'}else{im.src=b.dataset.play+'/previews/walk-4x.webp';im.removeAttribute('width');im.removeAttribute('height');b.textContent='정지'}});document.querySelectorAll('[data-step]').forEach(s=>s.oninput=()=>{const im=document.querySelector('[data-id="'+s.dataset.step+'"]');im.src=s.dataset.step+'/previews/frames/walk-'+String(s.value).padStart(3,'0')+'.png';document.querySelector('[data-play="'+s.dataset.step+'"]').textContent='재생';document.querySelector('[data-label="'+s.dataset.step+'"]').textContent='프레임 '+s.value});'''.replace('DATA',json.dumps(rows))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>잡몹 걷기 프레임 검토</title><style>body{margin:0;background:#17212a;color:#eee5d7;font:16px/1.7 system-ui}main{max-width:1120px;margin:auto;padding:24px}a{color:#e8c48d}section{margin:24px 0;padding:24px;background:#26313b;border-radius:12px}img{image-rendering:pixelated}.scroll{overflow:auto}.animation{display:block}.board{max-width:100%}button{padding:8px 18px;margin:10px}.map{position:relative;width:960px;height:540px;overflow:hidden}.background{position:absolute;width:960px;height:540px}.actor{position:absolute;width:384px;height:224px;top:94px;animation:travel 12s linear infinite}@keyframes travel{from{left:700px}to{left:-250px}}details{overflow:auto}</style><main><h1>잡몹5종 · 걷기 프레임과 수의 부유</h1><p>받은 GIF 원본5개(보행기2버전)의 전체372장을 추출하고 수의8키를 추가했습니다. PNG 시트도 동일 순서로 개별 분리했습니다. 원본 추출과 픽셀 크기·색 정리본을 구분합니다. 모션은 아트 검토용이며 맵은 이동 합성 미리보기입니다.</p><p>반복 재생은 크기 차이가 있는 첫 정지 입력을 제외합니다. 첫 장도 슬라이더·전체 프레임·원본 추출에 보존합니다. 정리본은 클립별 고정 배율이며 원본에서 생긴 형태·크기 변화와 잘림은 복원하지 않았습니다.</p><p><a download href="walk-review-20261031.zip">전체 프레임·확대본 ZIP</a> · <a href="manifest.json">추출 정보</a></p>'''+''.join(cards)+'<script>'+js+'</script></main></html>',encoding='utf-8')


if __name__=='__main__':
    build()
