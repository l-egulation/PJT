"""Suppress isolated temporal rim spikes on native elite animation frames."""
import json,io,base64,shutil
from pathlib import Path
import numpy as np
from scipy import ndimage as ndi
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];DEST=ROOT/'approved';MID='04_well-warden'
BACK=ROOT/'local-assets/99_archive/before-elite-outline-free-motions-v4'
OUT=ROOT/'local-assets/elite-outline-free-motions-v4'
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def enc(im):
 b=io.BytesIO();im.save(b,format='PNG');return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()
def smooth(ims,pal,loop,geometry=True):
 arr=[np.array(im) for im in ims];masks=[a[:,:,3]>0 for a in arr];out=[];stats=[]
 for i,a in enumerate(arr):
  b=a.copy();mask=masks[i];remove=np.zeros_like(mask);fill=remove.copy();recolor=remove.copy()
  if len(arr)>2 and (loop or 0<i<len(arr)-1):
   prev=arr[(i-1)%len(arr)];nxt=arr[(i+1)%len(arr)];pm=prev[:,:,3]>0;nm=nxt[:,:,3]>0
   near=ndi.convolve(mask.astype(int),np.ones((3,3),int),mode='constant')
   orth=ndi.convolve(mask.astype(int),np.array([[0,1,0],[1,0,1],[0,1,0]]),mode='constant')
   mass=ndi.convolve(mask.astype(int),np.ones((5,5),int),mode='constant')
   stable=(ndi.convolve((mask==pm).astype(int),np.ones((3,3),int),mode='constant')>=7)&(ndi.convolve((mask==nm).astype(int),np.ones((3,3),int),mode='constant')>=7)
   labs,n=ndi.label(mask,np.ones((3,3)));sizes=np.bincount(labs.ravel());support=(sizes[labs]>=120)&mask
   if geometry:
    remove=support&~pm&~nm&stable&(orth<=1)&(near<=4)&(mass>=12)
    fill=~mask&pm&nm&stable&(orth>=3)&(near>=6)&(mass>=18)&ndi.binary_dilation(support)
    # Keep the lowest contact pixels and all isolated particles / chain details.
    ys=np.where(mask)[0]
    if len(ys):remove[max(0,ys.max()-2):]=False;fill[max(0,ys.max()-2):]=False
    b[remove]=0
    nearest=ndi.distance_transform_edt(~mask,return_distances=False,return_indices=True)
    b[fill]=a[nearest[0][fill],nearest[1][fill]]
   idx=np.argmin(((a[:,:,None,:3].astype(float)-pal)**2).sum(3),2)
   pi=np.argmin(((prev[:,:,None,:3].astype(float)-pal)**2).sum(3),2)
   ni=np.argmin(((nxt[:,:,None,:3].astype(float)-pal)**2).sum(3),2)
   votes=np.stack([ndi.convolve(((idx==c)&mask).astype(int),np.ones((3,3),int),mode='constant') for c in range(6)])
   dom=votes.argmax(0);selfvotes=np.take_along_axis(votes,idx[None],axis=0)[0]
   depth=ndi.distance_transform_cdt(mask,metric='chessboard')
   recolor=support&~remove&pm&nm&stable&(depth==1)&(pi==ni)&(pi==dom)&(idx>dom)&(selfvotes<=2)&(votes.max(0)>=5)
   b[recolor,:3]=pal[dom[recolor]]
  out.append(Image.fromarray(b));stats.append({'frame':i+1,'removed':int(remove.sum()),'filled':int(fill.sum()),'recolored':int(recolor.sum())})
 return out,stats

