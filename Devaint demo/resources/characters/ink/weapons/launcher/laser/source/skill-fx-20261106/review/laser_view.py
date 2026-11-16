# 레이저포 현재 연출 · 흰 배경. Unity 카탈로그를 읽어 그림만 재현한다(판정 시뮬레이션 아님).
from pathlib import Path
import json, re
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher_laser')
conv=json.loads((ROOT/'data/combat/launcher_attack_conversions.json').read_text(encoding='utf-8'))['laser']
lset=next(s for s in cat['sets'] if s['form']=='launcher_laser')
clip=next(c for c in lset['clips'] if c['name']=='attack_single')
idle=next(c for c in lset['clips'] if c['name']=='idle')
IMPACT=conv['windup'];END=conv['duration'];DIST=conv['ranged']['distance']
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds'])
RELEASE=IMPACT+(clip['sourceRelease']-SRC_HIT)/(SRC_DUR-SRC_HIT)*(END-IMPACT)
MX,MY=clip['muzzleX'][0],clip['muzzleY'][0]
BEAM=fx['beam'];BEAM_DUR=sum(BEAM['seconds'])
TARGET_X=5.0
U=64;W,H=720,240;FEET=(90,200)
BIG=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',24)
cache={}
def media(n):
    if n not in cache:
        p=RES/(n+'.png');im=Image.open(p).convert('RGBA');m=p.with_suffix('.png.meta').read_text(encoding='utf-8')
        pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()))
        cache[n]=(im,pv,float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1]))
    return cache[n]
def px(ux,uy):return FEET[0]+ux*U,FEET[1]-uy*U
def paste(c,n,ux,uy,alpha=1.0,length=None):
    im,pv,ppu=media(n);k=U/ppu
    w=round(im.width*k) if length is None else round(length*U)
    im=im.resize((max(1,w),max(1,round(im.height*k))),Image.Resampling.NEAREST if length is None else Image.Resampling.BILINEAR)
    if alpha<1:im.putalpha(im.getchannel('A').point(lambda v:int(v*alpha)))
    x,y=px(ux,uy);c.alpha_composite(im,(round(x-im.width*pv[0]),round(y-im.height*(1-pv[1]))))
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
    return '준비' if a<IMPACT else '발사' if a<RELEASE else '회수'
def draw(a):
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,FEET[1],W,FEET[1]),fill='#d6d6d6',width=2)
    paste(c,body(a),0,0)
    x0,y0=px(TARGET_X,0);d.rectangle((x0,y0-2*U,x0+.8*U,y0),outline='#b9b9b9',width=3)
    if IMPACT<=a<RELEASE:
        # Unity LaserVisual: 두 장을 교차 페이드, 위에 흰 2px 심.
        length=DIST-0.05
        ph=(a-IMPACT)%BEAM_DUR;i=pick(BEAM['seconds'],ph);b=sum(BEAM['seconds'][:i]);blend=(ph-b)/BEAM['seconds'][i]
        paste(c,BEAM['frames'][i],MX,MY,1-blend,length);paste(c,BEAM['frames'][(i+1)%len(BEAM['frames'])],MX,MY,blend,length)
        paste(c,fx['core'],MX,MY,1,length)
    p=phase(a)
    if p:d.text((W-90,12),p,font=BIG,fill='#e0482f' if p=='발사' else '#333333')
    d.rectangle((0,H-6,int(W*min(1,max(0,a)/END)),H),fill='#e0a43a')
    return c
if __name__=='__main__':
    for name,step in (('laser-now-slow.gif',.005),('laser-now-real.gif',.02)):
        fr=[];t=-.1
        while t<.75:fr.append(draw(t).convert('RGB'));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    print('release',RELEASE)
