#ifndef NINJA_NEXT_CORE_INCLUDED
#define NINJA_NEXT_CORE_INCLUDED

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

// --------------------------------------------------------------------------
// Uniform Properties (_SpecColor is provided by Lighting.cginc)
// --------------------------------------------------------------------------
half _Mode;

half4 _Color;
half4 _AmbientColor;
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _MainTex_TexelSize;

float _Unlit;
float _DisableFog;
float _AlphaTest;
float _Cutoff;
float _AlphaToMask;

float _VertexColorScale;
float _HDRIntensity;

// Multi-Texture (TexMaps)
sampler2D _MainTex2;
float4 _MainTex2_ST;
float _MainTex2BlendMode; // 0: Off, 1: Multiply, 2: Decal, 3: Replace, 4: Blend, 5: Add, 6: Subtract

sampler2D _MainTex3;
float4 _MainTex3_ST;
float _MainTex3BlendMode;

// Matcap
float _UseMatcap;
sampler2D _MatcapTex;
half4 _MatcapColor;
float4 _MatcapTex_TexelSize;
float _MatcapMode; // 0: Multiply, 1: Add, 2: Replace

// Normal Mapping
sampler2D _BumpMap;
float4 _BumpMap_ST;
float _BumpScale;

// Specular
float _Shininess;
sampler2D _SpecGlossMap;

// Emission
half4 _EmissionColor;
sampler2D _EmissionMap;
float4 _EmissionMap_ST;
float _EmissionPower;

// --------------------------------------------------------------------------
// Structs & Material Inputs
// --------------------------------------------------------------------------
struct MaterialInputs
{
    half4 baseColor;
    half3 normal;
    half3 specularColor;
    half smoothness;
    half3 emissive;
};

void initMaterial(out MaterialInputs material)
{
    material.baseColor = half4(1.0, 1.0, 1.0, 1.0);
    material.normal = half3(0.0, 0.0, 1.0);
    material.specularColor = half3(0.0, 0.0, 0.0);
    material.smoothness = 0.0;
    material.emissive = half3(0.0, 0.0, 0.0);
}

struct ShadingParams
{
    float3 position;
    half3 view;
    half3 normal;
    half3 reflected;
    half NoV;
    half attenuation;
};

struct appdata_nn
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1: TEXCOORD1;
    centroid half4 color    : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f_nn
{
    float4 pos          : SV_POSITION;
    float4 uv0          : TEXCOORD0; // xy: MainTex, zw: MainTex2
    float4 uv1          : TEXCOORD1; // xy: MainTex3, zw: BumpMap
    float4 worldPos     : TEXCOORD2; // xyz: worldPos, w: fogFactor
    float3 worldNormal  : TEXCOORD3;
    centroid half4 color        : COLOR;
    UNITY_SHADOW_COORDS(4)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// --------------------------------------------------------------------------
// Texture Blending Operations
// --------------------------------------------------------------------------
half3 ApplyTextureBlend(half3 baseCol, half4 layerCol, float mode)
{
    int blendMode = (int)mode;
    switch (blendMode)
    {
        case 1: return baseCol * layerCol.rgb;                             // Multiply
        case 2: return lerp(baseCol, layerCol.rgb, layerCol.a);            // Decal / Desert Terrain
        case 3: return layerCol.rgb;                                       // Replace
        case 4: return (1.0 - layerCol.a) * baseCol;                       // Inverse Alpha Modulate (SceneC1T2)
        case 5: return baseCol + layerCol.rgb;                             // Additive (SceneUserMatCallback_Desert)
        case 6: return max(half3(0.0, 0.0, 0.0), baseCol - layerCol.rgb);  // Subtract
        default: return baseCol;
    }
}

// --------------------------------------------------------------------------
// Fog Support
// --------------------------------------------------------------------------
void applyUnityFog(inout half3 col, float depth)
{
    #if defined(FOG_EXP2) || defined(FOG_EXP) || defined(FOG_LINEAR)
        half fogFactor = 1.0;
        #if defined(FOG_LINEAR)
            fogFactor = depth * unity_FogParams.z + unity_FogParams.w;
        #elif defined(FOG_EXP)
            fogFactor = exp2(-unity_FogParams.y * depth);
        #elif defined(FOG_EXP2)
            half expVal = unity_FogParams.x * depth;
            fogFactor = exp2(-expVal * expVal);
        #endif

        half3 appliedFogColor = unity_FogColor.rgb;
        #if defined(UNITY_PASS_FORWARDADD)
            appliedFogColor = fixed3(0, 0, 0);
        #endif

        col.rgb = lerp(appliedFogColor, col.rgb, saturate(fogFactor));
    #endif
}

// --------------------------------------------------------------------------
// Vertex Shader
// --------------------------------------------------------------------------

v2f_nn vert_nn(appdata_nn v)
{
    v2f_nn o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos.xyz = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldPos.w = o.pos.z;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    
    o.uv0.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.uv0.zw = TRANSFORM_TEX(v.texcoord.xy, _MainTex2);
    o.uv1.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex3);
    o.uv1.zw = TRANSFORM_TEX(v.texcoord.xy, _BumpMap);

    o.color = v.color;

    UNITY_TRANSFER_SHADOW(o, v.texcoord1);
    return o;
}

