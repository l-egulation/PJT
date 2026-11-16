"""Prepare the unchanged selected pixel base on the requested solid white canvas."""
from pathlib import Path
import hashlib,json,shutil
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]


def build():
    spec=json.loads((ROOT/'data/presentation/stage2_walk_retry.json').read_text(encoding='utf-8'))
    ident=spec['activeId'];source=ROOT/spec['source'];out=ROOT/spec['output'];out.mkdir(parents=True,exist_ok=True)
    (out/'references').mkdir(exist_ok=True)
    shutil.copy2(source,out/'references/base-frame.png')
    shutil.copy2(source.parent.parent/'palette.json',out/'references/palette.json')
    im=Image.open(source).convert('RGBA');assert im.size==tuple(spec['canvas'])
    white=Image.new('RGBA',im.size,spec['background']);white.alpha_composite(im);white=white.convert('RGB')
    a=np.array(im);b=np.array(white);assert np.array_equal(a[a[:,:,3]==255,:3],b[a[:,:,3]==255]);assert np.all(b[a[:,:,3]==0]==255)
    white.save(out/f'{ident}-white-native.png');scale=spec['scale'];large=white.resize((white.width*scale,white.height*scale),Image.Resampling.NEAREST);large.save(out/f'{ident}-white-input-4x.png')
    bb=im.getbbox();box=(bb[0]-8,bb[1]-8,bb[2]+8,bb[3]+8);preview=white.crop(box);preview.resize((preview.width*scale,preview.height*scale),Image.Resampling.NEAREST).save(out/f'{ident}-white-preview-4x.png')
    if 'prompt' in spec:
        prompt=spec['prompt']
    else:
        prompt=(ROOT/spec['priorPrompt']).read_text(encoding='utf-8').replace('Keep the transparent background and identical canvas dimensions in every frame.','Keep a perfectly solid pure white (#FFFFFF) background and identical canvas dimensions in every frame.').replace('full-canvas transparent PNG frames','full-canvas white-background PNG frames').replace('No captions, grids, background, cast shadows or effects.','No captions, grids, scenery, cast shadows or effects. Keep the entire tank and both boots fully inside the canvas in every pose. Do not auto-zoom or crop the character.')
    assert 'transparent' not in prompt
    (out/'prompt-white.txt').write_text(prompt,encoding='utf-8')
    (out/'metadata.json').write_text(json.dumps({**spec,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'visibleSize':[bb[2]-bb[0],bb[3]-bb[1]],'inputSize':list(large.size),'foregroundUnchanged':True,'previewIsCropped':True},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(out/f'{ident}-white-input-4x.png')


if __name__=='__main__':build()
