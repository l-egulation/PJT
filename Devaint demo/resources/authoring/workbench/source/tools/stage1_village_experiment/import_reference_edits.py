"""Register CLI-edited sketch sprites while leaving normal game images untouched.

The edited image supplies its own alpha; no normal alpha is applied to it.
Opaque terrain strips are assembled into native roles, with interior hatching
rendered continuously in world space rather than repeated in each tile.
"""
from pathlib import Path
import argparse
import hashlib
import json
import uuid
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import map_coordinates
import triangle

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'unity/Assets/Stage1VillageExperiment/Resources/Village'
OUT=ROOT/'outputs/village/reference-correction'
DEST=RES/'reference-sketch'
PAPER=np.array([248,237,220],dtype=float)

# Architectural edges are explicit mesh constraints: a roof/eave cannot bend at
# an unrelated tree or window anchor. All coordinates use the original PNG frame.
REGISTRATION = {}
for registration_file in ('background_registration.json', 'near_registration.json'):
    path = Path(__file__).with_name(registration_file)
    REGISTRATION.update(json.loads(path.read_text(encoding='utf-8')))


def align_landmarks(image, name):
    registration = REGISTRATION.get(name)
    if not registration:
        return image
    points = np.asarray(registration['points'], dtype=float)
    source, target = points[:, :2], points[:, 2:]
    mesh = triangle.triangulate({'vertices': target,
                                'segments': registration['segments']}, 'pQ')
    if len(mesh['vertices']) != len(points):
        raise ValueError(f'Intersecting registration constraints: {name}')
    w, h = image.size
    yy, xx = np.mgrid[:h, :w]
    sx, sy = xx.astype(float), yy.astype(float)
    for indices in mesh['triangles']:
        src, dst = source[indices], target[indices]
        source_basis = (src[1:] - src[0]).T
        target_basis = (dst[1:] - dst[0]).T
        if np.linalg.det(source_basis) * np.linalg.det(target_basis) <= 0:
            raise ValueError(f'Folded registration triangle: {name} {indices}')
        x0, y0 = np.maximum(np.floor(dst.min(axis=0)).astype(int), 0)
        x1, y1 = np.minimum(np.ceil(dst.max(axis=0)).astype(int), [w-1, h-1])
        pixels = np.stack([xx[y0:y1+1, x0:x1+1], yy[y0:y1+1, x0:x1+1]], axis=-1)
        bary = (pixels-dst[0]) @ np.linalg.inv(target_basis).T
        inside = (bary.min(axis=-1) >= -1e-7) & (bary.sum(axis=-1) <= 1+1e-7)
        mapped = bary @ source_basis.T + src[0]
        sx[y0:y1+1, x0:x1+1][inside] = mapped[:, :, 0][inside]
        sy[y0:y1+1, x0:x1+1][inside] = mapped[:, :, 1][inside]
    rgba = np.asarray(image).astype(float)/255
    rgba[:, :, :3] *= rgba[:, :, 3:4]
    result = np.stack([map_coordinates(rgba[:, :, c], [sy, sx], order=1,
                                     mode='nearest') for c in range(4)], axis=2)
    np.divide(result[:, :, :3], result[:, :, 3:4], out=result[:, :, :3],
              where=result[:, :, 3:4] > .0001)
    return Image.fromarray(np.uint8(np.clip(result*255, 0, 255)))


def meta(path):
    side=Path(str(path)+'.meta')
    if side.exists():return
    guid=uuid.uuid5(uuid.NAMESPACE_URL,path.relative_to(ROOT).as_posix()).hex
    text=f'fileFormatVersion: 2\nguid: {guid}\n'
    if path.suffix=='.png':
        text+='TextureImporter:\n  serializedVersion: 13\n  textureType: 8\n  textureShape: 1\n  spriteMode: 1\n  spritePixelsToUnits: 32\n  nPOTScale: 0\n  isReadable: 1\n  alphaIsTransparency: 1\n'
    side.write_text(text,encoding='utf-8')


def warm_gray(image):
    data=np.array(image.convert('RGBA'))
    rgb=data[:,:,:3].astype(float)
    # Remove colored response-edge contamination, not alpha/background removal.
    luminance=rgb@np.array([.2126,.7152,.0722])
    t=np.clip((luminance-45)/(244-45),0,1)[:,:,None]
    data[:,:,:3]=np.uint8(np.array([67,64,58])*(1-t)+PAPER*t)
    data[:,:,3]=np.where(data[:,:,3]>235,255,data[:,:,3])
    data[data[:,:,3]<8]=0
    return Image.fromarray(data)


