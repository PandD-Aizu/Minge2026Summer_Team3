Shader "Minge/Environment/Stage Light Shaft"
{
    Properties
    {
        [Header(Light Colour)]
        [HDR] _Tint ("Pale sunlight tint", Color) = (1, 0.89, 0.65, 0.2)
        _Intensity ("Light intensity", Range(0, 4)) = 0.65
        _SunlightFade ("Time of day visibility", Range(0, 1)) = 1

        [Header(Shaft Shape)]
        _Softness ("Side edge softness in UV", Range(0.01, 0.5)) = 0.4
        _NoiseStrength ("Fine streak variation", Range(0, 1)) = 0.35
        _Speed ("Drift speed", Range(0, 2)) = 0.12

        [Header(Surface Fades)]
        _DepthFade ("Terrain intersection fade metres", Range(0.01, 10)) = 2
        _WaterHeight ("Water surface world Y", Float) = -1
        _WaterFade ("Fade above water metres", Range(0.01, 10)) = 1.5
        _NearFade ("Camera near fade metres", Range(0.01, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CoastalLightShaft"
            Tags { "LightMode" = "UniversalForward" }

            // 薄い光だけを加算し 既存の海面や地形の深度を維持する
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShaftVertex
            #pragma fragment ShaftFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity;
                float _SunlightFade;
                float _Softness;
                float _NoiseStrength;
                float _Speed;
                float _DepthFade;
                float _WaterHeight;
                float _WaterFade;
                float _NearFade;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>光線メッシュの位置とフェード用の UV および頂点色をフラグメントへ渡す</summary>
            /// <param name="input">UV の Y が下端 0 から上端 1 の静的メッシュ頂点</param>
            /// <returns>投影座標とワールド座標を持つ描画用頂点</returns>
            /// <example>頂点色の alpha を 0.5 にすると光線の寄与が半分になる</example>
            Varyings ShaftVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            /// <summary>光線の縁と遮蔽物との境界を薄め ゆっくり揺らぐ光を加算する</summary>
            /// <param name="input">補間された位置 UV と頂点色</param>
            /// <returns>フェードを乗算した HDR 光色と、描画先の alpha を維持するための 0</returns>
            /// <example>_WaterHeight を -1 にすると Y が -1 以下の光線が消える</example>
            half4 ShaftFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 四辺を消して板状の輪郭を隠し 下端を少し長くぼかす
                float sideDistance = min(input.uv.x, 1.0 - input.uv.x);
                float sideFade = smoothstep(0.0, max(_Softness, 0.001), sideDistance);
                float endFade = smoothstep(0.0, 0.24, input.uv.y)
                    * (1.0 - smoothstep(0.78, 1.0, input.uv.y));

                // テクスチャを使わず 細い筋をゆっくり横へ流す
                float time = _Time.y * _Speed;
                float drift = sin(input.uv.y * 3.7 + time) * 0.025;
                float stripeUV = input.uv.x + drift;
                float wideStreak = 0.5 + 0.5 * sin(stripeUV * 31.0 + time * 0.37);
                float fineStreak = 0.5 + 0.5 * sin(stripeUV * 83.0 - time * 0.21);
                float streaks = lerp(1.0, 0.45 + 0.55 * wideStreak * fineStreak,
                    saturate(_NoiseStrength));

                // 透視投影と平行投影の双方で深度を同じ視点距離へ変換する
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = IsPerspectiveProjection()
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
                float shaftEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthFade = saturate((sceneEyeDepth - shaftEyeDepth)
                    / max(_DepthFade, 0.001));

                // 海面は深度を書かないため ワールド高で水中への漏れを防ぐ
                float waterFade = smoothstep(_WaterHeight,
                    _WaterHeight + max(_WaterFade, 0.001), input.positionWS.y);
                float nearFade = smoothstep(0.0, max(_NearFade, 0.001),
                    shaftEyeDepth - _ProjectionParams.y);
                float coverage = sideFade * endFade * streaks * depthFade * waterFade * nearFade;

                // 太陽の色と輝度に追従し、夜間は暖色の光が残らないようにする
                Light sun = GetMainLight();
                half luminance = dot(sun.color, half3(0.2126h, 0.7152h, 0.0722h));
                half daylight = smoothstep(0.04h, 0.45h, luminance);
                half3 sunlight = sun.color / max(max(sun.color.r, max(sun.color.g, sun.color.b)), 0.001h);
                half3 lightColor = _Tint.rgb * sunlight * daylight
                    * input.color.rgb * max(_Intensity, 0.0);
                half alpha = saturate(coverage * _Tint.a * input.color.a) * saturate(_SunlightFade);
                return half4(lightColor * alpha, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

