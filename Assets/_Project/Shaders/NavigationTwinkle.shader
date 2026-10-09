Shader "Project/NavigationTwinkle"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (3, 2.5, 1.2, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            // パーティクルの座標と寿命に応じた色を描画へ渡す
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // テクスチャなしで柔らかな光と十字のきらめきを描く
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = abs(input.uv * 2.0 - 1.0);
                float glow = pow(saturate(1.0 - length(p)), 3.0);
                float star = pow(saturate(1.0 - min(p.x, p.y) * 12.0), 2.0)
                    * pow(saturate(1.0 - max(p.x, p.y)), 2.0);
                return half4(_Color.rgb * input.color.rgb, _Color.a * input.color.a * saturate(glow + star));
            }
            ENDHLSL
        }
    }
}
