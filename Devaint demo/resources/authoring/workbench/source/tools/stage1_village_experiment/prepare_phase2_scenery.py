"""Slice actual transparent paired sprite sheets without masking either partner."""
from pathlib import Path
import hashlib,json,subprocess
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='experiment/stage1-village-reference'
SRC=ROOT/'output/imagegen/phase2-production-scenery-20261027'
BASE=ROOT/'unity/Assets/Stage1VillageExperiment/Resources/Village'
records=[]

def visible_bounds(im):
    y,x=np.where(np.asarray(im.getchannel('A'))>8)
    if not len(x):raise ValueError('Empty alpha component')
    return (max(0,int(x.min())-2),max(0,int(y.min())-2),min(im.width,int(x.max())+3),min(im.height,int(y.max())+3))

def pair(name,im,target_width,target_height=None,source=None):
    sides=[im.crop((0,0,im.width//2,im.height)),im.crop((im.width//2,0,im.width,im.height))]
    boxes=[visible_bounds(s) for s in sides];parts=[s.crop(b) for s,b in zip(sides,boxes)]
    width=target_width;height=target_height or round(width*sum(p.height/p.width for p in parts)/2)
    outputs=[]
    for state,part in zip(['sketch','color'],parts):
        # Complete component resampling, native own alpha. No background removal,
        # cross-state alpha replacement, tile slicing or filling transparent holes.
        part=part.resize((width,height),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(width+8,height+8));canvas.alpha_composite(part,(4,4))
        dest=BASE/('reference-sketch/color/props' if state=='sketch' else 'color/props')
        dest.mkdir(parents=True,exist_ok=True);path=dest/(name+'.png');canvas.save(path)
        outputs.append((path,canvas))
    a=np.asarray(outputs[0][1].getchannel('A'))>166;b=np.asarray(outputs[1][1].getchannel('A'))>166
    # A crop has its own guard pixels in addition to the outer 4px padding.
    # Measure the shared physical footline; do not mistake rare hanging foliage
    # or faint resampling pixels for the base of a building/bench.
    contacts=[]
    for _,canvas in outputs:
        alpha=np.asarray(canvas.getchannel('A'))>166
        bottoms=[np.flatnonzero(alpha[:,x])[-1] for x in range(canvas.width) if alpha[:,x].any()]
        contacts.append(int(round(np.percentile(bottoms,90))))
    ground_offset=height+8-max(contacts)-1
    records.append(dict(kind=name,width=width+8,height=height+8,groundOffsetPixels=ground_offset,
        source=source,contactRows=contacts,
        silhouetteIoU=round(np.count_nonzero(a&b)/max(1,np.count_nonzero(a|b)),4),
        sourceBounds=[list(b) for b in boxes],
        files=[dict(path=str(p.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p,_ in outputs]))

for name,width in [('residential',240),('market',248),('plaza',210)]:
    source=ROOT/'output/imagegen/phase2-scene-grounding-20261027/plaza-grounded-pair.png' if name=='plaza' else SRC/(name+'-pair.png')
    pair('phase2_'+name,Image.open(source).convert('RGBA'),width,source=source.relative_to(ROOT).as_posix())

vegetation=Image.open(SRC/'vegetation-pair-clean.png').convert('RGBA')
row=np.count_nonzero(np.asarray(vegetation.getchannel('A'))>128,axis=1)>8
intervals=[];start=None
for y,value in enumerate(list(row)+[False]):
    if value and start is None:start=y
    if not value and start is not None:
        if y-start>10:intervals.append([start,y])
        start=None
merged=[]
for start,end in intervals:
    if merged and start-merged[-1][1]<12:merged[-1][1]=end
    else:merged.append([start,end])
if len(merged)!=4:raise ValueError('Expected four isolated plant rows, got '+str(merged))
for (name,width),(top,bottom) in zip([('tree',180),('cypress',76),('shrub',100),('grass',36)],merged):
    pair('phase2_'+name,vegetation.crop((0,max(0,top-4),vegetation.width,min(vegetation.height,bottom+4))),width)

manifest=dict(status='production-candidate / actual-camera review required',sourceDirectory=str(SRC.relative_to(ROOT)).replace('\\','/'),
    pairing='Each state uses its own generated native alpha; shared canvas and floor anchor; no donor masks.',entries=records)
(BASE/'phase2-scenery.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([{'kind':r['kind'],'size':[r['width'],r['height']],'IoU':r['silhouetteIoU']} for r in records],indent=2))
