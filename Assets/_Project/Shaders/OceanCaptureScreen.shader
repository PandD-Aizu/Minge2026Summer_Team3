Shader "Hidden/Project/OceanCaptureScreen"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _WaterCapture; // x=潜水率、y=入水後秒数、z=入水衝撃、w=縦横比

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float t = _WaterCapture.y;
                float impact = _WaterCapture.z;
                float waterline = _WaterCapture.x * 1.4 - 0.2 + sin(uv.x * 17 + t * 9) * impact * 0.04;
                float wet = 1.0 - smoothstep(waterline - 0.03, waterline + 0.03, uv.y);
                float2 displacement = float2(sin(uv.y * 24 + t * 6), cos(uv.x * 21 - t * 5)) * (0.004 + impact * 0.018) * wet;
                float2 refracted = saturate(uv + displacement);
                float blur = (0.0015 + impact * 0.009) * wet;
                half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refracted).rgb * 0.4;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refracted + float2(blur,0))).rgb * 0.15;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refracted - float2(blur,0))).rgb * 0.15;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refracted + float2(0,blur))).rgb * 0.15;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refracted - float2(0,blur))).rgb * 0.15;

                // 赤い光から吸収し、遠方ほど濁らせる
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float fog = 1.0 - exp(-min(depth, 30.0) * 0.16);
                half3 underwater = lerp(col * half3(0.34,0.72,0.79), half3(0.008,0.065,0.078), fog * 0.85);
                col = lerp(col, underwater, wet);

                // 入水時の白い泡と視界を上へ抜けていく大小の気泡
                float foam = 0;
                for (int b = 0; b < 18; b++)
                {
                    float seed = b + 1.0;
                    float2 center = float2(Hash(seed), frac(Hash(seed + 51) + t * (0.16 + Hash(seed + 8) * 0.28)));
                    center.x += sin(t * 3.0 + seed) * 0.025;
                    float radius = 0.006 + Hash(seed + 19) * 0.023;
                    float2 delta = (uv - center) * float2(_WaterCapture.w, 1);
                    float r = length(delta) / radius;
                    float ring = exp(-pow((r - 0.85) * 9, 2));
                    float glint = exp(-dot(delta + float2(radius * 0.25, -radius * 0.35), delta + float2(radius * 0.25, -radius * 0.35)) / (radius * radius * 0.035));
                    foam += ring * 0.15 + glint * 0.45;
                }
                float froth = pow(saturate(sin(uv.x * 67 + sin(uv.y * 53) * 3 + t * 22) * cos(uv.y * 83 - t * 15)), 3);
                col += half3(0.48,0.74,0.77) * (foam * wet + froth * impact * 0.32 * wet);
                float edge = smoothstep(0.2,0.72,length(uv - 0.5));
                col *= 1.0 - wet * (edge * 0.5 + saturate(t / 5.0) * 0.28);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
