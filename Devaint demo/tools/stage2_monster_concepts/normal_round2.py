"""Normal-monster comparisons with one selected ranged design held unchanged."""
from pathlib import Path
import json,shutil,hashlib,sys,html,zipfile,re
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,find_objects

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/waterway/normal-monsters-round2-20261031'
PRIOR=ROOT.parent/'stage2-waterway-reference/outputs/waterway'
REF=PRIOR/'parallax-kit-v3-20261031'
ORIGINAL=Path('C:/Users/SSAFY/.codex/generated_images/01a18def-9c96-7602-bc0f-d707e8e958ed/exec-27d2583a-96a3-4c10-9b02-88fad9327c0d.png')
N=Image.Resampling.NEAREST
FONT='C:/Windows/Fonts/malgun.ttf'
SETS={
 'A':{'title':'잔업 정비반','items':[
 ['틈새너트','근거리·빠른 접근','낮은 날개너트 몸과 짧게 벌어진 철사발','짧게 파고들어 너트턱으로 집기',62,38,'A tiny low WING-NUT scavenger: one squat rust-orange metal wingnut body with two broad pale ochre flanges and four short bent wire feet. Its split threaded front opens as a pinching jaw. A small low tool-insect silhouette, no robot head or reptile. Rust orange main body, bright warm sand upper face, muted violet wire feet.'],
 ['걸레매듭','근거리·기본 공격','자주색 천매듭 몸·밝은 걸레머리·갈고리봉','긴 천팔로 짧은 갈고리봉 휘두르기',100,64,'A stooped living mop-knot worker: broad DUSTY PURPLE knotted rag torso, a flat folded CREAM rag crest instead of a human head, two medium-length hanging cloth arms, one short COPPER hooked maintenance rod held forward and two planted thick rag feet. Compact nonhuman cloth character. One dark fold under the rag crest, no glowing eyes, no player hood. Clearly different from a boot or sack boss.'],
 ['벽돌지게','근거리·중압박','넓은 벽돌 가슴과 짧은 지게다리','벽돌팔로 밀고 무겁게 내려치기',108,78,'A broad squat animated maintenance HANDCART / brick hod carrier, two huge pale ochre masonry blocks making a rectangular chest in a dark wooden hod frame, two very short stout bracket-shaped feet, one broad chipped IVORY brick fist projected forward and one small balancing handle behind. Heavy rectangular low silhouette, only two big bricks, no humanoid robot face, no helmet or eyes on every component. Warm stone cream / muted amber body and dusty burgundy wooden frame.'],
 ['물통부리','원거리·직사','밝은 청록색 물통과 수평으로 향한 긴 부리관','몸을 숙여 좁고 긴 물줄기 분사',94,64,'A standing WATER-CAN beak creature: one broad PALE TURQUOISE enamel watering-can body, a single long warm BRASS spout-beak pointing HORIZONTALLY left, one large simple handle arcing behind, two short cream reed-root legs under the tank, small folded slate cloth brace at its back. The tank IS the creature, no person holding it, no attached head. Preparing a straight forward pressure shot, no water effect shown. A broad light blue-green tank and narrow horizontal attack tube.']
 ]},
 'B':{'title':'배수 틈의 자생종','items':[
 ['잎가위벌레','근거리·빠른 접근','연두색 겹잎 가위턱과 낮은 뿌리몸','낮게 튀어들며 겹잎으로 집기',62,38,'A low tiny botanical clipper bug: two broad folded LIGHT LIME leaves forming forward scissor-jaws, one small warm olive seed body and four short angular PALE root legs. A leaf-and-seed creature with an obvious snapping direction, no animal skull, no dinosaur, no long neck or humanoid torso. Few very large leaf planes.'],
 ['버섯등지기','근거리·기본 공격','기울어진 연보라 갓과 긴 뿌리팔·씨방몽둥이','갓 아래로 몸을 접고 씨방몽둥이 치기',100,68,'A stooped mushroom porter: a broad DUSTY LILAC cap pitched forward as its head and shoulders, a CREAM folded fungus membrane torso, two long ochre root arms and two thick short root legs; one oval amber SEED-POD club held horizontally forward. No human face under cap, only one dark gill notch. Compact hunched upright silhouette, large cap / body / club masses, not a realistic mushroom painting.'],
 ['연뿌리주먹','근거리·중압박','넓은 상아색 연근몸과 둔중한 적갈색 뿌리주먹','낮게 버티고 큰 뿌리주먹으로 찍기',108,76,'A squat broad LOTUS-ROOT bruiser: one large pale buff lobed lotus-root bulb body with exactly three big dark cross-section holes, one enormous rust-terracotta knotted root fist hanging forward, two very short fat supporting roots and one muted sage leaf folded across the back. No head, no humanoid armor, no green dinosaur. Broad chalk-and-warm-root mass, asymmetric low heavy weight.'],
 ['종자망울','원거리·직사','금빛 씨방과 길게 열린 앞쪽 꼬투리','꼬투리를 조여 씨앗을 수평 발사',90,66,'An upright seed-pod turret plant on two short root feet, one broad warm OCHRE seed capsule nested in PALE SAGE bracts, a long split pod mouth extended HORIZONTALLY left like opening seed lips and a big DUSTY ROSE folded petal bracing behind. Fully botanical, no pot or machinery, no eyes. Clearly straight forward seed firing, no airborne seeds or glow. Big warm capsule separated from cool leaf base.']
 ]},
 'C':{'title':'도시 밑 잔심부름꾼','items':[
 ['열쇠참새','근거리·빠른 접근','밝은 가슴·청회색 날개·짧은 황동 열쇠부리','짧게 뛰어들며 부리로 쪼기',62,40,'A compact round hopping KEY SPARROW: broad cream chest, dusty SLATE-BLUE folded wing, one short BRASS key-bit beak pointing left and two small quick ochre bird feet. One short squared key-tail. Small feathered bird, not a reptile, no long neck, no metal armor suit, no huge wings. Its one key-beak silhouette is the tool motif.'],
 ['매듭검문꾼','근거리·기본 공격','밝은 밧줄 매듭머리와 청보라 몸·검사갈고리','한 걸음 전진하며 긴 팔로 갈고리 치기',100,64,'A narrow rope-knot checkpoint errand creature: a large PALE LINEN rope knot for its head, muted BLUE-PURPLE work vest body, two long looped rope arms, short dark wrapped feet and one simple ochre inspection hook held forward. No conventional human face, one dark inner rope gap. Slim standing melee stance with long arm reach, unlike the selected short round orange signal thrower. No hood and no knight armor.'],
 ['모래포대','근거리·중압박','넓은 모래색 포대와 붉은 판자팔·짧은 발','판자팔로 밀어내고 포대몸으로 압박',108,76,'A broad low living SANDBAG: pale SAND BEIGE stuffed sack belly with a big cream stitched folded front plane, one broad dull RED wooden plank forearm held forward as a shove surface, one short knotted cloth arm behind, two very short DUSTY VIOLET padded feet. No separate animal head, a deep horizontal folded seam is its face. A small ordinary heavy enemy, not a giant boss or elite, only chest-high to a child in display scale. No fine stitching texture.'],
 ['우편딱따구리','원거리·직사','벽돌색 상자몸·긴 종이부리·청색 접지날개','부리를 열어 접힌 종이를 직선 발사',90,64,'A MAIL WOODPECKER creature: compact faded BRICK-RED box-shaped bird torso, small pale CERAMIC bird head with a long open folded-IVORY-paper beak aimed HORIZONTALLY left, one large dusty BLUE folded ticket wing against its back and two thin warm brass claw feet. Standing braced ready to shoot folded paper from its beak; no projectile outside the mouth. An object-bird hybrid with box body and bird posture, not the selected thrower, not a humanoid gunman.']
 ]}
}
COMMON='''Use case: stylized-concept. Static NORMAL-MONSTER design comparison for a crisp 2D side-scrolling pixel action game in an inhabited city's underground waterway.
Input 1: the selected orange-apron signal thrower is a STYLE AND GAME-FAMILY REFERENCE ONLY. It has already been selected. DO NOT redraw, vary, include or replace that character in this output. Input 2: approved player, scale and simplification reference ONLY, do not redraw it. Its actual drawing is 66x64 within a 192x160 transparent canvas. Input 3: unchanged sewer game environment, hue and contrast reference only; do not draw a background.
Generate exactly FOUR NEW ordinary monsters, no elite or boss. Upper row: small fast melee at x25%, standard melee at x75%. Lower row: heavy melee at x25%, straight-shooting ranged at x75%. Large empty margin between the four isolated silhouettes, every character fully inside its own quadrant. The three melee must differ in body shape, weight and attack reach; the new ranged must expose a clear HORIZONTAL launch organ/weapon, contrasting with the already selected OVERHEAD canister thrower.
Strict left-facing SIDE VIEW for a side-scrolling game, planted feet except small hopping pose, full body, broad readable shapes before small detail. Tiny/standard/heavy ordinary enemies roughly 38/64/76 pixels high when placed in game; they must not look like elites or bosses. Display enlarged crisp pixel clusters on a landscape 1536x1024 transparent board. Large sharp pixel blocks, 3 flat value steps per material, minimal inner texture, no gradients or realistic material rendering. Local material-shadow edge colors, no heavy continuous black outline, no neon rim or glow. Match the selected reference's compact simple fantasy personality, without putting its face and body on every object.
Use DIFFERENT material-appropriate main colors for all four monsters; they belong together through pixel density, silhouette clarity and limited shading, NOT a single cream/ochre/red uniform. Broad medium-light body planes must separate from dark grey-green walls, blue far arches and black foreground. No dinosaur/reptile body plans, no realistic animal rendering, no generic fantasy warriors. Objects, plants and animals are all welcome with unusual original anatomy and civic-maintenance character.
Transparent empty space only: no scenery, colored atmosphere, glows, floor or cast shadows, labels, text, panel frames or UI. No projectiles or effects floating outside bodies. This is a static concept board, NOT an animation or sprite sheet.
'''

