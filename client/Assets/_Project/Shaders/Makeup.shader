// Paints make-up onto a skin texture (character creator): the mask (tools/avatars/makeup.py) has lips in R, eyeshadow in
// G, blush in B and the lash line in A; each colour's alpha is how much. Lips and eyeshadow keep the skin's light and
// shade, blush is laid over like powder, eyeliner darkens the lash line.
Shader "Hidden/Reconnect/Makeup"
{
    Properties
    {
        _MainTex ("Skin", 2D) = "white" {}
        _Mask ("Mask", 2D) = "black" {}
        _Lips ("Lips", Color) = (0, 0, 0, 0)
        _Shadow ("Eyeshadow", Color) = (0, 0, 0, 0)
        _Blush ("Blush", Color) = (0, 0, 0, 0)
        _Liner ("Eyeliner", Color) = (0, 0, 0, 0)
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
            sampler2D _Mask;
            float4 _Lips;
            float4 _Shadow;
            float4 _Blush;
            float4 _Liner;

            float4 frag (v2f_img i) : SV_Target
            {
                float4 skin = tex2D(_MainTex, i.uv);
                float4 mask = tex2D(_Mask, i.uv);
                float luminance = dot(skin.rgb, float3(0.299, 0.587, 0.114));
                float3 colour = skin.rgb;
                colour = lerp(colour, _Lips.rgb * (0.55 + 0.9 * luminance), saturate(mask.r * _Lips.a));
                colour = lerp(colour, _Shadow.rgb * (0.55 + 0.8 * luminance), saturate(mask.g * _Shadow.a * 0.85));
                colour = lerp(colour, colour * _Blush.rgb * 1.5, saturate(mask.b * _Blush.a * 0.55));
                colour = lerp(colour, _Liner.rgb, saturate(mask.a * _Liner.a));
                return float4(colour, skin.a);
            }
            ENDCG
        }
    }
}
