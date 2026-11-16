from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from scipy import ndimage as ndi
from io import BytesIO
from zipfile import ZipFile,ZIP_DEFLATED
import numpy as np,json,hashlib,base64,subprocess
P=Path(__file__).resolve().parent;ROOT=P.parents[1]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='feat/story-unity-essentials'
B=P/'background-cleanup-20261111';C=B/'v2';C.mkdir(exist_ok=True)
manifest=json.loads((B/'source-manifest.json').read_text(encoding='utf-8'))
first=manifest['firstFrames'];second=manifest['secondFrames'];ms1=manifest['firstMs'];ms2=manifest['secondMs']
rows=[]
for row in manifest['files']:
 name=row['file'];source=B/'original'/name;prior=B/'transparent'/name
 assert hashlib.sha256(source.read_bytes()).hexdigest()==row['sha256']
 raw=np.array(Image.open(source).convert('RGB'));old=np.array(Image.open(prior).convert('RGBA'));out=old.copy()
 a=raw.astype(float); rgb=old[...,:3].astype(float); alpha=old[...,3]/255
 r,g,b=a.transpose(2,0,1);yy,xx=np.indices(alpha.shape)
 depth=ndi.distance_transform_edt(alpha>.5);opaque=alpha>.96
 dark=a.max(-1)<85
 blue=(b-r>45)&(g-r>25)&(a.mean(-1)<150)&opaque
 gold=(r-g>22)&(g-b>55)&(r>150)&(b<130)&opaque
 warm=(r-b>20)&(r-g>3)&opaque
 db,ib=ndi.distance_transform_edt(~blue,return_indices=True)
 dg,ig=ndi.distance_transform_edt(~gold,return_indices=True)
 dw=ndi.distance_transform_edt(~warm)
 dd,ii=ndi.distance_transform_edt(~dark,return_indices=True)
 # Only scarf-facing boundaries and ankle cuffs; preserve interior folds.
 scarf=(db<4.5)&(db<dw)&(depth<=2.2)&(alpha>0)
 ankle=(dg<4)&(yy>285)&(depth<=2.2)&(alpha>0)&(dg<=dd+0.5)
 target=(scarf|ankle)&~dark
 indices=np.where(ankle[None,:,:],ig,ib)
 pigment=rgb[tuple(indices)]
 source_pigment=a[tuple(indices)]
 v=255-source_pigment
 fit=np.clip(((255-a)*v).sum(-1)/np.maximum((v*v).sum(-1),1),0,1)
 fit=np.clip((fit-.10)/.90,0,1)
 out[target,:3]=np.rint(pigment[target]).astype(np.uint8)
 out[target,3]=np.rint(np.minimum(alpha[target],fit[target])*255).astype(np.uint8)
 shoe_edge=(yy>285)&(dg<11)&(dd<2.8)&(dd<dg)&(depth<=1.5)&(alpha>0)&~dark
 ink=a[tuple(ii)];v=255-ink
 ia=np.clip(((255-a)*v).sum(-1)/np.maximum((v*v).sum(-1),1),0,1)
 out[shoe_edge,:3]=np.rint(ink[shoe_edge]).astype(np.uint8)
 out[shoe_edge,3]=np.rint(np.minimum(alpha[shoe_edge],np.clip((ia[shoe_edge]-.14)/.86,0,1))*255).astype(np.uint8)
 # Reconstruct gray arcs consistently, including their intersections with
 # scarf/robe edges. The former distant-only mask left alternating dark bands.
 chroma=np.ptp(a,axis=-1);gray=a.mean(-1)
 neutral=(chroma<18)&(gray>100)&(gray<249)
 effect=np.zeros_like(neutral)
 if row['sourceFrame'] in [6,7,9,19,20,21,22] and neutral.any():
  fx_labels,n=ndi.label(neutral)
  sizes=np.bincount(fx_labels.ravel())
  minimum=60 if row['sourceFrame']==22 else 400
  effect=neutral&(sizes[fx_labels]>=minimum)
  if row['sourceFrame']==22:effect&=(xx>398)&(yy>220)&(yy<292)
  effect=ndi.binary_dilation(effect)&(chroma<22)&(gray>95)&(gray<249)&(dd>1.3)
  pigment_gray=float(np.floor(gray[effect].min())) if effect.any() else 150.
  fa=np.clip((255-gray)/(255-pigment_gray),0,1)
  fa=np.clip((fa-.055)/.945,0,1)
  out[effect,:3]=round(pigment_gray)
  out[effect,3]=np.rint(fa[effect]*255).astype(np.uint8)
  # Only discard detached low-coverage neutral specks; keep painted details.
  specks=neutral&~ndi.binary_dilation(effect,iterations=2)&(alpha<.35)&(depth<1)&(dd>2)&(db>3)&(dw>3)&(dg>3)
  out[specks]=0
 # Tighten very faint fringes within requested regions and around effects.
 local=target|effect|shoe_edge
 out[local&(out[...,3]<42)]=0
 out[out[...,3]==0]=0
 mask=out[...,3]>0;labs,n=ndi.label(mask);counts=np.bincount(labs.ravel())
 detached=mask&(counts[labs]<4)&~dark
 out[detached]=0
 # Never cut black face/hand/shoe pigment or alter unaffected body interiors.
 out[dark]=old[dark]
 assert np.array_equal(out[dark],old[dark])
 assert out.shape==(360,640,4)
 assert not out[...,3][raw.min(-1)>=249].any()
 dst=C/'transparent'/name;dst.parent.mkdir(exist_ok=True);Image.fromarray(out).save(dst)
 orig=C/'original'/name;orig.parent.mkdir(exist_ok=True);orig.write_bytes(source.read_bytes())
 rows.append(dict(row,output=str(dst),outputSha256=hashlib.sha256(dst.read_bytes()).hexdigest(),modifiedPixels=int(np.any(old!=out,axis=-1).sum()),previousSha256=hashlib.sha256(prior.read_bytes()).hexdigest()))
