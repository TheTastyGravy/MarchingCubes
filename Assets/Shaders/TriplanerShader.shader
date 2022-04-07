Shader "Custom/TriplanerShader"
{
    Properties
    {
        [NoScaleOffset]_BaseTex("Base Texture", 2D) = "white" {}
        [NoScaleOffset]_TopTex("Top Texture", 2D) = "white" {}
        [NoScaleOffset]_MatTex("Material Textures", 2DArray) = "" {}
        _TexScale("Texture Scale", float) = 1

        _BaryOffset("Barycenter Offset", Range(0, 1)) = 0.0
        _BlendOffset("Blend Offset", Range(0, 0.5)) = 0.0
        _BlendExponent("Blend Exponent", Range(1, 8)) = 1
        _Glossiness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0.0
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
            float3 baryCoord : TEXCOORD0;
            float3 matData : TEXCOORD1;
            float3 localCoord : POSITION;
            float3 localNormal : NORMAL;
        };

        UNITY_DECLARE_TEX2D(_BaseTex);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_TopTex);
        UNITY_DECLARE_TEX2DARRAY(_MatTex);
        half _TexScale;
        half _BaryOffset;
        half _BlendOffset;
        half _BlendExponent;
        half _Glossiness;
        half _Metallic;

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            data.baryCoord = v.texcoord.xyz;
            data.matData = v.texcoord1.xyz;
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
            half4 xColExtra = (0, 0, 0, 0);
            half4 yColExtra = (0, 0, 0, 0);
            half4 zColExtra = (0, 0, 0, 0);
            if (IN.matData.x != 0)
            {
                half4 xBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(xUV, IN.matData.x));
                half4 yBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(yUV, IN.matData.x));
                half4 zBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(zUV, IN.matData.x));
                xColExtra += xBlend.rgba * round(IN.baryCoord.x + _BaryOffset);
                yColExtra += yBlend.rgba * round(IN.baryCoord.x + _BaryOffset);
                zColExtra += zBlend.rgba * round(IN.baryCoord.x + _BaryOffset);
            }
            if (IN.matData.y != 0)
            {
                half4 xBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(xUV, IN.matData.y));
                half4 yBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(yUV, IN.matData.y));
                half4 zBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(zUV, IN.matData.y));
                xColExtra += xBlend.rgba * round(IN.baryCoord.y + _BaryOffset);
                yColExtra += yBlend.rgba * round(IN.baryCoord.y + _BaryOffset);
                zColExtra += zBlend.rgba * round(IN.baryCoord.y + _BaryOffset);
            }
            if (IN.matData.z != 0)
            {
                half4 xBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(xUV, IN.matData.z));
                half4 yBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(yUV, IN.matData.z));
                half4 zBlend = UNITY_SAMPLE_TEX2DARRAY(_MatTex, float3(zUV, IN.matData.z));
                xColExtra += xBlend.rgba * round(IN.baryCoord.z + _BaryOffset);
                yColExtra += yBlend.rgba * round(IN.baryCoord.z + _BaryOffset);
                zColExtra += zBlend.rgba * round(IN.baryCoord.z + _BaryOffset);
            }
            xColExtra = saturate(xColExtra);
            yColExtra = saturate(yColExtra);
            zColExtra = saturate(zColExtra);
            xCol = xCol * (1 - xColExtra.a) + xColExtra * xColExtra.a;
            yCol = yCol * (1 - yColExtra.a) + yColExtra * yColExtra.a;
            zCol = zCol * (1 - zColExtra.a) + zColExtra * zColExtra.a;
            float3 blendWeight = abs(normalize(IN.localNormal));
            //blendWeight = saturate(blendWeight - _BlendOffset);
            //blendWeight = pow(blendWeight, _BlendExponent);
            //blendWeight /= (blendWeight.x + blendWeight.y + blendWeight.z);
            if (blendWeight.x > blendWeight.y) blendWeight.y = 0; else blendWeight.x = 0;
            if (blendWeight.x > blendWeight.z) blendWeight.z = 0; else blendWeight.x = 0;
            if (blendWeight.y > blendWeight.z) blendWeight.z = 0; else blendWeight.y = 0;
            blendWeight = ceil(blendWeight);

            o.Albedo = (xCol * blendWeight.x + yCol * blendWeight.y + zCol * blendWeight.z);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
