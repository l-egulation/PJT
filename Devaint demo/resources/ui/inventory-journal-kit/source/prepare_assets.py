"""Build an empty inventory skin from generated book art and existing journal parts."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT.parents[1]
REF = RES / 'ui/journal-preview/preview/assets/refined'
OUT = ROOT / 'runtime'
OUT.mkdir(exist_ok=True)
used = {}

def source(name):
    p = REF / (name + '.png')
    used[str(p.relative_to(RES)).replace('\\', '/')] = hashlib.sha256(p.read_bytes()).hexdigest()
    return Image.open(p).convert('RGBA')

def sliced(im, size, src_border, dst_border, horizontal=False):
    w,h = size; sw,sh = im.size
    a,b = src_border,dst_border
    sx = [0,a,sw-a,sw]; dx = [0,b,w-b,w]
    sy = [0,sh] if horizontal else [0,a,sh-a,sh]
    dy = [0,h] if horizontal else [0,b,h-b,h]
    out = Image.new('RGBA',size)
    for iy in range(len(sy)-1):
        for ix in range(3):
            crop = im.crop((sx[ix],sy[iy],sx[ix+1],sy[iy+1]))
            crop = crop.resize((dx[ix+1]-dx[ix],dy[iy+1]-dy[iy]),Image.Resampling.LANCZOS)
            out.alpha_composite(crop,(dx[ix],dy[iy]))
    return out

def save(name, im):
    im.save(OUT/(name+'.png'))
    return im

def clean_book():
    raw = Image.open(ROOT/'source/empty-book-generated.png').convert('RGBA')
    a = np.array(raw)
    # Preserve actual alpha when returned. Otherwise remove the specified solid key.
    keyed = (a[:,:,0]>180)&(a[:,:,1]<110)&(a[:,:,2]>150)
    if keyed.sum()>a.shape[0]*a.shape[1]*.01:
        a[keyed,3] = 0
    a[:,:,3] = np.where(a[:,:,3]>=128,255,0)
    a[a[:,:,3]==0,:3] = 0
    im = Image.fromarray(a)
    assert im.getextrema()[3][0]==0, 'Book needs a real outside alpha cutout.'
    im = im.crop(im.getbbox())
    canvas = Image.new('RGBA',(800,464))
    im = im.resize((796,460),Image.Resampling.LANCZOS)
    canvas.alpha_composite(im,(2,2))
    return save('inventory-book',canvas)

book = clean_book()
parts = {
 'inventory-title': sliced(source('title'),(210,48),14,10,True),
 'weapon-tab': sliced(source('tab'),(172,36),14,9,True),
 'weapon-tab-active': sliced(source('tab-active'),(172,36),14,9,True),
 'filter-tab': sliced(source('tab'),(84,36),14,9,True),
 'filter-tab-active': sliced(source('tab-active'),(84,36),14,9,True),
 'equipment-slot': sliced(source('slot'),(170,64),4,3),
 'stat-slot': sliced(source('slot'),(60,64),4,3),
 'bag-slot': sliced(source('slot'),(66,60),4,3),
 'bag-slot-selected': sliced(source('slot-active'),(66,60),10,6),
 'close-button': sliced(source('slot'),(36,36),4,3),
 'section-rule': Image.new('RGBA',(348,1),(184,170,140,255))
}
cross = Image.new('RGBA',(64,64))
d = ImageDraw.Draw(cross)
for ends in [(10,10,54,54),(54,10,10,54)]:
    d.line(ends,fill=(36,35,29,255),width=6)
parts['close-icon'] = cross.resize((16,16),Image.Resampling.LANCZOS)
for name,im in parts.items(): save(name,im)

layout = {'format':'deviant-inventory-ui-skin-v1','canvas':[800,464],
 'bookBody':[0,24,800,440], 'origin':'top-left', 'pixelsPerUnit':100,
 'textBaked':False,'inkArtBaked':False,'removedActions':['discard','destroy','equip'],
 'titleText':{'rect':[48,12,180,28],'text':'가방 · 장비','fontSize':24,'bold':True},
 'weaponTabs':[[32,58,172,36],[208,58,172,36]],
 'equipmentSlots':{'form':[32,138,170,64],'special':[210,138,170,64],
 'stats':[[32+i*72,252,60,64] for i in range(5)]},
 'filters':[[420+i*88,58,84,36] for i in range(4)],
 'bagSlots':[[round(420+i%5*70.5),108+i//5*68,66,60] for i in range(15)],
 'rules':[[32,404,348,1],[420,320,348,1]],
 'close':[756,28,36,36], 'closeIcon':[766,38,16,16],
 'detail':{'icon':[420,332,40,56],'name':[472,332,296,24],'description':[472,365,296,54]},
 'labels':[{'text':'형태','rect':[32,108,170,24]}, {'text':'특수','rect':[210,108,170,24]},
 {'text':'능력치','rect':[32,222,348,24]}],
 'font':'existing JournalFonts regular and bold; text rendered separately',
 'sourcePartHashes':used}
base = book.copy()
def paste(name,rect): base.alpha_composite(parts[name],tuple(rect[:2]))
for i,r in enumerate(layout['weaponTabs']):paste('weapon-tab-active' if i==0 else 'weapon-tab',r)
for i,r in enumerate(layout['filters']):paste('filter-tab-active' if i==0 else 'filter-tab',r)
for key in ['form','special']:paste('equipment-slot',layout['equipmentSlots'][key])
for r in layout['equipmentSlots']['stats']:paste('stat-slot',r)
for r in layout['bagSlots']:paste('bag-slot',r)
for r in layout['rules']:paste('section-rule',r)
paste('close-button',layout['close'])
# Cross is separate from the empty base, like labels; no ink icons or symbols baked in.
save('inventory-base',base)
(ROOT/'runtime/layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

preview = Image.new('RGB',(1600,928))
p = ImageDraw.Draw(preview)
for y in range(0,928,24):
    for x in range(0,1600,24):
        p.rectangle((x,y,x+23,y+23),fill=(225,222,214) if (x//24+y//24)%2 else (244,241,233))
large = base.resize((1600,928),Image.Resampling.LANCZOS)
preview.paste(large,(0,0),large)
preview.save(ROOT/'preview/inventory-empty.png')

sheet = Image.new('RGB',(1100,760),(238,232,216)); d=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',16)
d.text((20,16),'Inventory UI / transparent parts / no text or ink art',font=font,fill=(35,31,25))
for i,(name,im) in enumerate(parts.items()):
    x,y=20+(i%3)*360,65+(i//3)*160
    scale=min(1.5,310/im.width,100/im.height)
    thumb=im.resize((round(im.width*scale),max(1,round(im.height*scale))),Image.Resampling.LANCZOS)
    sheet.paste(thumb,(x,y),thumb)
    d.text((x,y+108),name+'.png',font=font,fill=(35,31,25))
    d.text((x,y+130),str(im.width)+' x '+str(im.height),font=font,fill=(80,70,55))
sheet.save(ROOT/'preview/parts.png')
for f in OUT.glob('*.png'):
    with Image.open(f) as im:
        assert im.mode=='RGBA'
        assert im.getextrema()[3][1]>0
for r in layout['bagSlots']+layout['equipmentSlots']['stats']:
    assert r[0]>=0 and r[1]>=0 and r[0]+r[2]<=800 and r[1]+r[3]<=464
assert len(layout['bagSlots'])==15
print('Prepared',len(list(OUT.glob('*.png'))),'RGBA PNG assets; 15 bag slots and 7 equipment slots.')
