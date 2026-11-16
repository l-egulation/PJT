"""Verify a standalone final weapon package; emit hashes and a render contact sheet."""
import base64, hashlib, io, json, re, sys
from pathlib import Path
from urllib.parse import unquote
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
if ROOT.name=='tools': ROOT=ROOT.parent/'05_공유/무기11종_확정_20261021'
else: ROOT=ROOT.parent
def read(name): return json.loads((ROOT/name).read_text(encoding='utf-8'))
def image(uri): return Image.open(io.BytesIO(base64.b64decode(uri.split(',',1)[1]))).convert('RGBA')
def dump(name,obj): (ROOT/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
D=read('viewer-data.json'); F=read('effects-data.json'); count=0; sheets=0
assert len(D['weapons'])==11 and len(F['weapons'])==4
for w in D['weapons']:
    meta=read('무기별/'+w['name']+'/animation.json')
    for k,png in w['frames'].items():
        raw=base64.b64decode(png.split(',',1)[1]); file=ROOT/'무기별'/w['name']/meta['frames'][k]
        assert file.read_bytes()==raw
        a=image(png);b=a.getbbox();p=w['pivots'].get(k,w['pivot']);o=w['offsets'].get(k,[0,0]);count+=1
        if b: assert b[0]-p[0]+o[0]+D['ground'][0]>=0 and b[2]-p[0]+o[0]+D['ground'][0]<=D['canvas'][0] and b[1]-p[1]+o[1]+D['ground'][1]>=0 and b[3]-p[1]+o[1]+D['ground'][1]<=D['canvas'][1], (w['id'],k,'clipped')
    for key,seq in w['sequences'].items():
        info=meta['sheets'][key];sheet=Image.open(ROOT/'무기별'/w['name']/info['file']).convert('RGBA');cw,ch=info['cell'];sheets+=1
        assert info['timingsMs']==[s['ms'] for s in seq]
        for i,s in enumerate(seq):
            a=image(w['frames'][s['frame']]);p=w['pivots'].get(s['frame'],w['pivot']);o=w['offsets'].get(s['frame'],[0,0]);expected=Image.new('RGBA',(cw,ch));expected.alpha_composite(a,(info['pivot'][0]-p[0]+o[0],info['pivot'][1]-p[1]+o[1]));x,y=(i%info['columns'])*cw,(i//info['columns'])*ch
            assert expected.tobytes()==sheet.crop((x,y,x+cw,y+ch)).tobytes(), (w['id'],key,i)
    if w['kind']=='melee':
        for key,entries in [('attack',w['fx']),('single',w['singleFx'])]:
            for e,me in zip(entries,meta['effects'][key]): assert (ROOT/'무기별'/w['name']/me['file']).read_bytes()==base64.b64decode(e['png'].split(',',1)[1])
for w in F['weapons']:
    v=w['variants'][0];assert w['fixedVariant']==v['id'] and w['approval']=='user_approved_2026-10-21'
    folder=ROOT/'이펙트_스프라이트'/w['name']/v['id'];frames=folder/'full_beam' if w['id']=='laser' else folder
    for i,u in enumerate(v['frames'],1): assert image(u).tobytes()==Image.open(frames/f'{i:02}.png').convert('RGBA').tobytes()
    if v.get('muzzle'):
        for i,u in enumerate(v['muzzle']['frames'],1): assert image(u).tobytes()==Image.open(folder/'muzzle'/f'{i:02}.png').convert('RGBA').tobytes()
    if v.get('projectile'): assert image(v['projectile']).tobytes()==Image.open(folder/'projectile.png').convert('RGBA').tobytes()

links=0
for html in ROOT.rglob('*.html'):
    for href in re.findall(r'(?:src|href)=[\"\']([^\"\']+)',html.read_text(encoding='utf-8')):
        if href.startswith(('data:','https:','http:','#')) or '${' in href: continue
        file=(html.parent/unquote(href.split('?')[0].split('#')[0])).resolve()
        assert file.is_relative_to(ROOT.resolve()) and file.exists(), (html.name,href)
        links+=1
assert read('gallery-validation.json')['status']=='PASS' and read('ranged-validation.json')['status']=='PASS'
report=read('검증결과.json');report.update(status='PASS',frameFiles=count,sheets=sheets,exportedPixelsExact=True,allBodyFramesFitCanvas=True,selfContainedLinks=links,fullGalleryRuntime='PASS',rangedRuntime='PASS')
dump('검증결과.json',report)

# Render the approved runtime's sampled effect states, keeping native pixels intact.
sample=read('_source/approved-effect-samples.json');font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
contact=Image.new('RGB',(1000,530),'#eef2f5');dr=ImageDraw.Draw(contact)
def composite(canvas,a,x,y): canvas.alpha_composite(a,(round(x),round(y)))
for i,entry in enumerate(sample['weapons']):
    w=next(w for w in F['weapons'] if w['id']==entry['id']);v=w['variants'][0];s=entry['sample'];bg='#111b29' if w['id']=='laser' else '#ffffff';c=Image.new('RGBA',(480,220),bg)
    ImageDraw.Draw(c).line((8,172,472,172),fill='#8b98a2');composite(c,image(w['frames'][s['body']]),12,50)
    for b in s['beams']:
        a=image(v['frames'][b['f']]).resize((round(b['end']-b['x']),96),Image.Resampling.BILINEAR);a.putalpha(a.getchannel('A').point(lambda x:int(x*(1-b['mix']))));composite(c,a,b['x'],b['y']-48)
        a=image(v['frames'][b['next']]).resize((round(b['end']-b['x']),96),Image.Resampling.BILINEAR);a.putalpha(a.getchannel('A').point(lambda x:int(x*b['mix'])));composite(c,a,b['x'],b['y']-48);ImageDraw.Draw(c).rectangle((round(b['x']),round(b['y'])-1,479,round(b['y'])),fill='white')
    for e in s['effects']:
        part=v['muzzle'] if e['part']=='muzzle' else v;a=image(part['frames'][e['f']]);p=part['pivot'];scale=e['scale'];a=a.resize((round(a.width*scale),round(a.height*scale)),Image.Resampling.NEAREST)
        if e['angle']:
            # Rotate on a large transparent tile about the effect pivot.
            tile=Image.new('RGBA',(800,800));composite(tile,a,400-p[0]*scale,400-p[1]*scale);tile=tile.rotate(-e['angle'],Image.Resampling.NEAREST);composite(c,tile,e['x']-400,e['y']-400)
        else: composite(c,a,e['x']-p[0]*scale,e['y']-p[1]*scale)
    x,y=(i%2)*500,(i//2)*265;dr.text((x+10,y+6),w['name']+' · '+v['id']+' 확정',font=font,fill='#1b1616');contact.paste(c.convert('RGB'),(x+10,y+37))
contact.save(ROOT/'확정_공격이펙트.png')
files={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(ROOT.rglob('*')) if p.is_file() and p.name not in ['파일무결성.json','정리기록.json','정리대상.json']}
dump('파일무결성.json',files)
print(json.dumps(report,ensure_ascii=False))
