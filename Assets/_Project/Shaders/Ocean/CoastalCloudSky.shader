Shader "Minge/Environment/Coastal Cloud Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.16,0.36,0.65,1)
        _Horizon ("Horizon haze", Color) = (0.65,0.76,0.84,1)
        _CloudLight ("Sunlit cloud", Color) = (1,0.97,0.91,1)
        _CloudShadow ("Cloud shadow", Color) = (0.33,0.43,0.55,1)
        _Coverage ("Cloud coverage", Range(0,1)) = 0.52
        _Wind ("Wind speed", Range(0,0.1)) = 0.008
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith, _Horizon, _CloudLight, _CloudShadow;
            float _Coverage, _Wind;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.direction = i.positionOS.xyz;
                return o;
            }
            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell+float3(1,0,0)), f.x),
                                 lerp(Hash(cell+float3(0,1,0)), Hash(cell+float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(cell+float3(0,0,1)), Hash(cell+float3(1,0,1)), f.x),
                                 lerp(Hash(cell+float3(0,1,1)), Hash(cell+float3(1,1,1)), f.x), f.y), f.z);
            }
            float Density(float3 p)
            {
                // 大きな雲の塊に細かな凹凸を重ね、雲底と上端を柔らかくする
                float shape = Noise(p * float3(1,1.5,1)) * 0.58
                    + Noise(p * 2.07 + 13.7) * 0.28 + Noise(p * 4.13 + 29.3) * 0.14;
                float height = smoothstep(1.0,1.18,p.y) * (1-smoothstep(1.55,1.95,p.y));
                return saturate((shape - (0.73 - _Coverage * 0.42)) * 5) * height;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 ray = normalize(i.direction);
                Light sun = GetMainLight();
                float3 sunDirection = normalize(sun.direction);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(ray.y),0.45));
                sky += half3(1,0.85,0.62) * pow(saturate(dot(ray,sunDirection)),512) * 0.65;
                if(ray.y <= 0.08) return half4(sky,1);

                // 高度の異なる密度を積分し、平面画像では出ない厚みと陰影を作る
                float stepLength = 0.95 / (ray.y * 32);
                float jitter = Hash(floor(ray * 1400));
                float3 wind = float3(_Time.y * _Wind + 8.3,0,4.7);
                float transmission = 1;
                half3 cloud = 0;
                [loop] for(int n=0;n<32;n++)
                {
                    float3 p = ray * ((1.0 / ray.y) + (n+jitter)*stepLength) + wind;
                    float density = Density(p);
                    float lit = saturate(0.65 + (density-Density(p+sunDirection*0.24))*1.7);
                    float alpha = 1-exp(-density*stepLength*3.5);
                    half3 shade = lerp(_CloudShadow.rgb,_CloudLight.rgb,lit);
                    cloud += transmission * alpha * shade;
                    transmission *= 1-alpha;
                    if(transmission<0.025) break;
                }
                half3 result = cloud + sky*transmission;
                // 水平線付近の遠い雲は海霧へ溶け込ませる
                result = lerp(sky,result,smoothstep(0.08,0.38,ray.y));
                return half4(result,1);
            }
            ENDHLSL
        }
    }
}
