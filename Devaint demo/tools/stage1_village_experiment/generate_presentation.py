"""Support-aware near scenery and cavern/water anchors; no geometry mutations."""
import argparse,json,math,pathlib
REPO=pathlib.Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description='Generate world-fixed village scenery beside the geometry outputs.')
parser.add_argument('--output',type=pathlib.Path,default=REPO/'outputs/village/layout',help='Directory containing generated VillageWorld.json and village_layout.json.')
ROOT=parser.parse_args().output.resolve()
if not (ROOT/'VillageWorld.json').is_file():parser.error('Run generate_layout.py into --output first.')
world=json.loads((ROOT/'VillageWorld.json').read_text(encoding='utf-8'))
layout=json.loads((ROOT/'village_layout.json').read_text(encoding='utf-8'))
W,H=world['cols'],world['rows'];g=world['ground'];Z={z['id']:z for z in layout['zones']}
dims={'house_balcony':(260,291),'bell_tower':(100,335),'stone_arch':(260,183),'chapel':(292,273),'fountain':(228,156),'courtyard_wall':(292,81),'hedge':(228,84),'aqueduct':(484,171),'wall_lantern':(36,72)}
props=[];skipped=[]
facades={'house_balcony','bell_tower','stone_arch','chapel'}
# Door centres measured in the native sprites, relative to their centre pivot.
entry_offsets={'house_balcony':-42,'bell_tower':0,'stone_arch':0,'chapel':0,'fountain':0}
def ground(x,y):return 0<=x<W and 0<=y<H and g[y*W+x]!=0
def nearzone(x,y):return min(layout['zones'],key=lambda z:(x-z['x'])**2+(y-z['y'])**2)['id']
def supported(kind,x,y):
    w,h=dims[kind]
    # Require actual solid floor below the whole foundation, not one-way decks.
    a=(x*32-w/2+2)/32;b=(x*32+w/2-2)/32
    return all(ground(xx,y) and not ground(xx,y-1) for xx in range(math.floor(a),math.ceil(b)))
def body_clear(kind,x,y):
    w,h=dims[kind]
    # Conservative full-sprite clearance keeps roofs and side walls uncut by terrain.
    return all(not ground(xx,yy)
        for yy in range(math.floor((y*32-h+2)/32),y)
        for xx in range(math.floor((x*32-w/2+2)/32),math.ceil((x*32+w/2-2)/32)))
def placement_conflict(kind,x,y):
    px,py=x*32,y*32;w,h=dims[kind]
    for p in props:
        pw,ph=dims[p['kind']];dx=abs(px-p['x'])
        vertical_overlap=min(py,p['y'])>max(py-h,p['y']-ph)
        if kind in facades and p['kind'] in facades and vertical_overlap:
            # Complete facades have a visible gap, rather than intersecting roofs.
            if dx<(w+pw)/2+32:return 'facade_overlap'
            if kind==p['kind']=='house_balcony' and abs(py-p['y'])<=128 and dx<14*32:
                return 'repeated_house_cluster'
        if kind==p['kind'] and kind in {'courtyard_wall','hedge'} and abs(py-p['y'])<max(h,ph):
            if dx<(w+pw)/2+16:return 'repeated_low_prop_overlap'
        if kind=='hedge' and p['kind'] in entry_offsets and abs(py-p['y'])<64:
            # Bushes may frame walls but must not hide the door/fountain centre.
            if abs(px-(p['x']+entry_offsets[p['kind']]))<w/2+28:return 'entry_obscured'
    return None
def place(kind,x,y,zone=None,search=0,priority='secondary',order=-20):
    candidates=[x]
    for d in range(1,search*4+1):candidates.extend([x-d*.25,x+d*.25])
    xx=next((a for a in candidates if supported(kind,a,y) and body_clear(kind,a,y)
             and placement_conflict(kind,a,y) is None),None)
    if xx is None:
        supported_candidates=[a for a in candidates if supported(kind,a,y) and body_clear(kind,a,y)]
        reason='no_contiguous_floor_or_full_sprite_clearance' if not supported_candidates else placement_conflict(kind,supported_candidates[0],y)
        skipped.append(dict(kind=kind,requested=[x,y],zone=zone,reason=reason));return None
    z=zone or nearzone(xx,y)
    p=dict(id=f'{kind}_{len(props)+1:03d}',kind=kind,x=round(xx*32),y=y*32,scale=1,zone=z,
           sortingOrder=order,worldFixed=True,pivot='bottom_center',nativeSize=list(dims[kind]),
           priority=priority,attachment='ground',groundVerified=True,fullSpriteClearanceVerified=True)
    props.append(p);return p

