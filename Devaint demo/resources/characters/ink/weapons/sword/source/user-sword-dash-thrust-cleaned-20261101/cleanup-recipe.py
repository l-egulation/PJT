from pathlib import Path
import sys,json,io,base64,hashlib,shutil,subprocess
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy import ndimage as ndi
root=Path.cwd();res=root/'resources';sys.path.insert(0,str(res/'tools'))
from prepare_hammer_provisional import game_frame,checker
from refine_hammer_provisional import match_colors
assert subprocess.check_output(['git','branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
base=root/'outputs/sword-thrust-candidates-20261101';plan=json.loads((base/'selections.json').read_text())['candidates'][1]
out=root/'outputs/sword-dash-thrust-cleaned-review-20261101';out.mkdir(exist_ok=False)
for k in ['original','highres','detail4x','native224','preview']:(out/k).mkdir()
palette=[(27,22,22),(253,238,218),(210,179,150),(2,86,115),(2,100,140),(235,165,22)]
anchor=(570,633);mult=1.0434782608695652
frames=[];images=[];names=[];checks=[];font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
board=Image.new('RGB',(1600,850),'#eef1f4');bd=ImageDraw.Draw(board)
for i,(path,step) in enumerate(zip(sorted((base/plan['id']).glob('*.png')),plan['frames'])):
 raw=path.read_bytes();(out/'original'/path.name).write_bytes(raw);rgb=np.array(Image.open(path).convert('RGB'));a=rgb.astype(int);yy,xx=np.indices(a.shape[:2])
 keep=((a.max(2)-a.min(2)>25)&(a.min(2)<230))|(a.max(2)<100)
 keep=ndi.binary_closing(keep,iterations=2);lab,_=ndi.label(keep,structure=np.ones((3,3)));sz=np.bincount(lab.ravel());sz[0]=0;keep=lab==sz.argmax()
 # Keep a slight edge-on blade thickness at the extended thrust, preserving its tip and length.
 weapon=(a.max(2)<85)&(xx>780)&(yy>330)&(yy<470)&keep
 rows=np.where(weapon)[0];thick=rows.max()-rows.min() if len(rows) else 999
 blade=np.zeros(keep.shape,bool)
 if thick<35:blade=ndi.binary_dilation(weapon,structure=np.ones((5,1)))&~keep
 keep|=blade;work=rgb.copy();work[blade]=(27,22,22)
 core=ndi.binary_erosion(keep,iterations=3);near=ndi.distance_transform_edt(~core,return_distances=False,return_indices=True);edge=keep&~core;work[edge]=work[near[0][edge],near[1][edge]]
 rgba=np.dstack([work,np.where(keep,255,0).astype('uint8')]);rgba[~keep]=0
 high,_,_=match_colors(Image.fromarray(rgba),palette,path.stem,effect_frames=set());b=np.array(high);c=b[:,:,:3].astype(int);active=b[:,:,3]>0
 blue=active&(c[:,:,2]>c[:,:,0]+40);bright=active&(c[:,:,0]>140)&(c[:,:,1]>120)&(c[:,:,2]>90)
 lab,_=ndi.label(bright,structure=np.ones((3,3)));sz=np.bincount(lab.ravel());dist=ndi.distance_transform_edt(~blue)
 seed=bright&(sz[lab]<400)&(dist<10)&(xx<570)&(yy>300)&(yy<510);ids=np.unique(lab[seed]);ids=ids[ids!=0];mask=np.isin(lab,ids)&bright
 inside=ndi.binary_fill_holes(blue)|ndi.binary_closing(blue,iterations=3);near=ndi.distance_transform_edt(~blue,return_distances=False,return_indices=True)
 fix=mask&inside;b[fix,:3]=c[near[0][fix],near[1][fix]];b[mask&~inside]=0
 high=Image.fromarray(b);high.save(out/'highres'/path.name)
 # game_frame scales both the canvas and pivot with factor; use explicit affine for a fixed export canvas.
 scale=.145*mult
 def export(factor):
  sc=scale*factor
  return high.transform((224*factor,192*factor),Image.Transform.AFFINE,(1/sc,0,anchor[0]-96*factor/sc,0,1/sc,anchor[1]-151*factor/sc),resample=Image.Resampling.BICUBIC)
 detail=export(4);native=export(1);name=f'attack_dash_thrust_{i+1:02d}';names.append(name);frames.append(detail)
 detail.save(out/'detail4x'/(name+'.png'));native.save(out/'native224'/(name+'.png'));buf=io.BytesIO();detail.save(buf,format='PNG');images.append('data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
 checks.append({'sourceFrame':step['sourceFrame'],'sourceSha256':hashlib.sha256(raw).hexdigest(),'scarfResiduePixels':int(mask.sum()),'edgeOnBladePixelsAdded':int(blade.sum()),'sourceAnchor':anchor,'constantSourceScale':scale,'durationMs':step['durationMs']})
 bg=checker((400,245),20);bg.alpha_composite(high.crop((290,200,1090,690)).resize((400,245),Image.Resampling.LANCZOS));x=i%4*400;y=i//4*280;board.paste(bg.convert('RGB'),(x,y+30));bd.text((x+10,y+3),f'{i+1:02d} / F{step["sourceFrame"]:02d}',font=font,fill='#172333')
board.save(out/'preview/frames.png');times=[s['durationMs'] for s in plan['frames']]
for label,color in [('dark','#30363d'),('white','white'),('checker',None)]:
 pics=[]
 for f in frames:
  crop=f.crop((128,160,896,672));bg=checker(crop.size,24) if color is None else Image.new('RGBA',crop.size,color);bg.alpha_composite(crop);pics.append(bg.convert('RGB'))
 ds=times[:];ds[-1]+=700;pics[0].save(out/'preview'/f'thrust-10-{label}.gif',save_all=True,append_images=pics[1:],duration=ds,loop=0,disposal=2,optimize=False)
frames[4].save(out/'preview/thrust-held-transparent.png')
ed={'schemaVersion':1,'kind':'sword-dash-thrust-cleaned-10','weapon':'base_sword','width':896,'height':768,'names':names,'sourceFrames':names,'images':images,'ms':times,'sequences':{'attack_dash_thrust':[{'frame':n,'ms':t} for n,t in zip(names,times)]},'note':'Selected candidate2, F13 held200ms. Color correction, transparent background, small edge-on blade thickness adjustment; no spatial pixelization; no Unity deployment.'}
(out/'editor.json').write_text(json.dumps(ed,ensure_ascii=False),encoding='utf-8');(out/'animation.json').write_text(json.dumps({'actionId':'base_sword_movement','status':'cleanup_review','canvas':[224,192],'pivot':[96,151],'ppu':32,'detailExport':{'canvas':[896,768],'pivot':[384,604],'ppu':128},'durationMs':600,'dashStartMs':150,'dashEndMs':350,'holdSourceFrame':13,'checks':checks,'unityApplied':False},indent=2),encoding='utf-8')
assert len(frames)==10 and sum(times)==600 and times[4]==200
print(json.dumps({'frames':10,'durationMs':600,'holdMs':200,'scarfPixels':sum(c['scarfResiduePixels'] for c in checks),'output':str(out)},ensure_ascii=True))
