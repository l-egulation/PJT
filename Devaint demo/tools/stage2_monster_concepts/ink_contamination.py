"""Neglected sewer ecology invaded by ink; normal monsters only."""
from pathlib import Path
import sys,zipfile,json
import normal_round2 as base

base.OUT=base.ROOT/'outputs/waterway/ink-contaminated-normals-20261031'
base.SOURCE_PROMPT_PREFIX='ink-contamination'
base.SETS={
 'A':{'title':'퇴적물과 젖은 잔해','items':[
 ['먹묻은쥐','근거리·빠른 접근','회백색 굽은 등과 잉크에 굳은 뒷발','낮게 파고들어 물고 짧게 긁기',64,38,'A small hunched SEWER RAT corrupted by ink. Compact rounded rodent rump, blunt rat muzzle, cream-grey broad flank and cool slate-grey back, one torn ear and a thin curved tail. Its rear paws and one flank seam are stuck together by thick near-black violet ink, twisting its running crouch asymmetrically. Four short sharp legs, one eye obscured by an irregular ink scab. Keep recognizable rat anatomy, no reptile, dinosaur, back spikes, outfit, smile or cute round mascot eyes. Broad pale grey fur remains 75 percent of the surface.'],
 ['젖은먹걸레','근거리·기본 공격','연보라색 젖은 천덩이와 길게 굳은 먹팔','늘어진 먹팔로 앞쪽을 훑기',100,64,'A discarded wet FILTER RAG invaded by ink: a hunched dense DUSTY LILAC cloth mass with broad PALE BLUE-GREY folded planes, two short dragging cloth feet and one unnaturally long charcoal-violet ink-caked forelimb reaching low forward, its end hardened into a blunt hook. No humanoid head or cap, no clothes worn as an outfit, no carried tool. One deep wet seam in the upper mass implies a mouth; pale cloth stays about 75 percent, ink concentrates in the seam, one elbow and the thick hooked hand. Unsettling sagging tension, not a friendly worker.'],
 ['석회퇴적체','근거리·중압박','상아색 석회껍질과 새어 나오는 먹관절','낮게 밀고 들어와 굳은 퇴적팔로 치기',108,76,'A broad low LIME-SCALE SEDIMENT creature around a broken drain grate fragment. Three large warm IVORY crust plates form a heavy domed body, three short ink-rooted stump feet and one broad crust-covered forelimb pushed in front. The old slate drain fragment is partly embedded inside, not worn like armor. Charcoal-purple ink seeps only from the major shell joints and one sagging underbelly fold, binding rubble into a living mass. No human chest or face, no arms holding tools, no glowing eye. Broad bone-white mineral surface with muted ochre shadow planes, dark ink under 25 percent.'],
 ['막힌토관','원거리·직사','금 간 적갈색 토관과 부푼 먹주머니','관 안쪽 먹주머니를 조여 정면 분사',94,64,'A short BROKEN CERAMIC DRAIN ELBOW colonized from inside by an ink organism. A broad muted TERRACOTTA shard body with a lighter warm clay upper face, a pale slate swollen membrane sac visible under the broken back, two flattened ochre sludge feet. The open pipe lip aims HORIZONTALLY left and is irregularly crusted with charcoal-violet ink, preparing to spit forward. No separate animal head, human limbs or gun trigger; not a humanoid pipe soldier or full background pipe. Clay is dominant, the black slit and ink sac occupy only local connected regions. No projectile.']
 ]},
 'B':{'title':'오염된 뿌리와 균사','items':[
 ['먹풀뿌리','근거리·빠른 접근','마른 황토색 물풀과 먹에 엉킨 뿌리발','뿌리를 튕겨 낮게 달려들기',64,38,'A low skittering WATERWEED ROOT tuft poisoned by ink: three broad bent PALE STRAW-OCHRE reed blades over a small dusty sage seed knot, four short charcoal-violet root feet tangled into an uneven spring crouch. Two reed tips form a hooked forward grasp. No animal face, no eyes, no clean symmetrical plant mascot. Large light dry leaves contrast the dark corrupted roots; ink is local to root knots and one cracked stalk. Simple broad planes and sparse botanical structure.'],
 ['물든주름갓','근거리·기본 공격','찢어진 연보라 갓과 먹이 번진 긴 주름팔','몸을 접었다가 주름팔로 길게 치기',100,66,'An ink-infected SEWER FUNGUS: a broad floppy DUSTY LILAC mushroom cap split on one side and drooping over a pale cream folded membrane stem, two short root feet, one long ragged gill-arm reaching forward. The gill underside and one bent arm joint are clogged with charcoal-violet ink and a few thick connected clots, causing an asymmetric tense posture. No human face, hat worn on a person, carried club, outfit or smile. Pale cap and stem remain broad readable masses, ink less than 25 percent.'],
 ['부푼먹연근','근거리·중압박','부푼 뼈색 연근몸과 먹으로 막힌 큰 구멍','무거운 뿌리주먹으로 내려찍기',108,76,'A waterlogged swollen LOTUS ROOT mass corrupted by ink. Broad pale BUFF / BONE body tilted low, three large unequal root cross-section cavities packed with near-black violet ink, one massive dusty RUST-PINK root fist held low forward and two very short thick supporting roots. One desaturated sage leaf is trapped against the back. The holes are natural clogged cavities, NOT a cute face or eye trio. Ink leaks between two big root lobes, but pale root flesh dominates. Heavy uneven planted posture, not humanoid armor or a boss.'],
 ['먹씨앗주머니','원거리·직사','팽팽한 황토색 씨방과 검게 벌어진 꼬투리 입','먹에 부푼 씨방을 조여 정면으로 뱉기',90,66,'A diseased aquatic SEED POD inflated by ink: a broad muted GOLDEN-OCHRE capsule with a lighter straw upper plane, uneven split pod lips held HORIZONTALLY left, deep charcoal-purple interior visible as a single forward firing cavity, two pale cool SAGE bract legs and one dusty MAUVE folded rear petal. The stalk is pushed backward in an unnatural firing brace, local black ink seams distort the pod edges. No eyes, equipment, pot, emitted effects or projectile. Botanical silhouette, not bird or dinosaur.']
 ]},
 'C':{'title':'잠식된 수로 생물','items':[
 ['껍질벼룩','근거리·빠른 접근','밝은 청회색 마디껍질과 뒤틀린 먹뒷다리','굽힌 뒷다리로 튀어들어 집기',64,38,'A compact SEWER WATER FLEA / amphipod, low left-facing insect-like side profile. Three broad PALE BLUE-GREY chitin plates form a rounded folded body, soft muted peach underside, one large sharply folded jumping rear leg and three short front gripping legs. Charcoal-violet ink clots one antenna base and the jumping knee, twisting the crouch. No reptile face, long neck, winged mascot, clothes or glowing eyes. Light shell is at least 75 percent and much lighter than sewer walls.'],
 ['먹실수염','근거리·기본 공격','탁한 살구색 새우몸과 갈고리로 굳은 먹수염','긴 먹수염을 앞으로 뻗어 훑기',100,64,'An ink-corrupted freshwater SHRIMP: upright arched pale DUSTY SALMON chitin body with a broad cream gill plane, four small planted supporting legs and one long antenna fused into a thick charcoal-violet forward HOOK. The other antenna remains short and broken. One cloudy dark seam under the head, no conventional cute eyes or expression. Warm pale shrimp armor dominates; dark ink is localized at the long hook and two warped joints. Animal appendage attack, no human arms, tool or humanoid outfit. Clearly arthropod, no dinosaur.'],
 ['철망등게','근거리·중압박','회백색 껍질에 박힌 녹슨 철망과 굳은 먹집게','몸을 낮춰 밀고 무거운 집게로 치기',108,76,'A broad heavy SEWER CRAB with a pale CHALK BLUE-GREY shell, warm faded CORAL legs and one oversized forward claw partly petrified by thick charcoal-violet ink. A rusty wire drain-screen fragment with just three LARGE square openings is embedded across the upper shell, not a neat helmet. The shell is uneven and low, the ink-coated claw drags before striking, natural legs planted wide. No smiling face, cartoon eye stalks, carried shield or pipes. Broad pale shell and warm limbs remain 75 percent; ink mainly on claw and one under-shell joint.'],
 ['부푼먹조개','원거리·직사','연회색 석회껍질과 옆으로 열린 먹수관','살집을 조여 수평으로 먹덩이 분사',96,64,'An ink-infected SEWER BIVALVE: one broad pale GREY-LAVENDER calcified shell tipped slightly backward, broken warm ivory shell lip, muted ROSE soft siphon extended HORIZONTALLY left from the opening, a swollen charcoal-violet ink sac visible only under the hinge, and two squat pale fleshy bracing flanges underneath. The siphon is open and taut in firing preparation. No eyes, humanoid head, mechanical cannon, floating bubbles or projectile. A clam struggling around an internal ink parasite, light shell dominant, dark ink local.']
 ]}
}
# Korean labels are names for visual comparison, not fictional factions or jobs.
base.COMMON='''Use case: stylized-concept. FOUR static ordinary-monster designs for a 2D side-scrolling pixel action game.
SETTING AND MOOD: a NEGLECTED sewer beneath an INK CITY. Maintenance has lapsed; damp residue, lime crust, tangled roots and small sewer animals are slowly colonized by bad living ink. Quiet creeping unease, corrupted wet anatomy and unnatural held tension. These must plausibly inhabit old urban water conduits.
This is NOT a band of city workers, errand runners, checkpoint soldiers, happy object mascots or characters assigned human jobs. No job clothing, hats, backpacks, keys, mailboxes, handheld tools or cute black faces with yellow eyes. Do not simply put friendly eyes and legs on an object. Recognizable sewer material/ecology is warped from WITHIN by ink. No dinosaur/reptile body plan and no gore.
INK DESIGN: near-black violet ink is concentrated in cracks, clogged openings, one hardened appendage, bent joints and connected heavy clots. Change anatomy and posture around those locations. The rest of the creature's broad natural material planes remain visible. Do not paint the whole creature black or dark green, do not outline it with ink, and do not rely on eye glow. At least 70-75 percent of the visible body should retain its own light or medium-light material colors. No floating ink effects, clouds, external puddles, smoky aura or thin dripping decoration everywhere.
INPUTS: Image 1 selected orange-apron ranged character is an EXISTING FIXED DESIGN, provided only for pixel simplification and size. It is NOT an ink-corruption edit target: exclude it completely from generation and do not copy its outfit, cap, face or body. Image 2 approved player is scale and detail-density reference only: its actual drawing is 66x64 within a 192x160 canvas. Do not redraw it. Image 3 unchanged environment is hue and contrast reference only; do not generate scenery.
COMPOSITION: exactly FOUR separate full-body creatures on a landscape 1536x1024 transparent canvas. Strict left-facing SIDE PROFILE suitable for a side-scroller. UPPER LEFT small fast melee; UPPER RIGHT standard melee; LOWER LEFT squat heavy ordinary melee; LOWER RIGHT ranged with a clear HORIZONTAL launch opening. No elites or bosses. No repeated views. Generous transparent margins between all figures, every silhouette fully visible, no overlap or detached props.
MEDIUM: crisp SIMPLE PIXEL ART with large flat pixel clusters and only 3 value steps per material. Few broad head/trunk/limb/attack-organ masses. Target ordinary enemy art about 30-76 pixels high in game, then display enlarged. No smooth painting, realistic animal texture, 3D, soft gradients, dithering, heavy continuous black outlines, neon rim, gore or visual effects. Material-shadow colors at edges. The four bodies must have DIFFERENT material-appropriate colors, not a uniform cream/rust palette, and must read against dark green-grey wall, blue far arches and black foreground.
Truly transparent empty space. No background, shadows, floor, labels, numbers, text, frames or UI. Not an animation or sprite sheet.
'''

