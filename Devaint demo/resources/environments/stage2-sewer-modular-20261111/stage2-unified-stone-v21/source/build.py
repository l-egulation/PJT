from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,math
ROOT=Path(__file__).resolve().parents[1]
M=(55,74,81,255)
RAMPS=[[(69,88,94,255),(64,83,90,255),(59,78,85,255),(54,73,80,255)],[(72,89,91,255),(68,84,86,255),(63,79,82,255),(57,73,78,255)],[(65,86,94,255),(60,81,89,255),(56,77,85,255),(51,72,80,255)]]
CAP=[(183,194,182,255),(161,177,165,255),(139,160,149,255),(120,143,135,255),(103,129,123,255),(85,111,109,255),(61,82,85,255)]
def wall(width,v):
 im=Image.new('RGBA',(width,16),M);d=ImageDraw.Draw(im);hi,face,low,deep=RAMPS[v%3]
 # One-pixel mortar at the start of the course. Rounded brick corners stay inside it.
 d.rectangle((1,1,width-1,15),fill=face)
 d.line((3,1,width-3,1),fill=hi);d.line((2,2,2,11),fill=hi)
 d.polygon([(1,12),(width//2-2,12),(width//2,10),(width-2,10),(width-1,15),(2,15)],fill=low)
 d.line((4,15,width-4,15),fill=deep)
 for x,y in [(1,1),(1,2),(width-1,1),(width-2,1),(1,15),(width-1,14),(width-1,15)]:im.putpixel((x,y),M)
 im.putpixel((3,2),hi);im.putpixel((width-3,14),low)
 # Only two variants crack; remaining stones rely on volume and hue, not marks.
 if v in [2,5]:
  x=min(width-5,7+(v%3)*3)
  for xx,yy in [(x,1),(x,2),(x+1,3),(x+1,4)]:im.putpixel((xx,yy),deep)
  im.putpixel((x+2,4),face)
 if v in [1,4]:
  d.polygon([(5,5),(min(width-4,13),5),(min(width-4,11),7),(5,7)],fill=RAMPS[(v+1)%3][1])
 if v==3:
  for x,y in [(width-6,11),(width-5,11),(width-5,12),(width-4,12)]:im.putpixel((x,y),(66,82,73,255))
 return im

def cap(v):
 im=Image.new('RGBA',(32,16));d=ImageDraw.Draw(im)
 for y in range(16):
  c=CAP[0 if y==0 else 1 if y<4 else 2 if y==4 else 3 if y<11 else 4 if y<13 else 5 if y<15 else 6]
  d.line((0,y,31,y),fill=c)
 d.line((0,1,0,15),fill=CAP[6]);d.line((31,5,31,14),fill=CAP[5])
 # Large authored face planes rather than tiny uniform noise.
 if v in [0,4]:d.polygon([(5,6),(18,6),(15,8),(14,10),(5,10)],fill=CAP[2])
 elif v in [1,5]:d.polygon([(19,5),(28,5),(28,10),(24,10),(22,8)],fill=CAP[4])
 elif v==3:d.rectangle((8,9,14,10),fill=CAP[4])
 if v in [1,2,5]:
  x={1:7,2:23,5:14}[v]
  im.putpixel((x,0),CAP[2]);im.putpixel((x+1,1),CAP[3]);im.putpixel((x+2,2),CAP[0])
 if v==2:
  for x,y in [(23,3),(22,4),(22,5),(21,6)]:im.putpixel((x,y),CAP[5])
 return im

def canon(m):
 for i in [1,3,5,7]:
  if not(m&(1<<((i+7)%8))) or not(m&(1<<((i+1)%8))):m &= ~(1<<i)
 return m
masks=sorted(set(canon(i) for i in range(256)));assert len(masks)==47
for v in range(8):
 cap(v).save(ROOT/f'terrain/source/normalized/top-{v}.png')
 wall(32,v).save(ROOT/f'terrain/source/normalized/body-{v}.png');wall(32,v).save(ROOT/f'terrain/source/normalized/under-{v}.png');wall(16,v).save(ROOT/f'terrain/source/normalized/half-{v}.png')
 for mask in masks:
  im=Image.new('RGBA',(32,32));upper=wall(32,v);lower=wall(32,(v+1)%8)
  # Two16px courses, offset by a half brick. No rotated face lighting.
  im.alpha_composite(upper)
  for x in range(32):
   for y in range(16):im.putpixel((x,y+16),lower.getpixel(((x+16)%32,y)))
  if not(mask&1):im.paste(cap(v),(0,0))
  # Fine continuous contour, compatible with the same masonry palette.
  d=ImageDraw.Draw(im)
  if not(mask&64):d.line((0,0,0,31),fill=M)
  if not(mask&4):d.line((31,0,31,31),fill=M)
  if not(mask&16):d.line((0,31,31,31),fill=M)
  # Cosmetic 2px convex corner cuts; contact/collision rectangles are not edited.
  for active,sx,sy in [(not(mask&1) and not(mask&64),0,0),(not(mask&1) and not(mask&4),31,0),(not(mask&16) and not(mask&64),0,31),(not(mask&16) and not(mask&4),31,31)]:
   if active:
    for dx,dy in [(0,0),(1,0),(0,1)]:im.putpixel((sx+(dx if sx==0 else -dx),sy+(dy if sy==0 else -dy)),(0,0,0,0))
  im.save(ROOT/f'terrain/runtime/tiles/stone-{mask:02X}-v{v}.png')

names=['single','single-left-wall','single-right-wall','single-both-wall','left-free','left-wall','middle-a','middle-b','middle-c','right-free','right-wall']
for name in names:
 v=names.index(name)%8
 if name.startswith('middle'):v={'middle-a':6,'middle-b':1,'middle-c':3}[name]
 im=Image.new('RGBA',(32,32));im.alpha_composite(cap(v).crop((0,0,32,12)))
 d=ImageDraw.Draw(im);d.line((0,10,31,10),fill=CAP[5]);d.line((0,11,31,11),fill=CAP[6])
 left=name in ['single','single-right-wall','left-free'];right=name in ['single','single-left-wall','right-free']
 if left:
  im.putpixel((0,0),(0,0,0,0));im.putpixel((0,11),(0,0,0,0));im.putpixel((0,1),CAP[4])
 if right:
  im.putpixel((31,0),(0,0,0,0));im.putpixel((31,11),(0,0,0,0));im.putpixel((31,1),CAP[4])
 if name in ['left-wall','single-left-wall','single-both-wall']:
  for y in range(12):im.putpixel((0,y),im.getpixel((1,y)))
 im.save(ROOT/f'runtime/full/{name}.png');im.save(ROOT/f'runtime/contact/{name}.png')
 assert im.getchannel('A').crop((0,12,32,32)).getbbox() is None
# Native atlas with2px extrusion, no interpolation.
files=list(sorted((ROOT/'terrain/runtime/tiles').glob('*.png')))+list(sorted((ROOT/'runtime/full').glob('*.png')))
stride=36;cols=16;atlas=Image.new('RGBA',(cols*stride,math.ceil(len(files)/cols)*stride));rects=[]
for i,p in enumerate(files):
 im=Image.open(p).convert('RGBA');assert im.size==(32,32);x=i%cols*stride+2;y=i//cols*stride+2
 for yy in range(-2,34):
  for xx in range(-2,34):atlas.putpixel((x+xx,y+yy),im.getpixel((max(0,min(31,xx)),max(0,min(31,yy)))))
 rects.append({'id':p.stem,'x':x,'y':y,'w':32,'h':32,'file':str(p.relative_to(ROOT))})
atlas.save(ROOT/'runtime/atlas-32.png')
(ROOT/'runtime/tileset.json').write_text(json.dumps({'version':'v21','pixelsPerUnit':32,'cell':[32,32],'brick':[32,16],'halfBrick':[16,16],'mortar':1,'onewayVisible':12,'collisionThicknessSpecification':4,'atlas':{'file':'atlas-32.png','stride':36,'extrusion':2,'rects':rects},'canonicalLookup':[canon(i) for i in range(256)],'parts':names,'integrated':False},indent=2),encoding='utf-8')
print('387 native sprites rebuilt in one material vocabulary; atlas ready;11 one-way modules within12px.')
