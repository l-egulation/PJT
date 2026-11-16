"""Check the additional art deliverables without applying any consumer files."""
from pathlib import Path
import argparse
import colorsys
import json
from collections import Counter
from statistics import median

from PIL import Image

ROOT=Path(__file__).resolve().parents[1]


def main(main_root=None):
    layout=json.loads((ROOT/'authoring/needed-assets/source/output-layout.json').read_text(encoding='utf-8'))
    records=layout['sprites'];assert len(records)==151
    assert len({r['id'] for r in records})==151
    assert layout['runtimeApplied'] is False and layout['selectionStatus']=='unselected-candidates'
    images={}
    for r in records:
        with Image.open(ROOT/r['path']) as source:
            im=source.convert('RGBA');assert list(im.size)==r['size'],r['path']
            assert set(im.getchannel('A').getdata())<={0,255},r['path']
            assert all(a or not(r or g or b) for r,g,b,a in im.getdata()),r['path']
            images[r['path']]=im
    kinds=Counter(r['kind'] for r in records)
    assert kinds['input-glyph']==41 and kinds['blueprint-card']==3 and kinds['blueprint-illustration']==3
    clock='environments/clock-tower/runtime/needed-art'
    before=images[clock+'/ink-altar/altar-before.png'];after=images[clock+'/ink-altar/altar-after.png']
    assert before.size==(48,40) and before.getbbox()[3]==40
    assert before.getchannel('A').tobytes()==after.getchannel('A').tobytes()
    colors={name:[] for name in ['red','yellow','green','blue','purple']}
    for y in range(before.height):
        for x in range(before.width):
            r,g,b,a=before.getpixel((x,y))
            h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if not a or s<.42 or v<.25:continue
            degrees=h*360
            name='red' if degrees<30 or degrees>=340 else 'yellow' if degrees<75 else 'green' if degrees<170 else 'blue' if degrees<260 else 'purple'
            colors[name].append((x,y))
    centers=[]
    for name,points in colors.items():
        assert points,'Missing altar color: '+name
        centers.append(median(x for x,y in points))
        assert sum(max(after.getpixel(p)[:3]) for p in points)<sum(max(before.getpixel(p)[:3]) for p in points)
    assert centers==sorted(centers) and len(set(centers))==5
    assert images[clock+'/ink-altar/appear/altar-appear-01.png'].getbbox() is None
    assert images[clock+'/ink-altar/appear/altar-appear-08.png'].tobytes()==before.tobytes()
    assert images[clock+'/ink-altar/use/altar-use-08.png'].tobytes()==after.tobytes()
    assert images[clock+'/chapter1-exit/open/exit-open-08.png'].tobytes()==images[clock+'/chapter1-exit/exit-open.png'].tobytes()
    for r in records:
        im=images[r['path']]
        if r['kind']=='input-glyph':assert im.height==18 and im.width>=17
        if r['kind']=='blueprint-illustration':
            assert sum(v>0 for v in im.getchannel('A').getdata())/(im.width*im.height)<.6
        if r['kind'] in ('ink-destruction','altar-choice-acquire','altar-choice-discard','pigment-pickup-contrast'):
            if r['name'].endswith('-08'):assert im.getbbox() is None
            else:assert any(a and max(red,green,blue)<140 for red,green,blue,a in im.getdata()),r['path']
    for name in ['palette-atelier','supply-office','reclaim-workshop']:
        first=f'environments/village/runtime/effects/needed-art/color-spread/{name}/color-spread-01.png'
        last=first.replace('-01.png','-08.png')
        assert images[first].getbbox() is None
        original=Image.open(ROOT/f'environments/village/runtime/buildings/starting-hub/color/{name}.png').convert('RGBA')
        assert images[last].tobytes()==original.tobytes()
    helper='characters/story/ink-helper/runtime/'
    assert images[helper+'sacrifice/ink-helper-sacrifice-01.png'].tobytes()==images[helper+'ink-helper.png'].tobytes()
    assert images[helper+'sacrifice/ink-helper-sacrifice-08.png'].getbbox() is None
    if main_root:
        timing=json.loads((main_root/'data/combat/presentation/base_sword_hit.json').read_text(encoding='utf-8'))
        frames=[r for r in records if r['kind']=='basic-sword-hit-replacement-candidate'];total=0;ends=[]
        for r in frames:total+=r['frameDurationMs'];ends.append(round(total/1000,5))
        assert ends==timing['frameEnds']
    print('PASS: 151 unselected outputs; five ordered altar colors and dimmed used state; '
          '41 readable-size keycaps; cutouts without paper; effect endpoints/contrast; '
          'exact building masks and character dissolve endpoints. No Unity files applied.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--main-root',type=Path)
    a=p.parse_args();main(a.main_root.resolve() if a.main_root else None)
