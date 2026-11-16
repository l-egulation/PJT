"""Package freshly generated, complete UI sprites; never extract mockup pixels.

Original generations are copied unchanged. Runtime exports only trim transparent
padding. Utility masks are authored primitives; text remains live in the preview.
"""
from pathlib import Path
import hashlib
import io
import json
import math
import shutil
import zipfile
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT/'ui/combat-hud-rebuilt-20261106'
W, H = 1670, 942
SPRITES = {}
RECORDS = []
LAYOUT = []
TEXTS = []
DRAW_LAYERS = []
LABELS = {
    'health-plaque':'체력 프레임', 'hero-portrait':'캐릭터 초상', 'weapon-disc':'무기·스킬 공용 원형 슬롯',
    'run-card':'출발 팔레트 카드', 'minimap-frame':'미니맵 프레임', 'palette-tray':'팔레트 받침',
    'heading-plaque':'출발 팔레트 제목판', 'button-frame':'버튼·키캡 바탕',
    'weapon-sword':'검 아이콘', 'weapon-hammer':'망치 아이콘', 'skill-slash':'검 기본 공격',
    'skill-swirl':'회전 스킬', 'skill-impact':'망치 기본 공격', 'gold-coin':'골드', 'bag-glyph':'인벤토리',
    'drop-blue':'파랑 물방울', 'drop-yellow':'노랑 물방울', 'drop-red':'빨강 물방울', 'drop-green':'초록 물방울',
    'radial-ring-clean':'얇은 원형 링', 'pigment-existing-game':'기존 게임 안료',
    'progress-ring-mask':'진행률 원형 마스크', 'health-fill-mask':'체력 100% 채움 마스크',
    'pause-glyph':'일시정지', 'corner-accent':'카드 모서리 강조', 'chevron':'제목 화살표', 'divider':'카드 구분선'
}


def dump(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_sprite(name, im, origin, source, trim=None):
    dest = GROUP/f'runtime/sprites/{name}.png'
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest)
    SPRITES[name] = im
    RECORDS.append(dict(id=name, label=LABELS.get(name,name), file=dest.relative_to(GROUP).as_posix(),
                        size=list(im.size), origin=origin, source=source, transparentTrim=trim,
                        completeStandalone=True, sha256=sha(dest)))


def load_generations():
    entries=json.loads((GROUP/'source/generation-files.json').read_text(encoding='utf-8'))
    provenance=[]
    for entry in entries:
        name=entry['id']
        # The first ring is not selected; the later plain ring has no halo.
        target=GROUP/f'source/generated/{name}.png'
        target.parent.mkdir(parents=True,exist_ok=True)
        src=target if target.exists() else Path(entry['path'])
        if src.resolve()!=target.resolve():shutil.copy2(src,target)
        assert sha(src)==sha(target)
        if name=='radial-ring':
            provenance.append(dict(id=name, file=target.relative_to(GROUP).as_posix(), sha256=sha(target), selected=False))
            continue
        im=Image.open(target).convert('RGBA')
        alpha=np.asarray(im)[:,:,3]
        yy,xx=np.where(alpha>16)
        assert len(xx)>0, name
        # Trim only alpha padding, with a guard outside the visible silhouette.
        box=(max(0,int(xx.min())-8),max(0,int(yy.min())-8),min(im.width,int(xx.max())+9),min(im.height,int(yy.max())+9))
        crop=im.crop(box)
        save_sprite(name,crop,'new-image-generation',target.relative_to(GROUP).as_posix(),list(box))
        provenance.append(dict(id=name, file=target.relative_to(GROUP).as_posix(), sha256=sha(target),
                               originalSize=list(im.size), selected=True))
    pigment=GROUP/'source/pigment-existing-game.png'
    save_sprite('pigment-existing-game',Image.open(pigment).convert('RGBA'),'existing-game-asset',
                'source/pigment-existing-game.png')
    dump(GROUP/'source/provenance.json',dict(tool='built-in image_gen', mockupPixelExtraction=False,
         referenceOnly=['approved-battle.png','approved-palette.png'], generated=provenance,
         existingPigmentSha256=sha(pigment)))


