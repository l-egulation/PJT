Shader "Deviant/Chapter2/Terrain" {
 Properties { _MainTex("Stage1 original tile",2D)="white"{} }
 SubShader { Tags {"RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline"}
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
 struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 V vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.uv=a.uv;return v;}
 half4 frag(V v):SV_Target{return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv);}
 ENDHLSL
 } }
}
