"""Three-color export of selected A. Preserve all original and comparison PNGs."""
from pathlib import Path
from PIL import Image
import numpy as np,json
ROOT=Path(__file__).resolve().parents[1]
B=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
SRC=B/'phase1-playtest/effects/ink-burst/aether-compare/A'
OUT=SRC.parent/'a-clean';(OUT/'frames').mkdir(parents=True,exist_ok=True)
pal=np.array([[12,10,22],[52,30,73],[106,68,135]])
stats=[]
for i in range(16):
 a=np.array(Image.open(SRC/f'{i:02}.png').convert('RGBA'));rgb=a[:,:,:3].astype(float)
 # Reject pale matte mixtures and neutral grey edge contamination; keep black ink.
 keep=(a[:,:,3]>0)&(rgb.min(axis=2)<110)&(((rgb[:,:,2]>=rgb[:,:,0]+3)&(rgb[:,:,2]>=rgb[:,:,1]+3))|(rgb.max(axis=2)<48))
 index=((rgb[:,:,None,:]-pal[None,None,:,:])**2).sum(axis=3).argmin(axis=2)
 out=np.zeros_like(a);out[:,:,:3]=pal[index];out[:,:,3]=keep*255;out[~keep]=0
 image=Image.fromarray(out);bottom=image.getbbox()[3]
 if bottom!=72:
  shifted=Image.new('RGBA',(96,80));shifted.paste(image,(0,72-bottom));image=shifted
 image.save(OUT/'frames'/f'{i:02}.png')
 stats.append({'frame':i,'opaquePixels':int(keep.sum()),'mattePixelsRemoved':int(((a[:,:,3]>0)&~keep).sum())})
sheet=Image.new('RGBA',(768,160))
for i in range(16):sheet.paste(Image.open(OUT/'frames'/f'{i:02}.png'),(i%8*96,i//8*80))
sheet.save(OUT/'frames.png')
(OUT/'palette.json').write_text(json.dumps({'source':'../A/source-original.png','palette':pal.tolist(),'alpha':[0,255],'canvas':[96,80],'groundY':72,'selected':'A','gameIntegration':'not-performed','stats':stats},indent=2)+'\n',encoding='utf-8')
p=SRC.parent/'index.html';s=p.read_text(encoding='utf-8')
s=s.replace('에테르 잉크 분출 A/B · 바닥선 고정','A안 · 3색 배경 정리').replace('에테르 잉크 분출 · A / B','선택 A안 · 3색 / 외곽 정리')
s=s.replace('A: 잔방울이 많은 버전 / B: 큰 덩어리 중심 버전.','위: A 원본 추출 / 아래: A 3색 정리본. 흰색이 섞인 외곽을 제거하고 바닥선은 유지했습니다.')
s=s.replace('../../../../map-preview','../../../../../map-preview').replace("['A','B']","['../A','frames']")
s=s.replace('A · 잔방울 버전','A · 원본 추출').replace('B · 큰 덩어리 버전','A · 3색 정리본')
s=s.replace('기존 1페이즈 효과는 선택 전까지 유지.','정리본 검토 · Unity 적용 전.')
s=s.replace('<canvas width="960"','<p><label>배경 <select id="bg"><option value="map">기존 맵</option><option value="white">흰색</option><option value="dark">짙은 회청색</option></select></label></p><canvas width="960"')
s=s.replace('ctx.drawImage(map,0,0,960,520);',"if($('#bg').value==='map')ctx.drawImage(map,0,0,960,520);else{ctx.fillStyle=$('#bg').value==='white'?'#ffffff':'#16202a';ctx.fillRect(0,0,960,520);}")
(OUT/'index.html').write_text(s,encoding='utf-8')
print('A: 16 frames, exactly 3 colors, binary alpha, ground=72')
