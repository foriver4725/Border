Shader "Hidden/foriver4725/Border"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 pos : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            half _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.pos);
                return o;
            }

            half4 frag (v2f _) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
