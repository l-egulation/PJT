"""Unselected visual alternatives for the final ordinary melee and ranged slots."""
from pathlib import Path
import json, shutil
ROOT = Path(__file__).resolve().parents[2]
FONT = 'C:/Windows/Fonts/malgun.ttf'
OUT = ROOT / 'outputs/waterway/final-slots-candidates-20261031'
ADOPTED = ROOT / 'outputs/waterway/adopted-monsters-20261031'
SETS = {
 'K': [
  ['굽은잠행자','근거리·접근','등을 활처럼 굽힌 작은 몸과 긴 다리','몸을 펴며 얇은 팔날로 찌르기',78,60,
   'A small LANKY humanlike stalker folded into a deep forward ARCH: a narrow dusty CORAL ceramic back plate, a tiny face sunk under the front end of the arch, two LONG thin bent pale GREY legs, two slender trailing hooked arm-blades. No separate round head, no coat, no hood, no bulky shoulders or fists. The space beneath the arched spine is large and open. One foot about to spring left; sharply different from a low four-legged wedge or a tall shroud.'],
  ['먹톱고리','근거리·회전','빈 초승달 몸과 안쪽 톱니','고리를 기울여 가장자리로 베기',60,66,
   'An UPRIGHT INCOMPLETE CRESCENT-RING creature, a broad pale LILAC and IVORY crescent surrounding a large empty center, three short uneven ink toes at the base, three broad blunt serrations on the INNER front curve. A short dark face slit is embedded in the upper crescent, no detached head, arms, coat or shoulders. Side view clearly shows an open C-shaped crescent rather than a solid wheel, no physical weapon. Leaning left to sweep with the crescent edge.'],
  ['갈비분사체','원거리·직사','벌어진 갈비판과 가슴 안 발사 틈','갈비판을 벌리고 가슴에서 먹줄기 분사',84,66,
   'A headless low KNEELING ranged creature with a narrow slate lower body and FIVE large warm PALE YELLOW rib-plates fanning upward and forward around a horizontal dark CHEST firing slit aimed left. Two thin folded knees brace the ground behind. No separate head, shoulders, hands, long barrel, cloak or bag. Clear pale fan-shaped open rib silhouette with visible gaps, a body that opens to fire, not a skeletal animal.'],
  ['등짐분사수','원거리·분사','긴 사람몸과 세로 등짐통·짧은 호스','두 손으로 호스를 겨누고 먹물 분사',82,76,
   'A thin crouched HUMANLIKE hose shooter, dusty ROSE short jacket and pale grey wrapped legs, small flat steel face with NO hood or cap. One tall narrow CREAM pressure tank is strapped vertically to its back. Both small hands aim a SHORT curved hose nozzle left at waist height, clearly held not fused into a cannon arm. Tall backpack versus low forward nozzle create two different height peaks. No apron, overhead throwing pose, giant torso, bulky fists, robe or crossbow. Ink leaks only at the hose joint.']
 ],
 'L': [
  ['뿌리도약체','근거리·도약','갈라진 줄기몸과 길게 벌어진 뿌리다리','뿌리를 접어 도약한 뒤 발끝으로 치기',76,72,
   'An ink-corrupted ROOT humanoid, narrow forked WHEAT-GOLD stem torso, small off-center dusty PLUM seed-face fused into the fork, two LONG exposed root legs in a leaping crouch and one short twig arm. Sparse flat pale SAGE leaf clumps only on the rear shoulder. Big airy Y-shaped silhouette, no large crown, pot, dinosaur, animal muzzle, grass skirt, robe or bulky forearm. Three clear main stem masses, no fine branch network.'],
  ['횡팔전단체','근거리·중거리','짧은 몸 양옆으로 긴 전단팔','가는 두 팔날을 수평으로 교차해 베기',112,58,
   'A SHORT upright cutter with a compact muted MINT rectangular torso on two narrow pale GREY legs, tiny face notch tucked into its top, no neck. TWO very LONG thin dull BRICK shear-arms extend from opposite sides at WAIST height, one swept back and one aimed left; the flat blades cross in front with a big open V between them. A broad low horizontal arm axis with small central torso. No giant hands, thick shoulder pads, wedge shell, wheel, cloak, held scissors or large head.'],
  ['바늘부채','원거리·산탄','넓게 펼친 세 갈래 부채몸','세 발사 틈을 정면으로 모아 먹침 발사',98,62,
   'A hovering THREE-PRONG FAN creature, three separate long flat pale APRICOT panels fan left from one small dusty TURQUOISE rear knot, with large transparent wedges between the panels. Each panel has one dark tiny firing cleft at its front edge, all aimed left. The outer fan is broad, horizontally oblique and angular, no head, torso, legs, hanging sacs, face mask, trailing ribbons, cloth or wings. The panels are part of its body, not a carried fan or domestic appliance.'],
  ['압력벌림체','원거리·직사','각진 집게몸과 한 점 지지발','위아래 판을 벌렸다 닫으며 압축탄 발사',92,58,
   'An angular CLAMP-BODY creature on ONE short flat support foot. Two broad pale GREY and dusty MUSTARD plates form a large open sideways V mouth aimed left, joined by a small matte BLUE rear hinge. A dark ink throat in the hinge prepares pressure fire. No humanoid head or limbs, no teeth, animal jaw, wheel, shroud, wedge roof, turret tube, glowing eye or dinosaur. The large EMPTY V-shaped firing gap is the silhouette identity; body reads as a tense folding machine-organism.']
 ],
 'M': [
  ['구르는매듭','근거리·돌진','속이 빈 둥근 몸과 뒤로 흐르는 먹힘줄','둥근 몸을 굴려 짧게 들이받기',76,62,
   'A single upright ROLLING LOOP body, broad matte dusty RED outer arc and warm GREY inner side, open circular hole occupying most of its body. Two short ink tendons trail right and a tiny ochre face tab sits INSIDE the left arc. No spokes, mechanical tire tread, rider, separate head, walking legs, arms, shroud or jagged crescent. The loop itself contacts the ground and leans forward left. A simple corrupted living knot, not a literal vehicle wheel.'],
  ['빈가슴유랑자','근거리·기본','가슴의 큰 빈 구멍과 가는 팔다리','몸을 비틀어 긴 손끝으로 긁기',70,76,
   'A slender hunched HEADLESS porcelain-and-ink humanlike walker, pale PEACH upper torso with a LARGE empty oval hole through its chest, only two thin side ribs around that void. A small flat face sits directly on the LEFT collarbone, not on a neck. Two normal-length thin warm GREY legs in an uneven stride, two long narrow pale hands reaching forward with blunt fingers. No huge shoulders, fists, coat, hood, wedge, floating cloth or heavy armor. The chest void stays conspicuously open in silhouette.'],
  ['먹침필경사','원거리·직사','낮게 앉은 몸과 길게 뻗은 주둥이판','주둥이판을 고정하고 먹침 발사',100,56,
   'A very LOW SEATED ranged humanoid with a broad pale MAUVE torso, two splayed dusty PLUM bent legs and NO visible arms. A flattened warm IVORY face plate occupies the front of the torso, with one LONG THIN rigid mouth-channel projecting left at head height; no snout anatomy, animal jaw or teeth. A small ochre forehead patch, not a hood or mask. Its seated body forms a stable squat triangle and the mouth channel a long thin horizontal stroke. No tank, barrel arm, overhead throw, cloak, conjoined partner or large fists.'],
  ['굴절관측체','원거리·직사','엇갈린 두 판몸과 지그재그 연결축','윗몸을 앞쪽으로 꺾어 틈에서 발사',68,84,
   'A tall NARROW ranged machine-organism with TWO offset small rectangular matte BRASS and light BLUE plates connected by a thin ZIGZAG ink-bound strut. The upper plate leans left and contains a horizontal front firing louver, lower plate sits farther right above two short thin planted feet. Large negative space between offset plates, no ordinary head and torso, arms, pressure tank, long tube, orb, shroud, shield, spear or large shoulders. Light blue and warm brass cover the broad surfaces, not dark grey-green.']
 ]}

