from pathlib import Path
from PIL import Image, ImageDraw
import json, math, random, shutil
R=Path(__file__).resolve().parent
M=json.loads((R/'assets/asset-manifest.json').read_text(encoding='utf-8'))
# Ink warnings in the boss FX language: three colours, binary alpha, 20 FPS, 1 s warning.
INK,MID,GLINT='#171716','#41413D','#9C968D'
colors=[INK,MID,GLINT]
FRAMES=20

def save(folder,frames):
    out=R/'assets/fx'/folder;out.mkdir(parents=True,exist_ok=True)
    names=[]
    for f,im in enumerate(frames):
        name=f'assets/fx/{folder}/{f:02}.png';im.save(R/name);names.append(name)
    return names

def ellipse(d,cx,cy,rx,ry,color):
    d.ellipse([round(cx-rx),round(cy-ry),round(cx+rx),round(cy+ry)],fill=color)

def wall_boil(height=200):
    # Plays after the small fist has left (0.5 s): ink soaks through the launching wall in a
    # 192 px fist-shaped lens, then boils and cracks until the giant fist breaks out at 1.0 s.
    # Frames 0-5 soak (0.3 s); 6-13 boil and loop until the strike ends.
    rng=random.Random(5);frames=[]
    cracks=[(rng.uniform(.12,.9),rng.randint(16,34),rng.uniform(-.7,.7)) for _ in range(6)]
    edge=[rng.uniform(-3,3) for _ in range(height//8+2)]
    bubbles=[(rng.uniform(.08,.92),rng.uniform(0,1),rng.choice([6,8,9,11])) for _ in range(10)]
    mid=height-96
    for f in range(14):
        im=Image.new('RGBA',(80,height+8));d=ImageDraw.Draw(im)
        soak=min(1,f/5);boil=max(0,(f-6)/7);half=96*(.35+.65*soak);W=10+22*soak
        def width_at(y):
            u=(y-mid)/96;k=y/8;i=min(int(k),len(edge)-2);n=edge[i]*(1-(k-i))+edge[i+1]*(k-i)
            return W*math.sqrt(max(0,1-u*u))+n
        for y in range(round(mid-half),round(mid+half),2):
            w=round(width_at(y)+(2*math.sin(y*.21+f*1.7) if f>=6 else 0))
            if w>0:d.rectangle([0,y,w,y+1],fill=INK)
            if w>6 and y%12==0:d.rectangle([w-5,y,w-4,y],fill=MID)
        if f>=6:
            # Bubbles swell on the stain edge and pop into droplets on staggered beats.
            for fy,ph,r in bubbles:
                y=mid+(fy-.5)*180;x=width_at(y);p=(boil*2+ph)%1
                if p<.75:
                    rr=max(1,round(r*(.4+.8*p)));ellipse(d,x+rr*.6,y,rr,rr,INK if p<.5 else MID)
                    d.point((round(x+rr*.6-rr*.4),round(y-rr*.5)),fill=GLINT)
                else:
                    q=(p-.75)/.25
                    for dx,dy in ((1,-1),(1.4,.2),(.8,1)):
                        d.rectangle([round(x+4+dx*q*12),round(y+dy*q*10),round(x+5+dx*q*12),round(y+1+dy*q*10)],fill=INK)
            for fy,length,slope in cracks:
                y=mid+(fy-.5)*180;x0=round(width_at(y));n=round(length*min(1,boil*1.4))
                for s_ in range(n):
                    y+=slope+(.6 if s_%9==4 else 0)
                    d.rectangle([x0+s_,round(y),x0+s_+(1 if s_<n*.6 else 0),round(y)+(1 if s_<n*.6 else 0)],fill=INK)
        frames.append(im)
    return frames

def needle_ink(width=96,height=472,floor=464):
    # Open arena: ink seeps from the top screen edge over the locked 96 px landing column, swells into
    # thick drips and sheds drops to the floor; the main drip stretches just before the drop.
    rng=random.Random(11);half=width//2;cx=64;frames=[]
    lumps=[(x,rng.uniform(.65,1)) for x in range(-half+4,half-3,8)]
    drips=[(-26,.12,100,9),(0,.02,150,14),(25,.22,116,10)]  # long enough to show below the top HUD  # x, start, max length, top width
    drops=[(0,.5),(0,.7),(-26,.62),(25,.82),(0,.9)]       # x, release time
    def drip_len(start,length,t):
        u=max(0,min(1,(t-start)/.6));return length*(u*u*(3-2*u))
    for f in range(FRAMES):
        t=f/(FRAMES-1);im=Image.new('RGBA',(128,height));d=ImageDraw.Draw(im)
        seep=min(1,t/.25);sh=6+14*seep+(3*math.sin(t*50) if t>.8 else 0)
        d.rectangle([cx-half,0,cx+half-1,5],fill=INK)
        for x,k in lumps:ellipse(d,cx+x,0,7,sh*k,INK)
        for x,k in lumps[1::3]:d.rectangle([cx+x-2,round(sh*k*.45),cx+x,round(sh*k*.45)],fill=GLINT)
        for x,start,length,w in drips:
            L=drip_len(start,length,t)
            if x==0 and t>.8:L+=34*(t-.8)/.2   # final stretch: the needle is about to fall
            if L<2:continue
            top=round(sh*.6)
            for y in range(top,top+round(L)):
                u=(y-top)/max(1,L);ww=max(2,round(w*(1-u*.65)/2))
                d.rectangle([cx+x-ww,y,cx+x+ww-1,y],fill=INK)
                if ww>3:d.point((cx+x-ww+1,y),fill=MID)
            br=round(w*.42+2+(1.5*math.sin(t*40+x) if t>.6 else 0));by=top+L
            ellipse(d,cx+x,by,br,br*1.15,INK);d.rectangle([cx+x-br//2,round(by-br*.5),cx+x-br//2+1,round(by-br*.5)+1],fill=GLINT)
        for x,rel in drops:
            if t<rel:continue
            L=[l for dx,st,l,w in drips if dx==x][0];st=[st for dx,st,l,w in drips if dx==x][0]
            y0=round(sh*.6)+drip_len(st,L,rel)+6;dt=t-rel;y=y0+.5*2600*dt*dt
            if y<floor-6:
                ellipse(d,cx+x,y,4,7,INK);d.rectangle([cx+x-2,round(y)-3,cx+x-1,round(y)-2],fill=GLINT)
            elif dt<.5:
                ellipse(d,cx+x,floor-1,7,2,INK);d.rectangle([cx+x-10,floor-5,cx+x-9,floor-4],fill=INK);d.rectangle([cx+x+9,floor-6,cx+x+10,floor-5],fill=INK)
        frames.append(im)
    return frames

def clock_ring(r=192,size=416):
    # A clock face traced in ink: the full 192 px hit radius, with hour ticks.
    c=size/2;frames=[]
    def stamp(d,a,rad,thick,color):
        x=c+math.sin(a)*rad;y=c-math.cos(a)*rad;ellipse(d,x,y,thick,thick,color)
    for f in range(FRAMES):
        t=f/(FRAMES-1);im=Image.new('RGBA',(size,size));d=ImageDraw.Draw(im)
        trace=min(1,t/.45)*math.tau
        steps=int(trace/(math.tau/360))
        for s in range(steps):
            a=s*math.tau/360;wob=1.2*math.sin(a*7+f*.4)
            stamp(d,a,r-3+wob,2.6,INK)
        for s in range(0,steps,9):
            a=s*math.tau/360;stamp(d,a,r-6,.6,MID)
        if trace<math.tau:stamp(d,trace,r-3,5,INK)
        for h in range(12):
            a=h*math.tau/12
            if a>trace:continue
            length=22 if h%3==0 else 12;thick=2.6 if h%3==0 else 1.6
            for q in range(0,length,2):stamp(d,a,r-8-q,thick,INK)
            if h%3==0:stamp(d,a,r-10,1,GLINT)
        if trace>=math.tau:
            # Loop phase: drips leave the lower rim so the ring reads as wet ink.
            for k in range(6):
                a=math.pi*(.6+.16*k);ph=(t*2.2+k*.37)%1
                x=c+math.sin(a)*(r-1);y=c-math.cos(a)*(r-1)+ph*20
                d.rectangle([round(x)-1,round(y),round(x)+1,round(y)+3],fill=INK)
        frames.append(im)
    return frames

for old in ('fistFloorInk','mixerFloorInk'):
    # Floor ink was dropped: players learn the sweep by playing.
    M['fx'].pop(old,None);shutil.rmtree(R/'assets/fx'/old,ignore_errors=True)
shutil.rmtree(R/'assets/fx/wallInk',ignore_errors=True)
M['fx']['wallInk']={'frames':save('wallInk',wall_boil()),'fps':20,'pivot':[0,200],'startAt':.5,'loopFrom':6,'palette':colors,
  'notes':'Starts after the small fist leaves (pattern age 0.5 s). Soaks 0.3 s, then boils 192 px up the launching wall until the strike ends.'}
M['fx']['needleInk']={'frames':save('needleInk',needle_ink()),'fps':20,'pivot':[64,0],'ceilingY':0,'palette':colors,
  'notes':'1 s needle warning. Pivot sits on the top screen edge over the 96 px landing column; drops reach the floor at y=464.'}
M['fx']['clockRing']={'frames':save('clockRing',clock_ring()),'fps':20,'pivot':[208,208],'loopFrom':10,'palette':colors,
  'notes':'Ink clock face at the 192 px mixer hit radius. Frames 0-9 trace it, 10-19 loop while the mixer moves.'}
M['room']='assets/room-open.png'  # open arena: no side pillars or ceiling cornice
M['revision']='open-arena-v12'
(R/'assets/asset-manifest.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
print('ink telegraph FX: wallInk 80x208 14 frames, needleInk 128x472 20 frames, clockRing 416x416 20 frames')