def primitives():
    # These elementary masks belong in the renderer rather than painted artwork.
    # Retain editable SVG originals and antialiased PNGs for Unity's Image fill.
    def primitive(name,size,svg,draw):
        p=GROUP/f'source/primitives/{name}.svg';p.parent.mkdir(parents=True,exist_ok=True)
        p.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size[0]} {size[1]}">{svg}</svg>',encoding='utf-8')
        im=Image.new('RGBA',(size[0]*4,size[1]*4))
        draw(ImageDraw.Draw(im),4)
        im=im.resize(size,Image.Resampling.LANCZOS)
        save_sprite(name,im,'authored-ui-primitive',p.relative_to(GROUP).as_posix())
    def poly(d,s,pts,col):d.polygon([(x*s,y*s) for x,y in pts],fill=col)
    primitive('progress-ring-mask',(256,256),'<circle cx="128" cy="128" r="115" fill="none" stroke="white" stroke-width="14"/>',
              lambda d,s:d.ellipse((13*s,13*s,243*s,243*s),outline='white',width=14*s))
    primitive('health-fill-mask',(680,48),'<path d="M26 0H680L654 48H0Z" fill="white"/>',
              lambda d,s:poly(d,s,[(26,0),(680,0),(654,48),(0,48)],'white'))
    primitive('pause-glyph',(64,80),'<path d="M5 5H25V75H5ZM39 5H59V75H39Z" fill="#191b18"/>',
              lambda d,s:(d.rectangle((5*s,5*s,25*s,75*s),fill='#191b18'),d.rectangle((39*s,5*s,59*s,75*s),fill='#191b18')))
    primitive('corner-accent',(64,64),'<path d="M3 3L61 61H39L3 25Z" fill="#00d3d2"/>',
              lambda d,s:poly(d,s,[(3,3),(61,61),(39,61),(3,25)],'#00d3d2'))
    primitive('chevron',(24,40),'<path d="M19 4L6 20L19 36" fill="none" stroke="#00d3d2" stroke-width="5"/>',
              lambda d,s:d.line([(19*s,4*s),(6*s,20*s),(19*s,36*s)],fill='#00d3d2',width=5*s,joint='curve'))
    primitive('divider',(480,8),'<rect x="1" y="2" width="478" height="4" rx="2" fill="#191b18"/>',
              lambda d,s:d.rounded_rectangle((s,2*s,479*s,6*s),radius=2*s,fill='#191b18'))


def sprite(name,x,y,w,h,scene='battle',tint=None,flip=False,role=None):
    LAYOUT.append(dict(id=f'{name}-{len(LAYOUT)}',sprite=name,rect=[x,y,w,h],scene=scene,tint=tint,flipX=flip,role=role))


def text(value,x,y,size,color='#fff3de',scene='battle',center=False,role=None):
    TEXTS.append(dict(text=value,x=x,y=y,size=size,color=color,scene=scene,center=center,role=role,font='Malgun Gothic Bold'))


