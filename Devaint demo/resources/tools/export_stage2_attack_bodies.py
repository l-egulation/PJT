"""Pad preserved approved bodies without baking projectile/world FX into actor sprites."""
from pathlib import Path
import argparse,json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
GROUP=ROOT/'monsters/stage-02/attack-fx-selected'
def export(destination):
    destination=Path(destination)
    if destination.exists() and any(destination.iterdir()):raise ValueError('Use a new empty destination')
    destination.mkdir(parents=True,exist_ok=True)
    data=json.loads((GROUP/'source/body-bindings.json').read_text('utf-8'))
    for body in data['bodies']:
        for i,path in enumerate(body['sources']):
            image=Image.open(GROUP/path).convert('RGBA');assert image.size==(384,224)
            canvas=Image.new('RGBA',(512,384));canvas.alpha_composite(image,(128,68))
            out=destination/body['id']/f'{i:02}.png';out.parent.mkdir(parents=True,exist_ok=True);canvas.save(out)
    return data
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--destination',required=True);args=p.parse_args();export(args.destination)
