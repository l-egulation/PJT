from pathlib import Path
import json,base64,hashlib,copy
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
NAMES={'sword':'검','whip':'채찍','rapier':'레이피어','dagger':'단검'}
payload=json.loads((HERE/'환경데이터.json').read_text(encoding='utf8'))
combat=json.loads((HERE/'전투설정.json').read_text(encoding='utf8'))
hashes={}
def blob(p):
 b=p.read_bytes();hashes[str(p.relative_to(ROOT))]=hashlib.sha256(b).hexdigest();return 'data:image/png;base64,'+base64.b64encode(b).decode()
weapons={}
for key,name in NAMES.items():
 folder=ROOT/f'02_에셋/캐릭터/잉크/무기/{name}/04_확정본/게임용_최종'
 cfg=json.loads((folder/'재생설정.json').read_text(encoding='utf8'))
 hashes[str((folder/'재생설정.json').relative_to(ROOT))]=hashlib.sha256((folder/'재생설정.json').read_bytes()).hexdigest()
 ep=ROOT/cfg['effect_config'];ec=json.loads(ep.read_text(encoding='utf8'))
 w={'id':key,'name':name,'frames':{uid:blob(folder/f['file']) for uid,f in cfg['frames'].items()},'modules':cfg['modules'],'pivot':cfg['pivot_top_left'],'frameOffsets':{},'framePivots':{},'effects':{},'effectTiming':{},'fxOffset':ec['placement']['offset'],'fxPivot':ec['placement']['source_pivot'],'labels':combat[key]['labels'],'attacks':copy.deepcopy(combat[key]['attacks']),'comboWindowMs':combat[key]['comboWindowMs'],'package':str(folder.relative_to(ROOT))}
 for m in w['modules']:m['duration_ms']=sum(s['ms'] for s in m['steps'])
 for kind,e in ec['effects'].items():w['effects'][kind]=[blob(ep.parent/file) for file in e['frames']];w['effectTiming'][kind]=e['ms']
 w['effectMs']=next(iter(w['effectTiming'].values()))
 if ec['placement'].get('aligned_hits'):w['alignedFx']=ec['placement']['aligned_hits']
 for n,c in cfg['attack_events'].items():
  w['attacks'][n].update(c)
  duration=next(m['duration_ms'] for m in w['modules'] if m['id']=='attack'+n)
  w['attacks'][n]['bufferEnd']=duration;w['attacks'][n]['bufferMs']=duration+300
 if key=='sword':w['comboWindowMs']=220;w['labels'].update({'attack1':'1타 · 올려베기','attack2':'2타 · 내려베기'})
 weapons[key]=w
payload['weapons']=weapons;payload['defaultWeapon']='sword'
(HERE/'assets.js').write_text('const PLAY_ASSETS='+json.dumps(payload,ensure_ascii=False,separators=(',',':'))+';',encoding='utf8')
(HERE/'원본_출처.json').write_text(json.dumps({'status':'네 무기 확정본 플레이 테스트','source':'각 무기 04_확정본/게임용_최종 및 이펙트 04_확정본만 사용','source_sha256':hashes},ensure_ascii=False,indent=2),encoding='utf8')
html=(HERE/'page.html').read_text(encoding='utf8')
(HERE/'플레이.html').write_text(html,encoding='utf8')
print('Built presentation assets and common-game launch guide; source inputs:',len(hashes))