def define_layout():
    sprite('health-plaque',96,61,428,82)
    sprite('health-fill-mask',165,112,330,18,tint='#ff7062',role='health-fill')
    sprite('hero-portrait',29,30,147,118)
    text('82',192,61,43,role='health-value');text('/ 100',249,74,29)
    sprite('gold-coin',1255,28,47,47);text('240',1306,32,34,'#191b18')
    sprite('pigment-existing-game',1390,29,46,46);text('18',1442,32,34,'#191b18')
    sprite('button-frame',1510,24,61,61);sprite('bag-glyph',1527,37,28,32)
    sprite('button-frame',1583,24,61,61);sprite('pause-glyph',1600,40,27,30)
    sprite('minimap-frame',1251,89,394,144)
    for name,x,basic,special,key1,key2,colors in [
        ('sword',52,'skill-slash','skill-swirl','Q','E',['blue','yellow','blue']),
        ('hammer',407,'skill-impact',None,'W','R',['red','green','yellow'])]:
        sprite('weapon-disc',x,781,141,141)
        sprite('weapon-disc',x+132,814,84,84)
        sprite('weapon-disc',x+214,812,87,87)
        if name=='sword':sprite('progress-ring-mask',x+5,787,130,130,tint='#00dcdf')
        sprite('weapon-'+name,x+31,808,74,78,role='replaceable-weapon')
        sprite(basic,x+152,835,43,43,role='replaceable-skill')
        if special:sprite(special,x+230,830,56,56,role='replaceable-skill')
        else:
            sprite('progress-ring-mask',x+214,812,87,87,tint='#ece8df',role='cooldown-ring')
            text('3.2',x+257,836,29,center=True,role='cooldown-value')
        for key,kx in [(key1,x+153),(key2,x+234)]:
            sprite('button-frame',kx,792,42,33)
            text(key,kx+21,793,24,'#161915',center=True)
        sprite('palette-tray',x+74,879,171,49)
        for i,c in enumerate(colors):sprite('drop-'+c,x+102+i*42,886,26,33,role='palette-color')
    sprite('heading-plaque',691,248,289,67,scene='palette')
    text('출발 팔레트',835,254,35,scene='palette',center=True)
    sprite('chevron',720,271,9,15,scene='palette');sprite('chevron',940,271,9,15,scene='palette',flip=True)
    for name,x,cx,title,colors,flip in [('sword',516,653,'기본검',['blue','yellow','blue'],False),
                                      ('hammer',841,1014,'기본망치',['red','green','yellow'],True)]:
        sprite('run-card',x,332,313,201,scene='palette')
        sprite('weapon-disc',cx-45,290,90,90,scene='palette')
        sprite('weapon-'+name,cx-27,307,54,56,scene='palette')
        text(title,x+157,376,35,'#151914',scene='palette',center=True)
        sprite('divider',x+42,423,232,4,scene='palette')
        for i,c in enumerate(colors):sprite('drop-'+c,x+65+i*70,441,49,64,scene='palette',role='palette-color')
        sprite('corner-accent',x+(271 if flip else 10),491,31,31,scene='palette',flip=flip)


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',size)


def tint_image(im,hexcolor):
    # Renderer tint: source sprite is not modified on disk.
    color=tuple(int(hexcolor[i:i+2],16) for i in (1,3,5))
    layer=Image.new('RGBA',im.size,color+(255,));layer.putalpha(im.getchannel('A'));return layer


def render(hp=.82,cooldown=.65,with_palette=True,background=None):
    out=Image.new('RGBA',(W,H),background or (0,0,0,0))
    for item in LAYOUT:
        if item['scene']=='palette' and not with_palette:continue
        x,y,w,h=item['rect']; im=SPRITES[item['sprite']].resize((w,h),Image.Resampling.LANCZOS)
        if item['flipX']:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if item['tint']:im=tint_image(im,item['tint'])
        if item['role']=='health-fill':
            im=np.array(im);im[:,int(w*hp):,3]=0;im=Image.fromarray(im)
        if item['role']=='cooldown-ring':
            arr=np.array(im);yy,xx=np.indices((h,w));ang=(np.arctan2(xx-w/2,-(yy-h/2))+2*np.pi)%(2*np.pi)
            arr[ang>cooldown*2*np.pi,3]=0;im=Image.fromarray(arr)
        out.alpha_composite(im,(x,y))
        if background is None and hp==.82 and cooldown==.65:
            DRAW_LAYERS.append((item['id'],im.copy(),x,y))
    # Runtime map geometry is deliberately live, not a baked image.
    d=ImageDraw.Draw(out)
    d.line([(1270,183),(1295,183),(1295,125),(1400,125),(1400,153),(1460,153),(1460,183),(1270,183)],fill='#fff2dc',width=3)
    d.rectangle((1354,163,1372,181),fill='#00dcdb')
    for t in TEXTS:
        if t['scene']=='palette' and not with_palette:continue
        val=t['text']
        if t['role']=='health-value':val=str(round(hp*100))
        if t['role']=='cooldown-value':val=f'{cooldown*5:.1f}'
        f=font(t['size']); x=t['x']
        if t['center']:x-=d.textlength(val,font=f)/2
        d.text((x,t['y']),val,font=f,fill=t['color'],stroke_width=0)
    return out


