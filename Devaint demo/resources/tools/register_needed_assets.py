"""Register an explicitly reviewed new set without replacing existing source bytes."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil

ROOT=Path(__file__).resolve().parents[1]
NEW_GROUPS={
 'ui/blueprints':('ui.blueprints','기본 도면 카드와 큰 무기 그림'),
 'ui/keycaps':('ui.keycaps','키보드·게임패드 키캡'),
 'effects/ink-destruction':('effects.ink-destruction','잉크 파괴 표현'),
 'effects/basic-sword-contact':('effects.basic-sword-contact','누락 검 접촉광의 대체 후보'),
 'characters/story/human-clerk':('characters.story.human-clerk','인간 시기 주인공'),
 'characters/story/benefactor':('characters.story.benefactor','은인 인물 후보'),
 'characters/story/ink-helper':('characters.story.ink-helper','잉크 조력자 인물 후보'),
 'authoring/needed-assets':('authoring.needed-assets','추가 필요 에셋의 규격·출처·검토')}
SOURCE_GROUPS={
 'boss-ink-altar':'environments/clock-tower/source/needed-art/ink-altar',
 'chapter1-exit':'environments/clock-tower/source/needed-art/chapter1-exit',
 'blueprint-sword':'ui/blueprints/source/needed-art',
 'blueprint-blunt':'ui/blueprints/source/needed-art',
 'blueprint-ranged':'ui/blueprints/source/needed-art',
 'prologue-human-clerk':'characters/story/human-clerk/source',
 'prologue-benefactor':'characters/story/benefactor/source',
 'prologue-ink-helper':'characters/story/ink-helper/source',
 'ink-destruction':'effects/ink-destruction/source/needed-art',
 'altar-choice-acquire':'environments/clock-tower/source/needed-art/ink-altar',
 'altar-choice-discard':'environments/clock-tower/source/needed-art/ink-altar',
 'pigment-pickup-contrast':'effects/hub-unlock/source/pigment-pickup-contrast',
 'basic-sword-hit-candidate':'effects/basic-sword-contact/source/needed-art-candidate'}


def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes((json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))


def copy_new(source,name):
    target=ROOT/name
    if not target.resolve().is_relative_to(ROOT.resolve()):raise ValueError('Unsafe target')
    if target.exists() and target.read_bytes()!=source.read_bytes():
        raise ValueError('Preserve existing file: '+name)
    target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists():shutil.copyfile(source,target)
    return name


def refresh_registry(new_paths):
    catalog=json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
    by_folder={x['path']:x for x in catalog['assets']}
    for folder,(ident,title) in NEW_GROUPS.items():
        by_folder.setdefault(folder,{'id':ident,'path':folder,'fileCount':0})
    owned={}
    for name in new_paths:
        candidates=[p for p in by_folder if name.startswith(p+'/')]
        if not candidates:raise ValueError('Missing owner: '+name)
        folder=max(candidates,key=len);owned.setdefault(folder,[]).append(name[len(folder)+1:])
    for folder,files in owned.items():
        path=ROOT/folder/'asset.json'
        if path.exists():asset=json.loads(path.read_text(encoding='utf-8'))
        else:
            ident,title=NEW_GROUPS[folder]
            asset={'format':'deviant-resource-asset-v1','id':ident,'path':folder,'name':title,
                   'status':'review-candidates','approval':'pending-user-art-review',
                   'gameIntegration':'not-applied','files':[]}
        asset['files']=sorted(set(asset['files'])|set(files))
        asset.setdefault('workingSets',{})['needed-art']={
            'approval':'pending-user-art-review','gameIntegration':'not-applied',
            'consumerSelection':'not-added-to-unity-deployment',
            'layoutPathFromResourceRoot':'authoring/needed-assets/source/output-layout.json'}
        write(path,asset);by_folder[folder]['fileCount']=len(asset['files'])
    catalog['assets']=list(by_folder.values());catalog['generatedManifest']='generated-assets.json'
    write(ROOT/'catalog.json',catalog)
    path=ROOT/catalog['generatedManifest'];manifest=json.loads(path.read_text(encoding='utf-8'))
    records={r['path']:r for r in manifest['files']}
    for name in new_paths:
        p=ROOT/name;record={'path':name,'bytes':p.stat().st_size,'sha256':digest(p)}
        if name in records and records[name]!=record:raise ValueError('Existing generated record differs: '+name)
        records[name]=record
    manifest['scope']='generated production sets';manifest['files']=[records[k] for k in sorted(records)]
    write(path,manifest)


def register(task,review):
    layout=json.loads((review/'authoring/needed-assets/source/output-layout.json').read_text(encoding='utf-8'))
    paths=[]
    for record in layout['sprites']:paths.append(copy_new(review/record['path'],record['path']))
    common='authoring/needed-assets/source'
    paths.append(copy_new(review/(common+'/output-layout.json'),common+'/output-layout.json'))
    paths.append(copy_new(task/'audio-reuse.json',common+'/audio-reuse.json'))
    source_records=[]
    for name,folder in SOURCE_GROUPS.items():
        source=task/'selected-sources'/(name+'.png')
        target=folder+'/generated/'+name+'.png';paths.append(copy_new(source,target))
        prompt=folder+'/prompts/'+name+'.txt';paths.append(copy_new(task/'prompts'/(name+'.txt'),prompt))
        source_records.append({'id':name,'sourcePath':target,'sha256':digest(source),
                               'promptPath':prompt,'workflow':'imagegen CLI edit','model':'gpt-image-2','quality':'high'})
    altar=SOURCE_GROUPS['boss-ink-altar']
    paths.append(copy_new(task/'generated/boss-ink-altar.png',altar+'/reference/altar-initial.png'))
    paths.append(copy_new(task/'prompts/boss-ink-altar-refine.txt',altar+'/prompts/boss-ink-altar-refine.txt'))
    provenance={'format':'deviant-needed-art-production-v1','approved':False,'runtimeApplied':False,
        'primaryRequest':'temporary-and-needed-assets.md additional art; application guide only',
        'selectedSourceImages':source_records,
        'altarRefinement':{'initial':altar+'/reference/altar-initial.png',
                           'selected':altar+'/generated/boss-ink-altar.png',
                           'prompt':altar+'/prompts/boss-ink-altar-refine.txt'},
        'nativeUi':'KeycapMarker.SpriteFor visual recipe and readable 5x7 glyphs; no input bindings changed',
        'buildingMasks':'Existing color sprite material regions and exact original alpha; no approved source rewritten',
        'references':[
          {'path':'environments/clock-tower/runtime/unity-textures/room-rear.bytes','purpose':'actual boss-room PNG bytes'},
          {'path':'environments/village/runtime/buildings/reference-map-phase2/color/house_balcony.png','purpose':'color style'},
          {'path':'environments/village/runtime/buildings/reference-map-phase2/sketch/house_balcony.png','purpose':'pencil style'},
          {'path':'characters/ink/weapons/sword/runtime/approved-eleven-package/character/I01.png','purpose':'existing ink protagonist identity and scale'}],
        'notClaimed':['approved original BasicSwordHit restored','final character appearance approval',
                      'Unity deployment or scene changes','new audio recording or new event wiring']}
    for ref in provenance['references']:ref['sha256']=digest(ROOT/ref['path'])
    write(ROOT/common/'provenance.json',provenance);paths.append(common+'/provenance.json')
    refresh_registry(paths)
    print('Registered',len(paths),'new source/export files; consumer deployment untouched.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--task-directory',type=Path,required=True)
    p.add_argument('--review-directory',type=Path,required=True)
    a=p.parse_args();register(a.task_directory.resolve(),a.review_directory.resolve())
