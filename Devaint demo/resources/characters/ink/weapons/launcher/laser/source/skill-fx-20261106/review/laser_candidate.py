# 레이저포 시안 v1: 총구 터짐·총구 유지광·빔 머리·명중 불꽃. 원본 해상도(PPU32) 픽셀. 미승인 후보.
# 빔 그림(승인 B)은 그대로 쓰고, 나타남·꺼짐은 길이·두께 변화와 조각 흩어짐으로 만든다.
from pathlib import Path
import json, math, random
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent/'candidate-v1'
WHITE=(255,255,255,255);LB=(73,191,255,255);BL=(0,135,255,255);DB=(14,74,186,255);INK=(27,22,22,255)
for k in ('pop','hold','head','hit'):(OUT/k).mkdir(parents=True,exist_ok=True)

def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            if (x/rx)**2+(y/ry)**2<=1-rough*rnd.random():d.point((cx+x,cy+y),fill=col)
def ray(d,x,y,a,L,col):d.line((round(x-math.cos(a)*L),round(y-math.sin(a)*L),round(x),round(y)),fill=col)

# 총구 터짐: 72x64, 기준점(8,32). 흰 심 → 하늘색 덩어리 → 파란 조각 → 불티.
POP=[0.02,0.03,0.035,0.04]
rs=random.Random(7);sp=[(math.radians(rs.uniform(-50,50)),rs.uniform(240,380)) for _ in range(9)]
def pop(n):
    im=Image.new('RGBA',(72,64),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=8,32;t=sum(POP[:n])+POP[n]*.5
    if n==0:
        blob(d,cx+6,cy,9,9,BL,.45,1);blob(d,cx+6,cy,6,6,LB,.3,2);blob(d,cx+6,cy,3,3,WHITE)
        for k in (-1,0,1):d.line((cx+14,cy+k*4,cx+22,cy+k*6),fill=LB)
    elif n==1:
        blob(d,cx+9,cy,12,10,BL,.7,3);blob(d,cx+9,cy,7,5,LB,.5,4);blob(d,cx+9,cy,2,2,WHITE)
    elif n==2:
        for k,(dx,dy,r) in enumerate([(12,-8,3),(20,6,2),(8,9,2),(24,-3,2),(16,2,3),(5,-6,2)]):
            blob(d,cx+dx,cy+dy,r,r,BL if k%2 else DB,.3,10+k)
    for a,v in sp:
        if n==0:continue
        if n==3 and v<300:continue
        r=10+v*t;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r
        if x<70:ray(d,x,y,a,3 if n<2 else 2,LB if n<2 else DB)
    return im
for n in range(4):pop(n).save(OUT/'pop'/f'F{n:02d}.png')

# 총구 유지광: 쏘는 동안 총구에 붙어 깜빡이는 작은 빛. 24x24, 기준점(4,12). 2장 0.05초 순환.
def hold(n):
    im=Image.new('RGBA',(24,24),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=4,12
    blob(d,cx+3,cy,5 if n==0 else 4,5 if n==0 else 4,BL,.4,30+n);blob(d,cx+3,cy,3,3,LB,.2,32+n);blob(d,cx+3,cy,1,1,WHITE)
    for dx,dy in ([(9,-4),(10,3)] if n==0 else [(8,5),(11,-2)]):d.point((cx+dx,cy+dy),fill=LB)
    return im
for n in range(2):hold(n).save(OUT/'hold'/f'F{n:02d}.png')

# 빔 머리: 빔이 뻗는 동안 앞끝. 32x32, 기준점(16,16). 2장.
def head(n):
    im=Image.new('RGBA',(32,32),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=16,16
    blob(d,cx,cy,7,5 if n==0 else 6,BL,.5,40+n);blob(d,cx,cy,4,3,LB,.3,42+n);blob(d,cx+1,cy,2,1,WHITE)
    for a in ((-30,30) if n==0 else (-45,0,45)):
        aa=math.radians(a);ray(d,cx+math.cos(aa)*10,cy+math.sin(aa)*10,aa,3,LB)
    return im
for n in range(2):head(n).save(OUT/'head'/f'F{n:02d}.png')

# 명중: 96x96, 기준점(24,48). 흰 섬광 → 파란 조각이 진행 방향 ±60도로 튄다. 시전자 쪽 초기 속도 없음.
HIT=[0.03,0.03,0.04,0.04,0.05,0.05]
rh=random.Random(1106);parts=[]
for i in range(12):
    a=math.radians(-60+120*i/11+rh.uniform(-6,6));v=[100,145,190,235,280][i%5]+rh.uniform(-8,8)
    parts.append((a,v,[5,5,4,4,4,3,3,3,2,2,2,1][i],i%4==1,(DB,DB,BL)[i%3]))
rh.shuffle(parts)
def hit(n):
    im=Image.new('RGBA',(128,112),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=24,56;t=sum(HIT[:n])+HIT[n]*.5
    if n==0:blob(d,cx+4,cy,13,11,DB,.5,49);blob(d,cx+4,cy,10,8,BL,.4,50);blob(d,cx+4,cy,6,5,LB,.3,51);blob(d,cx+3,cy,3,2,WHITE)
    elif n==1:blob(d,cx+6,cy,9,8,BL,.65,53);blob(d,cx+6,cy,5,4,LB,.5,52);d.point((cx+6,cy),fill=WHITE)
    for a,v,size,streak,col in parts:
        if n==0:continue
        r=6+v*t;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r+160*t*t
        if streak:
            if n<=3:ray(d,x,y,a,5 if n<3 else 3,BL if n==1 else DB)
            continue
        s=size-1-(n>=4)-(n>=5)
        if s<0 and n==5:continue
        s=max(0,s);d.rectangle((round(x),round(y),round(x)+s,round(y)+s),fill=col if n<4 else DB)
    return im
for n in range(6):hit(n).save(OUT/'hit'/f'F{n:02d}.png')

json.dump({'status':'candidate_unapproved',
 'appear':{'grow_seconds':0.045,'thickness_from':0.35,'thickness_seconds':0.08,'head':{'pivot_px':[16,16],'seconds':[0.025,0.025]}},
 'pop':{'pivot_px':[8,32],'seconds':POP},'hold':{'pivot_px':[4,12],'seconds':[0.05,0.05]},
 'hit':{'pivot_px':[24,56],'seconds':HIT,'trigger':'when visual head passes enemy front (damage timing unchanged)'},
 'vanish':{'shrink_seconds':0.06,'shrink_to':0.12,'shards_spacing_units':0.3,'shard_life':[0.08,0.16],'sweep':'muzzle_to_tip'}},
 open(OUT/'candidate.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
sheet=Image.new('RGBA',(4*230+20,560),WHITE)
for n in range(4):sheet.alpha_composite(pop(n).resize((216,192),Image.Resampling.NEAREST),(10+n*230,10))
for n in range(6):sheet.alpha_composite(hit(n).resize((192,192),Image.Resampling.NEAREST),(10+(n%4)*230,210+(n//4)*0))  if n<4 else None
for n in range(2):sheet.alpha_composite(hold(n).resize((96,96),Image.Resampling.NEAREST),(10+n*110,420))
for n in range(2):sheet.alpha_composite(head(n).resize((128,128),Image.Resampling.NEAREST),(260+n*140,410))
sheet.convert('RGB').save(OUT/'zoom-check.png');print('ok')

# 빔 v2 적용본: 승인 빔 6장을 원본 보존한 채 복사해 두께 절반, 총구 쪽·끝 쪽을 가늘게 모은다.
SRC=Path(__file__).resolve().parents[2]/'unity/Assets/Deviant/Combat/Resources/EffectsApproved11/launcher_laser/beam'
(OUT/'beam').mkdir(exist_ok=True)
THICK=0.5;IN_TAPER=48;OUT_TAPER=20
def smooth(e):e=max(0.0,min(1.0,e));return e*e*(3-2*e)
for i in range(6):
    src=Image.open(SRC/f'F{i:02d}.png').convert('RGBA');w,h=src.size
    sq=src.resize((w,round(h*THICK)),Image.Resampling.BILINEAR)
    im=Image.new('RGBA',(w,h),(0,0,0,0));im.alpha_composite(sq,(0,(h-sq.height)//2))
    px=im.load();cy=h/2
    for x in range(w):
        f=min(smooth(x/IN_TAPER),smooth((w-1-x)/OUT_TAPER))
        half=1.5+f*h/2
        for y in range(h):
            r,g,b,a=px[x,y]
            if a==0:continue
            dy=abs(y+.5-cy)
            k=f**.6*(1 if dy<=half else 0)*(1-smooth((dy-half+3)/3) if dy>half-3 else 1)
            px[x,y]=(r,g,b,int(a*max(0,min(1,k))))
    im.save(OUT/'beam'/f'F{i:02d}.png')
print('beam v2')
