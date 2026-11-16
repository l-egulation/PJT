"""Build the selected D cleanup review using the existing independent player."""
import copy
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
GROUP=ROOT/'monsters/stage-02/boss-phase3-attacks-20261106'
PREVIEW=GROUP/'preview/homing-cleanup'
OLD=GROUP/'preview/homing-candidates'


def main():
    config=json.loads((OLD/'candidates.json').read_text(encoding='utf-8'))
    d=copy.deepcopy(config['candidates'][0])
    raw=copy.deepcopy(d)
    d.update(name='외곽 추가 정리 · V2',clean=True,version='homing-d-v2',note='로브·소매·후드 가장자리 1~2픽셀의 밝은 잔색과 드문 돌출 픽셀을 추가 정리했습니다. 가면·손·석궁·D 타이밍 유지.')
    raw.update(id='BEFORE',name='이전 정리본 · V1',clean=True,version='homing-d',note='직전에 확인한 첫 배경제거본입니다. 동일한 D 타이밍으로 V2와 비교할 수 있습니다.')
    config.update(format='stage2-homing-D-cleanup-review-v1',status='D-timing-selected-cleanup-review',candidates=[d,raw])
    config['previewMatte']={'D':'none; use exported V2 RGBA directly','BEFORE':'none; use preserved V1 RGBA directly'}
    config['cleanedMotion']='../../runtime/homing-d-v2/motion.json'
    (PREVIEW/'candidates.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    js=(OLD/'candidates.js').read_text(encoding='utf-8')
    start=js.index(' const ids=')
    end=js.index(' const [idle,player,room]',start)
    js=js[:start]+''' const ids=[...new Set(D.candidates.flatMap(c=>c.sourceFrames))],cut={};
 await Promise.all(D.candidates.flatMap(c=>c.sourceFrames.map(async id=>{
  const path=c.clean?'../../runtime/'+c.version+'/frames/'+String(id-4).padStart(2,'0')+'.png':D.frameBase+String(id).padStart(2,'0')+'.png';
  const im=await load(path);
  if(c.clean){cut[c.id+'/'+id]=im;return;}
  const canvas=document.createElement('canvas');canvas.width=640;canvas.height=360;
  const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(im,0,0);
  const pixels=ctx.getImageData(0,0,640,360);
  for(let i=0;i<pixels.data.length;i+=4){const rr=pixels.data[i],g=pixels.data[i+1],b=pixels.data[i+2];if(Math.min(rr,g,b)>=250&&Math.max(rr,g,b)-Math.min(rr,g,b)<=4)pixels.data[i+3]=0;}
  ctx.putImageData(pixels,0,0);cut[c.id+'/'+id]=canvas;
 })));
''' + js[end:]
    js=js.replace("function pose(ctx,id,x=192,y=232){ctx.drawImage(cut[id],x-240,y-253,480,270);}","function pose(ctx,c,id,x=192,y=232){if(c.clean)ctx.drawImage(cut[c.id+'/'+id],x-192,y-232);else ctx.drawImage(cut[c.id+'/'+id],x-240,y-253,480,270);}")
    js=js.replace("paintKey=id+'/'", "paintKey=c.id+'/'+id+'/'")
    js=js.replace("pose(clear(card.querySelector('canvas')),id)","pose(clear(card.querySelector('canvas')),c,id)")
    js=js.replace("const dk=id+'/'", "const dk=c.id+'/'+id+'/'")
    js=js.replace("pose(clear($('#motion')),id)","pose(clear($('#motion')),c,id)")
    js=js.replace("if(mapKey!==id)","if(mapKey!==c.id+'/'+id)")
    js=js.replace("pose(ctx,id,740,445);mapKey=id", "pose(ctx,c,id,740,445);mapKey=c.id+'/'+id")
    js=js.replace("D/E는 18장 연속 연결, R은 원본 35장 참고용입니다. 선택 후 최종 픽셀 보정을 진행합니다.","외곽 추가 정리 V2와 이전 V1을 같은 시간표로 비교합니다. 18장 · 1.70초 · 방출 1초. 투명 PNG 직접 재생 중입니다.")
    (PREVIEW/'candidates.js').write_text(js,encoding='utf-8')
    html=(OLD/'index.html').read_text(encoding='utf-8')
    style=html[html.index('<style>'):html.index('</style>')+8].replace('repeat(3,minmax(0,1fr))','repeat(2,minmax(0,1fr))')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>유도 화살 D · 외곽 추가 정리 V2</title><link rel="stylesheet" href="../review.css">'''+style+'''
<main><p class="eyebrow">STAGE 02 / PHASE 03 / HOMING D / V2</p><h1>D · 외곽 추가 정리 V2</h1>
<p class="notice">유도 화살 D V2 사용자 확정. <a href="../bounce-input/">다음 공격: 튕기는 화살 5연사 Aether 입력</a></p>
<p>D 확정 · 18장 연속 · 1.70초 · 1초에 방출. 프레임과 재생 속도는 선택한 D 그대로입니다.</p>
<p class="notice">로브·소매·후드 외곽의 밝은 잔색과 돌출 픽셀을 추가 정리했습니다. 가면·손·다리·장화와 석궁은 이전 정리본 그대로 유지했습니다.</p>
<nav><a href="../../runtime/homing-d-v2/sheet.png" target="_blank">V2 투명 PNG 시트</a><a href="homing-d-clean-v2.webp" target="_blank">V2 D 애니메이션</a><a href="../../runtime/homing-d-v2/motion.json" target="_blank">프레임·시간표</a><a href="../homing-candidates/">이전 타이밍 비교</a></nav>
<p>각 카드에서 개별 재생·반복·한 프레임 이동이 가능합니다. “동시에 한 번 재생”으로 정리 전후를 같은 시점에서 비교할 수 있습니다.</p>
<div class="controls"><button id="play">동시에 한 번 재생</button><button id="pause">일시정지</button><label><input id="loop" type="checkbox"> 검토용 반복</label><label>속도 <select id="speed"><option value="0.5">0.5배</option><option value="1" selected>1배</option></select></label><label>배경 <select id="bg"><option value="#ffffff">흰색</option><option value="#808080">중간 회색</option><option value="#000000" selected>검정</option></select></label></div>
<label style="display:flex">전체 시간 탐색 <input id="seek" type="range" min="0" max="1700" value="0" step="10" style="flex:1"><output id="time">D: 0 ms</output></label>
<div id="choices" class="choices"></div>
<section><h2>승인 기본과 비교 · 2배</h2><p>현재 선택: <strong id="selected">D</strong>. 캔버스 384×256, 피벗 (192,232), 본체 공통 0.75배 유지. 프레임별 자동 확대 없음.</p><div class="scroll"><div class="compare"><figure><figcaption>승인 기본 자세</figcaption><canvas id="baseline" width="384" height="256"></canvas></figure><figure><figcaption>선택한 D 동작</figcaption><canvas id="motion" width="384" height="256"></canvas></figure></div></div></section>
<section><h2>맵 + 플레이어 · 960×540</h2><p>왼쪽 플레이어, 가운데 승인 기본, 오른쪽 D 동작. 에셋 합성 검토 화면이며 Unity 적용 전입니다.</p><div class="controls"><label>화면 표시 <select id="mapZoom"><option value="1">1배</option><option value="2">2배</option></select></label></div><div class="scroll"><canvas id="map" width="960" height="540"></canvas></div></section>
<section><h2>외곽 확대 확인</h2><p>흰색·회색·검정 배경의 준비 / 연결 / 방출 자세입니다. 이미지를 열면 원래 크기로 볼 수 있습니다.</p><a href="edges-three-backgrounds-v2.png" target="_blank"><img style="width:100%;image-rendering:pixelated" src="edges-three-backgrounds-v2.png" alt="세 배경에서의 V2 D 외곽 비교"></a><p><a href="all-frames-black-v2.png" target="_blank">V2 18장 전체 · 검정 배경에서 보기</a> · <a href="edge-v1-v2.png" target="_blank">V1 / V2 외곽 확대 비교</a></p></section>
<p id="status" role="status">D 프레임 로딩 중…</p></main><script type="module" src="candidates.js"></script></html>'''
    (PREVIEW/'index.html').write_text(html,encoding='utf-8')


if __name__=='__main__':
    main()
