"""Preserve static inputs and prepare three built-in pose edits, without API calls."""
import json, shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'outputs/waterway/pixel-base-step1-20261031'
OLD = ROOT/'outputs/waterway/idle-reference-8-20261031'
MOTION = ROOT/'outputs/waterway/idle-motion-8-v1-20261031'
SOURCE = Path(__file__).parent

POSES = {
    'K4': 'Lean slightly hunched CORAL sewer ink sprayer, exactly ONE tall narrow ivory tank on its back, slate steel face mask with one narrow amber slit, grey wraps on two legs, black ink hose and short steel nozzle held low by both hands. Reveal the front plane of the mask, both shoulders and front of jacket. Tank remains BEHIND the figure on image RIGHT. Face and nozzle point diagonally LEFT, not at camera. Both boots planted, nozzle angled down without spraying. Do not turn the tank into two cylinders or add a gun barrel. Height112 native pixels including tank.',
    'I4-rose': 'The shorter ROSE hooded sibling with bone angular mask, ragged dusty rose tunic, brown sleeves and boots, ivory shin wraps. ONE long ivory and brown crossbow carried LOW across waist with both hands, pointing left and slightly toward viewer. Reveal broad front mask plane and both eye corners inside one dark slit, chest and both shoulders; keep head turned diagonally left. Feet planted shoulder-width, calm standing rather than a deep lunge. Two legs and arms, no blue sibling or inter-body bridge. Preserve crossbow identity and long forward span; do not substitute a handgun or ordinary bow. Height102 native pixels.',
    'I4-blue': 'The taller FADED BLUE hooded sibling, warm bone angular mask, ragged blue tunic, brown sleeves/boots and ivory shin wraps. ONE open palm at waist height supporting a SMALL matte charcoal-purple ink bead floating just above the palm. Other arm relaxed at side. Reveal front mask plane, broad chest, both shoulders and both eyes within its dark slit, facing diagonally LEFT. Calm planted stance with two legs, no step, no cast preparation. No staff, book, bow, other sibling or connecting bridge. Bead has only two flat purple facets, NO aura, glow or particles. Height127 native pixels.'
}

COMMON = '''Use case: identity-preserve / precise pose edit.
Deliver ONE STATIC BASIC POSE of this selected 2D pixel game monster, NOT multiple frames or a sheet.
Image1: edit target and identity/color/anatomy reference. Image2: approved player pixel style reference only. Image3: original sewer color/readability reference only. Never insert the player or background.
Change ONLY camera-facing pose as described. The user rejected the pure profile: rotate the face, chest and hips about30-35 degrees TOWARD THE VIEWER from left profile, so this is clearly a FRONT THREE-QUARTER LEFT view. A useful game angle, same slight three-quarter treatment as the other enemies. Not pure side view, not full frontal, no overhead perspective. Near and far shoulders and legs are distinct; front planes of mask and torso must be substantially visible. Preserve character anatomy, proportions, characteristic colors, material blocks and held item, with a relaxed idle stance.
Style: extremely economical flat pixel shapes matching the player's simplicity. Aim for12-16 total opaque colors, just TWO flat shades per material, one consistent square pixel grid, clear large color clusters. Work as if drawn at the stated native height then integer enlarged. No hairline detail, gradients, dithering, mottling, speckles, shiny bevels or painterly texture. No outside black/color outline whatsoever: material fill touches transparent silhouette directly. Keep dark ink only as substantial interior face/joint/hose masses. Do not add dark contour lines around clothing. No rim lighting. Maintain broad warm/light material planes for dark sewer readability.
Whole body and equipment tips fully inside a TRUE TRANSPARENT canvas with generous margins, no shadows, floor, letters, labels, extra drawings, effects or decorative objects. Just one neutral baseline pose, not an animation.
Specific character: '''

def prepare():
    OUT.mkdir(parents=True, exist_ok=True)
    shutil.copytree(MOTION/'references',OUT/'references',dirs_exist_ok=True)
    player = Image.open(OUT/'references/assets/approved-player.png').convert('RGBA')
    player.crop(player.getbbox()).resize((528,512),Image.Resampling.NEAREST).save(OUT/'references/player-8x.png')
    jobs=[]
    for ident,detail in POSES.items():
        p=OUT/ident
        (p/'raw').mkdir(parents=True,exist_ok=True)
        (p/'references').mkdir(exist_ok=True)
        target = MOTION/ident/'raw/key.png' if ident=='I4-blue' else OLD/ident/'enlarged/idle-8x.png'
        shutil.copy2(target,p/'references/edit-target.png')
        prompt=COMMON+detail
        (p/'pose-prompt.txt').write_text(prompt,encoding='utf-8')
        (SOURCE/f'pixel-base-{ident}-prompt.txt').write_text(prompt,encoding='utf-8')
        jobs.append({'id':ident,'prompt':prompt,'references':[str(p/'references/edit-target.png'),str(OUT/'references/player-8x.png'),str(OUT/'references/C-game.png')]})
    (OUT/'jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'output':str(OUT),'jobs':str(OUT/'jobs.json')},ensure_ascii=False))

if __name__=='__main__':prepare()