COMMON = '''Use case: stylized-concept. Asset: ordinary enemy design comparison for a 2D side-scrolling PIXEL action game. NEW character designs, not edits of reference images.
World: a neglected sewer beneath an ink city. Bad ink has inhabited damp civic remnants, bodies and roots; dark ink visibly binds gaps and stains broken material planes. Eerie tension, not a cheerful worker gang or ancient temple. No gore.
Style priority: genuinely OUTLINE-FREE pixel-art COLOR BLOCKS. Material fill itself touches transparency. No black outline, colored contour, thin perimeter stroke, bright rim, bevel rim, smoothing, 3D render or realistic texture. Only 2-3 broad shade planes per material, a small restrained palette and chunky pixel clusters. Broad pale/warm/mid-light body surfaces contrast with blue-grey/green-grey walls; bright eyes alone are insufficient. Ink is a few intentional dark inner masses, never an all-black body or drawn outline. Different suitable colors for each individual, no uniform beige cast.
Reference 1 is approved player for density only: actual drawing66x64 in canvas192x160. Reference 2 is the original sewer scene for world and contrast only. Do not redraw either reference or put a player/background in the result.
The adopted enemies already occupy these silhouettes: long legless ivory hanging SHROUD; low four-legged WEDGE; round short orange APRON THROWER with raised canister; HUGE asymmetric one-arm HULK; two CLOAKED linked partners with crossbow. NONE of those body templates, poses, costumes or proportions may recur. No dinosaur or cast of animals. Do not normalize new characters into squat robots with thick shoulders, short legs and gun fists. The individual body plan is the priority.
Exactly FOUR separate original characters on a truly TRANSPARENT landscape1536x1024 board. UPPER LEFT and UPPER RIGHT are melee alternatives; LOWER LEFT and LOWER RIGHT are ranged alternatives. LEFT-FACING SIDE PROFILE. Strict 2x2 separation with generous empty gaps; every complete figure inside its own quadrant. Different overall proportions, contact points, body voids and firing directions. No text, labels, panel borders, ground, shadow, aura, projectile, attack effects, duplicate views, animation or sprite sheet. Ordinary native heights roughly50-80px, display enlarged with crisp economical clusters rather than painted microtexture.
'''


