# 모아쏘기 3단계 안(제안, 데이터 미변경). 1단 0.2~0.4초 20 / 2단 0.4~0.6초 30 / 3단 0.6~0.8초 40, 0.8초 자동 발사.
# 판정 지름 0.3 / 0.45 / 0.6. 그림: 구슬 3단(오를 때 반짝) · 탄 S/M/L · 총구 1단 작게, 2·3단 크게 · 폭발 pixelfx 1·2·3단.
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import charge_view as v, charge_compare as cc, burst_tiers as bt

OUT=Path(__file__).resolve().parent
v.H,v.GROUND=400,360   # 큰 폭발·연기가 잘리지 않게 장면을 위로 넓힘
STEPS=[(0.4,20,0.15),(0.6,30,0.225),(9,40,0.3)]   # (이 시각 전까지, 피해, 판정 반지름)
UP_AT=[0.4,0.6]
import json
def _pfx(step):
    d=OUT/'pfx'/f'step{step}';m=json.loads((d/'meta.json').read_text(encoding='utf-8'))
    m['frames']=[Image.open(d/f'{i:02d}.png').convert('RGBA') for i in range(m['frames'])];return m
PFX=[_pfx(1),_pfx(2),_pfx(3)]
ORB_STAGE=[1,3,4]          # v1 구슬 5단 중 1단·2단·3단에 쓸 크기
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22);SM=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',18)
def step_at(t):
    for i,(until,_,_) in enumerate(STEPS):
        if t<until:return i
    return 2
class StepCase(v.Case):
    def __init__(s,release):
        super().__init__(release);s.step=step_at(s.r-1e-6 if s.r>=v.CMAX else s.r)
        _,s.damage,s.radius=STEPS[s.step];s.hit_t=(v.DUMMY-s.radius-v.RG['spawnX'])/v.RG['speed']
def new(cs,a,label):
    c=Image.new('RGBA',(v.W,v.H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,v.GROUND,v.W,v.GROUND),fill='#d6d6d6',width=2)
    x0,y0=v.px(v.DUMMY,0);d.rectangle((x0,y0-2*v.U,x0+.8*v.U,y0),outline='#b9b9b9',width=3)
    f,_=cc.body(cs,a);v.paste(c,f,0,0)
    mx,my=cc.MUZ['F048']
    if .05<=a<cs.r:
        st=step_at(max(a,v.CMIN)) if a>=v.CMIN else 0
        cc.put(c,cc.SUCK[int(a/.05)%3],mx+.12,my);cc.put(c,cc.ORB[ORB_STAGE[st]][int(a/.06)%2],mx+.12,my)
        for u in UP_AT:   # 단계 오를 때 반짝
            e=a-u
            if 0<=e<.12:cc.put(c,cc.FULL[v.pick([.03,.04,.05],e)],mx+.12,my)
    t=a-cs.r;fx,fy=cc.MUZ['F049']
    if cs.step==2 and 0<=t<.12:cc.put(c,cc.FULL[v.pick([.03,.04,.05],t)],mx+.12,my)
    if cs.step==0:
        if 0<=t<sum(cc.SMALL_SEC):cc.put(c,cc.SMALL[v.pick(cc.SMALL_SEC,t)],fx,fy,(10,32))
    elif 0<=t<sum(cc.cand['pop']['bigpop_seconds']):cc.put(c,cc.BIG[v.pick(cc.cand['pop']['bigpop_seconds'],t)],fx,fy,cc.cand['pop']['bigpop_pivot_px'])
    if 0<=t<cs.hit_t:
        x=v.RG['spawnX']+v.RG['speed']*t
        if x>fx:cc.put(c,cc.SHOT[cs.step][int(t/.04)%2],x,.875,cc.cand['shot']['pivot_px'])
    # 명중 폭발: pixelfx explosions/charge-shot-burst step 1·2·3 (30fps), 원점 = 맞은 자리
    P=PFX[cs.step];e=t-cs.hit_t
    if 0<=e<len(P['frames'])/P['fps']:cc.put(c,P['frames'][int(e*P['fps'])],v.DUMMY,v.RG['spawnHeight'],P['origin_px'])
    d=ImageDraw.Draw(c);d.text((14,10),label,font=LBL,fill='#e0482f')
    d.text((14,40),f'{cs.step+1}단 · 피해 {cs.damage} · 탄 지름 {cs.radius*2:.2f}',font=SM,fill='#999999')
    # 아래 시간 막대: 단계 경계 표시
    W=v.W;bar=lambda s:int(W*s/1.4)
    for x in (bar(.2),bar(.4),bar(.6),bar(.8)):d.line((x,v.H-12,x,v.H),fill='#cccccc',width=1)
    d.rectangle((0,v.H-5,bar(min(1.4,max(0,a))),v.H),fill='#e0a43a')
    return c
CASES=[(StepCase(0.3),'1단 (0.3초에 뗌)'),(StepCase(0.5),'2단 (0.5초에 뗌)'),(StepCase(0.8),'3단 (끝까지, 자동)')]
def frame(a):
    out=Image.new('RGB',(v.W,(v.H+12)*3),'#ffffff')
    for i,(cs,name) in enumerate(CASES):out.paste(new(cs,a,name).convert('RGB'),(0,i*(v.H+12)))
    return out
if __name__=='__main__':
    for name,step in (('charge-steps-slow.gif',.006),('charge-steps-real.gif',.02)):
        fr=[];t=-.06
        while t<1.75:fr.append(frame(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    # 끝까지 모을 때 단계 오름 정지판
    cs=CASES[2][0];cells=[]
    for a in (.15,.3,.42,.5,.62,.75):
        im=new(cs,a,'').convert('RGB').crop((0,v.GROUND-150,300,v.GROUND+6))
        ImageDraw.Draw(im).text((8,4),f'{a:.2f}초 · {step_at(max(a,v.CMIN))+1}단',font=SM,fill='#e0482f');cells.append(im)
    sh=Image.new('RGB',(3*310,2*(cells[0].height+10)),'#ffffff')
    for i,im in enumerate(cells):sh.paste(im,((i%3)*310,(i//3)*(im.height+10)))
    sh.save(OUT/'charge-steps-orb.png');print('ok')
