from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from io import BytesIO
from zipfile import ZipFile, ZIP_DEFLATED
import json, hashlib, base64, subprocess
import numpy as np
P=Path(__file__).resolve().parent
ROOT=P.parents[1]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='feat/story-unity-essentials'
C=P/'background-cleanup-20261111'; C.mkdir(exist_ok=True)
first=[1,3,5,6,7,9,12,13,16,18]; second=[19,20,21,22,24,27,28,29,30]
ms1=[40,40,40,30,40,30,40,40,30,30]; ms2=[70,50,30,30,20,20,35,35,30]
files=[]
for i in first+second:
 src=P/'foot-aligned'/f'frame-{i:02d}.png'
 dest=C/'original'/src.name; dest.parent.mkdir(exist_ok=True)
 dest.write_bytes(src.read_bytes())
 files.append(dict(file=src.name,size=[640,360],sha256=hashlib.sha256(src.read_bytes()).hexdigest(),sourceFrame=i))
manifest=dict(files=files,firstFrames=first,firstMs=ms1,secondFrames=second,secondMs=ms2,cycleMs=680,alignment='unchanged from approved foot-aligned images')
(C/'source-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
# Reuse the previously accepted native-resolution matte/color pipeline without
# running that batch's source reads, exports, or preview mutations.
source=(ROOT/'outputs/launcher-skill-motion-20261110/background-cleanup-20261110/remove_background.py').read_text(encoding='utf-8')
source=source[:source.index('\nrows=[]')]
source=source.replace('P=Path(__file__).resolve().parent;ROOT=P.parents[2]', 'P=TASK_CLEAN;ROOT=TASK_ROOT')
scope=dict(TASK_CLEAN=C,TASK_ROOT=ROOT,__file__=__file__)
exec(compile(source,'accepted_native_cleanup','exec'),scope)
rows=[scope['process'](row) for row in files]
# Neutral gray motion arcs are authored effects, not ink-colored weapons.
# Recover their white matte separately, keeping the character's dark pigment.
from scipy import ndimage as ndi
for row in rows:
 raw=np.array(Image.open(C/'original'/row['file']).convert('RGB'))
 out=np.array(Image.open(row['output']).convert('RGBA'))
 a=raw.astype(float); chroma=np.ptp(a,axis=-1); gray=a.mean(-1)
 painted=(chroma>18)&(a.min(-1)<220)
 dark=a.max(-1)<85
 distance=ndi.distance_transform_edt(~painted)
 dd=ndi.distance_transform_edt(~dark)
 fx=(chroma<9)&(gray>100)&(gray<249)&(distance>3)&(dd>2.3)
 if fx.any():
  pigment=float(np.floor(gray[fx].min()))
  out[fx,:3]=round(pigment)
  out[fx,3]=np.rint(np.clip((255-gray[fx])/(255-pigment),0,1)*255).astype(np.uint8)
  Image.fromarray(out).save(row['output'])
 row['outputSha256']=hashlib.sha256(Path(row['output']).read_bytes()).hexdigest()
 row['neutralEffectPixelsPreserved']=int(fx.sum())
(C/'validation.json').write_text(json.dumps(dict(frames=19,size=[640,360],cycleMs=680,spatialResampling=False,paletteQuantization=False,darkPaintPreserved=True,alignmentUnchanged=True,files=rows),indent=2),encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
box=(170,125,455,355)
for color,label in [('#20262d','dark'),('#ffffff','white')]:
 board=Image.new('RGB',(1500,5*310),color); draw=ImageDraw.Draw(board)
 for j,i in enumerate(first+second):
  im=Image.open(C/'transparent'/f'frame-{i:02d}.png').crop(box).resize((285,230),Image.Resampling.NEAREST)
  bg=Image.new('RGBA',im.size,color);bg.alpha_composite(im)
  x=j%4*375;y=j//4*310
  board.paste(bg.convert('RGB'),(x+35,y+40));draw.text((x+35,y+8),f'#{i:02d}',font=font,fill='white' if label=='dark' else 'black')
 board.save(C/f'inspection-{label}.png')
def uri(im):
 b=BytesIO();im.save(b,format='PNG');return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()
groups=[]
for name,ids,times in [('1타',first,ms1),('2타',second,ms2),('1타 → 2타',first+second,ms1+ms2)]:
 frames=[]
 for i,t in zip(ids,times):
  frames.append(dict(id=i,ms=t,url=uri(Image.open(C/'transparent'/f'frame-{i:02d}.png').crop(box))))
 groups.append(dict(name=name,frames=frames))
(C/'preview-data.json').write_text(json.dumps(groups,ensure_ascii=False),encoding='utf-8')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>맨손 19장 · 배경·외곽·색 보정</title><style>body{font:16px/1.6 system-ui;background:#161c22;color:#eee;margin:0;padding:24px}h1{font-size:25px}button,select{font:inherit;padding:9px 14px;margin:5px;border-radius:8px;border:1px solid #66717b}#grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:16px}section{border:1px solid #53606c;border-radius:12px;padding:16px}.stage{height:350px;display:flex;align-items:center;justify-content:center;background:#20262d}.stage img{width:100%;max-width:420px;max-height:100%;object-fit:contain}.info{font-variant-numeric:tabular-nums;color:#bdc9d1}a{color:#a4e6cf}</style><h1>맨손 1·2타 · 배경 / 외곽 / 색 보정</h1><p>19장 · 원본 640×360 유지 · 픽셀화 없음 · 발 정렬 유지 · 1타 0.36초 + 2타 0.32초</p><button id="play">일시정지</button><button id="step">한 장씩</button><label>배경 <select id="bg"><option value="#20262d">어두운 배경</option><option value="#000000">검정</option><option value="#ffffff">흰색</option><option value="checker">체커</option></select></label><label>재생 <select id="speed"><option value="1">실제 속도</option><option value="0.5">0.5배</option></select></label><p>반복 사이 0.6초 대기 · <a href="unarmed-play.html?v=clean-19">보정본으로 직접 공격 체험하기</a></p><div id="grid"></div><script>const groups=__DATA__;let playing=true,elapsed=0,last=performance.now();const grid=document.querySelector('#grid');groups.forEach(g=>{g.total=g.frames.reduce((s,f)=>s+f.ms,0);g.images=g.frames.map(f=>{let i=new Image;i.src=f.url;return i});const s=document.createElement('section');s.innerHTML=`<h2>${g.name} · ${g.frames.length}장</h2><div class="stage"><img></div><p class="info"></p>`;grid.append(s)});function frameIndex(g){let t=elapsed%(g.total+600),sum=0;for(let i=0;i<g.frames.length;i++){sum+=g.frames[i].ms;if(t<sum)return i}return 0}function draw(){[...grid.children].forEach((s,i)=>{let g=groups[i],n=frameIndex(g);s.querySelector('img').src=g.images[n].src;s.querySelector('.info').textContent=`${n+1}/${g.frames.length} · 원본 #${g.frames[n].id} · ${g.frames[n].ms}ms`})}document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시정지':'재생'};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#play').textContent='재생';let g=groups[2],n=(frameIndex(g)+1)%g.frames.length;elapsed=g.frames.slice(0,n).reduce((s,f)=>s+f.ms,0);draw()};document.querySelector('#bg').onchange=e=>document.querySelectorAll('.stage').forEach(s=>s.style.background=e.target.value==='checker'?'repeating-conic-gradient(#aaa 0% 25%,#eee 0% 50%) 0/24px 24px':e.target.value);function tick(now){if(playing&&!document.hidden)elapsed+=Math.min(100,now-last)*Number(document.querySelector('#speed').value);last=now;draw();requestAnimationFrame(tick)}draw();requestAnimationFrame(tick)</script></html>'''.replace('__DATA__',json.dumps(groups,ensure_ascii=False))
(C/'preview.html').write_text(html,encoding='utf-8')
play=ROOT/'outputs/whip-skill-review-20261106/zip-review-20261107/playback'
(play/'unarmed-cleanup-19.html').write_text(html,encoding='utf-8')
gif=[]
for i in first+second:
 im=Image.open(C/'transparent'/f'frame-{i:02d}.png').crop(box).resize((570,460),Image.Resampling.NEAREST)
 bg=Image.new('RGBA',im.size,'#20262d');bg.alpha_composite(im);gif.append(bg.convert('RGB'))
gif.append(gif[0])
gif[0].save(C/'combo-cleaned.gif',save_all=True,append_images=gif[1:],duration=ms1+ms2+[600],loop=0,disposal=2)
archive=P/'맨손_배경제거_외곽색보정_19장.zip'
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
 for j,i in enumerate(first):z.write(C/'transparent'/f'frame-{i:02d}.png',f'1타/{j+1:02d}_source-{i:02d}.png')
 for j,i in enumerate(second):z.write(C/'transparent'/f'frame-{i:02d}.png',f'2타/{j+1:02d}_source-{i:02d}.png')
 z.writestr('timing.json',json.dumps(manifest,ensure_ascii=False,indent=2))
with ZipFile(archive) as z:assert len([n for n in z.namelist() if n.endswith('.png')])==19 and z.testzip() is None
for row in rows:
 raw=np.array(Image.open(C/'original'/row['file']).convert('RGB'));out=np.array(Image.open(row['output']).convert('RGBA'))
 assert out.shape==(360,640,4) and not out[...,3][raw.min(-1)>=249].any()
 assert np.all(out[raw.max(-1)<85,3]==255)
 assert hashlib.sha256((P/'foot-aligned'/row['file']).read_bytes()).hexdigest()==row['sha256']
assert sum(ms1)+sum(ms2)==680
print(json.dumps(dict(frames=len(rows),output=str(C)),ensure_ascii=False))
