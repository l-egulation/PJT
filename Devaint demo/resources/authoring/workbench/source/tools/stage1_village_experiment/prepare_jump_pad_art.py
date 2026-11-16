"""Import whole native-alpha CLI cutouts at a shared ground-contact anchor."""
from pathlib import Path
import hashlib,json,shutil,subprocess
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='experiment/stage1-village-reference'
SRC=ROOT/'output/imagegen/super-jump-placement'
DEST=ROOT/'unity/Assets/Stage1VillageExperiment/Resources/Village/JumpPadArt'
DEST.mkdir(parents=True,exist_ok=True)
CONFIG=ROOT/'data/stage1-village-experiment/jump-pad-art.json'
config=json.loads(CONFIG.read_text(encoding='utf-8'))
records=[]
# Complete generated canvases, registered by the cap width and hard base contact.
# Keep native transparency; no paper removal or color-alpha overwrite is performed.
for name,source_center,cap_width,base_y in [('color',886,1470,786),('sketch',886,1344,778)]:
    path=SRC/(name+'-native-alpha.png')
    image=Image.open(path).convert('RGBA')
    assert image.getchannel('A').getextrema()==(0,255), 'Real generated alpha required'
    scale=config['topWidthPixels']/cap_width
    size=(round(image.width*scale),round(image.height*scale))
    resized=image.resize(size,Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',(config['canvasWidth'],config['canvasHeight']))
    x=round(config['anchorX']-source_center*scale)
    y=round(config['anchorY']-base_y*scale)
    assert x>=4 and y>=4 and x+size[0]<=canvas.width-4 and y+size[1]<=canvas.height-4
    canvas.alpha_composite(resized,(x,y))
    canvas.save(DEST/(name+'.png'))
    records.append({'name':name,'source':path.relative_to(ROOT).as_posix(),'sourceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'scale':scale,'sourceGroundContactY':base_y,'spriteSha256':hashlib.sha256((DEST/(name+'.png')).read_bytes()).hexdigest(),'canvas':[canvas.width,canvas.height],'placementOffset':[x,y]})
shutil.copy2(CONFIG,DEST/'layout.json')
motion=ROOT/'data/stage1-village-experiment/jump-pad-motion.json'
if motion.exists():shutil.copy2(motion,DEST/'motion.json')
(SRC/'import-manifest.json').write_text(json.dumps({'model':'gpt-image-1.5','method':'imagegen CLI native-alpha cutouts; uniform downsample and full-canvas ground registration','approval':'pending placement review','artifacts':records},indent=2),encoding='utf-8')
print('Prepared two padded native-alpha sprites and one visual-only placement.')
