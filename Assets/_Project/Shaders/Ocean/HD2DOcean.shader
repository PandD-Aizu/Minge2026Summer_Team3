Shader "Minge/Environment/HD2D Ocean"
{
    Properties
    {
        [Header(Water Colour)]
        _ShallowColor ("Shallow jade", Color) = (0.12, 0.62, 0.48, 1)
        _DeepColor ("Deep teal", Color) = (0.018, 0.22, 0.29, 1)
        _Absorption ("RGB absorption per metre", Vector) = (0.70, 0.18, 0.12, 0)
        _DepthDistance ("Colour transition depth", Range(0.1, 30)) = 5
        _MaxOpticalDepth ("Maximum optical depth", Range(1, 100)) = 30

        [Header(Surface Motion)]
        _WaveAmplitude ("Maximum wave height", Range(0, 0.3)) = 0.022
        _WaveSpeed ("Wave speed", Range(0, 3)) = 0.55
        _NormalScale ("Ripple scale", Range(0.1, 4)) = 1.8
        _NormalStrength ("Ripple normal strength", Range(0, 1)) = 0.32
        _CapillaryScale ("Capillary wave frequency", Range(1, 24)) = 8
        _CapillaryStrength ("Capillary wave slope", Range(0, 0.6)) = 0.22
        _RefractionStrength ("Refraction in screen UV", Range(0, 0.03)) = 0.008

        [Header(Fish Ripples)]
        _FishRippleStrength ("Fish ripple slope strength", Range(0, 3)) = 1
        _FishRippleFoamStrength ("Fish ripple crest foam", Range(0, 0.5)) = 0.08

        [Header(Shore And Foam)]
        _ShoreZ ("Shore world Z", Float) = -7
        _ShoreWidth ("Wave attenuation distance", Range(0.1, 30)) = 12
        _FoamColor ("Foam ivory", Color) = (0.72, 0.91, 0.85, 1)
        _FoamWidth ("Shore foam width", Range(0.1, 8)) = 0.7
        _FoamDepth ("Intersection foam depth", Range(0.01, 3)) = 0.3
        _FoamScale ("Foam cells per metre", Range(1, 16)) = 6
        _FoamStrength ("Shore foam strength", Range(0, 1)) = 0.66
        _CrestFoamStrength ("Offshore crest streaks", Range(0, 1)) = 0.08

        [Header(Light And Reflection)]
        _SkyHorizonColor ("Fallback horizon reflection", Color) = (0.62, 0.82, 0.88, 1)
        _SkyZenithColor ("Fallback sky reflection", Color) = (0.22, 0.46, 0.68, 1)
        _SkyCloudStrength ("Fallback cloud reflection", Range(0, 2)) = 0.55
        _ReflectionStrength ("Fresnel reflection strength", Range(0, 2)) = 1.25
        _EnvironmentBlend ("Reflection probe contribution", Range(0, 1)) = 0.3
        _Roughness ("Sun reflection roughness", Range(0.08, 0.6)) = 0.22
        _SunGlintStrength ("Sun glitter intensity", Range(0, 8)) = 2.4
        _CausticsStrength ("Underwater caustics", Range(0, 2)) = 0.32
        _CausticsScale ("Caustics scale", Range(0.1, 4)) = 1.7
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
        }

        Pass
        {
            Name "OceanForward"
            Tags { "LightMode" = "UniversalForward" }

            // 背景色をシェーダー内で合成し 魚や水しぶき用の深度は書き込まない
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _Absorption;
                float _DepthDistance;
                float _MaxOpticalDepth;
                float _WaveAmplitude;
                float _WaveSpeed;
                float _NormalScale;
                float _NormalStrength;
                float _CapillaryScale;
                float _CapillaryStrength;
                float _RefractionStrength;
                float _FishRippleStrength;
                float _FishRippleFoamStrength;
                float _ShoreZ;
                float _ShoreWidth;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamDepth;
                float _FoamScale;
                float _FoamStrength;
                float _CrestFoamStrength;
                half4 _SkyHorizonColor;
                half4 _SkyZenithColor;
                float _SkyCloudStrength;
                float _ReflectionStrength;
                float _EnvironmentBlend;
                float _Roughness;
                float _SunGlintStrength;
                float _CausticsStrength;
                float _CausticsScale;
            CBUFFER_END

            // 複数の魚が共有する波紋フィールドはマテリアルごとに複製しない
            TEXTURE2D(_OceanRippleField);
            SAMPLER(sampler_OceanRippleField);
            float4 _OceanRippleArea;
            float4 _OceanRipplePlane;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 waveData : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>格子座標から再現可能な疑似乱数を返す</summary>
            /// <param name="cell">ノイズの格子座標</param>
            /// <returns>0 から 1 の乱数</returns>
            /// <example>OceanHash(floor(worldXZ))</example>
            float OceanHash(float2 cell)
            {
                float3 value = frac(float3(cell.xyx) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            /// <summary>滑らかなノイズ値と解析的な勾配をまとめて返す</summary>
            /// <param name="position">ノイズ空間の座標</param>
            /// <returns>x が値 yz が各軸の勾配</returns>
            /// <example>OceanNoiseGradient(worldXZ * _NormalScale)</example>
            float3 OceanNoiseGradient(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                float2 blend = local * local * (3.0 - 2.0 * local);
                float2 derivative = 6.0 * local * (1.0 - local);

                float a = OceanHash(cell);
                float b = OceanHash(cell + float2(1, 0));
                float c = OceanHash(cell + float2(0, 1));
                float d = OceanHash(cell + float2(1, 1));
                float crossTerm = a - b - c + d;

                float value = a + (b - a) * blend.x + (c - a) * blend.y
                    + crossTerm * blend.x * blend.y;
                float2 gradient = derivative * (float2(b - a, c - a) + crossTerm * blend.yx);
                return float3(value, gradient);
            }

            /// <summary>方向波の高さと勾配を加算する</summary>
            /// <param name="position">ワールド XZ 座標</param>
            /// <param name="direction">正規化した波の進行方向</param>
            /// <param name="wavelength">波長のメートル値</param>
            /// <param name="weight">全振幅に対する寄与率</param>
            /// <param name="time">速度を適用済みの時間</param>
            /// <param name="wave">x が高さ yz が勾配の累積値</param>
            /// <example>AddOceanWave(position, float2(1, 0), 8, 0.5, time, wave)</example>
            void AddOceanWave(float2 position, float2 direction, float wavelength,
                float weight, float time, inout float3 wave)
            {
                float frequency = TWO_PI / wavelength;
                float phase = dot(position, direction) * frequency - time * sqrt(9.81 * frequency);
                float sine;
                float cosine;
                sincos(phase, sine, cosine);
                wave.x += sine * weight;
                wave.yz += cosine * weight * frequency * direction;
            }

            /// <summary>岸際で減衰する複数の波を合成する</summary>
            /// <param name="position">ワールド XZ 座標</param>
            /// <returns>x が高さ yz が勾配</returns>
            /// <example>OceanWaves(positionWS.xz)</example>
            float3 OceanWaves(float2 position)
            {
                float3 wave = 0;
                float time = _Time.y * _WaveSpeed;
                AddOceanWave(position, float2(0.9397, 0.3420), 9.5, 0.50, time, wave);
                AddOceanWave(position, float2(-0.6000, 0.8000), 4.2, 0.27, time, wave);
                AddOceanWave(position, float2(0.2425, 0.9701), 2.3, 0.15, time, wave);
                AddOceanWave(position, float2(0.8192, -0.5736), 1.3, 0.08, time, wave);

                // 岸で高さと勾配を滑らかにゼロへ近づける
                float shoreWidth = max(_ShoreWidth, 0.1);
                float shoreRatio = saturate((_ShoreZ - position.y) / shoreWidth);
                float attenuation = shoreRatio * shoreRatio * (3.0 - 2.0 * shoreRatio);
                float attenuationSlope = -6.0 * shoreRatio * (1.0 - shoreRatio) / shoreWidth;
                wave.z = wave.z * attenuation + wave.x * attenuationSlope;
                wave.xy *= attenuation;
                return wave * _WaveAmplitude;
            }

            /// <summary>画面の深度から水底のワールド位置を復元する</summary>
            /// <param name="screenUV">正規化した画面座標</param>
            /// <param name="hasGeometry">空以外の深度なら 1 を返す</param>
            /// <returns>透視投影と平行投影に対応したワールド位置</returns>
            /// <example>OceanBackgroundPosition(screenUV, hasGeometry)</example>
            float3 OceanBackgroundPosition(float2 screenUV, out float hasGeometry)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    hasGeometry = step(0.00001, rawDepth);
                #else
                    hasGeometry = 1.0 - step(0.99999, rawDepth);
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            }

            /// <summary>同じ水位の海だけへ共有波紋フィールドを適用する</summary>
            /// <param name="positionWS">評価する水面のワールド座標</param>
            /// <returns>XY が高さの XZ 勾配 Z が高さ W が弱い波頭の被覆率</returns>
            /// <example>OceanFishRipple(input.positionWS)</example>
            float4 OceanFishRipple(float3 positionWS)
            {
                float2 fieldUV = (positionWS.xz - _OceanRippleArea.xy) * _OceanRippleArea.zw;
                float4 ripple = SAMPLE_TEXTURE2D(_OceanRippleField, sampler_OceanRippleField, fieldUV);

                // フィールド外へのクランプの引き延ばしを防ぎ 縁の半メートルで滑らかに消す
                float2 edgeDistance = min(fieldUV, 1.0 - fieldUV)
                    / max(_OceanRippleArea.zw, float2(0.0001, 0.0001));
                float edgeFade = smoothstep(0.0, 0.5, min(edgeDistance.x, edgeDistance.y));
                float heightTolerance = max(_OceanRipplePlane.y, 0.01);
                float planeFade = 1.0 - smoothstep(heightTolerance * 0.5, heightTolerance,
                    abs(positionWS.y - _OceanRipplePlane.x));
                return ripple * edgeFade * planeFade * saturate(_OceanRipplePlane.z);
            }

            /// <summary>異なる大きさと方向の細波と魚の波紋から法線を求める</summary>
            /// <param name="position">ワールド XZ 座標</param>
            /// <param name="waveSlope">頂点波の勾配</param>
            /// <param name="fishRipple">共有フィールドから取得した魚の波紋</param>
            /// <param name="surfaceNoise">泡の分布にも利用するノイズ値</param>
            /// <returns>ワールド空間の上向き単位法線</returns>
            /// <example>OceanNormal(positionWS.xz, wave.yz, fishRipple, surfaceNoise)</example>
            float3 OceanNormal(float2 position, float2 waveSlope, float4 fishRipple, out float surfaceNoise)
            {
                float time = _Time.y * _WaveSpeed;
                float2 uv = position * _NormalScale * float2(0.7, 1.8);
                float3 broad = OceanNoiseGradient(uv + time * float2(-0.075, 0.055));
                float2 rotatedUV = float2(uv.x * 0.8 - uv.y * 0.6, uv.x * 0.6 + uv.y * 0.8);
                float3 fine = OceanNoiseGradient(rotatedUV * 2.17 + time * float2(0.095, -0.13));

                // 回転させたノイズ勾配をワールドの XZ 軸へ戻す
                float2 fineSlope = float2(fine.y * 0.8 + fine.z * 0.6, -fine.y * 0.6 + fine.z * 0.8);
                float footprint = max(length(ddx(position)), length(ddy(position))) * _NormalScale;
                float detailVisibility = rcp(1.0 + footprint * footprint * 4.0);
                float2 fishSlope = fishRipple.xy * _FishRippleStrength;
                float localCalm = 1.0 - saturate(length(fishSlope) * 2.0
                    + abs(fishRipple.z) * _FishRippleStrength * 12.0) * 0.3;
                float2 slope = waveSlope + (broad.yz + fineSlope * 0.55) * float2(0.7, 1.15)
                    * _NormalStrength * detailVisibility * localCalm;

                // 毛細波を別の周波数で重ね 鏡面反射を小さな光片へ分ける
                float2 capillaryUV = position * _CapillaryScale;
                float4 phase = float4(
                    dot(capillaryUV, float2(0.16, 0.987)),
                    dot(capillaryUV, float2(-0.43, 0.902)) * 1.43,
                    dot(capillaryUV, float2(0.70, 0.714)) * 2.31,
                    dot(capillaryUV, float2(-0.18, 0.984)) * 3.17);
                phase += time * float4(-2.5, 3.6, -4.7, 5.3);
                phase += float4(broad.x, fine.x, broad.x, fine.x) * float4(3, 2, 4, 3);
                float4 phaseFootprint = abs(ddx(phase)) + abs(ddy(phase));
                float4 waveVisibility = 1.0 - smoothstep(0.65, 3.0, phaseFootprint);
                float4 capillary = sin(phase) * waveVisibility * float4(0.60, 0.33, 0.18, 0.10);
                slope += _CapillaryStrength * localCalm * float2(
                    dot(capillary, float4(0.16, -0.43, 0.70, -0.18)),
                    dot(capillary, float4(0.987, 0.902, 0.714, 0.984)));

                // 波紋は画素で評価し メッシュ分割に依存せず反射と屈折へ反映する
                slope += fishSlope;

                surfaceNoise = broad.x * 0.65 + fine.x * 0.35;
                return normalize(float3(-slope.x, 1.0, -slope.y));
            }

            /// <summary>泡の細胞境界と丸い穴のために近傍二点の距離を求める</summary>
            /// <param name="position">泡の格子座標</param>
            /// <returns>x が最も近い点までの距離 y が細胞境界の指標</returns>
            /// <example>OceanFoamCells(worldXZ * _FoamScale)</example>
            float2 OceanFoamCells(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                float nearest = 8.0;
                float second = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x, y);
                        float2 offset = float2(OceanHash(cell + neighbour), OceanHash(cell + neighbour + 19.19));
                        float2 delta = neighbour + 0.15 + offset * 0.7 - local;
                        float distanceSquared = dot(delta, delta);
                        second = min(second, max(nearest, distanceSquared));
                        nearest = min(nearest, distanceSquared);
                    }
                }

                return float2(sqrt(nearest), second - nearest);
            }

            /// <summary>水底に投影する緩やかな集光模様を生成する</summary>
            /// <param name="position">水底のワールド XZ 座標</param>
            /// <param name="depth">水面から水底までの垂直距離</param>
            /// <returns>深さと画素サイズで減衰した集光強度</returns>
            /// <example>OceanCaustics(backgroundWS.xz, waterDepth)</example>
            float OceanCaustics(float2 position, float depth)
            {
                float time = _Time.y * _WaveSpeed;
                float2 uv = position * _CausticsScale;
                float2 warp = float2(sin(uv.y * 1.7 + time * 0.8), sin(uv.x * 1.4 - time * 0.65)) * 0.24;
                float2 cells = OceanFoamCells(uv + warp + time * float2(0.07, -0.045));
                float antialias = max(fwidth(cells.y), 0.008);
                float pattern = 1.0 - smoothstep(0.025 - antialias, 0.10 + antialias, cells.y);
                float breathing = 0.65 + 0.35 * sin(uv.x * 0.53 + uv.y * 0.71 + time * 0.6);
                float footprint = max(length(ddx(uv)), length(ddy(uv)));
                float visibility = rcp(1.0 + footprint * footprint * 8.0);
                return pattern * breathing * exp(-depth * 0.32) * saturate(depth * 3.0) * visibility;
            }

            /// <summary>岸 波頭と桟橋などの交差部に泡を描く</summary>
            /// <param name="position">ワールド XZ 座標</param>
            /// <param name="depth">水底までの垂直距離</param>
            /// <param name="waveHeight">頂点波の変位</param>
            /// <param name="surfaceNoise">細波のノイズ値</param>
            /// <returns>0 から 1 の泡被覆率</returns>
            /// <example>OceanFoam(positionWS.xz, depth, wave.x, noise)</example>
            float OceanFoam(float2 position, float depth, float waveHeight, float surfaceNoise)
            {
                float time = _Time.y * _WaveSpeed;
                float shoreDistance = max(_ShoreZ - position.y, 0.0);
                float shoreEnvelope = 1.0 - smoothstep(0, max(_FoamWidth, 0.01) * 2.2, shoreDistance);
                float intersection = 1.0 - smoothstep(0.02, max(_FoamDepth, 0.03), depth);

                // 細胞の輪郭と丸い穴を組み合わせ 連続した白帯を避ける
                float2 foamUV = position * _FoamScale + float2(time * 0.035, time * 0.06);
                float2 cells = OceanFoamCells(foamUV + surfaceNoise * 0.7);
                float cellAA = max(fwidth(cells.y), 0.015);
                float lace = 1.0 - smoothstep(0.065 - cellAA, 0.13 + cellAA, cells.y);
                float bubbleAA = max(fwidth(cells.x), 0.01);
                float bubbles = 1.0 - smoothstep(0.022, 0.022 + bubbleAA, abs(cells.x - 0.22));
                float patchNoise = OceanNoiseGradient(position * 2.3 + time * float2(0.045, -0.09)).x;
                float patches = smoothstep(0.25, 0.70, patchNoise);
                float foamTexture = saturate(lace * 0.72 + bubbles * 0.35) * lerp(0.35, 1.0, patches);

                // 岸へ寄せる泡は細い波頭とその後に残る粒へ分ける
                float foamPhase = shoreDistance * 7.2 + surfaceNoise * 3.5 + time * 1.25;
                float shoreBand = pow(saturate(0.5 + 0.5 * sin(foamPhase)), 5.0);
                float shoreFoam = shoreEnvelope * (shoreBand * 0.85 + patches * 0.15) * foamTexture;
                float contactFoam = intersection * foamTexture;

                // 沖の泡は細長い模様を波頭付近だけに残す
                float2 streakUV = float2(position.x * 0.23 + position.y * 0.09, position.y * 1.7);
                float streakNoise = OceanNoiseGradient(streakUV + float2(time * 0.04, -time * 0.19)).x;
                float normalizedHeight = waveHeight / max(_WaveAmplitude, 0.001);
                float crest = smoothstep(0.35, 0.85, normalizedHeight);
                float streak = smoothstep(0.66, 0.83, streakNoise) * crest * lace;

                return saturate(max(shoreFoam, contactFoam) * _FoamStrength
                    + streak * _CrestFoamStrength);
            }

            /// <summary>細波で分かれる太陽の GGX 反射を求める</summary>
            /// <param name="normalWS">水面の単位法線</param>
            /// <param name="viewWS">カメラへ向かう単位方向</param>
            /// <param name="light">影を含むメインライト</param>
            /// <returns>入射光を反映した鏡面反射色</returns>
            /// <example>OceanSunGlint(normalWS, viewWS, mainLight)</example>
            float3 OceanSunGlint(float3 normalWS, float3 viewWS, Light light)
            {
                float3 halfDirection = SafeNormalize(viewWS + light.direction);
                float nDotH = saturate(dot(normalWS, halfDirection));
                float nDotV = max(saturate(dot(normalWS, viewWS)), 0.05);
                float nDotL = saturate(dot(normalWS, light.direction));
                float vDotH = saturate(dot(viewWS, halfDirection));

                // サブピクセルの法線変化では反射を広げてちらつきを抑える
                float normalVariance = dot(ddx(normalWS), ddx(normalWS)) + dot(ddy(normalWS), ddy(normalWS));
                float alphaSquared = max(pow(_Roughness, 4.0), normalVariance * 0.12);
                float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
                float distribution = alphaSquared / max(PI * denominator * denominator, 0.00001);
                float visibilityV = nDotL * sqrt(nDotV * nDotV * (1.0 - alphaSquared) + alphaSquared);
                float visibilityL = nDotV * sqrt(nDotL * nDotL * (1.0 - alphaSquared) + alphaSquared);
                float visibility = 0.5 / max(visibilityV + visibilityL, 0.0001);
                float fresnel = 0.02 + 0.98 * pow(1.0 - vDotH, 5.0);

                return min(distribution * visibility * fresnel * nDotL, 8.0)
                    * light.color * light.shadowAttenuation * light.distanceAttenuation * _SunGlintStrength;
            }

            /// <summary>水面の頂点を上下させ描画に必要な値を渡す</summary>
            /// <param name="input">分割済み水面メッシュの頂点</param>
            /// <returns>クリップ座標 ワールド座標 波の情報</returns>
            /// <example>描画パスの vertex エントリーポイントとして使用</example>
            Varyings OceanVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 wave = OceanWaves(positionWS.xz);
                positionWS.y += wave.x;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.waveData = wave;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// <summary>屈折 吸収 反射と泡を合成して海の色を返す</summary>
            /// <param name="input">水面頂点から補間した描画情報</param>
            /// <returns>背景を一度だけ合成した不透明な最終色</returns>
            /// <example>描画パスの fragment エントリーポイントとして使用</example>
            half4 OceanFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceNoise;
                float4 fishRipple = OceanFishRipple(input.positionWS);
                float3 normalWS = OceanNormal(input.positionWS.xz, input.waveData.yz, fishRipple, surfaceNoise);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float centerHasGeometry;
                float3 centerBackground = OceanBackgroundPosition(screenUV, centerHasGeometry);
                float centerDepth = lerp(_MaxOpticalDepth,
                    max(input.positionWS.y - centerBackground.y, 0.0), centerHasGeometry);

                // 屈折先が手前の桟橋や陸地に当たる場合は元の画面座標へ戻す
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 refractedUV = saturate(screenUV + normalVS.xy * _RefractionStrength * saturate(centerDepth));
                float refractedHasGeometry;
                float3 backgroundWS = OceanBackgroundPosition(refractedUV, refractedHasGeometry);
                float surfaceEyeDepth = -TransformWorldToView(input.positionWS).z;
                float backgroundEyeDepth = -TransformWorldToView(backgroundWS).z;
                float validRefraction = (1.0 - refractedHasGeometry)
                    + refractedHasGeometry * step(surfaceEyeDepth + 0.02, backgroundEyeDepth);
                refractedUV = lerp(screenUV, refractedUV, validRefraction);
                backgroundWS = lerp(centerBackground, backgroundWS, validRefraction);
                float hasGeometry = lerp(centerHasGeometry, refractedHasGeometry, validRefraction);

                // 光が水中を進む距離に応じて赤から先に吸収する
                float waterDepth = lerp(_MaxOpticalDepth,
                    max(input.positionWS.y - backgroundWS.y, 0.0), hasGeometry);
                float opticalDepth = lerp(_MaxOpticalDepth,
                    min(length(backgroundWS - input.positionWS), _MaxOpticalDepth), hasGeometry);
                float3 transmission = exp(-max(_Absorption.xyz, 0.001) * max(opticalDepth, 0.0));
                float depthBlend = 1.0 - exp(-waterDepth / max(_DepthDistance, 0.1));
                float3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);
                float3 sceneColor = SampleSceneColor(refractedUV);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float caustics = OceanCaustics(backgroundWS.xz, waterDepth) * hasGeometry;
                sceneColor *= 1.0 + caustics * _CausticsStrength * mainLight.shadowAttenuation;
                waterTint *= lerp(0.72, 1.0, mainLight.shadowAttenuation);

                // 反射プローブが暗いシーンでも空色の反射を保つ
                float3 reflectionWS = reflect(-viewWS, normalWS);
                float3 skyReflection = lerp(_SkyHorizonColor.rgb, _SkyZenithColor.rgb,
                    pow(saturate(reflectionWS.y), 0.65));

                // 固定した空の雲が細波の向きに応じて映り込み 小さな明暗を作る
                float2 skyUV = reflectionWS.xz / max(reflectionWS.y + 0.25, 0.35);
                float cloud = OceanNoiseGradient(skyUV * float2(2.7, 4.1)).x;
                float cloudShape = smoothstep(0.42, 0.68, cloud);
                skyReflection += _SkyCloudStrength * cloudShape * float3(0.86, 0.94, 1.0);
                float3 probeReflection = GlossyEnvironmentReflection(reflectionWS, input.positionWS,
                    _Roughness, 1.0h, screenUV);
                float3 reflectedColor = lerp(skyReflection, max(probeReflection, skyReflection * 0.7), _EnvironmentBlend);
                float nDotV = saturate(dot(normalWS, viewWS));
                float reflectionWeight = saturate((0.02 + 0.98 * pow(1.0 - nDotV, 5.0)) * _ReflectionStrength);

                // 屈折で動かさない深度を使い 接触する泡を岸や桟橋へ固定する
                float foam = OceanFoam(input.positionWS.xz, centerDepth, input.waveData.x, surfaceNoise);
                foam = saturate(foam + fishRipple.w * _FishRippleFoamStrength
                    * lerp(0.45, 1.0, surfaceNoise));
                float3 foamColor = _FoamColor.rgb * lerp(0.65, 1.0, mainLight.shadowAttenuation);
                float3 backgroundWeight = transmission * (1.0 - reflectionWeight) * (1.0 - foam);
                float3 surfaceColor = waterTint * (1.0 - transmission) * (1.0 - reflectionWeight);
                surfaceColor += reflectedColor * reflectionWeight;
                surfaceColor += OceanSunGlint(normalWS, viewWS, mainLight);
                surfaceColor = lerp(surfaceColor, foamColor, foam);

                // 既に霧が適用された背景色へ二重に霧をかけず 水面成分のみを処理する
                float fogFactor = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                float fogVisibility = MixFogColor(float3(1, 1, 1), float3(0, 0, 0), fogFactor).r;
                float3 color = sceneColor * backgroundWeight + surfaceColor * fogVisibility
                    + unity_FogColor.rgb * (1.0 - backgroundWeight) * (1.0 - fogVisibility);
                return half4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
