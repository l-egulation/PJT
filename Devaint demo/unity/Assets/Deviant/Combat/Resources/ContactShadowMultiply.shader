// Contact shadow under actors: multiplies what is already drawn (Blend DstColor Zero), so inside the shadow every
// ground colour steps one shade darker and the grass and soil marks stay visible. The sprite's alpha is the shape;
// the SpriteRenderer colour (gamma) is the multiplier, its alpha the strength. The renderer colour arrives as
// _RendererColor (UnitySprites.cginc), as in the project's other sprite shaders, not only as the vertex colour.
Shader "Deviant/ContactShadowMultiply" {
 Properties {
  [PerRendererData] _MainTex ("Shape", 2D) = "white" {}
  _Color ("Tint", Color) = (1,1,1,1)
  [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
  [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"} Cull Off ZWrite Off Lighting Off Blend DstColor Zero
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnitySprites.cginc"
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 v vert(appdata_t i){v o;o.vertex=UnityObjectToClipPos(UnityFlipSprite(i.vertex,_Flip));o.uv=i.texcoord;o.color=i.color*_Color*_RendererColor;o.color.rgb=GammaToLinearSpace(o.color.rgb);return o;}
 fixed4 frag(v i):SV_Target{float k=tex2D(_MainTex,i.uv).a*i.color.a;return fixed4(lerp(float3(1,1,1),i.color.rgb,k),1);}
 ENDCG
 } }
}
