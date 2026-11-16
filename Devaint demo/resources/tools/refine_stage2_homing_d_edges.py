"""Refine the selected D's cloth rim only; retain v1 and all source inputs."""
import copy
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import binary_dilation, distance_transform_edt, convolve, label

ROOT=Path(__file__).resolve().parents[1]
GROUP=ROOT/'monsters/stage-02/boss-phase3-attacks-20261106'
OLD=GROUP/'runtime/homing-d'
OUT=GROUP/'runtime/homing-d-v2'
PREVIEW=GROUP/'preview/homing-cleanup'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def refine(image,palette):
    old=np.array(image)
    out=old.copy()
    rgba=old.astype(float);rgb=rgba[:,:,:3]
    ids,n=label(old[:,:,3]>0,np.ones((3,3)))
    sizes=np.bincount(ids.ravel());sizes[0]=0
    body=ids==sizes.argmax()
    distance=distance_transform_edt(body)
    # Pale mask/hand/shins and brown boots remain byte-identical, including their
    # silhouette. Purple crossbow components are excluded by the body mask.
    skin=(rgb[:,:,0]>rgb[:,:,2]*1.08)&(rgb[:,:,1]>rgb[:,:,2]*1.03)
    protect=binary_dilation(skin&body,iterations=2)
    cloth=body&~protect
    rim=cloth&(distance<=2)
    # Recover the local material from the inward sample, then snap only the rim
    # to that material's existing approved color. Never map a pale matte pixel
    # to the skin palette just because it happens to be closest in RGB.
    inner=cloth&(distance>2)
    _,near=distance_transform_edt(~inner,return_indices=True)
    samples=rgb[near[0],near[1]]
    cloth_palette=palette[(palette[:,2]>=palette[:,0]*.95)&(palette[:,2]>palette[:,1]*1.1)]
    points=np.column_stack(np.nonzero(rim))
    observed=rgb[rim]
    local=samples[rim]
    rays=255-cloth_palette
    alpha=np.clip(((255-observed[:,None])*rays).sum(2)/(rays*rays).sum(1),0,1)
    fitted=255-alpha[:,:,None]*rays
    cost=((fitted-observed[:,None])**2).sum(2)+.6*((local[:,None]-cloth_palette)**2).sum(2)+100*(1-alpha)**2
    colors=cloth_palette[cost.argmin(1)]
    out[rim,:3]=colors.astype(np.uint8)
    # Remove only a single, unsupported cloth-pixel spur (<=2 neighbours).
    # Fingers, mask, boots, actual narrow sleeve tips and the bow are protected.
    neighbours=convolve(body.astype(int),np.ones((3,3),int),mode='constant')-body
    spurs=cloth&(neighbours<=2)&(distance<=1)
    out[spurs]=0
    changed=np.any(old!=out,axis=2)
    assert not np.any(changed&~rim)
    assert np.array_equal(out[protect],old[protect])
    assert np.array_equal(out[~body],old[~body])
    assert set(np.unique(out[:,:,3])) <= {0,255}
    return Image.fromarray(out),{'changedPixels':int(changed.sum()),'removedSpurPixels':int(spurs.sum()),'protectedPixelsUnchanged':True,'bodyInteriorUnchanged':True,'crossbowUnchanged':True}


def main():
    if (OUT/'motion.json').exists() and json.loads((OUT/'motion.json').read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('V2 is user-approved. Preserve it and author further edits as a new version.')
    (OUT/'frames').mkdir(parents=True,exist_ok=True)
    approved=np.array(Image.open(GROUP/'source/approved-fused-idle-original.png'))
    palette=np.unique(approved[approved[:,:,3]>0,:3],axis=0).astype(float)
    manifest=json.loads((OLD/'motion.json').read_text(encoding='utf-8'))
    original_hashes={p:sha(p) for p in (OLD/'frames').glob('*.png')}
    frames=[];stats=[]
    for record in manifest['frames']:
        original=Image.open(OLD/record['file']).convert('RGBA')
        frame,stat=refine(original,palette)
        frame.save(OUT/record['file']);frames.append(frame);stats.append(stat)
        record['previousCleanupSha256']=sha(OLD/record['file'])
        record['sha256']=sha(OUT/record['file']);record['bounds']=frame.getbbox();record['edgeRefinement']=stat
    (OUT/'approved-crossbow.png').write_bytes((OLD/'approved-crossbow.png').read_bytes())
    sheet=Image.new('RGBA',(384*6,256*3))
    for n,frame in enumerate(frames):sheet.paste(frame,(n%6*384,n//6*256))
    sheet.save(OUT/'sheet.png')
    frames[0].save(PREVIEW/'homing-d-clean-v2.webp',save_all=True,append_images=frames[1:],duration=manifest['durationsMs'],loop=0,lossless=True,background=(0,0,0,0))
    board=Image.new('RGB',(384*6,280*3),'#111111');draw=ImageDraw.Draw(board)
    for n,frame in enumerate(frames):
        x,y=n%6*384,n//6*280;board.paste(frame,(x,y+24),frame);draw.text((x+12,y+6),f'V2 {n:02} / source {n+4:02}',fill='#dddddd')
    board.save(PREVIEW/'all-frames-black-v2.png')
    board=Image.new('RGB',(768*3,512*3))
    for row,bg in enumerate(['#ffffff','#808080','#000000']):
        for col,n in enumerate([0,8,9]):
            tile=Image.new('RGBA',(384,256),bg);tile.alpha_composite(frames[n]);board.paste(tile.resize((768,512),Image.Resampling.NEAREST),(col*768,row*512))
    board.save(PREVIEW/'edges-three-backgrounds-v2.png')
    comparison=Image.new('RGB',(1200,1000),'#303030')
    for col,p in enumerate([OLD/'frames/09.png',OUT/'frames/09.png']):
        im=Image.open(p).crop((100,55,250,235)).resize((600,720),Image.Resampling.NEAREST)
        comparison.paste(im,(col*600,40),im)
    draw=ImageDraw.Draw(comparison);draw.text((20,15),'V1',fill='white');draw.text((620,15),'V2: cloth rim only',fill='white')
    comparison.save(PREVIEW/'edge-v1-v2.png')
    manifest['format']='stage2-homing-d-clean-v2'
    manifest['previousCleanup']='../homing-d/motion.json'
    manifest['edgeRefinement']={'scope':'cloth silhouette 1-2 native pixels only','method':'local material-aware approved palette; remove isolated cloth spurs with at most two neighbours','maskHandShinsBootsProtected':True,'crossbowPixelsUnchanged':True,'timingUnchanged':True,'statistics':stats}
    (OUT/'motion.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    handoff=GROUP/'source/handoff.json';data=json.loads(handoff.read_text(encoding='utf-8'));data['cleanedMotion']='runtime/homing-d-v2/motion.json';data['previousCleanedMotion']='runtime/homing-d/motion.json';data['futureMatteProcessing']['manifest']='runtime/homing-d-v2/motion.json';handoff.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    assert all(sha(p)==digest for p,digest in original_hashes.items())
    print(json.dumps(stats))


if __name__=='__main__':main()
