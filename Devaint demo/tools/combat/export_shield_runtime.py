"""Export the approved shield-specific geometry and decoy retaliation rules."""
import json
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[2]
source = ROOT / 'data/combat/shield_runtime_rules.json'
target = ROOT / 'unity/Assets/Deviant/Combat/Resources/ShieldRuntimeRules.json'
data = json.loads(source.read_text(encoding='utf-8'))
assert data['schemaVersion'] == 1
assert 0 <= data['parryProjectileReach'] <= 1
assert data['decoyDamage'] > 0 and data['decoyRadius'] > 0
assert not data['decoyBurstOnReplacement']
assert 0 < data['parrySuccessCooldownSeconds'] <= 1.2
assert 0 < data['parrySuccessRecoverySeconds'] <= .45
retaliation = json.loads((ROOT / 'data/combat/shield_decoy_retaliation.json').read_text(encoding='utf-8'))['retaliation']
assert data['decoyDamage'] == retaliation['fixedDamage']
assert data['decoyRadius'] == retaliation['radiusUnits']
text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
if '--check' in sys.argv:
    assert target.read_text(encoding='utf-8') == text
else:
    target.write_text(text, encoding='utf-8')
print('Shield runtime rules match source.')
