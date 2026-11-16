"""Validate the two accepted bright-stage assets in a sparse checkout."""
from pathlib import Path
import hashlib,json,re,sys
from PIL import Image
REPO=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(REPO/'tools'))
from verify_resources import relative, References
NAMES=('stage1-modular-tiles-20261111','stage1-actual-layered-bright-20261111')
catalog=json.loads((REPO/'catalog.json').read_text(encoding='utf-8'))
manifest=json.loads((REPO/'generated-assets.json').read_text(encoding='utf-8'))
entries={i['path']:i for i in manifest['files']}
assert len(entries)==len(manifest['files'])
checked=0
for name in NAMES:
    base=REPO/'environments'/name
    meta=json.loads((base/'asset.json').read_text(encoding='utf-8'))
    entry=next(a for a in catalog['assets'] if a['id']==meta['id'])
    assert entry['fileCount']==len(meta['files'])
    assert meta['status'].startswith('art-approved')
    assert meta['gameIntegration']=='not-applied'
    actual={p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file() and p.name!='asset.json'}
    assert actual==set(meta['files'])
    for f in meta['files']:
        relative(f)
        p=base/f;data=p.read_bytes();digest=hashlib.sha256(data).hexdigest()
        assert digest==meta['fileHashes'][f],p
        record=entries[p.relative_to(REPO).as_posix()]
        assert digest==record['sha256'] and len(data)==record['bytes'],p
        if p.suffix=='.md':
            for link in re.findall(r'\]\(([^)]+)\)',data.decode('utf-8')):
                if not re.match(r'https?:',link):
                    target=(p.parent/link.split('#')[0]).resolve()
                    assert target.is_relative_to(REPO) and target.exists(),(p,link)
        if p.suffix=='.html':
            parser=References();parser.feed(data.decode('utf-8'))
            for link in parser.paths:
                target=(p.parent/link).resolve()
                assert target.is_relative_to(REPO) and target.exists(),(p,link)
        checked+=1
tile=REPO/'environments'/NAMES[0];layer=REPO/'environments'/NAMES[1]
meta=json.loads((tile/'runtime/tileset.json').read_text(encoding='utf-8'))
atlas=Image.open(tile/'runtime/atlas-32.png').convert('RGBA')
assert len(meta['atlas']['rects'])==199
for r in meta['atlas']['rects']:
    im=Image.open(tile/'runtime'/('tiles' if r['id'].startswith('stone') else 'platforms')/(r['id']+'.png')).convert('RGBA')
    assert im.size==(32,32)
    assert im.tobytes()==atlas.crop((r['x'],r['y'],r['x']+32,r['y']+32)).tobytes()
    if r['id'].startswith('stone'):assert im.getchannel('A').getextrema()==(255,255)
    else:assert im.getchannel('A').crop((0,12,32,32)).getextrema()==(0,0)
for rid in ('S1-04','S1-05','S1-07','S1-12'):
    files=list((layer/'layers'/rid).glob('*.png'));assert len(files)==4
    assert len({Image.open(p).size for p in files})==1
    a=Image.open(layer/'layers'/rid/'02-architecture.png').getchannel('A')
    assert a.histogram()[0]>a.width*a.height*.1
dep=json.loads((layer/'asset.json').read_text(encoding='utf-8'))['dependencies'][0]
assert dep['metadataSha256']==hashlib.sha256((tile/'asset.json').read_bytes()).hexdigest()
print(json.dumps({'scope':'two accepted assets only','files':checked,'nativeSprites':199,'layeredRooms':4,'hashesLinksManifestAtlasAlphaDependency':'PASS'}))
