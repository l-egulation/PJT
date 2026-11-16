// Procedural review motion. Source identity unchanged; not a hand-drawn animation sheet.
function idlePose(stage,time,enabled=true){
 const cycle=stage===0?1.1:1.5;
 const sampled=Math.floor(time*10)/10;
 const phase=(sampled%cycle)/cycle;
 const lift=enabled?Math.round((1-Math.cos(phase*Math.PI*2))*(stage===0?2:1.5)):0;
 return {lift,feet:20};
}
function drawIdleNpc(ctx,image,b,l,stage,time,enabled){
 const p=idlePose(stage,time,enabled),upper=l.height-p.feet;
 ctx.imageSmoothingEnabled=false;
 // Lower twenty rows are not translated or stretched, keeping both feet anchored.
 ctx.drawImage(image,b[0],b[1],l.width,upper,l.x,l.y-p.lift,l.width,upper+p.lift);
 ctx.drawImage(image,b[0],b[1]+upper,l.width,p.feet,l.x,l.y+upper,l.width,p.feet);
}
