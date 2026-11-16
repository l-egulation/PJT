# 유탄 v1(확정) vs v2(pixelfx 폭발). 탄·연기 꼬리·총구·파편 탄은 v1 그대로, 본체·파편 폭발만 pixelfx 로 바꾼다.
# 맞은 것이 적·만료(공중)면 air, 바닥이면 ground(땅에서 솟는 반구, 원점 = 땅).
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import grenade_view as v, grenade_compare as gc

OUT=Path(__file__).resolve().parent
def clip(name):
    d=OUT/'pfx'/name;m=json.loads((d/'meta.json').read_text(encoding='utf-8'))
    m['img']=[Image.open(d/f'{i:02d}.png').convert('RGBA') for i in range(m['frames'])];return m
PFX={k:clip(k) for k in ('main_air','main_ground','frag_air','frag_ground')}
LBL=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',22)
def kind(at,r):
    return 'ground' if v.touches(*at,r)=='ground' else 'air'
def blast(c,name,age,at,ground):
    P=PFX[name];i=int(age*P['fps'])
    if 0<=i<P['frames']:gc.put(c,P['img'][i],at[0],0 if ground else at[1],P['origin_px'])
def new2(a,label):
    # v1 그림에서 폭발만 빼고 그린 뒤 pixelfx 폭발을 얹는다
    keepB,keepF=gc.BLAST,gc.FBLAST
    gc.BLAST=[Image.new('RGBA',(1,1))]*len(keepB);gc.FBLAST=[Image.new('RGBA',(1,1))]*len(keepF)
    c=gc.new(a,label);gc.BLAST,gc.FBLAST=keepB,keepF
    s=a-v.T0
    g=kind(v.P_MAIN,v.B['radius'])=='ground'
    blast(c,'main_ground' if g else 'main_air',s,v.P_MAIN,g)
    for f in v.FRAGS:
        g=kind(f[3],v.B['fragmentRadius'])=='ground'
        blast(c,'frag_ground' if g else 'frag_air',s-f[2],f[3],g)
    return c
def pair(a):
    out=Image.new('RGB',(v.W,v.H*2+12),'#ffffff')
    out.paste(gc.new(a,'v1 (지금 확정)').convert('RGB'),(0,0));out.paste(new2(a,'v2 시안').convert('RGB'),(0,v.H+12))
    ImageDraw.Draw(out).line((0,v.H+6,v.W,v.H+6),fill='#bbbbbb',width=2)
    return out
if __name__=='__main__':
    for name,step in (('grenade-v2-slow.gif',.006),('grenade-v2-real.gif',.02)):
        fr=[];t=-.08
        while t<v.T0+1.25:fr.append(pair(t));t+=step
        fr[0].save(OUT/name,save_all=True,append_images=fr[1:],duration=20,loop=0,optimize=False)
    rows=[('터질 때',v.T0+.07),('불덩이',v.T0+.17),('파편 터짐',v.T0+.45),('연기',v.T0+.75)]
    ch=v.H-40;sheet=Image.new('RGB',(v.W,len(rows)*(ch*2+60)),'#ffffff');d=ImageDraw.Draw(sheet);y=0
    for label,a in rows:
        d.text((10,y+6),label,font=LBL,fill='#222222')
        sheet.paste(gc.new(a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+38));sheet.paste(new2(a,'').convert('RGB').crop((0,35,v.W,v.H-5)),(0,y+42+ch))
        d.text((v.W-70,y+42),'v1',font=LBL,fill='#888888');d.text((v.W-70,y+46+ch),'v2',font=LBL,fill='#e0482f');y+=ch*2+60
    sheet.save(OUT/'grenade-v2-four.png');print('ok')
