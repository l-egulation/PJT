from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageChops
import numpy as np,json,math,html
ROOT=Path(__file__).resolve().parents[1];PREV=ROOT;PACK=ROOT.parent/'stage2-unified-stone-v21'
for p in ['runtime/modules','runtime/background','runtime/foreground','preview']:(ROOT/p).mkdir(parents=True,exist_ok=True)
manifest=json.loads((PREV/'source/foreground-manifest.json').read_text(encoding='utf-8'))
DATA=ROOT.parent/'source/chapter2-layouts.json';layout=json.loads(DATA.read_text(encoding='utf-8'))['layouts'][0]
# One shared128px wall texture in all rooms, built from the exact existing stone vocabulary at quieter values.
wall=Image.new('RGB',(128,128))
for cy in range(4):
 for cx in range(4):
  im=Image.open(PACK/f'terrain/runtime/tiles/stone-FF-v{(cx*3+cy*5)%8}.png').convert('RGB');a=np.array(im).astype(np.int32)
  a=np.rint(a*.38+np.array([9,14,16])).clip(0,255).astype('uint8');wall.paste(Image.fromarray(a),(cx*32,cy*32))
wall.save(ROOT/'runtime/modules/common-wall-128.png')
# Small native architecture modules. They never include playable ledges or characters.
def pier():
 im=Image.new('RGBA',(32,64));d=ImageDraw.Draw(im)
 d.rectangle((2,0,29,63),fill=(40,56,62,255));d.rectangle((4,0,9,63),fill=(45,61,66,255));d.rectangle((26,0,29,63),fill=(33,49,56,255))
 for y in [0,16,32,48]:d.line((3,y,28,y),fill=(34,50,57,255))
 d.line((2,0,2,63),fill=(31,47,54,255));return im
p=pier();p.save(ROOT/'runtime/modules/pier-32x64.png')
arch=Image.new('RGBA',(192,160));d=ImageDraw.Draw(arch)
d.pieslice((16,0,176,160),180,360,fill=(46,62,67,255));d.rectangle((16,80,176,159),fill=(44,60,65,255))
d.pieslice((32,16,160,144),180,360,fill=(28,43,50,255));d.rectangle((32,80,160,159),fill=(28,43,50,255))
for degree in range(195,360,22):
 a=math.radians(degree);d.line((96+64*math.cos(a),80+64*math.sin(a),96+80*math.cos(a),80+80*math.sin(a)),fill=(34,50,57,255),width=1)
