"""Export native pixel overlays; keep generated source and player unchanged."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, shutil, math

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
OUT=BASE/'phase1-playtest/effects/ink-root'
OUT.mkdir(parents=True,exist_ok=True)
SRC=Path('C:/Users/SSAFY/.codex/generated_images/01a18def-9c96-7602-bc0f-d707e8e958ed/exec-d09f2582-4771-40b2-a676-632388c3a972.png')
shutil.copy2(SRC,OUT/'generated-original.png')
pal=[(32,24,41),(61,41,73),(99,71,111),(150,115,145)]
im=Image.open(SRC).convert('RGBA'); mask=im.getchannel('A').point(lambda a:255 if a>128 else 0)
crop=im.crop(mask.getbbox()).resize((44,22),Image.Resampling.NEAREST)
result=Image.new('RGBA',crop.size)
for y in range(crop.height):
 for x in range(crop.width):
  r,g,b,a=crop.getpixel((x,y))
  if a>128:
   c=min(pal,key=lambda c:sum((v-w)**2 for v,w in zip(c,(r,g,b))))
   result.putpixel((x,y),(*c,255))
result.save(OUT/'root.png')
# Fixed ink footprint, staggered dome growth, rupture, and falling droplets.
for frame in range(8):
 ink=Image.new('RGBA',(96,24)); d=ImageDraw.Draw(ink)
 d.polygon([(0,19),(4,17),(13,17),(17,16),(29,17),(35,16),(47,17),(55,15),(64,17),(78,16),(86,17),(93,17),(95,19),(92,22),(82,22),(76,23),(58,22),(47,23),(25,22),(12,23),(3,21)],fill=pal[0])
 d.line([(5,18),(20,18),(23,19),(40,18),(48,19),(68,18),(77,19),(89,18)],fill=pal[1],width=2)
 for j,cx in enumerate([13,38,63,84]):
  phase=(frame+j*2)%8
  if phase<5:
   radius=[2,3,4,5,5][phase]; top=18-radius*2
   d.ellipse((cx-radius,top,cx+radius,20),fill=pal[1])
   d.line([(cx-radius+2,top+2),(cx,top+1),(cx+2,top+2)],fill=pal[2],width=1)
   d.rectangle((cx-radius,19,cx+radius,21),fill=pal[0])
  elif phase==5:
   d.line([(cx-6,17),(cx-3,19),(cx+3,19),(cx+6,17)],fill=pal[2],width=1)
   d.rectangle((cx-5,9,cx-4,11),fill=pal[2]);d.rectangle((cx+4,12,cx+5,13),fill=pal[1])
  elif phase==6:
   d.rectangle((cx-7,13,cx-6,14),fill=pal[1]);d.rectangle((cx+6,15,cx+7,16),fill=pal[2])
 ink.save(OUT/f'trap-{frame:02}.png')
(OUT/'animation.json').write_text(json.dumps({'trap':{'size':[96,24],'surfaceY':20,'frames':8,'frameMs':120},'root':{'size':[44,22],'footOffset':[-26,-20],'durationSeconds':2,'palette':[list(c) for c in pal]},'status':'concept-review','gameIntegration':'not-performed'},indent=2),encoding='utf-8')
print(OUT)
