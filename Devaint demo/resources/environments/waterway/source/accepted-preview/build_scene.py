from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np,json
P=Path(__file__).parent
src=json.loads((P/'assets.json').read_text(encoding='utf-8'))
A={k:Image.open(P/v).convert('RGBA') for k,v in src.items()}
R=P/'assets/reference-ground'
E={e['name']:e for e in json.loads((R/'library.json').read_text())['entries']}
N=Image.Resampling.NEAREST
caps={n:Image.open(R/'Bright'/(n+'.png')).convert('RGBA') for n in ['cap-one-a','cap-one-b','cap-wide','cap-short']}
soil=np.array(Image.open(R/'Bright/soil.png').convert('RGB'))
W,H=4730,960
yy,xx=np.mgrid[0:H,0:W]
def smooth(a,b,v):
 t=np.clip((v-a)/(b-a),0,1);return t*t*(3-2*t)
def fade(im,start,end,origin=0):
 a=np.array(im);a[:,:,3]=(a[:,:,3]*(1-smooth(start,end,np.arange(im.width)+origin))[None,:]).astype('uint8');return Image.fromarray(a)
def floor(x):return 448 if x<3258 else 480 if x<3386 else 512 if x<3546 else 544
def ceiling(x):return 184+248*smooth(3600,4160,x)
world=Image.new('RGBA',(W,H))
# Full room and approach are exported from the live Unity layout.json, with original layer positions.
world.alpha_composite(Image.open(P/'actual-room.png').convert('RGBA'),(0,0))
world.alpha_composite(Image.new('RGBA',(960,960),(57,57,48,255)),(960,0))
def imported(name,size):
 im=Image.open(P/(name+'-original.png')).convert('RGBA').resize(size,N)
 ar=np.array(im);ar[:,:,3]=np.where(ar[:,:,3]>=240,255,ar[:,:,3]);ar[ar[:,:,3]==0,:3]=0
 im=Image.fromarray(ar);im.save(P/(name+'-native.png'));return im
module=imported('exit-connector',(768,512))
# The new whole terminal pier begins AFTER the old art, not over a dissolving mask.
world.alpha_composite(module,(1920,-64))
entry=imported('drain-connector',(1280,512))
world.alpha_composite(entry,(3034,64))
# Continue only the finished low-tunnel bay after the generated keyed masonry join.
tail=entry.crop((1120,0,1280,512))
for x in range(4314,W,160):world.alpha_composite(tail,(x,64))
for x in range(3034,W):
 sample=entry.getpixel((min(x-3034,1279),0))
 if sample[3]>=240:ImageDraw.Draw(world).line((x,0,x,63),fill=sample)
runs=[(0,3258,448),(3258,3386,480),(3386,3546,512),(3546,W,544)]
floors=np.select([xx<3258,xx<3386,xx<3546],[448,480,512],default=544)
depth=np.full((H,W),1e6)
for l,r,y in runs:
 dx=np.maximum(np.maximum(l-xx,xx-r),0);depth=np.minimum(depth,np.sqrt(dx*dx+(yy-y)**2))
t=smooth(4,76,depth);base=soil[yy%soil.shape[0],xx%soil.shape[1]].astype(float)*.86
rgb=base*(1-t[:,:,None])+np.array([39,35,36])*t[:,:,None]
mix=smooth(3630,4010,xx);rgb=rgb*(1-mix[:,:,None])+np.array([31,43,43])*mix[:,:,None]
arr=np.zeros((H,W,4),dtype='uint8');arr[:,:,:3]=rgb;arr[:,:,3]=(yy>=floors)*255;world.alpha_composite(Image.fromarray(arr))
# Original ReferenceGround underside: use the native contact anchor and soil-edge treatment.
# The boss ceiling is 15 units above the floor (-32 here), outside its fixed camera.
# The 6-unit approach passage has its ceiling at y256 and receives a visible underside.
roofMask=(xx>=960)&(xx<1920)&(yy<=256)
rd=np.maximum(0,256-yy);rt=smooth(4,76,rd)
rr=base*(1-rt[:,:,None])+np.array([39,35,36])*rt[:,:,None]
roof=np.zeros((H,W,4),dtype='uint8');roof[:,:,:3]=rr;roof[:,:,3]=roofMask*255
world.alpha_composite(Image.fromarray(roof))
under=Image.open(R/'underside.png').convert('RGBA');e=E['underside']
for x in range(960,1920,240):
 part=under.resize((round(under.width*242/256),under.height),N)
 pa=np.array(part);py,px=np.mgrid[0:part.height,0:part.width];left=round(x-1-e['anchorX']*242/256);top=round(256-e['anchorY'])
 dp=np.maximum(0,256-(py+top));tt=smooth(4,76,dp)
 rgb=soil[(py+top)%soil.shape[0],(px+left)%soil.shape[1]]*.86*(1-tt[:,:,None])+np.array([39,35,36])*tt[:,:,None]
 pa[:,:,:3]=rgb;world.alpha_composite(Image.fromarray(pa),(left,top))
