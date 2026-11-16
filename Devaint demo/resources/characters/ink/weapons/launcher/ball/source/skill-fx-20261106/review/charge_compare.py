# 모아쏘기 지금 vs 시안 v1. 그림만 재현. 발사 시각·탄 속도·명중 시각·범위 크기는 지금과 같다.
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import charge_view as v

OUT=Path(__file__).resolve().parent;C=OUT/'candidate-v1'
BASIC=OUT.parent/'launcher-basic-breakdown-20261106'/'candidate-v1'
cand=json.loads((C/'candidate.json').read_text(encoding='utf-8'))
def L(p):return Image.open(p).convert('RGBA')
ORB=[[L(C/'orb'/f'S{s}F{n:02d}.png') for n in range(2)] for s in range(5)]
SUCK=[L(C/'suck'/f'F{n:02d}.png') for n in range(3)];FULL=[L(C/'full'/f'F{n:02d}.png') for n in range(3)]
SHOT=[[L(C/'shot'/f'S{s}F{n:02d}.png') for n in range(2)] for s in range(3)]
BIG=[L(C/'bigpop'/f'F{n:02d}.png') for n in range(4)];BURST=[L(C/'burst'/f'F{n:02d}.png') for n in range(6)]
SMALL=[L(BASIC/'pop'/f'F{n:02d}.png') for n in range(4)];SMALL_SEC=[0.025,0.03,0.035,0.04]
FR={n.split('/')[-1]:n for n in v.clip['frames']}
MUZ={'F046':(0.84375,.875),'F048':(1.3125,.875),'F049':(1.15625,.875),'F051':(0.96875,.875),'F052':(0.84375,.875)}
REC=[('F049',.07),('F051',.17),('F052',.17)];K=0.3/sum(s for _,s in REC)
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22);SM=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',18)

def put(c,im,ux,uy,pivot=None):
    k=v.U/32;im=im.resize((round(im.width*k),round(im.height*k)),Image.Resampling.NEAREST)
    pvx,pvy=(pivot[0]*k,pivot[1]*k) if pivot else (im.width/2,im.height/2)
    x,y=v.px(ux,uy);c.alpha_composite(im,(round(x-pvx),round(y-pvy)))
def body(cs,a):
    if a<0 or a>=cs.end:return v.idle['frames'][0],None
    if a<cs.r:return (FR['F046'],'F046') if a<.05 else (FR['F048'],'F048')
    e=a-cs.r
    for name,sec in REC:
        if e<sec*K:return FR[name],name
        e-=sec*K
    return FR['F052'],'F052'
def amount_at(cs,a):return max(0,min(1,(min(a,cs.r)-v.CMIN)/(v.CMAX-v.CMIN)))
def new(cs,a,label):
    c=Image.new('RGBA',(v.W,v.H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,v.GROUND,v.W,v.GROUND),fill='#d6d6d6',width=2)
    x0,y0=v.px(v.DUMMY,0);d.rectangle((x0,y0-2*v.U,x0+.8*v.U,y0),outline='#b9b9b9',width=3)
    f,name=body(cs,a);v.paste(c,f,0,0)
    if .05<=a<cs.r:   # 충전 중: 빨려드는 불티 + 커지는 구슬
        mx,my=MUZ['F048'];am=amount_at(cs,a);st=min(4,int(am*4+.0001))
        put(c,SUCK[int(a/.05)%3],mx+.12,my);put(c,ORB[st][int(a/.06)%2],mx+.12,my)
    t=a-cs.r;mx,my=MUZ['F049']
    if cs.amount>=1 and 0<=t<.12:put(c,FULL[v.pick([.03,.04,.05],t)],MUZ['F048'][0]+.12,MUZ['F048'][1])
    if cs.amount<.5:
        if 0<=t<sum(SMALL_SEC):put(c,SMALL[v.pick(SMALL_SEC,t)],mx,my,(10,32))
    elif 0<=t<sum(cand['pop']['bigpop_seconds']):put(c,BIG[v.pick(cand['pop']['bigpop_seconds'],t)],mx,my,cand['pop']['bigpop_pivot_px'])
    if 0<=t<cs.hit_t:
        x=v.RG['spawnX']+v.RG['speed']*t
        if x>mx:
            size=0 if cs.amount<.34 else 1 if cs.amount<.67 else 2
            put(c,SHOT[size][int(t/.04)%2],x,.875,cand['shot']['pivot_px'])
    e=t-cs.hit_t
    if 0<=e<sum(cand['burst']['seconds']):put(c,BURST[v.pick(cand['burst']['seconds'],e)],v.DUMMY,v.RG['spawnHeight'])
    d=ImageDraw.Draw(c);d.text((14,10),label,font=LBL,fill='#e0482f')
    state='충전 중' if 0<=a<cs.r else '발사' if cs.r<=a<cs.r+.06 else ''
    if state and label:d.text((v.W-110,12),state,font=LBL,fill='#e0482f' if state=='발사' else '#333333')
    d.rectangle((0,v.H-5,int(v.W*min(1,max(0,a)/1.4)),v.H),fill='#e0a43a')
    return c
def pair(cs,name,a):
    out=Image.new('RGB',(v.W,v.H*2+12),'#ffffff')
    out.paste(v.draw(cs,a,'지금 · '+name).convert('RGB'),(0,0));out.paste(new(cs,a,'시안 · '+name).convert('RGB'),(0,v.H+12))
    ImageDraw.Draw(out).line((0,v.H+6,v.W,v.H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    MID=v.Case(0.5)
    for cs,name,tag in ((v.CASES[1][0],'끝까지','full'),(MID,'중간 (0.5초)','mid'),(v.CASES[0][0],'짧게','short')):
        for suf,step in (('slow',.006),('real',.02)):
            fr=[];t=-.08
            while t<cs.end+.3:fr.append(pair(cs,name,t));t+=step
            fr[0].save(OUT/f'charge-cmp-{tag}-{suf}.gif',save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    cs=v.CASES[1][0]
    rows=[('모으는 중 0.4초',.4),('모으는 중 0.7초',.7),('쏘는 순간',cs.r+.03),('맞을 때',cs.r+cs.hit_t+.05)]
    ch=v.H-40;sheet=Image.new('RGB',(v.W,len(rows)*(ch*2+60)),'#ffffff');d=ImageDraw.Draw(sheet);y=0
    for label,a in rows:
        d.text((10,y+6),label,font=LBL,fill='#222222')
        sheet.paste(v.draw(cs,a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+38));sheet.paste(new(cs,a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+42+ch))
        d.text((v.W-70,y+42),'지금',font=LBL,fill='#888888');d.text((v.W-70,y+46+ch),'시안',font=LBL,fill='#e0482f');y+=ch*2+60
    sheet.save(OUT/'charge-cmp-four.png');print('ok')
