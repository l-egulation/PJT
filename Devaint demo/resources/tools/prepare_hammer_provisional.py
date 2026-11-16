"""Conservative, non-quantized cutouts of the selected Aether hammer frames.

Only explicit waist-band masks, white matting, and subpixel exterior noise are
edited. Original RGB remains unchanged in the rest of each high-res interior.
Writes into a new review directory; never writes Unity files or gameplay data.
"""
from pathlib import Path
import argparse
import base64
import io
import json
import hashlib
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent
INPUT = REPO / 'outputs/hammer-frame-candidates-20261101/candidate-1-weighted-lift-15'
# Center-lines follow the actual visible stripe, including its stepped slope.
# The masks end at occlusion boundaries rather than continuing through hands.
BAND_STROKES = {
    'B19': ([(506,509),(524,517),(541,521),(547,528)], 18),
    'B20': ([(506,506),(528,514),(552,515),(574,520),(589,520)], 20),
    'B37': ([(510,503),(531,510),(552,513),(578,513),(599,513),(616,512),(632,504)], 22),
    'B53': ([(521,512),(547,522),(572,527),(600,531),(616,532)], 22),
    'B55': ([(521,508),(539,516),(554,524),(563,526)], 18),
    'B60': ([(523,507),(540,514),(553,522)], 18),
    'A09': ([(605,521),(621,520),(633,516)], 12),
    'A11': ([(512,498),(533,498),(551,503),(571,502),(590,501),(600,499)], 16),
    'A14': ([(506,499),(530,496),(549,501),(573,498),(592,496),(612,491),(621,487)], 17),
    'A20': ([(537,511),(554,517),(572,527),(593,530)], 17),
    'A21': ([(543,521),(553,527),(560,531)], 18),
    'A22': ([(540,515),(552,520),(563,526)], 18),
    'A24': ([(541,519),(553,523),(564,530)], 18),
    'A27': ([(538,513),(550,519),(560,525)], 18),
    'A36': ([], 0),
}


def check_branch():
    branch = subprocess.check_output(['git','-C',str(REPO),'branch','--show-current'], text=True).strip()
    if branch != 'feat/story-unity-essentials':
        raise SystemExit('Branch changed; stopped: '+branch)


def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def remove_band(rgb, key):
    region = Image.new('L', (rgb.shape[1], rgb.shape[0]))
    points, width = BAND_STROKES[key]
    if points:
        draw=ImageDraw.Draw(region)
        draw.line(points, fill=255, width=width, joint='curve')
        for x,y in [points[0],points[-1]]:
            r=width//2
            draw.ellipse((x-r,y-r,x+r,y+r),fill=255)
    area = ndi.binary_dilation(np.array(region)>0,iterations=3)
    a = rgb.astype(float)
    dark = a.max(axis=2)<170
    gold = (a[:,:,0]>150)&(a[:,:,1]>75)&(a[:,:,1]<230)&(a[:,:,2]<a[:,:,0]*0.68)&(a[:,:,1]>a[:,:,2]+35)
    mask = area & (dark | gold)
    # Preserve the diagonal/side edge of the actual hammer, never a waist belt.
    yy, xx = np.indices(mask.shape)
    if key == 'B37':
        mask &= ~((xx>=582)&(xx<=600)&(yy>=522))
    if key == 'B20':
        mask &= ~((xx>=581)&(yy>=528))
        mask &= xx<596
    if key == 'A11':
        mask &= ~((xx>=582)&(xx<=596)&(yy>=510))
        mask &= xx<604
    if key == 'A14':
        mask &= ~((xx>=581)&(xx<=598)&(yy>=505))
    if key == 'A20':
        mask &= xx<603
    # Gold here belongs only to the unwanted buckle, including its AA edge.
    mask |= gold&area
    mask = ndi.binary_dilation(mask, iterations=2)&area
    beige = (a[:,:,0]>155)&(a[:,:,1]>120)&(a[:,:,2]>95)&(a[:,:,0]>a[:,:,1]+3)&(a[:,:,1]>a[:,:,2]+4)
    beige &= ~gold & ~ndi.binary_dilation(mask, iterations=3)
    if mask.any():
        nearest = ndi.distance_transform_edt(~beige, return_distances=False, return_indices=True)
        filled = rgb[nearest[0],nearest[1]]
        corrected = rgb.copy()
        corrected[mask] = filled[mask]
    else:
        corrected = rgb.copy()
    return corrected, mask


