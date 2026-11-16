// Schematic fixture follows Chapter1ProceduralMap's real room coordinates and graph.
// It is not a live run. A future bridge supplies its ForestData snapshot through applyMapSnapshot.
const fixtureRooms=Array.from({length:9},(_,i)=>({id:i,x:12+i*58,y:60,w:i===8?30:32,h:18,kind:i===0?'start':i===8?'boss':'combat'}));
const fixturePassages=Array.from({length:8},(_,i)=>({from:i,to:i+1}));
[2,4,6].forEach((slot,i)=>{let a=9+i*2,y=i===1?108:12;fixtureRooms.push({id:a,x:12+slot*58,y,w:32,h:18,kind:'combat'},{id:a+1,x:12+(slot+1)*58,y,w:32,h:18,kind:['shop','maintenance','elite'][i]});fixturePassages.push({from:slot-1,to:a},{from:a,to:a+1});if(i<2)fixturePassages.push({from:a+1,to:slot+2})});
let mapSnapshot={cols:520,rows:156,rooms:fixtureRooms,passages:fixturePassages,visited:[0],current:0};
function applyMapSnapshot(snapshot){
 if(!snapshot||!Array.isArray(snapshot.rooms)||!snapshot.rooms.length||!Array.isArray(snapshot.passages)||!Array.isArray(snapshot.visited)||!snapshot.rooms.some(r=>r.id===snapshot.current))throw Error('Invalid map snapshot');
 const ids=new Set(snapshot.rooms.map(r=>r.id));
 if(snapshot.rooms.some(r=>![r.x,r.y,r.w,r.h].every(Number.isFinite)||r.w<=0||r.h<=0)||snapshot.visited.some(id=>!ids.has(id))||snapshot.passages.some(p=>!ids.has(p.from)||!ids.has(p.to)))throw Error('Invalid room geometry');
 mapSnapshot=structuredClone(snapshot);if(!mapSnapshot.visited.includes(mapSnapshot.current))mapSnapshot.visited.push(mapSnapshot.current);render();updateMapControls();
}
function enterPreviewRoom(id){const target=mapSnapshot.rooms.find(r=>r.id===id);if(!target||!mapSnapshot.passages.some(p=>p.from===mapSnapshot.current&&p.to===id||p.to===mapSnapshot.current&&p.from===id))return false;mapSnapshot.current=id;if(!mapSnapshot.visited.includes(id))mapSnapshot.visited.push(id);render();updateMapControls();return true}
function mapDrawing(mini=false){
 const s=mapSnapshot,v=new Set(s.visited),current=s.rooms.find(r=>r.id===s.current),visible=s.rooms.filter(r=>v.has(r.id));
 const passage=s.passages.filter(p=>v.has(p.from)||v.has(p.to)).map(p=>{const a=s.rooms.find(r=>r.id===p.from),b=s.rooms.find(r=>r.id===p.to);if(p.spaces)return p.spaces.map(t=>`<rect x="${t.x}" y="${t.y}" width="${t.w}" height="${t.h}"/>`).join('');const x=(a.x+a.w+b.x)/2;return `<path d="M${a.x+a.w} ${a.y+15}H${x}V${b.y+15}H${b.x}"/>`}).join('');
 const marker=(r)=>{const x=r.x+r.w/2,y=r.y+r.h/2;if(['maintenance','shop'].includes(r.kind))return `<g data-marker="maintenance" transform="translate(${x-5} ${y-5}) scale(.42)">${uiSvg('maintenance').replace(/<\/?svg[^>]*>/g,'')}</g>`;if(r.kind==='boss')return `<g data-marker="boss" transform="translate(${x-5} ${y-5}) scale(.42)">${uiSvg('boss').replace(/<\/?svg[^>]*>/g,'')}</g>`;return ''};
 const box=mini?`${Math.max(0,current.x-46)} ${Math.max(0,current.y-30)} 140 80`:`0 0 ${s.cols} ${s.rows}`;
 return `<svg class="explored-map" viewBox="${box}" role="img" aria-label="방문한 구역 지도"><g class="map-passages">${passage}</g><g class="map-rooms">${visible.map(r=>`<rect data-room="${r.id}" x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}"/>`).join('')}</g><g class="map-icons" fill="none" stroke="currentColor" stroke-width="2">${visible.map(marker).join('')}</g><path class="player-pin" d="M${current.x+current.w/2} ${current.y+current.h/2-4}l4 7h-8Z"/></svg>`;
}
map=function(){return page('지도',`<div class="map-layout"><div class="map-wrap"><div class="map-content" style="transform:translate(${state.pan[0]}px,${state.pan[1]}px) scale(${state.zoom})">${mapDrawing()}</div></div><div class="map-aside"><div class="map-legend"><span>▲ 현재 위치</span><span>${uiSvg('maintenance')}정비소</span><span>${uiSvg('boss')}보스</span></div>${action('+','zoom:1')}${action('−','zoom:-1')}${action('현재 위치','mapCenter')}${action('전체 보기','mapAll')}</div></div>`)};
const mapAwareScreen=renderScreen;
renderScreen=function(){let html=mapAwareScreen();if(['combat','toast'].includes(state.screen))html=html.replace(/<div class="minimap-drawing">[\s\S]*?<\/div>/,`<div class="minimap-drawing">${mapDrawing(true)}</div>`);return html};
const controls=document.createElement('label');controls.className='map-preview-controls';controls.innerHTML='방 이동 재현 <select id="previewRoom"></select>';document.querySelector('.toolbar').append(controls);
function updateMapControls(){const p=document.querySelector('#previewRoom');p.innerHTML=mapSnapshot.rooms.map(r=>{const connected=r.id===mapSnapshot.current||mapSnapshot.passages.some(e=>e.from===mapSnapshot.current&&e.to===r.id||e.to===mapSnapshot.current&&e.from===r.id);return `<option value="${r.id}" ${r.id===mapSnapshot.current?'selected':''} ${connected?'':'disabled'}>${r.id+1} ${({start:'입구',combat:'전투방',shop:'정비소',maintenance:'정비소',elite:'정예방',boss:'보스방'})[r.kind]||''}</option>`}).join('')}
controls.addEventListener('change',e=>enterPreviewRoom(Number(e.target.value)));
document.addEventListener('click',e=>{const cmd=e.target.closest('[data-action]')?.dataset.action;if(cmd==='mapCenter'){state.pan=[0,0];state.zoom=1;render()}if(e.target.closest('#reset')){mapSnapshot={cols:520,rows:156,rooms:structuredClone(fixtureRooms),passages:structuredClone(fixturePassages),visited:[0],current:0};updateMapControls();render()}});
updateMapControls();render();
