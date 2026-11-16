// Existing Unity terrain artwork is tiled at its 32 PPU scale. No new backdrop
// image replaces the village: opaque, non-colliding soil masses obscure it.
export const passageSpec={floorY:615,pixelsPerUnit:1280/30,worldWidth:4200,cameraTravel:2920,
 sections:[{from:0,to:1100,height:6},{from:1100,to:2100,height:5.75},{from:2100,to:4200,height:5.5}],
 rearWallStart:900,fullyCoveredFrom:2560,firstMechanism:2120};
export function ceilingY(x){const s=passageSpec.sections.find(s=>x<s.to)||passageSpec.sections.at(-1);return passageSpec.floorY-s.height*passageSpec.pixelsPerUnit;}
export function createPassage(ctx,images,{previousColors=false}={}){
 const {floorY:F,worldWidth:W,pixelsPerUnit:U}=passageSpec,S=U/32;
 const make=(w=W,h=720)=>{const c=document.createElement('canvas');c.width=w;c.height=h;c.getContext('2d').imageSmoothingEnabled=false;return c;};
 const earth=make(),e=earth.getContext('2d'),ceiling=make(),c=ceiling.getContext('2d');
 const hatch=g=>{const p=g.createPattern(images['terrain-body'],'repeat');p.setTransform(new DOMMatrix().scale(S));return p;};
 function fillShape(g,pts,fade=.55){g.save();g.beginPath();pts.forEach(([x,y],i)=>i?g.lineTo(x,y):g.moveTo(x,y));g.closePath();g.clip();g.fillStyle=hatch(g);g.fillRect(0,0,W,720);g.globalAlpha=fade;g.fillStyle='#f8eddc';g.fillRect(0,0,W,720);g.restore();}
 function lowerEdge(g,x1,x2,y,alpha=1){g.save();g.globalAlpha=alpha;g.beginPath();g.rect(x1,y-28*S,x2-x1,40*S);g.clip();for(let x=x1;x<x2;x+=256*S)g.drawImage(images['terrain-underside'],x-4*S,y-18.405*S,264*S,26*S);g.restore();}
 function sideEdge(g,x,y1,y2,side='left',alpha=.45){const im=images['terrain-wall-'+side],anchor=side==='left'?7.418:29.634;g.save();g.globalAlpha=alpha;g.beginPath();g.rect(x-15*S,y1,45*S,y2-y1);g.clip();for(let y=y1;y<y2;y+=128*S)g.drawImage(im,x-anchor*S,y-4*S,im.width*S,im.height*S);g.restore();}
 const masses=[
  {pts:[[900,0],[1330,0],[1330,F+8],[1150,F+8],[1150,445],[900,445]],edges:[[900,1150,445]],sides:[[1150,445,F,'left'],[1330,359,F,'right']]},
  {pts:[[1400,0],[1890,0],[1890,F+8],[1640,F+8],[1640,460],[1400,460]],edges:[[1400,1640,460]],sides:[[1640,460,F,'left'],[1890,371,F,'right']]},
  {pts:[[1950,0],[2280,0],[2280,F+8],[2040,F+8],[2040,425],[1950,425]],edges:[[1950,2040,425]],sides:[[2040,425,F,'left'],[2280,380,F,'right']]},
  {pts:[[2250,0],[W,0],[W,F+8],[2560,F+8],[2560,548],[2500,548],[2500,470],[2410,470],[2410,420],[2250,420]],edges:[[2250,2410,420],[2410,2500,470],[2500,2560,548]],sides:[[2560,548,F,'left'],[2500,470,548,'left'],[2410,420,470,'left']]}
 ];
 for(const a of masses){fillShape(e,a.pts);for(const [x1,x2,y]of a.edges)lowerEdge(e,x1,x2,y,.38);for(const [x,y1,y2,side]of a.sides)sideEdge(e,x,y1,y2,side);}
 // A depth tint changes only the opaque soil pixels; it cannot fade out the
 // distant village. The openings stay transparent until physically covered.
 e.save();e.globalCompositeOperation='source-atop';const shade=e.createLinearGradient(900,0,3500,0);shade.addColorStop(0,'rgba(80,73,57,0.04)');shade.addColorStop(.45,'rgba(80,73,57,0.16)');shade.addColorStop(1,'rgba(53,51,43,0.52)');e.fillStyle=shade;e.fillRect(0,0,W,720);e.restore();
 const outline=[[0,0],[W,0],[W,ceilingY(W)],[2100,ceilingY(2100)],[2100,ceilingY(2099)],[1100,ceilingY(1100)],[1100,ceilingY(1099)],[0,ceilingY(0)]];
 fillShape(c,outline,.08);
 for(const s of passageSpec.sections)lowerEdge(c,s.from,s.to,ceilingY(s.from));
 sideEdge(c,W-7,0,ceilingY(W),'right',.7);
 // Fill tiny anti-aliased strip below contour anchors with original hatch, so
 // the opaque playable ceiling never opens a >6-unit gap.
 // Uneven surviving masonry is embedded in the existing soil rather than
 // repeated freestanding prop stations. Taller patches continue behind ceiling.
 const props=[
  {kind:'wall',x:1140,y:409,h:234},{kind:'wall',x:1660,y:364,h:276,flip:true},
  {kind:'buttress',x:2110,y:355,h:260},
  {kind:'wall',x:2500,y:342,h:298},
  {kind:'wall',x:2780,y:298,h:338,flip:true},
  {kind:'wall',x:3120,y:308,h:332},
  {kind:'alcove',x:3450,y:336,h:284},
  {kind:'wall',x:3750,y:308,h:330,flip:true},
  {kind:'buttress',x:4005,y:341,h:274}
 ];
 const wall=make(),wg=wall.getContext('2d');
 // The source artwork already shares the village's warm paper palette.
 // Keep full light at the open entrance; only shade after entering the tunnel.
 function localLight(x){return previousColors?Math.max(.71,.90-(x-1000)/12000):Math.max(.80,1-Math.max(0,x-1800)/10000);}
 function patch(g,name,x,y,h,flip=false,opacity=.92){
  const im=images['aged-'+name+'-source'],w=im.width*h/im.height;
  g.save();g.translate(Math.round(x+(flip?w:0)),Math.round(y));if(flip)g.scale(-1,1);
  g.filter=`brightness(${localLight(x)})${previousColors?' saturate(0.48)':''}`;g.globalAlpha=opacity;
  g.imageSmoothingEnabled=true;g.imageSmoothingQuality='high';g.drawImage(im,0,0,w,h);g.restore();
 }
 for(const a of props)patch(wg,a.kind,a.x,a.y,a.h,a.flip);
 // A single functional hoist beside the maintenance recess, not a repeating motif.
 patch(wg,'pulley',3820,397,204,false,.82);
 // Broken masonry keeps its own silhouette across the soil openings.
 // Clipping it to the rectangular soil masses would slice off wall edges.
 function turningGear(name,worldX,cam,y,size,angle){const im=images['aged-'+name+'-source'];ctx.save();ctx.translate(Math.round(worldX-cam),Math.round(y));ctx.rotate(angle);ctx.filter=`brightness(${localLight(worldX)*.94})${previousColors?' saturate(0.48)':''}`;ctx.globalAlpha=.88;ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';ctx.drawImage(im,-size/2,-size/2,size,size);ctx.restore();}
 function background(cam){ctx.fillStyle='#f5ebd8';ctx.fillRect(0,0,1280,720);ctx.save();ctx.globalAlpha=.46;let im=images.village_tall_native,w=im.width*S;for(let x=-(cam*.25)%w-w;x<1280;x+=w)ctx.drawImage(im,Math.round(x),F-im.height*S,w,im.height*S);ctx.globalAlpha=.78;im=images.village_mid_native;w=im.width*S;for(let x=-(cam*.52)%w-w;x<1280;x+=w)ctx.drawImage(im,Math.round(x),F-im.height*S,w,im.height*S);ctx.restore();}
 function devices(cam,t){ctx.save();ctx.beginPath();ctx.rect(0,0,1280,F);ctx.clip();
  ctx.drawImage(wall,-cam,0);
  // One small early axle hints at the mechanism room. The larger gear pair
  // belongs to one maintenance recess, with clear quieter wall between them.
  turningGear('gear-small',2694,cam,526,59,-t*.15);
  turningGear('gear-large',3607,cam,483,101,t*.17);
  turningGear('gear-small',3669,cam,537,63,-t*.272+.17);
  ctx.restore();
 }
 function motes(cam,t){ctx.save();for(let i=0;i<55;i++){const wx=2050+i*41,x=wx-cam+Math.sin(t*.3+i)*5,y=590-((t*7+i*29)%195);if(x<0||x>1280)continue;ctx.globalAlpha=.12+(i%3)*.035;ctx.fillStyle=i%9===0?'#986552':'#797362';ctx.fillRect(Math.round(x),Math.round(y),i%4?2:3,2);}ctx.restore();}
 function guide(cam){const wx=cam+330,cy=ceilingY(wx),x=390;ctx.save();ctx.strokeStyle='#6e5140';ctx.fillStyle='#f8eedb';ctx.lineWidth=2;ctx.setLineDash([5,4]);ctx.beginPath();ctx.moveTo(x,cy+1);ctx.lineTo(x,F);ctx.stroke();ctx.setLineDash([]);for(const y of [cy,F]){ctx.beginPath();ctx.moveTo(x-8,y);ctx.lineTo(x+8,y);ctx.stroke();}ctx.fillRect(x+12,(F+cy)/2-14,182,29);ctx.fillStyle='#4c4033';ctx.font='13px "Malgun Gothic",sans-serif';ctx.fillText(((F-cy)/U).toFixed(2)+' u · 통로 높이',x+21,(F+cy)/2+6);ctx.restore();}
 return {earth,ceiling,masses,props,draw({progress,cameraX,t,decor,ambient,actors,showGuide}){const cam=Math.round(cameraX??progress*passageSpec.cameraTravel);background(cam);ctx.drawImage(earth,-cam,0);if(decor)devices(cam,t);if(ambient)motes(cam,t);ctx.drawImage(ceiling,-cam,0);ctx.drawImage(images['preview-existing-floor'],0,0);if(actors)ctx.drawImage(images['hero-original'],240,454);if(showGuide&&cam+330<W)guide(cam);},occlusionAt(progress){const cam=Math.round(progress*passageSpec.cameraTravel),data=e.getImageData(cam,400,1280,215).data;let n=0;for(let i=3;i<data.length;i+=4)if(data[i]>250)n++;return n/(1280*215);},wallCoverageAt(cameraX){const data=wg.getImageData(Math.round(cameraX),400,1280,215).data;let n=0;for(let i=3;i<data.length;i+=4)if(data[i]>80)n++;return n/(1280*215);}};
}