def prepare():
    OUT.mkdir(parents=True, exist_ok=True)
    background = ROOT.parent / 'stage2-waterway-reference/outputs/waterway/parallax-kit-v3-20261031'
    refs = OUT / 'references'
    for name in ['README.md','layout.json','A-game.png','B-game.png','C-game.png','D-game.png','E-game.png',
                 'assets/approved-player.png','assets/wall.png','assets/far.png','assets/foreground.png']:
        dst = refs / name; dst.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(background/name, dst)
    shutil.copytree(ADOPTED/'selected', OUT/'adopted', dirs_exist_ok=True)
    shutil.copy2(ADOPTED/'adopted-board.png', OUT/'adopted-board.png')
    # Retain the selection snapshot from this exploration's creation.
    if not (OUT/'choices.json').exists():
        shutil.copy2(Path(__file__).parent/'current-choices.json', OUT/'choices.json')
    for k, items in SETS.items():
        folder = OUT/k
        for sub in ['raw','cutouts','previews','references']:
            (folder/sub).mkdir(parents=True, exist_ok=True)
        for path in ['assets/approved-player.png','C-game.png']:
            shutil.copy2(refs/path, folder/'references'/Path(path).name)
        prompt = COMMON+'\n\n'+'\n\n'.join(f'{k}{i+1} ({["UPPER LEFT melee","UPPER RIGHT melee","LOWER LEFT ranged","LOWER RIGHT ranged"][i]}): {m[6]}' for i,m in enumerate(items))
        (folder/'prompt.txt').write_text(prompt, encoding='utf-8')
        (Path(__file__).parent/f'final-slots-{k}-prompt.txt').write_text(prompt, encoding='utf-8')
    (OUT/'catalog.json').write_text(json.dumps(SETS, ensure_ascii=False, indent=2), encoding='utf-8')
    print(OUT)


if __name__ == '__main__':
    prepare()
