Shader "Custom/InteractableOutlineShell"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.5, 0.2, 0.8, 1)
    }
    SubShader
    {
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

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
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
