"""Export accepted MVP growth data without replacing legacy runtime ink resources."""
import argparse, json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
VALUE_KEYS=('rechargeReduction','ratio','amount','damageTaken','windupReduction','duration','distance','radius','seconds','interval','attackSpeed','moveSpeed','switchWindupReduction','threshold','amountPerKill','cap','amountPerStack','maxStacks','resetSeconds','ratioPerState','count')
# Display labels and number formats match tools/ink-combination-lab/generate-color-parts.cjs, so the in-game
# slot readout uses the catalog's own wording ("비율 20%", "지속 0.8초").
PARAM_NAMES={'rechargeReduction':'충전 회복 단축','amountPerKill':'처치당','cap':'최대','amountPerStack':'중첩당','maxStacks':'최대 중첩','resetSeconds':'초기화','ratio':'비율','amount':'효과량','attackSpeed':'공속','moveSpeed':'이속','switchWindupReduction':'스위칭 준비 단축','damageTaken':'받는 피해','windupReduction':'준비 단축','threshold':'체력 기준','ratioPerState':'상태당 비율','duration':'지속','interval':'간격','seconds':'감소','radius':'반경','distance':'이동 거리','count':'대상 수'}
PERCENT_KEYS={'rechargeReduction','ratio','amount','damageTaken','windupReduction','attackSpeed','moveSpeed','switchWindupReduction','threshold','ratioPerState','amountPerKill','cap','amountPerStack'}
SECOND_KEYS={'duration','interval','seconds','resetSeconds'}
def fmt(key,base,value):
 number=lambda v:('%.2f'%v).rstrip('0').rstrip('.')
 if key in PERCENT_KEYS and (base<=1 or key=='ratio'): return number(round(value*10000)/100)+'%'
 return number(value)+('초' if key in SECOND_KEYS else '')
def stats(row,p,levels):
 keys=[t['parameter'] for t in row['upgradeTargets']]+[k for k in row.get('levelTable',{}) if k not in [t['parameter'] for t in row['upgradeTargets']]]
 def display(key,level):
  unit=row.get('statUnits',{}).get(key)
  if unit is None: return fmt(key,p['params'].get(key,levels[0][key]),level[key])
  value=level[key]*100 if unit=='%' else level[key]
  return ('%.3f'%value).rstrip('0').rstrip('.')+unit
 return [dict(label=row.get('statLabels',{}).get(k,PARAM_NAMES[k]),values=[display(k,level) for level in levels]) for k in keys]
def build():
 policy=json.loads((ROOT/'data/combat/weapon_growth_mvp_proposal.json').read_text(encoding='utf-8'))
 tuning=json.loads((ROOT/'data/combat/weapon_growth_mvp_tuning_draft.json').read_text(encoding='utf-8'))
 catalog=json.loads((ROOT/'data/combat/ink_color_parts_proposal.json').read_text(encoding='utf-8'))
 part_tuning=json.loads((ROOT/'data/combat/ink_color_parts_tuning.json').read_text(encoding='utf-8'))
 inventory=json.loads((ROOT/'data/inventory/temporary-inks.json').read_text(encoding='utf-8'))
 assert 'perKindCapacity' not in inventory and inventory['capacity']==5
 assert math.isfinite(tuning['proposedSafety']['shieldCap']) and tuning['proposedSafety']['shieldCap']>0,'shieldCap must be finite and positive'
 by_id={p['id']:p for p in catalog['parts']}
 assert tuning['status']=='accepted_test_baseline_implementation_pending'
 assert [r['id'] for r in part_tuning['partTuning']]==[p['id'] for p in catalog['parts']]
 parts=[]
 for row in part_tuning['partTuning']:
  p=by_id[row['id']]; levels=[]
  assert set(p['params'])<=set(VALUE_KEYS)|{'target'},p['id']
  for level in range(6):
   values={k:v for k,v in p['params'].items() if k in VALUE_KEYS}
   for target in row['upgradeTargets']: values[target['parameter']]*=tuning['upgradeMultipliers'][target['family']][level]
   for key,table in row.get('levelTable',{}).items():
    assert key in VALUE_KEYS and len(table)==6 and all(isinstance(v,(int,float)) and math.isfinite(v) and v>=0 for v in table), p['id']+' level table'
    assert table==sorted(table),p['id']+' nondecreasing levels'
    if row.get('statUnits',{}).get(key)=='HP' or key in ('count','maxStacks'):
     assert all(v==int(v) for v in table),p['id']+' integer quantities'
    values[key]=table[level]
   levels.append(values)
  parts.append(dict(id=p['id'],name=p['name'],description=p['playerText'],color=p['color'],trigger=p['trigger'],kind=p['kind'],scope=row['scope'],grade=row['fixedGrade'],duplicate=p['duplicate'],cooldown=p['cooldownSeconds'],targetCooldown=row['cooldownScope']=='instance_and_target',eliteCooldown=p.get('eliteCooldownSeconds',0),every=p.get('every',0),condition=p.get('condition',''),markIndex=p.get('markIndex',0),icon=part_tuning['temporaryIcons'][p['color']],levels=levels,stats=stats(row,p,levels)))
 assert len(parts)==53 and len({p['id'] for p in parts})==53
 assert all((p['trigger']=='mark_pop')==(1<=p['markIndex']<=7) for p in parts)
 assert all(0<=v.get('switchWindupReduction',0)<1 for p in parts for v in p['levels'])
 for color in part_tuning['temporaryIcons']:
  assert 0 < len([p for p in parts if p['color']==color]) and {p['grade'] for p in parts if p['color']==color}<={1,2,3,4},color
 result=dict(schema=1,parts=parts,gradeSources=[dict(source=k,weights=v) for k,v in tuning['gradeWeights'].items()],rewards=[dict(source=k,**v) for k,v in tuning['rewards'].items() if k!='firstElitePalette'],slotWeights=list(tuning['palettes']['slotWeights'].values()),colorWeights=[dict(color=k,weight=v) for k,v in tuning['palettes']['colorWeights'].items()],concentratedColorSelection=tuning['palettes'].get('concentratedColorSelection'),colorlessChancePerSlot=tuning['palettes']['colorlessChancePerSlot'],earlyOwnedColorInks=tuning['palettes']['earlyOwnedColorInks'],colorChangeGold=tuning['costs']['colorChangeGold'],colorChangesPerRun=tuning['costs']['colorChangesPerRun'],saleFraction=tuning['costs']['inkSale']['drawPriceFraction'],attackPerLevel=tuning['commonAttackGrowth']['perLevelAdditiveFraction'],attackMaxLevel=tuning['commonAttackGrowth']['proposedMaxLevel'],markCoefficient=tuning['mark']['coefficientPerConsumedMark'],markCap=tuning['mark']['cap'],markDuration=tuning['mark']['durationSeconds'],markPopInterval=tuning['mark']['popIntervalSeconds'],basicReferencePolicy=tuning.get('basicReferencePolicy'),**tuning['proposedSafety'])
 result['inkCapacity']=inventory['capacity']
 return json.dumps(result,ensure_ascii=False,indent=2)+'\n'
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
 target=ROOT/'unity/Assets/Deviant/Combat/Resources/WeaponGrowthMvp.json';text=build()
 if args.check:
  if not target.exists() or target.read_text(encoding='utf-8')!=text: raise SystemExit('MVP growth resource stale')
 else: target.write_text(text,encoding='utf-8')
 print('PASS: 53 color ink parts, six levels, prices and source-weighted rewards')
if __name__=='__main__': main()
