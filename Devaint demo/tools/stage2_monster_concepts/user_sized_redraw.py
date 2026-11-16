"""Regenerate the selected identities using the user's exported visible-height study."""
from pathlib import Path
import json, shutil
from PIL import Image
from style_unification import ROOT, SOURCE, ITEMS, FONT, sha

PRIOR=ROOT/'outputs/waterway/style-unified-20261031'
OUT=ROOT/'outputs/waterway/user-sized-redraw-20261031'
CONFIG_PATH=SOURCE/'user-size-reference.json'
COMMON='''Use case: style-transfer / identity-preserve. Redraw the SAME selected monster for a USER-SPECIFIED visible pixel height in a 960x540 side-scrolling game.
Image1 is the monster EDIT TARGET: keep its identity, whole-body proportions, side-facing pose, silhouette topology, major holes, limb/contact count, facial design, weapon and costume. Image2 is approved player STYLE/DENSITY reference: its actual visible art is66x64 pixels in a192x160 transparent canvas. Do not redraw or insert the player. Image3 is the original sewer background for palette/readability reference only, never redraw or include it.
The user has manually set the desired height. It means the ENTIRE visible monster, including raised tools, long cloth ends and the whole sibling pair. Respect the stated total height; do not normalize all monsters to64px, make the boss larger automatically, or change body proportions to fake the size.
This is a NEW PIXEL REDRAW for that working height, not a blurry enlargement or literal nearest-neighbor doubling of the previous small sprite. Design the pixel clusters at the new density: a logical pixel unit comparable to the player's pixel unit; clean1-3px edge steps, economical small face/hand clusters, broad uninterrupted material planes. At larger heights use a few purposeful medium clusters to articulate the existing forms without introducing finer texture, decorations, extra rips or extra equipment. Display the completed pixel drawing enlarged on the output canvas for inspection; it will be sampled down to the requested visible height.
OUTLINE-FREE color-block rendering. NO black/color perimeter strokes, beveled borders, bright rim, glow, smoothing, 3D render, gradients, realistic cloth, speckles or tiny scratches. The material fill directly touches transparency. At most2-3 broad flat shade planes per material, restrained warm bone lights, shared cool grey-blue shadows and charcoal-violet ink body gaps. Dark broad ink masses and face openings are allowed, not tracing lines. Preserve the target's individual color identity and a broad light/warm body plane that contrasts with dark blue-grey/green-grey sewer and black foreground. No neon or eye-only contrast solution.
Exactly ONE full isolated monster (or exactly the original joined sibling pair for I4), LEFT-facing side profile, centered with safe transparent margins on all four sides. Preserve all tips and feet fully. True TRANSPARENT background. No labels, text, borders, ground, shadow, effects, projectiles, duplicate views, animations or sprite sheet. No new monster design or new combat system.
'''


def config():
    data=json.loads(CONFIG_PATH.read_text(encoding='utf-8'))
    assert data['schema']=='stage2-monster-size-study-v1' and (data['width'],data['height'])==(960,540)
    assert {m['id'] for m in data['monsters']}==set(ITEMS) and len(data['monsters'])==7
    for m in data['monsters']:
        assert type(m['height']) is int and 4<=m['height']<=400
        assert type(m['visible']) is bool
    return data


def prepare():
    data=config();chosen={m['id']:m for m in data['monsters']};OUT.mkdir(parents=True,exist_ok=True)
    shutil.copytree(PRIOR/'references',OUT/'references',dirs_exist_ok=True)
    shutil.copy2(CONFIG_PATH,OUT/'references/user-sizes.json')
    shutil.copy2(SOURCE/'current-choices.json',OUT/'choices.json')
    records=[]
    for ident,m in ITEMS.items():
        p=OUT/ident
        for sub in ['before','references','raw','cutouts','previews']:(p/sub).mkdir(parents=True,exist_ok=True)
        for src,dst in [(PRIOR/ident/'cutouts/style-enlarged.png','design-cutout.png'),(PRIOR/ident/'cutouts/style-scale-study.png','old-scale-study.png'),(PRIOR/ident/'raw/generated.png','style-generation-original.png'),(PRIOR/ident/'prompt.txt','previous-prompt.txt')]:shutil.copy2(src,p/'before'/dst)
        shutil.copy2(p/'before/design-cutout.png',p/'references/edit-target.png')
        shutil.copy2(OUT/'references/assets/approved-player.png',p/'references/approved-player.png')
        shutil.copy2(OUT/'references/D-game.png',p/'references/D-game.png')
        old=Image.open(p/'before/design-cutout.png');height=chosen[ident]['height'];width=round(old.width*height/old.height)
        note=m[4].replace('Boss128px-height is a size study only.','Whole-pair height is the user-specified127px study; preserve both figures and the connecting ink bridge.')
        if ident=='B3':note+=' Preserve ALL FIVE visible ground-contact feet, including the small inner brown support behind the large copper foreleg. It belongs to the approved silhouette; do not remove it or add a sixth leg.'
        prompt=COMMON+f'\nTarget: {ident} {m[0]}, {m[1]}. USER visible whole-figure height: {height}px, approximate width {width}px from preserved aspect ratio. Relative to64px-tall player: {height/64:.3f} times total height.\nIdentity and palette: '+note
        (p/'prompt.txt').write_text(prompt,encoding='utf-8');(SOURCE/f'user-sized-{ident}-prompt.txt').write_text(prompt,encoding='utf-8')
        records.append({'id':ident,'name':m[0],'role':m[1],'requestedVisibleHeight':height,'estimatedWidth':width,'savedPosition':{'x':chosen[ident]['x'],'foot':chosen[ident]['foot']},'visibleInSavedScene':chosen[ident]['visible'],'beforeStyleSha256':sha(p/'before/design-cutout.png'),'beforeRawSha256':sha(p/'before/style-generation-original.png'),'referenceSource':str(PRIOR/ident),'status':'user-sized-redraw-candidate'})
    (OUT/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'out':str(OUT),'requestedHeights':{m['id']:m['height'] for m in data['monsters']},'configSha256':sha(CONFIG_PATH)},ensure_ascii=False))


if __name__=='__main__':prepare()
