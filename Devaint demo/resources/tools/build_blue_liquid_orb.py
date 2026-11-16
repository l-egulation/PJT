"""Pixel retouch of the preserved Blue orb: five colors and liquid poses."""
from pathlib import Path
import json,math
import numpy as np
from PIL import Image,ImageDraw
from scipy.ndimage import binary_fill_holes,binary_opening,binary_closing
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
OUT=BASE/'blue-palm-return/liquid-orb'
OUT.mkdir(exist_ok=True)
src=Image.open(BASE/'blue-static-orb/blue-ink-orb-original.png').convert('RGBA')
a=np.array(src);y,x=np.where(a[:,:,3]>128)
src=src.crop((x.min(),y.min(),x.max()+1,y.max()+1)).resize((64,64),Image.Resampling.NEAREST)
mask=np.array(src)[:,:,3]>128
mask=binary_fill_holes(binary_closing(binary_opening(mask,iterations=1),iterations=1))
pal=['#171824','#30243f','#51405e','#566775','#8b9b9d']
frames=[]
specs=[(1,.98,0),(1.02,1.02,1),(.98,1.04,2),(1.01,1,1),(1.18,.76,0),(1.12,.87,1),(1.06,.94,2),(1.14,.86,1)]
for i,(sx,sy,wave) in enumerate(specs):
    # Fixed canvas and center, preserving the original orb silhouette as input.
    yy,xx=np.mgrid[:80,:112];cx=48;cy=38
    wobble=np.sin((yy/10)+wave)*(.7 if i<4 else 1.2)
    px=np.rint((xx-cx-wobble)/sx+32).astype(int)
    py=np.rint((yy-cy)/sy+32).astype(int)
    valid=(px>=0)&(px<64)&(py>=0)&(py<64)
    m=np.zeros((80,112),bool);m[valid]=mask[py[valid],px[valid]]
    mi=Image.fromarray(np.uint8(m)*255);d=ImageDraw.Draw(mi)
    if i<4:
        d.ellipse((53,60+wave,58,67+wave),fill=255)
    elif i>4:
        # Leftward flight: heavy leading mass, two short trailing filaments.
        d.polygon([(68,25),(91+wave*2,29),(101,25+wave),(94,33),(72,40)],fill=255)
        d.polygon([(69,43),(91,49+wave),(83,50),(66,49)],fill=255)
        d.rectangle((102,33+wave,104,35+wave),fill=255)
    im=Image.new('RGBA',(112,80));c=Image.new('RGB',(112,80),pal[0]);d=ImageDraw.Draw(c)
    # Large material planes instead of internal swirls or a luminous outline.
    d.polygon([(14,32),(23,16),(39,9),(57,10),(73,21),(78,37),(66,48),(48,52),(28,43)],fill=pal[1])
    d.polygon([(19,28),(27,18),(42,12),(59,16),(67,24),(56,32),(37,35),(24,33)],fill=pal[2])
    d.polygon([(23,23),(30,17),(40,14),(51,16),(49,21),(38,22),(32,27),(25,28)],fill=pal[3])
    d.polygon([(28,21),(32,18),(40,17),(43,19),(36,20),(33,23)],fill=pal[4])
    if i>4:
        d.polygon([(65,30),(83,30),(91,29),(83,33),(69,35)],fill=pal[1])
    im.paste(c,(0,0));im.putalpha(mi)
    im.save(OUT/f'{i:02}.png');frames.append(im)
frames[0].save(OUT/'liquid-cycle.webp',save_all=True,append_images=frames[1:],duration=[180]*4+[65,110,110,110],loop=0,lossless=True)
board=Image.new('RGB',(896,320),'#18222e');d=ImageDraw.Draw(board)
for i,im in enumerate(frames):
    large=im.resize((224,160),Image.Resampling.NEAREST)
    board.paste(large,(i%4*224,i//4*160),large)
board.save(OUT/'poses.png')
(OUT/'material.json').write_text(json.dumps({'status':'review-pending','source':'../../blue-static-orb/blue-ink-orb-original.png','palette':pal,'canvas':[112,80],'anchor':[48,38],'chargeFrames':[0,1,2,3],'impactFrame':4,'flightFrames':[5,6,7],'direction':'left','note':'Pixel material/deformation study, independent projectile. No gameplay value changes.'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
asset=json.loads((BASE/'asset.json').read_text(encoding='utf-8'))
asset['files']=sorted(str(p.relative_to(BASE)).replace('\\','/') for p in BASE.rglob('*') if p.is_file() and p.name!='asset.json')
asset['motionReview']['blueLiquidOrb']='review-pending'
(BASE/'asset.json').write_text(json.dumps(asset,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
cat=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
for e in cat['assets']:
    if e['id']==asset['id']: e['fileCount']=len(asset['files'])
(ROOT/'catalog.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('8 liquid poses; 5 colors; original preserved')
