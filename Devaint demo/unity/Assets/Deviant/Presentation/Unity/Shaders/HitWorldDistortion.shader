Shader "Hidden/Deviant/HitWorldDistortion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Waves[8];float4 _WaveParams[8];float4 _InternalSize;
            int _WaveCount;float _MaximumPixels;
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.texcoord,offset=0;
                for(int i=0;i<_WaveCount;i++)
                {
                    float2 p=(uv-_Waves[i].xy)*_InternalSize.xy;
                    float distance=length(p);
                    float band=1-smoothstep(0,_Waves[i].w,abs(distance-_Waves[i].z));
                    offset+=p/max(distance,0.0001)*band*_WaveParams[i].x;
                }
                float amount=length(offset);
                offset*=min(1,_MaximumPixels/max(amount,0.0001));
                float2 halfTexel=0.5*_BlitTexture_TexelSize.xy;
                uv=clamp(uv+offset/_InternalSize.xy,halfTexel,1-halfTexel);
                return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
            }
            ENDHLSL
        }
    }
}
