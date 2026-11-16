"""Transport approved elite presentation bindings; combat settings remain the gameplay source."""
from pathlib import Path
import argparse,json
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');args=p.parse_args()
data=json.loads((ROOT/'data/combat/presentation/chapter2_elite_art.json').read_text(encoding='utf-8'))
game=json.loads((ROOT/'data/combat/chapter2_elite.json').read_text(encoding='utf-8'))
assert data['schema']==1 and data['ppu']==32 and data['bodyReferenceHeightPx']==88
assert len({c['id'] for c in data['clips']})==len(data['clips'])
for c in data['clips']:
    assert c['frames'] and all(f['holdMs']>0 for f in c['frames'])
    assert all(a['startMs']<=b['startMs'] for a,b in zip(c['frames'],c['frames'][1:]))
assert abs((game['teleportCast']+game['punchPrepare'])*1000-data['teleportHitMs'])<.001
assert abs(game['teleportCast']*1000-data['teleportArrivalMs'])<.001
assert abs(game['rockFire']*1000-data['rockHitMs'])<.001
assert game['comboTimes']==[1,1.5,2.5]
assert len(data['fragmentAxes'])==game['fragmentCount']==5
assert game['bodyArtScale']>0
unit=game['bodyHeight']*game['bodyArtScale']/data['bodyReferenceHeightPx']
assert abs(game['rockAhead']-data['rockCenterOffsetPx'][0]*unit)<.0001
assert abs(game['rockHeight']-data['rockCenterOffsetPx'][1]*unit)<.0001
assert abs(game['rockSize']-data['rockDiameterPx']*unit)<.0001
dest=ROOT/'unity/Assets/Chapter2/Resources/Chapter2EliteArt.json';expected=json.dumps(data,ensure_ascii=False,indent=2)+'\n'
if args.check:
    assert dest.read_text(encoding='utf-8')==expected,'Chapter2EliteArt.json is stale'
else:dest.write_text(expected,encoding='utf-8')
print('PASS: approved A3 art and gameplay contact bindings match')
