"""Authoring jobs for built-in imagegen and delivery of idle motion studies."""
from pathlib import Path
import argparse, json, shutil
from style_unification import ROOT, SOURCE, sha

PRIOR=ROOT/'outputs/waterway/idle-reference-8-20261031'
OUT=ROOT/'outputs/waterway/idle-motion-8-v1-20261031'
SPEC_PATH=ROOT/'data/presentation/stage2_idle_study.json'

BLUE_EDIT='''Use case: identity-preserve pixel-art character edit. Input1 is the blue sibling EDIT TARGET, input2 is approved player STYLE reference (actual66x64px), input3 is sewer PALETTE/readability reference, never include player or background.
Turn THIS SAME blue sibling into an ominous long-distance INK SPELLCASTER at rest. Preserve the faded BLUE ragged hooded tunic, warm BONE angular mask with dark horizontal eye opening, charcoal-violet inner body, brown forearm material, cream wrapped shins, boots, original slim figure and two arms/two legs. Keep visible height127px at game scale. Remove the empty-handed reaching-for-a-missing-bow gesture. Show a relaxed upright3/4-front stance facing LEFT: chest and mask front plane clearly visible, not a pure profile and not facing right. One elbow loosely bent with open palm at waist/chest level; ONE small dense matte ink bead hangs just above that palm, a charcoal-purple core with one broad dusty lilac/ivory lit facet. Bead stays BELOW the mask so total height is unchanged. Other hand hangs relaxed by side. This bead identifies a caster at rest; do not cast a projectile or use an attack windup. Boots planted, slight knee bend, quiet sinister confidence. No crossbow, staff, book, pentagram, glowing aura, magic text, neon, halo, extra props, other sibling or ink bridge. No new costume or armor.
CRISP OUTLINE-FREE PIXEL COLOR MASSES,2-3 flat shade planes per material, large chunky steps at127px working height; render enlarged for inspection. Material fills touch transparency directly: NO BLACK/COLORED OUTER STROKES, thin dark border tracing hood/coat/hand/boots, bright rim, smoothing, gradients,3D, microtexture, stray fringe or shadows. Broad mid-light blue/bone forms contrast with dark sewer. ONE complete isolated character centered with transparent margin all sides, TRUE TRANSPARENT background, no text, panels, ground or duplicates. This is an updated single idle key reference, not an animation sheet.'''

SHARED='''Use case: identity-preserve outline-free pixel-art game asset. Input1 is the monster EDIT TARGET. Input2 approved player is STYLE/pixel-density reference only (actual66x64px); input3 sewer is palette/readability reference only. Never redraw player or scenery.
Keep the selected character's anatomy, head/body proportions, appendage/foot count, major negative spaces, exact weapons/tool geometry and length, costume, individual color identity and working body scale. No redesign, extra gear or character. Outline-free coarse pixel COLOR FILLS with economical2-3 flat shade planes per material; shared cool slate shadows and charcoal-violet ink gaps. No outer strokes of ANY color, black/colored border tracing clothes/limbs, bright rim, neon, glow, gradients, realistic texture, smooth/3D rendering, blur or antialias fringe. Fills meet TRUE TRANSPARENCY directly. Render the working pixel clusters enlarged for clear inspection, not a high-detail illustration. Preserve complete silhouette and tips inside each cell with generous transparent gutters. No ground, cast shadow, text, numbering, grid strokes, panel outlines, scenery or watermarks.
'''


def spec():return json.loads(SPEC_PATH.read_text(encoding='utf-8'))


def save_job(ident,kind,prompt,target):
    p=OUT/ident
    for sub in ['raw','references','frames','views','enlarged','previews']:(p/sub).mkdir(parents=True,exist_ok=True)
    ref=p/'references'/f'{kind}-target.png';shutil.copy2(target,ref)
    (p/f'{kind}-prompt.txt').write_text(prompt,encoding='utf-8')
    (SOURCE/f'idle-motion-{ident}-{kind}-prompt.txt').write_text(prompt,encoding='utf-8')
    return {'id':ident,'kind':kind,'prompt':prompt,'references':[str(ref),str(OUT/'references/assets/approved-player.png'),str(OUT/'references/D-game.png')]}


