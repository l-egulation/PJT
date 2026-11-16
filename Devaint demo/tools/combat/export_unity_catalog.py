"""Convert design JSON into Unity JsonUtility's flat, array-only schema."""
import json
from pathlib import Path
from melee_source import load_melee

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / 'data/combat'
PROJECT = ROOT / 'unity'


def box(s):
    if not s:
        return None
    return dict(width=s['size'][0], height=s['size'][1], x=s['right_offset'][0], y=s['right_offset'][1])


def export():
    source=load_melee(HERE)
    forms=[]
    for f in source['forms']:
        b=f['basic']; specials=[]
        for s in f['specials']:
            hits=[]
            for h in s['hits']:
                c=h['control']
                hits.append(dict(time=h['time'],damage=h['damage'],active=h['active'],anchor=h['anchor'],targets=h['targets'],box=box(h['shape']),
                                 root=c.get('root',0),stun=c.get('stun',0),push=c.get('push_distance',0),pushSeconds=c.get('push_seconds',.1),pushWithCaster=c.get('push_with_caster',False)))
            m=s['movement']; e=s['independent_effect']; movement=None; effect=None
            if m:
                movement=dict(type=m.get('type','dash'),wallPolicy=m.get('wall_policy','end'),start=m.get('start',0),duration=m.get('duration',0),
                              distance=m.get('distance',m.get('max_distance',0)),speed=m.get('speed',0),wallRecovery=m.get('wall_recovery',0),
                              gap=m.get('target_gap',.25),primaryPush=m.get('primary_push',False),endDamage=m.get('primary_end_damage',0),
                              recastWindow=m.get('recast_window',0),targetRadius=m.get('target_radius',0),targetBox=box(m.get('target_box')))
            if e:
                xy=e.get('right_spawn',e.get('spawn_offset',[0,0]))
                effect=dict(type=e['type'],duration=e.get('duration',e.get('lifetime',0)),hp=e.get('hp',e.get('shield_hp',0)),
                            reduction=e.get('damage_reduction',0),speed=e.get('speed',0),distance=e.get('distance',0),radius=e.get('taunt_radius',0),
                            x=xy[0],y=xy[1],box=box(e.get('shape')),parryWindow=e.get('window',0),parryDamage=e.get('melee_reflect_base',0))
            charge=s.get('charge',{})
            specials.append(dict(id=s['id'],name=s['name'],slot=s['slot'],description=s['rules'],vertical=s['vertical'],facing=s['facing'],
                                 cooldown=s['cooldown'],activation=s['activation_time'],end=s['motion_end'],inputSpeed=s['input_move_speed'],hits=hits,movement=movement,effect=effect,
                                 invulStart=[v[0] for v in s['invulnerability']],invulEnd=[v[1] for v in s['invulnerability']],
                                 chargeMin=charge.get('min_hold',0),chargeMax=charge.get('max_hold',0),chargeMinDamage=charge.get('min_damage',0),chargeMaxDamage=charge.get('max_damage',0),
                                 capacity=s.get('charges',{}).get('capacity',1)))
            if charge.get('hold_until_release') or charge.get('release_windup'):
                specials[-1].update(chargeHoldUntilRelease=charge.get('hold_until_release',False),
                                    chargeReleaseWindup=charge.get('release_windup',0))
            if 'vertical_freeze' in s:
                specials[-1]['verticalFreeze'] = s['vertical_freeze']
            # Transport only. FrameTimeline/ActionTrackDefinition in C# owns
            # marker resolution for runtime, editor, startup and build gates.
            if 'action_tracks' in s:
                specials[-1]['actionTracks'] = s['action_tracks']
        basics=[dict(damage=d,duration=t,impact=i,active=a,box=box(sh)) for d,t,i,a,sh in zip(b['damages'],b['motion_durations'],b['local_hit_times'],b['active_durations'],b['shapes'])]
        forms.append(dict(id=f['id'],name=f['name'],category=f['category'],inputSpeed=b['input_move_speed'],basics=basics,specials=specials))
    assert len(forms)==7 and sum(len(f['specials']) for f in forms)==28
    ranged=json.loads((HERE/'ranged_basic_design.json').read_text(encoding='utf-8-sig'))
    skills_path=HERE/'launcher_skills.json'
    if skills_path.exists():
        source=json.loads(skills_path.read_text(encoding='utf-8-sig'))
        if source['status']=='user_approved_launcher_ink_skills_20261106':
            l=ranged['launcher']
            original=dict(name='볼 탄환',kind='ranged',damage=l['damage'],duration=l['base_motion_duration'],impact=l['windup'],active=.001,box=None,
                          ranged=dict(type='ball',distance=l['range_units'],speed=l['speed'],radius=l['hit_shape']['radius'],spawnHeight=l['spawn_center']['right'][1],spawnX=l['spawn_center']['right'][0],angles=[0]))
            # Every ink keeps the ball as S. The ink selects one D skill, like the other ranged forms.
            specials=[]
            for key in ['basic','laser','shotgun','grenade']:
                a=source[key]; kind='bomb' if key=='grenade' else 'ranged'
                payload=dict(a['bomb']) if kind=='bomb' else dict(a['ranged'],noBasicStagger=True)
                if kind=='ranged' and payload['type']!='laser':payload.setdefault('angles',[0])
                skill=dict(id=a['id'],name=a['name'],slot=a['slot'],kind=kind,cooldown=a['cooldown'],activation=a['windup'],end=a['duration'],
                           inputSpeed=source['ground_input_speed'],capacity=1,vertical='normal',facing=source['facing'],invulStart=[],invulEnd=[],
                           hits=[dict(time=a['windup'],damage=a['charge']['min_damage'] if 'charge' in a else a['damage'],active=.001,
                                      anchor='bomb_projectile' if kind=='bomb' else 'ranged_projectile',targets='all')])
                skill[kind]=payload
                if 'charge' in a:
                    c=a['charge'];skill.update(chargeMin=c['min_hold'],chargeMax=c['max_hold'],chargeMinDamage=c['min_damage'],chargeMaxDamage=c['max_damage'])
                    if c.get('steps'):skill['chargeSteps']=c['steps']
                specials.append(skill)
            forms.append(dict(id='launcher',name='발사기',category='원거리',inputSpeed=0,basics=[original],specials=specials))
    b=ranged.get('bomb',{})
    if b.get('status')=='basic_user_approved':
        bomb=dict(angle=b['angle_degrees'],speed=b['initial_speed'],gravity=b['gravity'],radius=b['hit_shape']['radius'],blastRadius=b['explosion_radius'],
                  lifetime=b['implementation_defaults']['cleanup_lifetime_seconds'],spawnHeight=b['implementation_defaults']['spawn_height'],spawnGap=b['implementation_defaults']['spawn_gap'])
        pending=[dict(id='bomb_pending_'+str(i),name='설계 대기',unavailable=True,cooldown=1,activation=0,end=0,capacity=1,vertical='normal',facing='windup_start',hits=[],invulStart=[],invulEnd=[]) for i in range(4)]
        burst=b.get('basic_special_design',{})
        if burst.get('status')=='user_approved_initial_spec':
            special_bomb=dict(bomb,angle=burst['angles_degrees'][0],blastRadius=burst['explosion_radius'])
            pending[0]=dict(id='bomb_basic_special',name='폭탄 3연속 투척',slot='base',kind='bomb',bomb=special_bomb,
                            cooldown=burst['cooldown'],activation=burst['windup'],end=burst['motion_duration'],inputSpeed=burst['ground_input_speed'],
                            capacity=1,vertical='normal',facing=burst['facing'],invulStart=[],invulEnd=[],
                            hits=[dict(time=t,damage=burst['damage_per_bomb'],active=.001,anchor='bomb_projectile',targets='all') for t in burst['local_release_times']])
        area=b.get('range_special_design',{})
        if area.get('status')=='user_approved_initial_spec':
            area_bomb=dict(bomb,angle=area['angle_degrees'],speed=area['initial_speed'],radius=area['projectile_diameter']/2,blastRadius=area['explosion_radius'])
            pending[1]=dict(id='bomb_range_special',name='대형 폭탄',slot='range',kind='bomb',bomb=area_bomb,
                            cooldown=area['cooldown'],activation=area['windup'],end=area['motion_duration'],inputSpeed=area['ground_input_speed'],
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=area['windup'],damage=area['damage'],active=.001,anchor='bomb_projectile',targets='all')])
        move=b.get('movement_special_design',{})
        if move.get('status')=='user_approved_initial_spec':
            missile=dict(speed=move['speed'],distance=move['max_distance'],blastRadius=move['explosion_radius'],recovery=move['recovery'],
                         manualDelay=move['manual_detonation_min_flight_age'],recoilHeight=move['recoil_height'],recoilDistance=move['recoil_distance'])
            if move.get('distance_bonus_max_total_damage'):
                assert move['distance_bonus_max_total_damage'] >= move['explosion_damage'] and move['distance_bonus_full_units'] > 0
                missile.update(distanceBonusMaxDamage=move['distance_bonus_max_total_damage'],distanceBonusFullDistance=move['distance_bonus_full_units'])
            pending[2]=dict(id='bomb_move_special',name='미사일 변신',slot='move',kind='missile',missile=missile,
                            cooldown=move['cooldown'],activation=move['windup'],end=move['windup']+move['max_distance']/move['speed']+move['recovery'],
                            inputSpeed=move['ground_input_speed'],capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=move['windup'],damage=move['explosion_damage'],active=.001,anchor='missile_explosion',targets='all')])
        follow=b.get('followup_special_design',{})
        if follow.get('status')=='user_approved_initial_spec':
            timed=dict(bomb,angle=follow['angle_degrees'],speed=follow['initial_speed'],radius=follow['projectile_diameter']/2,
                       blastRadius=follow['explosion_radius'],gravity=follow['gravity'],fuseSeconds=follow['fuse_seconds'],
                       lifetime=follow['unlanded_timeout_seconds'],explodeOnExpiry=True)
            pending[3]=dict(id='bomb_followup_special',name='착지 시한폭탄',slot='followup',kind='bomb',bomb=timed,
                            cooldown=follow['cooldown'],activation=follow['windup'],end=follow['motion_duration'],inputSpeed=follow['ground_input_speed'],
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=follow['windup'],damage=follow['damage'],active=.001,anchor='bomb_projectile',targets='all')])
        forms.append(dict(id='bomb',name='폭탄',category='원거리',inputSpeed=0,
                          basics=[dict(kind='bomb',bomb=bomb,damage=b['damage'],duration=b['motion_duration'],impact=b['windup'],active=.001,box=None)],specials=pending))
    s=ranged.get('shuriken',{})
    if s.get('status')=='basic_geometry_user_approved':
        shot=dict(type='shuriken',distance=s['range_units'],speed=s['speed'],radius=s['hit_shape']['radius'],
                  spawnHeight=s['spawn_center']['right'][1],spawnX=s['spawn_center']['right'][0],angles=[0])
        homing=s.get('homing')
        if homing:
            # Weak homing of both basic throws: degrees/s turn, acquisition half-cone and deflection cap from the throw direction.
            shot.update(homingTurnRate=homing['turn_rate_degrees_per_second'],homingCone=homing['acquire_cone_half_angle_degrees'],
                        homingMaxAngle=homing['max_deflection_degrees'])
        pending=[dict(id='shuriken_pending_'+str(i),name='설계 대기',unavailable=True,cooldown=1,activation=0,end=0,capacity=1,
                      vertical='normal',facing='windup_start',hits=[],invulStart=[],invulEnd=[]) for i in range(4)]
        special=s.get('basic_special_design',{})
        if special.get('status')=='user_approved_initial_spec':
            spawn=special['spawn_center']
            special_shot=dict(type='shuriken_special',ignoreTerrain=special.get('ignore_terrain',False),pierce=True,noBasicStagger=True,distance=special['range_units'],
                              speed=special['speed'],radius=special['diameter']/2,spawnX=spawn['right'][0],spawnHeight=spawn['right'][1],angles=[0])
            pending[0]=dict(id='shuriken_basic_special',name='큰 관통 수리검',slot='base',kind='ranged',ranged=special_shot,
                            cooldown=special['cooldown'],activation=special['windup'],end=special['motion_duration'],inputSpeed=special['ground_input_speed'],
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=special['windup'],damage=special['damage'],active=.001,anchor='ranged_projectile',targets='all')])
        basics=[dict(kind='ranged',ranged=shot,damage=d,duration=t,impact=i,active=.001,box=None)
                for d,t,i in zip(s['damages'],s['motion_durations'],s['local_release_times'])]
        area=s.get('range_special_design',{})
        if area.get('status')=='user_approved_initial_spec':
            spawn=area['spawn_center']
            blast_shot=dict(type='shuriken_range',ignoreTerrain=area.get('ignore_terrain',False),noBasicStagger=True,blastRadius=area['explosion_radius'],distance=area['range_units'],
                            speed=area['speed'],radius=area['projectile_diameter']/2,spawnX=spawn['right'][0],spawnHeight=spawn['right'][1],angles=[0])
            pending[1]=dict(id='shuriken_range_special',name='폭발 수리검',slot='range',kind='ranged',ranged=blast_shot,
                            cooldown=area['cooldown'],activation=area['windup'],end=area['motion_duration'],inputSpeed=area['ground_input_speed'],
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=area['windup'],damage=area['explosion_damage'],active=.001,anchor='ranged_projectile',targets='all')])
        move=s.get('movement_special_design',{})
        if move.get('status')=='user_approved_initial_spec':
            spawn=move['spawn_center']
            move_shot=dict(type='shuriken_move',ignoreTerrain=move.get('ignore_terrain',False),pierce=move['pierce'],noBasicStagger=True,distance=move['range_units'],speed=move['speed'],radius=move['projectile_diameter']/2,
                           spawnX=spawn['right'][0],spawnHeight=spawn['right'][1],angles=[0],returnDelay=move['return_delay_after_arrival_seconds'],behindGap=move['behind_target_gap'])
            pending[2]=dict(id='shuriken_move_special',name='수리검 뒤이동·복귀',slot='move',kind='ranged',ranged=move_shot,
                            cooldown=move['cooldown'],activation=move['windup'],end=move['motion_duration'],inputSpeed=5,
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=move['windup'],damage=move['damage'],active=.001,anchor='ranged_projectile',targets='all')])
        follow=s.get('followup_special_design',{})
        if follow.get('status')=='user_approved_initial_spec':
            spawn=follow['spawn_center']
            rotating=dict(type='shuriken_followup',ignoreTerrain=follow.get('ignore_terrain',False),pierce=True,noBasicStagger=True,distance=follow['range_units'],speed=follow['speed'],
                          radius=follow['projectile_diameter']/2,spawnX=spawn['right'][0],spawnHeight=spawn['right'][1],angles=[0],hitInterval=follow['per_target_hit_interval'])
            pending[3]=dict(id='shuriken_followup_special',name='전진 회전 수리검',slot='followup',kind='ranged',ranged=rotating,
                            cooldown=follow['cooldown'],activation=follow['windup'],end=follow['windup']+follow['recovery'],inputSpeed=5,
                            capacity=1,vertical='normal',facing='windup_input_else_facing',invulStart=[],invulEnd=[],
                            hits=[dict(time=follow['windup'],damage=follow['tick_damage'],active=.001,anchor='ranged_projectile',targets='all')])
        forms.append(dict(id='shuriken',name='수리검',category='원거리',inputSpeed=s['ground_input_speed'],basics=basics,specials=pending))
    # Bare hands own their approved two-hit balance independently of the first sword.
    unarmed = json.loads((HERE/'unarmed_basic.json').read_text(encoding='utf-8'))['form']
    forms.append(unarmed)
    # The bag (I) shows each equipped weapon's current special with this player-facing line (ui-and-interaction.md).
    summaries=json.loads((HERE/'skill_descriptions.json').read_text(encoding='utf-8-sig'))['skills']
    used=set()
    for f in forms:
        for s in f['specials']:
            if s.get('unavailable'):
                continue
            assert summaries.get(s['id']), f"skill_descriptions.json has no line for {s['id']}"
            s['summary']=summaries[s['id']]; used.add(s['id'])
    assert used==set(summaries), f"skill_descriptions.json has unused ids: {sorted(set(summaries)-used)}"
    path=PROJECT/'Assets/Deviant/Combat/Resources/MeleeCatalog.json'
    path.parent.mkdir(parents=True,exist_ok=True)
    reaction = json.loads((HERE/'enemy_hit_reaction.json').read_text(encoding='utf-8-sig'))
    assert reaction['schemaVersion'] == 1 and reaction['basicPushDistance'] >= 0 and reaction['basicPushSeconds'] > 0 and reaction['basicStaggerSeconds'] > 0
    weapon_switch = json.loads((HERE/'weapon_switch.json').read_text(encoding='utf-8-sig'))
    assert weapon_switch['schemaVersion'] == 1 and 0 <= weapon_switch['afterHitWindupReduction'] < 1
    ambush = json.loads((HERE/'stealth_ambush.json').read_text(encoding='utf-8-sig'))
    assert ambush['schemaVersion'] == 1 and ambush['firstHitBonusDamage'] >= 0
    ambush_payload = {key: ambush[key] for key in ('firstHitBonusDamage', 'useSourceBonuses', 'stealthFirstHitBonusDamage', 'smokeFirstHitBonusDamage', 'stealthExitBasicSpeedBonus', 'stealthExitSeconds') if key in ambush}
    for key in ('firstHitBonusDamage', 'stealthFirstHitBonusDamage', 'smokeFirstHitBonusDamage', 'stealthExitBasicSpeedBonus', 'stealthExitSeconds'):
        if key in ambush_payload: assert 0 <= ambush_payload[key] < float('inf')
    path.write_text(json.dumps(dict(forms=forms, enemyHitReaction=reaction, weaponSwitch=weapon_switch, stealthAmbush=ambush_payload),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Exported {len(forms)} forms / 28 melee specials / launcher ink skills / bomb and shuriken basics + approved shuriken special: {path}')
    # This schema already matches JsonUtility. Semantic validation belongs to the
    # shared C# LocomotionProfile compiler used by runtime and authoring.
    movement_text = (HERE/'locomotion/player.json').read_text(encoding='utf-8-sig')
    json.loads(movement_text)
    movement_path = path.with_name('PlayerLocomotion.json')
    movement_path.write_text(movement_text, encoding='utf-8')
    print(f'Exported player locomotion: {movement_path}')
    playtest_text = (HERE/'playtest_environment.json').read_text(encoding='utf-8-sig')
    json.loads(playtest_text)
    path.with_name('PlaytestEnvironment.json').write_text(playtest_text, encoding='utf-8')
    checkpoint_text = (HERE/'manual_checkpoint.json').read_text(encoding='utf-8-sig')
    checkpoint = json.loads(checkpoint_text)
    assert checkpoint['schemaVersion'] == 1
    for key in ('installSeconds', 'enterSeconds', 'appearSeconds'):
        assert 0 < checkpoint[key] < float('inf'), f'Invalid checkpoint duration: {key}'
    path.with_name('ManualCheckpoint.json').write_text(checkpoint_text, encoding='utf-8')


if __name__=='__main__': export()
