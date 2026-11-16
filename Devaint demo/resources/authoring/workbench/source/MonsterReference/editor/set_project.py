from pathlib import Path
import argparse,hashlib,json,re,shutil
ROOT=Path(__file__).resolve().parent
p=argparse.ArgumentParser(description='기존 공통 편집기의 데이터만 추가/교체합니다. HTML/JS/CSS는 수정하지 않습니다.')
p.add_argument('source',type=Path);p.add_argument('--title',required=True);p.add_argument('--keep-active',action='store_true');args=p.parse_args()
raw=args.source.read_bytes();data=json.loads(raw.decode('utf-8-sig'))
if not any(k in data for k in ['bodyImages','images','image']):raise SystemExit('지원되는 픽셀 편집 JSON이 아닙니다.')
prefix=re.sub('[^A-Za-z0-9_-]','-',str(data.get('kind',data.get('weapon','pixels'))))[:35]
identity=prefix+'-'+hashlib.sha256(raw).hexdigest()[:12]
catalog_file=ROOT/'projects/catalog.json';catalog=json.loads(catalog_file.read_text(encoding='utf-8')) if catalog_file.exists() else {'defaultId':identity,'projects':[]}
target=ROOT/'projects'/f'{identity}.json';target.write_bytes(raw)
entry={'id':identity,'title':args.title,'file':target.name,'sourceName':args.source.name,'sha256':hashlib.sha256(raw).hexdigest()}
catalog['projects']=[x for x in catalog['projects'] if x['id']!=identity]+[entry]
if not args.keep_active:catalog['defaultId']=identity
catalog_file.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
bundle={'defaultId':catalog['defaultId'],'revision':hashlib.sha256(json.dumps(catalog,sort_keys=True).encode()).hexdigest()[:16],'projects':[]}
for entry in catalog['projects']:
 d=json.loads((ROOT/'projects'/entry['file']).read_text(encoding='utf-8-sig'));bundle['projects'].append({'id':entry['id'],'title':entry['title'],'sourceName':entry['sourceName'],'data':d})
(ROOT/'project-data.js').write_text('window.PIXEL_EDITOR_CONFIG = '+json.dumps(bundle,ensure_ascii=True)+';\n',encoding='utf-8')
print(json.dumps({'id':identity,'title':args.title,'active':catalog['defaultId'],'sourcePreserved':target.read_bytes()==raw},ensure_ascii=False))
