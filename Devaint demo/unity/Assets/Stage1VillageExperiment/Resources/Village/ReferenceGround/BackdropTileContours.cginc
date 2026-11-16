// Native approved contour tiles, independent of the gameplay grid.
sampler2D _BackdropLeftTex,_BackdropRightTex,_BackdropBottomTex;
float4 _BackdropLeftCanvas,_BackdropRightCanvas,_BackdropBottomCanvas;
float4 _BackdropBeam,_BackdropColumn,_BackdropEdgePeriods;
float _BackdropTiles,_BackdropCorner;

// x = native alpha, y = actual drawn contour ink, z = signed interior distance.
float3 BackdropPart(float2 p,float4 bounds)
{
 float4 distances=float4(p.x-bounds.x,bounds.z-p.x,p.y-bounds.y,bounds.w-p.y);
 float near=min(min(distances.x,distances.y),min(distances.z,distances.w));
 float radius=min(_BackdropCorner,min(bounds.z-bounds.x,bounds.w-bounds.y)*.45);
 float2 q=abs(p-(bounds.xy+bounds.zw)*.5)-(bounds.zw-bounds.xy)*.5+radius;
 float depth=radius-length(max(q,0))-min(max(q.x,q.y),0);
 if(depth>=7)return float3(1,0,depth);
 if(depth<=-7)return float3(0,0,depth);
 float2 uv;float4 edge;
 if(near==distances.x)
 {
  uv=float2(_BackdropLeftCanvas.z+depth,_BackdropLeftCanvas.w+frac(p.y/_BackdropEdgePeriods.x)*_BackdropEdgePeriods.x)/_BackdropLeftCanvas.xy;
  edge=tex2D(_BackdropLeftTex,float2(uv.x,1-uv.y));
 }
 else if(near==distances.y)
 {
  uv=float2(_BackdropRightCanvas.z-depth,_BackdropRightCanvas.w+frac(p.y/_BackdropEdgePeriods.x)*_BackdropEdgePeriods.x)/_BackdropRightCanvas.xy;
  edge=tex2D(_BackdropRightTex,float2(uv.x,1-uv.y));
 }
 else
 {
  uv=float2(_BackdropBottomCanvas.z+frac(p.x/_BackdropEdgePeriods.y)*_BackdropEdgePeriods.y,_BackdropBottomCanvas.w-depth)/_BackdropBottomCanvas.xy;
  edge=tex2D(_BackdropBottomTex,float2(uv.x,1-uv.y));
 }
 float alpha=smoothstep(.65,.98,edge.a);
 float ink=(1-smoothstep(.25,.6,max(edge.r,max(edge.g,edge.b))))*alpha;
 return float3(alpha,ink,depth);
}

float3 BackdropCoverage(float2 p)
{
 float3 a=BackdropPart(p,_BackdropBeam),b=BackdropPart(p,_BackdropColumn);
 // Overlapping connection modules hide their internal outline, keeping only
 // the exposed silhouette. Source UVs never restart at the beam/column join.
 float ink=max(a.y*(1-smoothstep(0,7,b.z)),b.y*(1-smoothstep(0,7,a.z)));
 return float3(max(a.x,b.x),ink,max(a.z,b.z));
}
