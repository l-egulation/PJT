"""Reassemble selected masonry around vertical edges with a continuous depth field."""
from pathlib import Path
import argparse,shutil,json,subprocess,hashlib
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt,binary_dilation

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--motifs',type=Path,required=True);ap.add_argument('--destination',type=Path,required=True);args=ap.parse_args()
 p=args.destination.resolve();s=args.source.resolve()
 if p.exists():raise SystemExit('Use a new destination')
 shutil.copytree(s,p)
 d=json.loads((p/'layout.json').read_text(encoding='utf-8'));src=json.loads((p/'source/stage1-seed-17.json').read_text(encoding='utf-8'));g=np.array(src['ground'],bool).reshape(d['worldCells'][1],d['worldCells'][0]);rows,cols=g.shape
 # A vertical exposed face extends its material into the solid by two cells.
 left=np.zeros_like(g);right=np.zeros_like(g);left[:,1:]=g[:,1:]&~g[:,:-1];right[:,:-1]=g[:,:-1]&~g[:,1:]
 vertical=left|right;zone=binary_dilation(vertical,structure=np.ones((3,5),bool))&g
 bank=[np.array(Image.open(args.motifs/(name+'.png')).convert('RGBA'))[:32,:,:3].astype(float) for name in ['plain-a','plain-b','plain-c','moss-light-a','moss-light-b']]
 # The walking-surface highlight belongs only on an exposed top, never repeated on a wall.
 for face in bank:face[:3]=face[4:5]
 original=p.parent/'left-windowlight-v19-20261031';changed=0
 for gy in range(d['chunks'][1]):
  for gx in range(d['chunks'][0]):
   x0,y0=gx*32,gy*24;sub=zone[y0:y0+24,x0:x0+32]
   if not sub.any():continue
   path=p/'chunks'/f'{gx}-{gy}.png';im=Image.open(path).convert('RGBA');a=np.array(im);h,w=a.shape[:2]
   # True distance to nearby air at native pixel resolution; one shared field turns corners.
   pad=3;local=np.pad(g,((pad,pad),(pad,pad)),constant_values=True)[y0:y0+24+2*pad,x0:x0+32+2*pad]
   pix=np.repeat(np.repeat(local,32,axis=0),32,axis=1);dist=distance_transform_edt(pix)[96:96+h,96:96+w]
   texture=np.zeros((h,w,3),float)
   for cy in range(sub.shape[0]):
    for cx in range(sub.shape[1]):
     state=((x0+cx)*73856093^(y0+cy)*19349663)&0xffffffff;idx=state%5 if state%100<25 else state%3
     yy,xx=cy*32,cx*32;texture[yy:yy+32,xx:xx+32]=bank[idx]
   # Keep the face shallow: fade to the approved core within 40px (1.25 units).
   light=np.clip((40-dist)/32,0,1);light=light*light*(3-2*light)
   rgb=texture*light[:,:,None]+np.array([29,41,42])*(1-light[:,:,None])
   # Softly hand over to the selected floor material at the side-zone's inner boundary.
   z=np.repeat(np.repeat(sub,32,axis=0),32,axis=1)[:h,:w]
   zpad=np.pad(z,1);blend=np.clip(distance_transform_edt(zpad)[1:-1,1:-1]/24,0,1)
   # Original alpha protects irregular outer stones and the complete terrain silhouette.
   a[:,:,:3]=np.rint(a[:,:,:3]*(1-blend[:,:,None])+rgb*blend[:,:,None]).astype('uint8')
   ref=Image.open(s/'chunks'/path.name).convert('RGBA');assert np.array_equal(a[:,:,3],np.array(ref)[:,:,3])
   Image.fromarray(a).save(path);changed+=1
 subprocess.run(['python',str(p/'render_depth.py')],check=True)
 html=(p/'source-view.html').read_text(encoding='utf-8').replace('기본 돌·드문 이끼·2/3칸 연결 이끼를 고정 시드로 배치했습니다.','세로 벽과 계단 모서리의 돌 재질·안쪽 명암을 연결했습니다.')
 (p/'source-view.html').write_text(html,encoding='utf-8');subprocess.run(['python',str(p/'package_preview.py')],check=True)
 validation={'verticalEdgeCells':int(vertical.sum()),'changedChunks':changed,'alphaUnchanged':True,'geometryUnchanged':(p/'geometry-data.js').read_bytes()==(s/'geometry-data.js').read_bytes(),'unityApplied':False,'browserVerified':False}
 (p/'vertical-connection-validation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8');print(validation)

if __name__=='__main__':main()
