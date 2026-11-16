# 유탄 시안 v1 (2026-11-06 사용자 확정, Unity 미적용): 탄·연기 꼬리·총구 터짐·둥근 폭발(본체/파편)·파편 탄. 원본 해상도(PPU32) 픽셀. 미승인 후보.
# 판정·수치·궤적·파편 각도 불변. 몸 동작 그대로. 색은 확정 기본포·샷건과 같은 노랑·먹·아이보리. 검은 테 없음.
from pathlib import Path
import json, math, random
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent/'candidate-v1'
INK=(27,22,22,255);INK2=(58,50,48,255);IVORY=(249,238,220,255);YEL=(235,165,22,255)
SMOKE=(196,186,170,255);SMOKE2=(226,218,204,255)
for k in ('shell','smoke','pop','blast','frag','fragblast'):(OUT/k).mkdir(parents=True,exist_ok=True)

def blob(d,cx,cy,rx,ry,col,rough=0.0,seed=0):
    rnd=random.Random(seed)
    for y in range(-ry,ry+1):
        for x in range(-rx,rx+1):
            if (x/max(rx,.5))**2+(y/max(ry,.5))**2<=1-rough*rnd.random():d.point((cx+x,cy+y),fill=col)

# 탄: 14x10, 기준점(8,5)=탄 중심. 먹 몸통 9x7 + 아이보리 띠 + 꽁무니 노란 불씨. 2장 불씨 깜빡임. 진행 방향으로 회전.
def shell(n):
    im=Image.new('RGBA',(14,10),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=8,5
    d.rectangle((cx-4,cy-2,cx+3,cy+1),fill=INK);d.line((cx-3,cy-3,cx+2,cy-3),fill=INK);d.line((cx-3,cy+2,cx+2,cy+2),fill=INK)
    d.line((cx+4,cy-1,cx+4,cy),fill=INK)                      # 둥근 코
    for y in range(cy-2,cy+2):d.point((cx,y),fill=IVORY)      # 아이보리 띠
    d.point((cx+2,cy-2),fill=INK2)
    if n==0:d.point((cx-5,cy),fill=YEL);d.point((cx-6,cy),fill=YEL);d.point((cx-5,cy-1),fill=IVORY)
    else:d.point((cx-5,cy),fill=YEL);d.point((cx-6,cy+1),fill=YEL);d.point((cx-7,cy),fill=YEL)
    return im
for n in range(2):shell(n).save(OUT/'shell'/f'F{n:02d}.png')

# 연기 꼬리: 12x12, 기준점(6,6). 탄이 0.03초마다 떨어뜨리는 작은 연기. 4장 0.05초씩 커지며 옅어진다.
SMOKE_SEC=[0.05,0.05,0.06,0.07]
def smoke(n):
    im=Image.new('RGBA',(12,12),(0,0,0,0));d=ImageDraw.Draw(im)
    r=[2,3,3,2][n];blob(d,6,6-n//2,r,r,SMOKE if n<2 else SMOKE2,.3 if n<3 else .55,70+n)
    if n==0:d.point((6,6),fill=INK2)
    return im
for n in range(4):smoke(n).save(OUT/'smoke'/f'F{n:02d}.png')

# 총구 터짐: 64x56, 기준점(6,28)=총구(F049 끝 1.0,1.156). 쏜 자리에 고정. 노란 심 → 큰 노란 덩어리 → 먹 조각+연기 → 연기.
POP=[0.025,0.035,0.045,0.06]
def pop(n):
    im=Image.new('RGBA',(64,56),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=6,28
    if n==0:
        blob(d,cx+8,cy,9,8,YEL,.35,1);blob(d,cx+7,cy,4,3,IVORY,.2,2)
        for k in (-1,0,1):d.line((cx+16,cy+k*4,cx+26,cy+k*7),fill=YEL)
    elif n==1:
        blob(d,cx+13,cy,13,11,YEL,.6,3);blob(d,cx+11,cy,4,3,IVORY,.3,4)
        for k,(dx,dy) in enumerate([(26,-10),(30,6),(22,13)]):blob(d,cx+dx,cy+dy,2,2,INK,.2,5+k)
    elif n==2:
        for k,(dx,dy,r) in enumerate([(10,-7,5),(18,4,6),(26,-3,4),(8,8,4)]):blob(d,cx+dx,cy+dy,r,r-1,SMOKE,.35,10+k)
        blob(d,cx+12,cy,4,3,YEL,.6,15)
        for k,(dx,dy) in enumerate([(34,-12),(36,9),(28,16),(30,-17)]):blob(d,cx+dx,cy+dy,1,1,INK)
    else:
        for k,(dx,dy,r) in enumerate([(13,-10,5),(22,2,6),(31,-6,4),(11,9,3)]):blob(d,cx+dx,cy+dy,r,r-1,SMOKE2,.5,20+k)
    return im
for n in range(4):pop(n).save(OUT/'pop'/f'F{n:02d}.png')

# 둥근 폭발: 기준점=정중앙(맞은 자리). 어디서 맞아도(공중·바닥) 같은 그림. R=폭발 반경 픽셀.
# 0 노란 섬광+아이보리 심 / 1 노란 덩어리+파편 방향 노란 줄기4 / 2 먹 덩어리 조각이 바깥으로+노란 잔불 / 3~ 연기 흩어짐.
FA=[30,60,120,150]
def blast(n,R,seed,frag=False):
    S=R*2+16;im=Image.new('RGBA',(S,S),(0,0,0,0));d=ImageDraw.Draw(im);c=S//2
    rs=random.Random(seed)
    if n==0:
        blob(d,c,c,round(R*.55),round(R*.55),YEL,.25,seed);blob(d,c,c,round(R*.28),round(R*.28),IVORY,.1,seed+1)
    elif n==1:
        blob(d,c,c,round(R*.85),round(R*.8),YEL,.22,seed+2);blob(d,c,c,round(R*.35),round(R*.3),IVORY,.15,seed+3)
        if not frag:
            for a in FA:
                r=math.radians(a);d.line((round(c+R*.7*math.cos(r)),round(c-R*.7*math.sin(r)),round(c+R*1.0*math.cos(r)),round(c-R*1.0*math.sin(r))),fill=YEL,width=2)
    elif n==2:
        # 고리로 안 읽히게: 연기 덩어리로 바깥을 채우고, 먹 조각은 몇 개만 제각각 거리·크기로 흩는다.
        for k in range(6 if not frag else 4):
            a=rs.uniform(0,2*math.pi);r=R*rs.uniform(.35,.6);s=max(2,round(R*rs.uniform(.22,.3)))
            blob(d,round(c+r*math.cos(a)),round(c+r*math.sin(a)),s,s,SMOKE,.2,seed+40+k)
        blob(d,c,c,round(R*.45),round(R*.4),YEL,.25,seed+4);blob(d,c,c,round(R*.15),round(R*.12),IVORY,0,seed+5)
        angs=[rs.uniform(0,2*math.pi) for _ in range(5 if not frag else 3)]
        for k,a in enumerate(angs):
            r=R*rs.uniform(.6,1.05);s=max(1,round(R*[.13,.08,.1,.06,.09][k]))
            blob(d,round(c+r*math.cos(a)),round(c+r*math.sin(a)),s,max(1,s-1),INK if k%2==0 else INK2,.15,seed+10+k)
    else:
        m=n-3;cnt=7 if not frag else 5
        for k in range(cnt):
            a=2*math.pi*k/cnt+rs.uniform(-.4,.4)+m*.2;r=R*(.6+.15*m)*rs.uniform(.8,1.1);s=max(1,round(R*(.2-.04*m)))
            blob(d,round(c+r*math.cos(a)),round(c+r*math.sin(a)-m*R*.08),s,s,SMOKE if m==0 else SMOKE2,.35+.15*m,seed+30+k+m*9)
        if m==0:
            for k in range(4):
                a=rs.uniform(0,2*math.pi);r=R*rs.uniform(.9,1.1);d.point((round(c+r*math.cos(a)),round(c+r*math.sin(a))),fill=INK)
    return im
BLAST=[0.03,0.04,0.05,0.06,0.07,0.08];FRAGBLAST=[0.025,0.035,0.04,0.05,0.06]
R_MAIN=round(1.2*32);R_FRAG=round(0.6*32)   # 폭발 반경과 같은 크기
for n in range(6):blast(n,R_MAIN,100).save(OUT/'blast'/f'F{n:02d}.png')
for n in range(5):blast(n,R_FRAG,200,True).save(OUT/'fragblast'/f'F{n:02d}.png')

# 파편 탄: 10x8, 기준점(6,4). 먹 조각 5x4 + 노란 불씨 + 짧은 꼬리. 2장. 진행 방향으로 회전.
def frag(n):
    im=Image.new('RGBA',(10,8),(0,0,0,0));d=ImageDraw.Draw(im);cx,cy=6,4
    d.rectangle((cx-2,cy-1,cx+1,cy),fill=INK);d.line((cx-1,cy-2,cx,cy-2),fill=INK);d.line((cx-1,cy+1,cx,cy+1),fill=INK)
    d.point((cx,cy-1),fill=YEL)
    for x in range(0,cx-2):
        if (x+n)%3:d.point((x,cy),fill=YEL if x>=cx-4 else INK2)
    return im
for n in range(2):frag(n).save(OUT/'frag'/f'F{n:02d}.png')

json.dump({'status':'user_approved_look_20261106_not_in_unity',
 'shell':{'pivot_px':[8,5],'seconds':[0.05,0.05],'rotate':'velocity','visual_rule':'start on muzzle (F049 tip 1.0,1.156): y offset +0.156 over hit circle easing to 0 in 0.08s; hidden until past muzzle tip; hit circle unchanged'},
 'smoke':{'pivot_px':[6,6],'seconds':SMOKE_SEC,'emit_every':0.03,'from':'shell tail, world-fixed'},
 'pop':{'pivot_px':[6,28],'seconds':POP,'anchor':'fire-time muzzle, world-fixed; existing black muzzle (x0.4) kept underneath'},
 'blast':{'pivot':'center','radius_px':R_MAIN,'seconds':BLAST,'replaces':'ground silhouette blast for main grenade','yellow_streaks_at_fragment_angles':FA},
 'fragblast':{'pivot':'center','radius_px':R_FRAG,'seconds':FRAGBLAST,'replaces':'half-scale main blast'},
 'frag':{'pivot_px':[6,4],'seconds':[0.04,0.04],'rotate':'velocity','replaces':'half-scale grenade projectile'},
 'gun_tip_measured':{'F046':[1.0938,0.9219],'F047':[1.2812,0.9531],'F049':[1.0,1.1562],'F050':[1.1875,0.9844],'F051':[1.25,0.8906],'F053':[1.1875,0.8594]}},
 open(OUT/'candidate.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
sheet=Image.new('RGBA',(6*190+20,640),(255,255,255,255))
for n in range(6):sheet.alpha_composite(blast(n,R_MAIN,100).resize((176,176),Image.Resampling.NEAREST),(10+n*190,10))
for n in range(5):sheet.alpha_composite(blast(n,R_FRAG,200,True).resize((104,104),Image.Resampling.NEAREST),(10+n*190,200))
for n in range(4):sheet.alpha_composite(pop(n).resize((192,168),Image.Resampling.NEAREST),(10+n*200,320))
for n in range(2):sheet.alpha_composite(shell(n).resize((84,60),Image.Resampling.NEAREST),(10+n*100,520))
for n in range(4):sheet.alpha_composite(smoke(n).resize((60,60),Image.Resampling.NEAREST),(230+n*70,520))
for n in range(2):sheet.alpha_composite(frag(n).resize((60,48),Image.Resampling.NEAREST),(540+n*80,520))
sheet.convert('RGB').save(OUT/'zoom-check.png');print('ok')
