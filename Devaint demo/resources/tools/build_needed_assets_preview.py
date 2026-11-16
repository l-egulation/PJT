"""Build a fresh named-art review gallery; do not change consumer resources."""
from pathlib import Path
import argparse
import json
import os
from html import escape

from PIL import Image,ImageDraw

from authoring_output import ROOT,prepare_output

TITLES={'altar-state':'제단 사용 전·후','altar-state-compat':'제단 24px 호환본',
        'chapter-exit-state':'챕터 출구 상태','blueprint-card':'전체 도면 카드',
        'blueprint-illustration':'기존 버튼용 큰 무기 그림','input-glyph':'키캡',
        'story-character-still':'인물 정지 그림 후보','story-character-erased-state':'은인 색 소실 상태',
        'altar-appear':'제단 등장','altar-use':'제단 사용',
        'chapter-exit-open':'챕터 출구 열림','building-color-spread':'건물 영역별 채색',
        'ink-destruction':'가방 잉크 파괴','altar-choice-acquire':'제단 후보 획득',
        'altar-choice-discard':'제단 미선택 후보 폐기','pigment-pickup-contrast':'안료 획득 대비 보강',
        'story-helper-dissolve-mask':'조력자 희생용 정지 마스크',
        'basic-sword-hit-replacement-candidate':'누락 기본검 타격 그림의 새 대체 후보'}


