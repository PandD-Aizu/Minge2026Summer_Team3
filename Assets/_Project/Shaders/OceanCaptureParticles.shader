Shader "Project/OceanCaptureParticles"
{
    Properties { _Bubble ("Bubble", Float) = 0 }
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
            CBUFFER_START(UnityPerMaterial)
            float _Bubble;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float radius = length(p);
                float edge = 1 - smoothstep(0.82, 1, radius);
                float ring = smoothstep(0.63, 0.87, radius) * edge;
                float highlight = exp(-dot(p - float2(-0.3,0.4), p - float2(-0.3,0.4)) * 65);
                float spray = pow(saturate(1 - radius * radius), 1.6);
                float alpha = lerp(spray, ring * 0.65 + highlight + edge * 0.035, _Bubble);
                return half4(i.color.rgb + highlight * _Bubble * 0.3, i.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
