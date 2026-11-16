"""Standalone combat UI kit driven by the current weapon/ink design catalog.

No mockup extraction. Generated originals are copied byte-for-byte. Sprite export
only trims transparent margins. Composites retain their editable layer recipes.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import subprocess
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT/'ui/combat-hud-expanded-20261106'
OLD = ROOT/'ui/combat-hud-rebuilt-20261106'
MAIN = ROOT.parent
W, H = 1670, 942
SPRITES, RECORDS, RECIPES = {}, [], []
INK = '#1b1c18'
IVORY = '#fff0da'
TEAL = '#00d4d0'
RED = '#fa6159'
LABELS = {
 'minimap-frame-compact':'미니맵 · 출발 카드와 같은 비율', 'pigment-currency':'안료 재화 · 새 원화',
 'health-fill-red':'빨간 체력 채움', 'drop-purple':'보라 잉크', 'drop-colorless':'무색 범용 칸',
 'weapon-whip':'채찍', 'weapon-rapier':'레이피어', 'weapon-dagger':'한손 단검', 'weapon-shield':'방패',
 'weapon-greatsword':'대검', 'weapon-launcher':'발사기', 'weapon-shuriken':'수리검', 'weapon-bomb':'폭탄',
 'weapon-launcher-laser':'레이저', 'weapon-launcher-shotgun':'샷건', 'weapon-launcher-grenade':'유탄발사기',
 'weapon-sword':'기본검', 'weapon-hammer':'기본망치',
 'palette-tray-3':'3칸 팔레트 받침', 'palette-tray-4':'4칸 팔레트 받침', 'palette-tray-5':'5칸 팔레트 받침',
 'slot-empty':'빈 잉크 칸', 'slot-locked':'잠긴 잉크 칸', 'state-unavailable':'사용 불가',
 'state-empty-weapon':'빈 무기 칸', 'ring-selected':'선택 무기 링', 'ring-idle':'대기 무기 링',
 'ring-cooldown':'쿨타임 링', 'ring-charge':'차징 진행 링', 'charge-pip-ready':'사용 가능 스킬 횟수',
 'charge-pip-empty':'회복 중 스킬 횟수', 'state-ready':'차징 완료',
}

def dump(path, data):
 path.parent.mkdir(parents=True,exist_ok=True)
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def font(size): return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',size)

def save(name,im,origin,source,label=None,**extra):
 p=GROUP/f'runtime/sprites/{name}.png';p.parent.mkdir(parents=True,exist_ok=True)
 im.save(p);SPRITES[name]=im
 RECORDS.append(dict(id=name,label=label or LABELS.get(name,name),file=p.relative_to(GROUP).as_posix(),
  size=list(im.size),origin=origin,source=source,sha256=sha(p),**extra))

def artwork():
 old=json.loads((OLD/'runtime/sprites.json').read_text(encoding='utf-8'))['sprites']
 entries=json.loads((GROUP/'source/generation-files.json').read_text(encoding='utf-8'))
 overrides={e['runtimeId'] for e in entries if e.get('runtimeId') and e.get('selected') is not False}
 skip={'minimap-frame','pigment-existing-game','health-fill-mask','palette-tray'}|overrides
 for r in old:
  if r['id'] in skip: continue
  src=OLD/r['source'];dst=GROUP/r['source'];dst.parent.mkdir(parents=True,exist_ok=True)
  shutil.copy2(src,dst);assert sha(src)==sha(dst)
  save(r['id'],Image.open(OLD/r['file']).convert('RGBA'),'retained-standalone-art' if r['origin']=='new-image-generation' else 'authored-ui-primitive',
   r['source'],label=r['label'],sourceSha256=sha(dst),upstream='ui/combat-hud-rebuilt-20261106/'+r['file'])
 for e in entries:
  dst=GROUP/f"source/generated/{e['id']}.png";dst.parent.mkdir(parents=True,exist_ok=True)
  src=dst if dst.exists() else Path(e['path'])
  if src.resolve()!=dst.resolve():shutil.copy2(src,dst)
  assert sha(src)==sha(dst)
  if e.get('selected') is False:continue
  im=Image.open(dst).convert('RGBA');a=np.asarray(im)[:,:,3];ys,xs=np.where(a>16)
  box=(max(0,int(xs.min())-8),max(0,int(ys.min())-8),min(im.width,int(xs.max())+9),min(im.height,int(ys.max())+9))
  save(e.get('runtimeId',e['id']),im.crop(box),'new-image-generation',dst.relative_to(GROUP).as_posix(),transparentTrim=list(box),sourceSha256=sha(dst))
 for n in ['approved-battle.png','approved-palette.png']:
  shutil.copy2(OLD/'source'/n,GROUP/'source'/n)

def primitive(name,size,svg,paint,label=None):
 p=GROUP/f'source/primitives/{name}.svg';p.parent.mkdir(parents=True,exist_ok=True)
 p.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size[0]} {size[1]}">{svg}</svg>',encoding='utf-8')
 im=Image.new('RGBA',(size[0]*4,size[1]*4));paint(ImageDraw.Draw(im),4)
 save(name,im.resize(size,Image.Resampling.LANCZOS),'authored-ui-primitive',p.relative_to(GROUP).as_posix(),label=label)

def primitives():
 primitive('health-fill-red',(680,48),f'<path d="M26 0H680L654 48H0Z" fill="{RED}"/>',
  lambda d,s:d.polygon([(26*s,0),(680*s,0),(654*s,48*s),(0,48*s)],fill=RED))
 for name,color,width in [('ring-selected',TEAL,14),('ring-idle','#9f9b90',7),('ring-cooldown',IVORY,12),('ring-charge','#f7c34c',12)]:
  primitive(name,(256,256),f'<circle cx="128" cy="128" r="115" fill="none" stroke="{color}" stroke-width="{width}"/>',
   lambda d,s,c=color,w=width:d.ellipse((13*s,13*s,243*s,243*s),outline=c,width=w*s))
 for n in (3,4,5):
  w=45+n*42
  primitive(f'palette-tray-{n}',(w*2,98),f'<rect x="3" y="3" width="{w*2-6}" height="92" rx="43" fill="{IVORY}" stroke="{INK}" stroke-width="5"/>',
   lambda d,s,w=w:d.rounded_rectangle((3*s,3*s,(w*2-3)*s,95*s),radius=43*s,fill=IVORY,outline=INK,width=5*s))
 for name,filled in [('charge-pip-ready',True),('charge-pip-empty',False)]:
  c=TEAL if filled else '#35362f'
  primitive(name,(24,24),f'<circle cx="12" cy="12" r="8" fill="{c}" stroke="{IVORY}" stroke-width="2"/>',
   lambda d,s,c=c:d.ellipse((4*s,4*s,20*s,20*s),fill=c,outline=IVORY,width=2*s))
 primitive('slot-empty',(80,100),f'<circle cx="40" cy="55" r="17" fill="none" stroke="#898578" stroke-width="3" stroke-dasharray="5 6"/>',
  lambda d,s:[d.arc((23*s,38*s,57*s,72*s),a,a+30,fill='#898578',width=3*s) for a in range(0,360,60)])
 primitive('slot-locked',(80,100),f'<path d="M26 48V36a14 14 0 0 1 28 0V48" fill="none" stroke="{INK}" stroke-width="5"/><rect x="20" y="45" width="40" height="35" rx="5" fill="{INK}"/>',
  lambda d,s:(d.arc((26*s,22*s,54*s,50*s),180,360,fill=INK,width=5*s),d.line((26*s,36*s,26*s,48*s),fill=INK,width=5*s),d.line((54*s,36*s,54*s,48*s),fill=INK,width=5*s),d.rounded_rectangle((20*s,45*s,60*s,80*s),radius=5*s,fill=INK)))
 primitive('state-unavailable',(100,100),'<path d="M25 50H75" stroke="#9f9b90" stroke-width="7" stroke-linecap="round"/>',
  lambda d,s:d.line((25*s,50*s,75*s,50*s),fill='#9f9b90',width=7*s))
 primitive('state-empty-weapon',(100,100),'<path d="M50 25V75M25 50H75" stroke="#9f9b90" stroke-width="5"/>',
  lambda d,s:(d.line((50*s,25*s,50*s,75*s),fill='#9f9b90',width=5*s),d.line((25*s,50*s,75*s,50*s),fill='#9f9b90',width=5*s)))
 primitive('state-ready',(96,96),f'<path d="M22 50L42 69L77 28" fill="none" stroke="{TEAL}" stroke-width="8"/>',
  lambda d,s:d.line([(22*s,50*s),(42*s,69*s),(77*s,28*s)],fill=TEAL,width=8*s,joint='curve'))

def fit(im,w,h):
 im=im.copy();im.thumbnail((w,h),Image.Resampling.LANCZOS);return im

def paste(im,name,rect,contain=True):
 x,y,w,h=map(round,rect);src=SPRITES[name]
 sp=fit(src,w,h) if contain else src.resize((w,h),Image.Resampling.LANCZOS)
 im.alpha_composite(sp,(x+(w-sp.width)//2,y+(h-sp.height)//2))

def composite(name,layers,label,kind):
 im=Image.new('RGBA',(256,256))
 for layer in layers: paste(im,layer['sprite'],layer['rect'])
 RECIPES.append(dict(id=name,size=[256,256],layers=layers))
 save(name,im,'modular-composite','runtime/composition-recipes.json',label=label,category=kind)

ACTION_MAP={
 'base_sword':['skill-slash','action-wave','action-dash','action-echo'],
 'whip':['action-bind','action-net','action-dash','action-echo'],
 'rapier':['action-thrust','action-wave','action-dash','action-echo'],
 'dagger':['action-stealth','action-mist','action-dash','action-echo'],
 'hammer':['skill-impact','action-shockwave','action-dash','action-echo'],
 'shield':['action-guard','action-net','action-dash','action-decoy'],
 'greatsword':['skill-impact','action-wave','skill-swirl','action-mark'],
 'shuriken':['action-thrust','skill-impact','action-teleport','skill-swirl'],
 'bomb':['action-pellets','skill-impact','action-missile','action-timer'],
}
EFFECT_GLYPH={
 'extra_hit':'skill-slash','focus_stack_damage':'action-mark','next_attack_power':'action-thrust',
 'burst_extra_hit':'skill-impact','kill_area_hit':'action-shockwave','kill_stack_attack_power':'action-echo',
 'mark_damage_bonus':'action-mark','execute_damage':'weapon-dagger','risky_attack_power':'skill-slash','underline_mark':'action-mark',
 'electric_field':'action-lightning','attack_and_move_speed':'action-dash','dash_hit':'action-thrust','moving_attack_power':'action-dash',
 'hit_recovery_cancel':'action-echo','chain_lightning':'action-lightning','mark_chain_spread':'action-net','move_charge':'action-ball',
 'dash_followup':'action-echo','switch_slide':'action-teleport','shielded_attack_power':'weapon-shield','shield':'action-guard',
 'homing_counter':'action-thrust','heal':'action-heal','high_health_attack_power':'action-heal','hit_guard_shield':'action-guard','stored_retaliation':'skill-impact',
 'area_radius':'action-shockwave','cooldown_reduce':'action-timer','attack_reach':'action-thrust','mark_spread':'action-net',
 'extra_skill_charge':'action-echo','next_attack_geometry':'action-wave','bind':'action-bind','area_hit':'skill-impact',
 'stun':'action-lightning','stunned_dot':'action-timer','poison':'action-poison','cc_count_hit':'action-net',
 'poison_burst':'action-poison','controlled_damage':'action-thrust',
}
TRIGGER_GLYPH={'passive':'action-ball','hit':'skill-impact','kill':'weapon-dagger','switch':'action-teleport',
 'mark_burst':'action-shockwave','mark_pop':'action-mark','move':'action-dash','dash':'action-thrust','damaged':'action-guard','bind_end':'action-bind'}

def catalogs():
 data={};source_records=[]
 presentation_source=MAIN/'unity/Assets/Deviant/Combat/Resources/CharacterV2/ApprovedWeaponCatalog.json'
 presentation_dest=GROUP/'source/reference-data/ApprovedWeaponCatalog.json'
 presentation_dest.parent.mkdir(parents=True,exist_ok=True)
 if presentation_source.exists():shutil.copy2(presentation_source,presentation_dest)
 source_records.append(dict(file=presentation_dest.relative_to(GROUP).as_posix(),upstream='unity/Assets/Deviant/Combat/Resources/CharacterV2/ApprovedWeaponCatalog.json',sha256=sha(presentation_dest)))
 ui_contract=GROUP/'source/launcher-skill-ui.json'
 source_records.append(dict(file=ui_contract.relative_to(GROUP).as_posix(),authority='user-directed UI contract; gameplay binding pending',sha256=sha(ui_contract)))
 for n in ['melee_weapon_design','ranged_basic_design','ink_color_parts_proposal','ink_color_parts_tuning','launcher_attack_conversions','weapon_growth_mvp_proposal']:
  dst=GROUP/f'source/reference-data/{n}.json';dst.parent.mkdir(parents=True,exist_ok=True)
  src=MAIN/f'data/combat/{n}.json'
  if src.exists():shutil.copy2(src,dst)
  data[n]=json.loads(dst.read_text(encoding='utf-8-sig'))
  source_records.append(dict(file=dst.relative_to(GROUP).as_posix(),upstream=f'data/combat/{n}.json',sha256=sha(dst)))
 weapons=[];skills=[]
 for f in data['melee_weapon_design']['forms']:
  icon='weapon-'+('sword' if f['id']=='base_sword' else f['id'])
  basic={'base_sword':'skill-slash','whip':'action-bind','rapier':'action-thrust','dagger':'action-thrust','hammer':'skill-impact','shield':'action-guard','greatsword':'skill-slash'}[f['id']]
  weapon=dict(id=f['id'],name=f['name'],family='sword' if f['id'] in ['base_sword','whip','rapier','dagger'] else 'blunt',
   gameplayBinding=dict(formId=f['id']),icon=icon,basicIcon=basic,specials=[],modes=[],specialPolicy='cooldown-and-charges')
  for i,s in enumerate(f['specials']):
   sid='special-'+s['id'];a=ACTION_MAP[f['id']][i]
   composite(sid,[dict(sprite=a,rect=[57,6,189,189]),dict(sprite=icon,rect=[5,155,96,96])],s['name'],'skill')
   rec=dict(id=s['id'],weaponId=f['id'],name=s['name'],icon=sid,slot=s['slot'],cooldownSeconds=s['cooldown'],
    idOrigin='game-design-id',source=f"melee_weapon_design.json#/forms/{f['id']}/specials/{s['id']}")
   skills.append(rec);weapon['specials'].append(rec['id'])
  weapons.append(weapon)
 ranged_names={'launcher':'발사기','shuriken':'수리검','bomb':'폭탄'}
 titles={'shuriken':['관통 수리검','폭발 수리검','뒤로 이동 · 복귀','전진 회전 수리검'], 'bomb':['3연속 투척','대형 폭탄','미사일 변신','착지 시한폭탄']}
 keys=['basic_special_design','range_special_design','movement_special_design','followup_special_design']
 slots=['default','range','movement','follow_up']
 for wid in data['ranged_basic_design']['designed_form_candidates_approved']:
  weapon=dict(id=wid,name=ranged_names[wid],family='ranged',icon='weapon-'+wid,basicIcon='weapon-'+wid,specials=[],modes=[],specialPolicy='cooldown-and-charges')
  if wid=='launcher':
   for mode,glyph,title in [('ball','action-ball','차징 후 발사'),('laser','action-beam-clean' if 'action-beam-clean' in SPRITES else 'action-beam','레이저'),('shotgun','action-pellets','샷건'),('grenade','action-grenade','유탄포')]:
    cid='launcher-'+mode
    weapon_glyph='weapon-launcher' if mode=='ball' else 'weapon-launcher-'+mode
    composite(cid,[dict(sprite=glyph,rect=[57,6,189,189]),dict(sprite=weapon_glyph,rect=[5,155,96,96])],title,'skill')
    sid='launcher_default' if mode=='ball' else 'launcher_'+mode
    skills.append(dict(id=sid,weaponId='launcher',name=title,icon=cid,slot='default' if mode=='ball' else mode,
     presentationIcon=weapon_glyph,presentationForm='launcher' if mode=='ball' else 'launcher_'+mode,
     presentationName='기본 발사기' if mode=='ball' else title,
     idOrigin='ui-alias-pending-gameplay-skill-binding',source='source/launcher-skill-ui.json',
     cooldownSeconds=None,inputPresentation='charge-and-release' if mode=='ball' else 'game-owned'))
    weapon['specials'].append(sid)
   weapon['basicIcon']='action-ball'
  else:
   for i,k in enumerate(keys):
    spec=data['ranged_basic_design'][wid][k];uiid=f'{wid}_{slots[i]}'
    icon='special-'+uiid
    composite(icon,[dict(sprite=ACTION_MAP[wid][i],rect=[57,6,189,189]),dict(sprite=weapon['icon'],rect=[5,155,96,96])],titles[wid][i],'skill')
    rec=dict(id=uiid,weaponId=wid,name=titles[wid][i],icon=icon,slot=slots[i],cooldownSeconds=spec['cooldown'],
     idOrigin='ui-alias-for-design-field',source=f'ranged_basic_design.json#/{wid}/{k}')
    skills.append(rec);weapon['specials'].append(uiid)
  weapons.append(weapon)
 # Launcher variants are equipped skills. Their art remains a presentation form.
 launcher=next(w for w in weapons if w['id']=='launcher')
 for w in weapons:
  w.setdefault('gameplayBinding',dict(formId=w['id']))
 launcher['gameplayBinding']=dict(formId='launcher',skillBinding='pending-other-session')
 launcher['name']='기본 발사기'
 launcher['basicIcon']='action-ball'
 launcher_presentations=[dict(skillId=s['id'],name=s['name'],icon=s['presentationIcon'],form=s['presentationForm']) for s in skills if s['weaponId']=='launcher']
 legacy_selections={f'launcher_{m}':dict(weaponId='launcher',skillId=f'launcher_{m}') for m in ['laser','shotgun','grenade']}
 for w in weapons:
  w['identityOrigin']='base-blueprint' if w['id'] in ['base_sword','hammer','launcher'] else 'shape-stamp-result'
 order=['base_sword','rapier','whip','dagger','hammer','greatsword','shield','launcher','launcher_laser','launcher_shotgun','launcher_grenade','shuriken','bomb']
 weapons.sort(key=lambda w:order.index(w['id']))
 assert {w['id'] for w in weapons}|{s['form'] for s in launcher_presentations}=={w['form'] for w in json.loads(presentation_dest.read_text(encoding='utf-8-sig'))['sets']}
 grades={p['id']:p['fixedGrade'] for p in data['ink_color_parts_tuning']['partTuning']}
 inks=[]
 for p in data['ink_color_parts_proposal']['parts']:
  name='part-'+p['id'];glyph=EFFECT_GLYPH[p['kind']];trigger=TRIGGER_GLYPH[p['trigger']]
  effect=[dict(sprite=glyph,rect=[61,102,100,100])]
  if p['kind']=='focus_stack_damage':effect=[dict(sprite=glyph,rect=[55+i*35,104+i*15,65,65]) for i in range(3)]
  if p['kind']=='underline_mark':effect.append(dict(sprite='divider',rect=[64,202,95,5]))
  if p['kind']=='poison_burst':effect=[dict(sprite='skill-impact',rect=[48,88,128,128]),dict(sprite=glyph,rect=[75,112,78,78])]
  composite(name,[dict(sprite='drop-'+p['color'],rect=[20,3,184,244])]+effect+
   [dict(sprite='weapon-disc',rect=[176,180,76,76]),dict(sprite=trigger,rect=[189,193,50,50])],p['name'],'ink-part')
  inks.append(dict(id=p['id'],name=p['name'],color=p['color'],kind=p['kind'],trigger=p['trigger'],
   fixedGrade=grades[p['id']],colorIcon='drop-'+p['color'],partIcon=name,effectGlyph=glyph,triggerGlyph=trigger,
   source='ink_color_parts_proposal.json#/parts/'+p['id']))
 cat=dict(schema='deviant-expandable-combat-ui-v1',sourceData=source_records,weapons=weapons,skills=skills,inks=inks,
  launcherSkillPresentations=launcher_presentations,legacyWeaponSelections=legacy_selections,
  weaponIdentityPolicy='10 selectable weapons. Launcher has four equipped skills; its large icon and run card follow the equipped skill presentation.',
  familyCounts=dict(sword=4,blunt=3,ranged=3),
  inkColors=data['ink_color_parts_proposal']['colorRoles'],triggers=data['ink_color_parts_proposal']['triggers'],
  palette=dict(initialSlots=3,supportedSlots=[3,4,5],colorlessIsInkColor=False,colorlessIcon='drop-colorless',
   emptyIcon='slot-empty',lockedIcon='slot-locked',revealUses='colorIcon-only',revealSeconds=3.5,
   slotData=['color','partId|null','locked','universal'],firstUnlockDefault='first-slot-universal',rulesOwnedByGame=True),
  unknownContent=dict(weaponIcon='state-empty-weapon',skillIcon='state-unavailable',neverSubstituteSword=True),
  stateContract=dict(weapon=['active','idle','empty'],skill=['ready','cooldown','charging','charged','unavailable'],
   fields=['weaponId','skillId','launcherMode','selected','remainingSeconds','cooldownFraction','chargeProgress','availableCharges','chargeCapacity','paletteSlots'],
   cooldownFraction='remaining / duration; radial clockwise from 12 oclock',chargeProgress='held / required; independent from cooldown',
   launcher='one weapon; default charged shot or laser/shotgun/grenade skill changes the large icon and run-card badge; basic attack remains the launcher basic attack; gameplay timing and IDs require the gameplay adapter',charges='available / capacity; recovery independent per weapon',
   experimentalChargeStamp='render charging separately; gameplay adapter decides whether the stamp exists'))
 dump(GROUP/'runtime/catalog.json',cat);dump(GROUP/'runtime/composition-recipes.json',RECIPES)
 return cat

def checker(size):
 yy,xx=np.indices((size[1],size[0]));v=np.where((xx//16+yy//16)%2,83,97).astype(np.uint8)
 return Image.fromarray(np.repeat(v[:,:,None],3,axis=2)).convert('RGBA')

def contact(name,ids,title,subtitle,cols=5,cw=270,ch=230):
 p=GROUP/'preview';p.mkdir(exist_ok=True);rows=math.ceil(len(ids)/cols)
 out=Image.new('RGB',(cw*cols,100+ch*rows),'#efe9de');d=ImageDraw.Draw(out)
 d.text((22,14),title,font=font(28),fill=INK);d.text((24,57),subtitle,font=font(17),fill='#62655b')
 labels={r['id']:r['label'] for r in RECORDS}
 for i,sid in enumerate(ids):
  x=i%cols*cw;y=100+i//cols*ch;panel=checker((cw-14,ch-62));im=fit(SPRITES[sid],cw-32,ch-82)
  panel.alpha_composite(im,((panel.width-im.width)//2,(panel.height-im.height)//2));out.paste(panel.convert('RGB'),(x+7,y))
  d.text((x+10,y+ch-58),labels[sid],font=font(17),fill=INK)
  d.text((x+10,y+ch-29),sid,font=font(11),fill='#596359')
 out.save(p/(name+'.png'))

def render_scene(scene,background='#f8eddc'):
 out=Image.new('RGBA',(W,H),background or (0,0,0,0))
 for item in scene['layers']:
  kind=item['type']
  if kind=='sprite':
   x,y,w,h=map(round,item['rect']);src=SPRITES[item['sprite']]
   sp=fit(src,w,h) if item.get('contain',True) else src.resize((w,h),Image.Resampling.LANCZOS)
   if item.get('flip'):sp=sp.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
   layer=Image.new('RGBA',(w,h));layer.alpha_composite(sp,((w-sp.width)//2,(h-sp.height)//2))
   if 'fill' in item:
    a=np.array(layer);a[:,round(w*item['fill']):,3]=0;layer=Image.fromarray(a)
   if 'radial' in item:
    a=np.array(layer);yy,xx=np.indices((h,w));angle=(np.arctan2(xx-w/2,-(yy-h/2))+2*np.pi)%(2*np.pi)
    a[angle>item['radial']*2*np.pi,3]=0;layer=Image.fromarray(a)
   out.alpha_composite(layer,(x,y))
  elif kind=='text':
   d=ImageDraw.Draw(out);x=item['x'];f=font(item['size'])
   if item.get('center'):x-=d.textlength(item['text'],font=f)/2
   d.text((x,item['y']),item['text'],font=f,fill=item['color'])
  elif kind=='path':ImageDraw.Draw(out).line([tuple(p) for p in item['points']],fill=item['color'],width=item['width'],joint='curve')
  else:
   layer=Image.new('RGBA',(W,H));d=ImageDraw.Draw(layer);x,y,w,h=item['rect'];box=(x,y,x+w,y+h)
   if kind=='ellipse':d.ellipse(box,fill=item['color'])
   else:d.rectangle(box,fill=item['color'])
   out.alpha_composite(layer)
 return out

def scenes(cat):
 script=GROUP/'source/build-scenes.cjs'
 script.write_text('''const fs=require('fs'),path=require('path');
const group=path.resolve(__dirname,'..');
const cat=JSON.parse(fs.readFileSync(path.join(group,'runtime/catalog.json'),'utf8'));
const {buildScene,paletteSlots}=require('../runtime/ui-model.js');
const scenarios=[
 {id:'melee-three-slots',title:'기본검 + 기본망치 / 3칸',hp:82,reveal:true,weapons:[
  {weaponId:'base_sword',skillId:'base_sword_range',paletteSlots:[{color:'blue'},{color:'yellow'},{color:'blue'}]},
  {weaponId:'hammer',skillId:'hammer_default',status:'cooldown',cooldownFraction:.64,remainingSeconds:3.2,paletteSlots:[{color:'red'},{color:'green'},{color:'yellow'}]}]},
 {id:'ranged-four-slots',title:'레이저 + 수리검 / 4칸',hp:64,reveal:true,selected:1,weapons:[
  {weaponId:'launcher',skillId:'launcher_laser',paletteSlots:paletteSlots(4,2,true)},
  {weaponId:'shuriken',skillId:'shuriken_movement',status:'cooldown',cooldownFraction:.32,remainingSeconds:1.6,chargeCapacity:3,availableCharges:1,paletteSlots:paletteSlots(4,3,true)}]},
 {id:'mixed-five-slots',title:'폭탄 + 방패 / 5칸',hp:38,reveal:true,weapons:[
  {weaponId:'bomb',skillId:'bomb_follow_up',chargeCapacity:3,availableCharges:3,paletteSlots:paletteSlots(5,0,true)},
  {weaponId:'shield',skillId:'shield_range',status:'charging',chargeProgress:.72,paletteSlots:paletteSlots(5,2,true)}]},
 {id:'empty-and-charged',title:'빈칸 + 차징 완료',hp:100,reveal:false,selected:1,weapons:[
  {weaponId:'',paletteSlots:[{color:null},{color:null},{color:null,locked:true}]},
  {weaponId:'greatsword',skillId:'greatsword_default',status:'charged',chargeCapacity:2,availableCharges:2,paletteSlots:[{color:'colorless'},{color:'purple'},{color:'blue'},{color:'green'},{color:null,locked:true}]}]},
 {id:'boss-state',title:'보스 전투 / 상단 간소화',hp:25,boss:true,reveal:false},
 {id:'ranged-stamp-weapons',title:'발사기 / 샷건 스킬 + 유탄포 스킬',hp:82,reveal:true,weapons:[
  {weaponId:'launcher',skillId:'launcher_shotgun',paletteSlots:paletteSlots(5,0,true)},
  {weaponId:'launcher',skillId:'launcher_grenade',paletteSlots:paletteSlots(5,2,true)}]},
 {id:'launcher-charge',title:'발사기 / 기본 차징 스킬 + 레이저',hp:88,reveal:true,weapons:[
  {weaponId:'launcher',skillId:'launcher_default',status:'charging',chargeProgress:.72,paletteSlots:paletteSlots(5)},
  {weaponId:'launcher',skillId:'launcher_laser',status:'ready',paletteSlots:paletteSlots(5,2)}]},
];
for(const s of scenarios){fs.writeFileSync(path.join(group,'preview',s.id+'.scene.json'),JSON.stringify(buildScene(cat,s),null,2)+'\\n');}
fs.writeFileSync(path.join(group,'runtime','preview-scenarios.json'),JSON.stringify(scenarios,null,2)+'\\n');
console.log(scenarios.length+' scenarios built');
''',encoding='utf-8')
 subprocess.run(['node',str(script)],check=True,capture_output=True,text=True)
 scenario_data=json.loads((GROUP/'runtime/preview-scenarios.json').read_text(encoding='utf-8'))
 for s in scenario_data:
  scene=json.loads((GROUP/f"preview/{s['id']}.scene.json").read_text(encoding='utf-8'))
  render_scene(scene).save(GROUP/f"preview/{s['id']}.png")
  if s['id']=='ranged-four-slots':
   transparent=render_scene(scene,None);transparent.save(GROUP/'preview/assembled-transparent.png')
   check=checker((W,H));check.alpha_composite(transparent);check.save(GROUP/'preview/assembled-checker.png')
 rows=scenario_data[:4];out=Image.new('RGB',(1670,140+len(rows)*230),'#efe9de');d=ImageDraw.Draw(out)
 d.text((26,20),'DEVIANT / 팔레트 크기와 전투 상태',font=font(34),fill=INK)
 d.text((28,76),'3칸 → 4칸 → 5칸 · 무색 · 빈칸 · 잠금 · 스킬 없음 · 쿨타임 · 횟수 · 차징',font=font(23),fill='#64685c')
 for i,s in enumerate(rows):
  scene=json.loads((GROUP/f"preview/{s['id']}.scene.json").read_text(encoding='utf-8'))
  # Preview-only crop of our assembled scene; never reused as runtime art.
  im=render_scene(scene).crop((28,750,810,940)).resize((1173,285),Image.Resampling.LANCZOS)
  im.thumbnail((1050,205));out.paste(im.convert('RGB'),(550,140+i*230))
  d.text((30,175+i*230),s['title'],font=font(28),fill=INK)
  d.text((31,218+i*230),['선택 무기 · 스킬 준비 / 쿨타임','발사기 레이저 스킬 / 1회 사용 가능, 다음 회복 중','사용 가능 3회 / 차징 72%','무기 비어 있음 / 차징 완료 / 잠긴 칸'][i],font=font(18),fill='#62665b')
 out.save(GROUP/'preview/state-matrix.png')
 data=json.dumps(dict(catalog=cat,sprites=RECORDS),ensure_ascii=False).replace('</','<\\/')
 (GROUP/'preview/preview-data.js').write_text('window.PREVIEW_DATA = '+data+';\n',encoding='utf-8')
 layout=dict(referenceResolution=[W,H],coordinateOrigin='top-left',gameIntegration=True,
  anchors={'health':'top-left','weapons':'bottom-left','currencies-controls-map':'top-right','run-palette':'center'},
  minimap=dict(rect=[1251,98,394,394*201/313],aspectRatio='313:201',frame='minimap-frame-compact',contentInset=[22,22,22,22],below='currencies-and-buttons'),
  reveal=dict(cardRect=[0,0,313,201],durationSeconds=3.5,content='weapon identity + color slots only'),
  palette=dict(sizes={str(n):[45+n*42,43] for n in [3,4,5]},spriteBorderPixels=[49,45,49,45],sourceHeight=98,
   nineSlice='Use palette-tray-3 with left/bottom/right/top sprite borders. Render at scale 0.5; expand only center. Exact 4/5-slot sprites also supplied.'),
  bars=dict(health=dict(sprite='health-fill-red',imageType='Filled',fillMethod='Horizontal',fillOrigin='Left',tint='#ffffff'),
   cooldown=dict(sprite='ring-cooldown',imageType='Filled',fillMethod='Radial360',fillOrigin='Top',clockwise=True),
   charge=dict(sprite='ring-charge',fillMethod='Radial360',fillAmount='chargeProgress; never cooldownFraction')),
  unity=dict(textureType='Sprite (2D and UI)',spriteMode='Single',meshType='FullRect',alphaIsTransparency=True,mipmaps=False,
   filterMode='Bilinear',compression='None',pixelsPerUnit=100,canvasScaler='ScaleWithScreenSize',matchWidthOrHeight=.5),
  dynamicText=['health value','currency totals','weapon names','key bindings','cooldown seconds','available/capacity'],
  editorLayout='layout-edited.json',sceneBuilder='ui-model.js',catalog='catalog.json',rendererInputs='stateContract in catalog.json')
 dump(GROUP/'runtime/layout.json',layout)

def gun_scale_preview(cat):
 ids=['weapon-launcher','weapon-launcher-laser','weapon-launcher-shotgun','weapon-launcher-grenade']
 titles=['기본 발사기','레이저','샷건','유탄발사기']
 cues=['잉크통 + 물방울 총구','긴 총열 + 작은 집속부','넓은 몸통 + 쌍총열','큰 총구 + 둥근 탄창']
 sheet=Image.new('RGBA',(1440,650),'#f2ecdf');d=ImageDraw.Draw(sheet)
 d.text((26,15),'DEVIANT / 총 4종 · 작은 HUD용 정리',font=font(30),fill=INK)
 d.text((28,62),'안쪽 장식은 줄이고 무기마다 다른 실루엣을 유지했습니다.',font=font(19),fill='#60675d')
 for i,sid in enumerate(ids):
  x=i*360
  d.text((x+22,112),titles[i],font=font(25),fill=INK)
  d.text((x+22,151),cues[i],font=font(16),fill='#60675d')
  old=Image.open(GROUP/f'source/generated/{sid}.png').convert('RGBA');a=np.asarray(old)[:,:,3];ys,xs=np.where(a>16)
  box=(max(0,int(xs.min())-8),max(0,int(ys.min())-8),min(old.width,int(xs.max())+9),min(old.height,int(ys.max())+9))
  for off,im in [(22,old.crop(box)),(190,SPRITES[sid])]:
   pane=checker((144,144));im=fit(im,124,124);pane.alpha_composite(im,((144-im.width)//2,(144-im.height)//2));sheet.alpha_composite(pane,(x+off,190))
  d.text((x+63,341),'기존',font=font(17),fill='#60675d');d.text((x+233,341),'수정',font=font(17),fill=INK)
  d.text((x+22,389),'48px',font=font(15),fill='#60675d')
  paste(sheet,'weapon-disc',[x+122,380,66,66]);paste(sheet,sid,[x+131,389,48,48])
  d.text((x+22,473),'왼쪽 아래 HUD 크기',font=font(17),fill=INK)
  pane=Image.new('RGBA',(340,145),'#f8eddc')
  paste(pane,'weapon-disc',[2,0,142,142]);paste(pane,sid,[31,28,84,84]);paste(pane,'ring-selected',[9,7,128,128])
  basic=next(w['basicIcon'] for w in cat['weapons'] if w['id']=='launcher')
  special=next(s['icon'] for s in cat['skills'] if s.get('presentationIcon')==sid)
  for off,icon,key in [(134,basic,'Q'),(216,special,'E')]:
   paste(pane,'weapon-disc',[off,34,82,82]);paste(pane,icon,[off+16,50,50,50])
   paste(pane,'button-frame',[off+23,12,40,33],False)
   ImageDraw.Draw(pane).text((off+34,12),key,font=font(22),fill=INK)
  sheet.alpha_composite(pane,(x+10,501))
 sheet.convert('RGB').save(GROUP/'preview/guns-small-scale.png')

def documents(cat):
 counts={}
 for r in RECORDS:counts[r['origin']]=counts.get(r['origin'],0)+1
 md=['# 전투 HUD · 출발 팔레트 에셋 목록','',
  '선택 무기는 검 4종·둔기 3종·원거리 3종으로 총 10종입니다. 발사기는 차징 후 발사·레이저·샷건·유탄포 스킬을 장착하며, 스킬에 맞춰 큰 무기 원과 출발 카드의 외형을 바꿉니다. 외형 13종, 특수공격 40종, 잉크 54종을 포함합니다. Unity 연결·실행 검증과 새 원화의 미술 승인은 별도입니다.','',
  '| 묶음 | 제공 내용 | 상태 |','|---|---|---|',
  '| 체력 | 프레임, 초상, 실제 빨간 HP 채움 | 완료 |',
  '| 오른쪽 위 | 골드, 새 안료 아이콘, 가방, 일시정지, 공용 버튼 | 완료 |',
  '| 지도 | 얇은 테두리, 표시 비율 313:201, 상단 버튼 아래 배치 | 완료 |',
  '| 무기 | 검 4 + 둔기 3 + 원거리 3, 발사기 스킬 외형 3개 추가 | 10무기 · 13외형 |',
  '| 특수공격 | 동작 원화 + 무기 식별 레이어 조합 | 발사기 포함 40종 |',
  '| 발사기 | 차징 후 발사·레이저·샷건·유탄포를 장착 스킬로 표시, 큰 무기 외형도 연동 | 4스킬 · 게임 바인딩 별도 |',
  '| 팔레트 | 빨강·노랑·초록·파랑·보라, 별도 무색 범용 칸 | 5색 + 무색 완료 |',
  '| 팔레트 크기 | 3·4·5칸 받침, 9-slice 규격, 출발 카드 내 자동 간격 | 완료 |',
  '| 잉크 파츠 | 실제 ID·이름·색·효과·트리거·등급 연결 및 조합 PNG | 54종 완료 |',
  '| 상태 | 선택/대기, 빈 무기, 빈/잠긴 칸, 쿨타임, 사용 횟수, 차징/완료, 스킬 없음 | 완료 |',
  '| 런 시작 | 카드 2장, 무기 교체, 3~5색, 3.5초 표시 | 조립 미리보기 완료 |',
  '| Unity 적용 | 프리팹·실제 게임 이벤트 바인딩·실행 검증 | 미적용 |','',
  f'총 runtime PNG **{len(RECORDS)}개**. 다음 제작 구분을 섞어 세지 않습니다.','',
  '| 제작 구분 | 파일 수 |','|---|---|']
 origin_names={'retained-standalone-art':'이전 단계의 새 독립 원화 유지','new-image-generation':'이번 단계 새 독립 원화','authored-ui-primitive':'직접 작성한 UI 도형','modular-composite':'독립 원화의 레이어 조합'}
 md += [f"| {origin_names[k]} | {v} |" for k,v in counts.items()]
 md += ['', '54종 잉크와 40종 특수공격은 공용 원화를 조합한 결과입니다. 공용 원화와 색·효과·무기·발동 조건을 조합한 에셋입니다. `runtime/composition-recipes.json`에서 레이어를 수정할 수 있습니다. 출발 팔레트에는 파츠 문양 대신 순수 색 구성만 표시합니다. 무색은 잉크 종류가 아닌 범용 슬롯입니다.','',
  '## 무기와 특수공격','', '| ID | 이름 | 유형 | 특수공격 |','|---|---|---|']
 skill_names={s['id']:s['name'] for s in cat['skills']}
 for w in cat['weapons']:md.append(f"| `{w['id']}` | {w['name']} | {w['family']} | "+(' · '.join(skill_names[s] for s in w['specials']) if w['specials'] else '독립 스킬 없음 / 해당 무기의 기본 공격 표시')+' |')
 md += ['', '원거리 특수공격의 ID는 원본 JSON의 필드에 연결하는 UI 별칭입니다. 게임 내부 enum/저장 ID를 새로 확정하지 않습니다. 신규 무기는 catalog 항목과 아이콘을 추가하면 같은 슬롯에 표시되며, 미등록 ID는 빈 슬롯 표시를 사용합니다. 미확정 원거리 4번째 형태는 임의로 만들지 않았습니다.','',
  '## 잉크 54종','', '| ID | 이름 | 색 | 효과 종류 | 발동 조건 | 내부 등급 |','|---|---|---|---|---|---|']
 for p in cat['inks']:md.append(f"| `{p['id']}` | {p['name']} | {p['color']} | {p['kind']} | {p['trigger']} | {p['fixedGrade']} |")
 md += ['', '## 독립 PNG 파일','', '| ID | 용도 | 제작 |','|---|---|---|']
 for r in RECORDS:md.append(f"| [{r['id']}]({r['file']}) | {r['label']} | {origin_names[r['origin']]} |")
 (GROUP/'ASSET-LIST.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
 (GROUP/'README.md').write_text('''# 확장 전투 HUD · 출발 팔레트

현행 데이터에 맞춘 재사용 부품 세트입니다. 채택 시안은 화풍 참고에만 사용했고, 화면에서 잘라낸 픽셀은 에셋으로 쓰지 않았습니다. 기본 image_gen 도구로 새 원화를 만들고 원본을 source/generated에 보존했습니다. runtime/sprites는 투명 여백만 정리한 출력 또는 공개된 조합 레시피의 결과입니다.

- [전체 목록](ASSET-LIST.md): 제작 구분, 10무기·13외형, 발사기 포함 40특수공격, 54잉크, 전체 PNG.
- [배치 편집기](preview/index.html): 에셋·묶음 이동과 크기 조절, 텍스트·표시 순서, 되돌리기, 자동 저장, JSON 불러오기·저장, PNG 출력. [사용 안내](EDITOR.md).
- [사용자 배치 열기](preview/index.html?layout=matched-weapons): 사용자 수정값과 무기 2 맞춤을 적용합니다. [배치 JSON](runtime/layout-edited.json), [화면](preview/layout-edited.png), [발사기 스킬 예시](preview/layout-launcher-skills.png).
- `preview/편집기_열기.cmd`로 열면 로컬 브라우저에서 PNG 저장도 사용할 수 있습니다. 무기·스킬·잉크, HP, 쿨타임, 차징, 횟수, 3~5칸, 보스 상태도 계속 바꿀 수 있습니다.
- [수정 부품](preview/corrected-parts.png), [상태 비교](preview/state-matrix.png).
- [총 4종 축소 비교](preview/guns-small-scale.png): 내부 장식을 줄인 아이콘의 전후와 48px·HUD 표시 크기.
- [연결 데이터](runtime/catalog.json), [배치·Unity 설정](runtime/layout.json), [조합 레시피](runtime/composition-recipes.json).
- source/prompts-*.json: 기본 이미지 생성 도구에 전달한 요청문.
- source/reference-data: 게임 설계 JSON의 바이트를 보존한 참조 사본. 게임 규칙의 소유권은 메인에 있습니다.

## 변경한 내용

미니맵은 출발 카드와 같은 313:201 비율이며 재화·버튼 아래에 배치합니다. health-fill-red.png 자체가 빨간색이므로 흰색 tint로 사용합니다. 기존 32px 안료는 사용하지 않고 5색 팔레트 형태를 같은 화풍으로 새로 제작했습니다.

총 아이콘은 잉크통·긴 총열·쌍총열·큰 원통으로 구분하고, 홈·나사·세부 방아쇠 같은 내부 장식은 줄였습니다. 작은 HUD에서도 구분되도록 만든 새 그림은 source/generated/*-simple.png에 보존하며, 기존 그림도 같은 폴더에 유지합니다. runtime의 기존 무기 ID를 통해 새 그림을 사용합니다. 실제 요청문은 source/prompts-guns-simplified.json에 있습니다.

기본검·채찍·레이피어·한손 단검·기본망치·방패·대검·발사기·수리검·폭탄이 선택 가능한 무기 10종입니다. 발사기의 기본 스킬은 차징 후 발사이며, 레이저·샷건·유탄포는 발사기에 장착하는 스킬입니다. 장착한 스킬에 따라 큰 무기 아이콘과 출발 카드의 외형·이름도 바뀌고, 기본 공격 칸은 발사기 기본 공격을 유지합니다. 발사기 스킬 ID는 UI 별칭이며 별도 세션의 게임 구현과 연결해야 합니다. 기존 launcher_laser/shotgun/grenade 무기 선택 JSON은 발사기 + 해당 스킬로 호환 변환합니다. 스킬 준비 상태, 남은 쿨타임, 현재/최대 사용 횟수, 차징 진행률은 서로 다른 입력입니다. 선택하지 않은 무기도 독립적으로 회복 상태를 표시합니다.

팔레트는 3/4/5칸의 정확한 PNG와 9-slice 경계를 함께 제공합니다. 색상·잉크 ID·빈칸·잠금·무색 상태는 슬롯 데이터로 받습니다. 무색은 여섯 번째 잉크가 아닙니다. 출발 카드에는 색상 구성만 표시하며 상세 잉크 아이콘은 필요할 때 따로 씁니다. 기본 제공 출발 연출은 3.5초입니다. 조작 설명 줄은 만들지 않았습니다.

## 연결 방법

1. runtime/sprites를 Sprite/FullRect/알파/비압축/Bilinear로 가져옵니다. 원본 source는 편집 보존용입니다.
2. 공용 슬롯에 catalog의 weaponId/skillId/partId로 아이콘을 교체합니다. 원거리 스킬 ID는 원본 설계 필드에 연결하는 UI 별칭임을 확인합니다.
3. HP는 빨간 PNG의 Horizontal fill, 쿨타임/차징은 별도 링의 Radial360 fill을 갱신합니다. 숫자와 키캡 문자는 TMP 등 런타임 텍스트로 렌더링합니다.
4. layout.json의 anchor와 1670x942 기준에 layout-edited.json의 사용자 변경값을 적용합니다. 편집기의 editor-core.js가 변경값을 해석하는 참조입니다. 지도 내부는 실제 맵 데이터로 그리며 프레임 안쪽 여백으로 마스킹합니다.
5. 새 무기·잉크 추가 시 catalog와 그림/조합만 추가합니다. 첫 슬롯 무색 해금 등 게임 규칙은 게임에서 계산해 넘깁니다. UI가 임의 추첨하거나 게임 수치를 변경하지 않습니다.

프리뷰의 기본 숫자와 지도 경로는 검토용 예시입니다. 사용자 편집 배치는 메인의 TemporaryGameGui 전투 HUD·출발 팔레트에 로컬 적용되어 실제 상태를 표시합니다. 메인 문서 docs/development/combat-hud-approved.md에서 연결·실행 방법을 확인합니다. 로컬 게임 검토와 팀 배포는 별개이며, 발사기의 실제 공격 메커니즘 변경은 메인 게임 코드가 소유합니다.

## 다시 내보내기

리소스 저장소에서 `python -X utf8 tools/build_expanded_combat_ui.py`를 실행합니다. Pillow, NumPy, Node와 Windows 맑은 고딕이 필요합니다. 새로운 생성 API를 호출하지 않고 보존된 원본으로 출력합니다. 프롬프트와 미채택 beam 원본도 source에 남습니다. 검증은 `python -X utf8 tools/verify_expanded_combat_ui.py`입니다.
''',encoding='utf-8')

def register():
 files=sorted(p.relative_to(GROUP).as_posix() for p in GROUP.rglob('*') if p.is_file() and p.name!='asset.json')
 dump(GROUP/'asset.json',dict(format='deviant-resource-asset-v1',id='ui.combat-hud-expanded-20261106',path=GROUP.relative_to(ROOT).as_posix(),
  status='local-game-preview',approval='User-edited combat HUD and departure palette accepted for an in-game trial',
  gameIntegration='combat-hud-and-departure-local-preview',sources=['source/generation-files.json','runtime/catalog.json'],files=files))
 cp=ROOT/'catalog.json';cat=json.loads(cp.read_text(encoding='utf-8-sig'));entry=dict(id='ui.combat-hud-expanded-20261106',path=GROUP.relative_to(ROOT).as_posix(),fileCount=len(files))
 for i,a in enumerate(cat['assets']):
  if a['id']==entry['id']:cat['assets'][i]=entry;break
 else:cat['assets'].append(entry)
 dump(cp,cat)
 mp=ROOT/'generated-assets.json';man=json.loads(mp.read_text(encoding='utf-8-sig'));prefix=GROUP.relative_to(ROOT).as_posix()+'/'
 man['files']=[r for r in man['files'] if not r['path'].startswith(prefix)]
 for name in files:
  f=GROUP/name;man['files'].append(dict(path=f.relative_to(ROOT).as_posix(),bytes=f.stat().st_size,sha256=sha(f)))
 dump(mp,man)

if __name__=='__main__':
 artwork();primitives();cat=catalogs()
 dump(GROUP/'runtime/sprites.json',dict(sprites=RECORDS))
 contact('weapons-13',[w['icon'] for w in cat['weapons']]+[p['icon'] for p in cat['launcherSkillPresentations'] if p['form']!='launcher'],'DEVIANT / 무기 10종 · 외형 13종','검 4종 · 둔기 3종 · 원거리 3종 + 발사기 스킬 외형 3종',cols=5)
 contact('skills-40',[s['icon'] for s in cat['skills']],
  'DEVIANT / 특수공격 40종 · 발사기 스킬 4종 포함','차징 후 발사 · 레이저 · 샷건 · 유탄포 / 숫자·쿨타임은 별도 표시',cols=8,cw=210,ch=192)
 contact('ink-parts-54',[i['partIcon'] for i in cat['inks']],'DEVIANT / 잉크 54종','색상 물방울 + 효과 문양 + 발동 조건 배지 / 출발 팔레트에는 색상만 표시',cols=9,cw=200,ch=218)
 contact('corrected-parts',['health-fill-red','pigment-currency','minimap-frame-compact','run-card','drop-red','drop-yellow','drop-green','drop-blue','drop-purple','drop-colorless','palette-tray-3','palette-tray-4','palette-tray-5','slot-empty','slot-locked'],
  'DEVIANT / 수정 부품과 확장 팔레트','빨간 체력 · 새 안료 · 세로를 넓힌 지도 · 5색과 무색 · 3~5칸',cols=5,cw=330,ch=250)
 scenes(cat);gun_scale_preview(cat);documents(cat);register()
 print(json.dumps(dict(sprites=len(RECORDS),weapons=len(cat['weapons']),skills=len(cat['skills']),inks=len(cat['inks'])),ensure_ascii=False))
