from pathlib import Path
from PIL import Image
import shutil,json
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103/phase1-playtest/effects/ink-burst'
OUT.mkdir(parents=True,exist_ok=True)
SRC=Path('C:/Users/SSAFY/.codex/generated_images/01a18def-9c96-7602-bc0f-d707e8e958ed/exec-72131eca-52b5-4bfc-b4fa-d4203328e3ae.png')
shutil.copy2(SRC,OUT/'generated-original.png')
im=Image.open(SRC).convert('RGBA');pal=[(32,24,41),(61,41,73),(99,71,111),(150,115,145)]
frames=[]
for i in range(6):
 x=i%3*512;y=i//3*512
 # One shared crop/scale: never normalize each frame's bounding box.
 cell=im.crop((x,y+80,x+512,y+422)).resize((96,64),Image.Resampling.NEAREST)
 out=Image.new('RGBA',(96,64))
 for yy in range(64):
  for xx in range(96):
   r,g,b,a=cell.getpixel((xx,yy))
   if a>200:
    c=min(pal,key=lambda c:sum((u-v)**2 for u,v in zip(c,(r,g,b))))
    out.putpixel((xx,yy),(*c,255))
 out.save(OUT/f'{i:02}.png');frames.append(out)
sheet=Image.new('RGBA',(96*6,64))
for i,f in enumerate(frames):sheet.paste(f,(96*i,0))
sheet.save(OUT/'keys.png')
(OUT/'animation.json').write_text(json.dumps({'size':[96,64],'anchor':[48,64],'patternSeconds':[.6,.82,1,1.09,1.22,1.42,1.65],'damageAt':1,'damage':15,'status':'concept-review','gameIntegration':'not-performed','palette':pal},indent=2)+'\n',encoding='utf-8')
print('6 frames:96x64, binary alpha, four colors, common ground anchor')
