"""Silhouette structures derived from explicit visual research, not retained character templates."""
from pathlib import Path
import sys,json,re,zipfile,shutil
from PIL import Image
import next_forms as base

base.OUT=base.ROOT/'outputs/waterway/topology-reset-20261031'
base.PROMPT_PREFIX='topology-reset'
base.SCENE_MAP={'H':'A','I':'D','J':'E'}
base.FOOT_GAPS={'H3':18,'I1':18,'I3':18,'J3':20}
base.SETS={
 'H':[
 ['바닥먹그늘','근거리·접근','바닥에 붙은 길고 얇은 몸','납작하게 미끄러져 긴 가장자리로 치기',86,26,'A VERY FLAT HORIZONTAL ink creature, width at least FOUR TIMES its height. A long pale SAND and dusty ROSE membrane strip almost touches the ground along its whole underside, three tiny root-like contact points beneath, a small dark front slit, NO torso, shoulders, hands, normal head or walking legs. The thin upper membrane bends into a short hooked front cutting edge. A low crawling/smearing existence, not an animal or an appliance. Keep this extremely flat silhouette.'],
 ['갈고리잠행자','근거리·기본','좁은 코트몸과 뒤로 당긴 긴 갈고리','낮게 걸어들어 갈고리를 크게 휘두르기',100,68,'A LITHE CLOTHED HUMANLIKE stalker, narrow INDIGO short split coat, pale grey undershirt and a dusty BRICK half mask, normal small head attached to normal neck, long thin knees in a low forward step. It holds one long curved dull BRONZE dredging hook behind the waist in a clear diagonal wind-up. Coat hem and held hook create an open triangular gap. No huge shoulders, giant forearms, robot armor or floating ivory head. Weathered sewer-infiltrator character, not a copied knight or game character.'],
 ['늘어진수의','원거리·직사','발 없이 세로로 늘어진 긴 수의','접힌 손가락을 들어 먹줄기 발사',40,80,'A VERY NARROW VERTICAL HOVERING shroud spirit, height at least THREE TIMES its torso width. Long pale IVORY cloth tapers into three pointed hanging hems, dusty MAUVE inner fold and one narrow OCHRE mask inset near the top. NO LEGS OR FEET. Two tiny sleeve hands emerge only halfway up, one hand points horizontally left in firing preparation. No muscular shoulders, gun arm, barrel or large torso. Clearly suspended above the ground, a quiet unnerving sewer revenant.'],
 ['낮은삼각포','원거리·직사','낮은 삼각몸과 가는 세 받침','받침을 벌리고 낮은 포구로 분사',104,38,'An EXTREMELY LOW TRIANGULAR autonomous ranged machine, width nearly THREE TIMES height. One flat dusty RED sloped main plate, a thin ivory horizontal firing tube aligned along the front, and THREE VERY THIN tripod braces underneath, no head, shoulders, humanoid chest, arms or thick legs. Empty air visible between the thin supports. Worn blue-grey lower plate. Low pressure-turret posture, not a wedge beast or a domestic object with legs.']
 ],
 'I':[
 ['사슬매듭','근거리·접근','세 개의 큰 고리와 매달린 갈고리팔','공중에서 몸을 틀어 갈고리로 긁기',68,72,'A DANGLING legless chain-and-ink apparition: exactly THREE LARGE broken OCHRE and pale GREY oval links form a thin vertical S-shaped body around one tiny dark face knot; THREE narrow hanging appendages end in small flat hook blades. Big OPEN HOLES between links, no shoulders, pelvis, normal head, biped legs, thick fists or torso mass. Hovers with hooks below, side profile, ink only binds two link breaks. Not a household key or a wheel runner.'],
 ['막음자','근거리·기본','세로 방패 뒤의 좁은 사람몸과 긴 창','방패 옆으로 가는 창을 찌르기',104,72,'A SLENDER humanlike sewer guardian behind a TALL NARROW rectangular PALE BRONZE shield. Shield is height-dominant, not a wide chest. A small dull steel helmet and face appear above, two short wrapped feet below, dark dusty GREEN waterproof cloak barely visible behind. One LONG THIN ivory spear projects horizontally left past the shield, creating a CROSS silhouette of vertical shield and horizontal spear. It is a person carrying a shield, not an animated door, giant robot or giant fist.'],
 ['먹안개낭','원거리·곡사','넓은 부유막 아래 세 개의 늘어진 낭','앞쪽 낭을 오므려 먹방울 투척',96,72,'A WIDE-TOP NARROW-BOTTOM floating ink-membrane entity, like an irregular pale BLUE-GREY fan suspended in the air. Three separate warm OCHRE and dusty ROSE swollen sacs hang below on thin dark tendons, front sac neck bent up-left to lob ink. NO feet, legs, human head, shoulders, hands or gun. Broad light canopy, large transparent gaps between hanging sacs and a thin trailing edge. Not a mushroom, potted plant or detailed realistic jellyfish.'],
 ['쌍두엮임','원거리·직사','높이가 다른 두 머리와 하나로 엮인 세 다리','두 몸이 함께 당긴 석궁으로 발사',104,78,'ONE conjoined ranged creature with TWO SMALL heads at different heights and THREE shared legs. Front half crouches in dusty ROSE cloth, rear half stands taller in faded BLUE cloth; a short broad dark ink rib bridge physically joins both torsos. Their four hands brace ONE compact IVORY crossbow aimed left between them. Strong two-peak silhouette with a clear notch between heads, one continuous connected character, not two disconnected enemies. Neither head is a floating ivory pill; simple warm grey half masks. No large shoulders or gun arm.']
 ],
 'J':[
 ['흐름몸','근거리·접근','다리 없이 S자로 휜 연속 몸','몸 전체를 휘어 가늘게 후려치기',58,78,'A SLENDER S-CURVE ink-bodied creature with ONE continuous tapering tail contacting the ground. Pale BLUE-GREY and warm IVORY membrane bands around a dark ink core, a narrow flat forward slit at its top, two tiny side membrane fins only. NO separate head, shoulders, thick arms, normal legs or animal scales. Three alternating concave bites in the silhouette form a strong winding vertical S. Flexible body preparing a whip-like bend, not a muscular humanoid or an animal snake.'],
 ['외눈보행추','근거리·기본','둥근 몸 하나와 길게 꺾인 한 다리','한 다리를 접어 발끝으로 내려찍기',58,82,'A SINGLE ROUND BODY perched high on ONE LONG sharply bent STILT LEG. Matte warm COPPER orb about 35 percent of total height, one small off-center pale face slit, one tiny rear balancing arm. Long pale GREY-BLUE leg bends twice and ends in three small spread toes. No head+torso arrangement, shoulders, second walking leg, huge fists or household object. Tall top-heavy single-contact silhouette with lots of empty space under orb, unlike any biped robot.'],
 ['떠도는가면','원거리·직사','큰 가면 뒤로 길게 흐르는 두 띠','가면 입을 조여 먹파동 분사',108,56,'A HORIZONTAL flying entity: one large broken PALE BONE mask at the LEFT front, no torso or limbs, with TWO LONG dusty ROSE and faded CYAN membrane ribbons streaming right behind it. Broad face plane with one dark uneven eye slit and a short horizontal mouth opening ready to emit ink. Ribbons taper at different heights and leave a large open wedge of air between them. NO legs, shoulders, hands, cloak body, machine cannon or animal wings. No glowing aura.'],
 ['장대투척자','원거리·투척','가늘고 긴 코트몸과 머리 위 장대','장대를 머리 위로 당겼다가 정면 투척',116,90,'A VERY TALL NARROW clothed humanlike thrower in a long faded BLUE split waterproof coat, thin OCHRE wrapped legs, small copper half mask and pale grey forearms. Holds a LONG THIN barbed dredge spear ABOVE the head, drawn back diagonally and pointing left in a javelin wind-up. Body about 64 native pixels high, spear raises total silhouette to about 90. Coat is a vertical narrow triangle, pole is a long diagonal line; normal small hands, no huge shoulders, gun arm, barrel, floating head or muscular robot.']
 ]}
