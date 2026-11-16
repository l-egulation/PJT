"""Assemble the user's hammer on preserved, approved body frames.

Image generation supplies the black material reference. Native-grid snapping
retains the supplied silhouette, then this assembler moves only weapon layers.
It never changes Unity metadata, animation timing, or combat definitions.
Body shading, body silhouette and authored ground ink are reused verbatim.
"""
from pathlib import Path
import argparse
import base64
import hashlib
import io
import json
import math
import shutil
import subprocess

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

N = Image.Resampling.NEAREST
BLACK = (27, 22, 22, 255)
SIZE = (224, 192)
OFFSET = (32, 30)
PIVOT = (96, 151)
MOTIONS = {'대기': 'idle', '걷기': 'walk', '달리기': 'run', '점프': 'jump',
           '낙하착지': 'fall-land', '대시': 'dash', '죽음_2': 'death'}
SET = 'user-hammer-normal-20261031'
REFINE = False
RIGID = False
# Head-local planar faces in native pixels, with tiny straight chamfers.
RIGID_HEAD = [(32,-19),(49,-19),(51,-17),(51,21),(49,23),
              (33,23),(31,21),(31,6),(29,4),(29,-2),(31,-4),(31,-18)]
RIGID_HANDLE = [(-6,-3),(32,-3),(32,3),(-6,3)]


def prepare_body(body):
    """Preserve every visible approved body/ink pixel; normalize hidden RGB."""
    a=np.array(body.convert('RGBA'))
    a[a[:,:,3]==0]=0
    return Image.fromarray(a)


def body_canvas(body):
    canvas=Image.new('RGBA',SIZE)
    canvas.alpha_composite(prepare_body(body),OFFSET)
    return canvas


def approved_weapon_mask(reference, body):
    original=np.array(reference.convert('RGBA'))
    base=np.array(body.convert('RGBA'))
    original_black=(original[:,:,3]>0)&np.all(original[:,:,:3]==BLACK[:3],axis=2)
    body_black=(base[:,:,3]>0)&np.all(base[:,:,:3]==BLACK[:3],axis=2)
    return original_black & ~body_black


def reuse_approved_death_weapon(layer, body, reference_pixels, first_reference_pixels, first_new_pixels):
    """Reuse the approved weapon's visible-ink progression, without squashing.

    The new rigid silhouette moves into the existing ink and is occluded at
    the original ground row. Body, pool, timing and the final pose are untouched.
    """
    if reference_pixels==0:
        return Image.new('RGBA',SIZE)
    base=np.array(body.convert('RGBA'))
    body_black=(base[:,:,3]>0)&np.all(base[:,:,:3]==BLACK[:3],axis=2)
    target=round(first_new_pixels*reference_pixels/first_reference_pixels)
    pixels=np.array(layer)
    candidates=[]
    for dy in range(65):
        shifted=np.zeros_like(pixels)
        if dy: shifted[dy:]=pixels[:-dy]
        else: shifted[:]=pixels
        shifted[153:]=0
        visible=(shifted[:,:,3]>0)&~body_black
        candidates.append((abs(int(visible.sum())-target),dy,shifted))
    _,_,chosen=min(candidates,key=lambda c:(c[0],c[1]))
    return Image.fromarray(chosen)




def rigid_weapon_layer(hand, angle, depth=1.0):
    layer = Image.new('RGBA',SIZE)
    draw = ImageDraw.Draw(layer)
    hx,hy=hand[0]+OFFSET[0],hand[1]+OFFSET[1]
    theta=math.radians(angle)
    co,si=math.cos(theta),math.sin(theta)
    def placed(points):
        return [(round(hx+co*x-si*y*depth),round(hy+si*x+co*y*depth)) for x,y in points]
    draw.polygon(placed(RIGID_HANDLE),fill=BLACK)
    draw.polygon(placed(RIGID_HEAD),fill=BLACK)
    pixels=np.array(layer)
    mask=pixels[:,:,3]>0
    neighbors=ndi.convolve(mask.astype(int),np.ones((3,3),int),mode='constant')
    pixels[mask & (neighbors<=3)]=0
    return Image.fromarray(pixels)




