"""Create the experimental reference village layout. Coordinates are tile units, Y down.
Outputs are explicit build artifacts. ForestData coordinates follow ForestCombatWorld.
"""
import argparse, json, math, pathlib, hashlib
REPO=pathlib.Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description='Generate the isolated reference village geometry; never copies into Unity.')
parser.add_argument('--output',type=pathlib.Path,default=REPO/'outputs/village/layout',help='Generated output directory (default: repository outputs/village/layout).')
parser.add_argument('--layout',type=pathlib.Path,default=REPO/'data/stage1-village-experiment/map-layout.json',help='Authored village topology and landmark mapping.')
args=parser.parse_args()
ROOT=args.output.resolve();ROOT.mkdir(parents=True,exist_ok=True)
blueprint=json.loads(args.layout.read_text(encoding='utf-8'))
W,H=blueprint['width'],blueprint['height']
g=[[0]*W for _ in range(H)]
P=[]

def rect(x,y,w,h,value):
    for yy in range(max(0,y),min(H,y+h)):
        for xx in range(max(0,x),min(W,x+w)):g[yy][xx]=value

def deck(x,y,w,label='climb',full=False):
    if y<0 or y>=H:return
    # Decks are one-way and their original art is decorative only.
    P.append(dict(x=int(x*32),y=int(y*32),w=int(w*32),earthArtwork=False,blockDropThrough=False))

# Strong varied upper contour. Tall steps have authored ascent shafts below.
upper=blueprint['upperContour']
for a,b,y in upper:rect(a,y,b-a,H-y,1)

# Middle streets curve up and down under raised district terraces.
middle=blueprint['middleRoute']
lower=blueprint['lowerRoute']

def carve_profile(points,kind):
    for (a,ya),(b,yb) in zip(points,points[1:]):
        for x in range(a,b):
            # Every rise is at most one tile, sufficient running room per stair.
            y=round(ya+(yb-ya)*(x-a)/max(1,b-a))
            if kind=='lower':
                clearance=14 if 207<=x<245 else 15 if 324<=x<355 else 10
                if x%24 in (0,1):clearance-=1
            else:clearance=12 if 200<=x<292 else 10
            rect(x,y-clearance,1,clearance,0)
    return
carve_profile(middle,'middle');carve_profile(lower,'lower')

# Stairs along upper terraces where rises do not have a full vertical connector.
# A thin set of one-way landings provides bidirectional access, not ladders.
ascents=[]
for i in range(len(upper)-1):
    a,b,y=upper[i];c,d,z=upper[i+1]
    if abs(y-z)<=2:continue
    edge=b;lo=min(y,z);hi=max(y,z)
    # Side facing the lower terrain. Avoid making solid rectangular stair towers.
    left=y>z
    sx=max(1,edge-8) if left else edge
    for yy in range(lo+3,hi,3):
        if all(not g[yy-1][xx] and not g[yy-2][xx] for xx in range(sx,min(W,sx+8))):
            deck(sx,yy,min(8,W-sx),'terrace')
    ascents.append(dict(boundaryX=edge,topY=lo,bottomY=hi,lowSide='left' if left else 'right'))

# Reference-visible vertical links; these make surface/roof/underground loops.
shafts=blueprint['shafts']
for s in shafts:
    rect(s['x'],s['top']-3,s['w'],s['bottom']-s['top']+3,0)
    levels=list(range(s['top'],s['bottom'],4))
    # Full bridges at intersections allow straightforward horizontal traversal.
    intersections=set([s['top']])
    for profile in [middle,lower]:
        for (a,ya),(b,yb) in zip(profile,profile[1:]):
            if a<=s['x']+s['w']/2<b:
                intersections.add(round(ya+(yb-ya)*(s['x']+s['w']/2-a)/(b-a)))
    for idx,y in enumerate(levels):
        if any(abs(y-iy)<2 for iy in intersections):continue
        deck(s['x']+(0 if idx%2==0 else 4),y,8)
    for y in sorted(intersections):
        if s['top']<=y<s['bottom']:deck(s['x'],y,s['w'],'bridge',True)

