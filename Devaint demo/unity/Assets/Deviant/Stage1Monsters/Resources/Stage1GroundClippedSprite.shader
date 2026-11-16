Shader "Deviant/Stage1GroundClippedSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color",Color)=(1,1,1,1)
        [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
        _GroundY("Ground",Float)=-10000
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            struct Output { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float worldY:TEXCOORD1; };
            float _GroundY;
            Output vert(appdata_t v)
            { Output o;float4 at=UnityFlipSprite(v.vertex,_Flip);o.vertex=UnityObjectToClipPos(at);o.uv=v.texcoord;o.color=v.color*_Color*_RendererColor;o.worldY=mul(unity_ObjectToWorld,at).y;return o; }
            fixed4 frag(Output i):SV_Target
            { clip(i.worldY-_GroundY);return tex2D(_MainTex,i.uv)*i.color; }
            ENDCG
        }
    }
}
