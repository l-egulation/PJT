"""Retained choices plus sixteen new compact, outline-free character candidates."""
from pathlib import Path
import sys,json,shutil,hashlib,re,zipfile,html
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,find_objects
import normal_round2 as shared

ROOT=shared.ROOT;OUT=ROOT/'outputs/waterway/next-forms-16-20261031'
PRIOR=ROOT/'outputs/waterway/form-exploration-outlinefree-20261031'
N=Image.Resampling.NEAREST
SETS={
 'D':[
 ['스침몸','근거리·접근','낮은 어깨덩이와 납작한 두 발','몸을 낮게 미끄러뜨려 손날로 치기',70,42,'A small LOW SKIMMING humanoid with NO separate head: a broad dusty ROSE shoulder slab containing one short dark face slit, two flat light BLUE skating feet under a compressed waist, one IVORY blade-flat hand extended forward and one short arm trailing behind. A compact sliding stance, short limbs, no long neck, no wheel or animal muzzle. Broad rose/ivory/blue filled masses.'],
 ['붙은얼굴','근거리·기본','가슴에 붙은 얼굴과 짧고 넓은 팔','짧은 팔날을 번갈아 밀어치기',92,64,'A squat compact HUMANOID whose flat OCHRE face is sunk directly into a wide IVORY chest collar, no visible neck. Broad dull brick-red lower torso, two short strong slate legs and two wide arms, one warm-grey blade fist forward and the other pulled close. The chest and sunken face form one blunt mass, not a floating white head. Uneven shoulders, grounded weight, no armor costume or hand-held weapon.'],
 ['모은손','원거리·직사','넓은 몸과 발사구를 만드는 두 손바닥','두 손을 모았다 벌리며 먹탄 밀어내기',92,66,'A compact RANGED humanoid with a wide pale MINT torso and short PLUM planted legs, head a broad low dark-violet inset visor across the upper chest rather than a separate head. Both large PALE GOLD hands are cupped together in front, forming one horizontal left-facing dark launch gap between the palms. Hunched shoulders compress behind the hands, braced firing preparation. No bow, string, long cannon or projectile.'],
 ['접이포좌','원거리·직사','접힌 어깨판과 짧고 굵은 기계 발사팔','몸을 접어 고정한 뒤 짧게 분사',100,64,'An original compact MACHINE firing character: dusty TEAL three-piece shoulder plates folded around a short muted RED central body, two broad low ochre feet, one SHORT THICK ivory-blue articulated forearm whose rectangular open channel points horizontally left. No separate head; one small side vent is the face cue. Rear arm braces the elbow. A kneeling compressed firing stance, no domestic object, tank, barrel or long sniper gun.']
 ],
 'E':[
 ['뒤집힌걸음','근거리·접근','한 손에 실린 몸과 위로 접힌 두 다리','몸을 뒤집듯 튕겨 짧게 붙기',68,44,'A small INVERTED ink-humanoid in a one-hand vault: one big PALE BLUE palm planted forward under a compact IVORY shoulder mass, two thick dusty CORAL legs folded upward behind the mauve waist, tiny dark face notch integrated low at the waist. Off-balance but readable three-mass silhouette, no floating white head, animal body or everyday object. It moves by tipping from hand to foot, no motion trail.'],
 ['삼각어깨','근거리·기본','삼각형 어깨몸과 짧은 지지다리','넓은 어깨 아래 주먹으로 밀어치기',96,66,'A broad TRIANGULAR upper-body creature with no neck or detached head: one PALE STEEL-BLUE shoulder mass slopes into a dusty YELLOW belly, a single dark face notch under the forward shoulder, three SHORT bent supporting legs and one thick coral knuckle arm pushed left. The other arm is small and tucked against the triangle. A compact original humanoid-machine silhouette, no recognizable object or animal.'],
 ['목구멍몸','원거리·직사','상체 전체를 벌리는 넓은 목구멍과 짧은 발','몸 안쪽을 조여 전방으로 내뱉기',94,66,'A soft compact ranged body whose HEAD AND TORSO are one broad dusty MAUVE oval, with a large PALE SAND collar lip opening horizontally left around a dark ink throat. Two short thick pale-grey feet brace underneath, small folded arms press the oval sides. No separate skull, neck, pipe or pot; the opening is a flexible body fold. Firing tension in the compressed sides, no teeth, glow or projectile.'],
 ['가슴셔터','원거리·직사','넓은 기계가슴과 벌어진 두 셔터판','셔터를 열어 좁은 틈으로 사격',98,64,'A short broad MACHINE with one pale GREY-BLUE chest and two thick muted OCHRE shutter arms opening around a horizontal left-facing firing slit at upper-chest level. Headless sloped top, two chunky mauve feet widely planted, faded CORAL back plane. The shutters are body limbs, not a mailbox or appliance. Compact wide silhouette and a clear launch gap, no long barrel, small bolts or shot.']
 ],
 'F':[
 ['짧은궤도','근거리·접근','좁은 상체와 넓은 두 궤도발','낮게 주행해 앞팔로 찍기',72,44,'A small athletic humanoid MACHINE with a compact dusty RED upper body, one PALE MINT forward punch arm and two unusually broad short SLATE-BLUE tread feet beneath a narrow waist. Face is a small pale slit cut into the front chest; no separate head. Low forward lean with arms tucked, no actual tank, wheels as body, domestic object or animal features.'],
 ['밀린얼굴','근거리·기본','옆구리로 밀린 얼굴과 높게 솟은 한쪽 팔','몸을 비틀어 넓은 주먹으로 치기',98,66,'A chunky distorted HUMANOID with a broad dusty VIOLET chest, short sturdy PALE CYAN limbs, one OCHRE face embedded LOW on the left waist instead of on the neck, and one shoulder raised into a thick overhead elbow. Front arm is a wide pale-grey knuckle held at chest level. Upper silhouette uneven, compact full body, no long thin mannequin limbs, armor costume or recognizable object.'],
 ['깍지포','원거리·직사','맞물린 두 팔과 두꺼운 다리','팔을 맞물려 가운데 포구로 발사',98,66,'A sturdy compact RANGED humanoid, dusty STEEL-BLUE torso and two thick planted legs, a small RUST-colored head pressed into the shoulders. Both broad IVORY forearms interlock at the wrists into a SHORT horizontal left-facing launch opening; two curved elbows make open triangular gaps behind the joined hands. Braced squat stance, not a bow with strings or a long gun, no projectile.'],
 ['등판발사체','원거리·직사','높은 등판과 낮게 눌린 기계몸','등판을 세워 상부 틈에서 분사',98,66,'A low seated headless MACHINE creature: broad PALE BLUE-GREY central body, one tall simple IVORY back plate rising behind, three short coral bracing feet and two short warm-grey arms pressed to the ground. A dark firing slot at the upper front points horizontally left above the body, with one short gold lip. Strong high-back/low-body silhouette, no animal head, pipe, household object or long cannon.']
 ],
 'G':[
 ['반몸추격체','근거리·접근','한쪽 큰 다리에 실린 짧은 몸','큰 발을 튕겨 비스듬히 접근하기',68,44,'A small asymmetric skipping humanoid with one BIG bent CORAL support leg and a tiny rear grey foot under a compact PALE ROSE body. No head; two small dark face cuts sit on the chest. One broad PALE BLUE arm trails behind as counterweight and the front arm is tiny. Short blunt filled shape masses, no long thin runner skeleton, bird, shoe-object or animal muzzle.'],
 ['주먹걸음','근거리·기본','두 큰 주먹으로 버티는 낮은 몸','주먹으로 디디며 짧게 후려치기',100,64,'A low knuckle-walking humanoid MACHINE: broad dusty OCHRE upper body over a dark ink waist, two huge PALE GREY squared knuckle forearms used as forward supports and two tiny muted red feet behind. NO separate head; one short dark visor band across the upper torso. Piston-like wrist blocks, unequal shoulder height, massive compact front limbs, no gorilla face, animal fur, recognized appliance or rocks.'],
 ['턱없는포효','원거리·직사','어깨에 붙은 넓은 얼굴과 벌어진 목','앞목을 조여 수평 먹덩이 분출',96,66,'A compact RANGED humanoid with a wide pale IVORY mask-like face integrated into broad warm GREY shoulders, no detached head or neck. The large dark throat gap opens horizontally left below the face, dusty VIOLET short waist and legs brace backward. One small arm presses the belly and the other is raised beside the collar. No animal jaw, teeth, pipe or long gun; the body launches from the throat.'],
 ['추팔발사체','원거리·직사','짧은 기계몸과 아래로 무거운 발사팔','굵은 팔을 고정해 낮게 발사',100,66,'A short heavy-armed ranged MACHINE humanoid: compact worn COPPER body, small face slot integrated into a pale SAGE shoulder, one VERY THICK pale BLUE-WHITE segmented arm hanging forward with a short horizontal firing aperture at its low front tip. Two short grey legs and one rear support finger brace recoil. The arm is a weighty limb rather than a long cannon, no head on a neck, household object or animal.']
 ]}
