# 유탄 스킬(후속타 잉크 D) 지금 연출 · 흰 배경. Unity 카탈로그·스킬 데이터를 읽어 그림만 재현(판정은 단순화).
from pathlib import Path
import json, re, math
from PIL import Image, ImageDraw, ImageFont

WT=Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/launcher-skill-parts-20261106')
RES=WT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher_grenade')
sk=json.loads((WT/'data/combat/launcher_skills.json').read_text(encoding='utf-8'))['grenade'];B=sk['bomb']
gset=next(s for s in cat['sets'] if s['form']=='launcher_grenade')
clip=next(c for c in gset['clips'] if c['name']=='attack_single');idle=next(c for c in gset['clips'] if c['name']=='idle')
IMPACT=sk['windup'];END=sk['duration']
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds'])
MX,MY=clip['muzzleX'][0],clip['muzzleY'][0]
SPAWN=(.1875+.9375/2+B['radius']+B['spawnGap'],B['spawnHeight'])   # 몸 중심+앞끝+탄 반지름+0.05
MZ=fx['muzzle'];MZ_DUR=sum(MZ['seconds']);BL=fx['blast'];BL_DUR=sum(BL['seconds'])
START=1.5;DUMMY=START+4.5;DW,DH=.8,2.0
U=64;W,H=1000,330;GROUND=300
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
cache={}
def media(n):
    if n not in cache:
        p=RES/(n+'.png');im=Image.open(p).convert('RGBA');m=p.with_suffix('.png.meta').read_text(encoding='utf-8')
        pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()))
        cache[n]=(im,pv,float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1]))
    return cache[n]
def px(ux,uy):return 10+ux*U,GROUND-uy*U
def paste(c,n,ux,uy,scale=1.0,rot=0):
    im,pv,ppu=media(n);k=U/ppu*scale
    im=im.resize((max(1,round(im.width*k)),max(1,round(im.height*k))),Image.Resampling.NEAREST)
    pvx,pvy=im.width*pv[0],im.height*(1-pv[1])
    if rot:
        big=Image.new('RGBA',(im.width*3,im.height*3),(0,0,0,0));big.alpha_composite(im,(round(im.width*1.5-pvx),round(im.height*1.5-pvy)))
        im=big.rotate(rot,resample=Image.Resampling.NEAREST,center=(im.width*1.5,im.height*1.5));pvx,pvy=im.width/2,im.height/2
    x,y=px(ux,uy);c.alpha_composite(im,(round(x-pvx),round(y-pvy)))
def pick(sec,t):
    for i,d in enumerate(sec):
        if t<d:return i
        t-=d
    return len(sec)-1
def body(a):
    if a<0 or a>=END:return idle['frames'][0]
    s=a/IMPACT*SRC_HIT if a<IMPACT else SRC_HIT+(a-IMPACT)/(END-IMPACT)*(SRC_DUR-SRC_HIT)
    return clip['frames'][pick(clip['seconds'],s)]

def touches(x,y,r):
    """적(허수아비 상자)·바닥 접촉"""
    if y-r<=0:return 'ground'
    nx=min(max(x,DUMMY),DUMMY+DW);ny=min(max(y,0),DH)
    return 'enemy' if (x-nx)**2+(y-ny)**2<=r*r else None
def fly(x0,y0,vx,vy,g,r,life,dt=.0005):
    """(경로 함수, 끝 시각, 끝 자리)"""
    t=0;x,y=x0,y0
    if touches(x,y,r):return t,(x,y)
    while t<life:
        t+=dt;x=x0+vx*t;y=y0+vy*t-.5*g*t*t
        if touches(x,y,r):return t,(x,y)
    return life,(x,y)
ra=math.radians(B['angle']);V=(B['speed']*math.cos(ra),B['speed']*math.sin(ra))
O=(START+SPAWN[0],SPAWN[1])
T_MAIN,P_MAIN=fly(*O,*V,B['gravity'],B['radius'],B['lifetime'])
FRAGS=[]
for ang in B['fragmentAngles']:
    fa=math.radians(ang);sx,sy=P_MAIN[0]+math.cos(fa)*B['fragmentSpawnOffset'],P_MAIN[1]+math.sin(fa)*B['fragmentSpawnOffset']
    fv=(B['fragmentSpeed']*math.cos(fa),B['fragmentSpeed']*math.sin(fa))
    tf,pf=fly(sx,sy,*fv,B['fragmentGravity'],B['fragmentRadius'],B['fragmentLifetime'])
    FRAGS.append(((sx,sy),fv,tf,pf))
T0=IMPACT+T_MAIN   # 본체 폭발 시각

def scene():
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,GROUND,W,GROUND),fill='#d6d6d6',width=2)
    x0,y0=px(DUMMY,0);d.rectangle((x0,y0-DH*U,x0+DW*U,y0),outline='#b9b9b9',width=3)
    return c,d
def draw(a,label=None):
    c,d=scene()
    paste(c,body(a),START,0)
    t=a-IMPACT
    if 0<=t<MZ_DUR:paste(c,MZ['frames'][pick(MZ['seconds'],t)],START+MX,MY,fx['muzzleScale'])
    if 0<=t<T_MAIN:
        x=O[0]+V[0]*t;y=O[1]+V[1]*t-.5*B['gravity']*t*t;vy=V[1]-B['gravity']*t
        paste(c,fx['projectile'],x,y,1,math.degrees(math.atan2(vy,V[0])))
    s=a-T0
    if 0<=s<BL_DUR:paste(c,BL['frames'][pick(BL['seconds'],s)],*P_MAIN,B['blastRadius']/1.2)
    for (sx,sy),fv,tf,pf in FRAGS:
        if 0<=s<tf:
            x=sx+fv[0]*s;y=sy+fv[1]*s-.5*B['fragmentGravity']*s*s;vy=fv[1]-B['fragmentGravity']*s
            paste(c,fx['projectile'],x,y,.5,math.degrees(math.atan2(vy,fv[0])))
        e=s-tf
        if 0<=e<BL_DUR:paste(c,BL['frames'][pick(BL['seconds'],e)],*pf,B['fragmentBlastRadius']/1.2)
    if label:d.text((14,10),label,font=LBL,fill='#888888')
    d.rectangle((0,H-5,int(W*min(1,max(0,a)/(T0+.9))),H),fill='#e0a43a')
    return c
if __name__=='__main__':
    for name,step in (('grenade-now-slow.gif',.005),('grenade-now-real.gif',.02)):
        fr=[];t=-.08
        while t<T0+.95:fr.append(draw(t,'지금').convert('RGB'));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    print('main hit t',round(T_MAIN,3),'at',[round(v,2) for v in P_MAIN],'explode at',round(T0,3))
    for (sx,sy),fv,tf,pf in FRAGS:print(' frag start',(round(sx,2),round(sy,2)),'end t',round(tf,3),'at',[round(v,2) for v in pf],touches(*pf,B['fragmentRadius']))