def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def prepare():
 OUT.mkdir(parents=True,exist_ok=True)
 selected=OUT/'selected';selected.mkdir(exist_ok=True)
 shutil.copy2(ORIGINAL,selected/'original-board.png')
 raw=Image.open(ORIGINAL).convert('RGBA');box=(401,591,656,921)
 raw.crop(box).save(selected/'source-crop.png')
 arr=np.array(raw);labs,n=label(arr[:,:,3]>=180)
 # Identify the selected fifth silhouette by the clicked board's known crop center.
 selid=int(labs[760,520]);assert selid>0
 pixels=labs==selid
 clean=np.zeros_like(arr);clean[pixels,:3]=arr[pixels,:3];clean[pixels,3]=255
 cut=Image.fromarray(clean).crop(box);cut.save(selected/'design-cutout.png')
 f=62/cut.height;size=(round(cut.width*f),62)
 cut.resize(size,N).save(selected/'scale-study.png')
 prev=Image.new('RGBA',(360,400),(238,238,234,255));prev.alpha_composite(cut,((360-cut.width)//2,30));prev.convert('RGB').save(selected/'selected-design.png')
 adoption={'id':'R-selected','name':'신호투척수','status':'design-selected','selection':'User selected the fifth creature of the attached original board.','source':str(ORIGINAL),'sourceSha256':sha(ORIGINAL),'sourceBBox':list(box),'role':'원거리·곡사','identity':'주황색 앞치마·크림색 머리 덮개·보라색 팔·황토색 신호통','attackCharacter':'팔을 뒤로 젖힌 뒤 신호통 투척','scope':'Visual design selected only; no animation, spritesheet, Unity integration or finalized combat data.','reviewExtent':list(size),'originalRGBPreserved':True,'derivedAlpha':'Only the compositing copy uses the original silhouette at alpha >=180; the full original and exact rectangular source crop are retained.'}
 dump(selected/'adoption.json',adoption)
 refs=OUT/'references';refs.mkdir(exist_ok=True)
 sources=[]
 for name in ['README.md','A-game.png','B-game.png','C-game.png','D-game.png','E-game.png','assets/approved-player.png','assets/far.png','assets/wall.png','assets/foreground.png','layout.json']:
  src=REF/name;dst=refs/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
  sources.append({'source':str(src),'copy':str(dst.relative_to(OUT)),'sha256':sha(src)})
 dump(refs/'sources.json',sources)
 priorprompt=PRIOR/'monster-concepts-20261031/C/prompt.txt'
 if priorprompt.exists():shutil.copy2(priorprompt,selected/'original-prompt.txt')
 for k,s in SETS.items():
  p=OUT/k;p.mkdir(exist_ok=True)
  for sub in ['raw','cutouts','previews','references']:(p/sub).mkdir(exist_ok=True)
  for src,name in [(selected/'design-cutout.png','selected-signal-thrower.png'),(refs/'assets/approved-player.png','approved-player.png'),(refs/'C-game.png','C-game.png')]:shutil.copy2(src,p/'references'/name)
  prompt=COMMON+'\nDirection '+k+': '+s['title']+'\n\n'+'\n\n'.join(f'{k}{i+1} ({["UPPER LEFT fast melee","UPPER RIGHT standard melee","LOWER LEFT heavy melee","LOWER RIGHT straight ranged"][i]}): {m[6]}' for i,m in enumerate(s['items']))
  (p/'prompt.txt').write_text(prompt,encoding='utf-8')
 dump(OUT/'catalog.json',SETS)
 # Selected design status belongs to source control; binary art follows the separate bundle policy.
 dump(Path(__file__).parent/'selected-ranged.json',adoption)
 print(json.dumps({'out':str(OUT),'selected':adoption,'newDesigns':12},ensure_ascii=False))

def build():
 dump(OUT/'catalog.json',SETS)
 refs=OUT/'references';metadata={};selected=Image.open(OUT/'selected/design-cutout.png').convert('RGBA')
 selectednative=Image.open(OUT/'selected/scale-study.png').convert('RGBA')
 selectedhash=sha(OUT/'selected/design-cutout.png')
 player=Image.open(refs/'assets/approved-player.png').convert('RGBA');playerbox=player.getchannel('A').getbbox();player=player.crop(playerbox)
 layout=json.loads((refs/'layout.json').read_text(encoding='utf-8'));scenes={s['id']:s for s in layout['scenes']}
 front=Image.open(refs/'assets/foreground.png').convert('RGBA')
 font=ImageFont.truetype(FONT,18);small=ImageFont.truetype(FONT,15)
 sections=[];descriptions=['신호투척수 / 원거리·채택 / 주황 앞치마·보라 팔·황토 신호통 / 머리 위에서 신호통 곡사 투척']
 validations={'scope':'Normal monsters only. One selected ranged visual design plus twelve new candidates. No elite or boss generation, motion, sprite sheet or Unity integration.','selectedSourceOriginalHashMatches':sha(ORIGINAL)==sha(OUT/'selected/original-board.png'),'sets':{}}
 for k,s in SETS.items():
  raw=Image.open(OUT/k/'raw/generated.png').convert('RGBA');arr=np.array(raw);labs,n=label(arr[:,:,3]>=180)
  regions=[]
  for i,b in enumerate(find_objects(labs),1):
   area=int((labs[b]==i).sum())
   if area>1000:regions.append((i,[b[1].start,b[0].start,b[1].stop,b[0].stop],area))
  assert len(regions)==4
  top=sorted([r for r in regions if (r[1][1]+r[1][3])/2<raw.height/2],key=lambda r:r[1][0]);bottom=sorted([r for r in regions if (r[1][1]+r[1][3])/2>=raw.height/2],key=lambda r:r[1][0]);assert len(top)==len(bottom)==2
  cuts=[];native=[];records=[]
  for j,(ident,box,area) in enumerate(top+bottom):
   sel=labs==ident;a=np.zeros_like(arr);a[sel,:3]=arr[sel,:3];a[sel,3]=255
   cut=Image.fromarray(a).crop(tuple(box));cut.save(OUT/k/'cutouts'/f'{k}{j+1}-enlarged.png')
   item=s['items'][j];factor=min(item[4]/cut.width,item[5]/cut.height);size=(round(cut.width*factor),round(cut.height*factor))
   spr=cut.resize(size,N);spr.save(OUT/k/'cutouts'/f'{k}{j+1}-scale-study.png')
   cuts.append(cut);native.append(spr);records.append({'id':f'{k}{j+1}','sourceBBox':box,'reviewExtent':list(size),'role':item[1]})
  board=Image.new('RGBA',(1600,940),(238,238,234,255))
  # All figures share a 4x display factor from their proposed visible art extents.
  for j,((cx,ground),cut,spr) in enumerate(zip([(267,370),(800,370),(1333,370),(530,830),(1070,830)],cuts+[selected],native+[selectednative])):
   show=cut.resize((spr.width*4,spr.height*4),N);board.alpha_composite(show,(cx-show.width//2,ground-show.height))
  board.convert('RGB').save(OUT/k/'design-board.png')
  lineup=Image.new('RGBA',(960,230),(35,40,48,255));d=ImageDraw.Draw(lineup);ground=184
  d.line((20,ground,940,ground),fill=(82,94,104));lineup.alpha_composite(player,(24,ground-player.height));d.text((24,199),'승인 플레이어',font=small,fill=(230,231,225));x=165
  for ident,spr in list(zip([f'{k}{i+1}' for i in range(4)],native))+[('채택 원거리',selectednative)]:
   lineup.alpha_composite(spr,(x,ground-spr.height));d.text((x,199),ident,font=small,fill=(230,231,225));x+=max(spr.width,85)+24
  assert x<960;lineup.convert('RGB').save(OUT/k/'previews/scale-lineup.png')
  screenmeta=[]
  for name,scene,placements in [('melee',{'A':'A','B':'B','C':'E'}[k],[(0,224,352),(1,322,288),(2,432,288)]),('ranged','C',[(4,230,352),(3,358,288)])]:
   base=Image.open(refs/f'{scene}-game.png').convert('RGBA');layer=Image.new('RGBA',(960,540));fg=layer.copy();nx=scenes[scene]['view'][0];fg.alpha_composite(front,(490-round((nx-480)*.4375),-100))
   for index,x,foot in placements:
    sp=(native+[selectednative])[index];layer.alpha_composite(sp,(x,foot-sp.height))
   la=np.array(layer);fa=np.array(fg.getchannel('A'),dtype=np.uint16);la[:,:,3]=(la[:,:,3].astype(np.uint16)*(255-fa)//255).astype(np.uint8);mask=la[:,:,3]>0
   out=base.copy();out.alpha_composite(Image.fromarray(la));ba=np.array(base);oa=np.array(out)
   assert np.array_equal(ba[~mask],oa[~mask]);assert np.array_equal(ba[220:288,130:198],oa[220:288,130:198])
   out.convert('RGB').save(OUT/k/'previews'/f'{name}-960x540.png')
   screenmeta.append({'file':f'{name}-960x540.png','scene':scene,'playerAndBackgroundOutsideInsertedArtUnchanged':True,'foregroundOcclusionPreserved':True})
  compare=Image.new('RGB',(960,1160),(24,31,38));d=ImageDraw.Draw(compare)
  for y,labeltext,name in [(0,'근거리 3종','melee'),(580,'추가 직사 원거리 + 채택 곡사 원거리','ranged')]:
   d.text((16,y+8),f'{k} · {labeltext} · 960×540 합성 미리보기',font=font,fill=(230,231,225));compare.paste(Image.open(OUT/k/'previews'/f'{name}-960x540.png'),(0,y+40))
  compare.save(OUT/k/'previews/comparison.png')
  bgpatches=[Image.open(refs/'assets/wall.png').convert('RGBA').crop((96,128,416,344)),Image.open(refs/'assets/far.png').convert('RGBA').crop((800,300,1120,516)),Image.open(refs/'C-game.png').convert('RGBA').crop((620,0,940,216))]
  visibility=Image.new('RGBA',(960,1080))
  for i,spr in enumerate(native+[selectednative]):
   for j,bg in enumerate(bgpatches):
    cell=bg.copy();cell.alpha_composite(spr,((320-spr.width)//2,208-spr.height));visibility.alpha_composite(cell,(j*320,i*216))
  visibility.convert('RGB').save(OUT/k/'previews/visibility.png')
  metadata[k]={'items':records,'selectedReviewExtent':list(selectednative.size),'screens':screenmeta};dump(OUT/k/'metadata.json',metadata[k])
  validations['sets'][k]={'newSilhouetteCount':4,'selectedCopiedNotRegenerated':True,'screens':screenmeta}
  lines=[f'{m[0]} / {m[1]} / {m[2]} / {m[3]}' for m in s['items']];descriptions += [f'\n{k} · {s["title"]}']+lines
  descriptionshtml=''.join(f'<li><b>{k}{i+1}</b> {html.escape(line)}</li>' for i,line in enumerate(lines))
  sections.append(f'''<section id="{k}"><h2>{k} · {s['title']}</h2><img class="board" src="{k}/design-board.png" alt="{k} 일반 몬스터 비교 보드"><p>위: 근거리 3종 / 아래 왼쪽: 추가 직사 원거리 / 아래 오른쪽: 채택 신호투척수 원본 사본. 모든 그림은 검토 크기의 4배 확대입니다.</p><ul>{descriptionshtml}<li><b>공통</b> {html.escape(descriptions[0])}</li></ul><div class="native"><img src="{k}/previews/scale-lineup.png" width="960" height="230" alt="전 종과 승인 플레이어 실제 크기 비교"></div><h3>근거리 배치</h3><div class="native"><img src="{k}/previews/melee-960x540.png" width="960" height="540" alt="근거리 3종 합성"></div><h3>원거리 배치</h3><div class="native"><img src="{k}/previews/ranged-960x540.png" width="960" height="540" alt="채택 원거리와 추가 원거리 합성"></div><details><summary>어두운 벽 / 푸른 원경 / 검정 근경 위 비교</summary><p>위에서부터 {k}1~{k}4, 채택 신호투척수. 같은 개체를 같은 크기로 배치했습니다.</p><div class="native"><img src="{k}/previews/visibility.png" width="960" height="1080" alt="시인성 비교"></div></details><p><a href="{k}/prompt.txt">프롬프트</a> · <a href="{k}/raw/generated.png">생성 원본</a></p></section>''')
  # Prompt used by imagegen is preserved beside its source catalog in Git.
  shutil.copy2(OUT/k/'prompt.txt',Path(__file__).parent/f'{globals().get("SOURCE_PROMPT_PREFIX","normal-round2")}-{k}-prompt.txt')
 assert sha(OUT/'selected/design-cutout.png')==selectedhash
 validations['selectedCompositingCopyHashUnchanged']=True;validations['player']={'canvas':[192,160],'alphaBBox':list(playerbox),'visibleExtent':list(player.size),'sourceHashMatches':sha(refs/'assets/approved-player.png')==sha(REF/'assets/approved-player.png')}
 page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>지하수로 일반 몬스터 2차 비교</title><style>body{margin:0;background:#17212a;color:#e6e8e3;font:16px/1.65 "Malgun Gothic",sans-serif}main{max-width:1600px;margin:auto;padding:28px}a{color:#addbea}nav{position:sticky;top:0;padding:12px 28px;background:#17212af0;border-bottom:1px solid #52606a;z-index:2}nav a{margin-right:24px}section{padding:28px 0 40px;border-top:1px solid #52606a;scroll-margin-top:60px}.board{display:block;width:100%;max-width:1600px;height:auto}.native{overflow-x:auto;max-width:100%}.native img{display:block;max-width:none;image-rendering:pixelated}ul{list-style:none;padding:0}li{padding:6px 0;border-bottom:1px solid #34434d}b{color:#e8bd80}summary{cursor:pointer;padding:16px 0}h1{font-size:28px}h2{font-size:25px}h3{font-size:19px}.selected{display:flex;align-items:center;gap:28px;flex-wrap:wrap}.selected img{width:255px;height:auto}p{max-width:1100px}</style><nav><a href="#selected">채택 원거리</a><a href="#A">A 잔업 정비반</a><a href="#B">B 자생종</a><a href="#C">C 잔심부름꾼</a></nav><main><h1>지하수로 일반 몬스터 · 2차 디자인 비교</h1><p>채택 원거리 1종을 고정하고, 근거리 3종 + 추가 직사 원거리 1종을 A/B/C로 새로 비교합니다. 신규 디자인은 12종이며, 각 보드에 채택한 신호투척수가 같은 모습으로 함께 들어갑니다.</p><p>이번 범위는 근거리·원거리 정지 디자인입니다. 엘리트·보스는 이후 단계입니다. 그림은 디자인 시안과 합성 미리보기이며 실제 게임 캡처가 아닙니다. 기본 image_gen 도구를 사용했고 원본·참조·프롬프트를 보존했습니다.</p><div id="selected" class="selected"><img src="selected/selected-design.png" alt="채택된 다섯 번째 신호투척수"><div><h2>채택 · 신호투척수</h2><p>원거리 / 주황 앞치마·크림색 머리 덮개·보라 팔·황토 신호통 / 머리 위에서 신호통 투척</p><p>첨부한 기존 보드의 5번째 개체입니다. 다시 생성하지 않았으며, 원본을 보존하고 합성용 사본만 분리했습니다. 디자인 채택 상태이며 공격 수치나 모션·Unity 연결의 확정은 아닙니다.</p></div></div><p>크기 기준: 승인 플레이어 캔버스 192×160 / 실제 그림 66×64. 아래 합성 화면은 960×540 원본 배경을 그대로 사용하며 좁은 창에서는 가로로 스크롤합니다. 모든 후보는 일반 몬스터 크기로 검토합니다.</p>'''+''.join(sections)+'''<p><b>우선 비교 추천:</b> C1 열쇠참새 + A2 걸레매듭 + B3 연뿌리주먹 + A4 물통부리 + 채택 신호투척수. 작은 동물형·천 사물형·뿌리 식물형을 섞고, 직사와 곡사도 구분됩니다.</p><p><a href="normal-monsters-round2-20261031.zip">전체 ZIP</a> · <a href="descriptions.md">한 줄 설명</a> · <a href="validation.json">합성 검증 자료</a></p></main></html>'''
 page=page.replace('normal-monsters-round2-20261031.zip',OUT.name+'.zip')
 (OUT/'index.html').write_text(page,encoding='utf-8');(OUT/'descriptions.md').write_text('\n\n'.join(descriptions),encoding='utf-8')
 dump(OUT/'validation.json',validations)
 archive=OUT/(OUT.name+'.zip')
 links=re.findall(r'(?:src|href)="([^"]+)"',page);missing=[x for x in links if not x.startswith('#') and x!=archive.name and not (OUT/x).is_file()];assert not missing
 validations['localLinksMissing']=missing;dump(OUT/'validation.json',validations)
 (OUT/'normal_round2.py').write_bytes(Path(__file__).read_bytes())
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(OUT.rglob('*')):
   if path.is_file() and path!=archive and '__pycache__' not in path.parts:z.write(path,path.relative_to(OUT))
 print(json.dumps({'newDesignCount':12,'selectedDesignCount':1,'normalReviewBoards':3,'screens':6,'selectedCopyUnchanged':True,'missingLinks':missing,'zipBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
 if sys.argv[-1]=='prepare':prepare()
 elif sys.argv[-1]=='build':build()