COMMON='''OUTLINE-FREE pixel character design: absolutely no black contour, colored perimeter stroke, bevel rim, bright rim or outline of any thickness. Material fills meet transparency directly. Use large flat pixel clusters, about 6-10 matte colors per creature and 2-3 broad value planes per material. No smooth painting, 3D rendering, gradients, dithering, line hatching, tiny bolts or realistic texture.
World: original ordinary enemies from a neglected sewer beneath an ink city, subtly warped by ink at body joins. Explore strong compact CHARACTER anatomy, humanoid and purpose-designed mechanical bodies. Do not make animals or recognizable everyday objects with legs. Do NOT repeat thin long-limbed mannequins with small floating ivory heads. Vary headless bodies, integrated faces, weight, stance, supports and launch anatomy; at most ONE separate ivory head on the whole board. Dark ink is local inside joints/gaps, never a stroke tracing the outside. Broad material planes remain light enough against grey-green wall, blue far arches and black foreground. Each candidate has its own distinct palette; no single beige uniform. No neon, glow, gore or smiling mascot eyes.
Input 1 approved player: density/scale reference only, actual art 66x64 within canvas 192x160, do not redraw. Input 2 retained A3 hulking elite and Input 3 retained B3 wedge heavy: use ONLY their crisp lineless color-block rendering and substantial masses, not their exact silhouettes. Do not generate either retained figure or imitate their bodies. Input 4 unchanged sewer scene is environment context only, no scenery in output. The old selected orange thrower is not an input and must not appear.
Exactly FOUR completely different NEW full-body creatures on a landscape transparent board, 1536x1024. UPPER LEFT fast melee, UPPER RIGHT standard melee, LOWER LEFT ranged option 1, LOWER RIGHT ranged option 2. All strict LEFT-FACING SIDE PROFILE for a side-scrolling game. Ordinary sizes roughly 40-66px high in game; display enlarged. No new elite, boss or heavy-melee slot; those shapes have already been retained. Generous transparent gaps, no overlap, each figure fully in its quadrant. No duplicate views, labels, text, panel borders, floor, shadows, projectiles or external effects. Static concepts only, not animations or a sprite sheet.
Final edge check: extend the adjacent flat fill to every outer edge; no dark or colored line around any body part. Keep the shapes readable before details.
'''

