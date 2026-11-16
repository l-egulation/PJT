"""Build a design-only seven-platform composite from the user's layout sketch."""
from pathlib import Path
import shutil,json,hashlib
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'monsters/stage-02/boss-siblings-lineup-20261103'
OUT=BASE/'phase1-device-concepts/simplified-layout-v2'
OUT.mkdir(parents=True,exist_ok=True)
source=Path('C:/Users/SSAFY/.codex/generated_images/01a18def-9c96-7602-bc0f-d707e8e958ed/exec-e9719cfb-2a71-4e32-8935-487f78cbd593.png')
sketch=Path('C:/Users/SSAFY/AppData/Local/Temp/codex-clipboard-0cb25753-f507-409e-a9c5-4e845ffa468f.png')
shutil.copy2(source,OUT/'device-generated-original.png')
shutil.copy2(sketch,OUT/'user-layout-sketch.png')
im=Image.open(source).convert('RGBA'); a=np.array(im);yy,xx=np.where(a[:,:,3]>128)
im=im.crop((xx.min(),yy.min(),xx.max()+1,yy.max()+1))
im=im.resize((round(im.width*80/im.height),80),Image.Resampling.NEAREST)
a=np.array(im);mask=a[:,:,3]>128
palette=np.array([[232,211,178],[178,167,151],[189,143,61],[128,104,51],[76,91,105],[36,22,46]],dtype=np.int32)
d=((a[:,:,:3].astype(np.int32)[:,:,None,:]-palette[None,None,:,:])**2).sum(3)
a[:,:,:3]=palette[d.argmin(2)];a[:,:,3]=np.where(mask,255,0);a[~mask]=0
device=Image.fromarray(a);device.save(OUT/'device-pixel-80.png')
large=Image.new('RGBA',(device.width*6+96,576));large.paste(device.resize((device.width*6,480),Image.Resampling.NEAREST),(48,48));large.save(OUT/'device-enlarged.png')
assets=BASE/'map-preview/assets';scene=Image.open(assets/'D.png').convert('RGBA')
terrain=Image.open(assets/'terrain-preview.png').convert('RGBA');tile=terrain.crop((128,192,224,208))
# Two upper, three middle, two lower, following the supplied sketch.
platforms=[{'x':288,'y':162,'width':96},{'x':544,'y':166,'width':96},
 {'x':144,'y':294,'width':96},{'x':432,'y':286,'width':96},{'x':720,'y':288,'width':96},
 {'x':304,'y':446,'width':96},{'x':624,'y':434,'width':96}]
for p in platforms:scene.alpha_composite(tile,(p['x'],p['y']))
scene.alpha_composite(device,(480-device.width//2,286-80))
player=Image.open(assets/'approved-player.png').convert('RGBA');player=player.crop(player.getbbox())
scene.alpha_composite(player,(164,294-player.height))
scene.convert('RGB').save(OUT/'map-placement-960x540.png')
diagram=Image.new('RGB',(960,540),'#eef0ed');draw=ImageDraw.Draw(diagram)
for i,p in enumerate(platforms):
 draw.rectangle((p['x'],p['y'],p['x']+95,p['y']+11),fill='#67727c');draw.text((p['x']+43,p['y']+18),str(i+1),fill='#39434d')
diagram.paste(device,(480-device.width//2,206),device);diagram.paste(player,(164,294-player.height),player)
diagram.save(OUT/'layout-diagram.png')
meta={'status':'design-review','gameIntegration':'not-performed','canvas':[960,540],
 'platforms':platforms,'centralPlatformIndex':3,'deviceHeightPx':80,'playerOpaqueHeightPx':player.height,
 'placementNote':'Sketch proportions approximated. Platform spacing, device size and jump reach are review values, not final combat geometry.',
 'devicePalette':palette.tolist(),'originalSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'sketchSHA256':hashlib.sha256(sketch.read_bytes()).hexdigest(),
 'background':'../../map-preview/assets/D.png','platformSource':'../../map-preview/assets/terrain-preview.png','platformSourceRect':[128,192,96,16]}
(OUT/'layout.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
asset=json.loads((BASE/'asset.json').read_text(encoding='utf-8'));asset['files']=sorted(str(p.relative_to(BASE)).replace('\\','/') for p in BASE.rglob('*') if p.is_file() and p.name!='asset.json');asset['motionReview']['phase1Device']='simplified-design-review'
(BASE/'asset.json').write_text(json.dumps(asset,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
for e in catalog['assets']:
 if e['id']==asset['id']:e['fileCount']=len(asset['files'])
(ROOT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert len(platforms)==7 and player.height==64
print('Seven platforms; center device 80px; original player 64px; unchanged background')
