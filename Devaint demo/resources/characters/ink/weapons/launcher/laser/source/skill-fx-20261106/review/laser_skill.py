# 레이저 스킬(범위 잉크 D) 지금 vs 시안 v2. 흰 배경. 그림만 재현(판정 시뮬레이션 아님).
# 기준: main 66f353c81 — 가로 무한 관통, 첫 고체 벽에서 멈춤, 대상당28 1회(발사 순간), 준비0.15/전체0.6.
from pathlib import Path
import json, re, math, random
from PIL import Image, ImageDraw, ImageFont

WT=Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/launcher-skill-parts-20261106')
RES=WT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
V1=OUT.parent/'launcher-laser-review-20261106'/'candidate-v1'   # 지난 시안 원본(읽기만)
C2=OUT/'candidate-v2';(C2/'wall').mkdir(parents=True,exist_ok=True)

cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher_laser')
sk=json.loads((WT/'data/combat/launcher_skills.json').read_text(encoding='utf-8'))['laser']
lset=next(s for s in cat['sets'] if s['form']=='launcher_laser')
clip=next(c for c in lset['clips'] if c['name']=='attack_single');idle=next(c for c in lset['clips'] if c['name']=='idle')
IMPACT=sk['windup'];END=sk['duration']
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds'])
RELEASE=IMPACT+(clip['sourceRelease']-SRC_HIT)/(SRC_DUR-SRC_HIT)*(END-IMPACT)
MX,MY=clip['muzzleX'][0],clip['muzzleY'][0]
BEAM=fx['beam'];BEAM_DUR=sum(BEAM['seconds'])
DUMMIES=[3.6,7.4];WALL=11.0
U=64;W,H=1000,250;FEET=(70,205)
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
WHITE=(255,255,255,255);LB=(73,191,255,255);BL=(0,135,255,255);DB=(14,74,186,255)

cache={}
def media(n):
    if n not in cache:
        p=Path(n) if Path(n).is_absolute() else RES/(n+'.png')
        im=Image.open(p).convert('RGBA')
        mp=p.with_suffix('.png.meta')
        if mp.exists():
            m=mp.read_text(encoding='utf-8')
            pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()));ppu=float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1])
        else:pv,ppu=(0.0,0.5),32.0
        cache[n]=(im,pv,ppu)
    return cache[n]
def px(ux,uy):return FEET[0]+ux*U,FEET[1]-uy*U
def pick(sec,t):
    for i,d in enumerate(sec):
        if t<d:return i
        t-=d
    return len(sec)-1
def paste(c,n,ux,uy,alpha=1.0,length=None,thick=1.0):
    im,pv,ppu=media(n);k=U/ppu
    w=round(im.width*k) if length is None else round(length*U)
    im=im.resize((max(1,w),max(1,round(im.height*k*thick))),Image.Resampling.NEAREST if length is None else Image.Resampling.BILINEAR)
    if alpha<1:im.putalpha(im.getchannel('A').point(lambda v:int(v*alpha)))
    x,y=px(ux,uy);c.alpha_composite(im,(round(x-im.width*pv[0]),round(y-im.height*(1-pv[1]))))
def put(c,im,ux,uy,pivot):
    k=U/32;im=im.resize((round(im.width*k),round(im.height*k)),Image.Resampling.NEAREST)
    x,y=px(ux,uy);c.alpha_composite(im,(round(x-pivot[0]*k),round(y-pivot[1]*k)))
def body(a):
    if a<0 or a>=END:return idle['frames'][0]
    s=a/IMPACT*SRC_HIT if a<IMPACT else SRC_HIT+(a-IMPACT)/(END-IMPACT)*(SRC_DUR-SRC_HIT)
    return clip['frames'][pick(clip['seconds'],s)]

def stage(a):
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,FEET[1],W,FEET[1]),fill='#d6d6d6',width=2)
    paste(c,body(a),0,0)
    for x in DUMMIES:
        x0,y0=px(x,0);d.rectangle((x0,y0-2*U,x0+.8*U,y0),outline='#b9b9b9',width=3)
    x0,y0=px(WALL,0);d.rectangle((x0,0,x0+.7*U,y0),fill='#e4e4e4',outline='#bdbdbd',width=2)
    return c,d
