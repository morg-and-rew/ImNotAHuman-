Shader "Custom/InteractableOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.5, 0.2, 0.8, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.15)) = 0.03
    }
    SubShader
    {
        // Рисуем обводку ДО основного меша (Geometry-10), чтобы в билде контур не перекрывал объект (роутер/радио)
        Tags { "Queue" = "Geometry-10" "RenderType" = "Opaque" }
        Cull Front
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                float4 worldVertex = mul(unity_ObjectToWorld, v.vertex);
                // Inverse transpose для нормалей — при неоднородном масштабе контур не «рвётся»
                float3 worldNormal = normalize(mul(norm, (float3x3)unity_WorldToObject));
                worldVertex.xyz += worldNormal * _OutlineWidth;
                o.pos = mul(unity_MatrixVP, worldVertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    Fallback Off
}