def register(source,normal,backdrop=False):
    original=Image.open(normal).convert('RGBA');edited=Image.open(source).convert('RGBA')
    rgba=np.asarray(edited);bounds=Image.fromarray(np.uint8(rgba[:,:,3]>127)*255).getbbox()
    if bounds is None:raise ValueError('Empty generated edit '+str(source))
    if backdrop:
        result=edited.resize(original.size,Image.Resampling.LANCZOS)
    else:
        # Original visible frame is an anchor, not a clipping mask for edited alpha.
        target=original.getchannel('A').getbbox();x0,y0,x1,y1=target
        drawing=edited.crop(bounds).resize((x1-x0,y1-y0),Image.Resampling.LANCZOS)
        result=Image.new('RGBA',original.size);result.alpha_composite(drawing,(x0,y0))
    return warm_gray(align_landmarks(result,normal.stem))


def restore_far_openings(image):
    """Restore omitted architecture from existing generated parts, not color pixels."""
    donor_path=OUT/'generated/village_mid_native-style.png'
    donor=warm_gray(Image.open(donor_path).convert('RGBA').resize((1086,480),Image.Resampling.LANCZOS))
    before=image.copy()
    plaster=before.crop((398,310,432,321))
    image.paste(plaster.resize((48,52),Image.Resampling.LANCZOS),(352,305))
    window=donor.crop((353,241,381,282))
    image.paste(window.resize((24,39),Image.Resampling.LANCZOS),(355,309))
    image.paste(window.resize((22,34),Image.Resampling.LANCZOS),(404,317))
    # Preserve the complete foreground drawing from this same generated frame.
    # The boundary includes its ink; it is not the original/color coverage mask.
    crown=[(330,319),(335,334),(339,336),(343,344),(346,349),(350,346),
           (355,343),(358,345),(360,342),(361,337),(365,335),(367,332),
           (373,332),(376,331),(379,332),(383,333),(386,336),(391,339),
           (394,343),(396,349),(399,352),(402,348),(407,350),(411,352),
           (414,355),(416,361),(419,363),(421,368)]
    foreground=Image.new('L',image.size)
    ImageDraw.Draw(foreground).polygon(crown+[(421,391),(330,391)],fill=255)
    image.paste(before,(0,0),foreground)
    balcony=donor.crop((433,248,519,325))
    image.paste(balcony.resize((70,51),Image.Resampling.LANCZOS),(366,414))
    return image


def save(relative,image,source,role):
    path=DEST/relative;path.parent.mkdir(parents=True,exist_ok=True);image.save(path);meta(path)
    normal=RES/relative
    normal_bounds=Image.open(normal).convert('RGBA').getchannel('A').getbbox()
    sketch_bounds=image.getchannel('A').getbbox()
    pivot=[.5,0] if role=='near' else [0,1] if role=='platform' else [.5,.5] if role in ('terrain','generated-cloud-outline') else [0,0]
    if '/stone_trim/' in relative:pivot=[.5,.25]
    registration=REGISTRATION.get(normal.stem)
    donor=OUT/'generated/village_mid_native-style.png' if normal.stem=='village_tall_native' else None
    return {'normal':normal.relative_to(ROOT).as_posix(),'normalSha256':hashlib.sha256(normal.read_bytes()).hexdigest(),
            'sketch':path.relative_to(ROOT).as_posix(),'sketchSha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'size':list(image.size),'role':role,'source':str(source).replace('\\','/'),
            'sourceSha256':hashlib.sha256((ROOT/source).read_bytes()).hexdigest(),
            'retouchSources':[{'path':donor.relative_to(ROOT).as_posix(),
                               'sha256':hashlib.sha256(donor.read_bytes()).hexdigest(),
                               'role':'complete generated window and balcony parts'}] if donor else [],
            'pivotNormalizedBottomOrigin':pivot,'pixelsPerUnit':32,
            'normalVisibleBoundsTopOrigin':normal_bounds,'sketchVisibleBoundsTopOrigin':sketch_bounds,
            'normalContactRowTopOrigin':normal_bounds[3]-1 if normal_bounds else None,
            'sketchContactRowTopOrigin':sketch_bounds[3]-1 if sketch_bounds else None,
            'landingRowTopOrigin':6 if role=='platform' else None,
            'registration':{'method':'constrained piecewise affine, premultiplied RGBA',
                            'landmarks':len(registration['points'])} if registration else None,
            'alphaMeaning':{'terrain':'rock mask only; original terrain coverage retained by shader',
                            'paper-sky':'opaque paper field'}.get(role,'generated native alpha, no original alpha overwrite')}


