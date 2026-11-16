"""Open silhouette exploration: humanoid, machine and unstable ink forms; no outlines."""
from pathlib import Path
import sys,json,zipfile
from PIL import Image
import normal_round2 as base

base.OUT=base.ROOT/'outputs/waterway/form-exploration-outlinefree-20261031'
base.SOURCE_PROMPT_PREFIX='form-exploration'
base.SETS={
 'A':{'title':'조형 탐색 A','items':[
 ['접힌몸','근거리·접근형','짧게 접힌 사람형 몸과 뒤집힌 무릎','몸을 펴며 빠르게 파고들기',66,42,'A SMALL crouching deformed HUMANOID ink creature. Short broad pale powder-blue trunk folded nearly horizontal, tiny blank head tucked low under one shoulder, two very bent rusty coral legs with one knee turned backward, two short flattened ink-violet forearms close to the ground. A tense coiled humanlike shape ready to unfold into a dash, no animal muzzle, fur, shell or object body. Wide pale trunk and coral legs are the colored masses; ink is only one shoulder gap and two wrists. NO OUTLINE of any color.'],
 ['어긋난관절','근거리·기본형','가느다란 기계몸과 높이가 다른 두 주먹팔','엇박자로 긴 주먹팔 내지르기',100,66,'A narrow upright BIPEDAL MACHINE creature with an off-center pale worn TURQUOISE chest, very small ochre head integrated into the left shoulder, two long thin warm-brass shins and two asymmetrical articulated blunt PUNCH arms. The front fist is a broad light blue block at chest height, the rear arm hangs lower. A warped ink-filled dark-violet joint pulls the spine sideways. It is an original living machine with character posture, NOT an appliance, pipe, watering can, household object or human carrying tools. Simple open gaps around long limbs, flat colors, NO perimeter strokes or shaded bevel rims.'],
 ['편측거구','근거리·압박형','넓은 사람형 몸과 한쪽으로 치우친 큰 팔','짧게 전진하며 큰 팔로 밀어붙이기',110,78,'A broad pear-shaped HULKING HUMANOID ordinary enemy, small flat featureless head set low between unequal shoulders, warm pale OCHRE upper body, muted SAGE wide abdomen and two short widely planted pale feet. One enormous light GREY-BLUE hammer-like organic forearm hangs forward, the opposite arm is tiny. A single ink-violet cleft binds the heavy shoulder and waist. Asymmetrical living anatomy, ambiguous flesh/soft casing, no recognizable sack, barrel, rock pile, shell or armor suit. Heavy low center, no weapon held in the hand. Flat silhouette color fields without outlines.'],
 ['흉곽분사자','원거리·직사','가느다란 사람형 다리와 앞으로 열린 흉곽','흉곽을 벌려 전방으로 먹 분사',92,68,'A slender eerie HUMANOID ranged creature, two long pale blue-grey bent legs bracing an IVORY torso, a small narrow head tilted back and two short hands gripping the side ribs. The rib cage has THREE broad separated warm PEACH planes around one HORIZONTAL dark-violet opening facing left, ready to eject ink from the chest itself. Humanlike posture and clear torso launch direction, NOT a clam, pipe or animal mouth. Broad ivory chest stays light; no glowing eyes, floating effects or shot. Flat limited-color pixel clusters, absolutely no contour lines.']
 ]},
 'B':{'title':'조형 탐색 B','items':[
 ['반륜주행체','근거리·접근형','열린 반고리 하체와 앞으로 기운 작은 상체','몸을 굴려 붙고 앞팔로 찍기',70,44,'A small FAST INK-MACHINE hybrid with an incomplete C-shaped lower running hoop rather than legs, one compact pale LILAC upper torso leaning far forward from the hoop hub, a tiny flat ivory head, one short warm orange striking arm and a smaller rear counter-arm. The broken hoop is pale cool metal on its front and connected by a dark ink tendon across the open gap. A unique active character silhouette, not an everyday wheel with eyes pasted on it. No animal anatomy or cute face. Big flat pale lilac, ivory and rust-orange planes; no stroke around the hoop or the figure.'],
 ['늘어진몸','근거리·기본형','높고 좁은 머리와 유난히 긴 납작한 앞팔','낮게 늘어진 팔을 길게 휘두르기',102,68,'An unnaturally tall narrow HUMANOID made of pale warm GREY and dusty MAUVE material planes, tiny wedge-shaped featureless head high above slumped shoulders, thin bent knees, one very long flattened IVORY forearm reaching low in front and one elbow tucked behind. The torso is offset from the hips by a stretched ink-violet waist bridge. The body itself is the character: no rag, cloth costume, mushroom, plant, tools or recognizable object. Long-armed melancholic menace, large open negative space between arm and leg, no glowing eyes or spiky monster armor. NO OUTLINE, no perimeter shadow stripe.'],
 ['쐐기보행기','근거리·압박형','낮은 쐐기형 기계몸과 네 개의 기둥다리','쐐기몸을 낮춰 밀고 짧게 들이받기',112,72,'A heavy FOUR-LEGGED MACHINE organism, broad low triangular wedge-shaped PALE IVORY body at the front, four short thick faded COPPER pillar legs and one raised dusty TEAL rear mass with a simple ink core gap. Its body is purpose-designed unfamiliar machinery, no animal head, shell, drain grate, actual domestic object, tank or building. Front wedge and planted leg columns communicate pressure and weight. One ink-violet joint is stretched as the machine leans forward. Large flat matte shapes, no bolts, bevels, metallic glints or contour lines; only two broad shadow masses.'],
 ['편팔포격체','원거리·직사','분홍색 좁은 몸통과 수평으로 긴 발사팔','세 다리로 버티고 발사팔을 정면 조준',98,68,'A thin RANGED MACHINE-HUMANOID with a dusty ROSE vertical torso, small blank off-white head, three slender SLATE-BLUE articulated support legs and ONE enormous long PALE GOLD forearm that has a horizontal open firing channel at its left tip. Its other hand braces the cannon elbow near the chest. Strong inverted-L silhouette, the weapon is an integral arm, not a handheld gun or a pipe-body creature. A dark ink gap at the elbow strains backward in recoil preparation. Broad rose body and light gold arm, no glow and no shot. NO outlines or rim shading around any part.']
 ]},
 'C':{'title':'조형 탐색 C','items':[
 ['튕김다리','근거리·접근형','작은 둥근 상체와 길게 접힌 탄성다리','다리를 튕겨 짧게 접근해 찍기',66,44,'A small upright HUMAN-MACHINE hybrid spring runner: compact round cream upper torso with a tiny low blank head, two disproportionately long sharply folded light CORAL spring-legs, two pale turquoise arms folded backward like a sprinting person. Thin ink-violet bands at the knees pull the joints in opposite directions. No bird, ears, animal muzzle, shoe creature or mechanical toy smile. Upright top-heavy but agile silhouette, distinct from a crouching crawler or wheel. Large light planes with no black or colored outline.'],
 ['갈림몸체','근거리·기본형','위아래가 어긋난 사람형 몸과 넓은 앞팔','몸을 비틀며 넓은 팔날로 치기',100,68,'An unstable split HUMANOID, its two broad body halves vertically offset and held by one short ink-violet bridge: pale warm GREY narrow head and high right shoulder, a lower dusty CYAN left chest and a wide IVORY blade-flat front forearm. Two differently bent MAUVE legs keep a sideways stepping stance. No actual object, clothes, armor, human weapon, facial eyes or dinosaur features. Asymmetric hips, head placement and broad arm plane create the character; not an ordinary humanoid recolored. All edges are direct filled color blocks, with NO outline of any thickness.'],
 ['매달린거구','근거리·압박형','속이 빈 아치형 등과 아래로 늘어진 작은 머리','무거운 앞팔을 내려쳐 진로 압박',112,80,'A tall arched heavy MACHINE-HUMANOID with a broad PALE OCHRE curved back frame, a large OPEN NEGATIVE SPACE inside the back, a tiny ivory head hanging below its forward shoulder and two short planted warm grey legs. One very large dusty SALMON forearm hangs almost to the floor in front; the rear arm is a small brace. Ink-violet tendons suspend the head and pull one shoulder low. An unfamiliar heavy creature silhouette, not a pipe arch, gate, barrel, rock pile or conventional robot armor. No contour strokes, bevel rim or tiny rivets; simple flat color masses.'],
 ['벌림시위','원거리·직사','가느다란 사람형 몸과 활처럼 갈라진 긴 팔','갈라진 팔 사이 먹시위를 당겨 발사',98,68,'A lean HUMANOID archer-like ink creature, small low faceless head and pale DUSTY SAGE torso, two bent muted VIOLET legs, one long BONE-colored arm splitting into two broad prongs in front like an organic bow, the opposite warm-grey hand drawing a taut thick ink strand toward the chest. The forked arm and strand are visibly aiming HORIZONTALLY left. No separate bow prop, no hood, soldier uniform, animal, leaf, mailbox, gun barrel or projectile. Graceful tense side silhouette, not a realistic human. Flat large material-color pixels, NO outlining or rim light.']
 ]}
}
base.COMMON='''Highest priority style requirement: OUTLINE-FREE / LINELESS PIXEL ART. ZERO contour strokes, including colored strokes. Do NOT put a dark 1-pixel line, black border, local-color border, bevel rim or bright rim around a silhouette. The flat material fill must directly meet transparency. Shadows are a few broad internal or underside planes, never a stripe tracing the perimeter. This is crucial and takes precedence over decorative detail.
Generate FOUR wildly different original ordinary-enemy CHARACTERS for a 2D side-scrolling pixel action game. Freely explore HUMANLIKE, MECHANICAL and unstable INK-HYBRID anatomy. The personalities must come from body proportions, asymmetrical weight, posture, head position and attack anatomy. Do not keep making recognizable household objects, plants or animals with legs attached. No dinosaur/reptile forms. These are free silhouette experiments, not a matching group of workers or a uniform faction.
World anchor: a neglected waterway beneath an ink city, damp, abandoned and quietly threatening. Ink can distort joints, displace a body mass or bind a machine gap. Use ink as a limited dark structural mass, not as an outline or a coating over everything. No gore, neon, aura or glowing eyes. Their threat should come from held body tension and an unnatural stance.
Input image 1: approved player is a density and outline-free material-shape reference ONLY. Its actual drawing is 66x64 inside a 192x160 transparent canvas. Do NOT redraw it, borrow its hood/scarf/face, or surround the new creatures with its dark inner body colors. Input image 2: unchanged sewer scene is an environment reference ONLY; do not generate scenery. The already selected orange ranged character is deliberately NOT a generation reference and must not appear in this output.
LAYOUT: a 1536x1024 landscape transparent static color-design board. Exactly four isolated full bodies: UPPER LEFT small fast melee, UPPER RIGHT standard melee, LOWER LEFT heavy ordinary melee, LOWER RIGHT ranged aiming horizontally. Strict LEFT-FACING SIDE PROFILE, no front or 3/4 poses. Generous clear transparent space between them, each fully in its own quadrant. No elite, boss, repeated views, animation frames or sprite sheet. No ground, floor shadow, labels, text, numbers, frames or projectiles.
PIXEL RENDERING: large clean flat color clusters, sharply stepped pixel silhouettes, intentionally simple at roughly 40/64/76px native heights then enlarged for comparison. Use about 6-10 matte colors total per creature and only 2 or 3 broad value planes per major material. No realism, 3D icon rendering, anti-aliased vector curves, gradient shading, dithering, tiny texture, etched lines, bolts or anatomy hatching. The figure should be formed entirely from filled shape blocks. Prefer large continuous side planes reaching the silhouette edge; NO continuous contour-colored band.
COLOR VARIETY: use each creature's own materially appropriate palette. Broad pale blue, worn turquoise, coral, ivory, dusty rose, sage, ochre, lavender and grey planes can be mixed as directed; do not make every creature beige or every creature dark green. Body masses must separate from blue-grey/green-grey walls and black foreground without a stroke or glow.
Final check: if a perimeter line is visible around a head, arm, torso, leg or machine part, remove that line and extend the adjacent fill to the edge. NO OUTLINES, including colored outlines. Transparent space only outside the filled silhouettes.
'''