base.COMMON='''OUTLINE-FREE crisp PIXEL ART: direct material-color fills at every silhouette edge. NO black or colored contour strokes, bevel rim, bright rim, gradient, 3D volume rendering or vector smoothing. Large clean pixel clusters and 2-3 broad value planes per material, roughly 8-12 matte colors per creature. Ordinary game scale is 26-82px high depending on the actual body plan; enlarge for review. Broad light or warm material planes must read over dark blue-grey/green-grey sewer walls without an outline or glow.
Create FOUR ORIGINAL ENEMY CHARACTERS, each with a COMPLETELY DIFFERENT WHOLE-BODY STRUCTURE. Do not render four squat biped robots. The last attempt failed because every subject had big rounded shoulders, short thick legs, giant hands and a horizontal gun. Those body templates are REJECTED. No bulky armor lobes, giant fists, broad squat robot chests, cannon forearms or repeated cream pill-heads on ink necks.
Think like a genuinely diverse side-scrolling enemy roster: a flat crawler, a clothed weapon user, a legless hovering shape, a tall narrow shield, a wide floating membrane, a multi-head conjoined shape, a continuous S, a one-leg orb, or a flying face-with-streamers. Follow each individual structure exactly, especially its ground contact count, emptiness and width/height ratio. At most ONE ordinary two-leg humanoid per board unless explicitly specified as conjoined. Do not normalize all subjects to the same height, mass or stance. Clothing, carried weapons, soft membranes and unusual supports ARE ALLOWED; do not replace them with robot limbs.
World anchor: a neglected sewer below an ink city. Damp cloth, faded civic metal, masks, ink-bound gaps and unsettling held tension. Not an ancient temple, military faction, happy worker gang or a cast of animals. Different character-specific colors and materials, no common beige/ochre uniform. No neon, gore, aura, emitted effects or projectiles.
Reference input 1: approved player for PIXEL DENSITY and lineless color rendering only, actual art 66x64 in canvas192x160; do not copy its hood/scarf/face. Reference input 2: original unchanged sewer scene for environment and hue, not scenery to redraw. Retained hulking elite and wedge heavy are deliberately NOT generation references. We researched Skul's roster diversity only as an abstract design lesson; do not reproduce any Skul character, costume, weapon or outline style.
Composition: landscape1536x1024, truly transparent. Exactly four isolated full bodies: UPPER LEFT melee1, UPPER RIGHT melee2, LOWER LEFT ranged1, LOWER RIGHT ranged2. Strict LEFT-FACING SIDE VIEW. Each fully inside its own quadrant, wide transparent gaps. No labels, text, panel borders, shadows, floor, repeated views, animation or sprite sheet. Floating subjects must clearly have no feet and their designed negative spaces. Keep every external edge unoutlined.
'''

