Shader "Deviant/Chapter2/WindowCity" {
 Properties { _MainTex("Approved city",2D)="white"{} _Phase("Phase",Float)=0 _Camera("Camera offset",Float)=0 }
 SubShader { Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Pass { Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float _Phase,_Camera;
 V vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.uv=a.uv;return v;}
 half4 frag(V v):SV_Target{float2 uv=float2(frac(_Phase-_Camera+v.uv.x*64/960),.3+v.uv.y*80/540);half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);c.rgb=lerp(c.rgb*1.7,half3(1,.95,.82),.2);return c;}
 ENDHLSL
 }}}
