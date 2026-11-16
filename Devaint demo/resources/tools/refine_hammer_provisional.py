"""Match approved ink colors and correct boot alignment without redrawing poses."""
from pathlib import Path
from collections import Counter
import argparse
import base64
import hashlib
import io
import json
import subprocess

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy import ndimage as ndi
from prepare_hammer_provisional import check_branch, dump, foot_anchor, game_frame, checker
from assets import Store

ROOT=Path(__file__).resolve().parents[1]
ASSET=ROOT/'characters/ink/weapons/hammer'
PRIOR='user-hammer-basic-provisional-20261101'
PALETTE_FRAME=ASSET/'runtime/user-hammer-normal-reused-20261031/idle_01.png'
EFFECT_FRAMES={'B53','B55','A20','A21','A22','A24'}


def match_colors(image,palette,key,effect_frames=None):
    a=np.array(image)
    active=a[:,:,3]>0
    rgb=a[:,:,:3].astype('int32')
    # Preserve the intentional neutral impact/trail polygons. Gray matte and
    # texture in a character/weapon are not added to the character palette.
    fx=np.zeros(active.shape,bool)
    if key in (EFFECT_FRAMES if effect_frames is None else effect_frames):
        neutral=active&(rgb.max(axis=2)-rgb.min(axis=2)<20)&(rgb.mean(axis=2)>60)
        labels,_=ndi.label(neutral,structure=np.ones((3,3)))
        sizes=np.bincount(labels.ravel())
        fx=neutral&(sizes[labels]>=120)
    selected=active&~fx
    original=rgb[selected]
    pal=np.array(palette,dtype='int32')
    distances=((original[:,None,:]-pal[None,:,:])**2).sum(axis=2)
    a[selected,:3]=pal[distances.argmin(axis=1)].astype('uint8')
    a[~active]=0
    assert np.array_equal(a[:,:,3],np.array(image)[:,:,3])
    assert set(map(tuple,a[selected,:3])).issubset(set(palette))
    return Image.fromarray(a),int(fx.sum()),selected


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--destination',type=Path,required=True)
    args=ap.parse_args();check_branch()
    store=Store(ROOT)
    def verified(path):
        data=store.read({'asset':'characters.ink.weapons.hammer','path':path.relative_to(ROOT).as_posix()})
        return data
    palette_raw=verified(PALETTE_FRAME)
    ref=np.array(Image.open(io.BytesIO(palette_raw)).convert('RGBA'))
    counts=Counter(map(tuple,ref[ref[:,:,3]>0,:3]))
    palette=[tuple(map(int,rgb)) for rgb,n in counts.most_common()]
    assert len(palette)==6
    expected={(27,22,22),(253,238,218),(2,86,115),(210,179,150),(2,100,140),(235,165,22)}
    assert set(palette)==expected
    oldsource=ASSET/'source'/PRIOR
    an=json.loads(verified(oldsource/'animation.json'))
    framespec=an['frames']
    durations=[x['ms'] for seq in an['sequences'].values() for x in seq]
    newdurations=durations[:]
    # Redistribute the same 300ms second-hit lift rather than leaving a long
    # freeze on the raised-leg pose. Contact and clip-end times stay fixed.
    newdurations[6:9]=[80,100,120]
    assert sum(newdurations)==sum(durations)==1670
    assert sum(newdurations[6:9])==sum(durations[6:9])==300
    for contact_index in [4,10]:
        assert sum(newdurations[:contact_index])==sum(durations[:contact_index])
    out=args.destination.resolve();out.mkdir(parents=True,exist_ok=False)
    for folder in ['color-corrected-highres','original-frames','runtime/detail4x','runtime/native224','preview']:
        (out/folder).mkdir(parents=True)
    export=[];native=[];names=[];checks=[];images=[]
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',23)
    before_after=Image.new('RGB',(5*448,900),'#ebebeb');bd=ImageDraw.Draw(before_after)
    show={'B20':0,'B37':1,'A20':2,'A24':3,'A36':4}
    for i,spec in enumerate(framespec):
        key=spec['input'];name=spec['frame'];fid=f'{i+1:02d}-{key}'
        high=Image.open(io.BytesIO(verified(oldsource/'cleaned-highres'/f'{fid}.png'))).convert('RGBA')
        raw=verified(oldsource/'original-frames'/f'{fid}.png')
        (out/'original-frames'/f'{fid}.png').write_bytes(raw)
        original=np.array(Image.open(io.BytesIO(raw)).convert('RGB'))
        anchor=foot_anchor(original)
        expected_x=(555<=anchor[0]<=562) if key.startswith('B') else (565<=anchor[0]<=585)
        assert expected_x,(key,anchor)
        colored,fxcount,material=match_colors(high,palette,key)
        colored.save(out/'color-corrected-highres'/f'{fid}.png')
        detail=game_frame(colored,anchor,4);small=game_frame(colored,anchor)
        detail.save(out/'runtime/detail4x'/f'{name}.png')
        small.save(out/'runtime/native224'/f'{name}.png')
        export.append(detail);native.append(small);names.append(name)
        a=np.array(colored)
        checks.append({'frame':name,'input':key,'oldAnchor':spec['nativeFootAnchor'],'correctedAnchor':anchor,
            'sourceSha256':hashlib.sha256(raw).hexdigest(),'alphaAndPoseUnchanged':True,
            'materialColorsUseApprovedPaletteOnly':True,'preservedGrayEffectPixels':fxcount,
            'sourceHighres':f'color-corrected-highres/{fid}.png','durationMs':newdurations[i]})
        b=io.BytesIO();detail.save(b,format='PNG')
        images.append('data:image/png;base64,'+base64.b64encode(b.getvalue()).decode())
        if key in show:
            x=show[key]*448
            old=Image.open(ASSET/'runtime'/PRIOR/'detail4x'/f'{name}.png').convert('RGBA')
            for row,im in enumerate([old,detail]):
                bg=Image.new('RGBA',(448,320),'#30343b')
                bg.alpha_composite(im.crop((128,160,800,640)).resize((448,320),Image.Resampling.LANCZOS))
                y=40+row*415
                before_after.paste(bg.convert('RGB'),(x,y))
                bd.text((x+12,y-31),key+(' / 기존' if row==0 else ' / 색상·정렬 보정'),font=font,fill='black')
    before_after.save(out/'preview/before-after.png')
    for size,frames,sub in [((896,768),export,'detail4x'),((224,192),native,'native224')]:
        sheet=Image.new('RGBA',(size[0]*5,size[1]*3))
        for i,im in enumerate(frames):sheet.alpha_composite(im,(i%5*size[0],i//5*size[1]))
        sheet.save(out/'runtime'/sub/'sheet-15.png')
    for bgname,color in [('dark','#30343b'),('white','white'),('checker',None)]:
        seq=[];close=[]
        for im in export:
            bg=checker(im.size,32) if color is None else Image.new('RGBA',im.size,color)
            bg.alpha_composite(im);seq.append(bg.convert('RGB'));close.append(bg.convert('RGB').crop((128,160,800,640)))
        for variant,frames in [('',seq),('-close',close)]:
            frames[0].save(out/'preview'/f'combo{variant}-{bgname}.gif',save_all=True,append_images=frames[1:],duration=newdurations,loop=0,disposal=2,optimize=False)
    export[1].crop((128,160,800,640)).resize((1344,960),Image.Resampling.NEAREST).save(out/'preview/held-detail-transparent.png')
    palette_pic=Image.new('RGB',(1200,190),'white');pd=ImageDraw.Draw(palette_pic)
    labels={(27,22,22):'몸·얼굴·망치',(253,238,218):'로브·후드',(210,179,150):'로브 그림자',
        (2,86,115):'목도리 어두운 색',(2,100,140):'목도리 밝은 색',(235,165,22):'눈·장식'}
    for i,c in enumerate(palette):
        x=i*200;pd.rectangle((x+8,8,x+192,103),fill=c)
        pd.text((x+8,111),labels[c],font=font,fill='black')
        pd.text((x+8,147),'#%02X%02X%02X'%c,font=font,fill='black')
    palette_pic.save(out/'preview/approved-palette.png')
    sequences={'attack_1':[{'frame':names[i],'ms':newdurations[i]} for i in range(6)],
        'attack_2':[{'frame':names[i],'ms':newdurations[i]} for i in range(6,15)]}
    dump(out/'editor.json',{'schemaVersion':1,'kind':'hammer-color-motion-refined','weapon':'hammer',
        'width':896,'height':768,'sourceFrames':names,'names':names,'images':images,'ms':newdurations,
        'sequences':sequences,'palette':['#%02x%02x%02x'%c for c in palette],
        'note':'Approved RGB palette; native source silhouette unchanged; two ankle-based boot registration. No spatial pixelization or Unity deployment.'})
    dump(out/'animation.json',{'canvas':[224,192],'pivot':[96,151],'ppu':32,
        'detailExport':{'canvas':[896,768],'pivot':[384,604],'ppu':128},'scale':0.145,
        'paletteReference':PALETTE_FRAME.relative_to(ROOT).as_posix(),'paletteReferenceSha256':hashlib.sha256(palette_raw).hexdigest(),
        'approvedPalette':['#%02x%02x%02x'%c for c in palette], 'sequences':sequences,'reviewPauseMs':650,
        'motion':'Actual two boots define frame registration; natural foot shapes retained. Second-hit lift redistributed 80/100/120ms, same 300ms total.',
        'color':'Only material RGB mapped to the six approved colors; intentional neutral attack effects preserved. No spatial grid conversion.',
        'approval':'pending-user-review','gameIntegration':'not-deployed','frames':checks})
    dump(out/'validation.json',{'frames':15,'totalDurationMs':1670,'paletteReadFromActualApprovedPng':True,
        'poseAndAlphaUnchangedBeforeExport':True,'bothContactTimesUnchanged':True,
        'sourceAssetUnchanged':True,'noUnityOrGameplayChanges':True,'framesChecked':checks})
    check_branch();print(json.dumps({'destination':str(out),'frames':15,'palette':['#%02x%02x%02x'%c for c in palette],
        'correctedAnchors':[x['input'] for x in checks if list(x['correctedAnchor'])!=x['oldAnchor']]},ensure_ascii=False))


if __name__=='__main__':main()
