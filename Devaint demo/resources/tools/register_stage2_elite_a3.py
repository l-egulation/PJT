"""Register the explicitly approved A3 production art without changing source pixels."""
from pathlib import Path
import argparse,json,hashlib,shutil
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--authoring-root',type=Path,required=True)
args=parser.parse_args();author=args.authoring_root.resolve();base=author/'02_에셋/몬스터/2챕터_A3_엘리트'
group='monsters/stage-02/elite/stone-fist';dest=ROOT/group;dest.mkdir(parents=True,exist_ok=True)
asset_id='monsters.stage-02.elite.stone-fist';clips=[];settings=[]
def write(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def copy(source,target):target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
def clip(name,paths,durations,pivots,starts=None,sources=None,loop=False):
    frames=[];cursor=0
    for i,(path,hold) in enumerate(zip(paths,durations)):
        rel=f'runtime/{name}/{i:02d}.png';copy(path,dest/rel);w,h=Image.open(path).size
        pivot=pivots[i] if isinstance(pivots[0],list) else pivots
        resource_path=f'Chapter2EliteArt/{name}/{i:02d}'
        settings.append({'path':resource_path,'resource':rel,'canvas':[w,h],'pivot':pivot,'ppu':32})
        frames.append({'path':resource_path,'startMs':starts[i] if starts else cursor,'holdMs':hold,'source':sources[i] if sources else i+1})
        cursor+=hold
    clips.append({'id':name,'loop':loop,'durationMs':max(f['startMs']+f['holdMs'] for f in frames),'frames':frames})
    return frames

tele=base/'04_확정본/순간이동펀치_24프레임_중간픽셀_사용자면색기준_선정리'
slam=base/'04_확정본/바닥3연타_52프레임_중간픽셀_면색정리_밝은테두리보정'
punch=base/'04_확정본/전방펀치_28프레임_중간픽셀_마스터11색_외곽정리'
read=lambda p:json.loads(p.read_text(encoding='utf-8'))
td=read(tele/'animation.json');sd=read(slam/'animation.json');pd=read(punch/'animation.json')
tf=td['frames'];sf=sd['frames'];pf=pd['frames']
clip('idle',[tele/f['file'] for f in tf[:2]],[180,180],[120,120],loop=True)
phase_starts={'idle':-360,'prepare':0,'swing':870,'impact':1020,'recover':1170,'turn':1350}
tele_starts=[phase_starts[f['phase']]+f['start_ms'] for f in tf]
clip('teleport',[tele/f['file'] for f in tf],[f['hold_ms'] for f in tf],[120,120],tele_starts,[f['source'] for f in tf])
clip('combo',[slam/f['file'] for f in sf],[f['hold_ms'] for f in sf],[120,144],[f['start_ms'] for f in sf],[f['source_frame'] for f in sf])
rock_data=read(author/'output/elite-punch-slow-stone-fast-arm-20261103/composite-data.json')
starts=[0]+[f['startMs']+rock_data['bodyOffsetMs'] for f in rock_data['frames'][1:]]
holds=[starts[i+1]-v if i+1<len(starts) else rock_data['bodyEndMs']-v for i,v in enumerate(starts)]
clip('punch',[punch/f['file'] for f in rock_data['frames']],holds,[120,144],starts,[f['sourceFrame'] for f in rock_data['frames']])
formation=base/'03_작업중/돌응축_16프레임_20261103'
clip('formation',sorted((formation/'frames').glob('*.png')),[1450/16]*16,[164.5,158.5])
shatter=base/'03_작업중/돌파쇄_5파편_고속_20261103'
clip('shatter',sorted((shatter/'frames').glob('*.png'))[:2],[20,20],[64,112])
fx_assets=read(author/'output/elite-pattern12-effects-20261103/assets.json')
tfx=Path(fx_assets[0]['path']);gfx=Path(fx_assets[1]['path']);tm=fx_assets[0]['meta']
for name,key in [('blink','blink'),('teleHit','hit')]:
    seq=tm['sequences'][key];clip(name,[tfx/f['file'] for f in seq['frames']],seq['durationsMs'],[f.get('pivot',seq['pivot']) for f in seq['frames']])
ground=read(author/'output/elite-slam-rock-impact-20261103/composition.json')['attackEffects']['slam']
clip('groundWave',[gfx/f['file'] for f in ground['frames']],ground['durationsMs'],ground['pivot'])
third=base/'03_작업중/돌주먹_손감싸는궤적_접점섬광_20261103'
clip('wrap',sorted((third/'wrap/frames').glob('*.png')),[15,15,35],[20,48])
clip('contact',sorted((third/'flash/frames').glob('*.png')),[25,25,25,25,20,20],[48,48])
fragment_paths=sorted((shatter/'projectiles').glob('*.png'));assert len(fragment_paths)==5
clip('fragment',fragment_paths,[1]*5,[8,8])
axes=[]
for path in fragment_paths:
    a=np.asarray(Image.open(path).convert('RGBA'));y,x=np.nonzero(a[:,:,3]);e,v=np.linalg.eigh(np.cov(np.stack([x,-y])));axis=v[:,np.argmax(e)];degrees=float(np.degrees(np.arctan2(axis[1],axis[0])));axes.append((degrees+90)%180-90)
binding={'schema':1,'bodyReferenceHeightPx':88,'ppu':32,'teleportVanishMs':650,'teleportArrivalMs':770,'teleportHitMs':1020,'teleportEndMs':1650,'rockWrapMs':1455,'rockHitMs':1500,'rockBodyEndMs':rock_data['bodyEndMs'],'fragmentAxes':axes,'debris':ground['debris'],'clips':clips,'sprites':settings}
write(dest/'source/approved-art-binding.json',binding)
for label,folder in [('teleport',tele),('combo',slam),('punch',punch)]:
    copy(folder/'animation.json',dest/f'source/{label}-authoring.json');copy(folder/'palette.json',dest/f'source/{label}-palette.json')
for label,folder in [('teleport-effects',tfx),('ground-effects',gfx),('rock-contact',base/'03_작업중/돌주먹_흰타격효과_8프레임_20261103')]:
    for p in (folder/'source').rglob('*'):
        if p.is_file():copy(p,dest/'source'/label/p.relative_to(folder/'source'))
copy(author/'output/elite-pattern12-effects-20261103/generation-prompts.txt',dest/'source/generation-prompts.txt')
copy(author/'output/elite-slam-rock-impact-20261103/composition.json',dest/'source/approved-composition.json')
viz=Path('C:/Users/SSAFY/.codex/visualizations/2026/11/02/01a19b37-fa0a-7dc2-b9bc-106142aaf0d9/elite-a3-slam-rock-impact.html')
copy(viz,dest/'preview/approved-three-attacks.html')
files=sorted(p.relative_to(dest).as_posix() for p in dest.rglob('*') if p.is_file() and p.name!='asset.json')
write(dest/'asset.json',{'format':'deviant-resource-asset-v1','id':asset_id,'path':group,'status':'user-approved-art','approval':'approved-in-Codex-2026-11-03','gameIntegration':'Unity integration is verified independently','sources':['Approved A3 24/52/28 native pixel sequences and separately generated VFX; source bytes preserved'],'files':files})
catalog=read(ROOT/'catalog.json');catalog['assets']=[a for a in catalog['assets'] if a['id']!=asset_id]+[{'id':asset_id,'path':group,'fileCount':len(files)}];write(ROOT/'catalog.json',catalog)
generated=read(ROOT/'generated-assets.json');generated['files']=[f for f in generated['files'] if not f['path'].startswith(group+'/')]
for name in files:
    data=(dest/name).read_bytes();generated['files'].append({'path':group+'/'+name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
write(ROOT/'generated-assets.json',generated)
payloads=read(ROOT/'.lfs-payloads.json');payload=group+'/preview/approved-three-attacks.html';write(ROOT/'.lfs-payloads.json',sorted(set(payloads+[payload])))
print(json.dumps({'asset':asset_id,'files':len(files),'sprites':len(settings),'clips':[(c['id'],len(c['frames'])) for c in clips]}))