def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def prepare():
 OUT.mkdir(parents=True,exist_ok=True);refs=OUT/'references';refs.mkdir(exist_ok=True)
 source=ROOT.parent/'stage2-waterway-reference/outputs/waterway/parallax-kit-v3-20261031'
 for name in ['README.md','A-game.png','B-game.png','C-game.png','D-game.png','E-game.png','assets/approved-player.png','assets/far.png','assets/wall.png','assets/foreground.png','layout.json']:
  dst=refs/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source/name,dst)
 retained=[]
 for ident,role,name in [('A3','엘리트 후보','편측거구'),('B3','큰 근거리 후보','쐐기보행기')]:
  k=ident[0];meta=json.loads((PRIOR/k/'metadata.json').read_text(encoding='utf-8'))['items'][2];dst=OUT/'retained'/ident;dst.mkdir(parents=True,exist_ok=True)
  for src,new in [(PRIOR/k/'raw/generated.png','original-board.png'),(PRIOR/k/'cutouts'/f'{ident}-enlarged.png','design-cutout.png'),(PRIOR/k/'cutouts'/f'{ident}-scale-study.png','earlier-scale-study.png'),(PRIOR/k/'prompt.txt','original-prompt.txt')]:shutil.copy2(src,dst/new)
  raw=Image.open(dst/'original-board.png');raw.crop(meta['sourceBBox']).save(dst/'source-crop.png')
  cut=Image.open(dst/'design-cutout.png').convert('RGBA');h=112 if ident=='A3' else 65;size=(round(cut.width*h/cut.height),h);cut.resize(size,N).save(dst/'role-scale-study.png')
  retained.append({'id':ident,'name':name,'role':role,'status':'retained-design-candidate','sourceOriginal':str(PRIOR/k/'raw/generated.png'),'sourceSha256':sha(dst/'original-board.png'),'sourceBBox':meta['sourceBBox'],'cutoutSha256':sha(dst/'design-cutout.png'),'reviewExtent':list(size),'note':'User confirmed role assignment. Design candidate retained; scale is for visual comparison, not combat data or finalized production size.'})
 signal=ROOT/'outputs/waterway/normal-monsters-round2-20261031/selected';dst=OUT/'retained/R-selected';shutil.copytree(signal,dst,dirs_exist_ok=True)
 decisions={'retained':retained,'existingSelectedRanged':'R-selected / 신호투척수 / unchanged','rejectedPreviousFormIds':[f'{k}{i}' for k in 'ABC' for i in range(1,5) if f'{k}{i}' not in ['A3','B3']],'newScope':f'{len(SETS)*4} candidates: {len(SETS)*2} melee (fast/standard) and {len(SETS)*2} ranged alternatives; no new elites or bosses.'}
 # Exploration snapshots must not overwrite later user selections.
 dump(OUT/'choices.json',decisions);dump(OUT/'catalog.json',SETS)
 for k,items in SETS.items():
  p=OUT/k
  for sub in ['raw','cutouts','previews','references']:(p/sub).mkdir(parents=True,exist_ok=True)
  inputs=[(refs/'assets/approved-player.png','approved-player.png'),(OUT/'retained/A3/design-cutout.png','retained-A3.png'),(OUT/'retained/B3/design-cutout.png','retained-B3.png'),(refs/'C-game.png','C-game.png')]
  for src,name in inputs:shutil.copy2(src,p/'references'/name)
  prompt=COMMON+'\n\n'+'\n\n'.join(f'{k}{i+1} ({["UPPER LEFT fast melee","UPPER RIGHT standard melee","LOWER LEFT ranged 1","LOWER RIGHT ranged 2"][i]}): {m[6]}' for i,m in enumerate(items))
  (p/'prompt.txt').write_text(prompt,encoding='utf-8')
 print(json.dumps({'out':str(OUT),'retained':retained,'newCandidates':len(SETS)*4},ensure_ascii=False))

