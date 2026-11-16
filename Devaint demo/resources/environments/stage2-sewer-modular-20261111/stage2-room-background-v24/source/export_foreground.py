from pathlib import Path
from PIL import Image,ImageChops
import json,hashlib
ROOT=Path(__file__).resolve().parents[1];PACK=ROOT.parent/'stage2-unified-stone-v21';REVIEW=ROOT.parent/'stage2-unified-map-review-v22'
code=(PACK/'source/preview_map.py').read_text(encoding='utf-8');code=code[:code.index("font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)")]
code=code.replace('ROOT=Path(__file__).resolve().parents[1]','ROOT=PACK').replace("bg=Image.new('RGBA',(ww,hh),(255,255,255,255))","bg=Image.new('RGBA',(ww,hh),(0,0,0,0))").replace("return bg.crop((pad,pad,pad+W,pad+H)).convert('RGB')","return bg.crop((pad,pad,pad+W,pad+H)).convert('RGBA')")
scope={'__file__':__file__,'PACK':PACK};exec(code,scope)
manifest=json.loads((REVIEW/'manifest.json').read_text(encoding='utf-8'))
assert hashlib.sha256(scope['DATA'].read_bytes()).hexdigest()==manifest['sourceSha256']
for v in manifest['views']:
 fg=scope['render'](*v['camera'],actor=tuple(v['heroFeet']));assert fg.size==(960,540)
 white=Image.new('RGBA',fg.size,'white');white.alpha_composite(fg);old=Image.open(REVIEW/'preview'/v['screen']).convert('RGB')
 assert ImageChops.difference(white.convert('RGB'),old).getbbox() is None,v['id']
 fg.save(ROOT/'runtime/foreground'/v['screen'])
manifest['foregroundReconstructsPreviousScreensExactly']=True
(ROOT/'source/foreground-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('All8 foreground layers reconstruct the previous white previews pixel-for-pixel.')