LEN=WALL-MX

def now(a):
    c,d=stage(a)
    if IMPACT<=a<RELEASE:
        ph=(a-IMPACT)%BEAM_DUR;i=pick(BEAM['seconds'],ph);b=sum(BEAM['seconds'][:i]);bl=(ph-b)/BEAM['seconds'][i]
        paste(c,BEAM['frames'][i],MX,MY,1-bl,LEN);paste(c,BEAM['frames'][(i+1)%len(BEAM['frames'])],MX,MY,bl,LEN)
        paste(c,fx['core'],MX,MY,1,LEN)
    return c

# ---- 시안 v2 ----
def load(k,n):return [Image.open(V1/k/f'F{i:02d}.png').convert('RGBA') for i in range(n)]
POP,HOLD,HEAD,HIT=load('pop',4),load('hold',2),load('head',2),load('hit',6)
cand=json.loads((V1/'candidate.json').read_text(encoding='utf-8'))
BEAM2=[str(V1/'beam'/f'F{i:02d}.png') for i in range(6)]
GROW=0.05;TH_FROM=0.35;TH_SEC=0.08;SHR=0.06;SHR_TO=0.12

def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0,clip_x=None):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            if clip_x is not None and cx+x>clip_x:continue
            if (x/rx)**2+(y/ry)**2<=1-rough*rnd.random():d.point((cx+x,cy+y),fill=col)
