Shader "Hidden/Minge/Ocean Fish Ripple Field"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FishRippleField"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RippleVertex
            #pragma fragment RippleFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 各イベントの物理量を描画側の MaterialPropertyBlock から受け取る
            float4 _RippleRect;
            float4 _RippleShape;
            float4 _RippleWave;
            float4 _RippleDirection;
            float4 _OceanRippleArea;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 offsetWS : TEXCOORD0;
            };

            /// <summary>波紋の範囲を共有フィールドのクリップ座標へ配置する</summary>
            /// <param name="input">XY が -1 から 1 の四角形の頂点</param>
            /// <returns>描画座標と波紋中心からのワールド XZ 距離</returns>
            /// <example>イベントごとの四角形を RippleVertex で描画する</example>
            Varyings RippleVertex(Attributes input)
            {
                Varyings output;
                output.offsetWS = input.positionOS.xy * _RippleRect.z;
                float2 worldXZ = _RippleRect.xy + output.offsetWS;
                float2 fieldUV = (worldXZ - _OceanRippleArea.xy) * _OceanRippleArea.zw;
                float2 clipXY = fieldUV * 2.0 - 1.0;

                // RenderTexture の UV 原点に合わせ ワールド Z と採取 UV の向きを揃える
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                output.positionCS = float4(clipXY, 0.0, 1.0);
                return output;
            }

            /// <summary>滑らかな立ち上がりと入力座標に対する解析的な微分を返す</summary>
            /// <param name="lower">立ち上がりの開始位置</param>
            /// <param name="upper">立ち上がりの終了位置</param>
            /// <param name="value">評価位置</param>
            /// <returns>x が値 y が微分</returns>
            /// <example>RippleSmoothGradient(-2.2, -0.8, distanceInWaves)</example>
            float2 RippleSmoothGradient(float lower, float upper, float value)
            {
                float inverseRange = rcp(max(upper - lower, 0.0001));
                float ratio = saturate((value - lower) * inverseRange);
                return float2(ratio * ratio * (3.0 - 2.0 * ratio),
                    6.0 * ratio * (1.0 - ratio) * inverseRange);
            }

            /// <summary>外へ伝播する波束の高さと勾配を加算フィールドへ出力する</summary>
            /// <param name="input">波紋中心からのワールド XZ 距離</param>
            /// <returns>XY が高さの XZ 勾配 Z が高さ W が弱い波頭の被覆率</returns>
            /// <example>ARGBHalf フィールドへ Blend One One で重ねる</example>
            half4 RippleFragment(Varyings input) : SV_Target
            {
                float age = max(_RippleShape.x, 0.0);
                float duration = max(_RippleShape.y, 0.01);
                float wavelength = max(_RippleShape.z, 0.05);
                float radius = max(_RippleWave.x, 0.0) + age * max(_RippleShape.w, 0.0);

                // 発生位置の微分を有限に保ち 中心へ波が折り返す尖りを抑える
                float coreRadius = wavelength * 0.12;
                float distanceWS = sqrt(dot(input.offsetWS, input.offsetWS) + coreRadius * coreRadius);
                float2 radial = input.offsetWS / distanceWS;
                float distanceInWaves = (distanceWS - radius) / wavelength;
                float2 trailing = RippleSmoothGradient(-2.2, -0.8, distanceInWaves);
                float2 leading = RippleSmoothGradient(0.1, 0.85, distanceInWaves);
                float envelope = trailing.x * (1.0 - leading.x);
                float envelopeSlope = (trailing.y * (1.0 - leading.x) - trailing.x * leading.y)
                    / wavelength;

                // 生成直後と寿命末尾を滑らかにし 拡散による振幅低下を反映する
                float fadeIn = smoothstep(0.0, min(0.16, duration * 0.12), age);
                float fadeOut = 1.0 - smoothstep(duration * 0.45, duration, age);
                float sourceRadius = max(_RippleWave.x, wavelength * 0.45);
                float spreading = sqrt(sourceRadius / max(radius, sourceRadius));
                float temporal = fadeIn * fadeOut * spreading;

                // 進行方向の後方へ穏やかに寄せ 角度による高さ変化も法線へ反映する
                float directionalWeight = saturate(_RippleDirection.z);
                float alignment = dot(radial, _RippleDirection.xy);
                float aft = saturate(0.5 - alignment * 0.5);
                float directional = lerp(1.0, 0.38 + 0.62 * aft * aft, directionalWeight);
                float2 alignmentGradient = (_RippleDirection.xy - radial * alignment) / distanceWS;
                float2 directionalGradient = -0.62 * aft * directionalWeight * alignmentGradient;

                // 波長を解像できない場合はなだらかに消し 遠方や広い水面のちらつきを避ける
                float waveVisibility = 1.0 - smoothstep(0.18, 0.70, fwidth(distanceInWaves));
                float sine;
                float cosine;
                sincos(TWO_PI * distanceInWaves, sine, cosine);
                float amplitude = _RippleWave.y * temporal * waveVisibility;
                float height = amplitude * envelope * cosine;
                float radialSlope = amplitude * (envelopeSlope * cosine
                    - envelope * TWO_PI / wavelength * sine);
                float2 gradient = radialSlope * radial * directional + height * directionalGradient;

                // 波頭は弱い補助情報だけを残し 主な見え方を反射と屈折へ任せる
                float crest = smoothstep(0.45, 0.95, cosine) * envelope * temporal * directional;
                crest *= saturate(abs(_RippleWave.y) / 0.022) * waveVisibility * 0.35;
                return half4(gradient, height * directional, crest);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