def cutout(rgb):
    # Beige cloth is colored even when bright; luminance-only white removal
    # would incorrectly eat its shadows and highlights.
    signal = 255-rgb.min(axis=2).astype(float)
    keep = signal>=22
    keep = ndi.median_filter(keep, size=3)
    labels, count = ndi.label(keep, structure=np.ones((3,3)))
    sizes = np.bincount(labels.ravel())
    keep &= sizes[labels]>=40
    # Edge pixels can be opaque gray from the original white matte even when
    # their contrast is high. Borrow pigment from four pixels inside the mask
    # only at this exterior band; do not quantize any interior colors.
    core = ndi.binary_erosion(keep,iterations=4)
    nearest = ndi.distance_transform_edt(~core, return_distances=False, return_indices=True)
    result = np.dstack([rgb.copy(), np.where(keep,255,0).astype('uint8')])
    fringe = keep & ~core
    result[fringe,:3] = rgb[nearest[0][fringe], nearest[1][fringe]]
    result[~keep] = 0
    return Image.fromarray(result), fringe


def foot_anchor(rgb):
    a=rgb.astype(int)
    yy,xx=np.indices(a.shape[:2])
    # The generated hem contains detached dark strokes. Select boots by their
    # two approved yellow ankle accents, not by the largest dark components.
    gold=(a[:,:,0]>140)&(a[:,:,1]>85)&(a[:,:,1]<215)&(a[:,:,2]<110)
    gold&=(a[:,:,0]>a[:,:,1]+15)&(yy>540)&(xx>350)&(xx<800)
    labels,count=ndi.label(ndi.binary_closing(gold,iterations=2))
    ankles=[]
    for n,sl in enumerate(ndi.find_objects(labels),1):
        if sl is None: continue
        ys,xs=sl
        area=int((labels[sl]==n).sum())
        if area>40:
            ankles.append((area,(xs.start,ys.start,xs.stop,ys.stop)))
    ankles=sorted(ankles,reverse=True)[:2]
    assert len(ankles)==2,ankles
    assert abs(sum(ankles[0][1][::2])/2-sum(ankles[1][1][::2])/2)>70,ankles
    feet=[]
    for _,(x0,y0,x1,y1) in ankles:
        top=max(0,y1-3);left=max(0,x0-30)
        boot=a[top:min(a.shape[0],y1+65),left:min(a.shape[1],x1+35)].max(axis=2)<85
        by,bx=np.where(boot)
        assert len(bx)>100,(x0,y0,x1,y1)
        feet.append((int(bx.min()+left),int(by.min()+top),int(bx.max()+left+1),int(by.max()+top+1)))
    return ((min(b[0] for b in feet)+max(b[2] for b in feet))/2, max(b[3] for b in feet))


def game_frame(im, anchor, factor=1):
    # Resample once, retaining natural colors/antialiasing. No palette or grid
    # quantization, skeleton reconstruction, or copying feet across frames.
    scale=0.145*factor
    sx,sy=anchor
    return im.transform((224*factor,192*factor),Image.Transform.AFFINE,
        (1/scale,0,sx-96*factor/scale,0,1/scale,sy-151*factor/scale),
        resample=Image.Resampling.BICUBIC)