# Optional lookout decks create compact fight/readability checks in broad zones.
for x,y,w in [(72,62,7),(118,44,8),(209,39,8),(249,66,7),(287,32,8),
              (346,84,7),(393,76,7),(441,58,7)]:
    if all(not g[y-1][xx] and not g[y-2][xx] for xx in range(x,x+w)):deck(x,y,w,'lookout')

# Keep only decks with clear body space; trim rare edges that touch solid steps.
clean=[]
for p in P:
    x,y,w=p['x']//32,p['y']//32,p['w']//32
    spans=[];begin=None
    for xx in range(x,x+w+1):
        valid=xx<x+w and y>=3 and not any(g[yy][xx] for yy in range(y-3,y))
        if valid and begin is None:begin=xx
        if not valid and begin is not None:
            if xx-begin>=3:spans.append((begin,xx))
            begin=None
    for a,b in spans:clean.append(dict(p,x=a*32,w=(b-a)*32))
P=list({(p['x'],p['y'],p['w']):p for p in clean}.values())
# Repeated ascent shelves should read as compact stone ledges, not screen-wide
# floating strips. Keep junction bridges wide and preserve each shelf center.
for p in P:
    if 5*32 < p['w'] <= 8*32:
        p['x']+=(p['w']-5*32)//2
        p['w']=5*32

# Landmarks are navigation IDs, not fabricated reward/boss gameplay policies.
zones=[(z['id'],z['name'],math.floor(z['x']),z['y'],z['biome'],z['landmark'],z['role']) for z in blueprint['zones']]
links=[(c['a'],c['b']) for c in blueprint['connections']]

def supported(x,y):
    return g[y][int(x)] or any(p['y']==y*32 and p['x']/32<=x<(p['x']+p['w'])/32 for p in P)
def clearbody(x,y):
    return all(0<=xx<W and 0<=yy<H and not g[yy][xx] for xx in range(math.floor(x-.29),math.floor(x+.66)+1) for yy in range(math.floor(y-2),math.floor(y-.015)+1))

# Pin every navigation/warp marker to safe actual geometry near its authored point.
Z=[]
for n,name,x,y,biome,landmark,role in zones:
    candidates=[(abs(xx-x)+2*abs(yy-y),xx,yy) for yy in range(max(3,y-2),min(H-1,y+3)) for xx in range(max(2,x-7),min(W-2,x+8)) if supported(xx+.5,yy) and clearbody(xx+.5,yy)]
    if not candidates:raise ValueError(f'No safe footing for zone{n}')
    _,x,y=min(candidates)
    Z.append(dict(id=n,name=name,x=x+.5,y=y,biome=biome,landmark=landmark,role=role,initialSketch=n in [1,2,3,4,5,6,7,8],neighbors=sorted(b if a==n else a for a,b in links if n in (a,b))))

# Greedy vertical coalescing of identical horizontal spans = exact compact solids.
rects=[];active={}
for y,row in enumerate(g):
    spans=[];start=None
    for x in range(W+1):
        v=x<W and row[x]
        if v and start is None:start=x
        if not v and start is not None:spans.append((start,x-start));start=None
    next_active={}
    for span in spans:
        if span in active:r=active.pop(span);r['h']+=1
        else:r=dict(x=span[0],y=y,w=span[1],h=1)
        next_active[span]=r
    rects.extend(active.values());active=next_active
rects.extend(active.values())

# Native atlas mapping for fallback rendering (approved fifteen terrain order).
cells=[]
def solid(x,y):return x<0 or x>=W or y>=H or (y>=0 and g[y][x])
for y in range(H):
 for x in range(W):
    if not g[y][x]:continue
    up=solid(x,y-1);left=solid(x-1,y);right=solid(x+1,y)
    if not up:tid=3 if not left else 2 if not right else [0,1,4,5][(x*7+y)%4]
    elif not left:tid=7
    elif not right:tid=6
    else:tid=14
    cells.append(dict(x=x,y=y,id=tid))

