# 모아쏘기 폭발 D를 충전량 3단으로. 탄 크기 단과 같은 경계(작음 <0.34 / 중간 <0.67 / 큼).
# 판정은 어느 단이든 2x2unit(64px) 그대로. 작은 단도 섬광은 범위를 거의 덮는다.
# 큼: 불덩이 크게 + 옆 작은 폭발 둘 + 파편 많이 + 연기 한 장 더 높게.
from pathlib import Path
import math, random
from PIL import Image, ImageDraw, ImageFont

OUT=Path(__file__).resolve().parent;C=OUT/'candidate-v1'
INK=(27,22,22,255);INK2=(58,50,48,255);IVORY=(249,238,220,255);YEL=(235,165,22,255);DYEL=(204,124,18,255)
SMOKE=(196,186,170,255);SMOKE2=(226,218,204,255)
S=256;c=S//2   # 크게 키워도 픽셀 크기는 그대로(화폭만 넓힘)
def disc(d,cx,cy,r,col):
    r=max(1,round(r))
    for y in range(-r,r+1):
        for x in range(-r,r+1):
            if x*x+y*y<=r*r+r*.8:d.point((round(cx)+x,round(cy)+y),fill=col)
def lobed(d,cx,cy,R,col,lobes,seed,spread=(0,2*math.pi),body=True):
    rs=random.Random(seed)
    if body:disc(d,cx,cy,R,col)
    for k in range(lobes):
        a=spread[0]+(spread[1]-spread[0])*(k+.5)/lobes+rs.uniform(-.25,.25);r=R*rs.uniform(.7,.95);s=max(2,R*rs.uniform(.35,.5))
        disc(d,cx+r*math.cos(a),cy+r*math.sin(a),s,col)
SIDE=(-math.pi*1.1,math.pi*0.1);UP=(-math.pi*0.95,-math.pi*0.05)
TIERS={
 'S':dict(k=1.0,debris=6,frames=7,side=False,sec=[0.02,0.03,0.035,0.045,0.05,0.06,0.07]),
 'M':dict(k=1.4,debris=10,frames=8,side=False,sec=[0.025,0.03,0.04,0.05,0.06,0.07,0.08,0.09]),
 'L':dict(k=1.9,debris=18,frames=9,side=True,sec=[0.03,0.035,0.045,0.055,0.065,0.075,0.085,0.095,0.1]),
}
def frames(tier):
    T=TIERS[tier];k=T['k'];rd=random.Random(1207)
    DEB=[(rd.uniform(0,2*math.pi),rd.uniform(.8,1.3),rd.choice([2,2,3,3,4])) for _ in range(T['debris'])]
    out=[]
    for n in range(T['frames']):
        im=Image.new('RGBA',(S,S),(0,0,0,0));d=ImageDraw.Draw(im)
        def debris(t,fall,shrink,col=INK):
            for a,reach,s in DEB:
                r=(30+22*reach*t*(1.15 if tier=='L' else 1))*k;x,y=c+r*math.cos(a),c+r*math.sin(a)+fall
                if s-shrink>0:disc(d,x,y,(s-shrink)*(1+.35*(k-1)),col)
        def side(stage):   # 큼 전용: 아래 양옆 작은 폭발 둘
            if not T['side']:return
            for sx,seed in ((-24,90),(24,95)):
                x,y=c+sx*k,c+12*k
                if stage==0:disc(d,x,y,6*k,YEL);disc(d,x,y,3*k,IVORY)
                elif stage==1:lobed(d,x,y,9*k,YEL,5,seed);disc(d,x,y,4*k,IVORY)
                elif stage==2:lobed(d,x,y-4*k,8*k,INK,4,seed+1,UP);disc(d,x,y+2*k,4*k,YEL)
                elif stage==3:disc(d,x,y-8*k,6*k,INK2)
        if n==0:
            disc(d,c,c,18*k,YEL);disc(d,c,c,13*k,IVORY)
        elif n==1:
            lobed(d,c,c,24*k,YEL,9,10);lobed(d,c,c-1,13*k,IVORY,6,11)
        elif n==2:
            side(0);lobed(d,c,c,28*k,DYEL,10,20);lobed(d,c,c,23*k,YEL,9,21);disc(d,c,c+2,9*k,IVORY)
            lobed(d,c,c-14*k,14*k,INK,5,22,UP,body=False);debris(.3,1,1)
        elif n==3:
            side(1);lobed(d,c,c-6*k,26*k,INK,9,30,SIDE,body=False);disc(d,c,c-8*k,20*k,INK)
            lobed(d,c,c+6*k,14*k,YEL,6,31);disc(d,c,c+8*k,6*k,IVORY);debris(.75,3,0)
        elif n==4:
            side(2);disc(d,c,c-12*k,21*k,INK);lobed(d,c,c-12*k,19*k,INK,8,40,SIDE,body=False)
            lobed(d,c,c+6*k,9*k,YEL,5,41);debris(.92,7,1)
        elif n==5:
            side(3)
            for dx,dy,r in [(-12,-20,11),(8,-24,12),(-2,-10,10),(16,-10,8),(-18,-6,7)]:disc(d,c+dx*k,c+dy*k,r*k,INK2)
            disc(d,c+1,c+6*k,4*k,YEL);debris(1.0,12,2,INK2)
        elif n==6:
            last=T['frames']==7
            for dx,dy,r in [(-14,-26,9),(9,-30,10),(-3,-16,8),(18,-16,6),(-20,-12,5)]:disc(d,c+dx*k,c+dy*k,r*k*(.8 if last else 1),SMOKE2 if last else SMOKE)
            debris(1.05,18,3,INK2)
        elif n==7:
            lift=8 if tier=='L' else 0
            for dx,dy,r in [(-15,-31,7),(10,-35,8),(-4,-22,6),(20,-22,4)]:disc(d,c+dx*k,c+(dy-lift)*k,r*k,SMOKE if tier=='L' else SMOKE2)
        else:  # 큼 전용 마지막 연기
            for dx,dy,r in [(-16,-46,6),(10,-50,7),(-4,-38,5),(21,-38,3)]:disc(d,c+dx*k,c+dy*k,r*k,SMOKE2)
        out.append(im)
    return out