def prepare():base.prepare()

def build():
 base.SETS['C']['items'][2][2]='굽은 아치형 등과 아래로 늘어진 작은 머리'
 out=base.OUT;(out/'form_exploration.py').write_bytes(Path(__file__).read_bytes())
 base.build()
 # Main design boards show only new candidates, avoiding an old outlined reference in the new style review.
 for k in 'ABC':
  board=Image.new('RGBA',(1600,1040),(238,238,234,255))
  for i,(cx,ground) in enumerate([(400,470),(1200,470),(400,990),(1200,990)]):
   cut=Image.open(out/k/'cutouts'/f'{k}{i+1}-enlarged.png').convert('RGBA')
   native=Image.open(out/k/'cutouts'/f'{k}{i+1}-scale-study.png')
   show=cut.resize((native.width*5,native.height*5),Image.Resampling.NEAREST)
   board.alpha_composite(show,(cx-show.width//2,ground-show.height))
  board.convert('RGB').save(out/k/'design-board.png')
 page=(out/'index.html').read_text(encoding='utf-8')
 page=page.replace('지하수로 일반 몬스터 2차 비교','테두리 없는 사람형·기계형 조형 탐색').replace('지하수로 일반 몬스터 · 2차 디자인 비교','사람형 · 기계형 · 잉크 변형체 — 조형 탐색')
 page=page.replace('A 잔업 정비반','탐색 A').replace('B 자생종','탐색 B').replace('C 잔심부름꾼','탐색 C')
 page=page.replace('채택 원거리 1종을 고정하고, 근거리 3종 + 추가 직사 원거리 1종을 A/B/C로 새로 비교합니다. 신규 디자인은 12종이며, 각 보드에 채택한 신호투척수가 같은 모습으로 함께 들어갑니다.','이번 비교는 사람 같은 체형, 낯선 기계몸, 잉크로 어긋난 신체를 자유롭게 섞은 신규 일반 후보 12종입니다. 재질색 면이 실루엣까지 이어지는 테두리 없는 픽셀 그림을 목표로 합니다. 기존 채택 신호투척수 원본은 아래 별도 참고와 크기·배경 비교에서만 유지합니다.')
 page=page.replace('위: 근거리 3종 / 아래 왼쪽: 추가 직사 원거리 / 아래 오른쪽: 채택 신호투척수 원본 사본. 모든 그림은 검토 크기의 4배 확대입니다.','윗줄: 접근형 · 기본형 / 아랫줄: 압박형 · 원거리. 신규 후보만 검토 크기의 5배로 확대했습니다. 기존 채택본은 아래 실제 크기 비교에 별도로 표시합니다.')
 page=page.replace('C1 열쇠참새 + A2 걸레매듭 + B3 연뿌리주먹 + A4 물통부리 + 채택 신호투척수. 작은 동물형·천 사물형·뿌리 식물형을 섞고, 직사와 곡사도 구분됩니다.','A1 접힌몸 + A2 어긋난관절 + C3 매달린거구 + C4 벌림시위 + 채택 신호투척수. 접힌 작은 몸·가느다란 관절 기계·비어 있는 큰 등·시위를 당기는 팔로 역할과 실루엣을 구분합니다.')
 (out/'index.html').write_text(page,encoding='utf-8')
 validation=json.loads((out/'validation.json').read_text(encoding='utf-8'))
 validation['artDirection']='Open silhouette exploration of humanoid, machine and ink-hybrid ordinary monsters. New generation does not use selected outlined ranged as a style reference.'
 validation['mainBoards']='Four new candidates only, arranged 2x2, 5x review extent. Selected reference kept separately unchanged.'
 base.dump(out/'validation.json',validation)
 archive=out/(out.name+'.zip')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(out.rglob('*')):
   if path.is_file() and path!=archive and '__pycache__' not in path.parts:z.write(path,path.relative_to(out))
 print(json.dumps({'newDesigns':12,'scope':'ordinary melee and ranged only','archiveBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
 if sys.argv[-1]=='prepare':prepare()
 elif sys.argv[-1]=='build':build()
