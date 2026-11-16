"""Export blade anchors and ribbon presentation settings; native sprite pixels are untouched."""
import argparse,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check',action='store_true')
args=parser.parse_args()
source=ROOT/'data/combat/presentation/whip_trail.json'
target=ROOT/'unity/Assets/Deviant/Combat/Resources/WhipTrail.json'
data=json.loads(source.read_text(encoding='utf-8'))
for pose in data['poses']:
    if not (ROOT/'unity/Assets/Deviant/Combat/Resources/CharacterApproved11/whip'/(pose['frame']+'.png')).is_file():
        raise SystemExit('Missing approved blade frame: '+pose['frame'])
content=(json.dumps(data,indent=2)+'\n').encode('utf-8')
if args.check:
    if not target.is_file() or target.read_bytes()!=content:raise SystemExit('WhipTrail.json is stale')
else:target.write_bytes(content)
print('Whip curve anchors and Unity resource match.')
