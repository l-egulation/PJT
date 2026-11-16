"""Clean the user-approved 14-frame slam without changing its airborne poses."""
from pathlib import Path
from collections import Counter
import argparse,base64,hashlib,io,json
import numpy as np
from scipy import ndimage as ndi
from PIL import Image,ImageDraw,ImageFont
from assets import Store
from prepare_hammer_provisional import check_branch,cutout,foot_anchor,game_frame,checker,dump
from refine_hammer_provisional import match_colors

ROOT=Path(__file__).resolve().parents[1]
ASSET=ROOT/'characters/ink/weapons/hammer'
INPUT=ASSET/'source/user-hammer-skill-selection-20261101'
BELTS={
 'F001':[],
 'F010':[(568,490),(598,489),(622,485)],
 'F018':[(510,481),(538,483),(563,483),(596,483),(624,477)],
 'F022':[(508,476),(539,479),(567,478),(597,476),(621,468)],
 'F032':[(510,481),(544,482),(575,483),(606,484),(632,488)],
 'F036':[(513,438),(550,449),(590,458),(640,466)],
 'F038':[(516,404),(548,425),(588,449)],
 'F039':[(504,399),(533,422),(562,445)],
 'F042':[(487,401),(523,426),(552,448)],
 'F050':[(490,435),(522,456),(551,474)],
 'F054':[(517,491),(539,504)],
 'F062':[(520,496),(541,508)],
 'F070':[(552,489),(587,486),(613,480)],
 'F090':[],
}


def remove_belt(rgb,key):
    region=Image.new('L',(1280,720));draw=ImageDraw.Draw(region)
    points=BELTS[key]
    if points:
        width=28 if key in {'F018','F022','F032'} else 24 if key=='F036' else 20
        draw.line(points,fill=255,width=width,joint='curve')
        r=width//2
        for x,y in [points[0],points[-1]]:draw.ellipse((x-r,y-r,x+r,y+r),fill=255)
    area=np.array(region)>0;a=rgb.astype(int)
    # In this source the unwanted waist stripe is tan/brown, not the real
    # black hammer handle or gold cuffs. Never erase either foreground part.
    brown=(a[:,:,0]>135)&(a[:,:,0]<230)&(a[:,:,1]>105)&(a[:,:,1]<210)&(a[:,:,2]>85)&(a[:,:,2]<190)
    brown&=(a[:,:,0]>a[:,:,1]+4)&(a[:,:,1]>a[:,:,2]+4)
    mask=area&brown
    mask=ndi.binary_dilation(mask,iterations=1)&area&(a.max(axis=2)>130)&(a[:,:,2]>85)
    beige=(a[:,:,0]>155)&(a[:,:,1]>125)&(a[:,:,2]>100)&(a[:,:,0]>a[:,:,1]+3)&(a[:,:,1]>a[:,:,2]+4)
    beige&=~ndi.binary_dilation(area,iterations=6)
    corrected=rgb.copy()
    if mask.any():
        nearest=ndi.distance_transform_edt(~beige,return_distances=False,return_indices=True)
        corrected[mask]=rgb[nearest[0][mask],nearest[1][mask]]
    return corrected,mask


