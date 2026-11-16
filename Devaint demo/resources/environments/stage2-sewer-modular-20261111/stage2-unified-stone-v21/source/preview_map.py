from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
TERRAIN=ROOT/'terrain'
DATA=ROOT.parent/'source/chapter2-layouts.json'
layout=json.loads(DATA.read_text(encoding='utf-8'))['layouts'][0]
OUT=ROOT/'preview/map';OUT.mkdir(parents=True,exist_ok=True)
hero=Image.open(ROOT.parent/'source/approved-player.png').convert('RGBA');hero=hero.crop(hero.getbbox())
mat={k:np.array(Image.open(TERRAIN/f'source/normalized/{k}-0.png').convert('RGBA')) for k in ['top','body','under','half']}

font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
import math
def pick(i,n,lw=False,rw=False):
 if n==1:return 'single-both-wall' if lw and rw else 'single-left-wall' if lw else 'single-right-wall' if rw else 'single'
 if i==0:return 'left-wall' if lw else 'left-free'
 if i==n-1:return 'right-wall' if rw else 'right-free'
 return ['middle-a','middle-b','middle-c'][(i-1)%3]
def assemble(width,lw=False,rw=False):
 n=math.ceil(width/32);out=Image.new('RGBA',(width,32))
 for i in range(n):
  im=Image.open(ROOT/f'runtime/full/{pick(i,n,lw,rw)}.png').convert('RGBA');remain=min(32,width-i*32)
  if remain<32:
   cut=Image.new('RGBA',(remain,32));h=remain//2;cut.alpha_composite(im.crop((0,0,h,32)));cut.alpha_composite(im.crop((32-(remain-h),0,32,32)),(h,0));im=cut
  out.alpha_composite(im,(i*32,0))
 return out

def render(cx,cy,scale=32,W=960,H=540,actor=None):
 # Work at exact native pixel scale, including quarter-unit geometry.
 pad=32;ww=W+pad*2;hh=H+pad*2
 left=round(cx*32-W/2)-pad;top=round(cy*32+H/2)+pad
 mask=Image.new('1',(ww,hh));d=ImageDraw.Draw(mask)
 for s in layout['surfaces']:
  if s['oneWay']:continue
  x=round(s['x']*32)-left;y=top-round((s['y']+s['h'])*32)
  x2=round((s['x']+s['w'])*32)-left;y2=top-round(s['y']*32)
  if x2>x and y2>y:d.rectangle((x,y,x2-1,y2-1),fill=1)
 m=np.array(mask,dtype=bool);yy,xx=np.indices(m.shape)
 out=np.zeros((hh,ww,4),dtype=np.uint8);core=np.array(Image.open(TERRAIN/'runtime/tiles/stone-FF-v0.png').convert('RGBA'))
 cores=np.stack([np.array(Image.open(TERRAIN/f'runtime/tiles/stone-FF-v{v}.png').convert('RGBA')) for v in range(8)])
 mark=(((xx+left)//32)*73856093)^(((top-yy)//32)*19349663)
 variant=(mark^(mark>>13))&7
 out[:]=cores[variant,(top-yy)%32,(xx+left)%32]
 # Distance to an actual exposed boundary. Union first removes internal rectangle seams.
 distances=[]
 for axis,sign in [(0,1),(0,-1),(1,1),(1,-1)]:
  dist=np.full(m.shape,17,dtype=np.int16)
  for n in range(1,17):
   shifted=np.roll(m,sign*n,axis=axis)
   if axis==0:shifted[:n if sign==1 else 0,:]=True
   if axis==1:shifted[:,:n if sign==1 else 0]=True
   dist[(dist==17)&~shifted]=n-1
  distances.append(dist)
 best=np.argmin(np.stack(distances),axis=0);near=np.minimum.reduce(distances)
 # Complete horizontal cap owns a junction, side courses continue below it.
 for side in [2,3,1,0]:best[distances[side]<16]=side
 # Unified brick body for ceiling, sidewall and interior. Only upward exposed edges receive coping.
 sel=m&(distances[0]<16)
 tx=(xx+left)%32;ty=distances[0]
 caps=np.stack([np.array(Image.open(TERRAIN/f'source/normalized/top-{v}.png').convert('RGBA')) for v in range(8)])
 cap_mark=(((xx+left)//32)*73856093)^(((top-yy+distances[0])//32)*19349663)
 cap_variant=(cap_mark^(cap_mark>>13))&7
 out[sel]=caps[cap_variant[sel],np.clip(ty[sel],0,15),tx[sel]]
 # Fine dark silhouette: no bright stone band around walls or ceilings.
 border=m&((distances[1]==0)|(distances[2]==0)|(distances[3]==0))
 out[border&~sel]=[55,74,81,255]
 out[border&sel]=[61,82,85,255]
 out[~m]=[0,0,0,0]
 # Shave only the three extreme pixels at each convex corner, matching the native corner sprites.
 trim=np.zeros(m.shape,dtype=bool)
 for a,b in [(0,2),(0,3),(1,2),(1,3)]:
  trim |= m & (distances[a]<2) & (distances[b]<2) & (distances[a]+distances[b]<2)
 out[trim]=[0,0,0,0]
 terrain=Image.fromarray(out)
 bg=Image.new('RGBA',(ww,hh),(255,255,255,255));bd=ImageDraw.Draw(bg)
 # Quiet placeholders to assess terrain contrast; no new background asset implied.
 bg.alpha_composite(terrain)
 for s in layout['surfaces']:
  if not s['oneWay']:continue
  length=round(s['w']*32);x=round(s['x']*32)-left;y=top-round((s['y']+s['h'])*32)
  if x>ww or x+length<0 or y>hh or y+12<0:continue
  lw=0<=x-1<ww and 0<=y+3<hh and bool(m[y+3,x-1])
  rw=0<=x+length<ww and 0<=y+3<hh and bool(m[y+3,x+length])
  deck=assemble(length,lw,rw)
  bg.alpha_composite(deck,(x,y))
 if actor:
  x=round(actor[0]*32)-left;y=top-round(actor[1]*32)
  bg.alpha_composite(hero,(x-hero.width//2,y-hero.height))
 return bg.crop((pad,pad,pad+W,pad+H)).convert('RGB')


font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
board=Image.new('RGB',(960,3*580),'white');draw=ImageDraw.Draw(board)
views=[]
for i,rid in enumerate(['F01','F02','F05']):
 room=next(r for r in layout['rooms'] if r['id']==rid)
 actors=[a for a in layout['actors'] if a['roomId']==rid]
 actor=actors[2] if rid=='F01' else actors[0]
 cx=room['x']+room['w']/2;cy=actor['y']+4.6875
 im=render(cx,cy,actor=(actor['x'],actor['y']));im.save(OUT/f'{rid}-960x540.png')
 draw.text((16,i*580+8),rid+' · 배경 제외 / 960×540 / 32px·unit',font=font,fill='#253740');board.paste(im,(0,i*580+40))
 views.append({'room':rid,'camera':[cx,cy],'heroFeet':[actor['x'],actor['y']]})
board.save(OUT/'clean-ledges-three-rooms-white.png')
(OUT/'provenance.json').write_text(json.dumps({'source':'../source/chapter2-layouts.json','sha256':hashlib.sha256(DATA.read_bytes()).hexdigest(),'layout':'current','views':views,'platformAssembly':'adopted v21 native32 unified stone modules','background':'white','integration':'offline composition; existing physics unchanged'},indent=2),encoding='utf-8')
print('Map previews saved:',OUT)
