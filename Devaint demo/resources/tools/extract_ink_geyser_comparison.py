"""Extract user Aether sheets with fixed scale and individually locked ground."""
from pathlib import Path
from PIL import Image
import numpy as np
import shutil,json
ROOT=Path(__file__).resolve().parents[1]
B=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
OUT=B/'phase1-playtest/effects/ink-burst/aether-compare';OUT.mkdir(exist_ok=True)
sources={'A':'7cd8b4da-531b-4679-9199-4445e6e223e0.png','B':'fa30e6f3-5437-4076-a5b2-e80f611d0841.png'}
records={}
for label,name in sources.items():
 dest=OUT/label;dest.mkdir(exist_ok=True)
 src=Path('C:/Users/SSAFY/Downloads')/name;shutil.copy2(src,dest/'source-original.png');im=Image.open(src).convert('RGBA');meta=[]
 for i in range(16):
  cx=i%4*256;cy=i//4*256
  cell=im.crop((cx+5,cy+5,cx+251,cy+251));a=np.array(cell)
  # Dark/purple ink only: discard white matte and pale grey sheet dividers.
  mask=(a[:,:,:3].min(axis=2)<170)&(a[:,:,3]>128)
  ys,xs=np.where(mask);bottom=int(ys.max())+1
  # Ground contact center, rather than drifting crown/particle bounding center.
  groundxs=xs[ys>=bottom-10];center=float(np.median(groundxs)) if len(groundxs) else 123
  a[:,:,3]=mask*255;a[~mask]=0
  cut=Image.fromarray(a).resize((69,69),Image.Resampling.NEAREST)
  sx=round(center*69/246);sy=round(bottom*69/246)
  out=Image.new('RGBA',(96,80));out.paste(cut,(48-sx,72-sy));out.save(dest/f'{i:02}.png')
  meta.append({'frame':i,'sourceCell':[cx+5,cy+5,246,246],'groundSourceY':bottom,'groundSourceX':center,'offset':[48-sx,72-sy]})
 sheet=Image.new('RGBA',(96*8,80*2))
 for i in range(16):sheet.paste(Image.open(dest/f'{i:02}.png'),(i%8*96,i//8*80))
 sheet.save(dest/'frames.png');records[label]=meta
(OUT/'alignment.json').write_text(json.dumps({'canvas':[96,80],'anchor':[48,72],'sourceScale':69/246,'frameMs':90,'originalTiming':'not supplied; comparison uses same 90ms per frame for both','processing':'white matte removal, common nearest-neighbor scale, ground translation only; no per-frame resizing, no particle pruning, no palette replacement','frames':records},indent=2)+'\n',encoding='utf-8')
print('A/B: 16 frames each; identical scale; contact anchor [48,72]')
