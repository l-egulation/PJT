Shader "Deviant/UI/NotebookPage"
{
 Properties { [PerRendererData] _MainTex("Illustrated page", 2D) = "white" {} _DraftTex("Written page", 2D) = "white" {} _Reveal("Reveal", Range(0,1)) = 1 }
 SubShader
 {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
   sampler2D _MainTex, _DraftTex; float _Reveal;
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
   fixed4 frag(v2f i):SV_Target
   {
    // Identical margin pixels stay unchanged; an opaque feathered sweep covers the middle writing.
    float edge=lerp(-420,1820,_Reveal);
    float a=saturate((edge+420-i.uv.x*1400)/840);
    return lerp(tex2D(_DraftTex,i.uv),tex2D(_MainTex,i.uv),a)*i.color;
   }
   ENDCG
  }
 }
}
