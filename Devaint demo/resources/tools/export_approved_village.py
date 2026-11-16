"""Render the approved hub sources to an empty review destination (Pillow + NumPy)."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
from authoring_output import destination
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'environments/village/approved-hub-20261101/source'

def opaque(image,sketch):
    d=np.array(image.convert('RGBA')).astype(float);rgb=d[:,:,:3];lum=rgb@np.array([.2126,.7152,.0722]);base=np.array([244,240,229])
    d[:,:,3]=np.clip((d[:,:,3]-128)*8,0,255)
    d[:,:,:3]=base-np.maximum(0,245-lum)[:,:,None]*.53 if sketch else (lum[:,:,None]+(rgb-lum[:,:,None])*.48)*.65+base*.35
    return Image.fromarray(np.clip(d,0,255).astype('uint8'),'RGBA')

def export(out):
    bindings=json.loads((SOURCE/'bindings.json').read_text(encoding='utf-8'))
    rows=[]
    for key,row in bindings['assets'].items():
        b=row['boxes'][0];w=round((b[2]-b[0])*row['scale']);h=round((b[3]-b[1])*row['scale'])
        group='buildings' if key in ['palette_atelier','supply_office','reclaim_workshop'] else 'props'
        for state in ['color','sketch']:
            im=Image.open(SOURCE/row[state]).crop(b).convert('RGBA').resize((w,h),Image.Resampling.LANCZOS)
            rel=Path(group)/state/(key.replace('_','-')+'.png');p=out/rel;p.parent.mkdir(parents=True,exist_ok=True);im.save(p)
            rows.append({'id':key+'.'+state,'file':str(rel).replace('\\','/'),'size':[w,h],'ppu':32,'pivot':[.5,0]})
    for state in ['color','sketch']:
        sk=state=='sketch'
        for key,box,size in [('market',(0,477 if sk else 0,1536,(477 if sk else 0)+510),(1456,412)),('trees',(0,478 if sk else 0,1536,(478 if sk else 0)+510),(1400,389)),('tower',(938 if sk else 170,40,(938 if sk else 170)+450,540),(280,311)),('clouds',(768 if sk else 0,570,(768 if sk else 0)+768,970),(600,160))]:
            filename='market-neutral' if key=='market' else 'distant' if key in ['tower','clouds'] else key
            im=Image.open(SOURCE/'background'/(filename+'.png')).convert('RGBA');d=np.array(im)
            if sk and key in ['market','trees']:d[:540,:,3]=0
            im=opaque(Image.fromarray(d),sk).crop(box).resize(size,Image.Resampling.LANCZOS)
            p=out/'background'/state/(key+'.png');p.parent.mkdir(parents=True,exist_ok=True);im.save(p)
        sky=Image.new('RGBA',(960,540),(244,240,229,255)) if sk else Image.new('RGBA',(960,540),(184,212,223,255))
        if not sk:
            layer=Image.open(SOURCE/'background/sky.png').crop((0,0,1536,512)).convert('RGBA').resize((960,540),Image.Resampling.LANCZOS)
            layer.putalpha(layer.getchannel('A').point(lambda a:round(a*.45)));sky.alpha_composite(layer)
        sky.save(out/'background'/state/'sky.png')
    for name,width in [('altar-ready',80),('altar-spent',80),('exit-closed',96),('exit-open',96)]:
        im=Image.open(SOURCE/'boss'/(name+'.png')).convert('RGBA');h=round(im.height*width/im.width)
        p=out/'boss'/(name+'.png');p.parent.mkdir(exist_ok=True);im.resize((width,h),Image.Resampling.LANCZOS).save(p)
    for key in ['palette-atelier','supply-office','reclaim-workshop']:
        im=Image.open(out/'buildings/color'/(key+'.png')).convert('RGBA');d=np.array(im)
        yy,xx=np.indices((im.height,im.width))
        for i in range(1,9):
            f=d.copy();f[:,:,3]=np.where(xx+yy<(im.width+im.height)*i/8,d[:,:,3],0)
            p=out/'effects'/key/f'{i:02}.png';p.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(f).save(p)
    for name,kind in [('altar-ready','appear'),('altar-spent','use'),('exit-open','exit')]:
        im=Image.open(out/'boss'/(name+'.png')).convert('RGBA')
        for i in range(1,9):
            f=im.copy();f.putalpha(im.getchannel('A').point(lambda a:round(a*i/8)))
            p=out/'boss'/kind/f'{i:02}.png';p.parent.mkdir(parents=True,exist_ok=True);f.save(p)
    (out/'sprites.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Rendered',len(rows),'hub sprites, 10 background layers, 4 boss states')

if __name__=='__main__':export(destination(__doc__))
