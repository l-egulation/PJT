from pathlib import Path
import base64,json,argparse

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
P=ROOT/'02_에셋/캐릭터/잉크/무기/채찍/04_확정본/게임용_최종'
cfg=json.loads((P/'재생설정.json').read_text(encoding='utf8'))
ep=ROOT/cfg['effect_config'];ec=json.loads(ep.read_text(encoding='utf8'))
def uri(p):return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
assets={k:uri(P/f['file']) for k,f in cfg['frames'].items()}
groups={}
labels={'attack1':'1타 공격 · 캐릭터와 채찍','attack2':'2타 공격 · 캐릭터와 채찍','idle':'대기'}
for m in cfg['modules']:
    groups[m['id']]={'name':labels.get(m['id'],m['id']),'steps':[{'key':s['frame'],'ms':s['ms'],'label':s.get('phase','')} for s in m['steps']],'target':'character'}
for kind,e in ec['effects'].items():
    keys=[]
    for i,f in enumerate(e['frames']):
        key=f'FX_{kind}_{i+1:02}';assets[key]=uri(ep.parent/f);keys.append(key)
    groups['fx_'+kind]={'name':('1타' if kind=='up' else '2타')+' 이펙트만','steps':[{'key':k,'ms':ms,'label':kind} for k,ms in zip(keys,e['ms'])],'target':'effect','kind':kind}
data={'assets':assets,'groups':groups,'character':cfg,'effects':ec}
parser=argparse.ArgumentParser()
parser.add_argument('--retouch',type=Path)
args=parser.parse_args()
if args.retouch:
    saved=json.loads(args.retouch.read_text(encoding='utf-8-sig'))
    assert saved['type']=='whip_confirmed_retouch'
    for key,src in {**saved['assets'],**saved['effectAssets']}.items():
        assert key in data['assets']
        data['assets'][key]=src
    data['initial_changed']=saved['changedKeys']
html=(HERE/'template.html').read_text(encoding='utf8').replace('/*DATA*/',json.dumps(data,ensure_ascii=False,separators=(',',':')))
target=HERE/('편집_통일본.html' if args.retouch else '편집.html')
if args.retouch:
    html=html.replace('채찍 · 확정 프레임 픽셀 편집','채찍 · 사용자 수정 통일본 편집').replace("'채찍_확정본_리터치.json'","'채찍_확정본_리터치_통일.json'")
target.write_text(html,encoding='utf8')
print('Built native-grid whip editor:',len(assets),'editable character/effect frames.')
