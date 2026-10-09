Shader "Hidden/Project/OceanCaptureUnderside"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p = i.world.xz;
                float t = _Time.y;
                float wave = sin(p.x * 2.1 + sin(p.y * 1.7 + t) + t * 1.6)
                    * cos(p.y * 2.5 - t * 1.3 + sin(p.x * 1.5));
                float caustic = pow(saturate(1.0 - abs(wave) * 4.0), 4);
                float lightPool = exp(-length((i.uv - 0.5) * 60.0) * 0.12);
                half3 color = lerp(half3(0.015,0.06,0.08), half3(0.18,0.48,0.48), lightPool);
                color += caustic * half3(0.10,0.26,0.22) * lightPool;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
