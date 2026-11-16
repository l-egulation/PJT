# 샷건 시안 v1 (2026-11-06 사용자 확정, Unity 미적용): 산탄·총구 터짐·반동 먼지와 바람 자국·명중. 원본 해상도(PPU32) 픽셀. 미승인 후보.
# 판정·수치·생성점·반동 거리 불변. 몸 동작은 그대로. 색은 확정 기본포와 같은 노랑·먹·아이보리.
from pathlib import Path
import json, math, random
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent/'candidate-v1'
INK=(27,22,22,255);INK2=(58,50,48,255);IVORY=(249,238,220,255);YEL=(235,165,22,255)
DUST=(196,186,170,255);DUST2=(226,218,204,255)
for k in ('pellet','pop','dust','wind','hit'):(OUT/k).mkdir(parents=True,exist_ok=True)

def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            if (x/max(rx,.5))**2+(y/max(ry,.5))**2<=1-rough*rnd.random():d.point((cx+x,cy+y),fill=col)

# 산탄: 24x8, 기준점(18,4)=탄 중심. 6px 먹 탄 + 아이보리 2px + 2줄 끊긴 꼬리. 2장 꼬리 깜빡임.
def pellet(n):
    im=Image.new('RGBA',(24,8),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=18,4
    for y in range(cy-3,cy+3):
        for x in range(cx-3,cx+3):
            if (y in (cy-3,cy+2)) and (x in (cx-3,cx+2)):continue
            d.point((x,y),fill=INK)
    d.point((cx,cy-2),fill=IVORY);d.point((cx+1,cy-1),fill=IVORY)
    for x in range(0,cx-3):
        if (x+n)%5!=0 and x>=(2 if n==0 else 5):
            col=INK if x>cx-11 else INK2
            d.point((x,cy-1),fill=col)
            if x>cx-14:d.point((x,cy),fill=col)
    for x in range(cx-8,cx-3):
        if (x+n)%3:d.point((x,cy-2+3*n),fill=INK)
    return im
for n in range(2):pellet(n).save(OUT/'pellet'/f'F{n:02d}.png')

# 총구 터짐: 80x64, 기준점(6,32)=총구. 노란 심 → 다섯 갈래 부채꼴 → 먹 조각 → 불티. 쏜 자리에 고정.
POP=[0.02,0.03,0.035,0.045]
ANG=[-20,-10,0,10,20]
def pop(n):
    im=Image.new('RGBA',(80,64),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=6,32
    if n==0:
        blob(d,cx+7,cy,8,8,YEL,.4,1);blob(d,cx+6,cy,4,3,IVORY,.2,2)
        for a in ANG:
            r=math.radians(a);d.line((round(cx+12*math.cos(r)),round(cy-12*math.sin(r)),round(cx+26*math.cos(r)),round(cy-26*math.sin(r))),fill=YEL)
    elif n==1:
        blob(d,cx+9,cy,10,9,YEL,.6,3);blob(d,cx+8,cy,3,2,IVORY,.3,4)
        for a in ANG:
            r=math.radians(a);x0,y0=cx+14*math.cos(r),cy-14*math.sin(r);x1,y1=cx+40*math.cos(r),cy-40*math.sin(r)
            d.line((round(x0),round(y0),round(x1),round(y1)),fill=YEL,width=2)
            d.line((round(x1-4*math.cos(r)),round(y1+4*math.sin(r)),round(x1+3*math.cos(r)),round(y1-3*math.sin(r))),fill=INK)
    elif n==2:
        for k,(dx,dy,r) in enumerate([(12,-9,3),(18,7,2),(9,10,2),(24,-4,2),(15,1,3),(6,-7,2),(28,9,1)]):
            blob(d,cx+dx,cy+dy,r,r,INK if k%2 else INK2,.3,10+k)
        blob(d,cx+8,cy,4,3,YEL,.5,5)
    else:
        rs=random.Random(9)
        for k in range(8):
            a=math.radians(rs.uniform(-35,35));L=rs.uniform(30,48)
            d.point((round(cx+L*math.cos(a)),round(cy-L*math.sin(a))),fill=INK if k%3 else YEL)
        for k,(dx,dy) in enumerate([(14,-12),(20,10),(10,12)]):blob(d,cx+dx,cy+dy,1,1,INK2)
    return im
for n in range(4):pop(n).save(OUT/'pop'/f'F{n:02d}.png')

# 반동 먼지: 64x24, 기준점(32,22)=발 밑. 출발·착지 때 바닥에서 양옆으로 낮게 퍼진다. 4장.
DUSTS=[0.04,0.05,0.06,0.07]
def dust(n):
    im=Image.new('RGBA',(64,24),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=32,21
    for side in (-1,1):
        for k,(off,r,h) in enumerate([(6,4,3),(13,3,2),(19,2,2)]):
            o=off+n*4+k*n;rr=max(1,r-(n>=2)-(n>=3));up=h+n
            if n==3 and k==0:continue
            blob(d,cx+side*o,cy-up,rr+1,rr,DUST if (k+n)%2 else DUST2,.35,side*10+k+n*3)
    if n==0:d.line((cx-10,cy,cx+10,cy),fill=DUST)
    return im
for n in range(4):dust(n).save(OUT/'dust'/f'F{n:02d}.png')

# 바람 자국: 48x40, 기준점(8,20)=총 쪽 몸 앞. 몸이 뒤로 밀리는 동안 앞쪽에 남는 먹 선 세 줄. 2장 순환.
def wind(n):
    # 총 높이(가운데)는 비우고 어깨 위 두 줄, 무릎 높이 두 줄. 총에서 나오는 연기로 안 읽히게.
    im=Image.new('RGBA',(48,56),(0,0,0,0));d=ImageDraw.Draw(im)
    rows=[(4,22,2),(11,30,0),(45,26,4),(51,18,1)] if n==0 else [(5,28,0),(12,20,4),(44,20,1),(50,28,3)]
    for y,L,s in rows:
        for x in range(8+s,8+s+L):
            if (x+n)%7!=0:d.point((x,y),fill=INK2 if x>8+s+L*.6 else INK)
    return im
for n in range(2):wind(n).save(OUT/'wind'/f'F{n:02d}.png')

# 명중: 72x72, 기준점(16,36)=탄이 닿은 자리. 큰 노란 섬광 → 노란 줄기 → 먹 조각 앞쪽 ±60도 → 잔조각. 검은 테 없음.
HIT=[0.03,0.035,0.04,0.045,0.05]
rh=random.Random(611);parts=[]
for i in range(12):
    a=math.radians(-60+120*i/11+rh.uniform(-6,6));v=[120,170,220,270][i%4]+rh.uniform(-10,10)
    parts.append((a,v,[4,3,3,2,3,2,2,3,2,2,1,2][i],i%4==2))
rh.shuffle(parts)
def hit(n):
    im=Image.new('RGBA',(72,72),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=16,36;t=sum(HIT[:n])+HIT[n]*.5
    if n==0:
        blob(d,cx+4,cy,10,10,YEL,.3,20);blob(d,cx+4,cy,5,5,IVORY,.15,21)
        for a in (-50,-25,0,25,50):
            r=math.radians(a);d.line((round(cx+4+11*math.cos(r)),round(cy+11*math.sin(r)),round(cx+4+17*math.cos(r)),round(cy+17*math.sin(r))),fill=YEL)
    elif n==1:
        blob(d,cx+6,cy,11,9,YEL,.6,22);blob(d,cx+5,cy,3,3,IVORY,.3,23)
    elif n==2:
        blob(d,cx+8,cy,6,5,YEL,.7,24)
    for a,v,s,streak in parts:
        if n==0:continue
        r=8+v*t;x=cx+math.cos(a)*r;y=cy+math.sin(a)*r+150*t*t
        if streak:
            if n<=3:d.line((round(x-math.cos(a)*(6 if n<3 else 3)),round(y-math.sin(a)*(6 if n<3 else 3)),round(x),round(y)),fill=YEL if n==1 else INK,width=2 if n<3 else 1)
            continue
        s=s-(n>=3)-(n>=4)
        if s<0:continue
        d.rectangle((round(x),round(y),round(x)+s,round(y)+s),fill=INK if n<3 else INK2)
    return im
for n in range(5):hit(n).save(OUT/'hit'/f'F{n:02d}.png')

json.dump({'status':'user_approved_look_20261106_not_in_unity',
 'pellet':{'pivot_px':[18,4],'seconds':[0.04,0.04],'rotate':'pellet angle','visual_rule':'draw on muzzle axis (hit circle y+0.11) and hide until past fire-time muzzle tip; hit circle unchanged'},
 'pop':{'pivot_px':[6,32],'seconds':POP,'anchor':'fire-time muzzle (F049 tip ~1.0625,1.109), stays in world; existing black muzzle A also stays in world instead of following'},
 'dust':{'pivot_px':[32,22],'seconds':DUSTS,'trigger':'recoil start on ground and recoil landing'},
 'wind':{'pivot_px':[8,28],'seconds':[0.05,0.05],'anchor':'body front x+0.4, y 1.05; gun height left empty; during recoil only'},
 'hit':{'pivot_px':[16,36],'seconds':HIT,'trigger':'enemy hit: frames 0-4; terrain hit: frames 2-4 only'},
 'gun_tip_measured':{'F046':[1.125,0.797],'F047':[1.3125,0.896],'F048':[1.0625,1.109],'F049':[1.0625,1.109],'F050':[1.1875,0.859],'F051':[1.125,0.891]}},
 open(OUT/'candidate.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
sheet=Image.new('RGBA',(4*250+20,600),(255,255,255,255))
for n in range(4):sheet.alpha_composite(pop(n).resize((240,192),Image.Resampling.NEAREST),(10+n*250,10))
for n in range(5):sheet.alpha_composite(hit(n).resize((144,144),Image.Resampling.NEAREST),(10+n*200,210))
for n in range(4):sheet.alpha_composite(dust(n).resize((192,72),Image.Resampling.NEAREST),(10+n*250,370))
for n in range(2):sheet.alpha_composite(wind(n).resize((144,120),Image.Resampling.NEAREST),(10+n*160,460))
for n in range(2):sheet.alpha_composite(pellet(n).resize((144,48),Image.Resampling.NEAREST),(400+n*140,500))
sheet.convert('RGB').save(OUT/'zoom-check.png');print('ok')
