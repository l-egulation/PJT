# 모아쏘기 시안 v1: 충전 구슬·빨려드는 불티·꽉 참 반짝임·충전 탄 3단·큰 총구 터짐·2x2 범위 터짐. PPU32. 미승인 후보.
# 판정·수치 불변. 몸은 새 그림 없이 기존 프레임 재배열(충전 중 F048 겨눔 유지 → 뗄 때 F049 → F051 → F052).
from pathlib import Path
import json, math, random
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent/'candidate-v1'
INK=(27,22,22,255);INK2=(58,50,48,255);IVORY=(249,238,220,255);YEL=(235,165,22,255)
SMOKE=(196,186,170,255);SMOKE2=(226,218,204,255)
for k in ('orb','suck','full','shot','bigpop','burst'):(OUT/k).mkdir(parents=True,exist_ok=True)

def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            if (x/max(rx,.5))**2+(y/max(ry,.5))**2<=1-rough*rnd.random():d.point((cx+x,cy+y),fill=col)
def disc(d,cx,cy,r,col):
    for y in range(-r,r+1):
        for x in range(-r,r+1):
            if x*x+y*y<=r*r+r*.8:d.point((cx+x,cy+y),fill=col)

# 충전 구슬: 28x28 기준점 가운데. 5단(충전량 0/.25/.5/.75/1) × 2장 깜빡임. 먹 구슬이 커지고 아이보리 심·노란 테 불티.
ORB_R=[2,4,6,8,10]
def orb(stage,n):
    im=Image.new('RGBA',(28,28),(0,0,0,0));d=ImageDraw.Draw(im);c=14;r=ORB_R[stage]
    disc(d,c,c,r,INK)
    disc(d,c-max(0,r//3),c-max(0,r//3),max(0,r//2-1),IVORY) if r>=3 else d.point((c,c-1),fill=IVORY)
    rs=random.Random(stage*10+n)
    for k in range(stage+1):
        a=rs.uniform(0,2*math.pi);rr=r+1+rs.random()*1.5;d.point((round(c+rr*math.cos(a)),round(c+rr*math.sin(a))),fill=YEL)
    return im
for s in range(5):
    for n in range(2):orb(s,n).save(OUT/'orb'/f'S{s}F{n:02d}.png')

# 빨려드는 불티: 48x48 기준점 가운데. 3장 순환(0.05초). 바깥에서 구슬로 모여드는 노란 줄·먹 점.
rk=random.Random(5);SPARKS=[(rk.uniform(0,2*math.pi),rk.uniform(0,1)) for _ in range(8)]
def suck(n):
    im=Image.new('RGBA',(48,48),(0,0,0,0));d=ImageDraw.Draw(im);c=24
    for k,(a,ph) in enumerate(SPARKS):
        f=(ph+n/3)%1;r=22-16*f
        x,y=c+r*math.cos(a),c+r*math.sin(a);x2,y2=c+(r+3)*math.cos(a),c+(r+3)*math.sin(a)
        if k%3==0:d.point((round(x),round(y)),fill=INK2)
        else:d.line((round(x),round(y),round(x2),round(y2)),fill=YEL)
    return im
for n in range(3):suck(n).save(OUT/'suck'/f'F{n:02d}.png')

# 꽉 참 반짝임: 40x40 기준점 가운데. 3장. 노란 십자 반짝임 + 아이보리 점. 고리 없음.
def full(n):
    im=Image.new('RGBA',(40,40),(0,0,0,0));d=ImageDraw.Draw(im);c=20;L=[10,16,12][n]
    d.line((c-L,c,c+L,c),fill=YEL);d.line((c,c-L,c,c+L),fill=YEL)
    if n<2:d.line((c-L//2,c-L//2,c+L//2,c+L//2),fill=YEL);d.line((c-L//2,c+L//2,c+L//2,c-L//2),fill=YEL)
    disc(d,c,c,[3,2,1][n],IVORY)
    return im
for n in range(3):full(n).save(OUT/'full'/f'F{n:02d}.png')

# 충전 탄: 3단(작음/중간/큼) × 2장. 40x24 기준점(28,12)=탄 중심. 지름 10/14/19px = 판정 지름 0.3/0.45/0.6 근처.
SHOT_R=[5,7,9]
def shot(size,n):
    im=Image.new('RGBA',(40,24),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=28,12;r=SHOT_R[size]
    # 끊긴 꼬리
    # 꼬리: 줄마다 같은 자리에서 끊기는 통짜 토막(점묘 금지). 가운데 줄이 길고 바깥 줄은 짧다.
    for k in range(-size,size+1):
        L=14+size*3-abs(k)*5
        for x in range(cx-r-L,cx-r+1):
            if (x+n)%6!=0:d.point((x,cy+k),fill=INK if x>cx-r-7 else INK2)
    disc(d,cx,cy,r,INK)
    disc(d,cx-r//3+1,cy-r//3,max(1,r//3),IVORY)
    if size>=1:
        rs=random.Random(size*7+n)
        for k in range(2+size*2):
            a=rs.uniform(-2.2,2.2)+math.pi;rr=r+1+rs.random()*2
            d.point((round(cx-rr*math.cos(a)),round(cy+rr*math.sin(a))),fill=YEL)
    return im
for s in range(3):
    for n in range(2):shot(s,n).save(OUT/'shot'/f'S{s}F{n:02d}.png')

# 큰 총구 터짐(충전량 0.5 이상): 88x72 기준점(8,36). 노란 큰 덩어리 → 앞 부채꼴 노란 줄 → 먹 조각+연기 → 연기.
BIGPOP=[0.03,0.04,0.05,0.06]
def bigpop(n):
    im=Image.new('RGBA',(88,72),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=8,36
    if n==0:
        blob(d,cx+12,cy,13,12,YEL,.25,1);blob(d,cx+10,cy,6,5,IVORY,.1,2)
        for a in (-24,-12,0,12,24):
            r=math.radians(a);d.line((round(cx+22*math.cos(r)),round(cy+22*math.sin(r)),round(cx+40*math.cos(r)),round(cy+40*math.sin(r))),fill=YEL,width=2)
    elif n==1:
        blob(d,cx+17,cy,17,14,YEL,.3,3);blob(d,cx+14,cy,5,4,IVORY,.2,4)
        for k,(dx,dy) in enumerate([(40,-14),(46,8),(34,18),(38,-22)]):blob(d,cx+dx,cy+dy,2,2,INK,.1,5+k)
    elif n==2:
        for k,(dx,dy,r) in enumerate([(12,-9,6),(22,5,7),(33,-4,5),(10,10,4),(42,6,3)]):blob(d,cx+dx,cy+dy,r,r-1,SMOKE,.3,10+k)
        blob(d,cx+16,cy,5,4,YEL,.4,15)
        for k,(dx,dy) in enumerate([(50,-16),(54,10),(44,22),(46,-25)]):blob(d,cx+dx,cy+dy,1,1,INK)
    else:
        for k,(dx,dy,r) in enumerate([(16,-12,6),(27,2,7),(38,-8,5),(14,11,4)]):blob(d,cx+dx,cy+dy,r,r-1,SMOKE2,.45,20+k)
    return im
for n in range(4):bigpop(n).save(OUT/'bigpop'/f'F{n:02d}.png')

# 범위 터짐: 2x2unit = 64px 상자를 덮는 별 모양 터짐. 96x96 기준점 가운데. 6장.
# 0 노란 섬광 / 1 노란 덩어리+여덟 갈래 먹 물줄기(대각은 길게 → 네모 범위 모서리까지) / 2 먹 덩어리 제각각+노란 잔불 / 3 떨어지는 먹 방울 / 4·5 연기.
BURST=[0.03,0.04,0.05,0.06,0.07,0.08]
def burst(n):
    S=96;im=Image.new('RGBA',(S,S),(0,0,0,0));d=ImageDraw.Draw(im);c=S//2;rs=random.Random(300)
    spokes=[(k*45+rs.uniform(-6,6),(44 if k%2 else 32)*rs.uniform(.92,1.04)) for k in range(8)]
    if n==0:
        blob(d,c,c,14,14,YEL,.2,1);blob(d,c,c,7,7,IVORY,.1,2)
    elif n==1:
        blob(d,c,c,20,19,YEL,.25,3);blob(d,c,c,7,6,IVORY,.15,4)
        for a,L in spokes:
            r=math.radians(a)
            for t in range(18,int(L)):
                w=2 if t<L*.7 else 1
                x,y=c+t*math.cos(r),c+t*math.sin(r)
                if (t%6)!=0:d.rectangle((round(x)-w//2,round(y)-w//2,round(x)+w//2,round(y)+w//2),fill=INK)
    elif n==2:
        blob(d,c,c,11,10,YEL,.35,5)
        # 고리로 안 읽히게: 거리·각도·크기를 제각각, 일부 갈래는 비운다.
        for k,(a,L) in enumerate(spokes):
            if k in (2,5):continue
            r=math.radians(a+rs.uniform(-14,14));rr=L*rs.uniform(.5,1.05);s=[4,3,5,2,3,4,2,3][k]
            blob(d,round(c+rr*math.cos(r)),round(c+rr*math.sin(r)),s,max(1,s-1),INK if k%2 else INK2,.15,20+k)
        for k in range(4):blob(d,c+rs.randint(-16,16),c+rs.randint(-14,14),rs.randint(4,6),rs.randint(3,5),SMOKE,.3,70+k)
    elif n==3:
        for k,(a,L) in enumerate(spokes):
            r=math.radians(a);rr=L*.95;s=[3,2,3,1,2,3,1,2][k]
            blob(d,round(c+rr*math.cos(r)),round(c+rr*math.sin(r))+4,s,s,INK2,.1,40+k)
        for k in range(5):blob(d,c+rs.randint(-14,14),c+rs.randint(-12,12),rs.randint(4,6),rs.randint(3,5),SMOKE,.3,50+k)
    else:
        m=n-4
        for k in range(6):
            a=rs.uniform(0,2*math.pi);rr=rs.uniform(8,22)+m*5
            blob(d,round(c+rr*math.cos(a)),round(c+rr*math.sin(a))-m*3,5-m,4-m,SMOKE if m==0 else SMOKE2,.35+.15*m,60+k+m*7)
    return im
for n in range(6):burst(n).save(OUT/'burst'/f'F{n:02d}.png')

json.dump({'status':'candidate_unapproved',
 'body':{'rule':'charging: F046 0~0.05s then hold F048 (aim) until release; release: F049 0.07*k, F051 0.17*k, F052 0.17*k with k=0.3/0.41; auto-fire at 0.8 so no held full state','no_new_character_art':True},
 'orb':{'pivot':'center','stages_by_amount':[0,.25,.5,.75,1],'seconds':[0.06,0.06],'anchor':'F048 muzzle (1.3125,0.875) + 0.12 forward'},
 'suck':{'pivot':'center','seconds':[0.05,0.05,0.05],'while':'charging, amount<1'},
 'full':{'pivot':'center','seconds':[0.03,0.04,0.05],'trigger':'amount reaches 1 (0.8s; auto-fire same instant)'},
 'shot':{'pivot_px':[28,12],'sizes':{'S':[0,.34],'M':[.34,.67],'L':[.67,1]},'seconds':[0.04,0.04],'note':'visual diameter tracks collision 0.3->0.6; hit box unchanged'},
 'pop':{'amount<0.5':'basic approved pop (launcher-basic-breakdown-20261106/candidate-v1/pop)','amount>=0.5':'bigpop','bigpop_pivot_px':[8,36],'bigpop_seconds':BIGPOP,'anchor':'fire-time F049 muzzle (1.15625,0.875), world-fixed'},
 'burst':{'pivot':'center','seconds':BURST,'covers':'2x2 impact box (64px) with diagonal spokes to corners','replaces':'orange translucent debug square','trigger':'enemy hit only (terrain/expiry no burst, same as rule)'}},
 open(OUT/'candidate.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
sheet=Image.new('RGBA',(1200,700),(255,255,255,255))
for s in range(5):sheet.alpha_composite(orb(s,0).resize((112,112),Image.Resampling.NEAREST),(10+s*100,10))
for n in range(3):sheet.alpha_composite(suck(n).resize((144,144),Image.Resampling.NEAREST),(520+n*150,10))
for n in range(3):sheet.alpha_composite(full(n).resize((120,120),Image.Resampling.NEAREST),(10+n*130,160))
for s in range(3):sheet.alpha_composite(shot(s,0).resize((160,96),Image.Resampling.NEAREST),(420+s*170,170))
for n in range(4):sheet.alpha_composite(bigpop(n).resize((264,216),Image.Resampling.NEAREST),(10+n*280,280))
for n in range(6):sheet.alpha_composite(burst(n).resize((192,192),Image.Resampling.NEAREST),(10+n*196,500))
sheet.convert('RGB').save(OUT/'zoom-check.png');print('ok')
