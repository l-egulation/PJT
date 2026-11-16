# 샷건 지금 vs 시안 v1. 그림만 재현(판정 시뮬레이션 아님). 피해·탄 경로·반동 거리는 지금과 같다.
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import shotgun_view as v

C=Path(__file__).resolve().parent/'candidate-v1'
cand=json.loads((C/'candidate.json').read_text(encoding='utf-8'))
def load(k,n):return [Image.open(C/k/f'F{i:02d}.png').convert('RGBA') for i in range(n)]
PEL,POP,DUST,WIND,HIT=load('pellet',2),load('pop',4),load('dust',4),load('wind',2),load('hit',5)
R=v.R;TIP=(1.0625,1.109);AXIS=TIP[1]-v.SPAWN_Y
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)

def put(c,im,ux,uy,pivot,rot=0):
    k=v.U/32;im=im.resize((round(im.width*k),round(im.height*k)),Image.Resampling.NEAREST)
    pvx,pvy=pivot[0]*k,pivot[1]*k
    if rot:
        big=Image.new('RGBA',(im.width*2,im.height*2),(0,0,0,0));big.alpha_composite(im,(round(im.width-pvx),round(im.height-pvy)))
        im=big.rotate(rot,resample=Image.Resampling.NEAREST,center=(im.width,im.height));pvx,pvy=im.width/2,im.height/2
    x,y=v.px(ux,uy);c.alpha_composite(im,(round(x-pvx),round(y-pvy)))

def pellets(a):
    """각 탄: (화면 위치, 각도, 살아있나, 맞은 시각·자리)"""
    t=a-v.IMPACT;ox,oy=v.START+v.SPAWN_X,v.SPAWN_Y;out=[]
    for ang in R['angles']:
        ra=math.radians(ang);cs,sn=math.cos(ra),math.sin(ra)
        # 맞는 시각: 허수아비 앞면 / 바닥 / 사거리
        th=(v.TARGET-R['radius']-ox)/(cs*R['speed']);yh=oy+sn*R['speed']*th
        life=R['distance']/R['speed'];hit=None
        if 0<=yh<=2 and th<life:hit=(th,v.TARGET,yh,'enemy')
        if sn<0:
            tg=(R['radius']-oy)/(sn*R['speed'])
            if tg<life and (hit is None or tg<hit[0]):hit=(tg,ox+cs*R['speed']*tg,R['radius'],'ground')
        end=hit[0] if hit else life
        out.append((ang,ox+cs*R['speed']*t,oy+sn*R['speed']*t,0<=t<end,hit,t))
    return out

def new(a,label):
    c,d=v.scene(a);bx,by=v.player_pos(a);t=a-v.IMPACT
    v.paste(c,v.body(a),bx,by)
    # 반동 바람 자국: 반동 중 몸 앞쪽
    if 0.02<=t<v.RT:put(c,WIND[int(t/.05)%2],bx+.4,by+1.05,cand['wind']['pivot_px'])
    # 반동 먼지: 출발 자리, 착지 자리
    for t0,x0 in ((0,v.START),(v.RT,v.START-v.RD)):
        s=t-t0
        if 0<=s<sum(cand['dust']['seconds']):put(c,DUST[v.pick(cand['dust']['seconds'],s)],x0+.3,0,cand['dust']['pivot_px'])
    # 기존 검은 총구 A: 쏜 자리에 고정
    if 0<=t<v.MZ_DUR:v.paste(c,v.MZ['frames'][v.pick(v.MZ['seconds'],t)],v.START+v.MX,v.MY,v.fx.get('muzzleScale',1))
    if t>=0:
        for ang,x,y,alive,hit,_ in pellets(a):
            if alive and x>v.START+TIP[0]:put(c,PEL[int(t/.04)%2],x,y+AXIS,cand['pellet']['pivot_px'],rot=ang)
            if hit:
                h=t-hit[0]
                sec=cand['hit']['seconds'];first=0 if hit[3]=='enemy' else 2   # 지형은 작은 뒷부분 세 장만
                if 0<=h<sum(sec[first:]):put(c,HIT[first+v.pick(sec[first:],h)],hit[1],hit[2]+AXIS,cand['hit']['pivot_px'])
    if 0<=t<sum(cand['pop']['seconds']):put(c,POP[v.pick(cand['pop']['seconds'],t)],v.START+TIP[0],TIP[1],cand['pop']['pivot_px'])
    d.text((14,10),label,font=LBL,fill='#e0482f')
    d.rectangle((0,v.H-5,int(v.W*min(1,max(0,a)/v.END)),v.H),fill='#e0a43a')
    return c

def pair(a):
    out=Image.new('RGB',(v.W,v.H*2+12),'#ffffff')
    out.paste(v.draw(a,'지금').convert('RGB'),(0,0));out.paste(new(a,'시안').convert('RGB'),(0,v.H+12))
    ImageDraw.Draw(out).line((0,v.H+6,v.W,v.H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    OUT=Path(__file__).resolve().parent
    for name,step in (('shotgun-cmp-slow.gif',.005),('shotgun-cmp-real.gif',.02)):
        fr=[];t=-.08
        while t<.9:fr.append(pair(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    F=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
    rows=[('쏘는 순간',v.IMPACT+.03),('날아갈 때',v.IMPACT+.12),('맞을 때',v.IMPACT+.2),('착지',v.IMPACT+v.RT+.03)]
    ch=v.H-40;sheet=Image.new('RGB',(v.W,len(rows)*(ch*2+60)),'#ffffff');d=ImageDraw.Draw(sheet);y=0
    for label,a in rows:
        d.text((10,y+6),label,font=F,fill='#222222')
        sheet.paste(v.draw(a).convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+38));sheet.paste(new(a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+42+ch))
        d.text((v.W-70,y+42),'지금',font=F,fill='#888888');d.text((v.W-70,y+46+ch),'시안',font=F,fill='#e0482f');y+=ch*2+60
    sheet.save(OUT/'shotgun-cmp-four.png');print('ok')
