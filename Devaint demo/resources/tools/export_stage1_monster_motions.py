"""Copy the approved native pixels and timing into the isolated Unity module."""
from pathlib import Path
import hashlib, json
from PIL import Image
from authoring_output import ROOT as RESOURCE_ROOT, WORKBENCH, destination

SOURCE=WORKBENCH/'MonsterReference/approved'

def export():
    ROOT=destination(__doc__)
    OUT=ROOT/'unity/Assets/Deviant/Stage1Monsters'
    original=(SOURCE/'manifest.json').read_bytes();manifest=json.loads(original)
    for f in manifest['files']:
        assert hashlib.sha256((SOURCE/f['path']).read_bytes()).hexdigest()==f['sha256'],f['path']
    entries=[]
    for monster in manifest['monsters']:
        motions=[]
        for name,m in monster['motions'].items():
            assert len(m['frames'])==len(m['ms'])==m['frameCount']
            frames=[]
            for i,rel in enumerate(m['frames']):
                source=SOURCE/rel;target=OUT/'Art'/monster['id']/name/f'{i:02d}.png';target.parent.mkdir(parents=True,exist_ok=True)
                data=source.read_bytes()
                if not target.exists() or target.read_bytes()!=data:target.write_bytes(data)
                frames.append({'path':str(target.relative_to(ROOT/'unity')).replace('\\','/'),'sha256':hashlib.sha256(data).hexdigest()})
                im=Image.open(source);assert im.size==(m['width'],m['height'])
            motions.append({'id':name,'width':m['width'],'height':m['height'],'ppu':m['ppu'],'pivot':m['pivot'],
                'ground':m['groundBoundaryY'],'loop':m['loop'],'milliseconds':m['ms'],'frames':frames})
        entries.append({'id':monster['id'],'displayName':monster['name'],'motions':motions})
    result={'version':1,'sourceManifestSHA256':hashlib.sha256(original).hexdigest(),'palette':manifest['palette'],
        'monsters':entries,'scope':'Approved motion presentation only. No enemy AI, damage, hitstun or spawn policy changes.'}
    hero=RESOURCE_ROOT/'characters/ink/common/runtime/forest-character/idle/01.png'
    if hero.exists():
        target=OUT/'Art/Reference/player.png';target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(hero.read_bytes())
        result['playerReference']={'id':'player-reference','width':128,'height':128,'ppu':32,'ground':121,'pivot':[.5,7/128],
            'frames':[{'path':str(target.relative_to(ROOT/'unity')).replace('\\','/'),'sha256':hashlib.sha256(hero.read_bytes()).hexdigest()}]}
    (OUT/'Data').mkdir(parents=True,exist_ok=True)
    (OUT/'Data/import.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    assert original==(SOURCE/'manifest.json').read_bytes()
    print(json.dumps({'monsters':len(entries),'clips':sum(len(m['motions']) for m in entries),'frames':sum(len(c['frames']) for m in entries for c in m['motions'])}))

if __name__=='__main__':export()
