Shader "Minge/Weather/HD2D Puddle"
{
    Properties
    {
        _WaterColor ("Wet blue grey", Color) = (0.17, 0.25, 0.30, 1)
        _SkyColor ("Cool sky reflection", Color) = (0.60, 0.72, 0.83, 1)
        _WarmColor ("Warm light reflection", Color) = (1, 0.78, 0.48, 1)
        _Opacity ("Water opacity", Range(0, 1)) = 0.68
        _ReflectionStrength ("Sky reflection", Range(0, 2)) = 0.85
        _GlintStrength ("Warm glints", Range(0, 5)) = 1.25
        _RippleStrength ("Quiet ripples", Range(0, 0.1)) = 0.017
        _PuddleAmount ("Puddle amount", Range(0, 1)) = 0
        _RainIntensity ("Rain intensity", Range(0, 1)) = 0
        _Wetness ("Ground wetness", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-5" }

        Pass
        {
            Name "PuddleForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PuddleVertex
            #pragma fragment PuddleFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _SkyColor;
                half4 _WarmColor;
                float _Opacity;
                float _ReflectionStrength;
                float _GlintStrength;
                float _RippleStrength;
                float _PuddleAmount;
                float _RainIntensity;
                float _Wetness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            /// <summary>格子座標から固定した擬似乱数を求める</summary>
            /// <param name="cell">ノイズの格子座標</param>
            /// <returns>0から1の再現可能な値</returns>
            /// <example>PuddleHash(floor(positionWS.xz))</example>
            float PuddleHash(float2 cell)
            {
                float3 value = frac(float3(cell.xyx) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            /// <summary>格子の四隅を補間して水面と輪郭に使う滑らかなノイズを求める</summary>
            /// <param name="position">ノイズ空間の座標</param>
            /// <returns>0から1のノイズ値</returns>
            /// <example>PuddleNoise(positionWS.xz * 3)</example>
            float PuddleNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(lerp(PuddleHash(cell), PuddleHash(cell + float2(1, 0)), blend.x),
                    lerp(PuddleHash(cell + float2(0, 1)), PuddleHash(cell + 1.0), blend.x), blend.y);
            }

            /// <summary>地面へ接地済みの頂点を変換し、影と霧の座標を渡す</summary>
            /// <param name="input">位置と水たまりごとのUVと乱数</param>
            /// <returns>ワールド位置と描画に必要な補間値</returns>
            /// <example>PuddleVertex(meshVertex)</example>
            Varyings PuddleVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }

            /// <summary>不規則な輪郭、空の反射、静かな波紋を合成して浅い水たまりを描く</summary>
            /// <param name="input">水面の位置と水たまりごとのUV</param>
            /// <returns>地面が透ける水面色と輪郭の不透明度</returns>
            /// <example>PuddleFragment(interpolatedVertex)</example>
            half4 PuddleFragment(Varyings input) : SV_Target
            {
                // 小さな格子へ輪郭を寄せ、元のピクセル地面に馴染む不定形な縁を作る
                float seed = input.uv.z * 137.0;
                float2 local = input.uv.xy * 2.0 - 1.0;
                float2 shapeUV = floor(local * 30.0) / 30.0;
                float noise = PuddleNoise(shapeUV * 3.6 + seed);
                float radius = lerp(0.46, 0.83, sqrt(saturate(_PuddleAmount))) + (noise - 0.5) * 0.20;
                float distanceToEdge = radius - length(shapeUV);
                float edgeWidth = max(fwidth(distanceToEdge) * 1.4, 0.045);
                float edge = smoothstep(0.0, edgeWidth, distanceToEdge);
                clip(edge * _PuddleAmount - 0.001);

                // 雨上がりにも弱い細波を残し、降雨中は小さな円形波を足す
                float time = _Time.y;
                float2 world = input.positionWS.xz;
                float2 slope = float2(cos(world.x * 8.5 + world.y * 3.1 + time * 1.1),
                    sin(world.x * -4.2 + world.y * 9.8 - time * 0.9)) * _RippleStrength;
                float2 rippleCenter = float2(sin(seed), cos(seed * 1.7)) * 0.24;
                float2 rippleOffset = local - rippleCenter;
                float rippleDistance = length(rippleOffset);
                float phase = frac(time * lerp(0.37, 0.88, _RainIntensity) + input.uv.z);
                float ring = sin((rippleDistance - phase * 1.8) * 42.0) *
                    exp(-abs(rippleDistance - phase * 1.8) * 18.0) * (1.0 - phase);
                slope += rippleOffset / max(rippleDistance, 0.05) * ring * lerp(0.003, 0.024, _RainIntensity);
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 reflectionWS = reflect(-viewWS, normalWS);
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normalWS, viewWS)), 5.0);

                // 実際の視線方向から空を評価し、画面の上下反転による偽の映り込みを作らない
                float2 skyUV = reflectionWS.xz / max(reflectionWS.y + 0.5, 0.55);
                float clouds = smoothstep(0.32, 0.75,
                    PuddleNoise(skyUV * float2(2.6, 6.8) + float2(1.3, 2.8)));
                float3 sky = _SkyColor.rgb * lerp(0.86, 1.42, clouds);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 ambient = max(SampleSH(float3(0, 1, 0)), 0.0);

                // 空の反射を地面の直射光の影で黒くせず、主光源色で時間帯の明るさを保つ
                float3 skyIllumination = mainLight.color * (0.78 + ambient * 0.45);
                float3 groundIllumination = mainLight.color *
                    (ambient * 0.3 + lerp(0.26, 0.52, mainLight.shadowAttenuation));
                float3 water = _WaterColor.rgb * groundIllumination;
                float3 color = lerp(water, sky * skyIllumination,
                    saturate((0.62 + fresnel * 0.52) * _ReflectionStrength));

                // 滑らかな水面に細い雲の反射筋と途切れた水際を加え、地面の影と見分けやすくする
                float cloudStreak = smoothstep(0.59, 0.76,
                    PuddleNoise(skyUV * float2(3.8, 18.0) + float2(seed * 0.013, 4.1)));
                float rim = 1.0 - smoothstep(edgeWidth * 0.65, edgeWidth * 2.0, distanceToEdge);
                float rimBreakup = smoothstep(0.34, 0.7, PuddleNoise(local * 7.5 + seed));
                color += _SkyColor.rgb * skyIllumination *
                    (cloudStreak * 0.17 + rim * rimBreakup * 0.18);

                // 暖かい主光源の細い反射を冷たい水面へ重ねる
                float3 halfDirection = SafeNormalize(mainLight.direction + viewWS);
                float glint = pow(saturate(dot(normalWS, halfDirection)), 72.0);
                color += mainLight.color * _WarmColor.rgb * glint * _GlintStrength *
                    mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                color += sky * skyIllumination * ring * ring * 0.048 * saturate(_Wetness);
                color = MixFog(color, input.fogFactor);
                float alpha = edge * saturate(_PuddleAmount * 3.0) * _Opacity * lerp(0.72, 1.0, fresnel);
                return half4(max(color, 0.0), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