def fill_hammer_pinholes(rgb):
    dark=rgb.max(axis=2)<70
    enclosed=ndi.binary_fill_holes(dark)&~dark&(rgb.min(axis=2)>235)
    yy,xx=np.indices(enclosed.shape);enclosed&=xx>760
    labels,_=ndi.label(enclosed)
    sizes=np.bincount(labels.ravel());mask=enclosed&(sizes[labels]<=600)
    result=rgb.copy();result[mask]=(27,22,22)
    return result,mask


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--destination',required=True,type=Path)
    args=ap.parse_args();check_branch();store=Store(ROOT)
    def verified(p):return store.read({'asset':'characters.ink.weapons.hammer','path':p.relative_to(ROOT).as_posix()})
    selection=json.loads(verified(INPUT/'selection.json'))
    chosen=next(x for x in selection['candidates'] if x['count']==14)
    steps=chosen['steps'];assert len(steps)==14 and sum(x['ms'] for x in steps)==950
    palettepath=ASSET/'runtime/user-hammer-normal-reused-20261031/idle_01.png'
    ref=np.array(Image.open(io.BytesIO(verified(palettepath))).convert('RGBA'))
    palette=[tuple(map(int,c)) for c,n in Counter(map(tuple,ref[ref[:,:,3]>0,:3])).most_common()]
    assert len(palette)==6
    first=Image.open(io.BytesIO(verified(INPUT/'selected-14/01-F001.png'))).convert('RGB')
    anchor=foot_anchor(np.array(first))
    out=args.destination.resolve();out.mkdir(parents=True,exist_ok=False)
    for name in ['original-frames','cleaned-highres','masks','runtime/detail4x','runtime/native224','preview']:(out/name).mkdir(parents=True)
    details=[];natives=[];names=[];images=[];checks=[]
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',23)
    board=Image.new('RGB',(1920,1660),'#e9edf1');bd=ImageDraw.Draw(board)
    bd.text((16,12),'망치 스킬 · 확정 14장 · 허리띠·배경 정리 / 체공 유지',font=font,fill='#17212b')
    before_after=Image.new('RGB',(2240,880),'#e9edf1');ad=ImageDraw.Draw(before_after)
    show={'F018':0,'F022':1,'F036':2,'F039':3,'F090':4}
    for i,step in enumerate(steps):
        key=step['frame'];fid=f'{i+1:02d}-{key}'
        raw=verified(INPUT/'selected-14'/f'{fid}.png')
        (out/'original-frames'/f'{fid}.png').write_bytes(raw)
        rgb=np.array(Image.open(io.BytesIO(raw)).convert('RGB'))
        corrected,mask=remove_belt(rgb,key)
        corrected,holes=fill_hammer_pinholes(corrected)
        high,fringe=cutout(corrected)
        a=np.array(high);safe=ndi.binary_erosion(a[:,:,3]>0,iterations=5)&~mask&~holes&~fringe
        assert np.array_equal(a[safe,:3],rgb[safe]),key
        colored,fxcount,_=match_colors(high,palette,key,effect_frames={'F039','F042'})
        assert np.array_equal(np.array(colored)[:,:,3],a[:,:,3]),key
        colored.save(out/'cleaned-highres'/f'{fid}.png')
        Image.fromarray((mask*255).astype('uint8')).save(out/'masks'/f'{fid}-waist.png')
        # A single fixed first-frame registration for the entire skill. Never
        # call foot_anchor per frame: doing so would erase the approved lift.
        detail=game_frame(colored,anchor,4);native=game_frame(colored,anchor)
        name=f'hammer_default_{i+1:02d}';names.append(name);details.append(detail);natives.append(native)
        detail.save(out/'runtime/detail4x'/f'{name}.png');native.save(out/'runtime/native224'/f'{name}.png')
        buf=io.BytesIO();detail.save(buf,format='PNG');images.append('data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
        checks.append({'frame':name,'sourceFrame':key,'sourceSha256':hashlib.sha256(raw).hexdigest(),
            'ms':step['ms'],'startMs':step['startMs'],'fixedSourceAnchor':anchor,'waistPixelsEdited':int(mask.sum()),
            'hammerPinholesFilled':int(holes.sum()),'effectPixelsPreserved':fxcount,'originalInteriorExactBeforePalette':True,
            'airborneAndPosePreservedByConstantTransform':True})
        bg=checker((480,360));bg.alpha_composite(colored.crop((180,0,1140,720)).resize((480,360),Image.Resampling.LANCZOS))
        x=i%4*480;y=i//4*400+60;board.paste(bg.convert('RGB'),(x,y+28))
        bd.text((x+10,y),f'{i+1:02d} · {key} · {step["ms"]}ms',font=font,fill='#17677a')
        if key in show:
            x=show[key]*448
            for row,picture in enumerate([Image.fromarray(rgb).convert('RGBA'),colored]):
                bg=Image.new('RGBA',(448,336),'white') if row==0 else checker((448,336))
                bg.alpha_composite(picture.crop((180,0,1140,720)).resize((448,336),Image.Resampling.LANCZOS))
                y=40+row*420;before_after.paste(bg.convert('RGB'),(x,y))
                ad.text((x+12,y-30),key+(' / 원본' if row==0 else ' / 보정'),font=font,fill='#17212b')
    board.save(out/'preview/frames-14-checker.png');before_after.save(out/'preview/before-after.png')
    durations=[x['ms'] for x in steps];reviewtimes=durations[:];reviewtimes[-1]+=650
    for bgname,color in [('dark','#30343b'),('white','white'),('checker',None)]:
        frames=[]
        for detail in details:
            # Preserve the large backswing and unchanged world-space lift.
            crop=detail.crop((128,160,800,640))
            bg=checker(crop.size,24) if color is None else Image.new('RGBA',crop.size,color)
            bg.alpha_composite(crop);frames.append(bg.convert('RGB'))
        for suffix,factor in [('',1),('-slow',2)]:
            frames[0].save(out/'preview'/f'skill-14-{bgname}{suffix}.gif',save_all=True,append_images=frames[1:],duration=[t*factor for t in reviewtimes],loop=0,disposal=2,optimize=False)
    for size,frames,folder in [((896,768),details,'detail4x'),((224,192),natives,'native224')]:
        sheet=Image.new('RGBA',(size[0]*4,size[1]*4))
        for i,im in enumerate(frames):sheet.alpha_composite(im,(i%4*size[0],i//4*size[1]))
        sheet.save(out/'runtime'/folder/'sheet-14.png')
    details[7].crop((128,160,800,640)).resize((1344,960),Image.Resampling.NEAREST).save(out/'preview/contact-large-transparent.png')
    sequences={'hammer_default':[{'frame':names[i],'ms':steps[i]['ms']} for i in range(14)]}
    dump(out/'editor.json',{'schemaVersion':1,'kind':'hammer-skill-cleaned-14','weapon':'hammer',
        'width':896,'height':768,'sourceFrames':names,'names':names,'images':images,'ms':durations,'sequences':sequences,
        'palette':['#%02x%02x%02x'%c for c in palette],'note':'User-approved 14 poses. Waist band removed, transparent background, approved colors. Single fixed alignment preserves airborne movement. No Unity deployment.'})
    dump(out/'animation.json',{'canvas':[224,192],'pivot':[96,151],'ppu':32,
        'detailExport':{'canvas':[896,768],'pivot':[384,604],'ppu':128},'scale':.145,
        'fixedSourceAnchor':anchor,'motionApproval':'user-approved 14-frame selection','appearanceApproval':'cleanup prepared for review',
        'gameIntegration':'not-deployed','contactFrame':names[7],'contactSourceFrame':'F039','contactSeconds':.4,'durationSeconds':.95,
        'reviewPauseMs':650,'sequences':sequences,'frames':checks,
        'paletteReference':palettepath.relative_to(ROOT).as_posix(),'palette':['#%02x%02x%02x'%c for c in palette],
        'motionPolicy':'No frame substitutions, timing changes, per-frame root locking, foot copying or spatial pixelization.'})
    with Image.open(out/'preview/skill-14-dark.gif') as gif:
        assert gif.n_frames==14
        assert sum(gif.seek(i) or gif.info['duration'] for i in range(gif.n_frames))==1600
    assert sum(durations[:7])==400 and sum(durations)==950
    dump(out/'validation.json',{'frames':14,'contactMs':400,'endMs':950,'allSourcePixelsVerified':True,
        'constantTransformForAll14Frames':True,'sourceAnchor':anchor,'noUnityOrGameplayChanges':True,'checks':checks})
    check_branch();print(json.dumps({'output':str(out),'frames':14,'fixedAnchor':anchor,
        'waistPixels':[x['waistPixelsEdited'] for x in checks],'hammerHolesFilled':[x['hammerPinholesFilled'] for x in checks]},ensure_ascii=False))


if __name__=='__main__':main()
