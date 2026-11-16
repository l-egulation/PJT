from pathlib import Path
import json
from PIL import Image

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
DEST=ROOT/'unity/Assets/Deviant/World/Resources/RedInk'
SOURCES={'mark':(Path(r'C:\Users\SSAFY\Downloads\8fcdf1dd-8f84-43e1-9873-8ac2effa0cd4.png'),2),'burst':(Path(r'C:\Users\SSAFY\Downloads\dbe43dc7-a7e4-4249-8059-c1977be0740a.png'),3)}
for kind,(path,grid) in SOURCES.items():
    src=Image.open(path).convert('RGB');frames=[]
    for n in range(grid*grid):
        x,y=n%grid,n//grid
        im=src.crop((round(x*src.width/grid)+2,round(y*src.height/grid)+2,round((x+1)*src.width/grid)-2,round((y+1)*src.height/grid)-2)).resize((128,128),Image.Resampling.NEAREST).convert('RGBA')
        pixels=im.load()
        for py in range(128):
            for px in range(128):
                r,g,b,a=pixels[px,py]
                pixels[px,py]=(r,g,b,255) if r>55 and r>g+35 and r>b+35 else (0,0,0,0)
        frames.append(im)
    valid=[f.getbbox() for f in frames if f.getbbox()]
    box=(min(b[0] for b in valid),min(b[1] for b in valid),max(b[2] for b in valid),max(b[3] for b in valid))
    folder=DEST/kind;folder.mkdir(parents=True,exist_ok=True)
    mirror=HERE/kind;mirror.mkdir(exist_ok=True)
    sheet=Image.new('RGBA',(grid*128,grid*128))
    if kind=='mark':
        target=(8,12)
        for n,f in enumerate(frames):
            part=f.crop(box).resize(target,Image.Resampling.NEAREST)
            tile=Image.new('RGBA',(128,128));tile.alpha_composite(part,(60,58))
            tile.save(folder/f'{n+1:02}.png');tile.save(mirror/f'{n+1:02}.png')
            sheet.alpha_composite(tile,(n%grid*128,n//grid*128))
    else:
        longest=max(box[2]-box[0],box[3]-box[1]);size=round(128*40/longest)
        for n,f in enumerate(frames):
            # Keep the red family, but separate the burst from the existing solid-red hurt flash.
            px=f.load()
            for y in range(128):
                for x in range(128):
                    r,g,b,a=px[x,y]
                    if a and r>180:px[x,y]=(255,80,65,a)
            part=f.resize((size,size),Image.Resampling.NEAREST)
            tile=Image.new('RGBA',(128,128));tile.alpha_composite(part,((128-size)//2,(128-size)//2))
            tile.save(folder/f'{n+1:02}.png');tile.save(mirror/f'{n+1:02}.png')
            sheet.alpha_composite(tile,(n%grid*128,n//grid*128))
    sheet.save(HERE/f'{kind}_transparent_sheet.png')
(HERE/'sizes.json').write_text(json.dumps({'markVisiblePixels':[8,12],'burstPeakPixels':40,'spriteCell':[128,128],'ppu':32,'presets':{'small':[6,9,32],'recommended':[8,12,40],'large':[10,15,48]}},indent=2),encoding='utf-8')
print('Prepared transparent PNGs. Mark 8x12 visible pixels; burst 40px peak; PPU32.')
