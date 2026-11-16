from pathlib import Path
import sys,json,io,base64,hashlib,subprocess
import numpy as np
from scipy import ndimage as ndi
from PIL import Image,ImageDraw,ImageFont
root=Path.cwd();sys.path.insert(0,str(root/'resources/tools'))
from prepare_hammer_provisional import foot_anchor,game_frame,checker
from refine_hammer_provisional import match_colors
assert subprocess.check_output(['git','branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
base=root/'outputs/hammer-rush-candidates-20261101/candidate-1-hold'
out=root/'outputs/hammer-rush-cleaned-review-20261101';out.mkdir(exist_ok=False)
for f in ['original','highres','detail4x','native224','masks','preview']:(out/f).mkdir()
plan=json.loads((base/'selection.json').read_text());steps=plan['frames']
palette=[(27,22,22),(253,238,218),(210,179,150),(2,86,115),(2,100,140),(235,165,22)]
belts={1:[],7:[],9:[(540,500),(615,500)],11:[(540,500),(620,502)],13:[(525,502),(610,514)],15:[(522,495),(606,504)],21:[(561,491),(610,496)],22:[(536,474),(575,494),(646,503)],24:[(520,481),(564,503),(625,512)],25:[(519,480),(564,503),(628,512)],26:[(520,480),(565,503),(628,512)],28:[(520,480),(563,500),(620,506)],30:[],31:[]}
frames=[];names=[];images=[];checks=[];anchor=None
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
board=Image.new('RGB',(1600,1150),'#eef1f4');bd=ImageDraw.Draw(board)
for i,s in enumerate(steps):
 n=s['sourceFrame'];fid=f'{i+1:02d}-F{n:02d}';path=base/(fid+'.png');raw=path.read_bytes();(out/'original'/(fid+'.png')).write_bytes(raw)
 rgb=np.array(Image.open(path).convert('RGB'));a=rgb.astype(int);yy,xx=np.indices(a.shape[:2]);work=rgb.copy()
 if anchor is None:anchor=foot_anchor(rgb)
 maskim=Image.new('L',(1280,720));draw=ImageDraw.Draw(maskim)
 if belts[n]:draw.line(belts[n],fill=255,width=20,joint='curve')
 region=np.array(maskim)>0
 # Only the explicit thin waist line. Foreground handle and gloves above it remain intact.
 mask=region&(a.max(axis=2)<145)
 mask=ndi.binary_dilation(mask,iterations=1)&region
 beige=(a[:,:,0]>150)&(a[:,:,1]>120)&(a[:,:,2]>95)&(a[:,:,0]>a[:,:,1]+4)&(a[:,:,1]>a[:,:,2]+4)
 donors=beige&~ndi.binary_dilation(region,iterations=6)
 near=ndi.distance_transform_edt(~donors,return_distances=False,return_indices=True)
 work[mask]=(253,238,218)
 # Separate background/neutral dust from colored cloth and the dark connected body.
 b=work.astype(int);chroma=b.max(2)-b.min(2)
 keep=((chroma>25)&(b.min(2)<230))|(b.max(2)<100)
 keep=ndi.binary_closing(keep,iterations=2)
 lab,_=ndi.label(keep,structure=np.ones((3,3)));counts=np.bincount(lab.ravel());counts[0]=0
 keep=lab==counts.argmax()
 # Repair tiny bright grooves and slits inside the rigid hammer head only.
 head=keep&(xx>760)&(yy>160)&(yy<570)
 fill=ndi.binary_closing(head,iterations=5)|ndi.binary_fill_holes(head)
 hammerfix=fill&~head&(xx>775)
 # Convex solid repair is restricted to the right-hand hammer head, not body or handle.
 from scipy.spatial import ConvexHull
 pts=np.column_stack(np.where(head & (work.max(2)<100)))[:,::-1]
 hull=ConvexHull(pts);hm=Image.new('L',(1280,720));ImageDraw.Draw(hm).polygon([tuple(map(int,p)) for p in pts[hull.vertices]],fill=255)
 hullmask=np.array(hm)>0
 hammerfix |= hullmask
 keep|=hammerfix;work[hammerfix]=(27,22,22)
 core=ndi.binary_erosion(keep,iterations=3)
 near=ndi.distance_transform_edt(~core,return_distances=False,return_indices=True)
 fringe=keep&~core;work[fringe]=work[near[0][fringe],near[1][fringe]]
 rgba=np.dstack([work,np.where(keep,255,0).astype('uint8')]);rgba[~keep]=0
 high,_,_=match_colors(Image.fromarray(rgba),palette,fid,effect_frames=set())
 arr=np.array(high)
 # Never let neighboring scarf pigments bleed into the weapon boundary.
 arr[(xx>770)&(arr[:,:,3]>0),:3]=(27,22,22)
 high=Image.fromarray(arr)
 high.save(out/'highres'/(fid+'.png'));Image.fromarray(np.uint8(mask)*255).save(out/'masks'/(fid+'-waist.png'))
 detail=game_frame(high,anchor,4);native=game_frame(high,anchor)
 name=f'hammer_movement_{i+1:02d}';names.append(name);frames.append(detail)
 detail.save(out/'detail4x'/(name+'.png'));native.save(out/'native224'/(name+'.png'))
 buf=io.BytesIO();detail.save(buf,format='PNG');images.append('data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
 checks.append({'sourceFrame':n,'sourceSha256':hashlib.sha256(raw).hexdigest(),'waistPixels':int(mask.sum()),'hammerPixels':int(hammerfix.sum()),'anchor':anchor,'scale':.145,'durationMs':s['durationMs']})
 # Before/after for each frame, with a uniform crop for all poses.
 x=i%4*400;y=i//4*280
 bg=checker((400,245),20);bg.alpha_composite(high.crop((250,170,1050,660)).resize((400,245),Image.Resampling.LANCZOS));board.paste(bg.convert('RGB'),(x,y+30));bd.text((x+10,y+3),fid,font=font,fill='#172333')
board.save(out/'preview/frames.png')
times=[s['durationMs'] for s in steps]
for label,color in [('dark','#30363d'),('white','white'),('checker',None)]:
 imgs=[]
 for f in frames:
  crop=f.crop((128,160,800,640));bg=checker(crop.size,24) if color is None else Image.new('RGBA',crop.size,color);bg.alpha_composite(crop);imgs.append(bg.convert('RGB'))
 ds=times[:];ds[-1]+=650
 imgs[0].save(out/'preview'/f'rush-14-{label}.gif',save_all=True,append_images=imgs[1:],duration=ds,loop=0,disposal=2,optimize=False)
frames[7].crop((128,160,800,640)).save(out/'preview/held-transparent.png')
editor={'schemaVersion':1,'kind':'hammer-rush-cleaned-14','weapon':'hammer','width':896,'height':768,'names':names,'sourceFrames':names,'images':images,'ms':times,'sequences':{'hammer_movement':[{'frame':n,'ms':t} for n,t in zip(names,times)]},'palette':['#%02x%02x%02x'%c for c in palette],'note':'Original raster poses, palette correction only, waist stripe removal and transparent background. No spatial pixelization. Hold F22 180ms. Review only, not Unity deployed.'}
(out/'editor.json').write_text(json.dumps(editor,ensure_ascii=False),encoding='utf-8')
(out/'animation.json').write_text(json.dumps({'actionId':'hammer_movement','status':'cleanup_review','canvas':[224,192],'pivot':[96,151],'ppu':32,'detailExport':{'canvas':[896,768],'pivot':[384,604],'ppu':128},'sourceSelection':plan,'durationMs':sum(times),'checks':checks,'unityApplied':False},indent=2),encoding='utf-8')
assert sum(times)==850
print(json.dumps({'out':str(out),'anchor':anchor,'frames':len(frames),'waistPixels':[c['waistPixels'] for c in checks]},ensure_ascii=True))
