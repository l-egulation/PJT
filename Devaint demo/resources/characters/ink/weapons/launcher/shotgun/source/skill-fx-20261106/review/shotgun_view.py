# 샷건 스킬(이동 잉크 D) 지금 연출 · 흰 배경. Unity 카탈로그·스킬 데이터를 읽어 그림만 재현(판정 시뮬레이션 아님).
from pathlib import Path
import json, re, math
from PIL import Image, ImageDraw, ImageFont

WT=Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/launcher-skill-parts-20261106')
RES=WT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher_shotgun')
sk=json.loads((WT/'data/combat/launcher_skills.json').read_text(encoding='utf-8'))['shotgun'];R=sk['ranged']
sset=next(s for s in cat['sets'] if s['form']=='launcher_shotgun')
clip=next(c for c in sset['clips'] if c['name']=='attack_single');idle=next(c for c in sset['clips'] if c['name']=='idle')
IMPACT=sk['windup'];END=sk['duration']
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds'])
MX,MY=clip['muzzleX'][0],clip['muzzleY'][0]
SPAWN_X=.1875+.9375/2+R['radius']+R['spawnGap'];SPAWN_Y=R['spawnHeight']   # 몸 앞끝 + 탄 반지름 + 0.05
RD,RT,HOP=R['recoilDistance'],R['recoilDuration'],R['recoilHopHeight']
MZ=fx['muzzle'];MZ_DUR=sum(MZ['seconds'])
START=7.6;TARGET=START+4.0
U=56;W,H=1000,300;GROUND=255
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
cache={}
def media(n):
    if n not in cache:
        p=RES/(n+'.png');im=Image.open(p).convert('RGBA');m=p.with_suffix('.png.meta').read_text(encoding='utf-8')
        pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()))
        cache[n]=(im,pv,float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1]))
    return cache[n]
def px(ux,uy):return 10+ux*U,GROUND-uy*U
def paste(c,n,ux,uy,scale=1.0):
    im,pv,ppu=media(n);k=U/ppu*scale
    im=im.resize((max(1,round(im.width*k)),max(1,round(im.height*k))),Image.Resampling.NEAREST)
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
def player_pos(a):
    r=a-IMPACT
    if r<=0:return START,0.0
    k=min(1,r/RT);return START-RD*k,4*HOP*k*(1-k)
def scene(a):
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,GROUND,W,GROUND),fill='#d6d6d6',width=2)
    x0,y0=px(TARGET,0);d.rectangle((x0,y0-2*U,x0+.8*U,y0),outline='#b9b9b9',width=3)
    return c,d
def draw(a,label=None):
    c,d=scene(a);bx,by=player_pos(a)
    paste(c,body(a),bx,by)
    t=a-IMPACT
    if 0<=t<MZ_DUR:paste(c,MZ['frames'][pick(MZ['seconds'],t)],bx+MX,by+MY,fx.get('muzzleScale',1))
    if t>=0:
        ox,oy=START+SPAWN_X,SPAWN_Y   # 발사 순간 자리에서 나간다
        for ang in R['angles']:
            dist=min(R['distance'],R['speed']*t)
            if R['speed']*t>R['distance']:continue
            ra=math.radians(ang);wx,wy=ox+math.cos(ra)*dist,oy+math.sin(ra)*dist
            if 0<=wy<=2 and math.cos(ra)*R['speed']*t+ox>=TARGET-R['radius']:continue   # 허수아비에 맞으면 사라짐
            if wy<R['radius']:continue   # 바닥(고체)에 닿으면 사라짐
            x,y=px(wx,wy);r=R['radius']*U
            d.ellipse((x-r,y-r,x+r,y+r),fill=(27,22,22,255))
    if label:d.text((14,10),label,font=LBL,fill='#888888')
    d.rectangle((0,H-5,int(W*min(1,max(0,a)/END)),H),fill='#e0a43a')
    return c
if __name__=='__main__':
    for name,step in (('shotgun-now-slow.gif',.005),('shotgun-now-real.gif',.02)):
        fr=[];t=-.08
        while t<.9:fr.append(draw(t,'지금').convert('RGB'));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    # 쏘는 순간 확대: 총구 효과 6장 + 탄 생성점
    F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',18)
    cells=[]
    for i in range(len(MZ['frames'])):
        a=IMPACT+sum(MZ['seconds'][:i])+MZ['seconds'][i]*.5
        im=draw(a).crop((int(10+(START-4.8)*U),40,int(10+(START+3.2)*U),GROUND+8)).convert('RGB')
        im=im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST)
        ImageDraw.Draw(im).text((8,6),f'{a-IMPACT:.3f}초',font=F,fill='#e0482f');cells.append(im)
    cw,ch=cells[0].size;sheet=Image.new('RGB',(cw*3+20,ch*2+10),'#ffffff')
    for i,im in enumerate(cells):sheet.paste(im,((i%3)*(cw+10),(i//3)*(ch+10)))
    sheet.save(OUT/'shotgun-now-muzzle.png')
    print('spawn',SPAWN_X,SPAWN_Y,'muzzle',MX,MY,'muzzle dur',MZ_DUR,'flight',R['distance']/R['speed'],'recoil',RT)
