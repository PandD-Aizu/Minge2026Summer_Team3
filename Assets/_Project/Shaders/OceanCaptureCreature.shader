Shader "Hidden/Project/OceanCaptureCreature"
{
    Properties
    {
        _BaseMap ("Octopus texture", 2D) = "white" {}
        _Opacity ("Visibility", Range(0,1)) = 1
        _Tentacle ("Procedural suckers", Float) = 0
        _WaterHeight ("Water plane", Float) = 0.3
        _Scare ("Closeup", Float) = 0
        _Focus ("Eye focus", Float) = 0
        _Omen ("Eye only", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Opacity, _Tentacle, _WaterHeight, _Scare, _Focus, _Omen;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv = i.uv;
                if (_Scare > 0.5)
                {
                    float focus = smoothstep(0.1,0.75,_Focus);
                    float mask = 1-smoothstep(0.12,0.25,length(uv-0.5));
                    uv.x += (uv.x-0.5)*focus*0.65*mask + (1-focus)*0.018*mask;
                }
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                clip(col.a - 0.1);
                if (_Scare > 0.5)
                    col.a *= 1-smoothstep(0.40,0.5,max(abs(i.uv.x-0.5),abs(i.uv.y-0.5)));
                if (_Omen > 0.5)
                    col.a *= (1-smoothstep(0.16,0.23,length((i.uv-float2(0.5,0.77))*float2(1,0.85))));
                if (_Tentacle > 0.5)
                {
                    // 暗紫の皮膚と二列の黄土色の吸盤を元のタコの配色に合わせる
                    float2 cup = float2(frac(i.uv.x * 2.0) - 0.5, frac(i.uv.y * 16.0) - 0.5);
                    float ring = length(cup * float2(3.0, 2.5));
                    float sucker = (1.0 - smoothstep(0.65, 0.85, ring)) * step(0.35, i.uv.x);
                    float hollow = 1.0 - smoothstep(0.2, 0.5, ring);
                    col.rgb = lerp(half3(0.19,0.012,0.28), half3(0.63,0.50,0.12) * (1.0 - hollow * 0.7), sucker);
                    float lighting = 0.45 + 0.55 * abs(dot(normalize(i.normalWS), normalize(float3(-0.4,1,-0.3))));
                    float rim = pow(1.0 - abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))), 3);
                    col.rgb = col.rgb * lighting + half3(0.15,0.24,0.3) * rim * 0.25;
                }
                float depth = max(0, _WaterHeight - i.positionWS.y);
                col.rgb = lerp(col.rgb, half3(0.015,0.11,0.13), saturate(depth * 0.7));
                col.a *= _Opacity * exp(-depth * 0.8);
                return col;
            }
            ENDHLSL
        }
    }
}
