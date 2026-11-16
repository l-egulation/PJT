"""Export the generated concept atlas onto explicit native pixel canvases.

This is a nearest-neighbour, fixed-palette production conversion, not native
low-resolution generation. The unmodified generated source remains in source/.
"""
from pathlib import Path
import hashlib
import json
import random
import shutil
import argparse
import base64
from PIL import Image, ImageDraw, ImageFilter, ImageChops
from tutorial_sword_pixels import variants, PALETTE as SWORD_PALETTE
from tutorial_tool_pixels import variants as tool_variants

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'ui/first-run-tutorial-pixel'
PALETTE = ['#2c2d2c', '#f3e8cb', '#cbbb9f', '#a25bcb', '#294d5b',
           '#377184', '#d8a934', '#97b07d', '#827e76', '#b97870', '#181a1a']
HERO_PALETTE = {'cream':'#fdeeda','shadow':'#d2b396','ink':'#1b1616',
                'scarf':'#025673','scarfLight':'#02648c','gold':'#eba516'}
HERO_REMAP = {'#f3e8cb':'cream','#cbbb9f':'shadow','#181a1a':'ink','#2c2d2c':'ink',
              '#294d5b':'scarf','#377184':'scarfLight','#d8a934':'gold',
              '#97b07d':'shadow','#827e76':'shadow'}
SPECS = [('sketch',32,32), ('sword',32,32), ('hammer',32,32), ('ink',32,32),
         ('lock',16,16), ('arrow',24,16), ('curved-arrow',32,24), ('broken-sword',32,32),
         ('keycap',24,24), ('panel',48,48), ('slot',32,24), ('brush',64,16),
         ('hero-idle',64,64), ('hero-punch',96,64), ('gauge',64,8), ('gauge-fill',64,8)]

def write(path, value):
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8'))