original=json.loads((REPO/'unity/Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json').read_text(encoding='utf-8-sig'))
actors=[]
for z in Z:
    if z['id'] not in [2,6,9,11,15,17,18,20,23]:continue
    for offset in [-4,4]:
        x=z['x']+offset;y=z['y']
        if supported(x,y) and all(clearbody(x+dx,y) for dx in [-1.2,0,1.2]):
            actors.append(dict(kind=(z['id']+offset)%len(original['monsters']),zone=z['id'],x=x*32,y=y*32,roaming=True,roamSeed=6500+z['id']*10+offset))
views=[dict(name=f"{z['id']:02d} {z['name']}",x=z['x']*32,y=z['y']*32,a=max(0,z['x']-15)*32,b=min(W,z['x']+15)*32) for z in Z]
landmarks=[dict(kind=z['role'],name=z['name'],x=z['x']*32,y=z['y']*32,zone=z['id']) for z in Z if z['role'] in ['start','treasure','midboss_marker','boss_marker']]
world=dict(width=W*32,height=H*32,cols=W,rows=H,atlasColumns=8,uniqueTiles=15,spawn=dict(x=Z[0]['x']*32,y=Z[0]['y']*32),ground=[v for row in g for v in row],rects=rects,platforms=P,cells=cells,layers=[],views=views,monsters=original['monsters'],actors=actors,nodes=[],route=[],landmarks=landmarks,encounters=dict(minimumPerCombatZone=0,roamRadius=.8,roamSpeedScale=.35,waitSeconds=[1.2,3.2],walkSeconds=[.5,1.2]))
props=[]
for z in Z:
    props.append(dict(id=f"zone_{z['id']:02d}_{z['landmark']}",type=z['landmark'],zone=z['id'],x=z['x']-5,y=z['y'],width=14 if z['role'] in ['boss_marker','midboss_marker','vista'] else 9,height=12 if z['role'] in ['boss_marker','vista'] else 8,worldFixed=True,sortingOrder=-20,biome=z['biome']))
layout=dict(schemaVersion=1,status='experimental_unity_physics_not_yet_validated',coordinateSystem='tile units (32px), positive Y down; Unity=(x,-y)',width=W,height=H,zones=Z,connections=[dict(a=a,b=b,bidirectional=True,status='intended_topology') for a,b in links],shafts=shafts,terraceAscents=ascents,upperContour=upper,middleRoute=middle,lowerRoute=lower,nearProps=props,exit=dict(x=475,y=108),restoration=dict(initialSketchUntilX=158,initialNormalFromX=180,blendWidth=22,stateSpace='world_x',notes='Initial reference state only; runtime restoration must stay attached to world areas.'),movementSource='data/combat/locomotion/player.json',referenceNotes=['IDs are reference landmarks, not24 isolated rectangular rooms.','Only geometry and existing six species; boss and reward icons are markers.','Sky/remote layers excluded from ForestData; integration renderer supplies them.'])
for name,data in [('VillageWorld.json',world),('village_layout.json',layout)]:
    (ROOT/name).write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')

# Lightweight engineering map: derived actual geometry, not concept artwork.
svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 576"><rect width="1920" height="576" fill="#e8e1d0"/>']
for r in rects:svg.append(f'<rect x="{r["x"]*4}" y="{r["y"]*4}" width="{r["w"]*4}" height="{r["h"]*4}" fill="#30282b"/>')
for p in P:svg.append(f'<rect x="{p["x"]/8}" y="{p["y"]/8}" width="{p["w"]/8}" height="1.5" fill="#a38655"/>')
for z in Z:
    x=z['x']*4;y=z['y']*4-8
    svg.append(f'<circle cx="{x}" cy="{y}" r="8" fill="#173e40" stroke="#fff"/><text x="{x}" y="{y+3}" font-family="sans-serif" font-size="9" fill="white" text-anchor="middle">{z["id"]}</text>')
svg.append('</svg>');(ROOT/'layout_geometry.svg').write_text(''.join(svg),encoding='utf-8')
print(json.dumps(dict(zones=len(Z),rects=len(rects),platforms=len(P),actors=len(actors),size=[W,H],geometryHash=hashlib.sha256(bytes(world['ground'])).hexdigest())))
