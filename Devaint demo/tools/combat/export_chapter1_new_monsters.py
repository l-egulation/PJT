"""Export selected single PNGs and explicitly approved prototype combat rules."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def export():
    source = ROOT / 'data/combat/chapter1_new_monsters.json'
    data = json.loads(source.read_text(encoding='utf-8'))
    output = []
    for spec in data['monsters']:
        b = spec['behavior']
        ranged = b['movement'] == 'stationary'
        immovable = b.get('immovable', False)
        assert isinstance(immovable, bool) and (not immovable or ranged)
        assert b['hp'] > 0 and b['damage'] > 0
        assert spec['motionsReady'] and spec['frameCount'] > 1
        definition = dict(id=spec['id'], maxHP=b['hp'], damage=b['damage'],
                          bodySize=spec['bodySize'], bodyOffset=spec['bodyOffset'],
                          hurtSize=spec['hurtSize'], hurtOffset=spec['hurtOffset'],
                          attackSize=spec['hurtSize'], attackOffset=spec['hurtOffset'],
                          walkSpeed=0 if ranged else b['approachSpeed'], senseX=b['activationDistance'],
                          senseY=b['activationDistance'], sensePeriod=.1, pathPeriod=.1,
                          leash=1000, reengageTargetLimit=1000, searchWait=0, stagger=.3,
                          epsilon=.01, cliffMargin=.1, startGap=b['activationDistance'] if ranged else 12, startHeight=2,
                          prepare=b['inflationSeconds']+b['warningSeconds'] if ranged else b['telegraphSeconds'],
                          active=.01 if ranged else b['chargeDistance']/b['chargeSpeed'],
                          recovery=b['cooldownAfterExplosionSeconds'] if ranged else b['cooldownAfterChargeSeconds'],
                          interval=b['interval'], chargeDistance=0 if ranged else b['chargeDistance'],
                          chargeSpeed=0 if ranged else b['chargeSpeed'], deathDuration=.3,
                          attackFrameDurations=[1], moveCycle=1, hitStaggerImmune=False,
                          immovable=immovable)
        # Optional red wind-up overlay (playtest 2026-11-06): RGB and the alpha it reaches at the end of the telegraph.
        assert len(b.get('windupTint',[0,0,0])) == 3 and all(0 <= c <= 255 for c in b.get('windupTint',[0,0,0]))
        assert 0 <= b.get('windupTintMax',0) <= 1
        # Optional launch of the player its charge hits (playtest 2026-11-06): units/s along the charge for seconds, plus up.
        assert b.get('playerLaunchSpeed',0) >= 0 and b.get('playerLaunchSeconds',0) >= 0 and b.get('playerLaunchUp',0) >= 0
        resource = 'Chapter1NewMonsters/' + spec['sourceAssetId'] + '/base'
        src = ROOT / spec['sourceImage']
        # Image deployment is owned by resources/tools/assets.py, not this gameplay compiler.
        output.append(dict(id=spec['id'], name=spec['name'], resource=resource, ranged=ranged,
                           windupSeconds=b.get('inflationSeconds',0),warningSeconds=b.get('warningSeconds',0),
                           ascentSeconds=b.get('ascentSeconds',0),offscreenSeconds=b.get('offscreenSeconds',0),
                           fallSeconds=b.get('fallSeconds',0),stopBeforeChargeSeconds=b.get('stopBeforeChargeSeconds',0),
                           windupTint=b.get('windupTint',[0,0,0]),windupTintMax=b.get('windupTintMax',0),
                           playerLaunchSpeed=b.get('playerLaunchSpeed',0),playerLaunchSeconds=b.get('playerLaunchSeconds',0),playerLaunchUp=b.get('playerLaunchUp',0),
                           definition=definition, firstDelayMin=b.get('firstAttackDelayMin',0), firstDelayMax=b.get('firstAttackDelayMax',0),
                           blastSize=[b['damageWidth'],b['damageHeight']] if ranged else [0,0],
                           sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest()))
    result = dict(schema=data['schema'], status=data['status'], monsters=output)
    dest = ROOT / 'unity/Assets/ForestCombatV2/Resources/Chapter1NewMonsters.json'
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Exported 2 prototype rules; image deployment remains separate.')


if __name__ == '__main__':
    export()
