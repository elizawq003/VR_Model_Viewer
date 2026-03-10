Shader "Custom/JellyShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.3, 0.5, 0.6)
        _MainTex ("Texture", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0.1
        _Smoothness ("Smoothness", Range(0,1)) = 1.0
        _FresnelPower ("Edge Glow Strength", Range(0.5, 5)) = 2.0
        _FresnelColor ("Edge Glow Color", Color) = (1, 0.6, 0.8, 1)
        _Thickness ("Thickness", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _FresnelColor;
        half _Metallic;
        half _Smoothness;
        half _FresnelPower;
        half _Thickness;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Fresnel effect - edges glow like light passing through jelly
            float fresnel = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            // Make it work for back faces too (double sided)
            fresnel = max(fresnel, 1.0 - saturate(dot(normalize(IN.viewDir), -normalize(IN.worldNormal))));
            fresnel = pow(fresnel, _FresnelPower);

            // Mix base color with edge glow
            float3 finalColor = lerp(c.rgb, _FresnelColor.rgb, fresnel * 0.7);

            o.Albedo = finalColor;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            // Edges more opaque, center more transparent (like real jelly)
            o.Alpha = lerp(c.a * _Thickness, 0.95, fresnel * 0.6);
            // Add glow
            o.Emission = _FresnelColor.rgb * fresnel * 0.3;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
