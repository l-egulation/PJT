/* Renderer-independent scene recipe. Coordinates are 1670x942, top-left.
 * Inputs belong to the game. This module does not roll weapons, inks, or balance.
 */
(function (root) {
  'use strict';
  const colors = ['red', 'yellow', 'green', 'blue', 'purple'];
  const clamp = (n, a, b) => Math.max(a, Math.min(b, Number(n) || 0));
  function paletteSlots(count, offset = 0, universal = false) {
    return Array.from({length: count}, (_, i) => ({color: universal && i === 0 ? 'colorless' : colors[(offset + i) % 5], partId: null, locked: false}));
  }
  function buildScene(cat, state = {}) {
    const layers = [], weapons = Object.fromEntries(cat.weapons.map(w => [w.id, w]));
    const skills = Object.fromEntries(cat.skills.map(s => [s.id, s]));
    const parts = Object.fromEntries(cat.inks.map(p => [p.id, p]));
    let group='health';
    const add = o => {
      const coordinate=o.rect?o.rect.slice(0,2).join(','):o.type==='path'?'route':[o.x,o.y].join(',');
      layers.push({...o,id:group+'.'+(o.editorKey??o.type+':'+coordinate),group});
    };
    const sprite = (id, x, y, w, h, extra = {}) => add({type: 'sprite', sprite: id, rect: [x,y,w,h], contain: true, ...extra});
    const text = (value, x, y, size, color = '#fff0da', center = false, extra = {}) => add({type:'text',text:String(value),x,y,size,color,center,...extra});
    const box = (x,y,w,h,color) => add({type:'rect',rect:[x,y,w,h],color});
    const hp = clamp(state.hp ?? 82,0,100);
    sprite('health-plaque',96,61,428,82,{contain:false});
    sprite('health-fill-red',165,112,330,18,{contain:false,fill:hp/100});
    sprite('hero-portrait',29,30,147,118);
    text(Math.round(hp),192,59,42); text('/ 100',263,73,28);
    if (!state.boss) {
      group='currencies';
      sprite('gold-coin',1255,28,47,47); text(state.gold ?? 240,1308,34,34,'#1b1c18');
      sprite('pigment-currency',1390,28,47,47); text(state.pigment ?? 18,1446,34,34,'#1b1c18');
      group='inventory';sprite('button-frame',1510,24,61,61);sprite('bag-glyph',1528,39,26,31);
      // Exact same display aspect as the run palette card, with a thin rim.
      group='minimap';sprite('minimap-frame-compact',1251,98,394,394*201/313,{contain:false,role:'minimap'});
      add({type:'path',points:[[1276,298],[1301,298],[1301,231],[1369,231],[1369,165],[1467,165],[1467,203],[1558,203],[1558,298],[1276,298]],color:'#fff0da',width:3,role:'live-map-example'});
      box(1405,214,18,18,'#00d4d0');
    } else {
      group='boss-health';
      sprite('health-plaque',585,39,500,70,{contain:false});
      text('보스 체력',835,45,22,'#fff0da',true);
      sprite('health-fill-red',635,86,404,14,{contain:false,fill:clamp(state.bossHp ?? 65,0,100)/100});
    }
    group='pause';sprite('button-frame',1583,24,61,61);sprite('pause-glyph',1602,41,23,29);
    const configs = state.weapons ?? [
      {weaponId:'base_sword',skillId:'base_sword_default',paletteSlots:paletteSlots(3,3),status:'ready'},
      {weaponId:'hammer',skillId:'hammer_default',paletteSlots:paletteSlots(3),status:'cooldown',remainingSeconds:3.2,cooldownFraction:.64}
    ];
    configs.slice(0,2).forEach((cfg,i) => {
      group='weapon-'+i;
      const legacy=cat.legacyWeaponSelections?.[cfg.weaponId];
      if(legacy)cfg={...cfg,...legacy};
      const weapon = weapons[cfg.weaponId];
      const requested = skills[cfg.skillId];
      const skill = requested?.weaponId === weapon?.id ? requested : skills[weapon?.specials[0]];
      const slots=(cfg.paletteSlots?.length ? cfg.paletteSlots : paletteSlots(3,i)).slice(0,5);
      while(slots.length<3)slots.push({color:null,partId:null});
      const x=52+i*370, y=774, icon=skill?.presentationIcon ?? weapon?.icon ?? cat.unknownContent.weaponIcon;
      const weaponName=skill?.presentationName ?? weapon?.name ?? '빈 무기';
      sprite('weapon-disc',x,y,142,142);
      sprite(icon,x+29,y+28,84,84,{role:'weapon',weaponId:cfg.weaponId});
      sprite(i===(state.selected ?? 0)?'ring-selected':'ring-idle',x+7,y+7,128,128);
      const mode=weapon?.modes.find(m=>m.id===(cfg.launcherMode ?? 'ball'));
      const basic=mode?.icon ?? weapon?.basicIcon ?? cat.unknownContent.skillIcon;
      sprite('weapon-disc',x+132,808,82,82);sprite(basic,x+148,824,50,50);
      sprite('weapon-disc',x+214,808,82,82);
      const unavailable=!weapon || weapon.specialPolicy==='unavailable';
      const status=unavailable?'unavailable':(cfg.status ?? 'ready');
      const skillIcon=unavailable?cat.unknownContent.skillIcon:(skill?.icon ?? cat.unknownContent.skillIcon);
      sprite(skillIcon,x+227,821,56,56,{role:'skill',status,skillId:skill?.id});
      if(status==='cooldown') {
        add({type:'ellipse',rect:[x+220,814,70,70],color:'#10120fb8'});
        sprite('ring-cooldown',x+214,808,82,82,{radial:clamp(cfg.cooldownFraction ?? .64,0,1),editorKey:'skill-progress'});
        text(Number(cfg.remainingSeconds ?? 3.2).toFixed(1),x+255,830,25,'#fff0da',true);
      }
      if(status==='charging'||status==='charged') {
        const progress=status==='charged'?1:clamp(cfg.chargeProgress ?? .65,0,1);
        sprite(status==='charged'?'ring-selected':'ring-charge',x+214,808,82,82,{radial:progress,editorKey:'skill-progress'});
        if(status==='charged')sprite('state-ready',x+266,857,23,23);
        else text(Math.round(progress*100)+'%',x+255,863,13,'#f7c34c',true);
      }
      const capacity=Math.max(1,Math.round(cfg.chargeCapacity ?? 1));
      const available=clamp(cfg.availableCharges ?? (status==='cooldown'?0:capacity),0,capacity);
      if(capacity>1&&!unavailable) {
        box(x+260,865,44,18,'#1b1c18');text(available+'/'+capacity,x+282,864,13,'#fff0da',true);
        if(capacity<=3)for(let j=0;j<capacity;j++)sprite(j<available?'charge-pip-ready':'charge-pip-empty',x+219+j*13,872,12,12);
      }
      for(const [index,key] of (cfg.keys ?? (i===0?['Q','E']:['W','R'])).entries()){
        sprite('button-frame',x+155+index*82,786,40,33,{contain:false});text(key,x+175+index*82,788,23,'#1b1c18',true);
      }
      const n=slots.length, trayWidth=45+n*42;
      sprite('palette-tray-'+n,x+72,885,trayWidth,43,{contain:false});
      slots.forEach((s,j)=>{
        const part=parts[s.partId];
        const sid=s.locked?'slot-locked':!s.color?'slot-empty':state.showPartIcons&&part?part.partIcon:'drop-'+s.color;
        sprite(sid,x+91+j*42,889,29,35,{role:'ink',partId:s.partId??null,color:s.color??null,editorKey:'ink-'+j});
      });
      if(state.reveal) {
        group='reveal-'+i;
        const cx=516+i*325;
        sprite('run-card',cx,332,313,201,{contain:false});
        sprite('weapon-disc',cx+112,290,90,90);sprite(icon,cx+131,309,52,52);
        text(weaponName,cx+156.5,376,34,'#1b1c18',true);
        sprite('divider',cx+42,423,232,4,{contain:false});
        const dw=n===5?36:n===4?43:49, dh=dw*64/49, gap=n===5?49:n===4?60:70;
        slots.forEach((s,j)=>sprite(s.locked?'slot-locked':s.color?'drop-'+s.color:'slot-empty',cx+156.5+(j-(n-1)/2)*gap-dw/2,444,dw,dh,{role:'reveal-color',editorKey:'ink-'+j}));
        sprite('corner-accent',cx+(i?271:10),491,31,31,{flip:i===1});
      }
    });
    if(state.reveal) {
      group='reveal-heading';
      sprite('heading-plaque',691,248,289,67,{contain:false});text('출발 팔레트',835,254,35,'#fff0da',true);
      sprite('chevron',720,271,9,15);sprite('chevron',940,271,9,15,{flip:true});
    }
    return {width:1670,height:942,layers};
  }
  const api={buildScene,paletteSlots};
  if(typeof module!=='undefined'&&module.exports)module.exports=api;
  else root.DeviantUI=api;
})(typeof globalThis!=='undefined'?globalThis:this);
