"""Make a local, byte-exact purchase-pack tone comparison without sound design."""
import argparse
import html
import io
import json
import wave
import zipfile
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True)
    parser.add_argument('--round',type=int,choices=(1,2),default=2);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';rawdir=asset/'source/raw-originals';rawdir.mkdir(exist_ok=True)
    if args.round==1:
        specs=[('smoke',f'S{i}',f'가스 분출 원음 {i}',f'Explosion_Fire_Gas/gas_leak_short_burst_{i:02}.wav') for i in (1,2,3)]
        specs += [('bomb',f'B{i}',f'버블 원음 {i}',f'Comedy_Cartoon/bubble_effect_{i:02}.wav') for i in (1,2,3,4)]
        specs += [('bomb',f'B{i+4}',f'물 분출 원음 {i}',f'Comedy_Cartoon/cartoon_squirt_{i:02}.wav') for i in (1,2)]
    else:
        specs=[('smoke',f'S{i+11}',f'공압 장치 압력 방출 {n}',f'Doors/hydraulic_strut_air_gas_shock_door_{n:02}.wav') for i,n in enumerate((3,1,2,4,5))]
        specs += [('smoke',f'S{i+16}',f'분무기 분사 {n}',f'Miscellaneous/Spray Bottle/spray_bottle_{n:02}.wav') for i,n in enumerate((7,12,13,8,4))]
        specs += [('bomb',f'B{i+11}',f'물 분출 마법 {n}',f'Magic_Spells/water_blast_projectile_spell_{n:02}.wav') for i,n in enumerate((5,1,2,3,4))]
        specs += [('bomb',f'B{i+16}',f'마법 버블 {n}',f'Magic_Spells/magic_light_bubble_{n:02}.wav') for i,n in enumerate((1,2,3,4,5))]
    used=set()
    def collect(node):
        if isinstance(node,dict):
            if 'archiveEntry' in node:used.add(node['archiveEntry'])
            for value in node.values():collect(value)
        elif isinstance(node,list):
            for value in node:collect(value)
    collect(m.load(asset/'source/recipes.json')['audio'])
    records=[]
    with zipfile.ZipFile(args.archive) as z:
        for family,id,label,entry in specs:
            raw=z.read(entry);file=f'{id}-{entry.split("/")[-1]}';(rawdir/file).write_bytes(raw)
            with wave.open(io.BytesIO(raw)) as w:duration=w.getnframes()/w.getframerate()
            records.append(dict(family=family,id=id,label=label,archiveEntry=entry,file=file,duration=duration,sha256=m.sha(raw),byteExact=True,previouslyUsedInMix=entry in used))
    basename='raw-materials' if args.round==1 else 'raw-materials-02'
    m.dump(asset/f'source/{basename}.json',dict(kind='raw-tone-comparison',round=args.round,bombReference='Crazy Arcade water balloon burst; direction only, not sampled',
        processing='ZIP WAV bytes unchanged; player volume only',status='unverified tone materials, not finalized skill sounds',items=records))
    cards={}
    for family in ('smoke','bomb'):
        cards[family]=''.join(f'<article><h3>{r["id"]} · {r["label"]}</h3><p>{r["duration"]:.2f}초 · 구매팩 원음</p><audio controls preload="none" src="../source/raw-originals/{html.escape(r["file"])}"></audio><p class="hint">{"과거 합성에 일부 사용된 재료 · 여기서는 원음 그대로" if r["previouslyUsedInMix"] else "이전 합성에 사용하지 않은 원음"}</p><p class="file">{html.escape(r["archiveEntry"])}</p><button data-id="{r["id"]}" data-family="{family}">이 음색으로 진행</button></article>' for r in records if r['family']==family)
    page='''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>폭탄·연막 원음 비교</title><style>
body{margin:0;background:#142123;color:#f2f0e8;font:16px/1.6 'Malgun Gothic',sans-serif}main{max-width:1100px;margin:auto;padding:30px 22px}h1{margin:0}h2{margin-top:30px}h3{margin:0;font-size:17px}a{color:#a3e1c9}p{margin:6px 0}.hint,.file{color:#b5c4c2;font-size:13px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(275px,1fr));gap:14px}article{background:#213335;border:1px solid #46605d;border-radius:10px;padding:16px}audio{width:100%;margin:10px 0}.file{overflow-wrap:anywhere;font-size:11px}button{padding:9px 14px;background:#a3e1c9;color:#142123;border:0;border-radius:7px;cursor:pointer;font:inherit}button.chosen{background:#f1c281}#selection{padding:15px 0;color:#f1c281}label{display:block;margin:15px 0}input{vertical-align:middle}
</style></head><body><main><h1>폭탄·연막 원음 비교</h1>
<p>폭탄 기준: 크레이지 아케이드의 물풍선 파열. 연막 기준: 약 1초의 ‘푸쉬이이이’.</p>
<p class="hint">아래는 음색 확인용 구매팩 원음입니다. 합성·필터·속도·길이 변경을 하지 않았습니다. 선택은 음색 방향이며 게임 적용 확정과는 구분됩니다.</p>
<label>청취 음량 <input id="volume" type="range" min="0" max="100" value="50"> <span id="volumeValue">50%</span></label>
<h2>연막 · __SMOKE_TITLE__</h2><p class="hint">분출의 시작과 뒤로 빠지는 공기 질감을 비교합니다. 원음 길이를 그대로 두었으며 1초로 늘이거나 자르지 않았습니다.</p><div class="grid">__SMOKE__</div>
<h2>폭탄 · __BOMB_TITLE__</h2><p class="hint">크레이지 아케이드 물풍선 파열 방향의 음색 비교입니다. 아직 직접 청감으로 적합성을 검증한 후보는 아닙니다.</p><div class="grid">__BOMB__</div>
<p id="selection">아직 선택하지 않았습니다.</p><button id="save">음색 선택 파일 저장</button>
<p><a href="index.html">기존 전체 무기 청취 화면</a></p></main><script>
const key='lost-words-raw-sound-direction-v__ROUND__',records=__RECORDS__,players=[...document.querySelectorAll('audio')],buttons=[...document.querySelectorAll('[data-id]')];
let selected={};try{selected=JSON.parse(localStorage.getItem(key)||'{}')}catch(_){}
function update(){document.getElementById('selection').textContent=`음색 방향: 연막 ${selected.smoke||'미정'} / 폭탄 ${selected.bomb||'미정'}`;for(const b of buttons){const active=selected[b.dataset.family]===b.dataset.id;b.classList.toggle('chosen',active);b.setAttribute('aria-pressed',String(active));}}
for(const a of players){a.volume=.5;a.onplay=()=>{for(const b of players)if(a!==b)b.pause();};}
document.getElementById('volume').oninput=e=>{for(const a of players)a.volume=Number(e.target.value)/100;document.getElementById('volumeValue').textContent=e.target.value+'%';};
for(const b of buttons)b.onclick=()=>{selected[b.dataset.family]=b.dataset.id;localStorage.setItem(key,JSON.stringify(selected));update();};
document.getElementById('save').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify({kind:'raw-sound-direction',round:__ROUND__,selected,originals:records.filter(r=>Object.values(selected).includes(r.id)),status:'tone direction only; not applied'},null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='raw-sound-direction-__ROUND__.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};update();
</script></body></html>'''.replace('__SMOKE__',cards['smoke']).replace('__BOMB__',cards['bomb']).replace('__ROUND__',str(args.round)).replace('__RECORDS__',json.dumps(records,ensure_ascii=False).replace('</','<\\/'))
    page=page.replace('__SMOKE_TITLE__','가스 분출' if args.round==1 else '공압 방출 / 분무기 분사').replace('__BOMB_TITLE__','버블 / 물 분출 재료' if args.round==1 else '물 분출 마법 / 마법 버블')
    if args.round==2:page=page.replace('폭탄·연막 원음 비교','폭탄·연막 원음 비교 · 2차')
    (asset/f'preview/{basename}.html').write_text(page,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition',f'bomb/smoke revision 11 rejected; raw tone comparison round {args.round} pending; other confirmations preserved; not applied')
    for r in records:
        assert m.sha((rawdir/r['file']).read_bytes())==r['sha256']
    print(json.dumps(dict(rawFiles=len(records),page=str(asset/f'preview/{basename}.html'),noSoundProcessing=True)))


if __name__=='__main__':main()
