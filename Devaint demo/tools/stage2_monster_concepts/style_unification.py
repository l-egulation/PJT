"""Prepare non-destructive style edits of the seven selected monster designs."""
from pathlib import Path
import json, shutil, hashlib

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).parent
BASE = ROOT/'outputs/waterway/adopted-monsters-v2-20261031'
OUT = ROOT/'outputs/waterway/style-unified-20261031'
FONT = 'C:/Windows/Fonts/malgun.ttf'
ITEMS = {
 'H3': ['늘어진수의','원거리·잉크 공 투척',40,74,'Warm grey-IVORY shroud and dusty MULBERRY inner folds; muted ochre mask. Preserve the long narrow legless tapering cloth, two tiny black-ink hands, projecting left hand and exact mask shape. Use three broad cloth planes, no extra holes or new equipment. Approximate palette: ivory #decfb7, inner mauve #956678, cool shadow #74828c, ink #292b3b.'],
 'B3': ['쐐기보행기','큰 근거리',111,65,'Preserve the low long forward wedge shell, exactly the original support limbs and tall rear plates. Warm bone shell #d4c6aa, dusty COPPER legs #b27360, pale EUCALYPTUS rear plates #77918d, charcoal-violet internal joints. Remove extra little bevel bands; give each shell or leg at most 3 broad flat planes. No animal face, extra limb or equipment.'],
 'M3': ['먹침필경사','근거리·찌르기',100,39,'Preserve the very low seated body, splayed bent legs, pale flat face and extremely LONG THIN SOLID stabbing mouth-plate pointing left. This is a MELEE piercer, NOT a firearm: no muzzle bore, projectiles or barrel details. Dusty MAUVE body #b391a4, wine-plum lower masses #695168, warm bone face and mid-light ivory stabbing plate. Keep the long point readable against dark background; no head redesign, arms or new limbs.'],
 'R-selected': ['신호투척수','원거리·곡사',48,62,'Preserve the short broad humanoid, CREAM cap, orange APRON, purple raised arm and ochre signal canister held OVERHEAD, exact throwing pose and ordinary feet. Matte burnt ORANGE #c77a43 apron, muted VIOLET sleeves #7e6b88, warm grey trousers #b1a694, ivory cap #e0d1b9. Remove the black perimeter strokes around cap, arms, apron, canister and feet by allowing material fills to touch transparency. Keep the dark FACE OPENING and internal hand/contact shadows as broad masses, not outlines. Simplify apron patches and canister shading, no new weapon or body proportions.'],
 'K4': ['등짐분사수','근거리·접근 분사',82,73,'Preserve the lean crouched hose-shooter, coral jacket, grey wrapped legs, ONE tall narrow cream back tank and short hose held forward by both hands. Dusty CORAL #b56760 jacket, light warm grey leg planes, bone tank, pale STEEL #849198 face plate and hose/nozzle material. Keep the dark eye inset and ink hose interior but lift the nozzle exterior to mid-light steel so it remains visible over black foreground. Simplify straps and folds. No apron, overhead canister, extra tank, hood or new limb.'],
 'A3': ['편측거구','엘리트',144,112,'Preserve the exact hulking asymmetrical humanoid, HUGE low arm on the LEFT of the image and small right arm, small inset pale face and bent wide legs. Weathered HONEY-OCHRE coat #c7a266 with broad #ddbd7d lit planes, cool pale SLATE enlarged arm #93a5ae, sage-grey trousers #8a9783, dark ink inner chest and joints. Three flat planes at most per material, no extra armor, spikes, outlines or little muscular rendering. Keep the huge arm/body proportion and face size.'],
 'I4': ['수로를 지키는 남매(가칭)','보스·2인조 콘셉트',216,128,'Preserve BOTH original hooded figures at different heights, forward crouching rose figure, rear standing blue figure, original shared crossbow and DARK INK RIB BRIDGE connecting their backs. Do not separate or redesign them in this style pass; independent sibling bodies remain future work. Dusty ROSE coat #b98989 and light faded BLUE coat #8dabb3 must each have a BROAD MID-LIGHT main plane, with warm BONE masks #dcc9ac and wrapped legs. Remove the thin dark contour strokes around hoods, coats, bow and limbs; retain the broad ink bridge and internal occlusion shapes. Simplify to 2-3 planes per coat, no tiny cloth texture or added parts. Preserve face angles, weapons, figure count and pose. Boss128px-height is a size study only.'],
}
COMMON = '''Use case: style-transfer / identity-preserve. Edit ONLY the monster in input image1. This is an approved design, not a request for a new character.
Input1 EDIT TARGET: preserve its full silhouette, pose, proportions, facing direction, appendage count, major voids, held weapon, face and identity. Input2 STYLE/DENSITY REFERENCE: the approved player, actual visible art66x64 within transparent192x160 canvas; never redraw or insert the player. Input3 ENVIRONMENT REFERENCE: original960x540 sewer, do not redraw, brighten, blur or include scenery.
Primary request: make this target belong to the SAME outline-free pixel game as the player and damp blue-grey/green-grey ink-city sewer. Unify pixel-cluster economy, flat shading, cool shadow family and matte colors while retaining this individual's characteristic colors. Neglected damp materials and ink-bound joints, no decorative sewer props added.
Render as crisp COARSE PIXEL ART with consistent block steps suitable for the stated native scale. About10-14 well-chosen colors, at most2-3 broad shade planes per material. Build anatomy from clear filled blocks. No smooth painting, realistic texture, 3D rendering, antialiasing, gradients, gritty speckle or micro-details. Match the player's economical shape language, not its hood or costume.
CRITICAL: NO EXTERNAL OUTLINES of any color. No black perimeter, dark colored contour band, bright rim, bevel band or drop shadow. Extend the adjacent material-color FILL directly to every outer edge. Dark INK is allowed as a broad body/joint/face mass, never a tracing stroke around another material. Keep sharp pixel boundaries and large dark internal openings.
Broad body planes must remain lighter or warmer than the sewer wall, blue far layer and black foreground. Avoid an all-dark grey-green body, glowing outline, neon or bright eyes as the sole contrast solution. Shared cool grey-blue shade planes and charcoal-violet ink; restrained warm bone highlights. Matte damp ambience, simple upper-left ambient lighting, no external glow.
Output ONE complete isolated monster on a truly TRANSPARENT canvas. If the target is the original connected sibling pair, keep exactly that one pair. Preserve the target's side-facing orientation and major silhouette, no labels, text, background, ground, cast shadow, aura, projectiles, attack effects, duplicates, alternate views, animation or sprite sheet. Leave a little transparent margin around the entire figure and every weapon tip. Do not crop.
'''


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare():
    OUT.mkdir(parents=True,exist_ok=True)
    bg = ROOT.parent/'stage2-waterway-reference/outputs/waterway/parallax-kit-v3-20261031'
    for name in ['README.md','layout.json','A-game.png','B-game.png','C-game.png','D-game.png','E-game.png','assets/approved-player.png','assets/wall.png','assets/far.png','assets/foreground.png']:
        p=OUT/'references'/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(bg/name,p)
    shutil.copy2(SOURCE/'current-choices.json',OUT/'choices.json')
    manifest=[]
    for ident,m in ITEMS.items():
        p=OUT/ident
        for sub in ['before','raw','references','cutouts','previews']:(p/sub).mkdir(parents=True,exist_ok=True)
        shutil.copytree(BASE/'selected'/ident,p/'before',dirs_exist_ok=True)
        shutil.copy2(p/'before/design-cutout.png',p/'references/edit-target.png')
        for src,name in [(OUT/'references/assets/approved-player.png','approved-player.png'),(OUT/'references/C-game.png','C-game.png')]:shutil.copy2(src,p/'references'/name)
        prompt=COMMON+f'\nTarget identity: {ident} {m[0]}, role {m[1]}. Comparison native extent about{m[2]}x{m[3]} visible pixels; keep this pixel economy.\nSpecific material and invariant notes: '+m[4]
        (p/'prompt.txt').write_text(prompt,encoding='utf-8');(SOURCE/f'style-unified-{ident}-prompt.txt').write_text(prompt,encoding='utf-8')
        manifest.append({'id':ident,'name':m[0],'role':m[1],'beforeCutoutSha256':sha(p/'before/design-cutout.png'),'beforeOriginalSha256':sha(p/'before/original-board.png'),'reviewExtentCap':m[2:4],'styleVariantStatus':'review-candidate','identitySource':str(BASE/'selected'/ident),'note':'Non-destructive material/style edit. Original selection, roles and original source art remain authoritative.'})
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(OUT)


if __name__=='__main__': prepare()
