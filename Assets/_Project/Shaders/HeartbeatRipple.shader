Shader "Hidden/Project/HeartbeatRipple"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Heartbeat Ripple"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _HeartbeatWaves;
            float _HeartbeatAspect;

            // 中央から画面の隅まで広がり、時間とともに消える円形波
            float Ripple(float radius, float age, float strength, float maxRadius)
            {
                float phase = (radius - age * maxRadius) / 0.085;
                float envelope = exp(-phase * phase * 2.0);
                float fade = sin(saturate(age) * 3.14159265);
                return sin(phase * 3.14159265) * envelope * fade * strength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                // 縦横比を補正し、ワイド画面でも円の形を保つ
                float aspect = max(_HeartbeatAspect, 0.001);
                float2 centered = (uv - 0.5) * float2(aspect, 1.0);
                float radius = length(centered);
                float maxRadius = length(float2(aspect, 1.0)) * 0.5 + 0.15;
                float offset = Ripple(radius, _HeartbeatWaves.x, _HeartbeatWaves.y, maxRadius)
                    + Ripple(radius, _HeartbeatWaves.z, _HeartbeatWaves.w, maxRadius);
                float2 direction = centered / max(radius, 0.0001);
                float2 distorted = uv + direction * offset / float2(aspect, 1.0);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distorted));
            }
            ENDHLSL
        }
    }
}
