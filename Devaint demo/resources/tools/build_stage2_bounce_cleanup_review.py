"""Build the Bounce A cleanup review: cleaned RGBA vs. pre-cleanup source, same timing, independent players."""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
PREVIEW = GROUP / 'preview/bounce-cleanup'
OLD = GROUP / 'preview/bounce-v3-candidates'


def main():
    config = json.loads((OLD / 'candidates.json').read_text(encoding='utf-8'))
    motion = json.loads((GROUP / 'runtime/bounce-a/motion.json').read_text(encoding='utf-8'))
    a = next(c for c in config['candidates'] if c['id'] == 'A')
    assert a['sourceFrames'] == motion['sourceFrames'] and a['durationsMs'] == motion['durationsMs']
    clean = copy.deepcopy(a)
    clean.update(name='정리본 · 투명 PNG', clean=True,
                 frameUrls=[f'../../runtime/bounce-a/{f["file"]}' for f in motion['frames']],
                 note='배경제거·외곽 정리·승인 석궁 적용. 11·22의 소매 옆 잔선 제거. 가면·손·정강이·장화는 원본 색 유지.')
    raw = copy.deepcopy(a)
    raw.update(id='RAW', name='정리 전 · 원본 셀', clean=False,
               frameUrls=[f'{config["frameBase"]}{f:02}.png' for f in a['sourceFrames']],
               note='같은 A 시간표의 원본 셀. 흰색 키는 브라우저에서만 적용한 비교용이며 석궁도 생성된 그대로입니다.')
    config.update(format='stage2-bounce-A-cleanup-review-v1', status=motion['status'], candidates=[clean, raw],
                  cleanedMotion='../../runtime/bounce-a/motion.json')
    (PREVIEW / 'candidates.json').write_text(json.dumps(config, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    js = (OLD / 'candidates.js').read_text(encoding='utf-8')
    start = js.index(' const ids=')
    end = js.index(' const [idle,player,room]', start)
    js = js[:start] + ''' const ids=[...new Set(D.candidates.flatMap(c=>c.sourceFrames))],cut={};
 await Promise.all(D.candidates.flatMap(c=>c.sourceFrames.map(async(id,i)=>{const im=await load(c.frameUrls[i]);if(c.clean){cut[c.id+'/'+id]=im;return;}const canvas=document.createElement('canvas');canvas.width=640;canvas.height=360;const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(im,0,0);const px=ctx.getImageData(0,0,640,360),d=px.data,wm=D.previewMatte.whiteMinimum,dm=D.previewMatte.maximumChannelDifference;for(let j=0;j<d.length;j+=4){const mn=Math.min(d[j],d[j+1],d[j+2]),mx=Math.max(d[j],d[j+1],d[j+2]);if(mn>=wm&&mx-mn<=dm)d[j+3]=0;}ctx.putImageData(px,0,0);cut[c.id+'/'+id]=canvas;})));
''' + js[end:]
    replacements = [
        ("function pose(ctx,id,x=192,y=232){ctx.imageSmoothingEnabled=false;ctx.drawImage(cut[id],x-192+OX,y-232+OY,DW,DH);}",
         "function pose(ctx,c,id,x=192,y=232){ctx.imageSmoothingEnabled=false;if(c.clean)ctx.drawImage(cut[c.id+'/'+id],x-192,y-232);else ctx.drawImage(cut[c.id+'/'+id],x-192+OX,y-232+OY,DW,DH);}"),
        ("pk=id+'/'+$('#bg').value", "pk=c.id+'/'+id+'/'+$('#bg').value"),
        ("pose(clear(card.querySelector('canvas')),id)", "pose(clear(card.querySelector('canvas')),c,id)"),
        ("dk=id+'/'+$('#bg').value", "dk=c.id+'/'+id+'/'+$('#bg').value"),
        ("pose(clear($('#motion')),id)", "pose(clear($('#motion')),c,id)"),
        ("if(mapKey!==id)", "if(mapKey!==c.id+'/'+id+'/'+$('#bg').value)"),
        ("pose(ctx,id,740,445);mapKey=id", "pose(ctx,c,id,740,445);mapKey=c.id+'/'+id+'/'+$('#bg').value"),
        ("후보를 고르면 배경제거·픽셀 정리를 진행합니다.", "정리본은 투명 PNG를 그대로 재생합니다."),
    ]
    for old, new in replacements:
        assert old in js, old
        js = js.replace(old, new)
    (PREVIEW / 'candidates.js').write_text(js, encoding='utf-8')

    html = (OLD / 'index.html').read_text(encoding='utf-8')
    style = html[html.index('<style>'):html.index('</style>') + 8].replace('repeat(3,minmax(0,1fr))', 'repeat(2,minmax(0,1fr))')
    head = '<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>5연사 A 정리본</title><link rel="stylesheet" href="../review.css">'
    controls = html[html.index('<div class="controls"><button id="play">'):html.index('<div id="choices"')]
    controls = controls.replace('<option value="#808080" selected>중간 회색</option><option value="#000000">검정</option>', '<option value="#808080">중간 회색</option><option value="#000000" selected>검정</option>')
    sections = html[html.index('<section><h2>승인 기본과 비교'):html.index('<section><h2>방출 정점')]
    sections = sections.replace('비교 중인 후보', '비교 중').replace('5연사 · 같은 축척', '5연사 A · 같은 축척')
    body = f'''
<main><p class="eyebrow">STAGE 02 / PHASE 03 / BOUNCE ×5 / A CLEANUP</p><h1>튕기는 화살 5연사 · A 정리본</h1>{'<p class="notice">사용자 확정 (A 확정). Unity 적용 전입니다. <a href=\"../rain-input/\">다음 공격: 화살비 Aether 입력</a></p>' if motion.get('approval') else ''}
<p>2차(절도 프롬프트 v3) 결과 · A 타이밍 · 원본 01~32의 32장 · 2.95초 · 방출 0.5 / 1.0 / 1.5 / 2.0 / 2.5초 (06 ↖ · 12 → · 17 ↖ · 23 → · 28 ↖). 다섯 번째 방출 뒤 뻗은 자세로 끝나며 기본 자세로 돌아가지 않습니다.</p>
<p class="notice">배경제거와 외곽 정리를 마친 투명 PNG입니다. 유도 화살 D V2와 같은 방식으로 흰 배경을 원본 해상도에서 분리했고 알파는 0/255입니다. 이번 시트에 맞춘 보존 보정으로 장화·정강이·가면 눈가·장화 밑창을 원본 색 그대로 지켰습니다. 석궁은 승인 원본 107×41 픽셀을 이동만 했습니다. Unity 화면이 아닙니다.</p>
<nav><a href="../../runtime/bounce-a/sheet.png" target="_blank">투명 PNG 시트</a><a href="bounce-a-clean.webp" target="_blank">A 애니메이션</a><a href="../../runtime/bounce-a/motion.json" target="_blank">프레임·시간표</a><a href="../bounce-v3-candidates/">타이밍 비교 A/B/C</a><a href="../homing-cleanup/">유도 화살 D V2</a></nav>
<p>카드마다 재생·처음부터·이전/다음 프레임·시간 슬라이더·반복을 따로 쓸 수 있습니다. “동시에 한 번 재생”은 정리 전후를 같은 시점에 보여 줍니다.</p>
{controls}<div id="choices" class="choices"></div>
{sections}<section><h2>외곽 확대 확인</h2><p>흰색·회색·검정 배경에서 감기 / 왼쪽 위 정점 / 유지 / 오른쪽 정점 / 4발 / 5발 자세를 2배로 봅니다.</p><a href="edges-three-backgrounds.png" target="_blank"><img style="width:100%;image-rendering:pixelated" src="edges-three-backgrounds.png" alt="세 배경에서 본 A 정리본 외곽"></a><p><a href="all-frames-black.png" target="_blank">32장 전체 · 검정 배경</a></p></section>
<p id="status" role="status">프레임 로딩 중…</p></main><script type="module" src="candidates.js"></script></html>'''
    (PREVIEW / 'index.html').write_text(head + style + body, encoding='utf-8')


if __name__ == '__main__':
    main()
