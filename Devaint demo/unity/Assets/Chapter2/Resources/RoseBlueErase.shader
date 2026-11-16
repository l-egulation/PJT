Shader "Deviant/Chapter2/RoseBlueErase"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _EraseWorldY("Erase edge",Float)=10000
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct Varying {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float worldY:TEXCOORD1;fixed4 color:COLOR;};
            sampler2D _MainTex;float _EraseWorldY;
            Varying vert(Input v){Varying o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.worldY=mul(unity_ObjectToWorld,v.vertex).y;o.color=v.color;return o;}
            fixed4 frag(Varying v):SV_Target
            {clip(_EraseWorldY-v.worldY);fixed4 c=tex2D(_MainTex,v.uv)*v.color;c.rgb=lerp(c.rgb,fixed3(1,1,1),saturate(1-(_EraseWorldY-v.worldY)/.12));return c;}
            ENDCG
        }
    }
}
