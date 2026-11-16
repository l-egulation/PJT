"""Prepare additional review art outside the source and consumer checkouts."""
from pathlib import Path
import argparse
import json
import math
from collections import deque

from PIL import Image, ImageDraw, ImageFilter, ImageOps

from authoring_output import ROOT, prepare_output

FONT = {
 'A':['.###.','#...#','#...#','#####','#...#','#...#','#...#'],
 'B':['####.','#...#','#...#','####.','#...#','#...#','####.'],
 'C':['.####','#....','#....','#....','#....','#....','.####'],
 'D':['####.','#...#','#...#','#...#','#...#','#...#','####.'],
 'E':['#####','#....','#....','####.','#....','#....','#####'],
 'F':['#####','#....','#....','####.','#....','#....','#....'],
 'H':['#...#','#...#','#...#','#####','#...#','#...#','#...#'],
 'I':['#####','..#..','..#..','..#..','..#..','..#..','#####'],
 'L':['#....','#....','#....','#....','#....','#....','#####'],
 'M':['#...#','##.##','#.#.#','#.#.#','#...#','#...#','#...#'],
 'N':['#...#','##..#','##..#','#.#.#','#..##','#..##','#...#'],
 'P':['####.','#...#','#...#','####.','#....','#....','#....'],
 'Q':['.###.','#...#','#...#','#...#','#.#.#','#..#.','.##.#'],
 'R':['####.','#...#','#...#','####.','#.#..','#..#.','#...#'],
 'S':['.####','#....','#....','.###.','....#','....#','####.'],
 'T':['#####','..#..','..#..','..#..','..#..','..#..','..#..'],
 'V':['#...#','#...#','#...#','#...#','#...#','.#.#.','..#..'],
 'W':['#...#','#...#','#...#','#.#.#','#.#.#','##.##','#...#'],
 'X':['#...#','#...#','.#.#.','..#..','.#.#.','#...#','#...#'],
 'Y':['#...#','#...#','.#.#.','..#..','..#..','..#..','..#..'],
 '3':['####.','....#','....#','.###.','....#','....#','####.'],
}
KEYBOARD = [('f','F'),('e','E'),('q','Q'),('w','W'),('r','R'),('a','A'),
            ('s','S'),('d','D'),('i','I'),('m','M'),('c','C'),('v','V'),
            ('space','SPACE'),('shift','SHIFT'),('esc','ESC'),('enter','ENTER'),
            ('tab','TAB'),('up','up'),('down','down'),('left','left'),('right','right')]
GAMEPAD = [('a','A'),('b','B'),('x','X'),('y','Y'),('lb','LB'),('rb','RB'),
           ('lt','LT'),('rt','RT'),('l3','L3'),('r3','R3'),('menu','menu'),('view','view'),
           ('dpad-up','up'),('dpad-down','down'),('dpad-left','left'),('dpad-right','right'),
           ('face-cross','cross'),('face-circle','circle'),('face-square','square'),('face-triangle','triangle')]


def matte(path):
    im = Image.open(path).convert('RGBA')
    values = []
    for r,g,b,a in im.getdata():
        key = r > 180 and b > 180 and g < 100
        values.append((r,g,b,255) if a >= 225 and not key else (0,0,0,0))
    im.putdata(values)
    box = im.getbbox()
    if not box: raise ValueError('Empty source: '+str(path))
    return im.crop(box)