// --------------------------------------------------------------------------
// Material Setup
// --------------------------------------------------------------------------
// 
half2 getMatcapUVs(half3 normal, half3 viewDir)
{
    // Based on Masataka SUMI's implementation
    half3 worldUp = half3(0, 1, 0);
    half3 worldViewUp = normalize(worldUp - viewDir * dot(viewDir, worldUp));
    half3 worldViewRight = normalize(cross(viewDir, worldViewUp));
    return half2(dot(worldViewRight, normal), dot(worldViewUp, normal)) * 0.5 + 0.5;
}

float4 cubic(float v)
{
    float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
    float4 s = n * n * n;
    float x = s.x;
    float y = s.y - 4.0 * s.x;
    float z = s.z - 4.0 * s.y + 6.0 * s.x;
    float w = 6.0 - x - y - z;
    return float4(x, y, z, w);
}

float4 SampleBicubicBSpline(sampler2D tex, float4 texelSize, float2 uv)
{
    float2 coord = uv * texelSize.zw - 0.5;
    float fx = frac(coord.x);
    float fy = frac(coord.y);
    coord.x -= fx;
    coord.y -= fy;

    float4 xcubic = cubic(fx);
    float4 ycubic = cubic(fy);

    float4 c = float4(coord.x - 0.5, coord.x + 1.5, coord.y - 0.5, coord.y + 1.5);
    float4 s = float4(xcubic.x + xcubic.y, xcubic.z + xcubic.w, ycubic.x + ycubic.y, ycubic.z + ycubic.w);
    float4 offset = c + float4(xcubic.y, xcubic.w, ycubic.y, ycubic.w) / s;

    float4 sample0 = tex2D(tex, float2(offset.x, offset.z) * texelSize.xy);
    float4 sample1 = tex2D(tex, float2(offset.y, offset.z) * texelSize.xy);
    float4 sample2 = tex2D(tex, float2(offset.x, offset.w) * texelSize.xy);
    float4 sample3 = tex2D(tex, float2(offset.y, offset.w) * texelSize.xy);

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    return lerp(
        lerp(sample3, sample2, sx),
        lerp(sample1, sample0, sx), sy);
}

MaterialInputs MyMaterialSetup(v2f_nn i, bool isFrontFace)
{
    MaterialInputs material;
    initMaterial(material);

    //half4 mainTex = tex2D(_MainTex, i.uv0.xy);
    half4 mainTex = SampleBicubicBSpline(_MainTex, _MainTex_TexelSize, i.uv0.xy);
    half4 vcolor = i.color;
    vcolor.rgb *= _VertexColorScale;

    // Most materials are grey, anything brighter is glowy. So scale by grey. 
    float greyLevel = 150.0f / 255.0f;
    // Convert texture * material diffuse to gamma space for vertex color multiplication
    float3 linearToGamma = LinearToGammaSpace((mainTex * _Color).rgb);
    float3 gammaBlended = (linearToGamma * vcolor.rgb) / greyLevel;
    float3 gammaToLinear = GammaToLinearSpace(gammaBlended);

    // Doesn't apply since UNITY_HDR_ON is never set. 
#ifdef UNITY_HDR_ON
    float4 colorSpaceMult = unity_ColorSpaceDouble;
#else
    float4 colorSpaceMult = float4(1.0, 1.0, 1.0, 1.0);
#endif

    float hdrMult = _HDRIntensity * _EmissionPower;
    if (hdrMult <= 0.0) hdrMult = 1.0;

    float4 fullColorAlpha = (mainTex * _Color * vcolor);
    material.baseColor = float4(gammaToLinear * colorSpaceMult.rgb * hdrMult, fullColorAlpha.a);

    // 2. Multi-Texture Layers (TexMaps)
    if (_MainTex2BlendMode > 0.5)
    {
        half4 tex2 = tex2D(_MainTex2, i.uv0.zw);
        material.baseColor.rgb = ApplyTextureBlend(material.baseColor.rgb, tex2, _MainTex2BlendMode);
    }
    if (_MainTex3BlendMode > 0.5)
    {
        half4 tex3 = tex2D(_MainTex3, i.uv1.xy);
        material.baseColor.rgb = ApplyTextureBlend(material.baseColor.rgb, tex3, _MainTex3BlendMode);
    }

    // 3. Normal Vector Setup
    float3 N = normalize(i.worldNormal);
    if (!isFrontFace) N = -N;

    if (_BumpScale != 0.0)
    {
        half4 bumpTex = tex2D(_BumpMap, i.uv1.zw);
        float3 tangentNormal = UnpackScaleNormal(bumpTex, _BumpScale);
        N = normalize(N + tangentNormal * 0.5);
    }
    material.normal = N;

    // 4. Matcap Application
    if (_UseMatcap > 0.5)
    {
        float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
        float2 matcapUV = getMatcapUVs(N, viewDir);
        //half4 matcapCol = tex2D(_MatcapTex, matcapUV) * _MatcapColor;
        half4 matcapCol = SampleBicubicBSpline(_MatcapTex, _MatcapTex_TexelSize, matcapUV) * _MatcapColor;

        if (_MatcapMode < 0.5)
            material.baseColor.rgb *= matcapCol.rgb;
        else if (_MatcapMode < 1.5)
            material.baseColor.rgb += matcapCol.rgb * matcapCol.a;
        else
            material.baseColor.rgb = lerp(material.baseColor.rgb, matcapCol.rgb, matcapCol.a);
    }

    // 5. Specular Map & Shininess
    material.smoothness = _Shininess;
    half4 specTex = tex2D(_SpecGlossMap, i.uv0.xy);
    material.specularColor = _SpecColor.rgb * specTex.rgb;
    // Todo: Switch based on whether _SpecGlossMap exists. For PSU using the main tex looks more correct. 
    material.specularColor *= material.baseColor;

    // 6. Emission
    float maxEmisColor = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
    if (maxEmisColor > 0.001 || _EmissionPower > 1.0)
    {
        material.emissive = tex2D(_EmissionMap, TRANSFORM_TEX(i.uv0.xy, _EmissionMap)).rgb * _EmissionColor.rgb * _EmissionPower;
    }

    return material;
}

