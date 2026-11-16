# 발사기 확정 연출 다섯 개 모음(시안 쪽만, 실제 속도). 각 검토 폴더의 비교 스크립트를 그대로 불러 그린다.
import sys, importlib
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent;O=HERE.parent
for d in ('launcher-basic-breakdown-20261106','launcher-laser-skill-20261106','launcher-shotgun-review-20261106','launcher-grenade-review-20261106','launcher-charge-review-20261106'):
    sys.path.insert(0,str(O/d))
bv=importlib.import_module('view');bv.U=64;bv.W,bv.H=1000,250;bv.FEET=(70,215)   # 기본포 검토판은 96px 단위였다 → 다른 넷과 같은 64px
bc=importlib.import_module('compare')
ls=importlib.import_module('laser_skill')
sc=importlib.import_module('shotgun_compare')
gc=importlib.import_module('grenade_compare')
g2=importlib.import_module('grenade_v2')
cs=importlib.import_module('charge_steps')
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22);SM=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',17)
FULL=cs.CASES[2][0];ONE=cs.CASES[0][0]
ROWS=[
 ('기본 공격 · 볼 탄환','S, 잉크 상관없음',lambda a:bc.new(a)),
 ('레이저','범위 잉크 D',lambda a:ls.cand_(a)),
 ('샷건','이동 잉크 D',lambda a:sc.new(a,'')),
 ('유탄','후속타 잉크 D',lambda a:g2.new2(a,'')),
 ('모아쏘기 3단','미장착 D · 끝까지',lambda a:cs.new(FULL,a,'')),
 ('모아쏘기 1단','미장착 D · 짧게',lambda a:cs.new(ONE,a,'')),
]
W=1000
def frame(a):
    imgs=[]
    for name,sub,f in ROWS:
        im=f(a).convert('RGB');row=Image.new('RGB',(W,im.height),'#ffffff');row.paste(im,(0,0))
        d=ImageDraw.Draw(row);d.rectangle((0,0,330,64),fill='#ffffff')
        d.text((14,8),name,font=LBL,fill='#e0482f');d.text((14,36),sub,font=SM,fill='#999999');imgs.append(row)
    out=Image.new('RGB',(W,sum(i.height for i in imgs)+8*(len(imgs)-1)),'#ffffff');y=0;d=ImageDraw.Draw(out)
    for k,i in enumerate(imgs):
        out.paste(i,(0,y));y+=i.height
        if k<len(imgs)-1:d.line((0,y+3,W,y+3),fill='#bbbbbb',width=2);y+=8
    return out
if __name__=='__main__':
    fr=[];t=-.1
    while t<2.3:fr.append(frame(t));t+=.02
    fr[0].save(HERE/'launcher-all-real.gif',save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=True)
    print(len(fr),fr[0].size)
