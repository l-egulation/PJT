# 모아쏘기(미장착 D) 지금 연출 · 흰 배경. 코드 계산식대로 그림만 재현(실제 플레이 캡처 아님).
# 몸: clip.Attack(age, ImpactTime, MotionDuration). 충전 중 ImpactTime=0.2, 뗀 뒤 0.2+shift. MotionDuration=BaseEnd 0.5 고정.
from pathlib import Path
import json, re
from PIL import Image, ImageDraw, ImageFont

WT=Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/launcher-skill-parts-20261106')
RES=WT/'unity/Assets/Deviant/Combat/Resources'
OUT=Path(__file__).resolve().parent
cat=json.loads((RES/'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8'))
fx=next(e for e in json.loads((RES/'CharacterV2/ApprovedRangedEffects.json').read_text(encoding='utf-8'))['effects'] if e['form']=='launcher')
sk=json.loads((WT/'data/combat/launcher_skills.json').read_text(encoding='utf-8'))['basic'];RG=sk['ranged']
lset=next(s for s in cat['sets'] if s['form']=='launcher')
clip=next(c for c in lset['clips'] if c['name']=='attack_single');idle=next(c for c in lset['clips'] if c['name']=='idle')
CMIN,CMAX=sk['charge']['min_hold'],sk['charge']['max_hold']
BASE_END=0.5;RECOVER=0.3
SRC_HIT=clip['sourceImpact'];SRC_DUR=sum(clip['seconds'])
MZ=fx['muzzle'];MZ_DUR=sum(MZ['seconds'])
DUMMY=5.0;U=64;W,H=760,250;GROUND=215;X0=40
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22);SM=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',18)
cache={}
def media(n):
    if n not in cache:
        p=RES/(n+'.png');im=Image.open(p).convert('RGBA');m=p.with_suffix('.png.meta').read_text(encoding='utf-8')
        pv=tuple(map(float,re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',m).groups()))
        cache[n]=(im,pv,float(re.search(r'spritePixelsToUnits: ([\d.]+)',m)[1]))
    return cache[n]
def px(ux,uy):return X0+ux*U,GROUND-uy*U
def paste(c,n,ux,uy,scale=1.0):
    im,pv,ppu=media(n);k=U/ppu*scale
    im=im.resize((max(1,round(im.width*k)),max(1,round(im.height*k))),Image.Resampling.NEAREST)
    x,y=px(ux,uy);c.alpha_composite(im,(round(x-im.width*pv[0]),round(y-im.height*(1-pv[1]))))
def pick(sec,t):
    for i,d in enumerate(sec):
        if t<d:return i
        t-=d
    return len(sec)-1
def src(age,impact,end):
    return age/impact*SRC_HIT if age<impact and impact>0 else SRC_HIT+(age-impact)/max(.001,end-impact)*(SRC_DUR-SRC_HIT)
class Case:
    def __init__(s,release):
        s.r=max(CMIN,min(CMAX,release));s.amount=(s.r-CMIN)/(CMAX-CMIN)
        s.radius=RG['radius']+(RG['chargeRadius']-RG['radius'])*s.amount
        s.damage=20+20*s.amount;s.end=s.r+RECOVER
        sx=RG['spawnX'];s.hit_t=(DUMMY-s.radius-sx)/RG['speed']   # 발사 뒤 명중까지
    def impact(s,a):return CMIN if a<s.r else s.r
    def frame(s,a):
        if a<0 or a>=s.end:return idle['frames'][0],0
        i=pick(clip['seconds'],src(a,s.impact(a),BASE_END));return clip['frames'][i],i
def draw(case,a,label):
    c=Image.new('RGBA',(W,H),'#ffffff');d=ImageDraw.Draw(c)
    d.line((0,GROUND,W,GROUND),fill='#d6d6d6',width=2)
    x0,y0=px(DUMMY,0);d.rectangle((x0,y0-2*U,x0+.8*U,y0),outline='#b9b9b9',width=3)
    f,i=case.frame(a);paste(c,f,0,0)
    t=a-case.r
    if 0<=t<MZ_DUR:paste(c,MZ['frames'][pick(MZ['seconds'],t)],clip['muzzleX'][i],clip['muzzleY'][i],fx.get('muzzleScale',1))
    if 0<=t<case.hit_t:
        x,y=px(RG['spawnX']+RG['speed']*t,RG['spawnHeight']);r=case.radius*U
        d.ellipse((x-r,y-r,x+r,y+r),fill=(27,22,22,255))
    e=t-case.hit_t
    if 0<=e<.18:
        k=e/.18;cx,cy=px(DUMMY,RG['spawnHeight']);h=RG['impactBoxSize']/2*U
        ov=Image.new('RGBA',(W,H),(0,0,0,0));ImageDraw.Draw(ov).rectangle((cx-h,cy-h,cx+h,cy+h),fill=(255,89,8,int(255*.4*(1-k))))
        c.alpha_composite(ov)
    d=ImageDraw.Draw(c);d.text((14,10),label,font=LBL,fill='#888888')
    state='충전 중' if 0<=a<case.r else '발사' if case.r<=a<case.r+.06 else ''
    if state and label:d.text((W-110,12),state,font=LBL,fill='#e0482f' if state=='발사' else '#333333')
    d.text((14,40),f'피해 {case.damage:.0f} · 탄 지름 {case.radius*2:.2f}',font=SM,fill='#999999')
    d.rectangle((0,H-5,int(W*min(1,max(0,a)/1.4)),H),fill='#e0a43a')
    return c
CASES=[(Case(0.0),'짧게 (바로 뗌)'),(Case(0.8),'끝까지 (0.8초 자동)')]
def pair(a):
    out=Image.new('RGB',(W,H*2+12),'#ffffff')
    for k,(cs,name) in enumerate(CASES):out.paste(draw(cs,a,name).convert('RGB'),(0,k*(H+12)))
    ImageDraw.Draw(out).line((0,H+6,W,H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    for name,step in (('charge-now-slow.gif',.006),('charge-now-real.gif',.02)):
        fr=[];t=-.08
        while t<1.4:fr.append(pair(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    # 끝까지 모으는 동안 몸 프레임 변화
    cs=CASES[1][0];F=SM;cells=[]
    for a in (.1,.2,.3,.4,.55,.75,.82,1.0):
        im=draw(cs,a,'').convert('RGB').crop((0,62,300,GROUND+6));ImageDraw.Draw(im).text((8,4),f'{a:.2f}초',font=F,fill='#e0482f');cells.append(im)
    sh=Image.new('RGB',(4*310,2*(cells[0].height+10)),'#ffffff')
    for i,im in enumerate(cells):sh.paste(im,((i%4)*310,(i//4)*(im.height+10)))
    sh.save(OUT/'charge-now-body.png')
    for cs,name in CASES:print(name,'release',cs.r,'radius',round(cs.radius,3),'hit after',round(cs.hit_t,3),'end',cs.end)