void applyAlphaMask(inout half4 baseColor)
{
    if (_AlphaTest > 0.5)
    {
        baseColor.a = saturate((baseColor.a - _Cutoff) / max(fwidth(baseColor.a), 0.0001) + 0.5);
    }
    clip(baseColor.a - (1.0/256.0));
}

// --------------------------------------------------------------------------
// Consolidated Fragment Lighting
// --------------------------------------------------------------------------
half4 FragNNCommon(v2f_nn i, bool isFrontFace, uniform bool isForwardAdd)
{
    UNITY_SETUP_INSTANCE_ID(i);

    MaterialInputs material = MyMaterialSetup(i, isFrontFace);

    applyAlphaMask(material.baseColor);
    
    float fogDepth = distance(_WorldSpaceCameraPos, i.worldPos);

    ShadingParams shading = (ShadingParams)0;
    shading.position = i.worldPos;
    shading.view = normalize(_WorldSpaceCameraPos - i.worldPos);
    shading.normal = material.normal;
    shading.reflected = reflect(-shading.view, shading.normal);

    UNITY_LIGHT_ATTENUATION(atten, i, shading.position);
    shading.attenuation = atten;

    float3 L = normalize(UnityWorldSpaceLightDir(shading.position));
    float NdotL = max(0.0, dot(shading.normal, L));
    
    half3 baseBrightness = _LightColor0.rgb;
    // Todo: Apply lighting to meshes with normals, but only apply color to meshes without.
    // * NdotL * shading.attenuation;
    // We don't have a good way of telling if a mesh had normals before importing though... 
    // Could send a material prop with the import data. But REALLY, there should be a material flag
    // that specifies whether a mesh is "unlit" or "lit" or not. 
    // I wonder if we could use the diffuse and ambient colour here. Problem is, the ambient colour and 
    // diffuse colour are always the same in the meshes I've looked at. 
    // The best choice is probably to fake it. 
    half3 diffuse = baseBrightness * lerp(0.5, 1.0, min(NdotL, shading.attenuation));

    half3 specular = half3(0, 0, 0);
    if (material.smoothness > 0.0)
    {
        float3 H = normalize(L + shading.view);
        float NdotH = max(0.0, dot(shading.normal, H));
        float specPower = exp2(material.smoothness * 10.0);
        specular = _LightColor0.rgb * material.specularColor * pow(NdotH, specPower) * shading.attenuation;
    }

    half3 ambient = ShadeSH9(half4(shading.normal, 1.0)) * _AmbientColor.rgb;

    // Todo: In cg0005, there are reflections of cutout geometry using additive blending, but they 
    // should have the cutout applied to them even though they're additive...

    float3 finalColor;
    bool isUnlit = (_Unlit > 0.5);
    bool noFog = (_DisableFog > 0.5);
    
    if (isUnlit)
    {
    /*
        if (isForwardAdd)
        {
            return half4(0, 0, 0, material.baseColor.a);
        }
        half3 finalRGB = baseBrightness * material.baseColor.rgb + material.emissive;
        if (!noFog) applyUnityFog(finalRGB, fogDepth);
        return half4(finalRGB, material.baseColor.a);
    */    
        diffuse = baseBrightness;
        ambient = 0;
    }
    
    if (isForwardAdd)
    {
        finalColor = material.baseColor.rgb * diffuse + specular;
        if (!noFog) applyUnityFog(finalColor, fogDepth);
        return half4(finalColor, material.baseColor.a);
    };
    
    finalColor = material.baseColor.rgb * (ambient + diffuse) + specular + material.emissive;
    if (!noFog) applyUnityFog(finalColor, fogDepth);
    return half4(finalColor, material.baseColor.a);
}

half4 fragBase(v2f_nn i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    return FragNNCommon(i, isFrontFace, false);
}

half4 fragAdd(v2f_nn i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    return FragNNCommon(i, isFrontFace, true);
}

#endif // NINJA_NEXT_CORE_INCLUDED