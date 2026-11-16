"""Export approved mouse data; restore unchanged sprites from the separate bundle."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'data/monsters'
OUT = ROOT / 'unity/Assets/Deviant/Combat/Resources/MousePilot'


def main():
    catalog_path = BASE / 'chapter1_approved.json'
    raw = catalog_path.read_bytes()
    catalog = json.loads(raw)
    m = next(m for m in catalog['monsters'] if m['id'] == '01_field_mouse')
    c = catalog['common']; attack = m['attack']; phase = attack['phaseDurationsMs']
    d = dict(id=m['id'], sourceSha256=hashlib.sha256(raw).hexdigest(),
             maxHP=m['maxHP'], damage=m['attackDamage'], walkSpeed=m['walkAndReturnSpeedUnitPerSec'],
             senseX=c['detection']['horizontalFeetMaxUnit'], senseY=c['detection']['verticalFeetMaxUnit'],
             sensePeriod=c['detection']['pollIntervalMs']/1000, pathPeriod=c['pursuit']['blockedPathRetryIntervalMs']/1000,
             leash=c['pursuit']['spawnHorizontalLeashUnit'], reengageTargetLimit=c['return']['reengageTargetSpawnDistanceInclusiveMaxUnit'],
             searchWait=c['pursuit']['searchWaitMs']/1000, stagger=c['hitStaggerMs']/1000,
             epsilon=c['pursuit']['leashPhysicalEpsilonUnit'], cliffMargin=c['pursuit']['groundSafety']['cliffStopMarginUnit'],
             startGap=m['attackStart']['maxUnit'], startHeight=m['attackStart']['feetVerticalDifferenceMaxUnit'],
             prepare=phase['prepare']/1000, active=phase['active']/1000, recovery=phase['recovery']/1000,
             interval=attack['intervalMs']/1000, moveCycle=m['animationPlayback']['moveCycleMs']/1000,
             deathDuration=m['animationPlayback']['deathTotalMs']/1000,
             bodySize=m['body']['sizeUnit'], bodyOffset=m['body']['offsetRightUnit'],
             attackSize=attack['box']['sizeUnit'], attackOffset=attack['box']['offsetRightUnit'],
             attackFrameDurations=[s['durationMs']/1000 for s in attack['frames']])
    assert m['attackStart']['overlappingBodyGapPolicy'] == 'clamp-negative-body-gap-to-zero'
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT/'Definition.json').write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Exported approved field mouse definition; PNGs are restored separately.')


if __name__ == '__main__':
    main()