def build():
 refs=OUT/'references';font=ImageFont.truetype(shared.FONT,17);small=ImageFont.truetype(shared.FONT,14)
 player=Image.open(refs/'assets/approved-player.png').convert('RGBA');playerbox=player.getchannel('A').getbbox();player=player.crop(playerbox)
 retained={k:Image.open(OUT/'retained'/k/'role-scale-study.png').convert('RGBA') for k in ['A3','B3']}
 retained['R']=Image.open(OUT/'retained/R-selected/scale-study.png').convert('RGBA')
 choices=json.loads((OUT/'choices.json').read_text(encoding='utf-8'))
 for item in choices['retained']:assert sha(OUT/'retained'/item['id']/'design-cutout.png')==item['cutoutSha256']
 layout=json.loads((refs/'layout.json').read_text(encoding='utf-8'));scenes={s['id']:s for s in layout['scenes']};front=Image.open(refs/'assets/foreground.png').convert('RGBA')
 validation={'scope':'16 new normal candidates: 8 melee, 8 ranged. A3 retained for elite, B3 retained for heavy melee; prior R-selected preserved. No new elite/boss generation or animations.','retainedCutoutsMatchSourceHashes':True,'sets':{}}
 sections=[];allmeta={};descriptions=[]
 def screen(path,scene,placements):
  bg=Image.open(refs/f'{scene}-game.png').convert('RGBA');layer=Image.new('RGBA',bg.size);fg=layer.copy();nx=scenes[scene]['view'][0];fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
  for spr,x,foot in placements:layer.alpha_composite(spr,(x,foot-spr.height))
  la=np.array(layer);fa=np.array(fg.getchannel('A'),dtype=np.uint16);la[:,:,3]=(la[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8);mask=la[:,:,3]>0
  out=bg.copy();out.alpha_composite(Image.fromarray(la));a=np.array(bg);b=np.array(out)
  assert np.array_equal(a[~mask],b[~mask]);assert np.array_equal(a[220:288,130:198],b[220:288,130:198])
  out.convert('RGB').save(path)
  return {'scene':scene,'size':[960,540],'playerAndBackgroundOutsideInsertionUnchanged':True,'foregroundOcclusionPreserved':True}
 for k,items in SETS.items():
  raw=Image.open(OUT/k/'raw/generated.png').convert('RGBA');arr=np.array(raw);labs,n=label(arr[:,:,3]>=180);regions=[]
  for ident,b in enumerate(find_objects(labs),1):
   area=int((labs[b]==ident).sum())
   if area>1000:regions.append((ident,[b[1].start,b[0].start,b[1].stop,b[0].stop]))
  assert len(regions)==4,(k,len(regions))
  top=sorted([r for r in regions if (r[1][1]+r[1][3])/2<raw.height/2],key=lambda r:r[1][0]);bottom=sorted([r for r in regions if (r[1][1]+r[1][3])/2>=raw.height/2],key=lambda r:r[1][0]);assert len(top)==len(bottom)==2
  sprites=[];cuts=[];meta=[]
  for i,(ident,box) in enumerate(top+bottom):
   assert 0<box[0]<box[2]<raw.width and 0<box[1]<box[3]<raw.height,(k,box)
   a=np.zeros_like(arr);sel=labs==ident;a[sel,:3]=arr[sel,:3];a[sel,3]=255;cut=Image.fromarray(a).crop(tuple(box));cut.save(OUT/k/'cutouts'/f'{k}{i+1}-enlarged.png')
   m=items[i];f=min(m[4]/cut.width,m[5]/cut.height);size=(round(cut.width*f),round(cut.height*f));spr=cut.resize(size,N);spr.save(OUT/k/'cutouts'/f'{k}{i+1}-scale-study.png');sprites.append(spr);cuts.append(cut);meta.append({'id':f'{k}{i+1}','name':m[0],'role':m[1],'sourceBBox':box,'reviewExtent':list(size)})
  board=Image.new('RGBA',(1600,1040),(238,238,234,255))
  for cut,spr,(cx,ground) in zip(cuts,sprites,[(400,470),(1200,470),(400,990),(1200,990)]):
   im=cut.resize((spr.width*5,spr.height*5),N);board.alpha_composite(im,(cx-im.width//2,ground-im.height))
  board.convert('RGB').save(OUT/k/'design-board.png')
  strip=Image.new('RGBA',(960,260),(35,40,48,255));d=ImageDraw.Draw(strip);ground=210;d.line((20,ground,940,ground),fill=(86,98,108));strip.alpha_composite(player,(24,ground-player.height));d.text((24,226),'승인 플레이어',font=small,fill=(232,232,226));x=132
  for ident,spr in list(zip([f'{k}{i+1}' for i in range(4)],sprites))+[('B3 큰 근거리',retained['B3']),('채택 원거리',retained['R']),('A3 엘리트',retained['A3'])]:
   strip.alpha_composite(spr,(x,ground-spr.height));d.text((x,226),ident,font=small,fill=(232,232,226));x+=max(spr.width,70)+16
  assert x<960,(k,x);strip.convert('RGB').save(OUT/k/'previews/scale-lineup.png')
  gaps=globals().get('FOOT_GAPS',{})
  ms=screen(OUT/k/'previews/melee-960x540.png',globals().get('SCENE_MAP',{'D':'A','E':'B','F':'D','G':'E'}).get(k,'C'),[(sprites[0],224,352-gaps.get(k+'1',0)),(sprites[1],326,288-gaps.get(k+'2',0)),(retained['B3'],432,288)])
  rs=screen(OUT/k/'previews/ranged-960x540.png','C',[(retained['R'],230,352),(sprites[2],326,288-gaps.get(k+'3',0)),(sprites[3],436,288-gaps.get(k+'4',0))])
  comp=Image.new('RGB',(960,1160),(24,31,38));d=ImageDraw.Draw(comp)
  for y,text,name in [(0,'신규 근거리 2종 + 보존 B3 큰 근거리','melee'),(580,'신규 원거리 2종 + 기존 채택 원거리','ranged')]:
   d.text((16,y+8),f'{k} · {text} · 960×540 합성 미리보기',font=font,fill=(231,233,227));comp.paste(Image.open(OUT/k/'previews'/f'{name}-960x540.png'),(0,y+40))
  comp.save(OUT/k/'previews/comparison.png')
  patches=[Image.open(refs/'assets/wall.png').convert('RGBA').crop((96,128,416,344)),Image.open(refs/'assets/far.png').convert('RGBA').crop((800,300,1120,516)),Image.open(refs/'C-game.png').convert('RGBA').crop((620,0,940,216))]
  vis=Image.new('RGBA',(960,864))
  for i,spr in enumerate(sprites):
   for j,bg in enumerate(patches):
    cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height));vis.alpha_composite(cell,(j*320,i*216))
  vis.convert('RGB').save(OUT/k/'previews/visibility.png');dump(OUT/k/'metadata.json',meta);allmeta[k]=meta
  validation['sets'][k]={'silhouetteCount':4,'allFullBodiesInsideCanvas':True,'meleeScreen':ms,'rangedScreen':rs,'rawSha256':sha(OUT/k/'raw/generated.png')}
  lines=[f'{m[0]} / {m[1]} / {m[2]} / {m[3]}' for m in items];descriptions+=[f'\n{k} 신규 후보']+lines
  lis=''.join(f'<li><b>{k}{i+1}</b> {html.escape(line)}</li>' for i,line in enumerate(lines))
  sections.append(f'''<section id="{k}"><h2>탐색 {k} · 신규 4종</h2><img class="board" src="{k}/design-board.png" alt="{k} 테두리 없는 신규 후보"><p>윗줄: 접근형 · 기본 근거리 / 아랫줄: 원거리 대안 1 · 2. 검토 크기의 5배 확대입니다.</p><ul>{lis}</ul><div class="native"><img src="{k}/previews/scale-lineup.png" width="960" height="260" alt="신규 4종과 보존 3종의 실제 크기 비교"></div><h3>근거리 배치 · 신규 2종 + 보존 B3</h3><div class="native"><img src="{k}/previews/melee-960x540.png" width="960" height="540" alt="근거리 합성 미리보기"></div><h3>원거리 배치 · 신규 2종 + 기존 채택본</h3><div class="native"><img src="{k}/previews/ranged-960x540.png" width="960" height="540" alt="원거리 합성 미리보기"></div><details><summary>전 종 시인성: 어두운 벽 / 푸른 원경 / 검정 근경</summary><p>위에서부터 {k}1~{k}4. 배경은 원본 그대로 잘랐고 개체 크기는 동일합니다.</p><div class="native"><img src="{k}/previews/visibility.png" width="960" height="864" alt="시인성 비교"></div></details><p><a href="{k}/prompt.txt">프롬프트</a> · <a href="{k}/raw/generated.png">생성 원본</a></p></section>''')
  shutil.copy2(OUT/k/'prompt.txt',Path(__file__).parent/f'{globals().get("PROMPT_PREFIX","next-forms")}-{k}-prompt.txt')
 elitecheck=screen(OUT/'retained/elite-size-study-960x540.png','C',[(retained['A3'],322,288)])
 validation['retainedEliteSizeStudy']=elitecheck
 validation['player']={'canvas':[192,160],'alphaBBox':list(playerbox),'visibleExtent':list(player.size)}
 cards=''.join(f'<figure><img src="retained/{ident}/design-cutout.png" alt="{name} 보존 원본"><figcaption>{ident} {name} · {role}</figcaption></figure>' for ident,name,role in [('A3','편측거구','엘리트 후보'),('B3','쐐기보행기','큰 근거리 후보'),('R-selected','신호투척수','기존 원거리 채택본')])
 page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>추가 조형 탐색 · 신규 16종</title><style>body{margin:0;background:#17212a;color:#e6e8e3;font:16px/1.65 "Malgun Gothic",sans-serif}main{max-width:1600px;margin:auto;padding:28px}a{color:#addbea}nav{position:sticky;top:0;padding:12px 28px;background:#17212af0;border-bottom:1px solid #52606a;z-index:2}nav a{margin-right:24px}section{padding:28px 0 40px;border-top:1px solid #52606a;scroll-margin-top:60px}.board{display:block;width:100%;height:auto}.native{overflow-x:auto;max-width:100%}.native img{display:block;max-width:none;image-rendering:pixelated}ul{list-style:none;padding:0}li{padding:6px 0;border-bottom:1px solid #34434d}b{color:#e8bd80}summary{cursor:pointer;padding:16px 0}h1{font-size:28px}h2{font-size:25px}h3{font-size:19px}p{max-width:1100px}.cards{display:flex;gap:20px;flex-wrap:wrap}.cards figure{width:360px;margin:0}.cards img{display:block;width:100%;height:300px;object-fit:contain;background:#eeeeeA}.cards figcaption{padding:10px 0}</style><nav><a href="#retained">보존 선택</a><a href="#D">탐색 D</a><a href="#E">탐색 E</a><a href="#F">탐색 F</a><a href="#G">탐색 G</a></nav><main><h1>추가 조형 탐색 · 신규 일반 후보 16종</h1><p>A3 편측거구는 엘리트 후보, B3 쐐기보행기는 큰 근거리 후보로 보존했습니다. 이전 A/B/C에서 이 둘을 제외한 10종은 탈락으로 기록했고, 기존 채택 신호투척수도 유지합니다.</p><p>새 보드는 일반 근거리 8종과 원거리 대안 8종입니다. 새로운 엘리트·보스는 생성하지 않았습니다. 사람형·기계형·머리 없는 몸·비정상적인 지지 방식과 발사 부위를 넓게 비교합니다. 신규 그림은 외곽 테두리 없이 재질색 면과 제한된 명암으로 표현합니다.</p><p>디자인 시안과 합성 미리보기이며 실제 게임 캡처가 아닙니다. 생성 원본·참조·프롬프트는 보존했습니다. 플레이어는 192×160 캔버스 안 실제 그림 66×64를 기준으로 사용했습니다. 아래 화면은 960×540이며 좁은 창에서는 가로로 스크롤합니다.</p><section id="retained"><h2>보존한 세 개체</h2><div class="cards">'''+cards+'''</div><details><summary>A3 엘리트 크기 검토 보기</summary><p>기존 그림을 높이 112px로 표시한 비교용 예시입니다. 디자인 원본은 바꾸지 않았으며 최종 크기·전투 수치 확정이 아닙니다.</p><div class="native"><img src="retained/elite-size-study-960x540.png" width="960" height="540" alt="보존 엘리트 크기 검토"></div></details></section>'''+''.join(sections)+'''<p><b>우선 비교:</b> D1 스침몸 / E1 뒤집힌걸음 / F2 밀린얼굴 / D3 모은손 / E3 목구멍몸. 보존한 거구·쐐기형과 다른 움직임 축과 발사 부위를 가진 후보입니다.</p><p><a href="next-forms-16-20261031.zip">전체 ZIP</a> · <a href="choices.json">현재 보존·탈락 목록</a> · <a href="descriptions.md">한 줄 설명</a> · <a href="validation.json">합성 검증 자료</a></p></main></html>'''
 page=page.replace('next-forms-16-20261031.zip',OUT.name+'.zip')
 (OUT/'index.html').write_text(page,encoding='utf-8');(OUT/'descriptions.md').write_text('\n\n'.join(descriptions),encoding='utf-8')
 dump(OUT/'metadata.json',allmeta);dump(OUT/'validation.json',validation)
 dump(refs/'sources.json',[{'copy':str(p.relative_to(OUT)),'sha256':sha(p)} for p in sorted(refs.rglob('*')) if p.is_file() and p.name!='sources.json'])
 (OUT/'next_forms.py').write_bytes(Path(__file__).read_bytes())
 srcdir=OUT/'source';srcdir.mkdir(exist_ok=True)
 for name in ['next_forms.py','normal_round2.py','README.md','current-choices.json']:shutil.copy2(Path(__file__).parent/name,srcdir/name)
 archive=OUT/(OUT.name+'.zip');links=re.findall(r'(?:src|href)="([^"]+)"',page);missing=[x for x in links if not x.startswith('#') and x!=archive.name and not (OUT/x).is_file()];assert not missing,missing
 validation['localLinksMissing']=missing;dump(OUT/'validation.json',validation)
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(OUT.rglob('*')):
   if p.is_file() and p!=archive and '__pycache__' not in p.parts:z.write(p,p.relative_to(OUT))
 print(json.dumps({'newCandidates':len(SETS)*4,'retainedCandidates':3,'newNormalScreens':len(SETS)*2,'retainedEliteScaleStudy':1,'missingLinks':missing,'zipBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
 if sys.argv[-1]=='prepare':prepare()
 elif sys.argv[-1]=='build':build()