# Outdoor landmarks use complete sprites. Underground districts use their own
# darker vault art; bright outdoor arches are deliberately excluded there.
for kind,z,x,y in [
 ('stone_arch',1,14,68),('house_balcony',2,73,66),('bell_tower',3,45,34),
 ('house_balcony',5,121,69),('house_balcony',6,121,48),('house_balcony',9,183,74),
 ('bell_tower',10,183,36),('chapel',11,223,43),
 ('fountain',13,225,70),('stone_arch',14,285,70),('chapel',15,284,36),
 ('stone_arch',17,346,88),('bell_tower',18,338,54),
 ('house_balcony',20,398,80),('house_balcony',21,372,34),
 ('bell_tower',23,408,58),('chapel',24,450,62)]:
    place(kind,x,y,z,search=7,priority='landmark',order=-24)

# Secondary architecture leaves breathing room between repeated house sprites.
for x,y in [(6,68),(92,66),(136,50),(148,46),(200,40),(239,43),
            (258,39),(270,39),(305,44),(359,44),(392,34),(432,62),(475,62),
            (140,70),(203,70),(245,70),(259,70),(307,75),(358,88)]:
    place('house_balcony',x,y,search=4,order=-31)

# Low walls and shrubs retain depth at walking level without facade-sized stamps.
for a,b,y in layout['upperContour']:
    length=b-a
    if length<7:continue
    positions=[a+length*.5] if length<27 else [a+6,b-6]
    for x in positions:
        place('courtyard_wall',x,y,search=2,order=-35)
        place('hedge',x+1.5,y,search=5,order=-16)

# Outdoor lower terraces have their own foregrounddepth, notonlythefarvillage.
for x,y in [(84,66),(118,69),(146,70),(187,74),(214,70),(238,70),(252,70),
            (284,70),(348,88),(357,88),(381,80),(399,80)]:
    place('courtyard_wall',x,y,search=3,order=-36)
    place('hedge',x+2,y,search=5,order=-17)

# Avoid exact duplicate crowdedpropplacements.
unique=[];seen=set()
for p in props:
    k=(p['kind'],p['x'],p['y'])
    if k not in seen:unique.append(p);seen.add(k)
props=unique

# Pixelrectanglescoveralllowercavernairwithoverlapinsideopaqueterrain.
# Renderbehindterrain/actors;repeatarchwallstrip alignedto eachlocalfloor.
underground=[]
profile=layout['lowerRoute']
for (a,ya),(b,yb) in zip(profile,profile[1:]):
    minfloor=min(ya,yb);maxfloor=max(ya,yb)
    roomclearance=18 if 198<=a<246 or 320<=a<356 else 15
    underground.append(dict(x=(a-1)*32,y=(minfloor-roomclearance)*32,
       width=(b-a+2)*32,height=(maxfloor-minfloor+roomclearance+6)*32,
       floorY=maxfloor*32,repeatWidth=1086,sourceSize=[1086,362],sortingOrder=-55,
       upperFadePixels=64,zone=nearzone((a+b)/2,maxfloor)))

# Outdoor ascents remain open to the distant village. Rectangular repeating
# building stamps looked cut off and too large beside the complete native props.
# Caverns retain undergroundRects above, separate from these removed stamps.
shaft_rects=[]

lanterns=[]
for x,y in [(28,101),(44,104),(73,106),(96,104),(126,104),(151,104),
            (181,102),(208,104),(218,104),(233,104),(252,104),(284,104),
            (304,104),(337,116),(351,116),(378,108),(397,108),(422,108),(450,108),(474,108)]:
    lanterns.append(dict(kind='wall_lantern',x=x*32,y=y*32-110,scale=1,zone=nearzone(x,y),
       sortingOrder=-15,worldFixed=True,pivot='bottom_center',attachment='wall',lightRadius=88,
       color='#E9BC77',intensity=.16,affectsActors=False))

