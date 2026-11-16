"""Export generated standalone UI frames using uniform scaling and transparent padding only."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from PIL import Image
from authoring_output import prepare_output

def write_json(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def export(recipe_path,destination):
    recipe=json.loads(recipe_path.read_text(encoding='utf-8'))
    out=prepare_output(destination)
    (out/'source').mkdir();(out/'runtime').mkdir()
    frames=[]
    for entry in recipe['assets']:
        source=Path(entry['path']); original=out/'source'/(entry['id']+'.png')
        shutil.copy2(source,original)
        image=Image.open(original).convert('RGBA')
        # Extreme horizontal sprites are generated with transparent canvas padding.
        # Only that padding is trimmed; drawing pixels are never warped or repainted.
        if entry['id'].startswith(('title-','primary-')):
            bounds=image.getchannel('A').point(lambda a:255 if a>=64 else 0).getbbox()
        else:
            bounds=(0,0,*image.size)
        sprite=image.crop(bounds)
        target=(entry['w']*4,entry['h']*4)
        scale=min(target[0]/sprite.width,target[1]/sprite.height)
        fitted=sprite.resize((round(sprite.width*scale),round(sprite.height*scale)),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',target)
        offset=((target[0]-fitted.width)//2,(target[1]-fitted.height)//2)
        canvas.alpha_composite(fitted,offset)
        runtime=out/'runtime'/(entry['id']+'.png');canvas.save(runtime)
        frames.append({'id':entry['id'],'source':original.relative_to(out).as_posix(),
            'file':runtime.relative_to(out).as_posix(),'logicalSize':[entry['w'],entry['h']],
            'sourceSize':list(image.size),'sourceRect':list(bounds),'exportSize':list(target),
            'uniformScale':scale,'paddingOffset':list(offset),
            'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'sha256':hashlib.sha256(runtime.read_bytes()).hexdigest(),
            'renderPolicy':'preserve-aspect-contain; no nine-slice; no nonuniform scaling'})
    write_json(out/'source/generation.json',recipe)
    write_json(out/'runtime/frames.json',{'format':'deviant-native-workshop-frames-v1','status':'review-not-unity-integrated','frames':frames})
    files=sorted(p.relative_to(out).as_posix() for p in out.rglob('*') if p.is_file())
    write_json(out/'asset.json',{'format':'deviant-resource-asset-v1','id':'ui.workshop-frames-20261109',
        'path':'ui/workshop-frames-20261109','status':'generated-independent-frame-review',
        'approval':'pending visual review','gameIntegration':'not-applied-to-unity',
        'sources':['source/generation.json'],'files':files})
    return out

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe',required=True,type=Path)
    parser.add_argument('--destination',required=True,type=Path)
    args=parser.parse_args()
    print(export(args.recipe,args.destination))
