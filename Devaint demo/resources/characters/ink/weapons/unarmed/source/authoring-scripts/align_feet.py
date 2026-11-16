from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, hashlib, shutil
import numpy as np

P=Path(__file__).resolve().parent
OUT=P/'foot-aligned'
OUT.mkdir(exist_ok=True)
# Manually inspected front support-foot bounding-box centre and sole baseline.
# The raised/recovering leg in #6-12 is not a support point: use #5's
# translation throughout that phase, preserving its original vertical arc.
support={1:(354,340),2:(340,327),3:(354.5,328),4:(354.5,328),5:(354.5,328),
         13:(355.5,328),14:(353,328),15:(355.5,328),16:(355.5,328),
         17:(355.5,328),18:(355.5,328),19:(356.5,328),20:(354,333),
         21:(357.5,328),22:(357,328),23:(357,328),24:(357,328),25:(357,328),
         26:(357,328),27:(356.5,328),28:(355.5,328),29:(354,328),30:(349,328),
         31:(348,328),32:(348,328),33:(348,328),34:(348,328),35:(348,328),36:(348,328)}
records=[]
for i in range(1,37):
    airborne=6<=i<=12
    anchor=support[5] if airborne else support[i]
    dx=int(round(354-anchor[0]));dy=int(340-anchor[1])
    im=Image.open(P/f'source-{i:02d}.png').convert('RGB')
    a=np.asarray(im);paint=np.min(a,axis=2)<249
    yy,xx=np.where(paint)
    assert min(xx+dx)>=0 and max(xx+dx)<640 and min(yy+dy)>=0 and max(yy+dy)<360, i
    aligned=Image.new('RGB',im.size,'white');aligned.paste(im,(dx,dy))
    aligned.save(OUT/f'frame-{i:02d}.png')
    # Verify that the translated source region is byte-for-byte unchanged.
    x0=max(0,-dx);x1=min(640,640-dx);y0=max(0,-dy);y1=min(360,360-dy)
    assert np.array_equal(a[y0:y1,x0:x1],np.asarray(aligned)[y0+dy:y1+dy,x0+dx:x1+dx])
    records.append(dict(frame=i,offset=[dx,dy],phase='preserved_rise_and_return' if airborne else 'support_contact',
                        inspectedSupportAnchor=None if airborne else list(anchor),
                        reference=[354,340],resized=False,sourceSha256=hashlib.sha256((P/f'source-{i:02d}.png').read_bytes()).hexdigest()))
manifest=dict(canvas=[640,360],method='integer whole-frame translation; no resampling; source preserved',
              supportReference=[354,340],airborneFrames=list(range(6,13)),frames=records)
(P/'foot-alignment.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
if not (P/'recommended-before-foot-alignment.gif').exists():shutil.copy2(P/'recommended.gif',P/'recommended-before-foot-alignment.gif')

proposal=json.loads((P/'proposal.json').read_text(encoding='utf-8'))
seq=[(1,500,0)]+[(f,ms,hi+1) for hi,h in enumerate(proposal['variants'][0]['hits']) for f,ms in zip(h['frames'],h['ms'])]+[(1,650,0)]
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
frames=[];durations=[];elapsed=previous=0
for f,ms,hi in seq:
    canvas=Image.new('RGB',(700,340),'white');d=ImageDraw.Draw(canvas)
    for col,(path,label) in enumerate([(P/f'source-{f:02d}.png','보정 전'),(OUT/f'frame-{f:02d}.png','발 정렬 후')]):
        im=Image.open(path).crop((180,100,500,355))
        x=col*350+15;canvas.paste(im,(x,35))
        d.line((x,275,x+320,275),fill='#35a08a',width=1)
        d.line((x+174,268,x+174,283),fill='#35a08a',width=1)
        d.text((x,10),label,font=font,fill='#203b47')
    d.text((16,311),f'원본 #{f:02d} · {hi}타' if hi else '대기 · 접지 기준선 비교',font=font,fill='#233c48')
    frames.append(canvas);elapsed+=ms;rounded=round(elapsed/10)*10;durations.append(rounded-previous);previous=rounded
frames[0].save(P/'foot-alignment-comparison.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2)
frames[1].save(P/'foot-alignment-comparison.png')
print(json.dumps({'frames':len(records),'offsets':{str(r['frame']):r['offset'] for r in records if r['frame'] in [1,3,7,13,20,21,30]},'noResizing':True}))