def normalize(path, size, fit, center=False):
    im = matte(path)
    k = min(fit[0]/im.width, fit[1]/im.height)
    im = im.resize((max(1,round(im.width*k)), max(1,round(im.height*k))),Image.Resampling.NEAREST)
    canvas = Image.new('RGBA',size)
    at = ((size[0]-im.width)//2,(size[1]-im.height)//2 if center else size[1]-8-im.height)
    canvas.alpha_composite(im,at)
    return canvas


def paper(im):
    out = im.copy();pixels=[]
    for r,g,b,a in im.getdata():
        if not a: pixels.append((0,0,0,0));continue
        t=(1-(.25*r+.65*g+.1*b)/255)**1.6
        pixels.append((round(245-178*t),round(239-180*t),round(223-174*t),a))
    out.putdata(pixels);return out


def outline(im):
    a=im.getchannel('A');edge=a.filter(ImageFilter.MaxFilter(3))
    dark=Image.new('RGBA',im.size,(48,43,37,255));dark.putalpha(edge);dark.alpha_composite(im)
    return dark


def weapon_cutout(card):
    """Keep the weapon contour and its enclosed light faces, not the paper frame."""
    crop=card.crop((18,18,110,142));w,h=crop.size;px=crop.load()
    barriers={(x,y) for y in range(h) for x in range(w)
              if px[x,y][3] and sum(px[x,y][:3])/3 < 185}
    outside=set();queue=deque()
    for x in range(w):queue.extend([(x,0),(x,h-1)])
    for y in range(h):queue.extend([(0,y),(w-1,y)])
    while queue:
        p=queue.popleft();x,y=p
        if p in outside or p in barriers or not(0<=x<w and 0<=y<h):continue
        outside.add(p);queue.extend([(x-1,y),(x+1,y),(x,y-1),(x,y+1)])
    foreground={(x,y) for y in range(h) for x in range(w) if (x,y) not in outside}
    components=[]
    while foreground:
        seed=foreground.pop();group={seed};queue=deque([seed])
        while queue:
            x,y=queue.popleft()
            for dx in (-1,0,1):
                for dy in (-1,0,1):
                    p=(x+dx,y+dy)
                    if p in foreground:foreground.remove(p);group.add(p);queue.append(p)
        components.append(group)
    if not components:raise ValueError('No weapon illustration inside card')
    keep=max(components,key=len);out=Image.new('RGBA',crop.size);target=out.load()
    for x,y in keep:target[x,y]=px[x,y]
    return out.crop(out.getbbox())


def keycap(text, gamepad=False):
    text_glyph = text.upper() if text.lower() not in ['up','down','left','right','menu','view','cross','circle','square','triangle'] else None
    width=max(17,6*len(text_glyph)+5) if text_glyph else 17
    im=Image.new('RGBA',(width,18));px=im.load()
    ink=(46,38,34,255);face=(246,238,218,255);lip=(186,168,140,255);shine=(255,252,242,255)
    for y in range(18):
        bottom=17-y
        for x in range(width):
            corner=x in (0,width-1) and bottom in (0,17)
            edge=x in (0,width-1) or bottom in (0,17)
            px[x,y]=(0,0,0,0) if corner else ink if edge else (ink if bottom==3 else lip) if bottom<=3 else shine if bottom==16 else face
    if gamepad and text in ('A','B','X','Y','cross','circle','square','triangle'):
        im=Image.new('RGBA',(width,18));d=ImageDraw.Draw(im)
        d.ellipse((1,2,15,17),fill=lip,outline=ink)
        d.ellipse((1,0,15,15),fill=face,outline=ink)
        px=im.load()
    if text_glyph:
        left=(width-(len(text_glyph)*6-1))//2
        for i,ch in enumerate(text_glyph):
            for y,row in enumerate(FONT[ch]):
                for x,value in enumerate(row):
                    if value=='#':px[left+i*6+x,3+y]=ink
    else:
        d=ImageDraw.Draw(im);cx=width//2
        if text in ['up','down','left','right']:
            shape=[(0,-4),(-4,0),(-1,0),(-1,4),(1,4),(1,0),(4,0)]
            def turn(x,y):
                return {'up':(x,y),'down':(-x,-y),'left':(y,-x),'right':(-y,x)}[text]
            d.polygon([(cx+turn(x,y)[0],7+turn(x,y)[1]) for x,y in shape],fill=ink)
        elif text=='menu':
            for y in (4,7,10):d.line((cx-4,y,cx+4,y),fill=ink)
        elif text=='view':
            d.rectangle((cx-4,3,cx+1,8),outline=ink);d.rectangle((cx-1,6,cx+4,11),outline=ink)
        elif text=='cross':d.line((cx-3,4,cx+3,10),fill=ink,width=2);d.line((cx+3,4,cx-3,10),fill=ink,width=2)
        elif text=='circle':d.ellipse((cx-4,3,cx+4,11),outline=ink,width=2)
        elif text=='square':d.rectangle((cx-4,3,cx+4,11),outline=ink,width=2)
        elif text=='triangle':d.line([(cx,3),(cx-4,11),(cx+4,11),(cx,3)],fill=ink,width=2)
    return im


def effect_frames(raw, kind):
    source=normalize(raw,(64,64),(48,48),True)
    sizes=[18,28,40,50,44,36,26]
    frames=[]
    for i in range(8):
        im=Image.new('RGBA',(64,64))
        if i<7:
            small=source.resize((sizes[i],sizes[i]),Image.Resampling.NEAREST)
            if i>=4:
                pixels=small.load()
                for y in range(small.height):
                    for x in range(small.width):
                        if ((x//2)*17+(y//2)*11)%7 < i-3:pixels[x,y]=(0,0,0,0)
            shift=(8-i*3) if kind in ('acquire','pigment') else (-7+i*3) if kind=='discard' else 0
            im.alpha_composite(small,((64-small.width)//2,(64-small.height)//2+shift))
            im=outline(im)
        frames.append(im)
    return frames


def export(raw_root, destination):
    records=[]
    def emit(im,path,kind,ppu=32,pivot=None,**extra):
        im=im.convert('RGBA')
        im.putdata([(r,g,b,a) if a else (0,0,0,0) for r,g,b,a in im.getdata()])
        target=destination/path;target.parent.mkdir(parents=True,exist_ok=True);im.save(target)
        record={'id':str(Path(path).with_suffix('')).replace('\\','/').replace('/','.'),
                'name':Path(path).stem,'path':path,'kind':kind,'size':list(im.size),
                'alpha':'binary','approval':'pending-user-art-review','gameIntegration':'not-integrated'}
        if ppu:record['ppu']=ppu
        record['pivot']=pivot or [.5,.5]
        record.update(extra);records.append(record)
    def raw(name):return raw_root/(name+'.png')
    clock='environments/clock-tower/runtime/needed-art'
    # The primary altar has twice the source pixel dimensions at twice the PPU:
    # 48/32 x 40/32 is the same world size as the temporary 24/16 x 20/16.
    before=normalize(raw('boss-ink-altar'),(48,40),(48,40),True)
    # Contact the floor at pivot y=0 instead of leaving centering padding below.
    opaque_bottom=before.getbbox()[3]
    shifted=Image.new('RGBA',(48,40));shifted.alpha_composite(before,(0,40-opaque_bottom));before=shifted
    after=before.copy();pa=after.load();gem_mask=Image.new('L',before.size);gm=gem_mask.load()
    for y in range(after.height):
        for x in range(after.width):
            r,g,b,a=pa[x,y]
            if a and max(r,g,b)-min(r,g,b)>50:
                gm[x,y]=255
    gem_mask=gem_mask.filter(ImageFilter.MaxFilter(3));gm=gem_mask.load()
    for y in range(after.height):
        for x in range(after.width):
            if pa[x,y][3] and gm[x,y]:pa[x,y]=(111,103,91,255)
    for state,im in [('before',before),('after',after)]:
        emit(im,f'{clock}/ink-altar/altar-{state}.png','altar-state',pivot=[.5,0],worldSize=[1.5,1.25])
        emit(im.resize((24,20),Image.Resampling.NEAREST),f'{clock}/ink-altar/compat24/altar-{state}.png','altar-state-compat',ppu=16,pivot=[.5,0],alternativeTo='48x40 primary, do not apply both')
    for i in range(8):
        appear=before.copy();mask=appear.getchannel('A');draw=ImageDraw.Draw(mask)
        reveal=round(40*i/7)
        if reveal<40:draw.rectangle((0,0,47,39-reveal),fill=0)
        appear.putalpha(mask)
        emit(appear,f'{clock}/ink-altar/appear/altar-appear-{i+1:02}.png','altar-appear',pivot=[.5,0],frameDurationMs=80,timingStatus='visual-proposal')
        use=Image.blend(before,after,i/7)
        use.putalpha(before.getchannel('A'))
        emit(use,f'{clock}/ink-altar/use/altar-use-{i+1:02}.png','altar-use',pivot=[.5,0],frameDurationMs=80,timingStatus='visual-proposal')
    opened=normalize(raw('chapter1-exit'),(96,128),(80,112))
    seal=Image.new('RGBA',opened.size);sp=seal.load();op=opened.load()
    for y in range(23,120):
        opaque=[x for x in range(96) if op[x,y][3]]
        if len(opaque)<2:continue
        for x in range(max(min(opaque)+1,23),min(max(opaque),73)):
            if not op[x,y][3]:sp[x,y]=(65,70,63,255)
    if seal.getchannel('A').getbbox() is None:raise ValueError('Exit passage is not empty')
    sealed=seal.copy();sealed.alpha_composite(opened)
    emit(opened,f'{clock}/chapter1-exit/exit-open.png','chapter-exit-state',pivot=[.5,8/128],designStatus='exit-visual-candidate')
    emit(sealed,f'{clock}/chapter1-exit/exit-sealed.png','chapter-exit-state',pivot=[.5,8/128],designStatus='optional-closed-state-candidate')
    for i in range(8):
        frame=seal.copy();px=frame.load()
        for y in range(128):
            for x in range(96):
                if ((x-23)+(y//4)%3)/52 < i/7 or i==7:px[x,y]=(0,0,0,0)
        frame.alpha_composite(opened)
        emit(frame,f'{clock}/chapter1-exit/open/exit-open-{i+1:02}.png','chapter-exit-open',pivot=[.5,8/128],frameDurationMs=80,timingStatus='visual-proposal')
    for name in ('sword','blunt','ranged'):
        im=normalize(raw('blueprint-'+name),(128,160),(120,152),True)
        emit(im,f'ui/blueprints/runtime/needed-art/blueprint-{name}.png','blueprint-card',ppu=None)
        art=weapon_cutout(im);art.thumbnail((80,112),Image.Resampling.NEAREST)
        standalone=Image.new('RGBA',(96,128));standalone.alpha_composite(art,((96-art.width)//2,(128-art.height)//2))
        emit(standalone,f'ui/blueprints/runtime/needed-art/illustrations/blueprint-{name}-art.png','blueprint-illustration',ppu=None,preferredForExistingButtonFrame=True)
    for category,keys in [('keyboard',KEYBOARD),('gamepad',GAMEPAD)]:
        for name,text in keys:
            emit(keycap(text,category=='gamepad'),f'ui/keycaps/runtime/{category}/keycap-{name}.png','input-glyph',pivot=[.5,0],worldScale=2,bobSeconds=1,bobWorldPixels=4,glyph=text,platformSet=category)
    fx_sources=[('ink-destruction','destroy','effects/ink-destruction/runtime/needed-art/ink-destruction'),
                ('altar-choice-acquire','acquire',clock+'/ink-altar/choice-acquire/altar-choice-acquire'),
                ('altar-choice-discard','discard',clock+'/ink-altar/choice-discard/altar-choice-discard'),
                ('pigment-pickup-contrast','pigment','effects/hub-unlock/runtime/pigment-pickup-contrast/pigment-pickup-contrast')]
    for source,kind,folder in fx_sources:
        for i,im in enumerate(effect_frames(raw(source),kind)):
            emit(im,f'{folder}-{i+1:02}.png',source,frameDurationMs=80,timingStatus='visual-proposal')
    if raw('basic-sword-hit-candidate').exists():
        sequence=effect_frames(raw('basic-sword-hit-candidate'),'destroy')[:6]
        frame_ms=[30,30,30,40,50,60]
        for i,im in enumerate(sequence):
            emit(im,f'effects/basic-sword-contact/runtime/needed-art-candidate/{i:02}.png',
                 'basic-sword-hit-replacement-candidate',frameDurationMs=frame_ms[i],
                 timingStatus='preserves-main-frameEnds',restoresApprovedOriginalBytes=False)
    for name in ('palette-atelier','supply-office','reclaim-workshop'):
        original=Image.open(ROOT/f'environments/village/runtime/buildings/starting-hub/color/{name}.png').convert('RGBA')
        w,h=original.size
        for i in range(8):
            im=original.copy();px=im.load()
            for y in range(h):
                for x in range(w):
                    r,g,b,a=px[x,y]
                    if not a:continue
                    if x<w*.3 and y<h*.7:phase=1
                    elif y<h*.44 and r>g*1.12 and r>b*1.15:phase=2
                    elif max(r,g,b)<120:phase=3
                    elif r>g*1.1 and g>b*1.1:phase=4
                    elif g>r*.9 and g>b*1.18:phase=6
                    elif y>h*.79:phase=7
                    else:phase=5
                    if phase>i:px[x,y]=(0,0,0,0)
            emit(im,f'environments/village/runtime/effects/needed-art/color-spread/{name}/color-spread-{i+1:02}.png','building-color-spread',pivot=[.5,8/h],frameDurationMs=80,sourceBuilding=name,lastFrame='full-color-state',mask='source-material-regions; clipped to original building alpha')
    character_specs=[('prologue-human-clerk','human-clerk',(192,160),(56,64)),
                     ('prologue-benefactor','benefactor',(192,160),(56,64)),
                     ('prologue-ink-helper','ink-helper',(192,192),(128,128))]
    for source,name,size,fit in character_specs:
        im=normalize(raw(source),size,fit)
        emit(im,f'characters/story/{name}/runtime/{name}.png','story-character-still',pivot=[.5,8/size[1]],designStatus='character-appearance-candidate',locomotionProvided=False)
        if name=='benefactor':
            emit(paper(im),f'characters/story/{name}/runtime/benefactor-erased.png','story-character-erased-state',pivot=[.5,8/size[1]],designStatus='same-pose-color-loss-only')
        if name=='ink-helper':
            for i in range(8):
                frame=im.copy();px=frame.load()
                for y in range(size[1]):
                    for x in range(size[0]):
                        if ((x//3)*13+(y//3)*7)%29 < round(i/7*29):px[x,y]=(0,0,0,0)
                emit(frame,f'characters/story/{name}/runtime/sacrifice/ink-helper-sacrifice-{i+1:02}.png','story-helper-dissolve-mask',pivot=[.5,8/size[1]],frameDurationMs=80,timingStatus='visual-proposal-not-story-sequence-timing')
    path=destination/'authoring/needed-assets/source/output-layout.json';path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes((json.dumps({'format':'deviant-additional-needed-art-v1','scope':'art-and-application-guide-only','selectionStatus':'unselected-candidates','runtimeApplied':False,'sprites':records},ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    print('Exported',len(records),'named PNG files to fresh review directory:',destination)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw-directory',type=Path,required=True)
    parser.add_argument('--destination',type=Path,required=True)
    args=parser.parse_args()
    export(args.raw_directory.resolve(),prepare_output(args.destination))
