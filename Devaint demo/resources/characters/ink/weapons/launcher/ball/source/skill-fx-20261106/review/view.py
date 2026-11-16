# 기본포 현재 연출 · 흰 배경 단순 검토판. Unity 카탈로그를 읽어 그림만 재현한다(판정 시뮬레이션 아님).
from pathlib import Path
import json, re
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher')
lset=next(s for s in cat['sets'] if s['form']=='launcher')
clip=next(c for c in lset['clips'] if c['name']=='attack_single')
idle=next(c for c in lset['clips'] if c['name']=='idle')
D=json.loads((ROOT/'data/combat/ranged_basic_design.json').read_text(encoding='utf-8'))['launcher']
IMPACT=D['windup'];END=D['base_motion_duration'];SPEED=D['speed'];RADIUS=D['hit_shape']['radius'];SPAWN=D['spawn_center']['right']
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds']);MUZ=fx['muzzle'];MUZ_DUR=sum(MUZ['seconds'])
MX,MY=clip['muzzleX'][0],clip['muzzleY'][0]
TARGET_X=5.0
HIT_AT=IMPACT+(TARGET_X-RADIUS-SPAWN[0])/SPEED

U=96; W,H=720,300; FEET=(110,262)
BIG=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',28)
cache={}
def media(n):
    if n not in cache:
        p=RES/(n+'.png');im=Image.open(p).convert('RGBA');m=p.with_suffix('.png.meta').read_text(encoding='utf-8')
        pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()))
        ppu=float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1]);cache[n]=(im,pv,ppu)
    return cache[n]
def paste(c,n,ux,uy,alpha=1.0):
    im,pv,ppu=media(n);k=U/ppu;im=im.resize((round(im.width*k),round(im.height*k)),Image.Resampling.NEAREST)
    if alpha<1: im.putalpha(im.getchannel('A').point(lambda v:int(v*alpha)))
    x,y=FEET[0]+ux*U,FEET[1]-uy*U
    c.alpha_composite(im,(round(x-im.width*pv[0]),round(y-im.height*(1-pv[1]))))
def px(ux,uy):return FEET[0]+ux*U,FEET[1]-uy*U
def pick(sec,t):
    for i,d in enumerate(sec):
        if t<d:return i
        t-=d
    return len(sec)-1
def body(a):
    if a<0 or a>=END:return idle['frames'][0]
    s=a/IMPACT*SRC_HIT if a<IMPACT else SRC_HIT+(a-IMPACT)/(END-IMPACT)*(SRC_DUR-SRC_HIT)
    return clip['frames'][pick(clip['seconds'],s)]
def phase(a):
    if a<0 or a>=END:return ''
    return '준비' if a<IMPACT else '발사' if a<IMPACT+.04 else '회수'

def draw(a,layers):
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,FEET[1],W,FEET[1]),fill='#d6d6d6',width=2)
    paste(c,body(a),0,0,1 if 'body' in layers else .18)
    if 'bullet' in layers:
        x0,y0=px(TARGET_X,0)
        hit=HIT_AT<=a<HIT_AT+.06
        d.rectangle((x0,y0-2*U,x0+.8*U,y0),outline='#b9b9b9',width=3,fill='#ffd76a' if hit else None)
        if IMPACT<=a:
            bx=SPAWN[0]+SPEED*(a-IMPACT)
            if bx+RADIUS<TARGET_X:
                cx,cy=px(bx,SPAWN[1]);r=RADIUS*U;d.ellipse((cx-r,cy-r,cx+r,cy+r),fill=(27,22,22,255))
    if 'muzzle' in layers and 0<=a-IMPACT<MUZ_DUR:
        paste(c,MUZ['frames'][pick(MUZ['seconds'],a-IMPACT)],MX,MY)
    p=phase(a)
    if p:d.text((W-110,18),p,font=BIG,fill='#e0482f' if p=='발사' else '#333333')
    d.rectangle((0,H-6,int(W*min(1,max(0,a)/END)),H),fill='#e0a43a')
    return c.convert('RGB')
def gif(name,layers,step):
    fr=[];t=-.12
    while t<.6:fr.append(draw(t,layers));t+=step
    fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
ALL={'body','muzzle','bullet'}
if __name__=='__main__':
  gif('w-all-slow.gif',ALL,.005)
  gif('w-all-real.gif',ALL,.02)
  gif('w-body.gif',{'body'},.005)
  gif('w-muzzle.gif',{'muzzle'},.005)
  gif('w-bullet.gif',{'bullet'},.005)
  print('ok')
