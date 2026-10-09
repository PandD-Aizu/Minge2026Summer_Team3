Shader "Minge/Weather/Rain Streak"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (0.78, 0.88, 1, 0.7)
        _GroundY ("Ground height", Float) = -1000
        _NearFade ("Near camera fade distance", Range(0.01, 5)) = 1.5
        _EdgeSoftness ("Streak edge softness", Range(0.01, 0.5)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RainStreak"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RainVertex
            #pragma fragment RainFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _GroundY;
                float _NearFade;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>雨と雷の頂点を投影し、地表とカメラ付近のフェードに必要な値を渡す</summary>
            /// <param name="input">ParticleSystem または LineRenderer の位置、UV、色</param>
            /// <returns>描画用の投影座標と補間するワールド座標</returns>
            /// <example>雨では粒の頂点色の alpha が各雨筋の透明度に反映される</example>
            Varyings RainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }

            /// <summary>細い筋の縁を薄くし、地中とカメラ直前の過大な雨筋を描画しない</summary>
            /// <param name="input">補間した雨または雷の頂点情報</param>
            /// <returns>HDR 対応の色と合成用透明度</returns>
            /// <example>雷素材の色を HDR にすると同じ描画処理で Bloom に反映される</example>
            half4 RainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.positionWS.y - _GroundY);

                // 対称な UV フェードで雨筋と横方向 UV の LineRenderer を共用する
                float2 edge = min(input.uv, 1.0 - input.uv);
                float2 softness = smoothstep(0.0, max(_EdgeSoftness, 0.001), edge);
                float eyeDepth = -TransformWorldToView(input.positionWS).z;
                float nearFade = smoothstep(0.15, max(_NearFade, 0.16), eyeDepth);
                half alpha = saturate(input.color.a * softness.x * softness.y * nearFade);
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
