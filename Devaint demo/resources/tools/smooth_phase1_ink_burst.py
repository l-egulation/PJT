"""Insert solid pixel contour inbetweens; retain original six key PNGs."""
from pathlib import Path
from PIL import Image
import numpy as np
from scipy.ndimage import distance_transform_edt
import json
ROOT=Path(__file__).resolve().parents[1]
B=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103/phase1-playtest/effects/ink-burst'
OUT=B/'smooth-frames';OUT.mkdir(exist_ok=True)
pal=np.array([[32,24,41],[61,41,73],[99,71,111],[150,115,145]])
keys=[np.array(Image.open(B/f'{i:02}.png').convert('RGBA')) for i in range(6)]
def field(a):
 mask=a[:,:,3]>0
 signed=distance_transform_edt(mask)-distance_transform_edt(~mask)
 _,indices=distance_transform_edt(~mask,return_indices=True)
 return signed,a[indices[0],indices[1],:3]
fields=[field(a) for a in keys]
frames=[];times=[];keytimes=[.6,.82,1,1.09,1.22,1.42]
for i in range(5):
 for j in range(4):
  t=j/4
  if j==0:out=keys[i].copy()
  else:
   a,ca=fields[i];b,cb=fields[i+1]
   mask=a*(1-t)+b*t>0
   colors=ca*(1-t)+cb*t
   index=((colors[:,:,None,:]-pal[None,None,:,:])**2).sum(axis=3).argmin(axis=2)
   out=np.zeros((64,96,4),dtype=np.uint8);out[:,:,:3]=pal[index];out[:,:,3]=mask*255
   out[~mask]=0
  frames.append(Image.fromarray(out));times.append(round(keytimes[i]*(1-t)+keytimes[i+1]*t,4))
frames.append(Image.fromarray(keys[-1]));times.append(1.42)
for shift,at in [(2,1.49),(4,1.56),(6,1.62)]:
 out=Image.new('RGBA',(96,64));out.paste(frames[20],(0,shift));frames.append(out);times.append(at)
for i,f in enumerate(frames):f.save(OUT/f'{i:02}.png')
sheet=Image.new('RGBA',(96*8,64*3))
for i,f in enumerate(frames):sheet.paste(f,(i%8*96,i//8*64))
sheet.save(B/'smooth-keys.png')
config=json.loads((B/'animation.json').read_text());config['smooth']={'frames':24,'times':times,'offAt':1.65,'method':'signed-distance silhouette inbetweens; solid alpha and original four colors; original keyframes preserved'}
(B/'animation.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
module="const times="+json.dumps(times)+";\n"+'''export function burstKey(age){
 if(age<times[0]||age>=1.65)return -1;
 for(let i=times.length-1;i>=0;i--)if(age>=times[i])return i;
 return -1;
}
export function drawBurst(ctx,frames,x,foot,age,scale=1){
 const k=burstKey(age);if(k<0)return;
 ctx.save();ctx.imageSmoothingEnabled=false;
 ctx.drawImage(frames[k],Math.round(x),Math.round(foot-64*scale),96*scale,64*scale);ctx.restore();
}
'''
(B/'burst.mjs').write_text(module,encoding='utf-8')
print('24 opaque-pixel frames; original key times and damage at 1.0s preserved')
