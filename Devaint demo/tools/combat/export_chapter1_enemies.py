"""Compile all six approved species for the meadow; artwork remains unchanged."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def compile_definitions(raw):
    catalog = json.loads(raw)
    c = catalog['common']
    definitions = []
    for m in catalog['monsters']:
        a = m['attack']; p = a['phaseDurationsMs']
        charge = a.get('charge', {})
        definitions.append(dict(
            id=m['id'], sourceSha256=hashlib.sha256(raw).hexdigest(),
            maxHP=m['maxHP'], damage=m['attackDamage'], walkSpeed=m['walkAndReturnSpeedUnitPerSec'],
            senseX=c['detection']['horizontalFeetMaxUnit'], senseY=c['detection']['verticalFeetMaxUnit'],
            sensePeriod=c['detection']['pollIntervalMs']/1000,
            pathPeriod=c['pursuit']['blockedPathRetryIntervalMs']/1000,
            leash=c['pursuit']['spawnHorizontalLeashUnit'],
            reengageTargetLimit=c['return']['reengageTargetSpawnDistanceInclusiveMaxUnit'],
            searchWait=c['pursuit']['searchWaitMs']/1000, stagger=c['hitStaggerMs']/1000,
            epsilon=c['pursuit']['leashPhysicalEpsilonUnit'],
            cliffMargin=c['pursuit']['groundSafety']['cliffStopMarginUnit'],
            startGap=m['attackStart']['maxUnit'], startHeight=m['attackStart']['feetVerticalDifferenceMaxUnit'],
            minimumStartDistance=m['attackStart']['minUnit'] or 0,
            prepare=sum(p.get(k, 0) for k in ('prepare', 'raise', 'hold', 'fall'))/1000,
            active=p['active']/1000, brake=p.get('brake', 0)/1000,
            recovery=p['recovery']/1000, interval=a['intervalMs']/1000,
            chargeDistance=charge.get('totalDistanceIncludingBrakeUnit', 0),
            chargeSpeed=charge.get('constantSpeedUnitPerSec', 0),
            moveCycle=m['animationPlayback']['moveCycleMs']/1000,
            deathDuration=m['animationPlayback']['deathTotalMs']/1000,
            bodySize=m['body']['sizeUnit'], bodyOffset=m['body']['offsetRightUnit'],
            attackSize=a['box']['sizeUnit'], attackOffset=a['box']['offsetRightUnit'],
            attackFrameDurations=[f['durationMs']/1000 for f in a['frames']]))
    return dict(monsters=definitions)


def main():
    raw = (ROOT/'data/monsters/chapter1_approved.json').read_bytes()
    output = ROOT/'unity/Assets/Deviant/Combat/Resources/Chapter1Enemies.json'
    output.write_text(json.dumps(compile_definitions(raw), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Exported six approved chapter-one enemy definitions.')


if __name__ == '__main__':
    main()
