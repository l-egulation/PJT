"""Compile approved stage-two rules and retime references to existing sprites only."""
from pathlib import Path
import argparse,copy,hashlib,json,math
ROOT=Path(__file__).resolve().parents[2]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def encode(x):return json.dumps(x,ensure_ascii=False,indent=2)+'\n'
def emit(path,data,check):
    p=ROOT/path;text=encode(data)
    if check:
        if not p.exists() or p.read_text(encoding='utf-8')!=text:raise ValueError(str(path)+' is stale')
    else:p.write_text(text,encoding='utf-8')
def retime(clip,segments):
    """Piecewise monotone time warp. Split boundary keys using the same sprite."""
    paths=[];ms=[];begin=0
    def warp(t):
        old0=new0=0
        for old1,new1 in segments:
            if t<=old1:return round(new0+(t-old0)*(new1-new0)/(old1-old0))
            old0,new0=old1,new1
        return segments[-1][1]
    boundaries=[s[0] for s in segments]
    for path,duration in zip(clip['paths'],clip['milliseconds']):
        end=begin+duration;points=[begin]+[b for b in boundaries if begin<b<end]+[end]
        for a,b in zip(points,points[1:]):
            dt=warp(b)-warp(a)
            if dt>0:
                if paths and paths[-1]==path:ms[-1]+=dt
                else:paths.append(path);ms.append(dt)
        begin=end
    result=copy.deepcopy(clip);result.update(paths=paths,milliseconds=ms)
    assert sum(ms)==segments[-1][1] and all(t>0 for t in ms)
    assert set(paths)<=set(clip['paths'])
    return result
