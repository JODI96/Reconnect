// Tints the iris of an eye texture (its alpha is the iris mask, see tools/avatars/makeup.py) to any colour, keeping the
// fibres: the texel's brightness relative to the iris average shades the colour. The pupil and the white stay.
Shader "Hidden/Reconnect/Iris"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tint;

            float4 frag (v2f_img i) : SV_Target
            {
                float4 texel = tex2D(_MainTex, i.uv);
                float luminance = dot(texel.rgb, float3(0.299, 0.587, 0.114));
                float shade = clamp(luminance / 0.22, 0.25, 1.6);
                float3 iris = saturate(_Tint.rgb * shade);
                return float4(lerp(texel.rgb, iris, texel.a), 1);
            }
            ENDCG
        }
    }
}