FR={t:frames(t) for t in TIERS}
for t,fs in FR.items():
    (C/f'burstD_{t}').mkdir(exist_ok=True)
    for n,im in enumerate(fs):im.save(C/f'burstD_{t}'/f'F{n:02d}.png')

if __name__=='__main__':
    import charge_view as v, charge_compare as cc
    LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
    def tier_of(cs):return 'S' if cs.amount<.34 else 'M' if cs.amount<.67 else 'L'
    CASES=[(v.CASES[0][0],'짧게 · 작은 폭발'),(v.Case(0.5),'중간 0.5초 · 중간 폭발'),(v.CASES[1][0],'끝까지 · 큰 폭발')]
    def row(cs,name,a):
        t=tier_of(cs);cc.BURST=FR[t];cc.cand['burst']['seconds']=TIERS[t]['sec']
        return cc.new(cs,a,name)
    def frame(off,real=False):
        out=Image.new('RGB',(v.W,(v.H+12)*3),'#ffffff')
        for i,(cs,name) in enumerate(CASES):
            a=off if real else cs.r+cs.hit_t+off
            out.paste(row(cs,name,a).convert('RGB'),(0,i*(v.H+12)))
        return out
    fr=[];t=-.06
    while t<.7:fr.append(frame(t));t+=.006
    fr[0].save(OUT/'burstD-tiers-slow.gif',save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    fr=[];t=-.05
    while t<1.6:fr.append(frame(t,True));t+=.02
    fr[0].save(OUT/'burstD-tiers-real.gif',save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',20)
    sheet=Image.new('RGB',(9*262+20,3*300),'#ffffff');d=ImageDraw.Draw(sheet)
    for i,(t,name) in enumerate((('S','1단 폭발'),('M','2단 폭발'),('L','3단 폭발'))):
        d.text((10,i*300+4),name,font=F,fill='#222222')
        for n,f in enumerate(FR[t]):
            im=Image.new('RGBA',(S,S),(255,255,255,255));im.alpha_composite(f)
            ImageDraw.Draw(im).rectangle((c-32,c-32,c+32,c+32),outline=(232,232,232,255))
            sheet.paste(im.convert('RGB'),(10+n*262,i*300+34))
    sheet.save(OUT/'burstD-tiers-zoom.png');print('ok')
