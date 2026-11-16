"""Register complete native-alpha drawings; the 32px collision grid never clips their canvas."""
from pathlib import Path
import hashlib,json,subprocess
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='experiment/stage1-village-reference'
SRC=ROOT/'output/imagegen/platform-unified'
OUT=ROOT/'unity/Assets/Stage1VillageExperiment/Resources/Village/ReferenceGround'
OUT.mkdir(parents=True,exist_ok=True)
entries=[]
def save(name,source,rect,scale,anchor,logical,hard=False):
    im=Image.open(SRC/source).convert('RGBA').crop(rect)
    w,h=round(im.width*scale),round(im.height*scale)
    im=im.resize((w,h),Image.Resampling.LANCZOS)
    # Padding is outside the entire drawn crop, never a crop to a tile-sized box.
    padded=Image.new('RGBA',(w+8,h+8));padded.alpha_composite(im,(4,4));padded.save(OUT/(name+'.png'))
    entries.append(dict(name=name,width=w+8,height=h+8,anchorX=4+(anchor[0]-rect[0])*scale,anchorY=4+(anchor[1]-rect[1])*scale,logicalWidth=logical[0],logicalHeight=logical[1],hardExterior=hard,source=source,sourceRect=list(rect)))
# Comparable 24/32/40px stones. Tiny source stones are not enlarged into fat blocks.
save('cap-one-a','flat-matched.png',(271,292,455,462),32/174,(276,301),(32,0),True)
save('cap-one-b','flat-matched.png',(745,292,923,462),32/167,(750,301),(32,0),True)
save('cap-wide','flat-matched.png',(912,292,1126,462),40/202,(916,301),(40,0),True)
save('cap-short','flat-matched.png',(75,292,210,462),24/116,(85,301),(24,0),True)
# The continuous body and overlapping walls own corners; retired overlays stay archived.
save('wall-left','walls.png',(410,49,580,798),128/749,(430,49),(0,128),True)
save('wall-right','walls.png',(960,49,1136,798),128/749,(1110,49),(0,128),True)
save('underside','walls.png',(66,865,1470,965),256/1404,(66,944),(256,0),True)
save('platform','../reference-ground/platform-native-alpha.png',(150,65,1750,809),160/1566,(167,184),(160,0))

def cycle(a,period,overlap,vertical=False):
    if vertical:a=a.swapaxes(0,1)
    cost=np.mean((a[:,:overlap].astype(float)-a[:,period:period+overlap].astype(float))**2,axis=2)
    cost[:,0]+=1e8;cost[:,-1]+=1e8;dp=cost.copy();back=np.zeros(cost.shape,dtype=int)
    dp[0]=1e12;dp[0,overlap//2]=cost[0,overlap//2]
    for y in range(1,len(a)):
        for x in range(overlap):
            lo=max(0,x-1);hi=min(overlap,x+2);j=lo+int(np.argmin(dp[y-1,lo:hi]));back[y,x]=j;dp[y,x]+=dp[y-1,j]
    seam=np.zeros(len(a),dtype=int);seam[-1]=overlap//2
    for y in range(len(a)-2,-1,-1):seam[y]=back[y+1,seam[y+1]]
    out=a[:,:period].copy()
    for y,x in enumerate(seam):out[y,:x]=a[y,period:period+x]
    return out.swapaxes(0,1) if vertical else out
body=Image.open(SRC/'body.png').convert('RGB');body=body.resize((192,192),Image.Resampling.LANCZOS)
body_height=160
body=cycle(cycle(np.array(body),160,32),160,32,True)
Image.fromarray(body).save(OUT/'body.png')
manifest=dict(reference='Stage1VillageReference/ArtDirection/approved-sketch.png',detailReference='output/imagegen/reference-ground/platform-native-alpha.png',status='user-approved-first-pass',approvalDate='2026-10-26',ppu=32,bodyWidth=160,bodyHeight=body_height,entries=entries,rule='Platform lineweight, hatch and stone scale are the detail standard. Whole native-alpha sprites with contact anchors; no color-alpha clipping or quadrant slicing.',imageFiles=[dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(OUT.glob('*.png'))])
(OUT/'library.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
# A neutral contact sheet is only a review composite; source game PNG alpha is unchanged.
sheet=Image.new('RGBA',(1000,650),(248,237,220,255))
for name,x,y in [('cap-one-a',24,25),('cap-one-b',140,25),('cap-short',260,25),('cap-wide',370,25),('wall-left',570,100),('wall-right',700,100),('platform',570,390),('underside',24,540)]:
    tile=Image.open(OUT/(name+'.png'));sheet.alpha_composite(tile.resize((tile.width*2,tile.height*2),Image.Resampling.NEAREST),(x,y))
sheet.convert('RGB').save(ROOT/'outputs/village/reference-ground/asset-contact-sheet.png')
print('Prepared complete padded sprite canvases, anchored library and source-paper body material.')
