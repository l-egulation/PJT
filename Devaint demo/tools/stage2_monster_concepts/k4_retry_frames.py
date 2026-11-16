"""Extract the new K4 white-background sheet and show an actual selected eight-key loop."""
from pathlib import Path
import hashlib,json,shutil,zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_k4_retry_frames.json'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];source=out/'source'/spec['sourceName'];Image.MAX_IMAGE_PIXELS=100000000
    for sub in ('all-frames','selected','previews'):(out/sub).mkdir(parents=True,exist_ok=True)
    w,h=spec['frameSize'];cols,rows=spec['grid']
    with Image.open(source) as sheet:
        assert sheet.size==tuple(spec['sheetSize'])==(w*cols,h*rows)
        for i in range(cols*rows):
            x,y=i%cols*w,i//cols*h;sheet.crop((x,y,x+w,y+h)).save(out/'all-frames'/f'frame-{i:03}.png')
    selected=[];mapping=[];boxes=[]
    for key,index in enumerate(spec['selectedSourceIndicesZeroBased']):
        src=out/'all-frames'/f'frame-{index:03}.png';dst=out/'selected'/f'walk-{key+1:02}.png';shutil.copy2(src,dst)
        im=Image.open(dst).convert('RGBA');selected.append(im);a=np.array(im);y,x=np.where(a[:,:,:3].min(2)<200);boxes.append([int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)])
        mapping.append({'selectedKey':key+1,'sourceIndexZeroBased':index,'sheetRow':index//cols,'sheetColumn':index%cols,'png':dst.relative_to(out).as_posix(),'sha256':sha(dst)})
    b=np.array(boxes);box=(max(0,int(b[:,0].min())-24),max(0,int(b[:,1].min())-24),min(w,int(b[:,2].max())+24),min(h,int(b[:,3].max())+24))
    ds=spec['reviewDurationsMs'];selected[0].save(out/'previews/selected-full-canvas.webp',save_all=True,append_images=selected[1:],duration=ds,loop=0,lossless=True,exact=True)
    crops=[im.crop(box) for im in selected];crops[0].save(out/'previews/selected-loop.webp',save_all=True,append_images=crops[1:],duration=ds,loop=0,lossless=True,exact=True)
    cw,ch=crops[0].size;tw,th=cw//2,ch//2;board=Image.new('RGB',(tw*4,(th+32)*2),'white');draw=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
    for i,im in enumerate(crops):
        x,y=i%4*tw,i//4*(th+32);board.paste(im.resize((tw,th),Image.Resampling.NEAREST).convert('RGB'),(x,y+32));draw.text((x+8,y+4),f'{i+1:02} / 원본 {mapping[i]["sourceIndexZeroBased"]+1}번',fill='black',font=font)
    board.save(out/'previews/selected-8.png')
    manifest={**spec,'sourceSha256':sha(source),'extractedCount':cols*rows,'selectedCount':len(selected),'selectedMapping':mapping,'previewCrop':list(box),'exportPixelsUnchanged':True}
    (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');shutil.copy2(SPEC,out/'extraction-spec.json')
    thumbs=''.join(f'<a href="{r["png"]}"><img src="{r["png"]}"><span>선정 {r["selectedKey"]:02} · 원본 {r["sourceIndexZeroBased"]+1}번 PNG</span></a>' for r in mapping)
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>새 등짐분사수 · 선정8장</title><style>body{margin:0;background:#eee;font:16px/1.7 system-ui;color:#25242b}main{max-width:1100px;padding:24px;margin:auto}a{color:#815149}img{image-rendering:pixelated}.loop{display:block;max-width:100%;margin:auto}.board{width:100%}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.grid a{display:block;background:white;padding:8px}.grid img{width:100%}@media(max-width:600px){.grid{grid-template-columns:repeat(2,1fr)}}</style><main><h1>새 등짐분사수 · 선정8프레임</h1><p>새 PNG 시트를100장으로 분리하고 한 보행 구간의8장을 골랐습니다. 흰 배경과 원본 색·모양을 유지하며 개별 PNG는 원본 셀1280×720 그대로입니다. 아래 루프는 선정8장만 재생합니다. PNG에는 시간이 없어1초 주기를 검토용으로 제안했습니다.</p><p><a download href="k4-selected-8-20261031.zip">선정8장 PNG·미리보기 ZIP</a> · <a href="manifest.json">원본 번호·추출 정보</a></p><img class="loop" src="previews/selected-loop.webp"><h2>선정한8장</h2><img class="board" src="previews/selected-8.png"><div class="grid">'''+thumbs+'</div><p>재채색·배경 제거·몸체 합성·새 프레임 생성은 하지 않았습니다. 원본 전체100장은 all-frames에 보존합니다. 선정은 검토안이며 게임용 픽셀 규격과 최종 승인은 별도입니다.</p></main></html>',encoding='utf-8')
    (out/'README.md').write_text('''# 새 등짐분사수 걷기 추출

source에 받은12800×7200 원본을 보존한다. all-frames에10×10 시트를 행 우선으로 자른1280×720 RGBA PNG100장, selected에 선정8장을 무변형 복사한다. 프레임 대응·해시·미리보기 공통 크롭·제안 시간은 manifest.json에 기록한다. 배경 제거·팔레트 변환·논리 크기 축소·파츠 합성은 수행하지 않았다. previews의 루프는 선정8장만 사용하며1초는 PNG에 없는 검토용 제안 시간이다. ZIP에는 선정8장과 화면·미리보기·메타데이터만 포함한다. 이전 모션은 보류 상태를 유지한다.
''',encoding='utf-8')
    allowed={'selected','previews','index.html','manifest.json','README.md','extraction-spec.json'}
    with zipfile.ZipFile(out/'k4-selected-8-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.relative_to(out).parts[0] in allowed:z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({'extracted':100,'selectedSourceIndicesZeroBased':spec['selectedSourceIndicesZeroBased'],'previewCrop':box}))


if __name__=='__main__':build()
