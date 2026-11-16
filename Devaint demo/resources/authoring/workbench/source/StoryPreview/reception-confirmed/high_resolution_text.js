// Text is rasterized at the display's physical resolution, above the 960x540 art.
class HighResolutionTextLayer {
 constructor(scene,overlay){
  this.scene=scene;this.canvas=overlay;this.enabled=!!(overlay&&overlay.getContext);
  this.ctx=this.enabled?overlay.getContext('2d'):null;
  this.dpr=0;this.drawCount=0;this.resize();
 }
 resize(){
  if(!this.enabled)return;
  const rect=this.scene.getBoundingClientRect(),dpr=window.devicePixelRatio||1;
  const w=Math.max(1,Math.round(rect.width*dpr)),h=Math.max(1,Math.round(rect.height*dpr));
  if(this.canvas.width!==w)this.canvas.width=w;
  if(this.canvas.height!==h)this.canvas.height=h;
  this.dpr=dpr;this.scaleX=w/960;this.scaleY=h/540;
 }
 begin(){
  this.drawCount=0;if(!this.enabled)return;
  if((window.devicePixelRatio||1)!==this.dpr)this.resize();
  const c=this.ctx;c.setTransform(1,0,0,1,0,0);c.clearRect(0,0,this.canvas.width,this.canvas.height);
  c.setTransform(this.scaleX,0,0,this.scaleY,0,0);
 }
 draw(source,text,x,y){
  const c=this.ctx;c.save();c.font=source.font;c.fillStyle=source.fillStyle;
  c.textAlign=source.textAlign;c.textBaseline=source.textBaseline;c.globalAlpha=source.globalAlpha;
  c.shadowBlur=0;c.shadowOffsetX=0;c.shadowOffsetY=0;c.filter='none';
  c.fillText(text,x,y);c.restore();this.drawCount++;
 }
 cover(x,y,w,h){if(this.enabled)this.ctx.clearRect(x,y,w,h);}
}
