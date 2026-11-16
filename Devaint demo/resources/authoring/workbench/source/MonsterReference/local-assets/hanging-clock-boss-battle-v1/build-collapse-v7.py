from pathlib import Path
from PIL import Image, ImageDraw
import json, math, random
R=Path(__file__).resolve().parent
M=json.loads((R/'assets/asset-manifest.json').read_text(encoding='utf-8'))
# Hand-authored silhouette parameters, stable across frames; no random flicker.
colors=['#575451','#9C968D','#D6D0C7']
folder=R/'assets/fx/collapsePlume';folder.mkdir(exist_ok=True)
files=[]
lobes=[(-260,100,64,.08),(-180,218,87,.03),(-80,272,96,.07),(35,225,100,0),(140,300,90,.05),(248,170,78,.1)]
def blob(d,cx,cy,rx,ry,seed,color):
    pts=[]
    for k in range(64):
        a=k*math.tau/64
        rough=1+.045*math.sin(k*2.3+seed)+.04*math.sin(k*.8+seed*3)
        pts.append((round((cx+math.cos(a)*rx*rough)/2)*2,round((cy+math.sin(a)*ry*rough)/2)*2))
    d.polygon(pts,fill=color)
for f in range(60):
    t=f/24;im=Image.new('RGBA',(800,400));d=ImageDraw.Draw(im)
    for layer in range(2):
        for i,(dx,height,r,delay) in enumerate(lobes):
            age=max(0,t-delay);grow=1-math.exp(-age*9)
            drift=max(0,age-.3)
            cx=400+dx*(.55+.45*grow)+dx*.13*drift
            cy=390-height*grow-19*drift
            rx=r*grow*(1+drift*.16);ry=r*grow*(1+drift*.22)
            if age<=0:continue
            if layer==0:
                # Rising columns join into a dense, wide cloud rather than isolated bubbles.
                blob(d,cx,cy+(390-cy)*.46,rx*.78,(390-cy)*.55+ry*.35,i,colors[1])
                blob(d,cx,cy,rx,ry,i,colors[1])
            else:
                # Broad curling shadow bands, not concentric outlines or bright circular dots.
                pts=[]
                for k in range(18):
                    u=k/17
                    xx=cx-rx*.83+rx*1.6*u
                    yy=cy+ry*(.04+.2*math.sin(u*math.pi*1.8+i*.7))
                    pts.append((round(xx/2)*2,round((yy+math.sin(k*2.1+i)*4)/2)*2))
                for k in reversed(range(18)):
                    u=k/17;xx=cx-rx*.83+rx*1.6*u
                    yy=cy+ry*(.16+.42*math.sin(u*math.pi))
                    pts.append((round(xx/2)*2,round((yy+math.sin(k*1.7+i)*5)/2)*2))
                d.polygon(pts,fill=colors[0])
                # Rising overlaps break up the horizontal bands into turbulence.
                blob(d,cx-rx*.37,cy+ry*.32,rx*.36,ry*.23,i+7,colors[1])
    name=f'assets/fx/collapsePlume/{f:02}.png';im.save(R/name);files.append(name)
M['fx']['collapsePlume']={'frames':files,'fps':24,'pivot':[400,392],'palette':colors,'fadeAfter':.75,'fadeDuration':1.75}
M['revision']='weight-plume-v7'
(R/'assets/asset-manifest.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
print('60 native 800x400 three-color impact plume frames')
