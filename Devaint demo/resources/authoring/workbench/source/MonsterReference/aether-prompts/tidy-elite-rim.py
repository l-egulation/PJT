"""Conservative native silhouette cleanup, with no blur or new palette colors."""
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

def tidy(original, outlined, palette, fill_gaps=True):
    src=np.array(original);a=np.array(outlined);mask=src[:,:,3]>0
    idx=np.argmin(((src[:,:,None,:3].astype(float)-palette)**2).sum(3),2)
    depth=ndi.distance_transform_cdt(mask,metric='chessboard')
    components,n=ndi.label(mask,np.ones((3,3)));sizes=np.bincount(components.ravel())
    support=np.zeros(mask.shape,dtype=bool)
    # Blend votes from nearby material areas, not individual bright seed pixels.
    for k in range(1,n+1):
        if sizes[k]<120:continue
        body=components==k;support|=body
        seeds=body&(idx>0)
        if not seeds.any():continue
        weights=[]
        for tone in range(1,5):
            evidence=body&(np.minimum(idx,4)==tone)
            weights.append(ndi.convolve(evidence.astype(float)*(1+np.minimum(depth,3)),np.array([[1,2,3,2,1],[2,4,6,4,2],[3,6,9,6,3],[2,4,6,4,2],[1,2,3,2,1]],float),mode='constant'))
        votes=np.stack(weights);choice=votes.argmax(0)+1
        distance=ndi.distance_transform_edt(~seeds)
        rim=body&(depth<=2)&(idx==0)&(distance<=5)&(votes.max(0)>0)
        a[rim,:3]=palette[choice[rim]]
    # Patch tiny enclosed pinholes and single-pixel concave cracks only.
    if fill_gaps:
        holes=ndi.binary_fill_holes(mask)&~mask
        labels,total=ndi.label(holes);hs=np.bincount(labels.ravel())
        patch=np.zeros_like(mask)
        for k in range(1,total+1):
            if hs[k]<=3:patch|=labels==k
        cross=np.array([[0,1,0],[1,0,1],[0,1,0]])
        orth=ndi.convolve(mask.astype(int),cross,mode='constant')
        around=ndi.convolve(mask.astype(int),np.ones((3,3),int),mode='constant')
        patch|=(~mask)&(orth>=3)&(around>=6)
        patch &= ndi.binary_dilation(support)
        patch &= ndi.convolve(mask.astype(int),np.ones((5,5),int),mode="constant")>=17
        near=ndi.distance_transform_edt(~mask,return_distances=False,return_indices=True)
        a[patch]=a[near[0][patch],near[1][patch]]
    # Prevent individual bright rim specks from cutting into an otherwise solid edge.
    now=a[:,:,3]>0;depth=ndi.distance_transform_cdt(now,metric='chessboard')
    ai=np.argmin(((a[:,:,None,:3].astype(float)-palette)**2).sum(3),2)
    votes=np.stack([ndi.convolve(((ai==v)&now).astype(int),np.ones((3,3),int),mode='constant') for v in range(6)])
    dominant=votes.argmax(0)
    lonely=now&(depth<=2)&(ai>=3)&(votes.max(0)>=5)&(np.take_along_axis(votes,ai[None],axis=0)[0]<=2)&(dominant>0)&(dominant<ai)&support
    a[lonely,:3]=palette[dominant[lonely]]
    # Ink drips are part of the dark body, not a continuation of nearby metal.
    # Only correct newly introduced medium/light rim colors where the original
    # opaque neighborhood is predominantly solid ink. Original highlights and
    # normal dark-gray body shading remain untouched.
    original_depth=ndi.distance_transform_cdt(mask,metric='chessboard')
    ink_count=ndi.convolve((mask&(idx==0)).astype(float),np.ones((5,5)),mode='constant')
    material_count=ndi.convolve(mask.astype(float),np.ones((5,5)),mode='constant')
    ink_fraction=ink_count/np.maximum(material_count,1)
    ai=np.argmin(((a[:,:,None,:3].astype(float)-palette)**2).sum(3),2)
    ink_rim=now&(original_depth<=2)&(idx==0)&(ai>=2)&(ink_fraction>=0.68)&support
    a[ink_rim,:3]=palette[0]
    return Image.fromarray(a)
