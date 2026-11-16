(function(root){
  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  function shade(r,g,b,grade){
    const l=.2126*r+.7152*g+.0722*b,s=grade.saturation??1,c=grade.contrast??1,lift=grade.lift||[0,0,0];
    return [r,g,b].map((v,i)=>Math.round(clamp(((l+(v-l)*s)/255-.5)*c+.5+lift[i],0,1)*255));
  }
  // A display material: source PNGs, alpha, tile boundaries and collisions stay intact.
  function gradeImage(img,grade){
    if(!grade)return img;const canvas=document.createElement('canvas');canvas.width=img.naturalWidth||img.width;canvas.height=img.naturalHeight||img.height;
    const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(img,0,0);const pixels=ctx.getImageData(0,0,canvas.width,canvas.height),d=pixels.data,cache=new Map();
    for(let i=0;i<d.length;i+=4){if(!d[i+3])continue;const key=(d[i]<<16)|(d[i+1]<<8)|d[i+2];let color=cache.get(key);if(!color){color=shade(d[i],d[i+1],d[i+2],grade);cache.set(key,color);}d[i]=color[0];d[i+1]=color[1];d[i+2]=color[2];}
    ctx.putImageData(pixels,0,0);return canvas;
  }
  function followCamera(state,hero,dt,view,config={}){
    const foot=view.foot,up=config.airDeadzoneUp??84,down=config.airDeadzoneDown??52;
    let targetY=hero.y-foot;
    if(!hero.grounded){const screenY=hero.y-state.cameraY;targetY=screenY<foot-up?hero.y-foot+up:screenY>foot+down?hero.y-foot-down:state.cameraY;}
    const normal=config.verticalOmega??7,fall=clamp((hero.vy-200)/300,0,1),verticalOmega=normal+(12-normal)*fall;
    for(const [pos,vel,target,min,max,omega] of [
      ['camera','cameraVX',hero.x-view.width*.45,0,view.worldWidth-view.width,config.horizontalOmega??12.5],
      ['cameraY','cameraVY',targetY,-384,view.worldHeight-view.height,verticalOmega]
    ]){
      const goal=clamp(target,min,max),offset=state[pos]-goal,j=state[vel]+omega*offset,e=Math.exp(-omega*dt);
      state[pos]=goal+(offset+j*dt)*e;state[vel]=(state[vel]-omega*j*dt)*e;
      if(state[pos]<min||state[pos]>max){state[pos]=clamp(state[pos],min,max);state[vel]=0;}
    }
  }
  root.FOREST_PRESENTATION={shade,gradeImage,followCamera};
})(typeof window==='undefined'?globalThis:window);
