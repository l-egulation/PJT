"""Prepare preserved references and prompts; never calls an image API."""
from pathlib import Path
import json, shutil, hashlib
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/waterway/monster-concepts-v3-20261031'
REF = ROOT / 'outputs/waterway/parallax-kit-v3-20261031'

from catalog import SETS, PALETTES

COMMON = '''Use case: stylized-concept.
Asset type: color monster design comparison board for a 2D side-scrolling pixel action game; static design concepts ONLY, not animation or a sprite sheet.
References: image 1 approved-player.png is a SIZE AND PIXEL-DENSITY reference only, preserve its identity and DO NOT redraw it or include it in the output. Its canvas is 192x160 but its actual art bounds are only 66x64. Image 2 C-game.png is the unmodified sewer environment reference, image 3 wall.png establishes environmental hue. DO NOT include any scenery in the output.
World: an inhabited city's underground waterway, damp civic maintenance space, no ancient temple or grand dungeon.
Medium: CRISP BLOCKY PIXEL ART in the same simple visual family as the supplied player. Each material uses only 3 flat value steps, few large pixel clusters, broad readable trunk/head/limb/weapon masses. Design as if native ordinary monsters are roughly 40-84 pixels high, elite 108 pixels, boss 190 pixels, then display enlarged. Absolutely avoid fine smooth concept painting, realistic texture, 3D, gradients, anti-aliased vector curves, rim glow, neon and heavy black outline. Edges use the nearby local material shadow color; small dark face cavities are allowed. Do not inherit the detailed texture density of the environment.
Visibility and COLOR VARIETY: each of the SEVEN monsters MUST have its OWN material-appropriate distinct main color family as specified individually below. They share pixel density and value grouping, NOT a uniform palette. Include purples, greys, oranges, yellows, pale blues, pale greens as individually directed. Preserve natural worn material colors. Broad light or medium-light body planes separate from the dark blue-grey/green-grey environment and black foreground. No neon and no luminous outline, no mostly dark green bodies. Do not solve visibility with tiny glowing eyes. Do NOT recolor every creature cream-yellow-red. The individual MATERIAL PALETTE below overrides every color word in the anatomical description.
Transparency: truly transparent empty space, no painted backplate, no vignette, no colored atmosphere, no brown mist, no glow, no light spill, no translucent shading outside silhouettes, NO cast shadows or floor shadows. Only the hard-edged pixel-art creatures themselves have opacity. Transparent negative space inside limbs and between monsters.
Composition: one landscape transparent design board, roughly 1536x1024. These are original characters derived from sewer OBJECTS, PLANTS, and ANIMALS, with surprising simple silhouettes. Do not turn the whole cast into reptiles, dinosaurs, humanoid guards or robot heads with glowing eyes. Some creatures have no conventional eyes at all; personality comes from stance, main material and working mouth or appendage. Exactly SEVEN full-body monsters, all strict SIDE PROFILE, facing LEFT, all feet visible, no front or three-quarter view. No repeated pose views. Very generous transparent space between figures, no overlap, no ground plane, NO TEXT, numbers, labels, panels or borders.
Layout: TOP ROW has three melee designs evenly separated at horizontal centers 17%, 50%, 83%, around y=24%, all wholly inside y=3%-43%. BOTTOM ROW has four designs: ranged 1 at x=12%, ranged 2 at x=35%, elite at x=58%, boss at x=84%; all wholly inside y=52%-96%. Respect column boundaries. Boss broad and large but contained in its right-hand quarter; elite larger than ordinary. Each monster is its own disconnected silhouette. Ordinary monsters should have different heights and proportions, not the same humanoid recolored. No projectiles or effects that could be mistaken for an eighth creature.
'''

def main():
 OUT.mkdir(parents=True,exist_ok=True)
 refout=OUT/'references'; refout.mkdir(exist_ok=True)
 paths=['README.md','separated-layers.png']+[f'{c}-game.png' for c in 'ABCDE']+['assets/approved-player.png','assets/far.png','assets/wall.png','assets/terrain-preview.png','assets/foreground.png','layout.json']
 sources=[]
 for rel in paths:
  src=REF/rel; dst=refout/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
  sources.append({'source':str(src),'copy':str(dst.relative_to(OUT)),'sha256':hashlib.sha256(src.read_bytes()).hexdigest()})
 player=Image.open(refout/'assets/approved-player.png').convert('RGBA');bbox=player.getchannel('A').getbbox()
 scale={'logicalScreen':[960,540],'playerCanvas':list(player.size),'playerAlphaBBox':list(bbox),'playerVisibleExtent':[bbox[2]-bbox[0],bbox[3]-bbox[1]],'note':'Monster dimensions are proposed visible artwork extents, not collision boxes, finalized sprite canvases or combat specifications.'}
 (OUT/'scale-reference.json').write_text(json.dumps(scale,ensure_ascii=False,indent=2),encoding='utf-8')
 for key,s in SETS.items():
  dst=OUT/key;dst.mkdir(exist_ok=True)
  for sub in ['raw','references','previews','cutouts']: (dst/sub).mkdir(exist_ok=True)
  for rel in ['assets/approved-player.png','C-game.png','assets/wall.png']:
   shutil.copy2(refout/rel,dst/'references'/Path(rel).name)
  parts=[COMMON,f'Set {key}: {s["title"]}. Direction: {s["direction"]}.']
  for i,m in enumerate(s['monsters'],1):
   row='TOP ROW melee '+str(i) if i<=3 else 'BOTTOM ROW '+str(i-3)
   parts.append(f'{key}{i} ({row}): {m[6]}\nMATERIAL PALETTE FOR {key}{i} (highest priority): {PALETTES[key][i-1]}')
  prompt='\n\n'.join(parts)
  (dst/'prompt.txt').write_text(prompt,encoding='utf-8')
  (Path(__file__).parent/f'prompt-{key}.txt').write_text(prompt,encoding='utf-8')
 (OUT/'references/sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
 (OUT/'catalog.json').write_text(json.dumps(SETS,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'out':str(OUT),'player':scale,'sets':list(SETS)},ensure_ascii=False,indent=2))

if __name__=='__main__': main()