def ground_assets():
    source=OUT/'generated/ground-reference-edit.png'
    drawing=warm_gray(Image.open(source))
    tiles=[]
    # Register complete native stones between their drawn mortar joints. The old
    # uniform strip cuts bisected stones, then pasted an unrelated edge column.
    for left,right in ((325,658),(760,1076),(1194,1526),(1633,1997)):
        tile=np.zeros((32,32,4),dtype=np.uint8)
        part=np.array(drawing.crop((left,7,right+1,190)).resize((32,17),Image.Resampling.LANCZOS))
        part[14:17,:,3]=np.array([190,100,0],dtype=np.uint8)[:,None]
        tile[:17]=part
        tiles.append(tile)
    common=tiles[0][:,0].copy()
    for tile in tiles:
        tile[:,0]=common;tile[:,-1]=common
    tiles=[Image.fromarray(tile) for tile in tiles]

    # Bend the same native strip through a concave quarter turn. Its two axes
    # retain the exact adjoining wall/ceiling profile; no source RGB/alpha is used.
    yy,xx=np.mgrid[:32,:32]
    depth=np.sqrt((xx+.5)**2+(yy+.5)**2)-.5
    along=np.arctan2(yy+.5,xx+.5)/(np.pi*.5)*31
    donor=np.asarray(tiles[0],dtype=float)/255
    donor[:,:,:3]*=donor[:,:,3,None]
    inner=np.stack([map_coordinates(donor[:,:,k],[np.clip(depth,0,31),along],order=1,mode='nearest') for k in range(4)],axis=2)
    inner[depth>=16]=0
    inner[:,:,:3]=np.divide(inner[:,:,:3],inner[:,:,3,None],out=np.zeros_like(inner[:,:,:3]),where=inner[:,:,3,None]>0)
    inner=np.uint8(np.clip(inner*255+.5,0,255))
    inner[0]=common;inner[:,0]=common

    records=[]
    masks={1:1,2:1,3:3,4:9,5:1,6:1,7:2,8:8,12:1,13:1}
    for path in sorted((RES/'color/terrain').glob('*.png')):
        index=int(path.name[:2]);variant={1:0,2:1,5:2,6:3,12:0,13:1}.get(index,0)
        north=tiles[variant]
        if index in (11,14):
            data=inner[:,::-1].copy() if index==11 else inner.copy()
        else:
            data=np.zeros((32,32,4),dtype=np.uint8);best=np.full((32,32),99)
            for bit,rotation,edge_depth in [(1,None,yy),(2,Image.Transpose.ROTATE_270,31-xx),(8,Image.Transpose.ROTATE_90,xx)]:
                if not masks.get(index,0)&bit:continue
                part=np.asarray(north if rotation is None else north.transpose(rotation))
                take=(part[:,:,3]>0)&(edge_depth<best)
                data[take]=part[take];best[take]=edge_depth[take]
        records.append(save('color/terrain/'+path.name,Image.fromarray(data),source.relative_to(ROOT),'terrain'))

    # The existing coping is an L at x16/y24, not a horizontal strip at y0.
    # Register native stone onto those two limbs; renderer occupancy stays intact.
    left_turn=Image.new('RGBA',(32,32))
    wall=tiles[0].crop((0,0,32,17)).resize((20,7),Image.Resampling.LANCZOS).transpose(Image.Transpose.ROTATE_90)
    tread=tiles[0].crop((0,0,32,17)).resize((16,7),Image.Resampling.LANCZOS)
    left_turn.alpha_composite(wall,(10,4));left_turn.alpha_composite(tread,(9,24))
    for path in (RES/'color/stone_trim').glob('*.png'):
        tile=left_turn if '_left_' in path.name else left_turn.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        records.append(save('color/stone_trim/'+path.name,tile,source.relative_to(ROOT),'terrain'))
    return records


