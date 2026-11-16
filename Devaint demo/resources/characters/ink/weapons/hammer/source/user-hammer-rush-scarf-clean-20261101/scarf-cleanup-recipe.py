from pathlib import Path
import sys,json,io,base64,hashlib,shutil,subprocess
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy import ndimage as ndi
root=Path.cwd();res=root/'resources';sys.path.insert(0,str(res/'tools'))
from prepare_hammer_provisional import game_frame,checker
assert subprocess.check_output(['git','branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
assert subprocess.check_output(['git','-C','resources','branch','--show-current'],text=True).strip()=='feat/basic-sword-normal-weapon'
assetdir=res/'characters/ink/weapons/hammer';old='user-hammer-rush-cleaned-20261101';sid='user-hammer-rush-scarf-clean-20261101'
meta=[assetdir/'asset.json',res/'catalog.json',res/'generated-assets.json',res/'.lfs-payloads.json'];snap={p:p.read_bytes() for p in meta};asset,catalog,generated,payloads=[json.loads(snap[p]) for p in meta]
for kind in ['source','runtime','preview']:shutil.copytree(assetdir/kind/old,assetdir/kind/sid)
source=assetdir/'source'/sid;runtime=assetdir/'runtime'/sid;preview=assetdir/'preview'/sid
ed=json.loads((source/'editor.json').read_text());an=json.loads((source/'animation.json').read_text());anchor=an['checks'][0]['anchor'];frames=[];images=[];checks=[]
board=Image.new('RGB',(1600,1150),'#eef1f4');bd=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
zoom=Image.new('RGBA',(1200,14*150),'#30363d')
for i,f in enumerate(sorted((source/'highres').glob('*.png'))):
 a=np.array(Image.open(f).convert('RGBA'));before=a.copy();rgb=a[:,:,:3].astype('int32');active=a[:,:,3]>0;blue=active&(rgb[:,:,2]>rgb[:,:,0]+40)
 bright=active&(rgb[:,:,0]>140)&(rgb[:,:,1]>120)&(rgb[:,:,2]>90)
 lab,_=ndi.label(bright,structure=np.ones((3,3)));sz=np.bincount(lab.ravel());dist=ndi.distance_transform_edt(~blue);yy,xx=np.indices(active.shape)
 seed=bright&(sz[lab]<400)&(dist<10)&(xx<560)&(yy>290)&(yy<480)
 ids=np.unique(lab[seed]);ids=ids[ids!=0];mask=np.isin(lab,ids)&bright
 # Preserve actual hood/robe cloth: only tiny isolated pigments beside the scarf are selected.
 inside=ndi.binary_fill_holes(blue)|ndi.binary_closing(blue,iterations=3)
 nearest=ndi.distance_transform_edt(~blue,return_distances=False,return_indices=True)
 recolor=mask&inside;a[recolor,:3]=rgb[nearest[0][recolor],nearest[1][recolor]]
 remove=mask&~inside;a[remove]=0
 assert np.array_equal(a[~mask],before[~mask]);assert not np.any(a[mask,0]>140)
 high=Image.fromarray(a);high.save(f)
 detail=game_frame(high,anchor,4);native=game_frame(high,anchor);name=ed['names'][i]
 detail.save(runtime/'detail4x'/(name+'.png'));native.save(runtime/'native224'/(name+'.png'));frames.append(detail)
 buf=io.BytesIO();detail.save(buf,format='PNG');images.append('data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
 checks.append({'frame':name,'sourceFrame':an['checks'][i]['sourceFrame'],'isolatedBrightScarfPixelsRemoved':int(remove.sum()),'interiorScarfPixelsRecolored':int(recolor.sum()),'outsideTargetExact':True})
 for k,pic in enumerate([Image.fromarray(before),high]):
  crop=pic.crop((290,300,640,480)).resize((580,298),Image.Resampling.NEAREST).crop((0,65,580,215));bg=Image.new('RGBA',crop.size,'#30363d');bg.alpha_composite(crop);zoom.alpha_composite(bg,(k*600,i*150))
 bg=checker((400,245),20);bg.alpha_composite(high.crop((250,170,1050,660)).resize((400,245),Image.Resampling.LANCZOS));x=i%4*400;y=i//4*280;board.paste(bg.convert('RGB'),(x,y+30));bd.text((x+10,y+3),f.stem,font=font,fill='#172333')
ed['images']=images;ed['kind']='hammer-rush-scarf-clean-14';ed['note']+=' Targeted scarf edge white-residue cleanup; pose, timing and root preserved.'
(source/'editor.json').write_text(json.dumps(ed,ensure_ascii=False),encoding='utf-8');an['scarfCleanup']=checks;an['priorSet']=old
(source/'animation.json').write_text(json.dumps(an,indent=2),encoding='utf-8')
board.save(preview/'frames.png');zoom.convert('RGB').save(preview/'scarf-before-after.png')
for label,color in [('dark','#30363d'),('white','white'),('checker',None)]:
 imgs=[]
 for f in frames:
  crop=f.crop((128,160,800,640));bg=checker(crop.size,24) if color is None else Image.new('RGBA',crop.size,color);bg.alpha_composite(crop);imgs.append(bg.convert('RGB'))
 times=ed['ms'][:];times[-1]+=650
 imgs[0].save(preview/f'rush-14-{label}.gif',save_all=True,append_images=imgs[1:],duration=times,loop=0,disposal=2,optimize=False)
frames[7].crop((128,160,800,640)).save(preview/'held-transparent.png')
files=sorted(p for kind in ['source','runtime','preview'] for p in (assetdir/kind/sid).rglob('*') if p.is_file())
for p in files:
 asset['files'].append(p.relative_to(assetdir).as_posix());raw=p.read_bytes();generated['files'].append({'path':p.relative_to(res).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
entry=dict(asset['generatedSets'][old]);entry.update({k:f'{k}/{sid}' for k in ['source','runtime','preview']});entry['priorSet']=old;entry['approval']='selected motion; scarf residue refinement pending review';asset['generatedSets'][sid]=entry
next(x for x in catalog['assets'] if x['id']==asset['id'])['fileCount']=len(asset['files']);payloads.append((source/'editor.json').relative_to(res).as_posix())
for p in meta:assert p.read_bytes()==snap[p]
for p,d in zip(meta,[asset,catalog,generated,payloads]):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
common=root.parents[2]/'05_공유/공통_픽셀편집기';subprocess.run([sys.executable,'-X','utf8',str(common/'set_project.py'),str(source/'editor.json'),'--title','망치 몰아치기 14장 · 목도리 흰 잔색 추가 제거','--keep-active'],check=True)
print(json.dumps({'frames':14,'targetPixels':sum(c['isolatedBrightScarfPixelsRemoved']+c['interiorScarfPixelsRecolored'] for c in checks),'timingUnchanged':True,'originalPreserved':True,'set':sid}))