def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def encode(im):
    out = io.BytesIO()
    im.save(out, format='PNG')
    return 'data:image/png;base64,' + base64.b64encode(out.getvalue()).decode()


def protected_head(body):
    a = np.array(body)
    black = np.all(a[:, :, :3] == BLACK[:3], axis=2) & (a[:, :, 3] > 0)
    labels, count = ndi.label(black)
    groups = []
    for k in range(1, count + 1):
        yy, xx = np.where(labels == k)
        if len(yy) >= 80:
            groups.append((int(yy.min()), int(yy.max()), k))
    if not groups:
        return np.zeros(black.shape, bool)
    top, bottom, face_id = min(groups)
    beige = np.all(a[:, :, :3] == [253, 238, 218], axis=2) | np.all(a[:, :, :3] == [210, 179, 150], axis=2)
    return (labels == face_id) | (beige & (a[:, :, 3] > 0) & (np.indices(black.shape)[0] <= bottom))




def weapon_layer(icon, hand, angle):
    # Logical 32px source: lower handle is (7.5,25.5), head center (19.5,13.5).
    # Length is measured from the grip to the head, independently of body size.
    gx, gy = (6.5, 24.0) if REFINE else (7.5, 25.5)
    scale = 2.35
    phi = math.atan2(-12, 12) - math.radians(angle)
    c, s = math.cos(phi) / scale, math.sin(phi) / scale
    hx, hy = hand[0] + OFFSET[0], hand[1] + OFFSET[1]
    if RIGID:
        # Rasterize straight faces directly on the final grid. A Gaussian mask
        # made the previous head's corners read as soft, rounded lobes.
        return rigid_weapon_layer(hand,angle)
    if not REFINE:
        return icon.transform(SIZE, Image.Transform.AFFINE,
                              (c, -s, gx-c*hx+s*hy, s, c, gy-s*hx-c*hy), resample=N)
    # Filter coverage before snapping onto the native grid. Rotating already
    # enlarged coarse pixel stairs with nearest-neighbor created the teeth.
    coverage = ndi.gaussian_filter(np.array(icon.getchannel('A'), dtype=float)/255, .58)
    source = Image.fromarray(np.uint8(coverage*255)).resize((512,512), Image.Resampling.BICUBIC)
    factor = 4
    a = c*16; b = s*16
    hi = source.transform((SIZE[0]*factor,SIZE[1]*factor), Image.Transform.AFFINE,
        (a/factor,-b/factor,gx*16-a*hx+b*hy,b/factor,a/factor,gy*16-b*hx-a*hy),
        resample=Image.Resampling.BICUBIC)
    mask = ndi.gaussian_filter(np.array(hi.resize(SIZE,Image.Resampling.BOX),dtype=float)/255,.5) >= .5
    rgba = np.zeros((SIZE[1],SIZE[0],4),dtype=np.uint8)
    rgba[mask] = BLACK
    return Image.fromarray(rgba)