(C/'source-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(C/'validation.json').write_text(json.dumps(dict(frames=19,size=[640,360],cycleMs=680,spatialResampling=False,paletteQuantization=False,darkPaintPreserved=True,alignmentUnchanged=True,files=rows),indent=2),encoding='utf-8')
# Same native-frame exports and timing as the accepted version; separate files.
export=(P/'clean_selected.py').read_text(encoding='utf-8')
export=export[export.index('font=ImageFont.truetype'):]
export=export.replace('맨손_배경제거_외곽색보정_19장.zip','맨손_배경제거_외곽색보정_v2_19장.zip').replace('unarmed-cleanup-19.html','unarmed-cleanup-19-v2.html').replace('배경 / 외곽 / 색 보정</h1>','발목·목도리·이펙트 외곽 2차 정리</h1>')
export=export.replace('box=(170,125,455,355)','box=(170,125,500,355)').replace('resize((285,230)','resize((330,230)').replace('resize((570,460)','resize((660,460)')
exec(compile(export,'export_refined_selected','exec'),globals())
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
board=Image.new('RGB',(1400,1120),'#20262d');d=ImageDraw.Draw(board)
for j,i in enumerate([3,7,19,20]):
 for col,folder in enumerate([B,C]):
  im=Image.open(folder/'transparent'/f'frame-{i:02d}.png').crop((240,145,415,350)).resize((350,410),Image.Resampling.NEAREST)
  bg=Image.new('RGBA',im.size,'#20262d');bg.alpha_composite(im)
  x=j%2*700+col*350;y=j//2*560;board.paste(bg.convert('RGB'),(x,y+40));d.text((x+10,y+8),f'#{i} '+('이전' if col==0 else '2차'),font=font,fill='white')
board.save(C/'edge-comparison.png')