def checker(size, cell=20):
    yy,xx=np.indices((size[1],size[0]))
    a=np.where((xx//cell+yy//cell)%2,205,235).astype('uint8')
    return Image.fromarray(np.dstack([a,a,a,np.full_like(a,255)]))


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--destination',type=Path,required=True)
    args=ap.parse_args();check_branch()
    out=args.destination.resolve();out.mkdir(parents=True,exist_ok=False)
    for sub in ['original','highres','game','game4x','masks','preview']:(out/sub).mkdir()
    plan=json.loads((INPUT/'selection.json').read_text(encoding='utf-8'))
    steps=[(1,k,ms) for k,ms in plan['hit1']]+[(2,k,ms) for k,ms in plan['hit2']]
    frames=[];details=[];checks=[];names=[];durations=[];sequences={'attack_1':[],'attack_2':[]}
    comparison=Image.new('RGB',(2240,960),'#eeeeee')
    cd=ImageDraw.Draw(comparison);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',24)
    show=['B20','B37','A11','A14','A21']
    for i,(hit,key,ms) in enumerate(steps):
        path=next(INPUT.glob(f'*-{key}.png'));raw=path.read_bytes()
        (out/'original'/f'{i+1:02d}-{key}.png').write_bytes(raw)
        rgb=np.array(Image.open(path).convert('RGB'))
        corrected,mask=remove_band(rgb,key)
        hi,fringe=cutout(corrected)
        Image.fromarray((mask*255).astype('uint8')).save(out/'masks'/f'{i+1:02d}-{key}-waist.png')
        hi.save(out/'highres'/f'{i+1:02d}-{key}.png')
        anchor=foot_anchor(rgb);game=game_frame(hi,anchor);detail=game_frame(hi,anchor,4)
        name=f'attack_{hit}_{sum(1 for h,_,_ in steps[:i+1] if h==hit):02d}'
        game.save(out/'game'/f'{name}.png');detail.save(out/'game4x'/f'{name}.png')
        frames.append(game);details.append(detail);names.append(name);durations.append(ms)
        sequences[f'attack_{hit}'].append({'frame':name,'ms':ms})
        a=np.array(hi);interior=ndi.binary_erosion(a[:,:,3]>0,iterations=3)&~mask&~fringe
        assert np.array_equal(a[interior,:3],rgb[interior]), key
        assert not np.any(a[a[:,:,3]==0]), key
        assert not np.any(a[0,:,3]) and not np.any(a[-1,:,3]), key
        checks.append({'frame':name,'input':key,'sourceSha256':hashlib.sha256(raw).hexdigest(),
            'waistPixelsEdited':int(mask.sum()),'interiorOutsideWaistExact':True,
            'transparentPixelsRgbZero':True,'nativeFootAnchor':anchor,'nativeCanvas':[1280,720],
            'gameCanvas':[224,192],'gamePivot':[96,151],'gamePpu':32,
            'detailCanvas':[896,768],'detailPivot':[384,604],'detailPpu':128})
        if key in show:
            j=show.index(key);x=j*448
            cd.text((x+12,8),key+' / 원본',font=font,fill='black')
            comparison.paste(Image.fromarray(rgb).crop((220,0,1100,720)).resize((440,360)),(x,40))
            bg=checker((440,360));bg.alpha_composite(hi.crop((220,0,1100,720)).resize((440,360)))
            cd.text((x+12,430),'허리띠 제거 / 투명 배경',font=font,fill='black')
            comparison.paste(bg.convert('RGB'),(x,465))
    comparison.save(out/'preview'/'before-after.png')
    for bgname,color in [('dark','#30343b'),('white','white'),('checker',None)]:
        imgs=[]
        for frame in details:
            enlarged=frame
            bg=checker(enlarged.size,32) if color is None else Image.new('RGBA',enlarged.size,color)
            bg.alpha_composite(enlarged);imgs.append(bg.convert('RGB'))
        imgs[0].save(out/'preview'/f'combo-{bgname}.gif',save_all=True,append_images=imgs[1:],duration=durations,loop=0,disposal=2,optimize=False)
    details[1].resize((1792,1536),Image.Resampling.NEAREST).save(out/'preview'/'held-8x-transparent.png')
    sheet=Image.new('RGBA',(224*5,192*3))
    for i,frame in enumerate(frames):sheet.alpha_composite(frame,(i%5*224,i//5*192))
    sheet.save(out/'game'/'sheet-15.png')
    encoded=[]
    for frame in details:
        b=io.BytesIO();frame.save(b,format='PNG');encoded.append('data:image/png;base64,'+base64.b64encode(b.getvalue()).decode())
    dump(out/'editor.json',{'schemaVersion':1,'kind':'hammer-provisional-basic-cleanup','weapon':'hammer',
        'width':896,'height':768,'sourceFrames':names,'names':names,'images':encoded,'ms':durations,
        'sequences':sequences,'note':'Provisional non-quantized cleanup. No Unity deployment. Timing is a visual review, not gameplay data.'})
    dump(out/'animation.json',{'canvas':[224,192],'pivot':[96,151],'ppu':32,'sequences':sequences,
        'scale':0.145,'policy':'No palette quantization; highres body RGB preserved outside waist and exterior fringe.',
        'detailExport':{'canvas':[896,768],'pivot':[384,604],'ppu':128,
            'purpose':'High-detail provisional sprites; same world size as 224x192/PPU32. No Unity metadata created or edited.'},
        'reviewPauseMs':650,'approval':'pending-user-review','gameIntegration':'not-deployed', 'frames':checks})
    dump(out/'validation.json',{'frames':len(frames),'timingMatchesSelectedPreview':durations==[ms for _,_,ms in steps],
        'totalDurationMs':sum(durations),'noGameplayDataEdited':True,'sourceFramesPreserved':True,'framesChecked':checks})
    check_branch();print(json.dumps({'destination':str(out),'frames':len(frames),'waistPixels':[x['waistPixelsEdited'] for x in checks]},ensure_ascii=False))


if __name__=='__main__':main()
