Shader "Custom/SpriteVerticalCrumble"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CrumbleCutoff ("Crumble Cutoff", Range(-0.2, 1.2)) = -0.1
        _CrumbleSoftness ("Crumble Softness", Range(0.001, 0.25)) = 0.015
        _CrumbleNoise ("Crumble Noise", Range(0, 0.35)) = 0.22
        _CrumbleMinY ("Crumble Min Y", Float) = -0.5
        _CrumbleHeight ("Crumble Height", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float localY : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _CrumbleCutoff;
            float _CrumbleSoftness;
            float _CrumbleNoise;
            float _CrumbleMinY;
            float _CrumbleHeight;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.localY = input.vertex.y;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                float normalizedY = saturate((input.localY - _CrumbleMinY) / max(_CrumbleHeight, 0.0001));

                float2 chunkCell = floor(float2(input.texcoord.x * 10.0, normalizedY * 16.0));
                float coarseNoise = Hash21(chunkCell);
                float fineNoise = Hash21(floor(input.texcoord * 42.0));
                float breakup = normalizedY + (coarseNoise - 0.5) * _CrumbleNoise + (fineNoise - 0.5) * _CrumbleNoise * 0.35;

                float edge = smoothstep(_CrumbleCutoff, _CrumbleCutoff + _CrumbleSoftness, breakup);
                float hardChunk = step(_CrumbleCutoff, breakup + (coarseNoise - 0.5) * _CrumbleSoftness);
                float visible = min(edge, hardChunk);

                color.a *= visible;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
