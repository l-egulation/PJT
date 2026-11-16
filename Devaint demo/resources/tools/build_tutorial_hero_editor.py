"""Build the offline, self-contained hero pixel editor from registered native PNGs."""
from pathlib import Path
import base64,json,hashlib
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
GROUP=ROOT/'ui/first-run-tutorial-pixel'
frames=[]
for name,label in [('hero-idle','대기'),('hero-punch','공격')]:
    im=Image.open(GROUP/'runtime'/f'{name}.png').convert('RGBA')
    frames.append({'id':name,'label':label,'width':im.width,'height':im.height,
                   'rgba':base64.b64encode(im.tobytes()).decode('ascii')})
palette=json.loads((GROUP/'source/character-palette.json').read_text(encoding='utf-8'))['palette']
labels={'cream':'밝은 옷','shadow':'옷 그림자','ink':'잉크·윤곽','scarf':'목도리','scarfLight':'목도리 밝은색','gold':'눈·금색'}
revision=hashlib.sha256(''.join(f['rgba']for f in frames).encode('ascii')).hexdigest()[:16]
seed={'format':'deviant-hero-pixel-editor-v1','documentId':'hero-game-palette-'+revision,
      'palette':[{'label':labels[key],'hex':value}for key,value in palette.items()],'frames':frames}
template=(GROUP/'source/hero-editor-template.html').read_text(encoding='utf-8')
assert template.count('__EDITOR_SEED__')==1
output=GROUP/'authoring/hero-pixel-editor.html'
output.parent.mkdir(exist_ok=True)
output.write_text(template.replace('__EDITOR_SEED__',json.dumps(seed,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
payloads=ROOT/'.lfs-payloads.json'
values=json.loads(payloads.read_text(encoding='utf-8-sig'))
relative=output.relative_to(ROOT).as_posix()
if relative not in values:values.append(relative)
payloads.write_text(json.dumps(values,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(output)
