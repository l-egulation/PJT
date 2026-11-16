"""최종 PNG를 편집기의 시작 데이터로 갱신합니다. 기존 브라우저 작업은 보존됩니다."""
from pathlib import Path
from PIL import Image
import json, base64, hashlib

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
items=[]
for species,motion in [('잉크눈','이동'),('잉크눈','공격'),('멧돼지','걷기'),('멧돼지','돌진')]:
    folder=ROOT/species/'4_픽셀128'/motion
    meta=json.loads((folder/'정보.json').read_text(encoding='utf-8-sig'))
    frames=[base64.b64encode(Image.open(folder/f'{i:02}.png').convert('RGBA').tobytes()).decode('ascii') for i in range(1,13)]
    items.append({'id':species+'/'+motion,'species':species,'motion':motion,'width':meta['canvas'][0],'height':meta['canvas'][1],'palette':meta['palette'],'meta':meta,'settings':(folder/'Unity_설정.txt').read_text(encoding='utf-8-sig'),'frames':frames})
revision=hashlib.sha256(json.dumps(items,ensure_ascii=False).encode()).hexdigest()[:16]
project={'version':1,'type':'ink-monster-pixel-editor','revision':revision,'motions':items}
(HERE/'data.js').write_text('window.MONSTER_INITIAL='+json.dumps(project,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
print('Editor data: 4 motions / 48 frames / revision '+revision)