def export(source):
    (GROUP/'source').mkdir(parents=True, exist_ok=True)
    (GROUP/'runtime').mkdir(exist_ok=True)
    (GROUP/'preview').mkdir(exist_ok=True)
    if source.resolve() != (GROUP/'source/generated-atlas.png').resolve():
        shutil.copy2(source, GROUP/'source/generated-atlas.png')
    atlas=Image.open(source).convert('RGBA')
    colors=[tuple(bytes.fromhex(c[1:])) for c in PALETTE]
    sword_edit = GROUP/'source/sword-user-edit.json'
    document = json.loads(sword_edit.read_text(encoding='utf-8-sig'))
    frame, = document['frames']
    assert document['format'] == 'deviant-sword-pixel-editor-v1'
    assert (frame['id'], frame['width'], frame['height']) == ('sword', 32, 32)
    pixels = base64.b64decode(frame['rgba'], validate=True)
    assert len(pixels) == 4096 and set(pixels[3::4]) <= {0, 255}
    native_swords = variants(Image.frombytes('RGBA', (32, 32), pixels))
    native_tools=tool_variants()
    native_icons={**native_swords,**native_tools}
    user_edit=GROUP/'source/hero-user-edit.json'
    user_frames={}
    if user_edit.exists():
        edited=json.loads(user_edit.read_text(encoding='utf-8-sig'))
        assert edited['format']=='deviant-hero-pixel-editor-v1'
        assert len(edited['frames'])==2
        expected={'hero-idle':(64,64),'hero-punch':(96,64)}
        for frame in edited['frames']:
            name=frame['id'];size=(frame['width'],frame['height'])
            assert name in expected and size==expected[name] and name not in user_frames
            raw=base64.b64decode(frame['rgba'],validate=True)
            assert len(raw)==size[0]*size[1]*4 and set(raw[3::4])<={0,255}
            user_frames[name]=Image.frombytes('RGBA',size,raw)
    reference=GROUP/'source/reference-hero-idle.png'
    reference_bytes=reference.read_bytes()
    assert hashlib.sha256(reference_bytes).hexdigest()=='8adf9a881f5c01c7407bd4468bd3c1b67d012b91d951c9ee8fb1546eab9ee9fc'
    write(GROUP/'source/character-palette.json',{'sourceAsset':'characters.ink.common',
          'sourcePath':'characters/ink/common/runtime/forest-character/idle/01.png',
          'sourceSHA256':hashlib.sha256(reference_bytes).hexdigest(),'palette':HERO_PALETTE,
          'mapping':HERO_REMAP,'operation':'exact color replacement; geometry and alpha preserved'})
    write(GROUP/'source/sword-pixels.json',{'width':32,'height':32,'origin':'authored native integer-grid pixels; no image resampling',
          'palette':SWORD_PALETTE,'pixels':list(native_swords['sword'].getdata())})
    write(GROUP/'source/hammer-arrow-pixels.json',{'origin':'authored native integer-grid pixels; no image resampling',
          'palette':SWORD_PALETTE,'images':[{'name':name,'width':im.width,'height':im.height,'pixels':list(im.getdata())}
                                         for name,im in native_tools.items()]})
    # The ground is a one-pixel baseline with sparse tufts, sized for the attack panel.
    ground = Image.new('RGBA', (344, 15))
    pen = ImageDraw.Draw(ground)
    pen.line((0, 10, 343, 10), fill='#928a7c')
    pen.line((6, 11, 337, 11), fill='#625f56')
    rng = random.Random(53)
    for x in range(4, 340, 3):
        y = rng.choice((7, 8, 9, 10, 11, 12))
        pen.point((x, y), fill=rng.choice(('#625f56', '#a69c8d', '#827e76')))
        if rng.randrange(4) == 0:
            pen.line((x, 9, x - 2, y - 3), fill='#827e76')
            pen.point((x + 1, y - 2), fill='#a69c8d')
    ground.save(GROUP/'runtime/grass-line.png')
    review=Image.new('RGB',(896,768),'#2c2d2c')
    draw=ImageDraw.Draw(review)
    for i,(name,w,h) in enumerate(SPECS):
        col,row=i%4,i//4
        tile=atlas.crop((col*atlas.width//4,row*atlas.height//4,(col+1)*atlas.width//4,(row+1)*atlas.height//4))
        mask=tile.getchannel('A').point(lambda a:255 if a>=192 else 0)
        bounds=mask.getbbox()
        tile=tile.crop(bounds)
        # Keep aspect ratio and two pixels of transparent breathing room.
        factor=min((w-4)/tile.width,(h-4)/tile.height)
        tw,th=max(1,round(tile.width*factor)),max(1,round(tile.height*factor))
        tile=tile.resize((tw,th),Image.Resampling.NEAREST)
        result=Image.new('RGBA',(w,h))
        pixels=[]
        for r,g,b,a in tile.getdata():
            if a<192: pixels.append((0,0,0,0)); continue
            if name=='sketch': rgb=colors[1]
            elif name in ('slot','brush'): rgb=(255,255,255)
            elif name.startswith('hero-') and max(r,g,b)<65: rgb=colors[10]
            else: rgb=min(colors,key=lambda c:(r-c[0])**2+(g-c[1])**2+(b-c[2])**2)
            pixels.append((*rgb,255))
        tile.putdata(pixels)
        result.paste(tile,((w-tw)//2,(h-th)//2))
        # Simple UI frames are authored directly on their native pixel grid.
        if name in ('keycap','panel','gauge','gauge-fill'):
            result=Image.new('RGBA',(w,h))
            d=ImageDraw.Draw(result)
            if name=='gauge-fill': d.rectangle((1,2,w-2,h-3),fill=colors[7]+(255,))
            else:
                d.rectangle((1,1,w-2,h-2),fill=colors[0]+(255,),outline=colors[1]+(255,),width=1)
                for x,y in [(1,1),(w-2,1),(1,h-2),(w-2,h-2)]:d.point((x,y),fill=(0,0,0,0))
        if name == 'panel':
            result = Image.new('RGBA', (w,h))
            d = ImageDraw.Draw(result)
            # Two-pixel ivory border with a four-pixel chamfer at each corner.
            outer = [(6,1),(w-7,1),(w-2,6),(w-2,h-7),(w-7,h-2),(6,h-2),(1,h-7),(1,6)]
            inner = [(7,3),(w-8,3),(w-4,7),(w-4,h-8),(w-8,h-4),(7,h-4),(3,h-8),(3,7)]
            d.polygon(outer, fill='#f3ebd9')
            d.polygon(inner, fill='#292a29')
        if name == 'brush':
            result = Image.new('RGBA', (w,h))
            d = ImageDraw.Draw(result)
            rng = random.Random(17)
            for y in range(1,h-1):
                taper = abs(y-h//2)//2
                d.line((1+taper+rng.randrange(4),y,w-2-taper-rng.randrange(4),y),fill='white')
        if name == 'ink':
            result = Image.new('RGBA', (32,32))
            d = ImageDraw.Draw(result)
            # Symmetric row spans keep both cheeks on the same pixel staircase.
            radii = [0,0,1,1,2,3,3,4,5,5,6,7,7,8,9,9,10,10,10,10,10,10,9,9,8,7,5,3]
            for y, radius in enumerate(radii, start=2):
                d.line((16-radius,y,16+radius,y),fill='#bd70ed')
            d.line((12,13,10,17),fill='#f3e8db')
            d.line((10,17,10,21),fill='#f3e8db')
            d.point((10,23),fill='#f3e8db')
        if name == 'slot':
            result = Image.new('RGBA',(32,24))
            ImageDraw.Draw(result).polygon([(1,15),(3,11),(5,10),(7,6),(10,6),(12,4),(15,5),
                (18,3),(20,4),(23,2),(26,3),(27,5),(30,6),(30,10),(28,13),(27,16),
                (24,17),(22,20),(18,20),(15,22),(13,20),(10,21),(8,19),(5,20),(4,17),(2,17)],fill='white')
            alpha=result.getchannel('A')
            empty=result.copy()
            empty.putalpha(ImageChops.subtract(alpha,alpha.filter(ImageFilter.MinFilter(3))))
            empty.save(GROUP/'runtime/slot-empty.png')
        if name == 'keycap':
            result = Image.new('RGBA',(24,24))
            ImageDraw.Draw(result).rounded_rectangle((1,1,22,22),radius=3,outline='#f3ebd9',width=1)
        if name in native_icons: result=native_icons[name]
        if name.startswith('hero-'):
            remapped=[]
            for r,g,b,a in result.getdata():
                if not a: remapped.append((0,0,0,0)); continue
                key=f'#{r:02x}{g:02x}{b:02x}'
                rgb=bytes.fromhex(HERO_PALETTE[HERO_REMAP[key]][1:])
                remapped.append((*rgb,a))
            result.putdata(remapped)
        # User-finished pixels take precedence over generated/conversion recipes.
        if name in user_frames: result=user_frames[name]
        assert set(result.getchannel('A').getdata()) <= {0,255}
        result.save(GROUP/'runtime'/f'{name}.png')
        # One shared scale lets the two 64px-tall poses be compared honestly.
        scale=2
        shown=result
        if name in ('slot','brush'):
            shown=Image.new('RGBA',result.size,'#a25bcb' if name=='slot' else '#6b794f')
            shown.putalpha(result.getchannel('A'))
        enlarged=shown.resize((w*scale,h*scale),Image.Resampling.NEAREST)
        px,py=col*224+(224-enlarged.width)//2,row*192+104-enlarged.height//2
        draw.rectangle((col*224,row*192,col*224+223,row*192+191),outline='#414441')
        review.paste(enlarged,(px,py),enlarged)
        title={'slot':'Palette slot (tinted)','brush':'Keyword brush (tinted)'}.get(name,name)
        draw.text((col*224+12,row*192+10),title,fill='#f3e8cb')
        draw.text((col*224+12,row*192+26),f'{w}x{h} / 2x preview',fill='#a9b1a7')
    review.save(GROUP/'preview/native-pixels-2x.png')
    # Retain old links, but correct their misleading mixed-scale contents too.
    review.save(GROUP/'preview/native-pixels-4x.png')
    swords_review=Image.new('RGB',(864,336),'#2c2d2c')
    review_draw=ImageDraw.Draw(swords_review)
    for index,(name,im) in enumerate(native_swords.items()):
        enlarged=im.resize((256,256),Image.Resampling.NEAREST)
        swords_review.paste(enlarged,(index*288+16,8),enlarged)
        swords_review.paste(im,(index*288+16,284),im)
        review_draw.text((index*288+60,292),name+' 32x32 / 8x',fill='#eee9d3')
    swords_review.save(GROUP/'preview/sword-32px-review.png')
    tools_review=Image.new('RGB',(576,336),'#2c2d2c')
    review_draw=ImageDraw.Draw(tools_review)
    for index,(name,im) in enumerate(native_tools.items()):
        enlarged=im.resize((im.width*8,im.height*8),Image.Resampling.NEAREST)
        tools_review.paste(enlarged,(index*288+16,8+(256-enlarged.height)//2),enlarged)
        tools_review.paste(im,(index*288+16,284),im)
        review_draw.text((index*288+60,292),f'{name} {im.width}x{im.height} / 8x',fill='#eee9d3')
    tools_review.save(GROUP/'preview/hammer-arrow-review.png')
    heroes=Image.new('RGB',(704,352),'#2c2d2c')
    pen=ImageDraw.Draw(heroes)
    for index,name in enumerate(('hero-idle','hero-punch')):
        im=Image.open(GROUP/'runtime'/f'{name}.png').convert('RGBA')
        enlarged=im.resize((im.width*3,im.height*3),Image.Resampling.NEAREST)
        heroes.paste(enlarged,(index*352+(352-enlarged.width)//2,40),enlarged)
        pen.text((index*352+20,16),f'{name} / 3x preview',fill='#fdeeda')
    for i,(name,color) in enumerate(HERO_PALETTE.items()):
        x=16+i*114
        pen.rectangle((x,268,x+26,294),fill=color)
        pen.text((x,302),name,fill='#fdeeda')
        pen.text((x,318),color,fill='#fdeeda')
    heroes.save(GROUP/'preview/hero-game-palette.png')
    write(GROUP/'source/export.json',{'operation':'nearest-neighbour native-size export with fixed palette; not native low-res generation',
          'source':'generated-atlas.png','alpha':[0,255],'palette':PALETTE,
          'unity':{'filter':'Point','mipmaps':False,'compression':'None','integerScale':True},
          'preview':{'scale':2,'slotTint':'#a25bcb','brushTint':'#6b794f','runtimeMasksRemainWhite':True},
          'userSwordEdit':{'path':sword_edit.name,'sha256':hashlib.sha256(sword_edit.read_bytes()).hexdigest()},
          'userCharacterEdit':{'path':'hero-user-edit.json','sha256':hashlib.sha256(user_edit.read_bytes()).hexdigest()} if user_frames else None,
          'additionalSprites':[{'name':'grass-line','width':344,'height':15},{'name':'slot-empty','width':32,'height':24}],
          'sprites':[{'name':n,'width':w,'height':h,'origin':'user-finished native pixels' if n in user_frames else 'authored native grid' if n in native_icons or n in ('ink','slot','brush') else 'native grid frame' if n in ('keycap','panel','gauge','gauge-fill') else 'converted generated concept'}for n,w,h in SPECS]})
    files=sorted(p.relative_to(GROUP).as_posix()for p in GROUP.rglob('*')if p.is_file()and p.name!='asset.json')
    write(GROUP/'asset.json',{'format':'deviant-resource-asset-v1','id':'ui.first-run-tutorial-pixel','path':'ui/first-run-tutorial-pixel',
          'name':'첫 런 튜토리얼 네이티브 규격 픽셀 적용본','approval':'pending-review','status':'draft','files':files})
    catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8-sig'))
    catalog['assets']=[x for x in catalog['assets']if x['id']!='ui.first-run-tutorial-pixel']
    catalog['assets'].append({'id':'ui.first-run-tutorial-pixel','path':'ui/first-run-tutorial-pixel','fileCount':len(files)})
    write(ROOT/'catalog.json',catalog)
    generated=json.loads((ROOT/'generated-assets.json').read_text(encoding='utf-8-sig'))
    generated['files']=[x for x in generated['files']if not x['path'].startswith('ui/first-run-tutorial-pixel/')]
    for name in files:
        p=GROUP/name;data=p.read_bytes()
        generated['files'].append({'path':p.relative_to(ROOT).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    write(ROOT/'generated-assets.json',generated)
    print('Exported 18 native-size sprites. Alpha is binary; source preserved. Artwork review pending.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source',type=Path)
    export(parser.parse_args().source)
