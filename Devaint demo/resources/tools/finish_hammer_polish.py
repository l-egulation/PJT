"""Preview, validate and index the exact editor-layer outputs from the JS tool."""
from pathlib import Path
import argparse,base64,hashlib,json,subprocess,sys
import numpy as np
from PIL import Image
from assets import Store
from prepare_hammer_provisional import checker

ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parent
ASSET=ROOT/'characters/ink/weapons/hammer'
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def branch():
    for p,b in [(REPO,'feat/story-unity-essentials'),(ROOT,'feat/basic-sword-normal-weapon')]:
        assert subprocess.check_output(['git','-C',str(p),'branch','--show-current'],text=True).strip()==b,'Branch changed; stopped'

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--build-report',required=True,type=Path);ap.add_argument('--refresh',action='store_true');args=ap.parse_args()
    branch();report=json.loads(args.build_report.read_text('utf8'));store=Store(ROOT)
    for item in report['inputs']:
        p=Path(item['path']);raw=store.read({'asset':'characters.ink.weapons.hammer','path':p.relative_to(ROOT).as_posix()})
        assert hashlib.sha256(raw).hexdigest()==item['sha256']
    meta=[ASSET/'asset.json',ROOT/'catalog.json',ROOT/'generated-assets.json',ROOT/'.lfs-payloads.json']
    snapshots={p:p.read_bytes() for p in meta};asset,catalog,generated,payloads=[json.loads(snapshots[p]) for p in meta]
    newfiles=[];checks=[]
    for s in report['sets']:
        source,runtime,preview=[Path(s[k]) for k in ['source','runtime','preview']]
        an=json.loads((source/'animation.json').read_text('utf8'));ed=json.loads((source/'editor.json').read_text('utf8'))
        prior=ASSET/'source'/an['sourceSet'];old=json.loads((prior/'animation.json').read_text('utf8'))
        assert an['sequences']==old['sequences'] and an['detailExport']==old['detailExport']
        assert args.refresh or s['setid'] not in asset['generatedSets']
        frames=[]
        for i,(f,of) in enumerate(zip(an['frames'],old['frames'])):
            oldpath=ROOT/f['sourcePath'];before=np.array(Image.open(oldpath));p=source/'cleaned-highres'/oldpath.name;after=np.array(Image.open(p))
            assert before.shape==after.shape and np.array_equal(before[:,:,3],after[:,:,3])
            changed=np.any(before!=after,axis=2);assert int(changed.sum())==f['edgePixelsRecolored']
            assert set(map(tuple,after[changed,:3]))<={(27,22,22),(2,86,115),(2,100,140)}
            a=before[:,:,:3].astype(int);gold=(a[:,:,0]>150)&(a[:,:,1]>90)&(a[:,:,2]<100)
            neutral=a.max(axis=2)-a.min(axis=2)<25
            assert np.array_equal(before[gold|neutral],after[gold|neutral])
            for folder,size in [('detail4x',(896,768)),('native224',(224,192))]:
                image=Image.open(runtime/folder/(f['frame']+'.png'));assert image.mode=='RGBA' and image.size==size
                assert image.getbbox() and image.getextrema()[3][0]==0
            raw=(runtime/'detail4x'/(f['frame']+'.png')).read_bytes()
            assert base64.b64decode(ed['images'][i].split(',',1)[1])==raw
            frames.append(Image.open(runtime/'detail4x'/(f['frame']+'.png')).convert('RGBA'))
        durations=ed['ms'][:]
        if s['kind']=='skill':
            assert len(frames)==14 and sum(durations)==950 and sum(durations[:7])==400
            durations[-1]+=650
            assert max(an['scaleMultipliers'])==1.055
            if 'groundAnchors' in an:
                assert all(x==1.055 for x in an['scaleMultipliers'])
                assert len(an['groundAnchors'])==14
            else:assert an['scaleMultipliers'][0]==an['scaleMultipliers'][-1]==1
        else:assert len(frames)==15 and all(x==1 for x in an['scaleMultipliers'])
        for bgname,color in [('dark','#30363d'),('white','white'),('checker',None)]:
            pictures=[]
            for im in frames:
                crop=im.crop((128,160,800,640));bg=checker(crop.size,24) if color is None else Image.new('RGBA',crop.size,color)
                bg.alpha_composite(crop);pictures.append(bg.convert('RGB'))
            filename='skill-14' if s['kind']=='skill' else 'basic-15'
            pictures[0].save(preview/(filename+'-'+bgname+'.gif'),save_all=True,append_images=pictures[1:],duration=durations,loop=0,disposal=2,optimize=False)
        contact=7 if s['kind']=='skill' else 10
        frames[contact].crop((128,160,800,640)).save(preview/'contact-transparent.png')
        files=sorted(p for folder in [source,runtime,preview] for p in folder.rglob('*') if p.is_file())
        for p in files:
            rel=p.relative_to(ASSET).as_posix()
            if rel in asset['files']:assert args.refresh
            else:asset['files'].append(rel)
            raw=p.read_bytes();newfiles.append({'path':p.relative_to(ROOT).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
        asset['generatedSets'][s['setid']]={'source':source.relative_to(ASSET).as_posix(),'runtime':runtime.relative_to(ASSET).as_posix(),'preview':preview.relative_to(ASSET).as_posix(),
            'sourceSet':an['sourceSet'],'approval':'approved motion; requested small scale and edge refinement prepared for final review','gameIntegration':'not-deployed',
            'frameCount':len(frames),'detailExport':an['detailExport'],'spatialPixelization':False,
            'timingUnchanged':True,'scaleMultipliers':an['scaleMultipliers']}
        payload=source.relative_to(ROOT).as_posix()+'/editor.json'
        if payload in payloads:assert args.refresh
        else:payloads.append(payload)
        checks.append({'set':s['setid'],'frames':len(frames),'sourceAlphaUnchanged':True,'onlyTargetPigmentsRecolored':True,'goldAndNeutralFxPreserved':True,
            'timingAndPivotUnchanged':True,'edgePixelsCorrected':sum(s['edgePixels']),'scaleMultipliers':s['scales']})
    existing={r['path'] for r in generated['files']}
    if args.refresh:
        records={r['path']:r for r in generated['files']}
        for r in newfiles:
            assert any(r['path'].startswith('characters/ink/weapons/hammer/'+folder+'/'+s['setid']+'/') for folder in ['source','runtime','preview'] for s in report['sets'])
            records[r['path']]=r
        generated['files']=[records[k] for k in records]
    else:
        assert not existing&{r['path'] for r in newfiles};generated['files'].extend(newfiles)
    next(x for x in catalog['assets'] if x['id']==asset['id'])['fileCount']=len(asset['files'])
    branch()
    for p,b in snapshots.items():assert p.read_bytes()==b,'Concurrent metadata change: '+str(p)
    for p,v in zip(meta,[asset,catalog,generated,payloads]):dump(p,v)
    store=Store(ROOT)
    for r in newfiles:assert hashlib.sha256(store.read({'asset':asset['id'],'path':r['path']})).hexdigest()==r['sha256']
    common=REPO.parents[2]/'05_공유/공통_픽셀편집기'
    for s in report['sets']:
        title='망치 '+('평타 15장 · 외곽 잔색 최종 보정' if s['kind']=='basic' else '스킬 14장 · 타격 크기·외곽 잔색 최종 보정')
        subprocess.run([sys.executable,'-X','utf8',str(common/'set_project.py'),str(Path(s['source'])/'editor.json'),'--title',title,'--keep-active'],check=True)
    dump(args.build_report.parent/'validation.json',{'indexedFilesVerified':len(newfiles),'sourceInputsUnchanged':True,'unityApplied':False,'checks':checks})
    branch();print(json.dumps({'indexedFiles':len(newfiles),'checks':checks},ensure_ascii=False))

if __name__=='__main__':main()