# 벽 닿음: 40x48, 기준점(30,24) = 벽면. 벽 앞으로 납작하게 퍼지는 빛 + 뒤로 튀는 불티.
# 0~2: 닿는 순간 3장, 3~4: 쏘는 동안 2장 순환.
def wall(n):
    im=Image.new('RGBA',(40,48),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=30,24
    if n<3:
        r=[(6,13),(5,16),(4,17)][n]
        blob(d,cx,cy,r[0],r[1],DB if n<2 else BL,.45,60+n,cx);blob(d,cx,cy,r[0]-2,r[1]-5,BL if n<2 else LB,.4,63+n,cx)
        if n<2:blob(d,cx,cy,2,4,WHITE,0,0,cx)
        rs=random.Random(70)
        for k in range(7):
            ang=math.radians(180+rs.uniform(-70,70));L=(8+n*7)+rs.uniform(-2,2)
            x=cx+math.cos(ang)*L;y=cy+math.sin(ang)*L+n*n
            if n<2:d.line((round(x),round(y),round(x-math.cos(ang)*3),round(y-math.sin(ang)*3)),fill=LB)
            else:d.point((round(x),round(y)),fill=DB)
    else:
        m=n-3
        blob(d,cx,cy,4,8 if m==0 else 7,BL,.45,80+m,cx);blob(d,cx,cy,2,4,LB,.3,82+m,cx);d.point((cx,cy),fill=WHITE)
        for dx,dy in ([(-7,-6),(-9,5)] if m==0 else [(-8,-3),(-6,7)]):d.point((cx+dx,cy+dy),fill=LB)
    return im
WALLF=[wall(i) for i in range(5)]
for i,im in enumerate(WALLF):im.save(C2/'wall'/f'F{i:02d}.png')
WALL_HIT=[0.03,0.04,0.05];WALL_HOLD=0.05;WALL_PV=(30,24)

def beam(c,length,thick,alpha,t):
    if length<=0.02:return
    ph=t%BEAM_DUR;i=pick(BEAM['seconds'],ph);b=sum(BEAM['seconds'][:i]);bl=(ph-b)/BEAM['seconds'][i]
    for n,a in ((BEAM2[i],(1-bl)*alpha),(BEAM2[(i+1)%6],bl*alpha)):paste(c,n,MX,MY,a,length,thick)
    x,y=px(MX,MY);ImageDraw.Draw(c).rectangle((x,y-2,x+length*U,y+1),fill=(255,255,255,int(255*alpha)))
rs=random.Random(3);SHARDS=[]
n=int(LEN/0.3)
for i in range(n):
    SHARDS.append((MX+(i+.5)*0.3+rs.uniform(-.1,.1),SHR*i/n+rs.uniform(0,.02),rs.uniform(.08,.16),rs.uniform(-1.6,1.6),rs.choice([(255,255,255),(73,191,255),(0,135,255)])))

def cand_(a):
    c,d=stage(a);t=a-IMPACT
    if 0<=t<RELEASE-IMPACT:
        L=LEN*min(1,t/GROW);th=TH_FROM+(1-TH_FROM)*min(1,t/TH_SEC)
        beam(c,L,th,1,t)
        if t<GROW:put(c,HEAD[int(t/.025)%2],MX+L,MY,cand['appear']['head']['pivot_px'])
        if t>=.06:put(c,HOLD[int(t/.05)%2],MX,MY,cand['hold']['pivot_px'])
        w=t-GROW
        if w>=0:
            if w<sum(WALL_HIT):put(c,WALLF[pick(WALL_HIT,w)],WALL,MY,WALL_PV)
            else:put(c,WALLF[3+int(w/WALL_HOLD)%2],WALL,MY,WALL_PV)
    va=a-RELEASE
    if 0<=va<SHR:
        f=va/SHR;beam(c,LEN,1-(1-SHR_TO)*f,1-.5*f,va+RELEASE-IMPACT)
    for x,start,life,dy,col in SHARDS:
        s=va-start
        if 0<=s<life:
            k=s/life;X,Y=px(x,MY+dy*.12*k);w=max(1,round(3*(1-k)*2));d.rectangle((X-w,Y-1,X+w,Y+1),fill=col+(255,))
    if 0<=t<sum(cand['pop']['seconds']):put(c,POP[pick(cand['pop']['seconds'],t)],MX,MY,cand['pop']['pivot_px'])
    for x in DUMMIES:
        h=t-GROW*(x-MX)/LEN
        if 0<=h<sum(cand['hit']['seconds']):put(c,HIT[pick(cand['hit']['seconds'],h)],x,MY,cand['hit']['pivot_px'])
    return c

def pair(a):
    out=Image.new('RGB',(W,H*2+12),'#ffffff')
    for k,(img,name) in enumerate([(now(a),'지금'),(cand_(a),'시안')]):
        d=ImageDraw.Draw(img);d.text((14,10),name,font=LBL,fill='#888888' if k==0 else '#e0482f')
        d.rectangle((0,H-5,int(W*min(1,max(0,a)/END)),H),fill='#e0a43a')
        out.paste(img.convert('RGB'),(0,k*(H+12)))
    ImageDraw.Draw(out).line((0,H+6,W,H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    for name,step in (('laser-skill-slow.gif',.005),('laser-skill-real.gif',.02)):
        fr=[];t=-.08
        while t<.8:fr.append(pair(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    rows=[('뻗는 순간',IMPACT+.03),('쏘는 중',IMPACT+.2),('꺼질 때',RELEASE+.05)]
    F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22);ch=H-40
    sheet=Image.new('RGB',(W,len(rows)*(ch*2+60)),'#ffffff');d=ImageDraw.Draw(sheet);y=0
    for label,a in rows:
        d.text((10,y+6),label,font=F,fill='#222222')
        sheet.paste(now(a).convert('RGB').crop((0,35,W,H-5)),(0,y+38));sheet.paste(cand_(a).convert('RGB').crop((0,35,W,H-5)),(0,y+42+ch))
        d.text((W-70,y+42),'지금',font=F,fill='#888888');d.text((W-70,y+46+ch),'시안',font=F,fill='#e0482f');y+=ch*2+60
    sheet.save(OUT/'laser-skill-three.png')
    z=Image.new('RGBA',(5*130+10,170),WHITE)
    for i,im in enumerate(WALLF):z.alpha_composite(im.resize((120,144),Image.Resampling.NEAREST),(10+i*130,10))
    z.convert('RGB').save(OUT/'wall-zoom.png')
    print('impact',IMPACT,'release',round(RELEASE,4),'end',END,'len',LEN)