for y in range(80,160,16):d.line((17,y,31,y),fill=(34,50,57,255));d.line((161,y,175,y),fill=(34,50,57,255))
d.arc((17,1,175,159),185,265,fill=(49,65,69,255),width=1)
arch.save(ROOT/'runtime/modules/small-recess-192x160.png')
barred=arch.copy();d=ImageDraw.Draw(barred)
for x in [54,82,110,138]:d.line((x,59,x,159),fill=(38,54,61,255),width=3)
barred.save(ROOT/'runtime/modules/barred-recess-192x160.png')
drain=Image.new('RGBA',(64,64));d=ImageDraw.Draw(drain);d.ellipse((3,3,60,60),fill=(44,60,65,255));d.ellipse((12,12,51,51),fill=(27,42,49,255))
for x in [20,28,36,44]:d.line((x,15,x,49),fill=(36,52,59,255),width=3)
d.arc((4,4,59,59),185,280,fill=(49,65,69,255),width=1);drain.save(ROOT/'runtime/modules/drain-64.png')
pipe=Image.new('RGBA',(24,64));d=ImageDraw.Draw(pipe)
d.rectangle((8,0,17,63),fill=(34,52,58,255));d.rectangle((9,0,11,63),fill=(43,60,65,255));d.line((17,0,17,63),fill=(28,44,51,255))
d.rectangle((5,26,20,33),fill=(37,54,60,255));d.line((6,27,18,27),fill=(46,62,66,255));pipe.save(ROOT/'runtime/modules/downpipe-24x64.png')
# Explicit world-space compositions use the same wall with different room fittings.
styles=[
 {'archDx':5,'arch':'small','piers':[-6,3,10],'drainDx':-4,'pipeDx':9},
 {'archDx':-3.5,'arch':'barred','piers':[-8,1.5,9],'drainDx':6,'pipeDx':1.5},
 {'archDx':6,'arch':'small','piers':[-5,3,10.5],'drainDx':-3,'pipeDx':10.5},
 {'archDx':0,'arch':'barred','piers':[-7,2.5,10],'drainDx':7,'pipeDx':-7},
 {'archDx':-4,'arch':'small','piers':[-8,1,9],'drainDx':4,'pipeDx':1},
 {'archDx':3.5,'arch':'barred','piers':[-6,0,8],'drainDx':-4,'pipeDx':8},
 {'archDx':-5,'arch':'small','piers':[-8,-1,7],'drainDx':4,'pipeDx':-1},
 {'archDx':4,'arch':'barred','piers':[-7,1,9],'drainDx':-4,'pipeDx':-7}]
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',22);sm=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
boards=[Image.new('RGB',(1920,1210),'#182a31'),Image.new('RGB',(1920,1210),'#182a31')]
views=[];opaque_count=0
for i,v in enumerate(manifest['views']):
 px,py=v['heroFeet'];cx,cy=v['camera'];left=round(cx*32-480);top=round(cy*32+270)
 bg=Image.new('RGBA',(960,540),(32,47,54,255))
 # Global wall phase is identical across rooms, rather than one painted scene per room.
 for y in range((top%128)-128,540,128):
  for x in range((-left%128)-128,960,128):bg.alpha_composite(wall.convert('RGBA'),(x,y))
 style=styles[i];fittings=[]
 # Slender continuous pilasters split the common wall into human-scale service bays.
 for dx in style['piers']:
  x=round((px+dx)*32)-left-16
  for y in range((top%64)-64,540,64):bg.alpha_composite(p,(x,y))
  fittings.append({'type':'pier','xWorld':px+dx})
 ax=round((px+style['archDx'])*32)-left-96;ay=top-round((py+5)*32)
 bg.alpha_composite(barred if style['arch']=='barred' else arch,(ax,ay));fittings.append({'type':style['arch']+'-recess','worldCenterX':px+style['archDx'],'worldTop':py+5,'sizePx':[192,160]})
 dx=round((px+style['drainDx'])*32)-left-32;dy=top-round((py+3)*32)
 bg.alpha_composite(drain,(dx,dy))
 pipex=round((px+style['pipeDx'])*32)-left+12
 for y in range((top%64)-64,540,64):bg.alpha_composite(pipe,(pipex,y))
 bg.convert('RGB').save(ROOT/'runtime/background'/v['screen'])
 fg=Image.open(PREV/'runtime/foreground'/v['screen']).convert('RGBA');fg.save(ROOT/'runtime/foreground'/v['screen']);comp=bg.copy();comp.alpha_composite(fg)
 f=np.array(fg);c=np.array(comp);mask=f[:,:,3]==255;assert np.array_equal(c[:,:,:3][mask],f[:,:,:3][mask]);opaque_count+=int(mask.sum())
 white=Image.new('RGBA',(960,540),'white');white.alpha_composite(fg);original=Image.open(ROOT.parent/'stage2-unified-map-review-v22/preview'/v['screen']).convert('RGB');assert ImageChops.difference(white.convert('RGB'),original).getbbox() is None
 comp.convert('RGB').save(ROOT/'preview'/v['screen'])
 board=boards[i//4];dd=ImageDraw.Draw(board);j=i%4;x=j%2*960;y=60+j//2*575
 dd.text((x+16,y),v['id']+' · '+v['title'],font=font,fill='#d1dbd3');board.paste(comp.convert('RGB'),(x,y+33))
 views.append({**v,'fittings':fittings,'wall':'common-wall-128.png','style':style})
for i,board in enumerate(boards):
 dd=ImageDraw.Draw(board);dd.text((16,12),f'공통 벽 + 작은 구조물 · 구간 비교 {i+1}/2 / 960×540 / 캐릭터·타일 원본 유지',font=font,fill='#d1dbd3');board.save(ROOT/f'preview/room-backgrounds-{i+1}.png')
# Module proof on the wall, all shown at native pixel scale.
b=Image.new('RGB',(960,570),(32,47,54));dd=ImageDraw.Draw(b)
for y in range(0,570,128):
 for x in range(0,960,128):b.paste(wall,(x,y))
dd.text((24,20),'공통 벽 · 모든 방에서 같은 벽 텍스처 사용',font=font,fill='#c2d0c7')
for x,key,label in [(40,'pier-32x64.png','기둥 32px'),(160,'small-recess-192x160.png','작은 벽감 192×160px'),(440,'barred-recess-192x160.png','철창 벽감'),(760,'drain-64.png','배수구 64px')]:
 dd.text((x,82),label,font=sm,fill='#b6c6be');im=Image.open(ROOT/'runtime/modules'/key).convert('RGBA');b.paste(im,(x,120),im)
dd.text((24,340),'구조물 크기: 캐릭터 약 64px 높이 기준 / 거대한 홀 대신 작은 관리 공간',font=sm,fill='#b6c6be')
b.save(ROOT/'preview/module-vocabulary.png')
# Representative gameplay frames.
b=Image.new('RGB',(1920,1210),'#182a31');dd=ImageDraw.Draw(b);dd.text((16,12),'공통 벽을 구조물로 나눈 지하수로 / 각 화면 960×540',font=font,fill='#d1dbd3')
for j,i in enumerate([0,2,5,7]):
 v=views[i];x=j%2*960;y=60+j//2*575;dd.text((x+16,y),v['id']+' · '+v['title'],font=font,fill='#d1dbd3');b.paste(Image.open(ROOT/'preview'/v['screen']),(x,y+33))
b.save(ROOT/'preview/four-gameplay-views.png')
sections=''.join(f'<section><h2>{v["id"]} · {html.escape(v["title"])}</h2><a href="preview/{v["screen"]}"><img src="preview/{v["screen"]}" width="960" height="540"></a></section>' for v in views)
(ROOT/'review.html').write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>공통 벽과 작은 수로 공간</title><style>body{font:16px system-ui;margin:24px;background:#182a31;color:#d1dbd3}section{margin:30px 0}img{max-width:100%;height:auto;image-rendering:pixelated}a{color:#c2d0c7}</style><h1>공통 벽 + 방별 구조물</h1><p>960×540 · 32px/unit. 캐릭터와 타일은 승인본 그대로, 같은 벽을 작은 기둥·벽감·배수구로 구획한 합성본입니다. Unity 적용 전입니다.</p>'+sections+'</html>',encoding='utf-8')
(ROOT/'manifest.json').write_text(json.dumps({'wallShared':True,'wallNativeSize':[128,128],'wallTextureResampled':False,'structureModulesNative':True,'foregroundUnchanged':True,'opaqueForegroundPixelsChecked':opaque_count,'screenSize':[960,540],'pixelsPerUnit':32,'views':views,'unityModified':False},ensure_ascii=False,indent=2),encoding='utf-8')
print('Shared wall +5small structure modules,8room compositions. All original foreground pixels retained.')
