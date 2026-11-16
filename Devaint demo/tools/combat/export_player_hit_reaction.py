"""Copy the player knockback source into the Unity resource without changing other numbers."""
import argparse,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check',action='store_true')
args=parser.parse_args()
source=ROOT/'data/combat/player_hit_reaction.json'
target=ROOT/'unity/Assets/Deviant/Combat/Resources/PlayerHitReaction.json'
content=(json.dumps(json.loads(source.read_text(encoding='utf-8')),indent=2)+'\n').encode('utf-8')
if args.check:
    if not target.is_file() or target.read_bytes()!=content:raise SystemExit('PlayerHitReaction.json is stale')
else:target.write_bytes(content)
print('Player knockback source and Unity resource match.')
