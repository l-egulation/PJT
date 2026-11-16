Shader "Stage1Village/ColorBaseline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _SketchTex("Registered sketch",2D)="white"{}
        _HasSketch("Registered sketch available",Float)=0
        _TerrainFill("Continuous terrain paper fill",Float)=0
        _TerrainArtwork("Terrain sketch alpha is rock coverage",Float)=0
        _HideSketch("Sketch replaced by complete reference drawings",Float)=0
        _NearSketchInk("Platform-matched near scenery ink",Float)=0
        _RegionAnchor("World-fixed structure anchor",Vector)=(0,0,0,0)
        _RegionLayer("Whole distant layer priority",Float)=0
        _SketchPaperMatch("Match near paper to approved terrain",Float)=0
        _SketchVertex("Sketch drawing for procedural sign",Float)=0
        _SketchInk("Procedural sign ink",Float)=0
        _Color("Tint",Color)=(1,1,1,1)
        _Haze("Distant haze",Range(0,1))=0
        _HazeColor("Distant haze color",Color)=(.6,.73,.75,1)
        _WorldUV("World-fixed backdrop with moving texture",Float)=0
        _WorldRepeat("Backdrop size in world units",Vector)=(1,1,0,0)
        _WorldScroll("Backdrop parallax sample offset",Vector)=(0,0,0,0)
        _WorldOrigin("Shared upright backdrop origin",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True"}
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex,_SketchTex;
            fixed4 _Color,_HazeColor;
            float _Haze,_WorldUV,_HasSketch,_TerrainFill,_TerrainArtwork,_SketchVertex,_SketchInk,_HideSketch,_NearSketchInk;
            // Globals intentionally stay out of Properties so materials cannot shadow them.
            float _VillageArtMode,_VillageArtWeight,_VillageBoundaryPixels,_VillageEndpointOverride;
            float4 _WorldRepeat,_WorldScroll,_WorldOrigin,_VillageAuthoredRange;
            float _VillageCrispRegions,_RegionLayer,_SketchPaperMatch;float4 _RegionAnchor;
            #include "VillageCrispRegions.cginc"
            struct input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct interpolator {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float2 world:TEXCOORD1;};
            interpolator vert(input v){interpolator o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}

            float ArtWeight(float2 worldPixels)
            {
                if(_VillageEndpointOverride>=0)return saturate(_VillageEndpointOverride);
                if(_VillageArtMode<.5)return 0;
                if(_VillageArtMode<1.5)return 1;
                if(_VillageArtMode<2.5)return saturate(_VillageArtWeight);
                // Keep both curves identical to VillageArtTransition.SampleWeight.
                // Rendered world position, never backdrop UVs or camera coordinates.
                float curve=sin(worldPixels.y*.004)*28+sin(worldPixels.y*.010+1.4)*12;
                if(_VillageArtMode<3.5)
                {
                    // Explicit color-left/sketch-right 180px review band.
                    float t=saturate((worldPixels.x-_VillageBoundaryPixels-curve+90)/180);
                    return t*t*(3-2*t);
                }
                float width=_VillageAuthoredRange.y-_VillageAuthoredRange.x;
                float authored=saturate((worldPixels.x-_VillageAuthoredRange.x)/width);
                float bend=clamp(curve/width,-.25,.25);
                authored=saturate(authored-bend*4*authored*(1-authored));
                return 1-authored*authored*(3-2*authored);
            }

            float HatchHash(float2 cell)
            {
                float3 p=frac(float3(cell.x,cell.y,cell.x)*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }

            float TerrainStroke(float2 strokeSpace,float seedOffset)
            {
                float lane=floor(strokeSpace.x/5.5);
                float laneSeed=HatchHash(float2(lane,seedOffset));
                float period=27+18*laneSeed;
                float along=strokeSpace.y+frac(laneSeed*13.79)*period;
                float segment=floor(along/period);
                along-=segment*period;
                float seed=HatchHash(float2(lane+seedOffset,segment+37));
                float length=period*(.55+.3*frac(seed*19.13));
                float start=(period-length)*(.15+.7*frac(seed*7.31));
                float t=(along-start)/length;
                // Each stroke has its own lean and tapered ends, with no shared
                // rectangular group edge or camera-dependent phase.
                float center=(lane+.5)*5.5+(seed-.5)*1.2;
                center+=(frac(seed*11.17)-.5)*.9*(t-.5);
                center+=(frac(seed*23.71)-.5)*.8*t*(1-t);
                float width=.32+.12*frac(seed*17.13);
                float stroke=1-smoothstep(width,width+.45,abs(strokeSpace.x-center));
                stroke*=smoothstep(start,start+1.5,along)*
                    (1-smoothstep(start+length-2,start+length,along));
                return stroke*(.48+.3*frac(seed*29.47));
            }

            float3 TerrainPaper(float2 worldPixels)
            {
                // Interior ink depends only on world pixels, never a source tile's
                // soil brightness or UVs. Surface shade lives in the native rock art.
                float2 pixel=floor(worldPixels)+.5;
                float2 strokeSpace=float2(pixel.x+pixel.y,pixel.x-pixel.y)*.70710678;
                float hatch=TerrainStroke(strokeSpace,19);
                // Subpixel strokes disappear at overview scale rather than aliasing.
                float2 footprint=fwidth(worldPixels);
                hatch*=1-smoothstep(1.25,3,max(footprint.x,footprint.y));
                float3 paper=float3(248,237,220)/255;
                float3 ink=float3(207,198,181)/255;
                #ifndef UNITY_COLORSPACE_GAMMA
                    paper=GammaToLinearSpace(paper);
                    ink=GammaToLinearSpace(ink);
                #endif
                return lerp(paper,ink,hatch);
            }

            fixed4 frag(interpolator i):SV_Target
            {
                float2 uv=i.uv;
                if(_WorldUV>.5)
                {
                    float2 cell=(i.world+_WorldScroll.xy+_WorldOrigin.xy)/max(_WorldRepeat.xy,float2(.001,.001));
                    uv=float2(1-abs(frac(cell.x*.5)*2-1),frac(cell.y));
                }
                fixed4 color=tex2D(_MainTex,uv)*i.color;
                color.rgb=lerp(color.rgb,_HazeColor.rgb,_Haze);
                // Unpaired artwork and Color retain the original haze and exact path.
                if((_VillageEndpointOverride<0&&_VillageArtMode<.5)||(_HasSketch<.5&&_TerrainFill<.5&&_TerrainArtwork<.5&&_SketchVertex<.5))return color;
                float2 worldPixels=i.world*float2(32,-32);
                float weight=ArtWeight(worldPixels);
                if(_VillageCrispRegions>.5&&_VillageEndpointOverride<0&&_VillageArtMode>2.5)
                    weight=VillageMaterialRegion(worldPixels,tex2D(_MainTex,uv).rgb,_RegionAnchor,_RegionLayer);
                if(weight<=0)return color;
                fixed4 sketch;
                if(_HideSketch>.5)sketch=fixed4(0,0,0,0);
                else if(_TerrainArtwork>.5)
                {
                    fixed4 rock=tex2D(_SketchTex,uv);
                    sketch=fixed4(lerp(TerrainPaper(worldPixels),rock.rgb,rock.a),color.a);
                }
                else if(_TerrainFill>.5)
                    sketch=fixed4(TerrainPaper(worldPixels),color.a);
                else if(_SketchVertex>.5)
                {
                    float3 drawn=_SketchInk>.5?float3(143,136,122)/255:float3(236,225,206)/255;
                    #ifndef UNITY_COLORSPACE_GAMMA
                        drawn=GammaToLinearSpace(drawn);
                    #endif
                    sketch=fixed4(drawn,color.a);
                }
                else
                    sketch=tex2D(_SketchTex,uv)*i.color;
                if(_SketchPaperMatch>.5)
                {
                    float3 drawn=sketch.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                        drawn=LinearToGammaSpace(drawn);
                    #endif
                    float3 paper=float3(248,237,220)/255;
                    float value=dot(drawn,float3(.299,.587,.114));
                    float density=saturate((.94-value)/.76);
                    // Preserve local ink and all native alpha. Only the hue/value
                    // of the fill is matched to the existing warm terrain paper.
                    drawn=lerp(paper,float3(.22,.21,.19),density);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        drawn=GammaToLinearSpace(drawn);
                    #endif
                    sketch.rgb=drawn;
                }
                if(_NearSketchInk>.5)
                {
                    // Restore graphite pressure only in world-fixed nearby drawings.
                    // Keep warm paper, alpha, silhouette and source sample positions intact.
                    float3 paper=float3(248,237,220)/255;
                    float3 ink=float3(48,44,39)/255;
                    float3 drawn=sketch.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                        drawn=LinearToGammaSpace(drawn);
                    #endif
                    float darkness=saturate(dot(paper-drawn,float3(.2126,.7152,.0722)));
                    float pressure=.42*smoothstep(.09,.48,darkness);
                    drawn=lerp(drawn,min(drawn,ink),pressure);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        drawn=GammaToLinearSpace(drawn);
                    #endif
                    sketch.rgb=drawn;
                }
                // Sketch assets already contain their own warm distance treatment.
                // Applying the color haze here would cool the approved paper/lines.
                if(weight>=1)return sketch;
                float alpha=lerp(color.a,sketch.a,weight);
                float3 premultiplied=lerp(color.rgb*color.a,sketch.rgb*sketch.a,weight);
                // Blend SrcAlpha expects straight alpha. Interpolate premultiplied
                // colors first so transparent paired edges cannot darken or double.
                return fixed4(alpha>0?premultiplied/alpha:float3(0,0,0),alpha);
            }
            ENDCG
        }
    }
}