def prepare(blue_only=False):
    OUT.mkdir(parents=True,exist_ok=True);shutil.copytree(PRIOR/'references',OUT/'references',dirs_exist_ok=True)
    shutil.copy2(SPEC_PATH,OUT/'motion-spec.json')
    if blue_only:
        jobs=[save_job('I4-blue','key',BLUE_EDIT,PRIOR/'I4-blue/enlarged/idle-4x.png')]
    else:
        jobs=[]
        for m in spec()['assets']:
            ident=m['id'];target=OUT/ident/'raw/key.png' if ident=='I4-blue' else PRIOR/ident/'enlarged/idle-4x.png'
            special='Blue sibling is now a long-distance ink caster with a small matte ink bead over one open palm, NOT a bow assistant. Keep it and the new3/4-front chest/mask read. ' if ident=='I4-blue' else ''
            identity='Subject '+ident+' '+m['name']+'. Native working visible height '+str(m['height'])+'px. '+special+m['motion']
            idle=SHARED+identity+'''
Deliver EXACTLY FOUR full character KEY DRAWINGS in ONE HORIZONTAL ROW of FOUR equal-size cells, read left-to-right. Wide4:1 canvas layout, each figure entirely inside its own quarter, same camera and scale, baseline/foot coordinates and center registration identical in all four cells, large blank gutters. These are FOUR subtle INHALATION keys of ONE idle cycle: frame0 relaxed reference; frame1 tiny breath begins; frame2 slightly fuller chest/cloth; frame3 fullest but still very small breath. They will be played0,1,2,3,2,1 so do NOT put an exhale or a reset in frame3. Draw genuine small changed cloth/hand/chest poses, not4 identical clones or whole-image translations/scalings. Limb lengths, head size, weapon/tool shape and overall volume never morph. Planted feet do not step, slide or disappear. Changes should be about1-2 native pixels, not exaggerated gestures. Face LEFT with a slight front plane of torso visible as in the edit target; this is gameplay idle, not turning toward camera between frames. For B3 retain FIVE profile support contacts in EACH frame. Do not add front/back/alternate angles to this row. No motion blur, trails or attack effects. TRUE TRANSPARENT canvas.'''
            views=SHARED+identity+'''
Deliver EXACTLY THREE complete static character design VIEWS in ONE HORIZONTAL ROW of THREE equal-size cells: LEFT cell FRONT view facing viewer with front of mask/chest visible; CENTER cell THREE-QUARTER FRONT view facing toward LEFT with both front and side visible; RIGHT cell clean LEFT-FACING SIDE view. These are distinct viewpoints, not3 almost identical profiles. All three are relaxed IDLE, with same height, head-to-body ratio, tools and costume. Keep the same stance family and no attack action. Perspective foreshortening is allowed in front view, but do not add/reorder anatomy to make all limbs visible; occluded supports remain the same five for B3. For M3 the solid long attached needle points toward viewer in front view so it foreshortens naturally; it must not become an extra horn, mouth gun or held weapon. For A3 preserve the SAME anatomical giant arm in every view, do not mirror it to a different arm. Wide3:1 layout, each view fully inside its cell with generous transparent margins and consistent foot baseline. No animation, duplicates, labels or grid. TRUE TRANSPARENT background.'''
            jobs.extend([save_job(ident,'idle',idle,target),save_job(ident,'views',views,target)])
    (OUT/'jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'out':str(OUT),'jobs':[(j['id'],j['kind']) for j in jobs]},ensure_ascii=False))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare-blue','prepare']);a=p.parse_args();prepare(a.command=='prepare-blue')
