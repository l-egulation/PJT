Shader "Hidden/Stage1PaperAsh" {
 SubShader { Tags {"Queue"="Transparent+50" "RenderType"="Transparent"} Cull Off ZWrite Off Lighting Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a {float4 vertex:POSITION;fixed4 color:COLOR;};struct v {float4 vertex:SV_POSITION;fixed4 color:COLOR;};
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.color=i.color;o.color.rgb=GammaToLinearSpace(o.color.rgb);return o;}
 fixed4 frag(v i):SV_Target{return i.color;}
 ENDCG
 } }
}
