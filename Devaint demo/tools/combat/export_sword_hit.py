"""Publish the approved sword contact presentation timings; gameplay is unchanged."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / 'data/combat/presentation/base_sword_hit.json'
target = ROOT / 'unity/Assets/ForestCombatV2/Resources/BasicSwordHit/settings.json'
data = json.loads(source.read_text(encoding='utf-8'))
assert 0 < data['flashHold'] < data['flashDuration'] < 1
ends = data['frameEnds']
assert data['schemaVersion'] == 1 and data['frameCount'] == len(ends) == 6
assert all(a < b for a, b in zip([0] + ends[:-1], ends))
assert data['pixelsPerUnit'] == 32
particles = data['particles']
assert 0 < particles['count'] <= 32
assert 0 < particles['spreadHalfAngle'] < 90
assert particles['exitOffset'] >= 0
assert 0 < particles['minSpeed'] <= particles['maxSpeed']
assert 0 < particles['minLifetime'] <= particles['maxLifetime'] < 1
assert all(particles[key] > 0 for key in ['smallSize', 'lineLength', 'lineWidth'])
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(target)