def choices():
 p=base.OUT/'choices.json';c=json.loads(p.read_text(encoding='utf-8'));c['rejectedNextFormIds']=[f'{k}{i}' for k in 'DEFG' for i in range(1,5)];c['researchBasis']=['https://store.steampowered.com/app/1147560/Skul_The_Hero_Slayer/','https://steamcommunity.com/games/1147560/announcements/detail/2805137023068596466','https://skul.fandom.com/wiki/Enemies'];base.dump(p,c)

def prepare():
 base.prepare();choices()
 notes={'observations':['Official enemy screenshot7: ordinary spear/sword figures and a much larger armed soldier differ in weapon axis and body scale.','Official screenshot8: root-based plant enemies occupy a broad continuous base instead of human leg spacing.','Official developer blog: vertical robe masses and tall carried implements establish different upper silhouettes.','Official character roster contains PLAYABLE skull forms, not an enemy list; used only for general silhouette comparison.'],'designDecision':'Do not use retained A3/B3 as body templates. Establish contact topology and width/height differences before color or gear. Preserve outline-free project style.','sourcesFile':'references/research/sources.json'}
 base.dump(base.OUT/'references/research/analysis.json',notes)

def build():
 out=base.OUT;(out/'topology_reset.py').write_bytes(Path(__file__).read_bytes());base.build();choices()
 page=(out/'index.html').read_text(encoding='utf-8')
 nav='<nav><a href="#retained">보존 선택</a>'+''.join(f'<a href="#{k}">탐색 {k}</a>' for k in 'HIJ')+'</nav>'
 page=re.sub(r'<nav>.*?</nav>',nav,page)
 page=page.replace('추가 조형 탐색 · 신규 16종','실루엣 구조 재탐색 · 신규 12종').replace('추가 조형 탐색 · 신규 일반 후보 16종','실루엣 구조부터 다시 · 신규 일반 후보 12종')
 page=page.replace('새 보드는 일반 근거리 8종과 원거리 대안 8종입니다.','새 보드는 일반 근거리 6종과 원거리 대안 6종입니다. 직전 D~G 16종은 모두 제외했습니다.')
 page=page.replace('사람형·기계형·머리 없는 몸·비정상적인 지지 방식과 발사 부위를 넓게 비교합니다.','스컬의 공식 자료에서 형태·무기 축·옷의 큰 면·접지 구조 차이를 살펴보고, 이번에는 납작한 바닥형·세로 부유형·직립 방패형·단일 다리·S형·쌍두형처럼 전체 몸의 구조부터 다르게 잡았습니다. 스컬 캐릭터를 재현하지 않고 우리 게임의 테두리 없는 화풍을 유지합니다.')
 page=page.replace('D1 스침몸 / E1 뒤집힌걸음 / F2 밀린얼굴 / D3 모은손 / E3 목구멍몸. 보존한 거구·쐐기형과 다른 움직임 축과 발사 부위를 가진 후보입니다.','H1 바닥먹그늘 / H2 갈고리잠행자 / H3 늘어진수의 / I3 먹안개낭 / J2 외눈보행추. 바닥형·무기 인물형·세로 부유형·막형·한 다리형을 나란히 비교하세요.')
 page=page.replace('</main>', '<details><summary>참고한 스컬 공식 자료와 분석</summary><p>아래는 외부 게임의 실제 참고 자료입니다. 우리 게임 시안이나 캡처가 아닙니다. 개발 블로그는 적의 로브·도구 축, 공식 스크린샷은 병사·뿌리형 적의 차이를 살폈습니다. 별도 로스터 이미지는 플레이어 캐릭터 자료이므로 적 목록과 구분했습니다.</p><p><a href="https://store.steampowered.com/app/1147560/Skul_The_Hero_Slayer/">공식 스토어</a> · <a href="https://steamcommunity.com/games/1147560/announcements/detail/2805137023068596466">공식 개발 블로그</a> · <a href="references/research/analysis.json">분석</a></p><img class="board" src="references/research/official-devblog.png" alt="스컬 공식 개발 블로그 참고"><img class="board" src="references/research/official-screen-7.jpg" alt="스컬 공식 적 배치 참고"><img class="board" src="references/research/official-screen-8.jpg" alt="스컬 공식 뿌리형 적 참고"></details></main>')
 for k in 'HIJ':
  # Same alpha, same scale as the color board: expose silhouette sameness directly.
  color=Image.open(out/k/'design-board.png').convert('RGBA');sil=Image.new('RGBA',color.size,(238,238,234,255))
  for i,(cx,ground) in enumerate([(400,470),(1200,470),(400,990),(1200,990)]):
   cut=Image.open(out/k/'cutouts'/f'{k}{i+1}-enlarged.png').convert('RGBA');native=Image.open(out/k/'cutouts'/f'{k}{i+1}-scale-study.png');cut.putdata([(26,31,39,a) for r,g,b,a in cut.getdata()]);cut=cut.resize((native.width*5,native.height*5),Image.Resampling.NEAREST);sil.alpha_composite(cut,(cx-cut.width//2,ground-cut.height))
  sil.convert('RGB').save(out/k/'previews/silhouette-board.png')
  marker=f'<section id="{k}">';start=page.index(marker);end=page.index('</section>',start)
  segment=page[start:end];segment+='<details><summary>검은 실루엣만 비교</summary><img class="board" src="'+k+'/previews/silhouette-board.png" alt="색을 뺀 구조 비교"></details>';page=page[:start]+segment+page[end:]
 (out/'index.html').write_text(page,encoding='utf-8')
 v=json.loads((out/'validation.json').read_text(encoding='utf-8'));v['scope']='12 new ordinary designs (6 melee,6 ranged), distinct body/support topology. Retained 3 unchanged. Prior D~G16 excluded.';v['referenceResearch']='Official Skul screenshots and developer blog inspected, playable roster separated from enemy evidence; no direct character reproduction.'
 links=re.findall(r'(?:src|href)="([^"]+)"',page);missing=[x for x in links if not x.startswith(('#','https://')) and not (out/x).is_file()];assert not missing;v['localLinksMissing']=missing;base.dump(out/'validation.json',v)
 for name in ['topology_reset.py','next_forms.py','normal_round2.py','README.md','current-choices.json']:shutil.copy2(Path(__file__).parent/name,out/'source'/name)
 archive=out/(out.name+'.zip')
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(out.rglob('*')):
   if p.is_file() and p!=archive and '__pycache__' not in p.parts:z.write(p,p.relative_to(out))
 print(json.dumps({'newCandidates':12,'bodyPlans':'flat/cloth/hover/tripod/chain/shield/membrane/conjoined/S/single-leg/mask/pole','archiveBytes':archive.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
 if sys.argv[-1]=='prepare':prepare()
 elif sys.argv[-1]=='build':build()
