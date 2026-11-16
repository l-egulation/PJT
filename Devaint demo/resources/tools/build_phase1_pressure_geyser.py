from pathlib import Path
from PIL import Image
import shutil,json
ROOT=Path(__file__).resolve().parents[1];B=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103/phase1-playtest/effects/ink-burst'
OUT=B/'pressure-v3';OUT.mkdir(exist_ok=True)
SRC=Path('C:/Users/SSAFY/.codex/generated_images/01a18def-9c96-7602-bc0f-d707e8e958ed/exec-625007cd-dbfc-4ba5-9360-6033d4e4754a.png')
shutil.copy2(SRC,OUT/'generated-original.png');im=Image.open(SRC).convert('RGBA')
pal=[(32,24,41),(61,41,73),(99,71,111),(150,115,145)];keys=[]
for i in range(8):
 x=i%4*384;y=i//4*512
 cell=im.crop((x,y+22,x+384,y+474)).resize((96,64),Image.Resampling.NEAREST)
 out=Image.new('RGBA',(96,64))
 for yy in range(64):
  for xx in range(96):
   r,g,b,a=cell.getpixel((xx,yy))
   if a>200:
    c=min(pal,key=lambda c:sum((u-v)**2 for u,v in zip(c,(r,g,b))))
    out.putpixel((xx,yy),(*c,255))
 keys.append(out);out.save(OUT/f'key-{i:02}.png')
# Fast pressure release: jet traverses the height in 60ms, no slow shape dissolve.
seq=[(0,0,.6),(1,0,.85),(2,34,1),(2,14,1.025),(2,0,1.055),(3,0,1.08),(4,0,1.14),(3,0,1.20),(4,0,1.25),(5,0,1.31),(5,6,1.36),(6,0,1.41),(6,4,1.46),(6,8,1.51),(7,0,1.56),(7,2,1.61)]
for i,(k,dy,t) in enumerate(seq):
 out=Image.new('RGBA',(96,64));out.paste(keys[k],(0,dy));out.save(OUT/f'{i:02}.png')
sheet=Image.new('RGBA',(96*8,64*2))
for i in range(16):sheet.paste(Image.open(OUT/f'{i:02}.png'),(i%8*96,i//8*64))
sheet.save(OUT/'frames.png')
(OUT/'animation.json').write_text(json.dumps({'times':[s[2] for s in seq],'offAt':1.68,'damageAt':1,'palette':pal,'size':[96,64],'status':'concept-review','gameIntegration':'not-performed'},indent=2)+'\n',encoding='utf-8')
module='const times='+json.dumps([s[2] for s in seq])+";\n"+'''export function burstKey(age){if(age<times[0]||age>=1.68)return -1;for(let i=times.length-1;i>=0;i--)if(age>=times[i])return i;return -1;}
export function drawBurst(ctx,frames,x,foot,age,scale=1){const k=burstKey(age);if(k<0)return;ctx.save();ctx.imageSmoothingEnabled=false;ctx.drawImage(frames[k],Math.round(x),Math.round(foot-64*scale),96*scale,64*scale);ctx.restore();}
'''
(B/'burst.mjs').write_text(module,encoding='utf-8');print('Pressure geyser: 16 frames, 60ms upward breakthrough')
