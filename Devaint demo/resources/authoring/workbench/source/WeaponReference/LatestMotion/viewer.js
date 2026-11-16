
(() => {
  const root=document.getElementById('ink-four-motion-review');
  const DATA=window.INK_MOTION_DATA;
  const get=name=>root.querySelector('[data-control="'+name+'"]');
  const names={idle:'대기',walk:'걷기',run:'달리기',jump:'점프',fall:'낙하·착지',dash:'대시',combo:'공격 연속',death:'죽음'};
  const order=Object.keys(names),imgs={},cards={},sequences={};
  const groups={jump:['jump_start','jump_rise','jump_apex','fall_start','fall_loop','land'],fall:['fall_start','fall_loop','land'],dash:['dash_start','dash_active','dash_end'],death:['death_start','death_fall','death_hold']};
  let motion='idle',time=0,automatic=true,playing=true,last=null,ready=false,theme={},error=false;
  function sequence(w,name){
    const modules=Object.fromEntries(w.modules.map(m=>[m.id,m]));
    const ids=name==='combo'?Object.keys(modules).filter(id=>id.startsWith('attack')).sort():groups[name]||[name];
    const steps=[],effects=[];let start=0;
    for(const id of ids){
      const mod=modules[id];if(!mod)continue;
      const hit=id.startsWith('attack')?Number(id.slice(6)):0,attack=w.attacks[String(hit)];
      const limit=name==='combo'&&id!==ids.at(-1)?attack?.chainAt??Infinity:Infinity;let used=0;
      if(hit&&attack)effects.push({hit,start:start+attack.fxStart,kind:attack.effect||(hit===1?'up':'down')});
      for(const step of mod.steps){if(used>=limit)break;const ms=Math.min(step.ms,limit-used);steps.push({...step,ms});used+=ms;}
      start+=used;
    }
    return {steps,effects,duration:start};
  }
  for(const[k,w]of Object.entries(DATA.weapons)){
    sequences[k]=Object.fromEntries(order.map(id=>[id,sequence(w,id)]));
    const figure=document.createElement('figure'),caption=document.createElement('figcaption'),name=document.createElement('span'),frame=document.createElement('span'),canvas=document.createElement('canvas');
    name.textContent=w.name;frame.className='ink-frame text-small';canvas.width=DATA.viewport[0]*2;canvas.height=DATA.viewport[1]*2;
    canvas.setAttribute('role','img');canvas.setAttribute('aria-label',w.name+' 전체 모션 재생');caption.append(name,frame);figure.append(caption,canvas);get('weapons').append(figure);
    cards[k]={canvas,frame,g:canvas.getContext('2d')};
  }
  function selectFrame(w,seq){
    let t=time;
    if(['idle','walk','run'].includes(motion))t%=seq.duration;
    else if(!automatic&&motion!=='death')t%=seq.duration+600;
    if(!['idle','walk','run'].includes(motion)&&t>=seq.duration){
      if(motion==='death')return {...seq.steps.at(-1),local:seq.duration-1};
      return {frame:w.modules.find(m=>m.id==='idle').steps[0].frame,local:t,phase:'대기 복귀'};
    }
    const local=t;for(const step of seq.steps){if(t<step.ms)return {...step,local};t-=step.ms;}
    return {...seq.steps.at(-1),local};
  }
  function refreshTheme(){const s=getComputedStyle(root);theme.line=s.getPropertyValue('--border').trim();}
  function draw(){
    if(!ready||error)return;
    for(const[k,w]of Object.entries(DATA.weapons)){
      const c=cards[k],g=c.g,seq=sequences[k][motion],s=selectFrame(w,seq),image=imgs[k+':'+s.frame];
      g.clearRect(0,0,c.canvas.width,c.canvas.height);g.imageSmoothingEnabled=false;
      if(theme.line){g.fillStyle=theme.line;g.fillRect(0,DATA.ground[1]*2,c.canvas.width,1);}
      const p=w.framePivots?.[s.frame]||w.pivot,o=w.frameOffsets?.[s.frame]||[0,0];
      g.drawImage(image,Math.round((DATA.ground[0]-p[0]+o[0]+(s.dx||0))*2),Math.round((DATA.ground[1]-p[1]+o[1]+(s.dy||0))*2),image.width*2,image.height*2);
      if(get('effects').checked)for(const e of seq.effects){
        let age=s.local-e.start;if(age<0)continue;
        const timing=w.effectTiming?.[e.kind]||w.effectMs,frames=w.effects[e.kind];if(!frames)continue;let n=0;
        while(n<frames.length&&age>=timing[n])age-=timing[n++];if(n>=frames.length)continue;
        const fx=imgs[k+':fx:'+e.kind+':'+n],fp=w.fxPivot||w.pivot;g.save();
        if(w.alignedFx){const cfg=w.alignedFx[e.hit-1],off=cfg.offsets[n];g.translate((DATA.ground[0]-fp[0]+off[0])*2,(DATA.ground[1]-fp[1]+off[1])*2);if(cfg.flip_x){g.translate(256,0);g.scale(-1,1);}g.drawImage(fx,0,0,256,256);}
        else{const off=w.fxOffset||[0,0];g.drawImage(fx,(DATA.ground[0]-fp[0]+off[0])*2,(DATA.ground[1]-fp[1]+off[1])*2,fx.width*2,fx.height*2);}g.restore();
      }
      const label=motion==='death'&&time>=seq.duration?'사망 유지':s.phase||s.frame;
      if(c.frame.textContent!==label)c.frame.textContent=label;
    }
  }
  function stageDuration(){const longest=Math.max(...Object.values(sequences).map(s=>s[motion].duration));return ['idle','walk','run'].includes(motion)?longest*2:longest+600;}
  function setStage(next){motion=next;time=0;get('stage').textContent=names[next];}
  function save(){if(window.openai?.setWidgetState)window.openai.setWidgetState({modelContent:{motion:automatic?'전체 순서 재생':names[motion],speed:Number(get('speed').value),effects:get('effects').checked},privateContent:{motion,automatic,playing,speed:get('speed').value,effects:get('effects').checked}}).catch(()=>{});}
  function restore(state){const s=state?.privateContent;if(!s)return;if(names[s.motion])motion=s.motion;automatic=s.automatic!==false;playing=s.playing!==false;if(['1','0.5','0.25'].includes(s.speed))get('speed').value=s.speed;get('effects').checked=s.effects!==false;get('motion').value=automatic?'auto':motion;get('stage').textContent=names[motion];get('play').textContent=playing?'일시정지':'재생';time=0;}
  restore(window.openai?.widgetState);
  window.addEventListener('openai:set_globals',e=>{restore(e.detail?.globals?.widgetState);draw();});
  get('motion').onchange=()=>{automatic=get('motion').value==='auto';setStage(automatic?'idle':get('motion').value);playing=true;get('play').textContent='일시정지';draw();save();};
  get('play').onclick=()=>{playing=!playing;get('play').textContent=playing?'일시정지':'재생';save();};
  get('speed').onchange=save;get('effects').onchange=()=>{draw();save();};
  const tasks=[];for(const[k,w]of Object.entries(DATA.weapons)){
    for(const[id,src]of Object.entries(w.frames))tasks.push([k+':'+id,src]);
    for(const[kind,frames]of Object.entries(w.effects))frames.forEach((src,n)=>tasks.push([k+':fx:'+kind+':'+n,src]));
  }
  Promise.all(tasks.map(([key,src])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{imgs[key]=im;resolve();};im.onerror=()=>reject(Error(key));im.src=src;}))).then(()=>{
    ready=true;refreshTheme();get('status').textContent='대기 640ms · 죽음 12포즈 / 800ms · 네 무기 공통 시간';draw();
    function tick(now){const dt=last===null?0:Math.min(now-last,100);last=now;
      if(playing){time+=dt*Number(get('speed').value);if(automatic&&time>=stageDuration()){
        if(motion==='death'){automatic=false;get('motion').value='death';}
        else{const carry=time-stageDuration();setStage(order[order.indexOf(motion)+1]);time=carry;}
      }}draw();requestAnimationFrame(tick);
    }requestAnimationFrame(tick);
  }).catch(e=>{error=true;get('status').setAttribute('role','alert');get('status').textContent='이미지를 불러오지 못했습니다: '+e.message;});
  new MutationObserver(refreshTheme).observe(document.documentElement,{attributes:true,attributeFilter:['class','style','data-theme']});
})();
