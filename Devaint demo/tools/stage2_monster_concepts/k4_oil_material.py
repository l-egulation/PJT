"""Present AI-drawn heavy-oil key shapes beside the unchanged approved body."""
import base64
from html import escape
import json
from pathlib import Path
import shutil

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label

from b3_walk_return import dump,white
from k4_d_pixel_walk import ROOT,N,sha

SPEC=ROOT/'data/presentation/stage2_k4_oil_material.json'


def isolate_row(source,index):
    # Generated keys can cross an equal-third row boundary. Isolate the three
    # main connected shapes, attaching each detached drop to the nearest key.
    a=np.array(source);mask=a[:,:,3]>=128
    assert mask.any() and not mask.all(),'Expected actual transparent effect rows'
    parts,_=label(mask);counts=np.bincount(parts.ravel());counts[0]=0
    major=np.argsort(counts)[-3:].tolist();major.sort(key=lambda c:np.where(parts==c)[0].mean())
    bounds=[]
    for c in major:
        y,x=np.where(parts==c);bounds.append([int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)])
    selected=parts==major[index]
    for c in np.flatnonzero(counts>=9):
        if c in major:continue
        y,x=np.where(parts==c);cx=x.mean();cy=y.mean()
        distance=[max(b[0]-cx,0,cx-b[2])**2+max(b[1]-cy,0,cy-b[3])**2 for b in bounds]
        if int(np.argmin(distance))==index:selected|=parts==c
    a[~selected]=0;main=parts==major[index]
    yy,xx=np.where(main);right=int(xx.max());root_y=float(yy[xx>=right-3].mean())
    y,x=np.where(selected);box=[int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]
    return Image.fromarray(a).crop(tuple(box)),[right-box[0],root_y-box[1]],box


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];(out/'references').mkdir(parents=True,exist_ok=True)
    source=Image.open(ROOT/s['source']).convert('RGBA');body=Image.open(ROOT/s['body']).convert('RGBA');shutil.copy2(ROOT/s['body'],out/'references/approved-hold-native.png');shutil.copy2(SPEC,out/'references/presentation-spec.json')
    cells=[isolate_row(source,i) for i in range(3)]
    scale=s['firstFlowWidthPxTrial']/cells[0][0].width;palette=np.array(s['paletteRgb'],np.uint8);records=[];contexts=[]
    nozzle=np.array(s['bodyOffsetPx'])+s['nozzleBodyPx']
    for phase,(cell,root,box) in zip(s['phases'],cells):
        row=out/'source-rows'/f'{phase["id"]}.png';row.parent.mkdir(exist_ok=True);cell.save(row)
        size=[max(1,round(cell.width*scale)),max(1,round(cell.height*scale))];raw=np.array(cell.resize(tuple(size),N));mask=raw[:,:,3]>=128
        nearest=((raw[:,:,:3,None].astype(int)-palette.T[None,None].astype(int))**2).sum(2).argmin(2)
        a=np.zeros_like(raw);a[mask,:3]=palette[nearest[mask]];a[mask,3]=255;pixel=Image.fromarray(a)
        p=out/'native'/f'{phase["id"]}.png';p.parent.mkdir(exist_ok=True);pixel.save(p)
        root_px=[min(size[0]-1,round(root[0]*scale)),min(size[1]-1,round(root[1]*scale))];pos=tuple(nozzle-np.array(root_px))
        context=Image.new('RGBA',tuple(s['contextCanvas']));context.alpha_composite(pixel,pos);context.alpha_composite(body,tuple(s['bodyOffsetPx']));contexts.append(context)
        p=out/'context'/f'{phase["id"]}-white-2x.png';p.parent.mkdir(exist_ok=True);white(context).resize((context.width*2,context.height*2),N).save(p)
        record={**phase,'sourceRow':row.relative_to(out).as_posix(),'sourceBoundsInSheet':box,'native':f'native/{phase["id"]}.png','nativeSize':size,'rootNativePx':root_px,'placementPx':list(map(int,pos)),'context':p.relative_to(out).as_posix(),'bodySha256':sha(ROOT/s['body'])};records.append(record)
        assert set(np.unique(a[:,:,3]))<={0,255}
        assert set(map(tuple,np.unique(a[mask,:3],axis=0)))<=set(map(tuple,palette))
        assert pos[0]>=0 and pos[1]>=0 and pos[0]+size[0]<=context.width and pos[1]+size[1]<=context.height,'Effect must not clip'
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18);board=Image.new('RGBA',(960,3*680),'white');d=ImageDraw.Draw(board)
    for i,(record,f) in enumerate(zip(records,contexts)):
        d.text((24,i*680+12),record['name']+' · 재질/형태 시안',font=font,fill='#80586e');board.alpha_composite(f.resize((960,640),N),(0,i*680+40))
    board.save(out/'context-board.png')
    uri=lambda p:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode('ascii')
    cards=''.join('<section><h2>'+escape(r['name'])+'</h2><p>'+escape(r['note'])+'</p><div class="viewport"><img src="'+uri(out/r['context'])+'" alt="'+escape(r['name'])+'"></div></section>' for r in records)
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 · 검은 기름 재질 시안</title><style>body{margin:0;background:#eeeae3;color:#29252f;font:16px/1.65 system-ui,'Malgun Gothic',sans-serif}main{max-width:1000px;margin:auto;padding:24px}section{background:white;padding:20px;margin:20px 0;border:1px solid #d6cdc5;border-radius:8px}h1{font-size:27px}h2{font-size:22px}.viewport{overflow:auto}img{display:block;image-rendering:pixelated;width:960px;max-width:none}.small{font-size:14px;color:#70636a}a{color:#80586e}</style><main><h1>등짐분사수 · 검은 기름의 무게감</h1><p>이미지 생성 도구로 그린 큰 재질 덩어리를 기존 캐릭터에 붙인 <strong>정지 키 형태 시안</strong>입니다. 세 그림은 시간순 재질 방향이며 아직 완성 공격 애니메이션이 아닙니다.</p><p class="small">캐릭터 버팀 자세·기존 두 색·동일 픽셀 배율 · 디자인 합성 미리보기 / 실제 게임 캡처 아님</p>'''+cards+'''<p class="small">원본/프롬프트는 generated/와 references/, 원본 행은 source-rows/, 기존 팔레트 적용본은 native/에 보존합니다. 기름 농도의 시각 방향을 검토하는 단계이며 물성 측정/힛박스 확정/게임 적용이 아닙니다. 채택된 늘어진 수의27장은 변경하지 않습니다.</p></main></html>'''
    (out/'index.html').write_text(html,encoding='utf-8');dump(out/'manifest.json',{**s,'rawSourceSha256':sha(ROOT/s['source']),'commonPixelScale':scale,'results':records,'motionCreated':False,'h3PixelsChanged':0,'runtimeIntegration':'not-performed'})
    print(json.dumps({'materialKeys':3,'commonScale':scale,'nativeSizes':[r['nativeSize'] for r in records],'bodyPixelsChanged':0,'motionCreated':False}))


if __name__=='__main__':build()
