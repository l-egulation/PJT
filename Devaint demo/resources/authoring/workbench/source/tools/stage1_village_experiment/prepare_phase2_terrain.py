"""Register AI-recolored native-alpha parts to the approved full sprite canvases.

This imports generated artwork, not synthetic replacement drawings. Each bright
part keeps its OWN alpha; no sketch mask is reapplied to a differently shaped art.
"""
from pathlib import Path
import hashlib, json, subprocess
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='experiment/stage1-village-reference'
SRC=ROOT/'output/imagegen/phase2-production-terrain-20261027'
BASE=ROOT/'unity/Assets/Stage1VillageExperiment/Resources/Village/ReferenceGround'
OUT=BASE/'Bright'
OUT.mkdir(parents=True,exist_ok=True)
library=json.loads((BASE/'library.json').read_text())
entries={e['name']:e for e in library['entries']}
records=[]

def bounds(im,threshold=8):
    a=np.asarray(im.getchannel('A'))
    y,x=np.where(a>threshold)
    if not len(x): raise ValueError('Empty generated sprite')
    return (int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1))

def register(name,im,source):
    reference=Image.open(BASE/(name+'.png')).convert('RGBA')
    # Reposition and resample whole native-alpha component, including its padding.
    # The generated alpha is never multiplied by the reference silhouette.
    # Generated alpha=1 flecks can exist anywhere on an otherwise transparent
    # canvas. Exclude these invisible flecks from registration, not from the art.
    sourcebox=bounds(im,1);targetbox=bounds(reference,1)
    l,t,r,b=targetbox
    part=im.crop(sourcebox).resize((r-l,b-t),Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',reference.size);canvas.alpha_composite(part,(l,t))
    canvas.save(OUT/(name+'.png'))
    sa=np.asarray(reference.getchannel('A'))>166;ca=np.asarray(canvas.getchannel('A'))>166
    intersection=int(np.count_nonzero(sa&ca));union=int(np.count_nonzero(sa|ca))
    records.append(dict(name=name,source=source,sourceBounds=list(sourcebox),targetBounds=list(targetbox),size=list(canvas.size),
                        opaqueSilhouetteIoU=round(intersection/max(1,union),4),alpha='generated native alpha, no donor mask'))

caps=Image.open(SRC/'caps-color.png').convert('RGBA')
w,h=caps.size
for n,name in enumerate(['cap-one-a','cap-one-b','cap-wide','cap-short']):
    x=(n%2)*w//2;y=(n//2)*h//2
    register(name,caps.crop((x,y,x+w//2,y+h//2)),'caps-color.png quadrant '+str(n))
register('platform',Image.open(SRC/'platform-color.png').convert('RGBA'),'platform-color.png')

def quilt_axis(a,period,overlap,vertical=False):
    if vertical:a=a.swapaxes(0,1)
    cost=np.mean((a[:,:overlap].astype(float)-a[:,period:period+overlap].astype(float))**2,axis=2)
    cost[:,0]+=1e8;cost[:,-1]+=1e8;dp=cost.copy();back=np.zeros(cost.shape,dtype=int)
    dp[0]=1e12;dp[0,overlap//2]=cost[0,overlap//2]
    for y in range(1,len(a)):
        for x in range(overlap):
            lo=max(0,x-1);hi=min(overlap,x+2);j=lo+int(np.argmin(dp[y-1,lo:hi]));back[y,x]=j;dp[y,x]+=dp[y-1,j]
    seam=np.zeros(len(a),dtype=int);seam[-1]=overlap//2
    for y in range(len(a)-2,-1,-1):seam[y]=back[y+1,seam[y+1]]
    result=a[:,:period].copy()
    for y,x in enumerate(seam):result[y,:x]=a[y,period:period+x]
    return result.swapaxes(0,1) if vertical else result

# Seam quilting chooses original generated pixels, without blurring drawn edges.
soil=Image.open(SRC/'soil-color.png').convert('RGB').resize((192,128),Image.Resampling.LANCZOS)
soil=quilt_axis(quilt_axis(np.asarray(soil),160,32),96,32,True)
Image.fromarray(soil).save(OUT/'soil.png')
manifest=dict(status='production-candidate / Unity visual review required',reference='Stage1VillageReference/ArtDirection/phase2-selected/05-matched-terrain.png',
    sourceDirectory=str(SRC.relative_to(ROOT)),registration=records,
    soilSize=[160,96],canvasRule='Same canvas and placement anchors as approved sketch; complete native-alpha generated components fitted without clipping.',
    images=[dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(OUT.glob('*.png'))])
(OUT/'library.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(records,ensure_ascii=False,indent=2))