def platform_assets():
    source=OUT/'generated/platform-reference-edit.png'
    pixels=np.asarray(warm_gray(Image.open(source)),dtype=float)/255
    pixels[:,:,:3]*=pixels[:,:,3,None]
    # Sample the large front course rather than compressing the narrow cap into
    # a bright horizontal stripe. Keep its native joint at output contact row6.
    contact_x=np.array([0,60,100,180,400,600,850,950,1035,1210,1280,1370,1410,1500,1570,1750,1950,2020,2070,2120,2170])
    contact_y=np.array([286,287,289,288,288,286,286,286,289,285,287,290,289,288,289,289,289,291,294,295,296])

    def registered_stone(left,right):
        # Premultiplied supersampling retains the generated transparent contour.
        x=np.linspace(left,right,128)
        y=np.linspace(0,240,92)[:,None]+np.interp(x,contact_x,contact_y)[None,:]
        x=np.broadcast_to(x,y.shape)
        sampled=np.stack([map_coordinates(pixels[:,:,k],[y,x],order=1,mode='constant',cval=0) for k in range(4)],axis=2)
        sampled[:,:,:3]=np.divide(sampled[:,:,:3],sampled[:,:,3,None],out=np.zeros_like(sampled[:,:,:3]),where=sampled[:,:,3,None]>0)
        image=Image.fromarray(np.uint8(np.clip(sampled*255+.5,0,255)))
        return np.array(image.resize((32,23),Image.Resampling.LANCZOS))

    # Use broad front stones and the original generated endcaps, not every tiny
    # underside stone compressed into one repeated five-piece strip.
    pieces=[registered_stone(left,right) for left,right in ((10,140),(850,1035),(1210,1370),(1410,1570),(2100,2160))]
    seam=registered_stone(1280,1280)[:,0]
    records=[]
    for i,piece in enumerate(pieces):
        # Runtime reorders/repeats middle roles: 1->3->4->2 and 1->5 are valid.
        # Every attachable edge therefore shares one actual donor cross-section.
        if i>0:piece[:,0]=seam
        if i<4:piece[:,-1]=seam
        frame=Image.new('RGBA',(32,32));frame.alpha_composite(Image.fromarray(piece),(0,6))
        records.append(save(f'color/platforms/platform_c{i+1:02d}_32x32.png',frame,source.relative_to(ROOT),'platform'))
    return records


def sky_assets():
    source=RES/'backgrounds/sky_native.png'
    records=[save('backgrounds/sky_native.png',Image.new('RGBA',Image.open(source).size,(*map(int,PAPER),255)),source.relative_to(ROOT),'paper-sky')]
    for i in range(3):
        normal=RES/f'backgrounds/cloud_native_{i}.png';source=OUT/f'generated/cloud_native_{i}-style.png'
        image=register(source,normal)
        data=np.array(image);data[:,:,:3]=np.uint8(data[:,:,:3]*.45+PAPER*.55);data[data[:,:,3]==0,:3]=0
        records.append(save(f'backgrounds/cloud_native_{i}.png',Image.fromarray(data),source.relative_to(ROOT),'generated-cloud-outline'))
    return records


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--representative',action='store_true')
    parser.add_argument('--prop',action='append',default=[])
    parser.add_argument('--background',action='append',default=[])
    args=parser.parse_args();records=[]
    if args.representative:
        records.extend(ground_assets());records.extend(platform_assets())
        records.extend(sky_assets())
        args.prop+=['house_balcony','stone_arch','courtyard_wall','hedge']
        args.background+=['village_tall_native','village_mid_native']
    for name in args.prop:
        filename='house-reference-edit.png' if name=='house_balcony' else name+'-style.png'
        source=OUT/'generated'/filename;normal=RES/f'color/props/{name}.png'
        records.append(save(f'color/props/{name}.png',register(source,normal),source.relative_to(ROOT),'near'))
    for name in args.background:
        source=OUT/'generated'/(name+'-style.png');normal=RES/f'backgrounds/{name}.png'
        image=register(source,normal,backdrop=name!='vault_front_native')
        if name=='village_tall_native':image=restore_far_openings(image)
        if name in ('village_tall_native','village_mid_native','vault_rear_native'):
            data=np.array(image);strength=.30 if name=='village_tall_native' else .50 if name=='village_mid_native' else .45
            data[:,:,:3]=np.uint8(data[:,:,:3]*strength+PAPER*(1-strength));data[data[:,:,3]==0,:3]=0;image=Image.fromarray(data)
        records.append(save(f'backgrounds/{name}.png',image,source.relative_to(ROOT),'background'))
    path=DEST/'art-manifest.json';old=json.loads(path.read_text(encoding='utf-8')) if path.exists() else {'assets':[]}
    combined={r['sketch']:r for r in old['assets']};combined.update({r['sketch']:r for r in records})
    old.update({'styleId':'STAGE1_WARM_PAPER_HATCH','normalPolicy':'original color artwork untouched','sketchPolicy':'CLI direct edits, native registration, own generated alpha; no coordinate-traced substitute','assets':list(combined.values())})
    path.write_text(json.dumps(old,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');meta(path)
    print(json.dumps({'imported':len(records),'manifest':str(path)},indent=2))


if __name__=='__main__':main()