def prepare():
 base.prepare()

def build():
 out=base.OUT
 (out/'ink_contamination.py').write_bytes(Path(__file__).read_bytes())
 base.build()
 p=out/'index.html';page=p.read_text(encoding='utf-8')
 page=page.replace('지하수로 일반 몬스터 2차 비교','잉크 오염 지하수로 · 일반 몬스터 3차 비교').replace('지하수로 일반 몬스터 · 2차 디자인 비교','잉크 도시 아래 · 방치된 지하수로')
 page=page.replace('A 잔업 정비반','A 퇴적물과 잔해').replace('B 자생종','B 뿌리와 균사').replace('C 잔심부름꾼','C 수로 생물')
 page=page.replace('채택 원거리 1종을 고정하고, 근거리 3종 + 추가 직사 원거리 1종을 A/B/C로 새로 비교합니다. 신규 디자인은 12종이며, 각 보드에 채택한 신호투척수가 같은 모습으로 함께 들어갑니다.','관리되지 않은 지하수로의 잔해·뿌리·작은 생물에 잉크가 스며들어 형태와 움직임이 뒤틀린 방향입니다. A/B/C는 같은 오염된 수로의 소재를 나눠 본 시안이며 직업이나 별도 세력 설정이 아닙니다. 신규 일반 디자인은 12종입니다. 채택 신호투척수는 원본 사본을 비교 기준으로 유지했습니다.')
 page=page.replace('C1 열쇠참새 + A2 걸레매듭 + B3 연뿌리주먹 + A4 물통부리 + 채택 신호투척수. 작은 동물형·천 사물형·뿌리 식물형을 섞고, 직사와 곡사도 구분됩니다.','A1 먹묻은쥐 + B2 물든주름갓 + C3 철망등게 + A4 막힌토관 + 채택 신호투척수. 수로 동물·젖은 균사·낡은 배수 부품을 섞되, 잉크는 관절과 발사구에 집중해 몸체의 시인성을 유지합니다.')
 p.write_text(page,encoding='utf-8')
 prior=base.ROOT/'outputs/waterway/normal-monsters-round2-20261031/selected/design-cutout.png'
 assert base.sha(prior)==base.sha(out/'selected/design-cutout.png')
 validation=json.loads((out/'validation.json').read_text(encoding='utf-8'))
 validation['artDirection']='Neglected sewer beneath an ink city; native fauna, roots, wet residue and old drainage fragments colonized from within by ink. No job/faction characterization.'
 validation['selectedDesignMatchesEarlierAcceptedCopy']=True
 base.dump(out/'validation.json',validation)
 archive=out/(out.name+'.zip')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(out.rglob('*')):
   if path.is_file() and path!=archive and '__pycache__' not in path.parts:z.write(path,path.relative_to(out))
 print(json.dumps({'theme':'neglected sewer colonized from within by ink','newOrdinaryMonsters':12,'selectedRangedUnchanged':True,'archiveBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
 if sys.argv[-1]=='prepare':prepare()
 elif sys.argv[-1]=='build':build()
