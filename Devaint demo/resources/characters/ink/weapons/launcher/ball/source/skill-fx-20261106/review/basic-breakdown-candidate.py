# 기본포 시안 v1: 탄환 그림·명중 먹물 튀김·프레임별 총구점. 원본 해상도(PPU32) 픽셀로 그린다. 미승인 후보.
from pathlib import Path
import json, math, random
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent/'candidate-v1'
(OUT/'bullet').mkdir(parents=True,exist_ok=True);(OUT/'hit').mkdir(parents=True,exist_ok=True)
INK=(27,22,22,255);INK2=(58,50,48,255);IVORY=(249,238,220,255);WARM=(240,214,160,255)

def disc(d,cx,cy,r,col):
    for y in range(-r,r+1):
        for x in range(-r,r+1):
            if x*x+y*y<=r*r+r*.6: d.point((cx+x,cy+y),fill=col)

# 탄환: 32x16, 기준점 = 탄 중심(24,8). 3장 순환 + 첫 장은 발사 직후 늘어난 모양.
def bullet(k,launch=False):
    im=Image.new('RGBA',(32,16),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=24,8
    if launch:
        for x in range(-7,6):
            h=3 if abs(x)<4 else 2 if abs(x)<6 else 1
            d.line((cx+x,cy-h,cx+x,cy+h),fill=INK)
        d.rectangle((cx-1,cy-1,cx+2,cy+1),fill=IVORY)
        return im
    # 꼬리: 끊긴 먹 줄기와 방울. 장마다 방울 위치를 한 칸씩 민다.
    for x in range(cx-14,cx-4):
        if (x+k)%5!=0: d.point((x,cy),fill=INK if x>cx-10 else INK2)
    for x in range(cx-9,cx-4):
        if (x+k)%3: d.point((x,cy-1+(k%2)),fill=INK)
    drops=[(cx-17-k,cy+(1 if k==1 else 0),1),(cx-20+k,cy-1,0)]
    for x,y,s in drops: d.rectangle((x,y,x+s,y+s),fill=INK2)
    disc(d,cx,cy,5,INK)
    # 아이보리 심지: 진행 방향 쪽으로 치우친 2~3px 덩어리.
    core=[(1,-1),(2,-1),(1,0),(2,0),(3,0),(1,1),(2,1)] if k!=1 else [(1,-1),(2,-1),(1,0),(2,0),(1,1),(2,1),(3,-1)]
    for x,y in core: d.point((cx+x,cy+y),fill=IVORY)
    d.point((cx+3,cy-2),fill=WARM)
    return im
frames=[bullet(0,True)]+[bullet(k) for k in range(3)]
for i,f in enumerate(frames): f.save(OUT/'bullet'/f'F{i:02d}.png')

# 명중: 96x96, 기준점 = 접점(24,48). 진행 방향(오른쪽) ±65도 부채꼴, 시전자 쪽 초기 속도 없음.
YEL=(235,165,22,255)
random.seed(1106)
parts=[]
N=12
for i in range(N):
    a=math.radians(-55+110*i/(N-1)+random.uniform(-6,6));v=[80,115,150,190,230][i%5]+random.uniform(-8,8)
    size=[4,4,3,3,3,3,2,2,2,2,1,1][i]
    parts.append((a,v,size,i%4==1))
random.shuffle(parts)
SEC=[0.03,0.03,0.04,0.04,0.05,0.05]
def hit(n):
    im=Image.new('RGBA',(96,96),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=24,48
    t=sum(SEC[:n])+SEC[n]*.5
    if n==0:
        for y in range(-7,8):
            w=7-abs(y);d.line((cx-max(0,w-4),cy+y,cx+w+2,cy+y),fill=YEL)
        disc(d,cx+1,cy,2,IVORY)
    elif n==1:
        for y in range(-4,5):
            w=4-abs(y)
            for x in range(-max(0,w-2),w+2):
                if (x*7+y*3)%5: d.point((cx+x,cy+y),fill=YEL)
        d.point((cx+1,cy),fill=IVORY)
    for a,v,size,streak in parts:
        if n==0:continue
        r=6+v*t;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r+160*t*t
        if streak:
            if n>3:continue
            L=4 if n<3 else 2
            d.line((round(x-math.cos(a)*L),round(y-math.sin(a)*L),round(x),round(y)),fill=YEL if n==1 else INK)
            continue
        s=size-1-(1 if n>=4 else 0)-(1 if n>=5 else 0)
        if s<0:
            if n==5:continue
            s=0
        d.rectangle((round(x),round(y),round(x)+s,round(y)+s),fill=INK)
    return im
for n in range(6): hit(n).save(OUT/'hit'/f'F{n:02d}.png')

# 총구 터짐: 80x64, 기준점 = 총구점(10,32). 발사 순간 노란 폭발 + 아이보리 심 + 앞으로 튀는 불티·먹 연기.
(OUT/'pop').mkdir(exist_ok=True)
POP_SEC=[0.025,0.03,0.035,0.04]
random.seed(61106)
sparks=[(math.radians(random.uniform(-40,40)),random.uniform(260,420),i%3==0) for i in range(9)]
puffs=[(math.radians(a),r) for a,r in [(-70,7),(-35,9),(0,10),(35,9),(70,7),(-110,5),(110,5)]]
def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            e=(x/rx)**2+(y/ry)**2
            if e<=1-rough*rnd.random(): d.point((cx+x,cy+y),fill=col)
puffs=[(-60,9,3),(-100,6,2),(60,9,3),(100,6,2),(-25,12,2),(25,12,2)]
def pop(n):
    im=Image.new('RGBA',(80,64),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=10,32
    t=sum(POP_SEC[:n])+POP_SEC[n]*.5
    # 먹 연기: 첫 장엔 없음. 둘째 장부터 총구 위아래로 덩어리가 밀려나며 작아진다.
    if n in (1,2):
        for k,(ang,r,sz) in enumerate(puffs):
            a=math.radians(ang);rr=r*(1+(n-1)*.6);s2=sz if n==1 else max(1,sz-1)
            blob(d,round(cx+5+math.cos(a)*rr),round(cy+math.sin(a)*rr),s2+1,s2,INK if n==1 else INK2,.35,k+n*10)
    if n==0:
        blob(d,cx+8,cy,10,7,YEL,.5,1);blob(d,cx+5,cy,5,3,IVORY,.3,2)
        for k in (-1,0,1): d.line((cx+13,cy+k*3,cx+21,cy+k*5),fill=YEL)
    elif n==1:
        blob(d,cx+12,cy,12,8,YEL,.7,3);blob(d,cx+11,cy,4,2,IVORY,.4,4)
    elif n==2:
        for k,(dx,dy,r) in enumerate([(15,-5,3),(22,3,2),(11,6,2),(26,-2,2),(18,1,3)]):
            blob(d,cx+dx,cy+dy,r,r-1 if r>2 else r,YEL,.3,20+k)
    for a,v,big in sparks:
        if n==3 and not big:continue
        if n==0:continue
        r=10+v*t;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r
        if x>78:continue
        L=3 if n<2 else 2
        d.line((round(x-math.cos(a)*L),round(y-math.sin(a)*L),round(x),round(y)),fill=YEL if n<3 else INK)
    return im
for n in range(4): pop(n).save(OUT/'pop'/f'F{n:02d}.png')

# 프레임별 총구점(유닛, 발 기준). 총구 끝 열 x-84, 총구 구멍 중심 y. 원본 그림에서 잰 값.
muzzle={'F046':[0.84375,0.875],'F047':[1.28125,0.875],'F048':[1.3125,0.875],'F049':[1.15625,0.875],
        'F050':[1.1875,0.84375],'F051':[0.96875,0.875],'F052':[0.84375,0.875]}
json.dump({'status':'user_approved_look_20261106_not_in_unity','bullet':{'frames':len(frames),'pivot_px':[24,8],'launch_seconds':0.03,'loop_seconds':[0.04,0.04,0.04],
           'visual_rule':'draw on barrel axis (y-0.125 from hit circle) and hide until past muzzle tip; hit circle unchanged'},
           'hit':{'pivot_px':[24,48],'seconds':SEC},'pop':{'pivot_px':[10,32],'seconds':POP_SEC},'muzzle_per_frame':muzzle},open(OUT/'candidate.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)

# 확대 확인판
sheet=Image.new('RGBA',(3*300+40,150+2*300),(255,255,255,255))
for i,f in enumerate(frames): sheet.alpha_composite(f.resize((256,128),Image.Resampling.NEAREST),(10+i*270,10))
for n in range(6):
    h=hit(n).resize((288,288),Image.Resampling.NEAREST);sheet.alpha_composite(h,(10+(n%3)*300,150+(n//3)*300))
sheet.convert('RGB').save(OUT/'zoom-check.png')
ps=Image.new('RGBA',(4*330,280),(255,255,255,255))
for n in range(4): ps.alpha_composite(pop(n).resize((320,256),Image.Resampling.NEAREST),(5+n*330,10))
ps.convert('RGB').save(OUT/'pop-check.png')
print('ok')
