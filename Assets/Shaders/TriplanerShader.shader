Shader "Custom/TriplanerShader"
{
    Properties
    {
        [NoScaleOffset]_BaseTex("Base Texture", 2D) = "white" {}
        [NoScaleOffset]_TopTex("Top Texture", 2D) = "white" {}
        [NoScaleOffset]_Mat1Tex("Material 1 Texture", 2D) = "white" {}
        _TexScale("Texture Scale", float) = 1

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
        #pragma target 3.5
        #include "UnityCG.cginc"
        
        struct Input
        {
            fixed2 matData : TEXCOORD0;
            float3 localCoord : POSITION;
            float3 localNormal : NORMAL;
        };

        UNITY_DECLARE_TEX2D(_BaseTex);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_TopTex);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Mat1Tex);
        half _TexScale;
        half _BlendOffset;
        half _BlendExponent;
        half _Glossiness;
        half _Metallic;

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            data.matData = v.texcoord.xy;
            data.localCoord = v.vertex.xyz;
            data.localNormal = v.normal.xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed2 xUV = IN.localCoord.zy * _TexScale;
            fixed2 yUV = IN.localCoord.xz * _TexScale;
            fixed2 zUV = IN.localCoord.xy * _TexScale;
            //base texture
            half3 xCol = UNITY_SAMPLE_TEX2D(_BaseTex, xUV);
            half3 zCol = UNITY_SAMPLE_TEX2D(_BaseTex, zUV);
            half3 yCol;
            if (IN.localNormal.y > 0)
                yCol = UNITY_SAMPLE_TEX2D_SAMPLER(_TopTex, _BaseTex, yUV);
            else
                yCol = UNITY_SAMPLE_TEX2D(_BaseTex, yUV);
            //extra textures
            if (IN.matData.x > 0.5)
            {
                half4 xBlend = UNITY_SAMPLE_TEX2D_SAMPLER(_Mat1Tex, _BaseTex, xUV);
                half4 yBlend = UNITY_SAMPLE_TEX2D_SAMPLER(_Mat1Tex, _BaseTex, yUV);
                half4 zBlend = UNITY_SAMPLE_TEX2D_SAMPLER(_Mat1Tex, _BaseTex, zUV);
                xCol = xCol * (1 - xBlend.a) + xBlend.rgb * xBlend.a;
                yCol = yCol * (1 - yBlend.a) + yBlend.rgb * yBlend.a;
                zCol = zCol * (1 - zBlend.a) + zBlend.rgb * zBlend.a;
            }

            float3 blendWeight = abs(normalize(IN.localNormal));
            blendWeight = saturate(blendWeight - _BlendOffset);
            blendWeight = pow(blendWeight, _BlendExponent);
            blendWeight /= (blendWeight.x + blendWeight.y + blendWeight.z);

            o.Albedo = (xCol * blendWeight.x + yCol * blendWeight.y + zCol * blendWeight.z);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
