"""Register the user-approved barrel, millstone and FX; preserve original inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT=Path(__file__).resolve().parents[1]
PALETTE=['#171716','#41413D','#575451','#817D77','#9C968D','#D6D0C7']

def write(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def build(inputs):
    assets=[];clips=[];deploy=[]
    def asset(name):
        folder=ROOT/'monsters/stage-01/normal'/name
        folder.mkdir(parents=True,exist_ok=True)
        assets.append((name,folder))
        return folder
    barrel=asset('barrel-gunner');wheel=asset('millstone-charger')
    def copy(src,dest):
        dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
    def clip(folder,id,files,ms,pivot,loop=False,role='body',palette=PALETTE):
        records=[]
        for i,file in enumerate(files):
            relative=file.relative_to(ROOT).as_posix()
            resource='Chapter1NewMonsterArt/'+id+f'/{i:02d}'
            raw=file.read_bytes();width,height=Image.open(file).size
            records.append({'resource':resource,'sha256':hashlib.sha256(raw).hexdigest()})
            deploy.append({'target':'unity/Assets/Deviant/Stage1Monsters/Resources/'+resource+'.png','sha256':records[-1]['sha256'],'bytes':len(raw),'source':{'asset':'monsters.stage-01.normal.'+folder.name,'path':relative}})
        clips.append({'id':id,'role':role,'width':width,'height':height,'ppu':32,'pivot':pivot,'ms':ms,'loop':loop,'palette':palette,'alpha':'binary' if role=='body' else 'approved-soft-alpha','frames':records})
    src=inputs/'barrel-aether-extract-20261103/right-appendage-clean-v7'
    meta=json.loads((src/'animation.json').read_text(encoding='utf-8'))
    body=[];yy,xx=np.indices((256,256))
    for i,record in enumerate(meta['frames']):
        original=src/record['file'];copy(original,barrel/'source/approved-attack'/f'{i:02d}.png')
        im=Image.open(original).convert('RGBA')
        if 49<=i<=58:
            a=np.array(im)
            material=(a[:,:,3]>0)&np.any(a[:,:,:3]!=(23,23,22),axis=2)&(yy<150)
            labels,n=ndi.label(material,np.ones((3,3)))
            counts=np.bincount(labels.ravel());counts[0]=0
            lid=ndi.binary_dilation(labels==counts.argmax(),iterations=2) if n else np.zeros((256,256),dtype=bool)
            a[(yy<150)&~lid]=0;im=Image.fromarray(a)
        dest=barrel/'runtime/attack'/f'{i:02d}.png';dest.parent.mkdir(parents=True,exist_ok=True);im.save(dest);body.append(dest)
    copy(src/'animation.json',barrel/'source/approved-attack/animation.json')
    clip(barrel,'barrel-attack',body,[r['durationMs'] for r in meta['frames']],[.5,29/256])
    clip(barrel,'barrel-idle',[body[0]],[1000],[.5,29/256],True)
    preview=inputs/'two-monsters-game-preview-20261103'
    files=[]
    for i in range(8):
        dest=barrel/'runtime/launch-fx'/f'{i:02d}.png';copy(preview/'barrel-pop-variants/c-vapor-puff/frames'/f'{i:02d}.png',dest);files.append(dest)
    clip(barrel,'barrel-launch',files,[40]*8,[.5,24/128],role='fx',palette=['#FFFFFF','#171716','#41413D'])
    warning=barrel/'runtime/warning/bomb-64.png';copy(preview/'bomb-marker-native64/bomb-64.png',warning)
    clip(barrel,'barrel-warning',[warning],[1000],[.5,.5],True,'fx',['#EF514A','#FA7569','#FFFFFF'])
    for file in ['source.png','prompt.txt','asset.json']:
        copy(preview/'bomb-marker-native64'/file,barrel/'source/warning'/file)
    for file in ['source.png','prompt.txt']:
        copy(preview/'barrel-pop-variants/c-vapor-puff'/file,barrel/'source/launch-fx'/file)
    # Reuse the approved preview's deterministic deforming ink projectile/impact recipe.
    preview_code=(preview/'build_preview.py').read_text(encoding='utf-8')
    start=preview_code.index('def ink_blob(');end=preview_code.index('\ndef charge_air(',start)
    env={'Image':Image,'ImageDraw':__import__('PIL.ImageDraw',fromlist=['ImageDraw']),'np':np,'math':__import__('math'),'BALL_RADIUS':18,'INK':(23,23,22),'colors':[tuple(bytes.fromhex(c[1:])) for c in PALETTE]}
    exec(preview_code[start:end],env)
    projectile=[]
    for i in range(8):
        im=env['ink_blob'](i*__import__('math').tau/8,True)
        dest=barrel/'runtime/projectile'/f'{i:02d}.png';dest.parent.mkdir(parents=True,exist_ok=True);im.save(dest);projectile.append(dest)
    clip(barrel,'barrel-projectile',projectile,[40]*8,[.5,.5],True,'fx',PALETTE[:2])
    import textwrap
    impact_code=preview_code[preview_code.index('    if 3.2<=t<3.6:'):preview_code.index('    if 3.2<=t<3.95:')]
    impact_code=textwrap.dedent(impact_code).replace('480','80')
    impact=[]
    for i in range(8):
        im=Image.new('RGBA',(160,96));draw=env['ImageDraw'].Draw(im)
        exec(impact_code,dict(env,t=3.2+i*.05,GROUND=64,d=draw))
        dest=barrel/'runtime/impact'/f'{i:02d}.png';dest.parent.mkdir(parents=True,exist_ok=True);im.save(dest);impact.append(dest)
    clip(barrel,'barrel-impact',impact,[50]*8,[.5,32/96],role='fx',palette=[PALETTE[0]])
    # Source-to-runtime separation is explicit; raw attack input remains untouched.
    write(barrel/'source/recipe.json',{'approval':'user-approved','bodyInput':'approved-attack','derivedAttack':'only baked upper ink stem removed, largest wooden lid component preserved; separate projectile','groundPixelY':227,'warningInput':'warning/source.png','projectileRecipe':'projectile.py','bodyPalette':PALETTE,'softAlphaExceptions':['launch-fx','warning'],'noOtherMotionClaims':True})
    (barrel/'source/projectile.py').write_text(preview_code[start:end],encoding='utf-8')
    idle=wheel/'runtime/idle/00.png';copy(inputs/'millstone-roll-20261102/reference.png',idle)
    clip(wheel,'millstone-idle',[idle],[1000],[77/256,8/160],True)
    rolling=[]
    for i in range(8):
        dest=wheel/'runtime/roll'/f'{i:02d}.png';copy(inputs/'millstone-roll-20261102/pixel-size-v2/frames'/f'roll-{i:02d}.png',dest);rolling.append(dest)
    clip(wheel,'millstone-roll',rolling,[40]*8,[81/256,8/160],True)
    shock=[]
    for i in range(8):
        dest=wheel/'runtime/charge-wave'/f'{i:02d}.png';copy(preview/'charge-shockwave-white-thin/frames'/f'{i:02d}.png',dest);shock.append(dest)
    clip(wheel,'millstone-wave',shock,[40]*8,[48/256,.5],True,'fx',['#FFFFFF'])
    copy(inputs/'millstone-roll-20261102/pixel-size-v2/animation.json',wheel/'source/roll.json')
    copy(preview/'charge-shockwave-white/source.png',wheel/'source/charge-wave-original.png')
    copy(preview/'charge-shockwave-white/prompt.txt',wheel/'source/charge-wave-prompt.txt')
    write(wheel/'source/recipe.json',{'approval':'user-approved','rollInput':'runtime/roll','rollFrameMs':40,'waveInput':'runtime/charge-wave','waveScale':1.55,'waveGrowSeconds':.2,'wavePivotTopPixels':[48,96],'waveOffsetPixels':[48,62],'groundShadow':False,'softAlphaException':'white charge-wave','noOtherMotionClaims':True})
    # The shared presentation manifest is an indexed source, not a gameplay-time owner.
    write(barrel/'source/presentation.json',{'schema':'chapter1-new-monster-art-v1','clips':clips})
    write(barrel/'source/deployment.json',{'files':deploy})
    catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
    generated=json.loads((ROOT/'generated-assets.json').read_text(encoding='utf-8'))
    prefixes=[folder.relative_to(ROOT).as_posix()+'/' for _,folder in assets]
    generated['files']=[r for r in generated['files'] if not any(r['path'].startswith(prefix) for prefix in prefixes)]
    for name,folder in assets:
        files=sorted(p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file() and p!=folder/'asset.json')
        id='monsters.stage-01.normal.'+name
        write(folder/'asset.json',{'format':'deviant-resource-asset-v1','id':id,'path':folder.relative_to(ROOT).as_posix(),'status':'user-approved-art','approval':'approved-in-Codex-2026-11-03','gameIntegration':'Unity integration verified separately','sources':['approved imagegen and Aether sprite production preserved under source/'],'files':files})
        catalog['assets']=[a for a in catalog['assets'] if a['id']!=id]
        catalog['assets'].append({'id':id,'path':folder.relative_to(ROOT).as_posix(),'fileCount':len(files)})
        for file in files:
            data=(folder/file).read_bytes()
            generated['files'].append({'path':(folder/file).relative_to(ROOT).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    write(ROOT/'catalog.json',catalog);write(ROOT/'generated-assets.json',generated)
    print(f'Registered {len(clips)} clips and {len(deploy)} runtime images; original inputs preserved.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input-root',type=Path,required=True)
    build(parser.parse_args().input_root)