def main():
    global SET, REFINE, RIGID
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--workspace', type=Path, required=True)
    ap.add_argument('--reference', type=Path, required=True)
    ap.add_argument('--generated', type=Path, required=True)
    ap.add_argument('--set-id', default=SET)
    ap.add_argument('--refine', action='store_true')
    ap.add_argument('--reuse-placements', type=Path)
    ap.add_argument('--rigid', action='store_true')
    args = ap.parse_args()
    SET = args.set_id
    REFINE = args.refine
    RIGID = args.rigid
    root = Path(__file__).resolve().parents[1]
    repo = root.parent
    workspace = args.workspace.resolve()
    if subprocess.check_output(['git', '-C', str(repo), 'branch', '--show-current'], text=True).strip() != 'feat/story-unity-essentials':
        raise SystemExit('Main branch changed; stopped.')
    asset_root = root / 'characters/ink/weapons/hammer'
    source = asset_root / 'source' / SET
    runtime = asset_root / 'runtime' / SET
    preview = asset_root / 'preview' / SET
    if source.exists() or runtime.exists() or preview.exists():
        raise SystemExit('Output set already exists; refusing to replace it.')
    for path in [source, runtime, preview, source/'bodies', source/'weapon-layers']:
        path.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.reference, source/'user-hammer-original.png')
    shutil.copy2(args.generated, source/'imagegen-black-original.png')
    original = Image.open(args.reference).convert('RGBA')
    mask = original.resize((32, 32), N).getchannel('A')
    icon = Image.new('RGBA', (32, 32), BLACK)
    icon.putalpha(mask)
    icon.save(source/'hammer-native32.png')
    icon.resize((512, 512), N).save(preview/'hammer-black-16x.png')
    if RIGID:
        dump(source/'rigid-geometry.json', {'units':'native pixels', 'head':RIGID_HEAD,
             'handle':RIGID_HANDLE,'grip':[0,0],'material':'#1b1616',
             'contour':'straight faces, short planar chamfers; no Gaussian rounding'})
        weapon_layer(icon,[12,66],0).save(source/'hammer-rigid-native.png')
    master = workspace / '02_에셋/캐릭터/잉크/공통몸체/04_확정본'
    cfg = json.loads((master/'재생설정.json').read_text(encoding='utf-8'))['motions']
    placements_path = workspace/'02_에셋/캐릭터/잉크/무기/공통제작/03_작업중/오른손전환/배치정보.json'
    records = json.loads(placements_path.read_text(encoding='utf-8'))
    records = {(r['motion'], r['frame']): r for r in records if r['weapon'] == '검'}
    reuse = {r['frame']:r for r in json.loads(args.reuse_placements.read_text(encoding='utf-8'))['placements']} if args.reuse_placements else {}
    approved_paths=[asset_root/'runtime/approved-eleven/character'/f'F{34+i:03}.png' for i in range(12)]
    approved_deaths=[Image.open(path).convert('RGBA') for path in approved_paths]
    approved_source=source/'approved-death'
    approved_source.mkdir()
    for i,path in enumerate(approved_paths):
        shutil.copy2(path,approved_source/f'F{i+1:02}.png')
    first_body=body_canvas(Image.open(master/'죽음_2/01.png'))
    first_reference_pixels=int(approved_weapon_mask(approved_deaths[0],first_body).sum())
    first_new_pixels=None
    names, frames, times, placements, sequences, checks = [], [], [], [], {}, []
    for motion, key in MOTIONS.items():
        steps = []
        for i in range(12):
            input_path = master/'벽차기_1차_20261025/jump_master'/f'F{i+1:02}.png' if motion == '점프' and i < 7 else master/motion/f'{i+1:02}.png'
            fid = f'{key}_{i+1:02}'
            shutil.copy2(input_path, source/'bodies'/f'{fid}.png')
            body = prepare_body(Image.open(input_path))
            rec = records[motion, i+1]
            a = np.array(body)
            hand = rec['palm']
            released = rec['released']
            # Preserve the approved hand path. The heavy head stays forward of
            # the face rather than automatically inheriting a sword's lean.
            desired = rec['angle_deg']
            if key == 'run':
                desired = max(-15, desired + 38)
            if key in ('jump', 'fall-land'):
                desired = max(-18, desired + 15)
            if key == 'dash':
                desired = [0, 0, 3, 6, 6, 6, 6, 6, 3, 0, -2, 0][i]
            protect = protected_head(body)
            candidates = [reuse[fid]['angle']] if fid in reuse else [desired] if released else sorted(range(-75, 76), key=lambda x: abs(x-desired))
            choices = []
            for angle in candidates:
                layer = weapon_layer(icon, hand, angle)
                box = layer.getbbox()
                if box is None or box[0] < 2 or box[1] < 2 or box[2] > SIZE[0]-2 or box[3] > SIZE[1]-2:
                    continue
                la = np.array(layer)
                hit = np.count_nonzero((la[30:158, 32:160, 3] > 0) & protect)
                ground_penalty = max(0, box[3]-PIVOT[1]) if key not in ('jump', 'fall-land') else 0
                choices.append((hit*30 + ground_penalty*50 + abs(angle-desired), angle, layer))
                if hit == 0 and ground_penalty == 0:
                    break
            if not choices:
                raise ValueError('No valid hammer placement: '+fid)
            _, angle, layer = min(choices, key=lambda x: x[0])
            if key=='death':
                reference_body=body_canvas(body)
                reference_pixels=int(approved_weapon_mask(approved_deaths[i],reference_body).sum())
                if i==0:
                    first_new_pixels=int(approved_weapon_mask(layer,reference_body).sum())
                if i>=3:
                    angle=reuse.get('death_03',{}).get('angle',-9)
                    layer=weapon_layer(icon,hand,angle)
                    layer=reuse_approved_death_weapon(layer,reference_body,reference_pixels,
                                                      first_reference_pixels,first_new_pixels)
                released=False
            if released:
                box = layer.getbbox()
                if box[3] > PIVOT[1]:
                    moved = Image.new('RGBA', SIZE)
                    moved.alpha_composite(layer, (0, PIVOT[1]-box[3]))
                    layer = moved
            canvas_body = Image.new('RGBA', SIZE)
            canvas_body.alpha_composite(body, OFFSET)
            result = canvas_body.copy()
            if released:
                result = layer.copy()
                result.alpha_composite(canvas_body)
            else:
                result.alpha_composite(layer)
                # Composite the original near hand and head in front, without
                # inventing skin, fingers, or repainting the approved body.
                restore = np.zeros_like(a)
                hx, hy = rec['master_palm']
                glove = np.zeros(a.shape[:2], bool)
                glove[max(0,hy-3):hy+4, max(0,hx-3):hx+4] = True
                glove &= np.all(a[:, :, :3] == BLACK[:3], axis=2) & (a[:, :, 3] > 0)
                restore[protect | glove] = a[protect | glove]
                result.alpha_composite(Image.fromarray(restore), OFFSET)
            normalized = np.array(result)
            normalized[normalized[:, :, 3] == 0] = 0
            result = Image.fromarray(normalized)
            result.save(runtime/f'{fid}.png')
            if key=='death' and i>=7:
                # The approved reference contains no separate weapon here.
                # Preserve these entire original PNGs byte-for-byte.
                shutil.copy2(approved_paths[i],runtime/f'{fid}.png')
                result=Image.open(runtime/f'{fid}.png').convert('RGBA')
            layer.save(source/'weapon-layers'/f'{fid}.png')
            ra, ba, wa = np.array(result), np.array(canvas_body), np.array(layer)
            safe = wa[:, :, 3] == 0
            assert np.array_equal(ra[safe], ba[safe]), fid
            assert set(np.unique(ra[:, :, 3])) <= {0, 255}, fid
            assert not np.any(ra[0,:,3]) and not np.any(ra[-1,:,3]) and not np.any(ra[:,0,3]) and not np.any(ra[:,-1,3]), fid
            checks.append({'frame':fid, 'approvedBodyOutsideWeaponExact':True, 'binaryAlpha':True,
                           'notClipped':True, 'bodyRecolored':False,'bodyInkPreserved':True})
            ms = cfg[motion]['durations_ms'][i]
            names.append(fid); frames.append(result); times.append(ms)
            placements.append({'frame':fid, 'hand':hand, 'angle':angle, 'released':released,
                               'bodySource':input_path.relative_to(workspace).as_posix()})
            steps.append({'frame':fid, 'ms':ms, 'motion':key})
        sequences[key] = steps
    sequences['all'] = [step for key in MOTIONS.values() for step in sequences[key]]
    dump(source/'editor.json', {'schemaVersion':1, 'kind':'hammer-user-design-normal', 'weapon':'hammer',
        'width':SIZE[0], 'height':SIZE[1], 'sourceFrames':names, 'names':names,
        'images':[encode(im) for im in frames], 'ms':times, 'sequences':sequences,
        'palette':['#1b1616','#eba516','#fdeeda','#d2b396','#02648c','#025673'],
        'note':'User hammer silhouette, black material, preserved approved body frames. New attacks are separate pending Aether output.'})
    dump(source/'animation.json', {'canvas':SIZE, 'pivot':PIVOT, 'ppu':32, 'bodyOffset':OFFSET,
        'bodySourcePivot':[64,121], 'weaponGripNative':[6.5,24.0] if REFINE else [7.5,25.5], 'weaponScale':2.35,
        'sequences':sequences, 'placements':placements,
        'deathWeapon':'approved-eleven original visible-weapon progression; frames 8-12 are original approved PNGs',
        'bodyPolicy':'approved body and ground ink reused without recoloring, cleanup, rescaling, or added motion',
        'silhouettePolicy':'Rigid planar faces with short straight chamfers, native polygon rasterization.' if RIGID else 'Filtered original silhouette coverage; crisp native pixel contour without rotation teeth.' if REFINE else 'Original alpha snapped to its 32px logical grid; generated black reference normalized to approved #1b1616.'})
    for suffix, bg in [('transparent',None), ('white','white')]:
        im = frames[0].resize((SIZE[0]*8,SIZE[1]*8), N)
        if bg:
            background = Image.new('RGBA', im.size, bg)
            background.alpha_composite(im)
            im = background.convert('RGB')
        im.save(preview/f'hammer-held-8x-{suffix}.png')
    frames[0].save(preview/'aether-start-native.png')
    frames[0].save(preview/'aether-end-native.png')
    for name in ['start','end']:
        shutil.copy2(preview/'hammer-held-8x-transparent.png',preview/f'aether-{name}-8x.png')
    lookup = dict(zip(names, frames))
    for key, steps in sequences.items():
        shown=[]
        for step in steps:
            im=lookup[step['frame']].resize((672,576), N)
            bg=Image.new('RGB', im.size, 'white'); bg.paste(im, (0,0), im)
            shown.append(bg)
        palette=shown[0].quantize(colors=32)
        shown=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in shown]
        shown[0].save(preview/f'hammer-{key}.gif',save_all=True,append_images=shown[1:],
                      duration=[s['ms'] for s in steps],loop=0,disposal=2)
    board=Image.new('RGB',(1344,1152),'#e4e4e4')
    draw=ImageDraw.Draw(board)
    for j,fid in enumerate(['idle_01','walk_05','run_06','jump_04','fall-land_05','dash_06','death_06','death_12']):
        im=lookup[fid].resize((336,288),N)
        x=j%4*336; y=j//4*576
        board.paste(im,(x,y),im); draw.text((x+12,y+300),fid,fill='black')
    board.save(preview/'normal-motion-contact.png')
    # Register only newly created files, retaining every unrelated source entry.
    asset=json.loads((asset_root/'asset.json').read_text(encoding='utf-8'))
    catalog=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
    generated=json.loads((root/'generated-assets.json').read_text(encoding='utf-8'))
    payloads=json.loads((root/'.lfs-payloads.json').read_text(encoding='utf-8'))
    files=sorted(p for directory in [source,runtime,preview] for p in directory.rglob('*') if p.is_file())
    for path in files:
        asset['files'].append(path.relative_to(asset_root).as_posix())
        raw=path.read_bytes()
        generated['files'].append({'path':path.relative_to(root).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
    asset.setdefault('generatedSets',{})[SET]={'source':source.relative_to(asset_root).as_posix(),
        'runtime':runtime.relative_to(asset_root).as_posix(), 'preview':preview.relative_to(asset_root).as_posix(),
        'approval':'user-requested-weapon-replacement; pending-user-visual-review',
        'gameIntegration':'prepared-not-playtested', 'newAttacks':'not-included'}
    for entry in catalog['assets']:
        if entry['id']==asset['id']: entry['fileCount']=len(asset['files'])
    payloads.append((source/'editor.json').relative_to(root).as_posix())
    for path,value in [(asset_root/'asset.json',asset),(root/'catalog.json',catalog),
                       (root/'generated-assets.json',generated),(root/'.lfs-payloads.json',payloads)]:
        dump(path,value)
    log=repo/('outputs/hammer-refine-20261031' if REFINE else 'outputs/hammer-normal-20261031')
    log.mkdir(parents=True,exist_ok=True)
    dump(log/'assembly-validation.json',{'frames':checks,'originalPreserved':True,'generatedOriginalPreserved':True})
    print(json.dumps({'frames':len(frames),'runtime':str(runtime),'preview':str(preview),'canvas':SIZE,'pivot':PIVOT},ensure_ascii=True))


if __name__ == '__main__':
    main()
