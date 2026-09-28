// Recolours a garment or hair texture to a tint while keeping its light and shade: the texel's brightness relative to the
// texture's average (lowest mip) scales the tint. Used once per (texture, tint) by the character creator (Graphics.Blit).
Shader "Hidden/Reconnect/Recolour"
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
                float4 average = tex2Dlod(_MainTex, float4(0.5, 0.5, 0, 12));
                float luminance = dot(texel.rgb, float3(0.299, 0.587, 0.114));
                float mean = max(dot(average.rgb, float3(0.299, 0.587, 0.114)), 0.04);
                float shade = saturate(luminance / mean * 0.5) * 2.0;
                return float4(saturate(_Tint.rgb * shade), texel.a);
            }
            ENDCG
        }
    }
}
