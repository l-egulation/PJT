# 유탄 지금 vs 시안 v1. 그림만 재현(판정 단순화). 궤적·폭발 위치·파편 각도와 시각은 지금과 같다.
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import grenade_view as v

C=Path(__file__).resolve().parent/'candidate-v1'
cand=json.loads((C/'candidate.json').read_text(encoding='utf-8'))
def load(k,n):return [Image.open(C/k/f'F{i:02d}.png').convert('RGBA') for i in range(n)]
SHELL,SMOKE,POP,BLAST,FRAG,FBLAST=load('shell',2),load('smoke',4),load('pop',4),load('blast',6),load('frag',2),load('fragblast',5)
B=v.B;TIP=(1.0,1.15625);LIFT=TIP[1]-v.SPAWN[1]
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)

def put(c,im,ux,uy,pivot=None,rot=0):
    k=v.U/32;im=im.resize((round(im.width*k),round(im.height*k)),Image.Resampling.NEAREST)
    pvx,pvy=(pivot[0]*k,pivot[1]*k) if pivot else (im.width/2,im.height/2)
    if rot:
        big=Image.new('RGBA',(im.width*3,im.height*3),(0,0,0,0));big.alpha_composite(im,(round(im.width*1.5-pvx),round(im.height*1.5-pvy)))
        im=big.rotate(rot,resample=Image.Resampling.NEAREST,center=(im.width*1.5,im.height*1.5));pvx,pvy=im.width/2,im.height/2
    x,y=v.px(ux,uy);c.alpha_composite(im,(round(x-pvx),round(y-pvy)))
def main_at(t):
    x=v.O[0]+v.V[0]*t;y=v.O[1]+v.V[1]*t-.5*B['gravity']*t*t
    return x,y+LIFT*max(0,1-t/.08),math.degrees(math.atan2(v.V[1]-B['gravity']*t,v.V[0]))
def frag_at(f,s):
    (sx,sy),fv,tf,pf=f;x=sx+fv[0]*s;y=sy+fv[1]*s-.5*B['fragmentGravity']*s*s
    return x,y,math.degrees(math.atan2(fv[1]-B['fragmentGravity']*s,fv[0]))

def new(a,label):
    c,d=v.scene();v.paste(c,v.body(a),v.START,0);t=a-v.IMPACT;s=a-v.T0
    # 연기 꼬리: 0.03초마다 탄 자리에 남긴다(본체·파편)
    k=0
    while k*.03<min(t,v.T_MAIN):
        e=t-k*.03;x,y,_=main_at(k*.03)
        if x>v.START+TIP[0]+.1 and 0<=e<sum(cand['smoke']['seconds']):put(c,SMOKE[v.pick(cand['smoke']['seconds'],e)],x,y)
        k+=1
    for f in v.FRAGS:
        k=1
        while k*.04<min(s,f[2]):
            e=s-k*.04;x,y,_=frag_at(f,k*.04)
            if 0<=e<sum(cand['smoke']['seconds'][1:]):put(c,SMOKE[1+v.pick(cand['smoke']['seconds'][1:],e)],x,y)
            k+=1
    if 0<=t<v.MZ_DUR:v.paste(c,v.MZ['frames'][v.pick(v.MZ['seconds'],t)],v.START+v.MX,v.MY,v.fx['muzzleScale'])
    if 0<=t<v.T_MAIN:
        x,y,ang=main_at(t)
        if x>v.START+TIP[0]:put(c,SHELL[int(t/.05)%2],x,y,cand['shell']['pivot_px'],ang)
    if 0<=t<sum(cand['pop']['seconds']):put(c,POP[v.pick(cand['pop']['seconds'],t)],v.START+TIP[0],TIP[1],cand['pop']['pivot_px'])
    for f in v.FRAGS:
        if 0<=s<f[2]:x,y,ang=frag_at(f,s);put(c,FRAG[int(s/.04)%2],x,y,cand['frag']['pivot_px'],ang)
        e=s-f[2]
        if 0<=e<sum(cand['fragblast']['seconds']):put(c,FBLAST[v.pick(cand['fragblast']['seconds'],e)],*f[3])
    if 0<=s<sum(cand['blast']['seconds']):put(c,BLAST[v.pick(cand['blast']['seconds'],s)],*v.P_MAIN)
    d.text((14,10),label,font=LBL,fill='#e0482f')
    d.rectangle((0,v.H-5,int(v.W*min(1,max(0,a)/(v.T0+.9))),v.H),fill='#e0a43a')
    return c

def pair(a):
    out=Image.new('RGB',(v.W,v.H*2+12),'#ffffff')
    out.paste(v.draw(a,'지금').convert('RGB'),(0,0));out.paste(new(a,'시안').convert('RGB'),(0,v.H+12))
    ImageDraw.Draw(out).line((0,v.H+6,v.W,v.H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    OUT=Path(__file__).resolve().parent
    for name,step in (('grenade-cmp-slow.gif',.005),('grenade-cmp-real.gif',.02)):
        fr=[];t=-.08
        while t<v.T0+.95:fr.append(pair(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
    rows=[('쏘는 순간',v.IMPACT+.03),('날아갈 때',v.IMPACT+.25),('터질 때',v.T0+.05),('파편',v.T0+.3)]
    ch=v.H-40;sheet=Image.new('RGB',(v.W,len(rows)*(ch*2+60)),'#ffffff');d=ImageDraw.Draw(sheet);y=0
    for label,a in rows:
        d.text((10,y+6),label,font=F,fill='#222222')
        sheet.paste(v.draw(a).convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+38));sheet.paste(new(a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+42+ch))
        d.text((v.W-70,y+42),'지금',font=F,fill='#888888');d.text((v.W-70,y+46+ch),'시안',font=F,fill='#e0482f');y+=ch*2+60
    sheet.save(OUT/'grenade-cmp-four.png');print('ok')
