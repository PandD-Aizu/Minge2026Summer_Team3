Shader "Minge/Enemy/StealthCloak"
{
    Properties
    {
        _BaseMap ("Enemy texture", 2D) = "white" {}
        _BaseColor ("Enemy color", Color) = (1, 1, 1, 1)
        _Cutoff ("Texture cutout", Range(0, 1)) = 0.5
        _Opacity ("Visibility", Range(0, 1)) = 1
        _EdgeColor ("Cloak shimmer", Color) = (0.25, 0.8, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "StealthCloak"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Cutoff;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            // 元の輪郭を保ち、消失途中だけ僅かに揺らす
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float transition = 4 * _Opacity * (1 - _Opacity);
                float3 position = input.positionOS.xyz;
                position.x += sin(position.y * 25 + _Time.y * 13) * 0.015 * transition;
                output.positionWS = TransformObjectToWorld(position);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 元画像の抜きを保ちながら走査線と青白い揺らぎを重ねて薄くする
            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texel.a - _Cutoff);
                float transition = 4 * _Opacity * (1 - _Opacity);
                float scan = 0.5 + 0.5 * sin(input.positionWS.y * 55 - _Time.y * 9);
                float3 normal = normalize(input.normalWS);
                float rim = pow(1 - saturate(abs(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)))), 3);
                Light light = GetMainLight();
                half3 lighting = max(SampleSH(normal) + light.color * saturate(dot(normal, light.direction)), half3(0.12, 0.12, 0.12));
                half3 color = texel.rgb * _BaseColor.rgb * lighting;
                color += _EdgeColor.rgb * transition * (0.15 * scan + 0.35 * rim);
                float alpha = texel.a * _BaseColor.a * _Opacity * lerp(1, 0.55 + scan * 0.45, transition);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
