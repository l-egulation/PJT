# 기본포 지금 vs 시안 v1 비교. 그림만 재현(판정 시뮬레이션 아님). 탄 판정 원·속도·생성점은 그대로 쓴다.
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import view as v

C=Path(__file__).resolve().parent/'candidate-v1'
cand=json.loads((C/'candidate.json').read_text(encoding='utf-8'))
MPF=cand['muzzle_per_frame']
BUL=[Image.open(C/'bullet'/f'F{i:02d}.png').convert('RGBA') for i in range(4)]
HIT=[Image.open(C/'hit'/f'F{i:02d}.png').convert('RGBA') for i in range(6)]
HSEC=cand['hit']['seconds']
POP=[Image.open(C/'pop'/f'F{i:02d}.png').convert('RGBA') for i in range(4)]
PSEC=cand['pop']['seconds']
VIS_Y=0.875  # 총구 축. 판정 원 중심(1.0)보다 0.125 아래에 그림만 그린다.
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',24)

def put(c,im,ux,uy,pivot):
    k=v.U/32;im=im.resize((round(im.width*k),round(im.height*k)),v.Image.Resampling.NEAREST)
    x,y=v.px(ux,uy);c.alpha_composite(im,(round(x-pivot[0]*k),round(y-pivot[1]*k)))

def now(a):
    c=Image.new('RGBA',(v.W,v.H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,v.FEET[1],v.W,v.FEET[1]),fill='#d6d6d6',width=2)
    v.paste(c,v.body(a),0,0)
    x0,y0=v.px(v.TARGET_X,0);d.rectangle((x0,y0-2*v.U,x0+.8*v.U,y0),outline='#b9b9b9',width=3)
    if a>=v.IMPACT:
        bx=v.SPAWN[0]+v.SPEED*(a-v.IMPACT)
        if bx+v.RADIUS<v.TARGET_X:
            cx,cy=v.px(bx,v.SPAWN[1]);r=v.RADIUS*v.U;d.ellipse((cx-r,cy-r,cx+r,cy+r),fill=(27,22,22,255))
    if 0<=a-v.IMPACT<v.MUZ_DUR:v.paste(c,v.MUZ['frames'][v.pick(v.MUZ['seconds'],a-v.IMPACT)],v.MX,v.MY)
    return c

def new(a):
    c=Image.new('RGBA',(v.W,v.H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,v.FEET[1],v.W,v.FEET[1]),fill='#d6d6d6',width=2)
    frame=v.body(a);v.paste(c,frame,0,0)
    x0,y0=v.px(v.TARGET_X,0);d.rectangle((x0,y0-2*v.U,x0+.8*v.U,y0),outline='#b9b9b9',width=3)
    fa=a-v.IMPACT
    if 0<=fa<v.MUZ_DUR:
        mx,my=MPF[frame.split('/')[-1]] if frame in v.clip['frames'] else (v.MX,v.MY)
        v.paste(c,v.MUZ['frames'][v.pick(v.MUZ['seconds'],fa)],mx,my)
        if fa<sum(PSEC):put(c,POP[v.pick(PSEC,fa)],mx,my,cand['pop']['pivot_px'])
    if a>=v.IMPACT:
            bx=v.SPAWN[0]+v.SPEED*(a-v.IMPACT)
            if v.MX<=bx and bx+v.RADIUS<v.TARGET_X:
                seen=(bx-v.MX)/v.SPEED
                k=0 if seen<.03 else 1+int((seen-.03)/.04)%3
                put(c,BUL[k],bx,VIS_Y,cand['bullet']['pivot_px'])
    ha=a-v.HIT_AT
    if 0<=ha<sum(HSEC):put(c,HIT[v.pick(HSEC,ha)],v.TARGET_X,VIS_Y,cand['hit']['pivot_px'])
    return c

def pair(a,slow):
    out=Image.new('RGB',(v.W,v.H*2+12),'#ffffff')
    for k,(img,name) in enumerate([(now(a),'지금'),(new(a),'시안')]):
        d=ImageDraw.Draw(img);d.text((16,14),name,font=LBL,fill='#888888' if k==0 else '#e0482f')
        p=v.phase(a)
        if p:d.text((v.W-110,14),p,font=LBL,fill='#333333')
        d.rectangle((0,v.H-6,int(v.W*min(1,max(0,a)/v.END)),v.H),fill='#e0a43a')
        out.paste(img.convert('RGB'),(0,k*(v.H+12)))
    ImageDraw.Draw(out).line((0,v.H+6,v.W,v.H+6),fill='#bbbbbb',width=2)
    return out
def gif(name,step):
    fr=[];t=-.12
    while t<.6:fr.append(pair(t,step<.01));t+=step
    fr[0].save(v.OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
gif('cmp-slow.gif',.005);gif('cmp-real.gif',.02)

# 세 곳 확대: 왼쪽 지금, 오른쪽 시안
F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
def crop(img,ux,uy,w=2.4,h=1.5):
    x,y=v.px(ux,uy);box=(round(x-w/2*v.U),round(y-h/2*v.U),round(x+w/2*v.U),round(y+h/2*v.U))
    base=Image.new('RGBA',(v.W+400,v.H),'#ffffff');base.alpha_composite(img)
    return base.crop(box).resize((round(w*v.U*1.4),round(h*v.U*1.4)),Image.Resampling.NEAREST)
rows=[('1. 쏘는 순간',v.IMPACT+.01,1.7,.95),
      ('2. 회수 때 불꽃 위치',.2,1.6,.9),
      ('3. 명중',v.HIT_AT+.05,v.TARGET_X+.5,.9)]
cells=[]
for title,a,ux,uy in rows:
    cells.append((title,crop(now(a),ux,uy),crop(new(a),ux,uy)))
cw,ch=cells[0][1].size
sheet=Image.new('RGB',(cw*2+30,len(cells)*(ch+60)+40),'#ffffff');d=ImageDraw.Draw(sheet)
d.text((10,6),'지금',font=F,fill='#888888');d.text((cw+30,6),'시안',font=F,fill='#e0482f')
for i,(title,a,b) in enumerate(cells):
    y=40+i*(ch+60);d.text((10,y),title,font=F,fill='#222222')
    sheet.paste(a.convert('RGB'),(10,y+34));sheet.paste(b.convert('RGB'),(cw+20,y+34))
    d.rectangle((10,y+34,10+cw,y+34+ch),outline='#dddddd');d.rectangle((cw+20,y+34,cw+20+cw,y+34+ch),outline='#dddddd')
sheet.save(v.OUT/'cmp-three.png')
print('ok')
