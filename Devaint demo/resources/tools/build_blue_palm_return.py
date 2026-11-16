"""Extract the user-supplied Blue motion; fixed transform and palette, no redraw."""
from pathlib import Path
import hashlib,json,shutil
import numpy as np
from PIL import Image,ImageDraw,ImageSequence
from scipy.ndimage import label,binary_erosion

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
OUT=BASE/'blue-palm-return'
OUT.mkdir(exist_ok=True)
(OUT/'source').mkdir(exist_ok=True)
(OUT/'frames').mkdir(exist_ok=True)
sources=['acb7b02e-7261-4e87-8155-3f3732a1307c.gif','928b53bb-e976-44a5-bc8c-7d48ed2a75ba.png']
hashes={}
for name in sources:
    src=Path('C:/Users/SSAFY/Downloads')/name
    dst=OUT/'source'/name
    shutil.copy2(src,dst)
    hashes[name]=hashlib.sha256(src.read_bytes()).hexdigest()
    assert hashlib.sha256(dst.read_bytes()).hexdigest()==hashes[name]
sheet=Image.open(OUT/'source'/sources[1]).convert('RGB')
assert sheet.size==(7680,4320)
gif=Image.open(OUT/'source'/sources[0])
durations=[f.info.get('duration',90) for f in ImageSequence.Iterator(gif)]
def cut(im):
    a=np.array(im)
    ids,n=label(a.min(2)<238)
    counts=np.bincount(ids.ravel()); counts[0]=0
    mask=ids==counts.argmax()
    # Preserve intentional negative spaces between the arms and torso.
    return Image.fromarray(np.dstack([a,np.uint8(mask)*255]))
first=cut(sheet.crop((0,0,1280,720)))
b=first.getbbox()
# Ground hood is 32.44px wide; the 150px air fit gave 29.95px.
# Use a common 8% increase so posture length does not shrink the head/torso.
body_height=162
scale=body_height/(b[3]-b[1]); anchor=((b[0]+b[2])/2,b[3])
# Palette comes only from the returned neutral Blue, excluding the white matte.
rgb=np.array(first)[:,:,:3]; valid=binary_erosion(np.array(first)[:,:,3]>0,iterations=3)
sample=Image.fromarray(rgb[valid].reshape(1,-1,3)).quantize(colors=24,method=Image.Quantize.MEDIANCUT)
pal=np.array(sample.getpalette()[:72],dtype=np.int16).reshape(-1,3)
frames=[]
for i in range(36):
    raw=sheet.crop((i%6*1280,i//6*720,(i%6+1)*1280,(i//6+1)*720))
    if i in (15,16):
        # The return baked a white motion streak over the extended forearm.
        # Restore only that pale streak from the matching settled arm (18).
        ref=np.array(sheet.crop((5*1280,2*720,6*1280,3*720)))
        r=np.array(raw)
        region=np.zeros(r.shape[:2],dtype=bool); region[170:240,485:610]=True
        streak=region & (r.min(2)>170) & (r.max(2)-r.min(2)<65)
        r[streak]=ref[streak]
        raw=Image.fromarray(r)
    sprite=cut(raw)
    # One common affine transform. Never fit each frame to its own bounds.
    native=sprite.transform((320,200),Image.Transform.AFFINE,(1/scale,0,anchor[0]-180/scale,0,1/scale,anchor[1]-180/scale),Image.Resampling.NEAREST)
    a=np.array(native); pixels=a[:,:,:3].astype(np.int16)
    distances=((pixels[:,:,None,:].astype(np.int32)-pal[None,None,:,:])**2).sum(3)
    a[:,:,:3]=pal[distances.argmin(2)]
    # Recover the original material from white-matte silhouette pixels.
    # Quantizing contaminated edges directly to cream caused a pale rim.
    mask=a[:,:,3]>0
    edge=mask & ~binary_erosion(mask)
    ep=pixels[edge].astype(np.float32)
    rays=255-pal.astype(np.float32)
    coverage=np.clip(((255-ep[:,None,:])*rays[None,:,:]).sum(2)/(rays*rays).sum(1),0,1)
    fitted=255-coverage[:,:,None]*rays[None,:,:]
    error=((ep[:,None,:]-fitted)**2).sum(2)+100*(1-coverage)**2
    choice=error.argmin(1)
    a[edge,:3]=pal[choice]
    a[edge,3]=np.where(coverage[np.arange(len(ep)),choice]>=.55,255,0).astype(np.uint8)
    a[a[:,:,3]==0]=0
    native=Image.fromarray(a)
    native.save(OUT/'frames'/f'{i+1:02}.png')
    frames.append(native)
keys=[1,7,9,11,13,14,15,16,18,21,24,26,28,30,33,36]
ms=[700,70,70,70,60,45,35,55,80,80,80,65,65,65,75,220]
frames[0].save(OUT/'selected-motion.webp',save_all=True,append_images=[frames[k-1] for k in keys[1:]],duration=ms,loop=0,lossless=True)
board=Image.new('RGB',(1280,800),'white');draw=ImageDraw.Draw(board)
for j,k in enumerate(keys):
    x=j%4*320;y=j//4*200
    board.paste(frames[k-1],(x,y),frames[k-1]);draw.text((x+12,y+10),f'{j+1:02} / source {k:02}',fill='#233344')
board.save(OUT/'selected-keys.png')
meta={'status':'review-pending','sourceSHA256':hashes,'grid':[6,6],'sourceCell':[1280,720],'sourceDurationsMs':durations,'canvas':[320,200],'neutralBodyHeight':body_height,'scaleReference':{'groundHoodWidthPx':32.44084,'oldAirHoodWidthPx':29.95169,'airCorrection':1.08},'fixedScale':scale,'sourceAnchor':anchor,'nativeAnchor':[180,180],'palette':pal.tolist(),'keys':keys,'durationsMs':ms,'releaseSourceFrame':16,'releaseKeyIndex':7,'spawnNative':[round(180+(b[0]-40-anchor[0])*scale),round(180+(180-anchor[1])*scale)],'note':'Review composite only; no Unity integration. Source 16 is the palm thrust; orb rendered independently.'}
(OUT/'motion.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
asset=json.loads((BASE/'asset.json').read_text(encoding='utf-8'))
asset['files']=sorted(str(p.relative_to(BASE)).replace('\\','/') for p in BASE.rglob('*') if p.is_file() and p.name!='asset.json')
asset['motionReview']['bluePalmReturn']='review-pending'
(BASE/'asset.json').write_text(json.dumps(asset,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
for entry in catalog['assets']:
    if entry['id']==asset['id']:entry['fileCount']=len(asset['files'])
(ROOT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'keys':keys,'fixedScale':scale,'firstBounds':b,'spawn':meta['spawnNative'],'sourceFrames':len(durations)},ensure_ascii=False))
