const merchantLines=[[
 '오, 너는 누구니? 못 보던 얼굴인데.',
 '아… 너도 저 스케치 사건에 휘말렸구나?',
 '그런데 몸이… 잉크잖니?',
 '말하기 싫으면 안 해도 돼. 일단 필요한 것부터 챙기자.',
 '여기선 잉크를 챙기고, 팔레트와 장비를 정비할 수 있어.',
 '강화도 할 수 있단다. 하나씩 살펴보렴.',
 '야, 너! 잠깐만.',
 '돌아오는 길도 만들 수 있으면서, 그냥 가려고?',
 '혹시 모르니 여기 하나 남겨 둬. C를 누르면 포탈을 설치할 수 있어.',
 '돌아올 땐 V야. 멀리 갔다 와도 여기로 돌아올 수 있단다.'
],[ '뭐야, 넌 누구냐?', '…정비하러 왔냐. 쓰는 법은 알지?', '그럼 빨리 필요한 거 챙겨. 나 쉬게.' ]];
let conversation={mode:'off',index:0,start:0,shown:false,stage:0};
function startConversation(){original=false;conversation={mode:'approach',index:0,start:t,shown:false,stage:viewIndex};}
function nextDialogue(){
 const c=conversation;if(c.stage!==viewIndex)return;
 if(c.mode==='service'){if(viewIndex===0){c.mode='talk';c.index=6;c.start=t;c.shown=false}else c.mode='done';return;}
 if(c.mode!=='talk')return;
 const text=merchantLines[viewIndex][c.index];
 if(!c.shown&&(t-c.start)*30<text.length){c.shown=true;return;}
 if(viewIndex===0&&c.index===5||viewIndex===1&&c.index===2){c.mode='service';return;}
 c.index++;c.start=t;c.shown=false;
 if(c.index>=merchantLines[viewIndex].length)c.mode='done';
}
function wrap(text,max,font){ctx.font=font;const out=[];let line='';for(const word of text.split(' ')){const next=line?line+' '+word:word;if(ctx.measureText(next).width<=max){line=next;continue;}if(line)out.push(line);line='';for(const ch of word){if(ctx.measureText(line+ch).width>max&&line){out.push(line);line=ch}else line+=ch}}if(line)out.push(line);return out;}
function speakerAnchor(){const l=data.views[viewIndex].layers.find(l=>l.id==='npc');return [l.x+l.width*.43,l.y-idlePose(viewIndex,t,idle).lift];}
function drawMerchantBubble(text){
 const c=conversation,font='12px GalmuriBold',lines=wrap(text,184,font),pad=9,lh=15;
 ctx.font=font;const w=Math.ceil(Math.max(...lines.map(l=>ctx.measureText(l).width)))+20,h=lines.length*lh+18;
 const [hx,hy]=speakerAnchor(),x=Math.round(Math.max(8,Math.min(952-w,hx-w/2))),y=Math.round(Math.max(32+6+15,hy-h-10));
 const dark=viewIndex===0,fill=dark?'#181615':'#fbf9f2',line=dark?'#f0ede4':'#1f1b19';
 ctx.fillStyle=line;ctx.fillRect(x+2,y,w-4,h);ctx.fillRect(x,y+2,w,h-4);ctx.fillRect(x+1,y+1,w-2,h-2);
 ctx.fillStyle=fill;ctx.fillRect(x+3,y+2,w-6,h-4);ctx.fillRect(x+2,y+3,w-4,h-6);
 const cx=Math.round(Math.max(x+12,Math.min(x+w-12,hx))),ty=y+h-2;
 for(let r=0;r<7;r++){const rw=14-2*r;ctx.fillStyle=line;ctx.fillRect(cx-rw/2,ty+r,rw,1)}
 for(let r=0;r<5;r++){const rw=10-2*r;ctx.fillStyle=fill;ctx.fillRect(cx-rw/2,ty+r,rw,1)}
 const name=viewIndex===0?'마을 장인':'수로 정비공',nw=Math.ceil(ctx.measureText(name).width)+12;
 ctx.fillStyle=line;ctx.fillRect(x+6,y-14,nw,16);ctx.fillRect(x+5,y-13,nw+2,14);
 ctx.fillStyle=fill;ctx.textAlign='left';ctx.textBaseline='top';ctx.fillText(name,x+12,y-12);
 const chars=c.shown?text.length:Math.min(text.length,Math.floor((t-c.start)*30));
 // Reveal using original text offsets, including whitespace consumed by wrapping.
 let offset=0;ctx.fillStyle=line;for(let i=0;i<lines.length;i++){
  const l=lines[i],pos=text.indexOf(l,offset);offset=pos+l.length;ctx.fillText(l.slice(0,Math.max(0,chars-pos)),x+pad+1,y+6+i*lh);
 }
 if(chars>=text.length&&(t%.8)<.4)for(let r=0;r<4;r++)ctx.fillRect(x+w-16+r,y+h-10+r,8-2*r,1);
}
function dialoguePlayerX(l){const c=conversation;if(c.stage!==viewIndex||c.mode==='off')return l.x;const d=viewIndex===0?66:-8;const f=c.mode==='approach'?Math.min(1,(t-c.start)/1.1):1;return Math.round(l.x+d*f);}
function drawConversation(){
 const c=conversation;if(c.stage!==viewIndex||original||c.mode==='off')return;
 ctx.save();ctx.imageSmoothingEnabled=false;
 if(c.mode==='approach'&&t-c.start>=1.1){c.mode='talk';c.start=t;}
 if(c.mode==='talk'||c.mode==='approach'){
  const amount=c.mode==='approach'?Math.min(1,(t-c.start)/.3):1;ctx.fillStyle='#090b0c';ctx.fillRect(0,0,960,Math.round(32*amount));ctx.fillRect(0,540-Math.round(32*amount),960,Math.round(32*amount));
  if(c.mode==='talk')drawMerchantBubble(merchantLines[viewIndex][c.index]);
 }else if(c.mode==='service'){
  ctx.fillStyle='rgba(10,16,18,.9)';ctx.fillRect(255,155,450,205);ctx.fillStyle='#f0ede4';ctx.font='12px GalmuriBold';ctx.textAlign='left';ctx.textBaseline='top';
  ['정비 기능 소개','잉크·팔레트  ·  파츠 장착과 색 변경','성장  ·  캐릭터와 팔레트 슬롯 강화','수급·장비  ·  잉크 뽑기와 장비 정비','Space / F / Enter · 닫고 대화 계속'].forEach((s,i)=>ctx.fillText(s,277,177+i*33));
 }else if(c.mode==='done'){
  ctx.font='12px GalmuriBold';ctx.fillStyle='#f0ede4';ctx.fillStyle=viewIndex===0?'#1f1b19':'#f0ede4';ctx.fillText(viewIndex===0?'C · 포탈 설치     V · 귀환':'F · 정비소 이용',28,485);
 }
 ctx.restore();
 document.getElementById('dialogue-status').textContent=c.mode==='service'?'정비 화면 연결 위치 · 기능 소개용 화면':c.mode==='done'?'첫 만남 종료 · 다시 보려면 첫 방문 재생':c.mode==='approach'?'정비소에 접근 중':'Space · F · Enter: 글자 완성 / 다음 대사';
}
document.getElementById('intro').onclick=startConversation;
document.getElementById('next-line').onclick=nextDialogue;
document.getElementById('skip-intro').onclick=()=>{conversation.mode='done'};
window.addEventListener('keydown',e=>{if(!['Space','KeyF','Enter'].includes(e.code)||e.repeat||e.target.closest('input,select,textarea'))return;e.preventDefault();nextDialogue()});