def build(output):
    destination=ROOT/'authoring/needed-assets/preview'
    records=json.loads((ROOT/'authoring/needed-assets/source/output-layout.json').read_text(encoding='utf-8'))['sprites']
    def rel(path):return os.path.relpath(ROOT/path,destination).replace('\\','/')
    static=[r for r in records if r['kind'] in ['altar-state','altar-state-compat','chapter-exit-state',
                 'blueprint-card','blueprint-illustration','story-character-still','story-character-erased-state']]
    board=Image.new('RGB',(1120,((len(static)+6)//7)*240),'#eee8da');d=ImageDraw.Draw(board)
    for i,r in enumerate(static):
        im=Image.open(ROOT/r['path']).convert('RGBA');scale=3 if max(im.size)<70 else 1
        im=im.resize((im.width*scale,im.height*scale),Image.Resampling.NEAREST);im.thumbnail((150,190),Image.Resampling.NEAREST)
        x=i%7*160;y=i//7*240;board.paste(im,(x+(150-im.width)//2,y),im)
        d.text((x+3,y+197),r['name'],fill='#413e34')
    board.save(output/'static-overview.png')
    keys=[r for r in records if r['kind']=='input-glyph']
    board=Image.new('RGB',(1024,((len(keys)+7)//8)*110),'#eee8da');d=ImageDraw.Draw(board)
    for i,r in enumerate(keys):
        im=Image.open(ROOT/r['path']).resize((r['size'][0]*3,54),Image.Resampling.NEAREST)
        x=i%8*128;y=i//8*110;board.paste(im,(x+(120-im.width)//2,y),im)
        d.text((x+2,y+61),r['path'].split('/')[-2]+'\n'+r['name'],fill='#413e34')
    board.save(output/'keycaps-overview.png')
    static_html=''.join('<article><div class="still"><img src="'+rel(r['path'])+'" alt="'+escape(r['name'])+'" style="'+('width:144px' if r['kind']=='altar-state' else '')+'"></div><h3>'+TITLES[r['kind']]+'</h3><code>'+escape(r['name'])+'</code><small>'+str(r['size'][0])+'×'+str(r['size'][1])+' · pivot '+str(r['pivot'])+'</small></article>' for r in static)
    key_html=''.join('<a href="'+rel(r['path'])+'"><img src="'+rel(r['path'])+'" alt="'+r['name']+'" style="width:'+str(r['size'][0]*2)+'px;height:36px"><span>'+r['platformSet']+' / '+r['glyph']+'</span></a>' for r in keys)
    groups={}
    for r in records:
        if 'frameDurationMs' in r:
            key=r['kind']+':'+r.get('sourceBuilding','')
            groups.setdefault(key,[]).append(r)
    animations=[];data=[]
    for i,(key,members) in enumerate(groups.items()):
        head=members[0];under=''
        if head['kind']=='building-color-spread':
            under='<img class="under" src="'+rel('environments/village/runtime/buildings/starting-hub/sketch/'+head['sourceBuilding']+'.png')+'" alt="원래 스케치 건물">'
        zoom=1 if max(head['size'])>192 else 2
        w,h=head['size'];style=f'width:{w*zoom}px;height:{h*zoom}px'
        initial=members[-1] if head['kind'] in ['building-color-spread','altar-appear','chapter-exit-open'] else members[0]
        frames=[{'src':rel(r['path']),'ms':r['frameDurationMs']} for r in members]
        data.append(frames)
        strip=''.join('<img src="'+f['src']+'" alt="'+str(j+1)+'프레임">' for j,f in enumerate(frames))
        animations.append('<article class="animation"><h3>'+TITLES[head['kind']]+(' · '+head['sourceBuilding'] if head.get('sourceBuilding') else '')+'</h3><div class="stage"><div class="stack" style="'+style+'">'+under+'<img id="frame-'+str(i)+'" class="frame" src="'+rel(initial['path'])+'" alt="연출 프레임"></div></div><div class="controls"><button data-play="'+str(i)+'">재생</button><input type="range" aria-label="'+TITLES[head['kind']]+' 프레임" min="0" max="'+str(len(members)-1)+'" value="'+str(members.index(initial))+'" data-scrub="'+str(i)+'"><span id="count-'+str(i)+'">'+str(members.index(initial)+1)+' / '+str(len(members))+'</span></div><div class="strip">'+strip+'</div></article>')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Deviant · 추가 필요 에셋 검토</title><style>
*{box-sizing:border-box}body{margin:0;background:#f2eedf;color:#3c473c;font:14px/1.8 'Malgun Gothic',sans-serif}main{max-width:1280px;margin:auto;padding:35px 24px}h1{font:700 40px Georgia,serif;margin:0}h2{font-size:23px;margin:38px 0 16px}h3{font-size:13px;margin:10px 0 0}p{color:#7b846e}code{font-size:10px;color:#8b927e;word-break:break-all}small{display:block;font-size:10px;color:#8a927e}.note{background:#e3e9d8;border-left:3px solid #719376;padding:14px 18px;margin:20px 0;color:#60705b}.toolbar{display:flex;gap:8px;align-items:center;margin:20px 0}button{font:inherit;cursor:pointer;border:1px solid #aeba9e;border-radius:4px;padding:7px 15px;background:#faf7e9;color:#496b50}button[aria-pressed=true]{background:#3d6c55;color:white}button:focus-visible,a:focus-visible,input:focus-visible{outline:3px solid #9d793b;outline-offset:3px}img{display:block;max-width:100%;image-rendering:pixelated}.static-grid{display:grid;grid-template-columns:repeat(5,1fr);gap:12px}article{background:#faf7ec;border:1px solid #cfd6bf;border-radius:6px;padding:16px;overflow:hidden}.still{height:200px;display:flex;align-items:center;justify-content:center;background:var(--art-bg,#ece7d8)}.still img{max-height:192px;object-fit:contain}.key-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:9px}.key-grid a{display:flex;align-items:center;flex-direction:column;gap:12px;padding:16px 5px;text-decoration:none;border:1px solid #cdd3be;background:#e8e9d9;border-radius:5px}.key-grid span{font-size:10px;color:#76826b}.animations{display:grid;grid-template-columns:repeat(3,1fr);gap:15px}.stage{height:280px;background:var(--art-bg,#ece7d8);display:flex;justify-content:center;align-items:center;overflow:auto;margin:12px 0}.stack{position:relative;max-width:100%;flex-shrink:0}.stack>img{width:100%;height:100%;object-fit:contain}.under{position:absolute;inset:0}.frame{position:relative}.controls{display:flex;gap:9px;align-items:center}.controls input{min-width:50px;width:90px}.controls span{font-size:10px;color:#849077}.controls button{font-size:11px;padding:6px 12px}.strip{display:flex;gap:3px;margin-top:12px;overflow:auto}.strip img{width:32px;height:32px;object-fit:contain;flex-shrink:0;background:#dbe0cf}.dark{--art-bg:#424840}.dark .key-grid a{background:#596052}.overview{border:1px solid #d1d7c3;background:#ece7d8;max-width:1120px}.foot{margin:35px 0;font-size:12px;color:#89937c}.badge{font-size:11px;letter-spacing:1px;color:#6c8362}@media(max-width:900px){.static-grid{grid-template-columns:repeat(3,1fr)}.animations{grid-template-columns:repeat(2,1fr)}.key-grid{grid-template-columns:repeat(5,1fr)}}@media(max-width:550px){main{padding:24px 14px}.static-grid,.animations{grid-template-columns:1fr 1fr}.key-grid{grid-template-columns:repeat(3,1fr)}.stage{height:230px}article{padding:10px}.controls{flex-wrap:wrap}.still{height:190px}}
</style><main><span class="badge">NAMED REVIEW ASSETS / NO GAME DEPLOYMENT</span><h1>Deviant</h1><p>temporary-and-needed-assets.md의 추가 작업본 · 이름별 PNG와 적용안 검토</p><div class="note"><strong>새 PNG 151개</strong> · 제단·출구·도면 카드/그림·키캡 41개·채색/파괴/획득 효과·인물 후보.<br>파일 검수용입니다. 게임 코드·씬·Unity 배치 계약에는 적용하지 않았습니다. 외형과 출구 연출·신규 프레임 시간은 승인 전 후보입니다. 기본검 6장은 승인 원본 복원이 아닌 새 대체 후보입니다.</div><div class="toolbar"><button data-theme="paper" aria-pressed="true">종이 배경</button><button data-theme="dark" aria-pressed="false">어두운 배경</button><span>상태·투명도·문양·가독성을 비교하세요.</span></div><h2>정지 그림과 인물</h2><div class="static-grid">__STATIC__</div><h2>키보드·게임패드 키캡 41개</h2><div class="key-grid">__KEYS__</div><h2>연출 프레임 · 재생하거나 직접 넘겨 보기</h2><div class="animations">__ANIMATIONS__</div><h2>파일 크기 비교</h2><img class="overview" src="static-overview.png" alt="정지 그림 비교"><h2>키캡 확대 비교</h2><img class="overview" src="keycaps-overview.png" alt="모든 키캡의 네이티브 문양 비교"><p class="foot">효과음은 메인 저장소의 기존 승인 WAV 6개를 8개 사건에 재사용하는 제안으로 문서화했습니다. 원본 음원과 기존 그림은 바꾸지 않습니다. 원화·프롬프트·피벗·규격·출처는 같은 제작 세트에서 확인할 수 있습니다.</p><script>
const frames=__FRAME_DATA__,timers={};
function show(i,n){const row=frames[i];document.getElementById('frame-'+i).src=row[n].src;document.getElementById('count-'+i).textContent=(n+1)+' / '+row.length;document.querySelector('[data-scrub="'+i+'"]').value=n}
document.querySelectorAll('[data-play]').forEach(b=>b.addEventListener('click',()=>{const i=Number(b.dataset.play);clearTimeout(timers[i]);let n=0;function tick(){show(i,n);if(n<frames[i].length-1){const ms=frames[i][n].ms;n++;timers[i]=setTimeout(tick,ms)}}tick()}));
document.querySelectorAll('[data-scrub]').forEach(s=>s.addEventListener('input',()=>{const i=Number(s.dataset.scrub);clearTimeout(timers[i]);show(i,Number(s.value))}));
document.querySelectorAll('[data-theme]').forEach(b=>b.addEventListener('click',()=>{document.body.classList.toggle('dark',b.dataset.theme==='dark');document.querySelectorAll('[data-theme]').forEach(x=>x.setAttribute('aria-pressed',String(x===b)))}));
</script></main></html>'''
    for key,value in {'STATIC':static_html,'KEYS':key_html,'ANIMATIONS':''.join(animations),'FRAME_DATA':json.dumps(data,ensure_ascii=False)}.items():html=html.replace('__'+key+'__',value)
    (output/'index.html').write_bytes(html.encode('utf-8'))
    print('Built static/keycap sheets and',len(groups),'playable frame reviews.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--destination',type=Path,required=True)
    build(prepare_output(p.parse_args().destination))
