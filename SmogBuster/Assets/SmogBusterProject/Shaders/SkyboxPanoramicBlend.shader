// Skybox panorámico (equirectangular) que mezcla dos cielos: con smog y limpio.
// _Blend = 0 muestra solo el cielo con smog; 1, solo el limpio. Lo controla SkyPurification.
// _CleanHorizonClamp oculta el paisaje del HDRI limpio cerca del horizonte.
// Basado en el Skybox/Panoramic integrado de Unity (modo latitud-longitud, 360°).
Shader "SmogBuster/Skybox/Panoramic Blend"
{
    Properties
    {
        [NoScaleOffset] _SmogTex ("Cielo con smog (HDR)", 2D) = "grey" {}
        _SmogTint ("Tinte con smog", Color) = (0.5, 0.5, 0.5, 0.5)
        [NoScaleOffset] _CleanTex ("Cielo limpio (HDR)", 2D) = "grey" {}
        _CleanTint ("Tinte limpio", Color) = (0.5, 0.5, 0.5, 0.5)
        _CleanHorizonClamp ("Recorte del horizonte limpio (grados)", Range(0, 45)) = 0
        _Blend ("Mezcla (0 smog, 1 limpio)", Range(0, 1)) = 0
        [Gamma] _Exposure ("Exposición", Range(0, 8)) = 1
        _Rotation ("Rotación", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _SmogTex;
            half4 _SmogTex_HDR;
            half4 _SmogTint;
            sampler2D _CleanTex;
            half4 _CleanTex_HDR;
            half4 _CleanTint;
            half _CleanHorizonClamp;
            half _Blend;
            half _Exposure;
            float _Rotation;

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            float2 ToRadialCoords(float3 coords)
            {
                float3 n = normalize(coords);
                float latitude = acos(n.y);
                float longitude = atan2(n.z, n.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = ToRadialCoords(i.texcoord);

                half3 smog = DecodeHDR(tex2D(_SmogTex, uv), _SmogTex_HDR) * _SmogTint.rgb;
                // Por debajo de _CleanHorizonClamp grados se repite la franja de cielo de esa altura,
                // para ocultar el paisaje fotografiado (árboles, suelo) de un HDRI que no es solo cielo
                float2 cleanUv = float2(uv.x, max(uv.y, 0.5 + _CleanHorizonClamp / 180.0));
                half3 clean = DecodeHDR(tex2D(_CleanTex, cleanUv), _CleanTex_HDR) * _CleanTint.rgb;

                half3 c = lerp(smog, clean, _Blend) * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