def main(check=False):
    stats=read('data/combat/stage2_monster_stats.json')['monsters'];geo=read('data/combat/stage2_monster_geometry.json')['monsters'];damage=read('data/combat/stage2_monster_damage_reaction.json')['monsters'];timing=read('data/combat/stage2_monster_attack_timing.json');projectiles=read('data/combat/stage2_projectile_rules.json')['monsters']
    art=read('resources/monsters/stage-02/normal-approved/runtime/unity/catalog.json');fx=read('data/combat/presentation/chapter2_attack_fx.json')
    for override in fx.get('bodyClips',[]):
        m=next(m for m in art['monsters'] if m['id']==override['monster']);m['clips']=[{k:v for k,v in override.items() if k!='monster'} if c['id']==override['id'] else c for c in m['clips']]
    species={'K4':'02_broom-stalker','B3':'draft_07_heavy_charger','M3':'01_thorn-root','R-selected':'03_gutter-spitter','H3':'draft_06_stationary_turret'}
    bank={'schema':1,'monsters':[]};binding={'scope':'approved chapter2 combat and existing-frame retiming','bodyClips':[],'sourceFiles':{}}
    for p in sorted((ROOT/'data/combat').glob('stage2_*.json')):binding['sourceFiles'][p.name]=hashlib.sha256(p.read_text(encoding='utf-8').encode('utf-8')).hexdigest()
    for m in art['monsters']:
        mid=m['id'];s=stats[mid];g=geo[mid];d=damage[mid];attack=next(c for c in m['clips'] if c['id']=='attack');original=copy.deepcopy(attack)
        if mid=='M3':
            nums=timing[mid]['sourceFrameNumbers'];attack['paths']=[original['paths'][i-1] for i in nums];attack['milliseconds']=timing[mid]['milliseconds']
        else:
            segments={'K4':[(550,650),(2650,1300),(3350,2000)],'B3':[(550,450),(600,600),(1500,1500)],'R-selected':[(680,650),(2040,1350)],'H3':[(1600,1600),(5800,2500)]}[mid]
            attack=retime(original,segments);m['clips']=[attack if c['id']=='attack' else c for c in m['clips']]
        binding['bodyClips'].append({'monster':mid,**attack,'originalFrameCount':len(original['paths']),'originalDurationMs':sum(original['milliseconds'])})
        melee=mid in ['K4','B3','M3'];primary={'K4':'spray','B3':'slam','M3':'thrust','R-selected':'projectile','H3':'orb'}[mid];hit=d['attacks'][primary]
        c={'id':mid,'species':species[mid],'name':m['name'],'hp':s['maxHP'],'moveSpeed':s['moveSpeed'],'startRange':s['attackStartRange'],'maxStartHeight':2 if melee else -1,'cooldown':s['cooldownSeconds'],'windup':s.get('windupSeconds',.55),'active':s.get('attackActiveSeconds',.45),'recovery':s.get('recoverySeconds',.59),'hurt':g['hurt']['boxPixels'],'attack':g['attack']['boxPixels'],'damage':hit['damagePerHit'],'playerLock':hit['playerMovementLockSeconds'],'enemyStagger':.3,'firstDelayMin':.3,'firstDelayMax':.8,'hitInterval':.3 if mid=='K4' else .15,'maxHits':3 if mid in ['K4','M3'] else 1,'pushDistance':.15 if mid in ['K4','M3'] else 0,'pushSeconds':.08 if mid in ['K4','M3'] else 0,'hitWindowStart':[],'hitWindowEnd':[]}
        if mid=='M3':c.update(windup=.55,active=.45,recovery=.59,hitWindowStart=[w['startInclusiveMs']/1000 for w in timing[mid]['damageWindows']['windows']],hitWindowEnd=[w['endExclusiveMs']/1000 for w in timing[mid]['damageWindows']['windows']])
        if mid=='B3':c.update(charge=g['charge']['boxPixels'],chargeDamage=d['attacks']['charge']['damagePerHit'],chargeSpeed=12,chargeDistance=6,chargeSeconds=.5,slamWindup=.45,slamActive=.15)
        if not melee:
            p=projectiles[mid]
            minimum=p['minimumFlightSeconds'];assert math.isfinite(minimum) and 0<minimum<=p['maxLifetimeSecondsFromRelease'],mid+' invalid minimum flight time'
            c.update(projectileSpeed=p['horizontalSpeedUnitsPerSecond'],minimumFlightSeconds=minimum,apex=p['apexRiseAboveLaunchUnits'],projectileLifetime=p['maxLifetimeSecondsFromRelease'],launchX=2.1 if mid=='R-selected' else 3.4,launchY=3.0 if mid=='R-selected' else 3.5)
        if mid=='H3':c.update(hazard=g['groundHazard']['boxPixels'],hazardDamage=d['attacks']['groundHazard']['damagePerHit'],hazardDuration=4,hazardFirst=.5,hazardTick=.5)
        bank['monsters'].append(c)
    # Melee wind-up cue (playtest 2026-11-06, same look as the stage 1 melee monsters): shake, red rising to tintMax, flash.
    cue=fx['windupCue'];assert set(cue['melee'])<={'K4','B3','M3'} and cue['shakeStepSeconds']>0 and 0<=cue['shakeWidenFrom']<=1 and cue['flashSeconds']>0 and 0<cue['tintMax']<=1
    assert all(len(cue[k])==3 and all(0<=v<=255 for v in cue[k]) for k in ('flashColor','tintColor'))
    # Separate selected ink FX spans the approved spray; retain every existing FX path.
    ink=next(c for c in fx['clips'] if c['id']=='k4-ink');adapt=retime({'paths':ink['frames'],'milliseconds':ink['milliseconds']},[(sum(ink['milliseconds']),650)]);ink['frames']=adapt['paths'];ink['milliseconds']=adapt['milliseconds']
    emit('unity/Assets/Chapter2/Resources/Chapter2ApprovedNormals.json',bank,check)
    emit('data/combat/presentation/chapter2_approved_motion_bindings.json',binding,check)
    emit('unity/Assets/Chapter2/Resources/Chapter2MonsterArt.json',art,check)
    emit('unity/Assets/Chapter2/Resources/Chapter2AttackFx.json',fx,check)
    print('PASS approved 5-species configuration and existing-frame timings; no pixel bytes generated')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args();main(a.check)