def main():
 OUT.mkdir(parents=True,exist_ok=True)
 if not BACK.exists():
  BACK.mkdir(parents=True);shutil.copytree(DEST/MID,BACK/MID);shutil.copy2(DEST/'manifest.json',BACK/'manifest.json')
 manifest=read(BACK/'manifest.json');mon=next(x for x in manifest['monsters'] if x['id']==MID);pal=np.array([list(bytes.fromhex(c[1:])) for c in manifest['palette']],dtype=np.uint8)
 preview={'id':MID,'name':mon['name'],'motions':{}};report={};allims={};remap={}
 def save(m,ims):
  d=read(BACK/m['editorJSON'])
  for key,f,im in zip(d.get('sourceFrames',[0]),m['frames'],ims):
   im.save(DEST/f)
   if 'images' in d:d['images'][str(key)]=enc(im)
   else:d['image']=enc(im)
  d['temporalRimCleanup']='elite-outline-free-motions-v4';write(DEST/m['editorJSON'],d)
  if m.get('sheet'):
   sheet=Image.new('RGBA',(ims[0].width*len(ims),ims[0].height))
   for j,im in enumerate(ims):sheet.paste(im,(j*im.width,0))
   sheet.save(DEST/m['sheet'])
  m['temporalRimCleanup']='elite-outline-free-motions-v4'
 for key,m in mon['motions'].items():
  originals=[Image.open(BACK/f).convert('RGBA') for f in m['frames']]
  if key=='death':
   body=m['layers']['body'];bi=[Image.open(BACK/f).convert('RGBA') for f in body['frames']];bout,stats=smooth(bi,pal,False,False);save(body,bout);ims=[]
   for j,im in enumerate(originals):
    oc=Image.new('RGBA',im.size);oc.paste(bi[j],tuple(m['bodyOffset']));nc=Image.new('RGBA',im.size);nc.paste(bout[j],tuple(m['bodyOffset']));a=np.array(im);o=np.array(oc);b=np.array(nc);fx=np.array(Image.open(BACK/m['layers']['fx']['frames'][j]).convert('RGBA'));edit=(o[:,:,3]>0)&(fx[:,:,3]==0)&np.all(a==o,2);a[edit]=b[edit];ims.append(Image.fromarray(a))
  else:ims,stats=smooth(originals,pal,m['loop'],key in ['idle','walk','attack'])
  for old,new in zip(originals,ims):
   remap[old.tobytes()]=new
   a=np.array(old);b=np.array(new);assert old.size==new.size;assert set(map(tuple,b[b[:,:,3]>0,:3]))<=set(map(tuple,pal));inner=ndi.distance_transform_cdt(a[:,:,3]>0,metric='chessboard')>1
   assert np.array_equal(a[inner],b[inner])
  save(m,ims);allims[key]=ims;report[key]=stats;preview['motions'][key]={'before':[enc(im) for im in originals],'after':[enc(im) for im in ims],'ms':m['ms'],'loop':m['loop'],'pivot':m['pivot']}
 for name in ['hook-until-pull.json','claw-followup.json']:
  p=Path(MID)/'attack'/name;d=read(BACK/p)
  for k,v in d['images'].items():
   im=Image.open(io.BytesIO(base64.b64decode(v.split(',')[1]))).convert('RGBA');assert im.tobytes() in remap;d['images'][k]=enc(remap[im.tobytes()])
  d['temporalRimCleanup']='elite-outline-free-motions-v4';write(DEST/p,d)
 for f in (BACK/MID/'death/fx').rglob('*'):
  if f.is_file():assert f.read_bytes()==(DEST/f.relative_to(BACK)).read_bytes()
 manifest['eliteTemporalRimCleanup']='elite-outline-free-motions-v4';write(DEST/'manifest.json',manifest);write(OUT/'data.json',[preview]);write(OUT/'verification.json',report)
 html=(ROOT/'local-assets/elite-outline-free-motions-v3/index.html').read_text(encoding='utf-8').replace('잉크 가장자리 색 보정 후','프레임 경계 정돈 후').replace('잉크·금속 재질색 분리','외곽 1픽셀 튐 보정').replace('정돈 전','v3 보정 전');(OUT/'index.html').write_text(html,encoding='utf-8')
 lut={tuple(c):i+1 for i,c in enumerate(pal)};gpal=[255]*3+pal.ravel().tolist();gpal += [0]*(768-len(gpal));gifs=[];durations=[]
 for key in ['idle','walk','attack','hit','death']:
  m=mon['motions'][key]
  for repeat in range(2 if key in ['idle','walk'] else 1):
   for im,ms in zip(allims[key],m['ms']):
    canvas=Image.new('RGBA',(480,208));canvas.paste(im,(330-round(m['pivot'][0]*im.width),192-round((1-m['pivot'][1])*im.height)));p=Image.new('P',canvas.size);p.putpalette(gpal);p.putdata([lut[tuple(v[:3])] if v[3] else 0 for v in np.array(canvas).reshape(-1,4)]);gifs.append(p.resize((960,416),Image.Resampling.NEAREST));durations.append(ms)
  gifs.append(gifs[-1].copy());durations.append(650)
 gifs[0].save(OUT/(MID+'.gif'),save_all=True,append_images=gifs[1:],duration=durations,loop=0,optimize=False,disposal=1)
 print(json.dumps({k:{t:sum(s[t] for s in v) for t in ['removed','filled','recolored']} for k,v in report.items()}))
if __name__=='__main__':main()
