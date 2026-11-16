"""Restore source in-betweens and improve the loop boundary, keeping pixels untouched."""
from pathlib import Path
import hashlib,json,shutil,zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_k4_walk_smooth.json'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def sequence(prior,indices):
    return [Image.open(prior/'all-frames'/f'frame-{i:03}.png').convert('RGBA') for i in indices]


def encode(frames,ds,path):
    frames[0].save(path,save_all=True,append_images=frames[1:],duration=ds,loop=0,lossless=True,exact=True)


def pose_distance(a,b):
    a=np.array(a.crop((350,430,930,710)).resize((116,56),Image.Resampling.NEAREST))[:,:,:3].astype(float)
    b=np.array(b.crop((350,430,930,710)).resize((116,56),Image.Resampling.NEAREST))[:,:,:3].astype(float)
    return float(np.mean((a-b)**2))


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));prior=ROOT/spec['prior'];out=ROOT/spec['output']
    for sub in ('selected','previews'):(out/sub).mkdir(parents=True,exist_ok=True)
    oldmeta=json.loads((prior/'manifest.json').read_text(encoding='utf-8'));indices=list(range(spec['sourceStartZeroBased'],spec['sourceEndZeroBasedInclusive']+1));frames=sequence(prior,indices);old=sequence(prior,oldmeta['selectedSourceIndicesZeroBased']);mapping=[]
    for key,index in enumerate(indices):
        src=prior/'all-frames'/f'frame-{index:03}.png';dst=out/'selected'/f'walk-{key+1:02}.png';shutil.copy2(src,dst);mapping.append({'key':key+1,'sourceIndexZeroBased':index,'png':dst.relative_to(out).as_posix(),'sha256':sha(dst)})
    boxes=[]
    for im in frames+old:
        a=np.array(im);y,x=np.where(a[:,:,:3].min(2)<200);boxes.append([int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)])
    b=np.array(boxes);box=(max(0,int(b[:,0].min())-24),max(0,int(b[:,1].min())-24),min(1280,int(b[:,2].max())+24),min(720,int(b[:,3].max())+24));crops=[f.crop(box) for f in frames];ds=[spec['frameDurationMs']]*len(frames)
    encode(crops,ds,out/'previews/selected-21.webp');encode([f.crop(box) for f in old],oldmeta['reviewDurationsMs'],out/'previews/previous-8.webp')
    tw,th=crops[0].width//3,crops[0].height//3;board=Image.new('RGB',(tw*7,(th+28)*3),'white');draw=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
    for i,f in enumerate(crops):
        x,y=i%7*tw,i//7*(th+28);board.paste(f.resize((tw,th),Image.Resampling.NEAREST).convert('RGB'),(x,y+28));draw.text((x+4,y+4),f'{i+1:02} / 원본 {indices[i]+1}',fill='black',font=font)
    board.save(out/'previews/selected-21.png')
    def metrics(seq):
        adjacent=[pose_distance(a,b) for a,b in zip(seq,seq[1:])];seam=pose_distance(seq[-1],seq[0]);return {'adjacentMedianMse':float(np.median(adjacent)),'seamMse':seam,'seamVsAdjacentMedian':seam/max(float(np.median(adjacent)),1)}
    manifest={**spec,'selectedCount':len(frames),'sourceIndicesZeroBased':indices,'durationsMs':ds,'cycleMs':sum(ds),'mapping':mapping,'previewCrop':list(box),'previousMetrics':metrics(old),'currentMetrics':metrics(frames),'sourceSheetHash':oldmeta['sourceSha256'],'sourcePixelsUnchanged':True};(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');shutil.copy2(SPEC,out/'selection-spec.json')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 걷기 · 21프레임</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1200px;padding:24px;margin:auto}a{color:#815149}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}.pair>div{min-width:0;background:white;padding:12px}img{image-rendering:pixelated;max-width:100%}.board{width:100%}@media(max-width:650px){.pair{grid-template-columns:1fr}}</style><main><h1>등짐분사수 · 중간 자세를 더한21프레임</h1><p>8장에서 빠졌던 원본 중간 자세를 넣고, 끝에서 처음으로 돌아갈 때 가까운 자세가 이어지도록 구간도 다시 골랐습니다. 원본8~28번째 셀의 연속21장을 사용합니다. 장당0.05초,20fps·총1.05초는 검토용 제안 속도입니다. 원본에는 시간 정보가 없습니다.</p><p><a download href="k4-walk-smooth21-20261031.zip">21장 PNG·미리보기 ZIP</a> · <a href="manifest.json">프레임 대응·정보</a></p><div class="pair"><div><h2>이전 · 8장 / 1초</h2><img src="previews/previous-8.webp"></div><div><h2>보완 · 21장 / 1.05초</h2><img src="previews/selected-21.webp"></div></div><details><summary>선정21장과 개별 PNG</summary><img class="board" src="previews/selected-21.png">'''+''.join(f'<a href="{r["png"]}">{r["key"]:02}</a> · ' for r in mapping)+'''</details><p>흰 배경·원본 색·몸체·1280×720 PNG를 그대로 유지합니다. 새 프레임 합성이나 보간은 하지 않았습니다. 원본 자세의 변형은 남아 있을 수 있으며 아트 검토용입니다.</p></main></html>''',encoding='utf-8')
    (out/'README.md').write_text('''# 등짐분사수21프레임 보완

기존8키에서 건너뛴 중간 자세를 원본 연속21장으로 복구했다. 원본0기준7~27, 행 우선8~28번째 셀이다. selected는1280×720 PNG를 그대로 복사한다. 미리보기만 같은 공통 크롭을 적용하며 흰 배경·색·몸체를 변경하지 않는다. 50ms×21=1050ms는 검토용 제안이다. 이전8장과 새21장을 같은 크롭으로 비교한다. 원본·이전 결과는 보존한다. 다리 영역의 인접/루프 경계 MSE는 프레임 선택을 돕는 대조값이지 최종 시각 품질·게임 동작 검증이 아니다.
''',encoding='utf-8')
    with zipfile.ZipFile(out/'k4-walk-smooth21-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({'frames':len(frames),'cycleMs':sum(ds),'previous':manifest['previousMetrics'],'current':manifest['currentMetrics']}))


if __name__=='__main__':build()
