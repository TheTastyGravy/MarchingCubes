Shader "Custom/TriplanerShader"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _TopTex("Top Texture", 2D) = "white" {}
        _SideTex("Side Texture", 2D) = "white" {}
        _BlendOffset("Blend Offset", Range(0,0.5)) = 0.0
        _BlendExponent("Blend Exponent", Range(1, 8)) = 1

        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
            #pragma surface surf Standard vertex:vert fullforwardshadows
            #pragma target 3.0

            sampler2D _TopTex;
            float4 _TopTex_ST;
            sampler2D _SideTex;
            float4 _SideTex_ST;

            struct Input
            {
                float3 localCoord;
                float3 localNormal;
            };

            half _BlendOffset;
            half _BlendExponent;

            fixed4 _Color;
            half _Glossiness;
            half _Metallic;

            void vert(inout appdata_full v, out Input data)
            {
                UNITY_INITIALIZE_OUTPUT(Input, data);
                data.localCoord = v.vertex.xyz;
                data.localNormal = v.normal.xyz;
            }

            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                half3 xCol = tex2D(_SideTex, TRANSFORM_TEX(IN.localCoord.zy, _SideTex));
                half3 zCol = tex2D(_SideTex, TRANSFORM_TEX(IN.localCoord.xy, _SideTex));
                half3 yCol;
                if (IN.localNormal.y > 0)
                    yCol = tex2D(_TopTex, TRANSFORM_TEX(IN.localCoord.xz, _TopTex));
                else
                    yCol = tex2D(_SideTex, TRANSFORM_TEX(IN.localCoord.xz, _SideTex));
                float3 blendWeight = abs(normalize(IN.localNormal));
                blendWeight = saturate(blendWeight - _BlendOffset);
                blendWeight = pow(blendWeight, _BlendExponent);
                blendWeight /= (blendWeight.x + blendWeight.y + blendWeight.z);

                o.Albedo = (xCol * blendWeight.x + yCol * blendWeight.y + zCol * blendWeight.z) * _Color.rgb;
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = _Color.a;
            }
            ENDCG
        }
            FallBack "Diffuse"
}