def checker(size):
    yy,xx=np.indices((size[1],size[0]));v=np.where((xx//16+yy//16)%2,90,102).astype(np.uint8)
    return Image.fromarray(np.repeat(v[:,:,None],3,axis=2)).convert('RGBA')


def previews():
    p=GROUP/'preview';p.mkdir(exist_ok=True)
    ui=render();ui.save(p/'assembled-transparent.png')
    check=checker((W,H));check.alpha_composite(ui);check.save(p/'assembled-checker.png')
    render(background='#f8eddc').save(p/'assembled-paper.png')
    render(hp=.25,cooldown=.2,with_palette=False,background='#f8eddc').save(p/'dynamic-low-health.png')
    render(hp=1,cooldown=1,with_palette=False,background='#f8eddc').save(p/'dynamic-full-health.png')
    cols,cw,ch=4,410,252;rows=math.ceil(len(RECORDS)/cols)
    sheet=Image.new('RGB',(cols*cw,105+rows*ch),'#eee9df');d=ImageDraw.Draw(sheet)
    d.text((25,15),'DEVIANT / 새로 제작한 독립 UI 에셋',font=font(32),fill='#202620')
    d.text((27,61),'완전한 프레임 · 독립 아이콘 · 비어 있는 카드 · 별도 진행률 마스크',font=font(19),fill='#5e665c')
    for i,r in enumerate(RECORDS):
        x=i%cols*cw;y=105+i//cols*ch
        panel=checker((cw-16,ch-64));im=SPRITES[r['id']].copy();im.thumbnail((cw-42,ch-84),Image.Resampling.LANCZOS)
        panel.alpha_composite(im,((panel.width-im.width)//2,(panel.height-im.height)//2));sheet.paste(panel.convert('RGB'),(x+8,y))
        d.text((x+13,y+ch-60),r['label'],font=font(19),fill='#202620')
        d.text((x+13,y+ch-32),r['id'],font=font(13),fill='#596359')
    sheet.save(p/'new-assets-contact-sheet.png')


def html_preview():
    data=json.dumps(dict(sprites=RECORDS,layout=LAYOUT,texts=TEXTS),ensure_ascii=False).replace('</','<\\/')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>새로 만든 DEVIANT UI 에셋</title><style>
*{box-sizing:border-box}body{margin:0;background:#1b2220;color:#f6efdf;font:15px "Malgun Gothic",sans-serif}main{max-width:1740px;margin:auto;padding:26px}h1{margin:0;font-size:27px}p{line-height:1.7;color:#c0cbbf}nav{display:flex;gap:17px;flex-wrap:wrap;align-items:center;margin:18px 0}button{background:#2a4036;color:#d5ffef;border:1px solid #6b8f7d;border-radius:5px;padding:9px 14px;font:inherit;cursor:pointer}input{vertical-align:middle;accent-color:#10cab9}#stage{position:relative;width:100%;aspect-ratio:1670/942;background:#f8eddc;border:1px solid #697267;overflow:hidden}#stage.checker{background:repeating-conic-gradient(#5a5a5a 0% 25%,#666 0% 50%) 0/32px 32px}#stage.dark{background:#161a19}#stage .item{position:absolute;background-size:100% 100%;background-repeat:no-repeat}#stage .text{position:absolute;white-space:pre;font-family:"Malgun Gothic",sans-serif;font-weight:700;line-height:1.35}#stage .center{transform:translateX(-50%)}#stage.hide-palette .palette{display:none}#stage.empty .foreground{display:none}#map{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:14px;margin-top:24px}.card{border:1px solid #506356;border-radius:7px;overflow:hidden}.thumb{height:165px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#5a5a5a 0% 25%,#666 0% 50%) 0/24px 24px}.thumb img{max-height:145px;max-width:94%}.caption{padding:12px}small{display:block;color:#b6c5b7;margin-top:7px}a{color:#96e8d3}label{white-space:nowrap}summary{cursor:pointer;padding:10px 0}</style>
<main><h1>채택 디자인 → 새로 제작한 독립 에셋</h1><p>각 부품을 완전한 형태로 새로 만들었습니다. 아래 조립 화면에서 체력·쿨타임을 바꾸거나 내용물을 숨겨 바탕을 확인할 수 있습니다.</p>
<nav><label>체력 <input id="hp" type="range" min="0" max="100" value="82"><output id="hpValue">82</output></label><label>쿨타임 <input id="cd" type="range" min="0" max="100" value="65"></label><button id="palette">출발 팔레트 켜기/끄기</button><button id="empty">프레임만 보기</button><button id="bg">배경 바꾸기</button></nav>
<div id="stage"></div><p>숫자·단축키·이름은 실시간 텍스트이며 체력·쿨타임도 마스크로 움직입니다. 안료는 기존 게임 아이콘을 그대로 사용했습니다.<br>이 화면은 에셋 조립 검토용이며 Unity 게임에 적용한 실행 화면은 아닙니다.</p><p><a href="../runtime/layout.json">배치·Unity 설정</a> · <a href="new-assets-contact-sheet.png">전체 에셋 시트</a> · <a href="../source/ui-assembly.ora">편집용 레이어 문서</a> · <a href="../README.md">사용 안내</a></p>
<details><summary>채택 기준 원본 보기</summary><img src="../source/approved-battle.png" style="width:49%"><img src="../source/approved-palette.png" style="width:49%"></details><div class="grid" id="grid"></div></main>
<script>const data=__DATA__;const stage=document.querySelector('#stage'),grid=document.querySelector('#grid');const byId=Object.fromEntries(data.sprites.map(a=>[a.id,a]));
for(const a of data.layout){const el=document.createElement('div');el.className='item '+a.scene;el.dataset.role=a.role||'';let[x,y,w,h]=a.rect;Object.assign(el.style,{left:x/1670*100+'%',top:y/942*100+'%',width:w/1670*100+'%',height:h/942*100+'%'});const url='../'+byId[a.sprite].file;if(a.tint){el.style.backgroundColor=a.tint;el.style.mask='url("'+url+'") center/100% 100% no-repeat';el.style.webkitMask=el.style.mask}else el.style.backgroundImage='url("'+url+'")';if(a.flipX)el.style.transform='scaleX(-1)';if(a.role||['weapon-sword','weapon-hammer','hero-portrait','gold-coin','pigment-existing-game','bag-glyph','pause-glyph','chevron'].includes(a.sprite))el.classList.add('foreground');stage.append(el)}
const ns='http://www.w3.org/2000/svg';const svg=document.createElementNS(ns,'svg');svg.setAttribute('viewBox','0 0 1670 942');svg.id='map';svg.classList.add('foreground');svg.innerHTML='<path d="M1270 183H1295V125H1400V153H1460V183H1270" stroke="#fff2dc" fill="none" stroke-width="3"/><rect x="1354" y="163" width="18" height="18" fill="#00dcdb"/>';stage.append(svg);
for(const t of data.texts){const el=document.createElement('div');el.className='text foreground '+t.scene+(t.center?' center':'');el.textContent=t.text;el.dataset.role=t.role||'';el.dataset.size=t.size;Object.assign(el.style,{left:t.x/1670*100+'%',top:t.y/942*100+'%',color:t.color});stage.append(el)}
const resize=()=>{for(const el of stage.querySelectorAll('.text'))el.style.fontSize=Number(el.dataset.size)*stage.clientWidth/1670+'px'};new ResizeObserver(resize).observe(stage);resize();
const update=()=>{const hp=Number(document.querySelector('#hp').value),cd=Number(document.querySelector('#cd').value);document.querySelector('#hpValue').textContent=hp;stage.querySelector('[data-role="health-fill"]').style.clipPath='inset(0 '+(100-hp)+'% 0 0)';stage.querySelector('[data-role="health-value"]').textContent=hp;stage.querySelector('[data-role="cooldown-value"]').textContent=(cd/20).toFixed(1);const ring=stage.querySelector('[data-role="cooldown-ring"]');const url='../'+byId['progress-ring-mask'].file;ring.style.maskImage='url("'+url+'"),conic-gradient(#000 '+cd+'%,transparent 0)';ring.style.maskComposite='intersect';ring.style.webkitMaskComposite='source-in';};document.querySelector('#hp').oninput=update;document.querySelector('#cd').oninput=update;update();
document.querySelector('#palette').onclick=()=>stage.classList.toggle('hide-palette');document.querySelector('#empty').onclick=()=>stage.classList.toggle('empty');let bg=0;document.querySelector('#bg').onclick=()=>{bg=(bg+1)%3;stage.classList.toggle('checker',bg===1);stage.classList.toggle('dark',bg===2)};
for(const a of data.sprites){const card=document.createElement('div');card.className='card';const link=document.createElement('a');link.className='thumb';link.href='../'+a.file;const im=new Image();im.src=link.href;im.alt=a.label;link.append(im);const cap=document.createElement('div');cap.className='caption';cap.textContent=a.label;const small=document.createElement('small');small.textContent=a.id+' · '+a.size.join(' × ');cap.append(small);card.append(link,cap);grid.append(card)}
</script></html>'''
    (GROUP/'preview/index.html').write_text(html.replace('__DATA__',data),encoding='utf-8')


def ora():
    root=ET.Element('image',{'w':str(W),'h':str(H),'name':'Rebuilt complete standalone UI assets'})
    stack=ET.SubElement(root,'stack')
    with zipfile.ZipFile(GROUP/'source/ui-assembly.ora','w') as z:
        z.writestr('mimetype','image/openraster',compress_type=zipfile.ZIP_STORED)
        for i,(name,im,x,y) in reversed(list(enumerate(DRAW_LAYERS))):
            src=f'data/{i:03}.png';buf=io.BytesIO();im.save(buf,format='PNG');z.writestr(src,buf.getvalue())
            ET.SubElement(stack,'layer',{'name':name,'src':src,'x':str(x),'y':str(y),'opacity':'1.0','visibility':'visible','composite-op':'svg:src-over'})
        z.writestr('stack.xml',ET.tostring(root,encoding='utf-8',xml_declaration=True))
        # Editable sprite layers, no flattened text labels baked into sprites.
        merged=Image.new('RGBA',(W,H))
        for _,im,x,y in DRAW_LAYERS:merged.alpha_composite(im,(x,y))
        buf=io.BytesIO();merged.save(buf,format='PNG');z.writestr('mergedimage.png',buf.getvalue())
        merged.thumbnail((256,256));buf=io.BytesIO();merged.save(buf,format='PNG');z.writestr('Thumbnails/thumbnail.png',buf.getvalue())


def metadata():
    dump(GROUP/'runtime/sprites.json',dict(format='deviant-standalone-ui-sprites-v1',sprites=RECORDS))
    dump(GROUP/'runtime/layout.json',dict(format='deviant-complete-ui-layout-v1',referenceResolution=[W,H],
         coordinates='top-left pixels',sprites=LAYOUT,texts=TEXTS,
         runtimeDrawn=['HP fill with Image Filled Horizontal','cooldown with Image Filled Radial360','map geometry from data',
                       'numbers, weapon names and keycaps with live TMP text'],
         anchors={'health':'top-left','weapons':'bottom-left','currencies-controls-map':'top-right','run-palette':'center'},
         unity={'textureType':'Sprite (2D and UI)','spriteMode':'Single','meshType':'FullRect','pixelsPerUnit':100,
                'alphaIsTransparency':True,'mipmaps':False,'filterMode':'Bilinear','compression':'None','maxTextureSize':4096,
                'canvasScaler':'ScaleWithScreenSize','referenceResolution':[W,H],'matchWidthOrHeight':0.5},
         approval='Design references selected; new independent sprite artwork pending visual review',gameIntegration=False))
    readme='''# 새로 제작한 전투 HUD + 출발 팔레트 에셋

채택한 두 시안을 스타일·형태의 기준으로 삼아 **각 부품을 독립적인 완전체로 새로 생성**했습니다. 시안의 픽셀을 잘라낸 에셋을 사용하지 않습니다. 가려진 부분도 완성된 프레임으로 존재하며 카드·제목판·지도·원형 슬롯에는 글자나 아이콘이 구워져 있지 않습니다.

## 바로 확인하기

- `preview/index.html`: 새 에셋 조립, 체력·쿨타임 조절, 팔레트 표시, 프레임만 보기, 배경색 전환.
- `preview/new-assets-contact-sheet.png`: 단독 에셋 목록.
- `preview/assembled-paper.png`, `preview/assembled-checker.png`: 새 에셋으로 조립한 화면.
- `runtime/sprites/`: 독립 투명 PNG. 각 파일에 다른 에셋의 겹침·숫자·부분 잘림이 없습니다.
- `runtime/sprites.json`: 에셋별 출처·규격·해시.
- `runtime/layout.json`: 기준 크기, 배치, 라이브 텍스트, Unity 가져오기 설정.
- `source/ui-assembly.ora`: 위치를 보존한 편집용 레이어 문서. 글자는 포함하지 않으며 런타임 텍스트로 연결합니다.

## 제작 구분

프레임·초상·무기·스킬·골드·물방울은 기본 image_gen 도구로 새로 생성했습니다. `source/generated/`는 생성 원본 바이트를 보존합니다. `runtime/sprites/`는 알파 여백만 정리한 사본입니다. 그림 내부를 시안에서 복사하거나 잘라내지 않았습니다. `source/prompts-*.json`에 실제 프롬프트를 보관합니다. 반려된 첫 링은 source에만 보존하며 runtime에는 포함하지 않습니다.

진행률 링·HP 채움·일시정지·구분선·모서리 강조·화살표는 단순 UI 도형으로 별도 작성했습니다. `source/primitives/`에 SVG 원본을 두고 투명 PNG를 함께 제공합니다. 새로 생성한 원형 슬롯 위에 중립 링을 청록으로 tint하고 Radial360으로 표시하면 선택·쿨타임 상태를 구현할 수 있습니다. 전체 원이 있으므로 어떤 진행률에서도 잘린 부분을 메울 필요가 없습니다.

안료는 사용자가 요청한 기존 게임 원본을 사용했습니다. `source/pigment-existing-game.png`와 동일한 이미지이며, 새 아이콘으로 바꾸지 않았습니다. 이 안료는 원래 해상도가 작은 기존 에셋입니다.

## 적용

체력 프레임·캐릭터 초상·채움 마스크·체력 숫자를 별도 Image/TMP로 연결합니다. 숫자·키캡 문자·무기 이름·쿨타임은 래스터 에셋에 포함하지 않습니다. 카드와 무기 뱃지, 물방울도 분리되어 조합을 자유롭게 바꿀 수 있습니다. 지도 프레임 아래에서 실제 맵 데이터를 그립니다. 지도는 오른쪽 위 재화·버튼 줄의 아래에 놓습니다.

Unity 설정은 layout.json을 따릅니다. 참조 해상도는 1670 × 942이며 픽셀 좌표 기준입니다. 비율이 다른 화면은 코너 anchor와 중앙 anchor에 맞춰 연결합니다. HP는 Horizontal Fill, 쿨타임은 Radial360을 사용합니다. UI 폰트는 현재 프로젝트의 Malgun Gothic Bold 계열로 검토 화면을 구성했습니다. Unity에 새 파일·프리팹·코드를 적용하거나 실행 검증하지는 않았습니다.

이 패키지는 **원본과 동일한 픽셀을 복원한 결과가 아니라, 채택 디자인을 따르는 새 에셋**입니다. 새 그림의 세부 곡선·선 굵기는 조립 미리보기에서 확인할 수 있습니다. 시안 선택과 이 새 에셋의 미술 승인은 구분합니다.

## 재생성

생성 원본이 source/generated에 보존되어 있습니다. 빌드 도구는 리소스 저장소 `tools/build_rebuilt_combat_ui.py`이며 generated 파일 연결, 여백 정리, 마스크, 배치·프리뷰·색인을 생성합니다. 기존 게임 코드와 수치는 변경하지 않습니다.
'''
    (GROUP/'README.md').write_text(readme,encoding='utf-8')


def register():
    files=sorted(p.relative_to(GROUP).as_posix() for p in GROUP.rglob('*') if p.is_file() and p.name!='asset.json')
    dump(GROUP/'asset.json',dict(format='deviant-resource-asset-v1',id='ui.combat-hud-rebuilt-20261106',
        path='ui/combat-hud-rebuilt-20261106',status='new-standalone-artwork-review',
        approval='Source designs selected; newly generated complete sprites pending visual approval',
        gameIntegration='not-integrated',sources=['source/provenance.json'],files=files))
    cp=ROOT/'catalog.json';cat=json.loads(cp.read_text(encoding='utf-8-sig'))
    cat['assets']=[a for a in cat['assets'] if a['id']!='ui.combat-hud-rebuilt-20261106']
    cat['assets'].append(dict(id='ui.combat-hud-rebuilt-20261106',path='ui/combat-hud-rebuilt-20261106',fileCount=len(files)));dump(cp,cat)
    mp=ROOT/'generated-assets.json';manifest=json.loads(mp.read_text(encoding='utf-8-sig'))
    prefix=GROUP.relative_to(ROOT).as_posix()+'/'
    manifest['files']=[r for r in manifest['files'] if not r['path'].startswith(prefix)]
    for n in files:
        f=GROUP/n;manifest['files'].append(dict(path=f.relative_to(ROOT).as_posix(),bytes=f.stat().st_size,sha256=sha(f)))
    # Preserve the rejected attempt, including its updated do-not-use notice.
    for name in ['README.md','preview/index.html']:
        old=ROOT/'ui/combat-hud-approved-20261106'/name
        for row in manifest['files']:
            if row['path']==old.relative_to(ROOT).as_posix():row.update(bytes=old.stat().st_size,sha256=sha(old))
    manifest['files'].sort(key=lambda r:r['path']);dump(mp,manifest)


if __name__=='__main__':
    load_generations();primitives();define_layout();previews();html_preview();ora();metadata();register()
    print(json.dumps({'sprites':len(RECORDS),'generated':sum(r['origin']=='new-image-generation' for r in RECORDS),
                      'primitives':sum(r['origin']=='authored-ui-primitive' for r in RECORDS),'layoutInstances':len(LAYOUT)},ensure_ascii=False))
