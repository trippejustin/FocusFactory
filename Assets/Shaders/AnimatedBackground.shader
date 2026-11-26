Shader "Custom/AnimatedBackground"
{
    Properties
    {
        _CycleDuration ("Cycle Duration", Float) = 30
        _DitherStrength ("Dither Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _CycleDuration;
            float _DitherStrength;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * 6.2831853 /
                          max(_CycleDuration, 0.01);

                float3 charcoal = float3(41,39,36) / 255.0;
                float3 forest = float3(41,56,50) / 255.0;
                float3 graphite = float3(53,56,61) / 255.0;

                float a = (cos(t) + 1) * 0.5;
                float b = (cos(t - 2.094395) + 1) * 0.5;
                float c = (cos(t - 4.188790) + 1) * 0.5;

                float3 color =
                    (charcoal*a + forest*b + graphite*c)
                    / (a+b+c);

                float noise = frac(
                    52.9829189 * frac(
                        dot(floor(i.vertex.xy),
                            float2(0.06711056, 0.00583715))
                    )
                );

                color += (noise - 0.5)
                         * _DitherStrength / 255.0;

                return float4(color, 1);
            }
            ENDCG
        }
    }
}