joint=Image.open(P/'joint-correction-original.png').convert('RGBA').resize((1312,448),N)
joint.save(P/'joint-correction-native.png')
world.alpha_composite(joint.crop((128,0,1184,448)),(960,0))
# Same cap source and anchors; the colour returns over distance, including the ground.
for left,right,top in runs:
 end=min(right,3610)
 if end<=left:continue
 length=end-left;count=max(1,round(length/32));rhythm=[1,1.2,1,.8,1];total=sum(rhythm[i%5] for i in range(count));cursor=float(left)
 for i in range(count):
  width=length*rhythm[i%5]/total;n='cap-wide' if width>36 else 'cap-short' if width<28 else 'cap-one-a' if i%2==0 else 'cap-one-b';e=E[n];s=width/e['logicalWidth'];part=caps[n].resize((round(caps[n].width*s),caps[n].height),N)
  # Whole cap pieces change material at an actual joint, never through alpha fading.
  world.alpha_composite(part,(round(cursor-e['anchorX']*s),round(top-e['anchorY'])))
  cursor+=width
for x in range(4122,W,32):
 for name,y in [('s2-top',floor(x)),('s2-inner',floor(x)+32)]:
  world.alpha_composite(A[name],(x,y))
# A single authored sixteen-unit material join; full pieces keep the existing stone scale.
join=Image.open(P/'terrain-join-original.png').convert('RGBA').resize((512,128),N)
ja=np.array(join);ja[:,:,3]=np.where(ja[:,:,3]>=240,255,ja[:,:,3]);ja[ja[:,:,3]==0,:3]=0
join=Image.fromarray(ja);join.save(P/'terrain-join-native.png')
anchor=25
# Reuse the existing terrain's depth-to-core shading below the exposed stone courses.
jy,jx=np.mgrid[0:128,0:512];dep=np.maximum(0,jy-anchor)
weight=smooth(32,84,dep)[:,:,None];core=np.array([31,43,43])
soilGain=1-.14*smooth(12,20,dep)[:,:,None]
ja[:,:,:3]=np.clip(ja[:,:,:3]*soilGain*(1-weight)+core*weight,0,255).astype('uint8')
world.alpha_composite(Image.fromarray(ja),(3610,544-anchor))
# A continuous, world-anchored desaturation ramp: no camera-wide colour switch.
ar=np.array(world);col=ar[:,:,:3].astype(float);lum=col[:,:,0]*.299+col[:,:,1]*.587+col[:,:,2]*.114
paper=np.stack([lum*1.04,lum*1.01,lum*.94],axis=2);gain=smooth(1190,2426,xx)[:,:,None]
ar[:,:,:3]=np.clip(paper*(1-gain)+col*gain,0,255).astype('uint8');world=Image.fromarray(ar)
world.save(P/'scene-world.png')
def frame(cx,cy,px):
 out=Image.new('RGBA',(960,540),(103,161,220,255))
 # Match viewer.js: distant city moves 15% of the camera travel, sky 4%.
 skyx=1600+(cx-2000)*.96-cx;cityx=2032+(cx-2000)*.85-cx
 out.alpha_composite(A['s1-sky'],(round(skyx),round(-100-cy*.04+14)))
 out.alpha_composite(A['s1-city'],(round(cityx),round(-64-cy*.08+14)))
 out.alpha_composite(world.crop((cx,cy-14,cx+960,cy+526)))
 if cy<14:out.alpha_composite(world.crop((cx,0,cx+960,1)).resize((960,14-cy),N),(0,0))
 if cx+960>4280:
  d=ImageDraw.Draw(out);gx=4314-cx;top=432-cy+14;bottom=544-cy+14
  for x in range(gx-24,gx+27,11):d.rectangle((x,top,x+4,bottom),fill=(102,122,113))
  for y in [top+8,bottom-12]:d.rectangle((gx-27,y,gx+27,y+5),fill=(77,97,89))
 out.alpha_composite(A['player'],(px-cx-80,floor(px)-cy-137+14));return out.convert('RGB')
sheet=Image.new('RGB',(1920,1080))
for i,(cx,cy,px) in enumerate([(1020,0,1440),(1970,0,2390),(2920,32,3340),(3750,96,4220)]):
 f=frame(cx,cy,px);f.save(P/f'view-{i+1}.png');sheet.paste(f,((i%2)*960,(i//2)*540))
sheet.save(P/'review-sheet.png')
print('Built transparent world overlay, gradual colour, native-scale distant city and low drain')

bf=frame(0,0,180).convert('RGBA');bf.alpha_composite(Image.open(P/'boss-assembly.png'),(0,14));bf.convert('RGB').save(P/'boss-room-preview.png')
frame(3350,96,3770).save(P/'terrain-join-preview.png')
frame(730,0,1162).save(P/'boss-exit-joint-preview.png')
frame(1450,0,1882).save(P/'cave-joint-preview.png')