water=[
 dict(id='cistern_still_water',kind='pool',x=34*32,y=104*32+3,width=15*32,height=12,zone=4,sortingOrder=2,visualOnly=True),
 # At x335 the actual cavern ceiling ends at tileY101; connect the fall
 # to that ceiling and carry it down into the surface at tileY116.
 dict(id='market_waterfall',kind='waterfall',x=335*32,y=101*32,width=28,height=15*32+8,zone=19,sortingOrder=2,visualOnly=True),
 dict(id='canal_pool',kind='pool',x=335*32,y=116*32+3,width=18*32,height=4,zone=19,sortingOrder=2,visualOnly=True),
 dict(id='east_aqueduct_fall',kind='waterfall',x=431*32,y=108*32+3,width=24,height=18*32,zone=22,sortingOrder=2,visualOnly=True),
 dict(id='exit_aqueduct_fall',kind='waterfall',x=468*32,y=108*32+3,width=32,height=22*32,zone=22,sortingOrder=2,visualOnly=True)]
fountains=[dict(kind='fountain_spray',x=p['x'],y=p['y']-118,zone=13,width=44,height=50,sortingOrder=-14,visualOnly=True) for p in props if p['kind']=='fountain']
aqueducts=[dict(kind='aqueduct',x=x*32,y=108*32+171,scale=1,zone=22,sortingOrder=1,
   worldFixed=True,pivot='bottom_center',nativeSize=[484,171],attachment='bridge_underside',
   topAtFloorY=108*32,groundVerified=False,notes='Decorativearcadeoverdarksoilinterior;notnewcollision.') for x in [428,443,458,473]]

# Usefulfixedworlddistrictbackdropcoverage/skyocclusionhints,notgeometry.
result=dict(schemaVersion=1,coordinateSystem='PIXELS, Ypositive down; Unity=(x/32,-y/32)',
   pivot='bottom_center',basePPU=32,props=props,undergroundRects=underground,shaftRects=shaft_rects,
   lanterns=lanterns,waterAnchors=water,fountainAnchors=fountains,aqueducts=aqueducts,
   landmarkProps=[p for p in props if p['priority']=='landmark'],
   initialSketch=dict(untilX=158*32,normalFromX=180*32,worldAnchored=True),
   notes=['Ground props have contiguous solid support and full sprite clearance at native 1x.',
          'Complete facades have at least 32px between their bounding boxes; repeated houses are at least 14 tiles apart on a shared terrace.',
          'Low walls and hedges do not overlap their own kind; hedges leave facade doors and fountain centres visible.',
          'Do not rescale facades with the camera: a native house is 260x291px. No cropped outdoor shaft stamps.',
          'Lanterns are wall mounted; water and underside aqueducts are visual decoration, not hazards.',
          'Underground districts use dedicated dark vault scenery, without bright outdoor stone arches.',
          'Underground rectangle padding is hidden by terrain; preserve it when changing the rear-wall art.'],
   counts=dict(props=len(props),majorLandmarks=sum(p['priority']=='landmark' for p in props),lanterns=len(lanterns),undergroundRects=len(underground),shaftRects=len(shaft_rects)),
   skippedPlacements=skipped)
outdoor_zones={1,2,3,5,6,9,10,11,13,14,15,17,18,20,21,23,24}
missing=sorted(outdoor_zones-{p['zone'] for p in props if p['priority']=='landmark'})
assert not missing, f'Missing outdoor landmarks: {missing}'
assert all(supported(p['kind'],p['x']/32,p['y']//32) and body_clear(p['kind'],p['x']/32,p['y']//32) for p in props)
assert all(p['y']<100*32 for p in props), 'Outdoor props must not be placed in the lower underground route.'
(ROOT/'presentation_layout.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result['counts']))
print('Outdoor landmarks: 17/17; underground districts use dedicated vault art. All prop foundations and full sprite bounds clear.')
