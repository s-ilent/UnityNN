Shader "NinjaNext/Standard"
{
    Properties
    {
        // Render Pipeline State Controls
        _Mode ("Rendering Mode", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Operation", Float) = 0.0
        [Enum(Off,0,On,1)] _ZWrite ("Depth Write", Float) = 1.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0
        [Queue]_CustomRenderQueue ("Custom Render Queue", Float) = -1.0

        // Lighting Mode
        [ToggleUI] _Unlit ("Unlit (Disable Lighting)", Float) = 0.0

        // Surface & Alpha
        _AlphaTest ("Enable Alpha Test", Float) = 0.0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [ToggleUI] _AlphaToMask ("Alpha to Coverage", Float) = 0.0
        _Color ("Main Color", Color) = (1,1,1,1)
        _AmbientColor ("Ambient Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _VertexColorScale ("Vertex Color Ambient Multiplier", Float) = 1.0
        _HDRIntensity ("HDR Intensity", Float) = 1.0

        // Matcap
        _UseMatcap ("Enable Matcap", Float) = 0.0
        _MatcapTex ("Matcap Texture", 2D) = "black" {}
        _MatcapColor ("Matcap Color", Color) = (1,1,1,1)
        [Enum(Multiply,0,Add,1,Replace,2)] _MatcapMode ("Matcap Mode", Float) = 0.0

        // Multi-Texture Layers (TexMaps)
        _MainTex2 ("TexMap Layer 2", 2D) = "white" {}
        [Enum(Off,0,Multiply,1,Decal,2,Replace,3,Blend,4,Add,5,Subtract,6)] 
        _MainTex2BlendMode ("Layer 2 Blend Mode", Float) = 0.0

        _MainTex3 ("TexMap Layer 3", 2D) = "white" {}
        [Enum(Off,0,Multiply,1,Decal,2,Replace,3,Blend,4,Add,5,Subtract,6)] 
        _MainTex3BlendMode ("Layer 3 Blend Mode", Float) = 0.0

        // Bump / Normal Map
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 0.0

        // Specular & Shininess
        _SpecColor ("Specular Color", Color) = (0, 0, 0, 1)
        _SpecGlossMap ("Specular Map", 2D) = "white" {}
        _Shininess ("Shininess / Power", Range(0, 1)) = 0.0

        // Emission
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionPower ("Emission Multiplier / HDR", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            BlendOp [_BlendOp]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            AlphaToMask [_AlphaToMask]

            CGPROGRAM
            #pragma vertex vert_nn
            #pragma fragment fragBase
            #pragma target 4.0

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "NN_Core.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }

            Blend [_SrcBlend] One
            BlendOp [_BlendOp]
            ZWrite Off
            ZTest LEqual
            AlphaToMask [_AlphaToMask]

            CGPROGRAM
            #pragma vertex vert_nn
            #pragma fragment fragAdd
            #pragma target 4.0

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #include "NN_Core.cginc"
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual

            CGPROGRAM
            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster
            #pragma target 4.0

            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct v2f_shadow
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            half4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaTest;
            float _Cutoff;

            v2f_shadow vertShadowCaster(appdata_base v)
            {
                v2f_shadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 fragShadowCaster(v2f_shadow i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv) * _Color;
                if (_AlphaTest > 0.5)
                {
                    clip(col.a - _Cutoff);
                }
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma target 4.0

            #include "UnityCG.cginc"

            struct v2f_depth
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            half4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaTest;
            float _Cutoff;

            v2f_depth vertDepth(appdata_base v)
            {
                v2f_depth o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            half4 fragDepth(v2f_depth i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv) * _Color;
                if (_AlphaTest > 0.5)
                {
                    clip(col.a - _Cutoff);
                }
                return 0;
            }
            ENDCG
        }
    }

    CustomEditor "SilentTools.NinjaNextShaderGUI"
}