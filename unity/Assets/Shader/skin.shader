Shader "Custom/skin"
{
    Properties
    {
        _MainTex ("Base (Diffuse)", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _AOTex ("AO", 2D) = "white" {}
        _SkinColor ("Skin Tint", Color) = (1, 0.9, 0.85, 1)
        _RimColor ("Rim Light Color", Color) = (1, 0.8, 0.7, 1)
        _RimPower ("Rim Light Sharpness", Range(1, 10)) = 4
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _AOTex;

        fixed4 _SkinColor;
        fixed4 _RimColor;
        float _RimPower;
        float _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex);
            fixed ao = tex2D(_AOTex, IN.uv_MainTex).r;
            fixed4 normalSample = tex2D(_NormalMap, IN.uv_MainTex);

            // �⺻ ���� AO, Skin Tint ����
            o.Albedo = albedo.rgb * ao * _SkinColor.rgb;

            // �ݼӼ� ����
            o.Metallic = 0.0;

            // �ε巯�� �Ǻ� ����
            o.Smoothness = _Smoothness;

            // ��� ����
            o.Normal = UnpackNormal(normalSample);

            // Rim Light (������) ȿ�� �߰�
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            rim = pow(rim, _RimPower);
            o.Emission = _RimColor.rgb * rim * 0.4;
        }
        ENDCG
    }
    FallBack "Diffuse"
}