// Decorative art is separate from every interactive control and every label.
const baseRenderScreen=renderScreen;
renderScreen=function(){
  if(state.screen==='pause')return page('일시정지',`<div class="pause-layout"><img class="pause-vignette" src="assets/refined/pause-vignette.png" alt="땅에 꽂힌 검"><div class="stack">${action('계속하기','go:combat','primary')}${action('조작 안내','go:guide')}${action('타이틀로','go:title')}</div></div>`,'modal-small');
  if(state.screen==='title')return `<div class="title-screen"><img class="title-logo" src="assets/refined/title-logo.png" alt="DEVIANT"><div class="title-menu">${action('시작하기','go:hub','primary')}${action('종료','exit')}</div></div>`;
  return baseRenderScreen();
};
const baseHud=hud;
hud=function(){if(state.screen==='title')return '';return baseHud()};
map=function(){return page('지도',`<div class="map-layout"><div class="map-wrap"><div class="map-content" style="transform:translate(${state.pan[0]}px,${state.pan[1]}px) scale(${state.zoom})">${[[70,70,140,0],[210,70,140,0],[350,70,100,90],[350,170,140,0],[490,170,100,-90],[70,70,100,90]].map(([x,y,w,a])=>`<div class="corridor" style="left:${x}px;top:${y}px;width:${w}px;transform:rotate(${a}deg)"></div>`).join('')}${[[30,50,'입구'],[170,50,'● 현재 위치'],[310,50,'정비소'],[310,150,''],[450,150,'◆ 보스'],[30,150,'']].map(([x,y,n],i)=>`<div class="room ${i===1?'current':''}" style="left:${x}px;top:${y}px">${n}</div>`).join('')}</div></div><div class="map-aside"><div class="map-legend">● 현재 위치<br>□ 탐험한 구역<br>◆ 보스<br>⌂ 정비소</div><div class="row">${action('−','zoom:-1')}${action('+','zoom:1')}</div>${action('현재 위치','mapCenter')}${action('전체 보기','mapAll')}</div></div>`)};
$('#assetGrid').innerHTML=[...partData.map(p=>['refined/parts/'+p.id,p.name]),...['paper','spine','title','primary','secondary','disabled','tab-active','tab','card','slot','slot-active','pause-vignette','title-logo','coin','heart','bag','slots','time','kills','pigment','sword','hammer','launcher'].map(n=>['refined/'+n,n]),...['map','stamp'].map(n=>['density/'+n,n]),...['death','clear'].map(n=>['drawn/'+n,n])].map(([f,n])=>`<figure><img src="assets/${f}.png" alt="${n}"><figcaption>${n}</figcaption></figure>`).join('');
const requestedScreen=location.hash.slice(1);
if(screens.some(s=>s[0]===requestedScreen))state.screen=requestedScreen;
render();
