### Shader Architecture

The codebase consists of four main sub-systems across 49 shader files:
1. **Scene Master Shader (`Scene*.msasm`)** — A modular scene pipeline supporting dynamic texture counts (`T0`, `T1`, `T2`), lighting modes (`C1`, `C2`), per-vertex directional and ambient lighting (`N`), desert terrain blending, user material callbacks, procedural UV animation, and projective shadow mapping with 4-tap PCF (Percentage-Closer Filtering) and distance fade.
2. **Post-Processing Pipeline (`Glare*.msasm`, `VelocityMap.msasm`)** — Glare extraction (8-tap brightpass max downsampler), streak blurring (4-tap directional ray filter), and temporal motion blur (5-tap velocity accumulator).
3. **Shadow Pass Shaders (`ShadowPlayer*.msasm`)** — Depth pass shaders for rigid and 4-bone skinned character models outputting normalized linear depth ($z/w$).
4. **Velocity Pass Shaders (`VelocityPlayer*.msasm`)** — Motion vector pass shaders for rigid and skinned character models computing dual-frame world positions, silhouette extrusion backface tests, and screen-space NDC velocity vectors.

---

### 1. Scene Master Pixel Shader (`Scene.hlsl`)

This single master pixel shader reproduces all 17 `Scene*_pso` variants using preprocessor directives (`NUM_TEXTURES`, `COLOR_MODE`, `IS_DESERT`, `IS_CALLBACK`, `IS_CALLBACK_DESERT`, `IS_NORMAL`, `SIMPLE_WHITE_PASS`).

```hlsl
// Scene.hlsl - Master Scene Pixel Shader
// Configuration combinations:
//   NUM_TEXTURES = 0, 1, 2
//   COLOR_MODE   = 1, 2
//   IS_DESERT, IS_CALLBACK, IS_CALLBACK_DESERT, IS_NORMAL, SIMPLE_WHITE_PASS

sampler2D s0 : register(s0); // Base / Diffuse Texture 0
sampler2D s1 : register(s1); // Diffuse Texture 1 / Detail Map / Terrain Blend
sampler2D s2 : register(s2); // Normal Map / Secondary Shadow Sampler (SceneN)
sampler2D s3 : register(s3); // Shadow Map Depth Sampler

struct PS_INPUT
{
    float4 color0    : COLOR0;
    float4 color1    : COLOR1;
    float4 texCoord0 : TEXCOORD0; // Diffuse UV 0 or Receiver Depth (z, w) in T0
    float4 texCoord1 : TEXCOORD1; // Diffuse UV 1 or Receiver Depth in T1 / Shadow Proj UV in T0
    float4 texCoord2 : TEXCOORD2; // Receiver Depth in T2 / Shadow Proj UV in T1
    float4 texCoord3 : TEXCOORD3; // Shadow Proj UV in T2
};

// 4-Tap PCF Shadow Evaluation Helper
// Returns total shadow factor in [0.0, 1.0] (0.0 = fully lit, > 0.0 = in shadow)
float EvaluateShadow(float4 shadowUV, float depthZ, float depthW)
{
    float4 uvs[4];
    uvs[0] = shadowUV;
    uvs[1] = shadowUV + float4(-0.1f, -0.1f, 0.0f, 0.0f);
    uvs[2] = shadowUV + float4( 0.1f, -0.1f, 0.0f, 0.0f);
    uvs[3] = shadowUV + float4(-0.1f, -0.3f, 0.0f, 0.0f);

    float shadowFactor = 0.0f;

    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        float shadowDepth = tex2Dproj(s3, uvs[i]).r;
        float depthDiff = shadowDepth * depthW - depthZ;

        // Penumbra calculation: clamp lower bound to -1.6f
        float penumbra = max(depthDiff, -1.6f);
        penumbra = (penumbra * (2.0f / 3.0f) + 1.0f) * 0.08f;

        // Shadow contribution only when receiver is behind caster (depthDiff <= 0)
        float sampleShadow = (depthDiff <= 0.0f) ? penumbra : 0.0f;
        shadowFactor += sampleShadow;
    }

    return shadowFactor;
}

float4 main(PS_INPUT input) : COLOR
{
#if defined(SIMPLE_WHITE_PASS) // SceneTex0, SceneTex1, SceneTex2, SceneNrm
    return float4(1.0f, 1.0f, 1.0f, 1.0f);
#else

    // 1. Unpack Receiver Depth and Projective Shadow UV Coordinates
    #if NUM_TEXTURES == 0
        float depthZ = input.texCoord0.z;
        float depthW = input.texCoord0.w;
        float4 shadowUV = input.texCoord1;
    #elif NUM_TEXTURES == 1
        float depthZ = input.texCoord1.z;
        float depthW = input.texCoord1.w;
        float4 shadowUV = input.texCoord2;
    #else // NUM_TEXTURES == 2
        float depthZ = input.texCoord2.z;
        float depthW = input.texCoord2.w;
        float4 shadowUV = input.texCoord3;
    #endif

    // 2. Base Color & Texture Sampling
    float4 baseColor = float4(1.0f, 1.0f, 1.0f, 1.0f);

    #if NUM_TEXTURES == 0
        #if COLOR_MODE == 2
            baseColor = input.color0 * input.color1; // SceneC2T0
        #else
            baseColor = input.color0;                // SceneC1T0
        #endif

    #elif NUM_TEXTURES == 1
        float4 tex0 = tex2D(s0, input.texCoord0.xy);
        baseColor = tex0 * input.color0;             // SceneC1T1, SceneC2T1, SceneC2T1Add, SceneT1

    #elif NUM_TEXTURES == 2
        float4 tex0 = tex2D(s0, input.texCoord0.xy);

        #if defined(IS_CALLBACK)
            // SceneUserMatCallback: s0 sampled twice at UV0 and UV1
            float4 tex1_s0 = tex2D(s0, input.texCoord1.xy);
            baseColor.rgb = tex0.rgb * input.color0.rgb * tex1_s0.a;
            baseColor.a   = tex1_s0.a * input.color0.a;

        #elif defined(IS_CALLBACK_DESERT)
            // SceneUserMatCallback_Desert: Additive blend with s1 and s0 at UV1
            float4 tex1_s1 = tex2D(s1, input.texCoord1.xy);
            float4 tex1_s0 = tex2D(s0, input.texCoord1.xy);
            baseColor.rgb = tex0.rgb * input.color0.rgb + tex1_s1.rgb;
            baseColor.a   = tex1_s0.a * input.color0.a;

        #elif defined(IS_DESERT) || COLOR_MODE == 2
            // SceneC1T2_Desert, SceneC2T2: Terrain alpha blend lerp(tex0, tex1, tex1.a)
            float4 tex1 = tex2D(s1, input.texCoord1.xy);
            float3 blendedRGB = lerp(tex0.rgb, tex1.rgb, tex1.a);
            baseColor = float4(blendedRGB, tex0.a) * input.color0;

        #else
            // SceneC1T2: Alpha inverted modulation with MaterialSpecular.w
            float4 tex1 = tex2D(s1, input.texCoord1.xy);
            float3 blendedRGB = (1.0f - tex1.a) * tex0.rgb * input.color1.w;
            baseColor = float4(blendedRGB * input.color0.rgb, input.color0.a);
        #endif
    #endif

    // 3. Modulate Lighting with Shadow
    #if defined(IS_NORMAL)
        // Subtractive shadow attenuation for per-vertex lighting (SceneN, SceneT1N)
        #if NUM_TEXTURES == 1
            float4 tex0 = tex2D(s0, input.texCoord0.xy);
            float3 litSurface = tex0.rgb * input.color0.rgb;
            float litAlpha = tex0.a * input.color0.a;
        #else
            float3 litSurface = input.color0.rgb;
            float litAlpha = input.color0.a;
        #endif

        float shadowFactor = EvaluateShadow(shadowUV, depthZ, depthW);
        return float4(litSurface - shadowFactor, litAlpha);

    #else
        // Multiplicative shadow attenuation with distance-based fade check
        float shadowFactor = EvaluateShadow(shadowUV, depthZ, depthW);
        float shadowMultiplier = 1.0f - shadowFactor;

        float4 finalColor;
        finalColor.rgb = shadowMultiplier * baseColor.rgb;
        finalColor.a   = baseColor.a;

        // Shadow distance fade test: depthZ - 0.85 * depthW
        // If outside shadow range (depthZ < 0.85 * depthW), revert to unshadowed base color
        if (depthZ - 0.85f * depthW < 0.0f)
        {
            finalColor.rgb = baseColor.rgb;
        }

        return finalColor;
    #endif
#endif
}
```

---

### 2. Post-Processing Shaders

#### `GlareFirstStage.hlsl` (8-Tap Brightpass / Max Downsampler)
```hlsl
sampler2D s0 : register(s0);

cbuffer GlareOffsets : register(c0)
{
    float4 SampleOffsets[8]; // c0..c7: 8 UV sample offsets
};

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float4 maxColor = float4(0.0f, 0.0f, 0.0f, 0.0f);

    [unroll]
    for (int i = 0; i < 8; ++i)
    {
        float4 sampleColor = tex2D(s0, uv - SampleOffsets[i].xy);
        maxColor = max(maxColor, sampleColor);
    }

    return maxColor;
}
```

#### `GlareSecondStage.hlsl` (Directional Glare Blur / Streak Filter)
```hlsl
sampler2D s0 : register(s0); // Glare Vector Map
sampler2D s1 : register(s1); // Scene Color / Downsampled Glare

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float4 dir = tex2D(s0, uv);
    float2 offset = dir.xy - 0.5f;

    float2 uv0 = uv;
    float2 uv1 = offset * (1.0f / 3.0f) + uv;
    float2 uv2 = offset * (2.0f / 3.0f) + uv;
    float2 uv3 = offset * 1.0f + uv;

    float4 sum = tex2D(s1, uv0)
               + tex2D(s1, uv1)
               + tex2D(s1, uv2)
               + tex2D(s1, uv3);

    return sum * 0.25f; // 4-tap average (1/4)
}
```

#### `VelocityMap.hlsl` (Motion Blur / Velocity Accumulation)
```hlsl
sampler2D s0 : register(s0); // Scene Color Texture
sampler2D s1 : register(s1); // Velocity Vector Map

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float2 vel = tex2D(s1, uv).xy - 0.5f;

    float2 uv0 = uv;
    float2 uv1 = vel * (1.0f / 9.0f) + uv;
    float2 uv2 = vel * (2.0f / 9.0f) + uv;
    float2 uv3 = vel * (3.0f / 9.0f) + uv;
    float2 uv4 = vel * (4.0f / 9.0f) + uv;

    float4 colorAccum = tex2D(s0, uv0)
                      + tex2D(s0, uv1)
                      + tex2D(s0, uv2)
                      + tex2D(s0, uv3)
                      + tex2D(s0, uv4);

    return colorAccum * 0.20f; // 5-tap average (1/5)
}
```

---

### 3. Shadow & Velocity Pass Pixel Shaders

#### `ShadowPlayerBone.hlsl` / `ShadowPlayerRigid.hlsl` / `VelocityPlayer.hlsl`
Computes normalized screen-space linear depth ($z/w$) across all four channels:
```hlsl
float4 main(float4 depthCoord : TEXCOORD1) : COLOR
{
    float linearDepth = depthCoord.z / depthCoord.w;
    return float4(linearDepth, linearDepth, linearDepth, linearDepth);
}
```

#### `VelocityPlayerBone.hlsl` / `VelocityPlayerRigid.hlsl`
Outputs screen-space motion/velocity vector coordinates directly:
```hlsl
float4 main(float4 velocity : TEXCOORD1) : COLOR
{
    return velocity;
}
```

---

### 4. Post-Processing Vertex Shaders (`Glare*.hlsl`, `VelocityMap_vso.hlsl`)

Pass-through vertex shaders used for screen-space quad rendering.

```hlsl
// PostProcess_VS.hlsl - Vertex Shader for Glare & Motion Blur Passes
struct VS_INPUT
{
    float4 position : POSITION;
    float4 color    : COLOR0;
    float4 texCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 color    : COLOR0;
    float4 texCoord : TEXCOORD0;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;
    output.position = input.position;
    output.color    = input.color;
    output.texCoord = input.texCoord;
    return output;
}
```

---

### 5. Scene Master Vertex Shader (`Scene_VS.hlsl`)

Handles all vertex transformations across all `Scene*_vso` variants:
- World-View-Projection matrix transformation (`c12..c15`).
- Light-space shadow projection transformations (`c0..c3` & `c4..c7`).
- Per-vertex dual directional + ambient lighting (`SceneN_vso`, `SceneT1N_vso`).
- Procedural UV animation scrolling via `c33`.

```hlsl
// Scene_VS.hlsl - Master Scene Vertex Shader
cbuffer MatrixBuffer : register(c0)
{
    float4x4 ShadowMatrix0;   // c0..c3   - Light Space Matrix 0 (Receiver Depth)
    float4x4 ShadowMatrix1;   // c4..c7   - Light Space Matrix 1 (Projective Shadow UV)
    float4x4 WorldViewProj;   // c12..c15 - World-View-Projection Matrix
    float3x3 WorldMatrix;     // c16..c18 - World Matrix (for Normal Lighting)
    float4 MaterialColor;     // c28      - Base Material Color Multiplier
    float4 MaterialSpecular;  // c29      - Specular / Ambient Constant
    float4 UVScrollOffset;    // c33      - UV Animation Offset (xy: Stage 0, zw: Stage 1)
};

cbuffer LightBuffer : register(c39)
{
    float4 LightColor0;      // c39      - Directional Light 0 Color
    float4 LightColor1;      // c41      - Directional Light 0 Secondary/Ambient Color
    float3 LightDir0;        // -c43     - Directional Light 0 Vector
    float4 LightColor2;      // c46      - Directional Light 1 Color
    float3 LightAmbient;     // c48      - Ambient Light Color
    float3 LightDir1;        // -c50     - Directional Light 1 Vector
};

struct VS_INPUT
{
    float4 position : POSITION;
    float4 color    : COLOR0;
    float4 specular : COLOR1;
    float3 normal   : NORMAL;
    float2 uv0      : TEXCOORD0;
    float2 uv1      : TEXCOORD1;
};

struct VS_OUTPUT
{
    float4 position   : POSITION;
    float4 color0     : COLOR0;
    float4 color1     : COLOR1;
    float4 texCoord0  : TEXCOORD0; // UV 0 (Scrolled) or Shadow Matrix 0
    float4 texCoord1  : TEXCOORD1; // UV 1 (Scrolled) or Shadow Matrix 1
    float4 shadowTex0 : TEXCOORD2; // Light-space Shadow Map Coord 0
    float4 shadowTex1 : TEXCOORD3; // Light-space Shadow Map Coord 1
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output = (VS_OUTPUT)0;

    // 1. Position Transformation
    output.position = mul(WorldViewProj, input.position);

    // 2. Per-Vertex Color & Lighting Computation
#if defined(PER_VERTEX_LIGHTING) // SceneN_vso, SceneT1N_vso
    float3 worldNormal = normalize(mul(WorldMatrix, input.normal));

    float NdotL0 = max(dot(worldNormal, LightDir0), 0.0f);
    float NdotL1 = max(dot(worldNormal, LightDir1), 0.0f);

    float4 diffuse = LightColor0 * NdotL0;
    diffuse.rgb += LightColor1.rgb;
    diffuse.rgb += LightAmbient.rgb;
    output.color0 = LightColor2 * NdotL1 + diffuse;

    #if NUM_TEXTURES == 1
        output.texCoord0  = float4(input.uv0 + UVScrollOffset.xy, 0.0f, 0.0f);
        output.shadowTex0 = mul(ShadowMatrix0, input.position);
        output.shadowTex1 = mul(ShadowMatrix1, input.position);
    #else
        output.texCoord0  = mul(ShadowMatrix0, input.position);
        output.texCoord1  = mul(ShadowMatrix1, input.position);
    #endif

#elif defined(NORMAL_PROJECTION_PASS) // SceneNrm_vso
    output.texCoord0.x = dot(input.normal, float3(c152.xyz));
    output.texCoord0.y = dot(input.normal, float3(c153.xyz));
    output.texCoord0.zw= float2(0.0f, 0.0f);
    output.shadowTex0  = mul(ShadowMatrix0, input.position);
    output.shadowTex1  = mul(ShadowMatrix1, input.position);
    output.color0      = float4(1.0f, 1.0f, 1.0f, 1.0f);

#else
    #if NUM_TEXTURES == 0
        output.texCoord0 = mul(ShadowMatrix0, input.position);
        output.texCoord1 = mul(ShadowMatrix1, input.position);
        output.color0    = input.color;
        #if COLOR_MODE == 2
            output.color1 = input.specular;
        #endif

    #elif NUM_TEXTURES == 1
        output.texCoord0  = float4(input.uv0 + UVScrollOffset.xy, 0.0f, 0.0f);
        output.shadowTex0 = mul(ShadowMatrix0, input.position);
        output.shadowTex1 = mul(ShadowMatrix1, input.position);
        #if defined(USE_MATERIAL_SPECULAR_COLOR) // SceneT1_vso
            output.color0 = MaterialSpecular;
        #else
            output.color0 = input.color;
        #endif

    #elif NUM_TEXTURES == 2
        output.texCoord0  = float4(input.uv0 + UVScrollOffset.xy, 0.0f, 0.0f);
        output.texCoord1  = float4(input.uv1 + UVScrollOffset.zw, 0.0f, 0.0f);
        output.shadowTex0 = mul(ShadowMatrix0, input.position);
        output.shadowTex1 = mul(ShadowMatrix1, input.position);

        #if defined(SCALED_MATERIAL_COLOR)
            output.color0 = input.color * MaterialColor;
        #else
            output.color0 = input.color;
        #endif

        #if COLOR_MODE == 2
            output.color1 = input.specular;
        #else
            output.color1 = MaterialSpecular;
        #endif
    #endif
#endif

    return output;
}
```

---

### 6. Rigid Shadow Map Generation (`ShadowPlayerRigid_VS.hlsl`)

```hlsl
// ShadowPlayerRigid_VS.hlsl - Rigid Mesh Shadow Map Vertex Shader
cbuffer ViewProjection : register(c12)
{
    float4x4 WorldViewProj; // c12..c15
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 shadowUV : TEXCOORD1;
    float4 dummyD0  : COLOR0;
    float4 dummyT0  : TEXCOORD0;
};

VS_OUTPUT main(float4 position : POSITION)
{
    VS_OUTPUT output;
    float4 clipPos = mul(WorldViewProj, position);

    output.position = clipPos;
    output.shadowUV = clipPos; // Depth (z/w) evaluation in pixel shader
    output.dummyD0  = float4(0.0f, 0.0f, 0.0f, 0.0f);
    output.dummyT0  = float4(0.0f, 0.0f, 0.0f, 0.0f);

    return output;
}
```

---

### 7. Skinned Character Shadow Map Generation (`ShadowPlayerBone_VS.hlsl`)

Computes 4-bone palette skinning with automatic 4th weight normalization and View-Projection transformation.

```hlsl
// ShadowPlayerBone_VS.hlsl - Skinned Bone Shadow Map Vertex Shader
cbuffer ViewProjection : register(c20)
{
    float4x4 ViewProjMatrix; // c20..c23
};

cbuffer MatrixPalette : register(c67)
{
    // 4 Bone matrices (each 4x3 row-major affine transform + translation)
    float4 BonePalette[16]; // c67..c82
};

struct VS_INPUT
{
    float4 position : POSITION;
    float3 weights  : BLENDWEIGHT; // w0, w1, w2
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 shadowUV : TEXCOORD1;
    float4 dummyD0  : COLOR0;
    float4 dummyT0  : TEXCOORD0;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;

    // Dynamic 4th weight calculation and threshold validation (threshold 0.9999f)
    float w0 = input.weights.x;
    float w1 = input.weights.y;
    float w2 = input.weights.z;
    float sumWeights = w0 + w1 + w2;
    float w3 = (sumWeights < 0.9999f) ? (1.0f - sumWeights) : 0.0f;

    // Bone 0 (c67..c70), Bone 1 (c71..c74), Bone 2 (c75..c78), Bone 3 (c79..c82)
    float4 row0 = BonePalette[0] * w0 + BonePalette[4] * w1 + BonePalette[8]  * w2 + BonePalette[12] * w3;
    float4 row1 = BonePalette[1] * w0 + BonePalette[5] * w1 + BonePalette[9]  * w2 + BonePalette[13] * w3;
    float4 row2 = BonePalette[2] * w0 + BonePalette[6] * w1 + BonePalette[10] * w2 + BonePalette[14] * w3;
    float3 row3_xyz = BonePalette[3].xyz * w0 + BonePalette[7].xyz * w1 + BonePalette[11].xyz * w2 + BonePalette[15].xyz * w3;
    float4 row3 = float4(row3_xyz, 1.0f);

    float4 skinnedWorldPos = float4(
        dot(input.position, row0),
        dot(input.position, row1),
        dot(input.position, row2),
        dot(input.position, row3)
    );

    float4 clipPos = mul(ViewProjMatrix, skinnedWorldPos);

    output.position = clipPos;
    output.shadowUV = clipPos;
    output.dummyD0  = float4(0.0f, 0.0f, 0.0f, 0.0f);
    output.dummyT0  = float4(0.0f, 0.0f, 0.0f, 0.0f);

    return output;
}
```

---

### 8. Velocity Motion Vector Shaders (`VelocityPlayerRigid_VS.hlsl` & `VelocityPlayerBone_VS.hlsl`)

#### `VelocityPlayerRigid_VS.hlsl` (Rigid Mesh Motion Vector Vertex Shader)
```hlsl
// VelocityPlayerRigid_VS.hlsl - Rigid Mesh Motion Vector Vertex Shader
cbuffer Transforms : register(c4)
{
    float3x3 NormalMatrix;    // c4..c6   - World Normal Matrix
    float4x4 CurrWorldMatrix; // c8..c11  - Current Frame World Matrix
    float4x4 PrevWorldMatrix; // c16..c19 - Previous Frame World Matrix
    float4x4 ViewProjMatrix;  // c20..c23 - View-Projection Matrix
};

struct VS_INPUT
{
    float4 position : POSITION;
    float3 normal   : NORMAL;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 velocity : TEXCOORD1; // Screen space velocity output
    float4 dummyD0  : COLOR0;
    float4 dummyT0  : TEXCOORD0;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;

    // 1. Current & Previous World Positions
    float4 currWorldPos = mul(CurrWorldMatrix, input.position);
    float4 prevWorldPos = mul(PrevWorldMatrix, input.position);

    // 2. Current & Previous Clip Space Positions
    float4 currClipPos = mul(ViewProjMatrix, currWorldPos);
    float4 prevClipPos = mul(ViewProjMatrix, prevWorldPos);

    // 3. Silhouette Edge Extrusion Test
    float3 worldDelta = currWorldPos.xyz - prevWorldPos.xyz;
    float3 worldNormal = mul(NormalMatrix, input.normal);
    float dotProduct = dot(worldDelta, worldNormal);

    output.position = (dotProduct > 0.0f) ? currClipPos : prevClipPos;

    // 4. Screen-Space NDC Velocity Encoding into [0, 1] offset space
    float2 currNDC = currClipPos.xy / currClipPos.w;
    float2 prevNDC = prevClipPos.xy / prevClipPos.w;

    output.velocity.xy = (currNDC - prevNDC) + 0.5f;
    output.velocity.z  = 0.0f;
    output.velocity.w  = 1.0f;

    output.dummyD0 = float4(0.0f, 0.0f, 0.0f, 0.0f);
    output.dummyT0 = float4(0.0f, 0.0f, 0.0f, 0.0f);

    return output;
}
```

#### `VelocityPlayerBone_VS.hlsl` (Skinned Character Motion Vector Vertex Shader)
```hlsl
// VelocityPlayerBone_VS.hlsl - Skinned Character Motion Vector Vertex Shader
cbuffer Transforms : register(c4)
{
    float3x3 NormalMatrix;        // c4..c6
    float4x4 ViewProjMatrix;      // c20..c23
    float4 PrevBonePalette[16];   // c39..c54 (Previous Frame 4-Bone Palette)
    float4 CurrBonePalette[16];   // c67..c82 (Current Frame 4-Bone Palette)
};

struct VS_INPUT
{
    float4 position : POSITION;
    float3 normal   : NORMAL;
    float3 weights  : BLENDWEIGHT;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 velocity : TEXCOORD1;
    float4 dummyD0  : COLOR0;
    float4 dummyT0  : TEXCOORD0;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;

    float w0 = input.weights.x;
    float w1 = input.weights.y;
    float w2 = input.weights.z;
    float sumWeights = w0 + w1 + w2;
    float w3 = (sumWeights < 0.9999f) ? (1.0f - sumWeights) : 0.0f;

    // --- 1. Current Frame Skinned Position ---
    float4 c_r0 = CurrBonePalette[0] * w0 + CurrBonePalette[4] * w1 + CurrBonePalette[8]  * w2 + CurrBonePalette[12] * w3;
    float4 c_r1 = CurrBonePalette[1] * w0 + CurrBonePalette[5] * w1 + CurrBonePalette[9]  * w2 + CurrBonePalette[13] * w3;
    float4 c_r2 = CurrBonePalette[2] * w0 + CurrBonePalette[6] * w1 + CurrBonePalette[10] * w2 + CurrBonePalette[14] * w3;
    float3 c_r3_xyz = CurrBonePalette[3].xyz * w0 + CurrBonePalette[7].xyz * w1 + CurrBonePalette[11].xyz * w2 + CurrBonePalette[15].xyz * w3;
    float4 c_r3 = float4(c_r3_xyz, 1.0f);

    float4 currWorldPos = float4(dot(input.position, c_r0),
                                 dot(input.position, c_r1),
                                 dot(input.position, c_r2),
                                 dot(input.position, c_r3));
    float4 currClipPos = mul(ViewProjMatrix, currWorldPos);

    // --- 2. Previous Frame Skinned Position ---
    float4 p_r0 = PrevBonePalette[0] * w0 + PrevBonePalette[4] * w1 + PrevBonePalette[8]  * w2 + PrevBonePalette[12] * w3;
    float4 p_r1 = PrevBonePalette[1] * w0 + PrevBonePalette[5] * w1 + PrevBonePalette[9]  * w2 + PrevBonePalette[13] * w3;
    float4 p_r2 = PrevBonePalette[2] * w0 + PrevBonePalette[6] * w1 + PrevBonePalette[10] * w2 + PrevBonePalette[14] * w3;
    float3 p_r3_xyz = PrevBonePalette[3].xyz * w0 + PrevBonePalette[7].xyz * w1 + PrevBonePalette[11].xyz * w2 + PrevBonePalette[15].xyz * w3;
    float4 p_r3 = float4(p_r3_xyz, 1.0f);

    float4 prevWorldPos = float4(dot(input.position, p_r0),
                                 dot(input.position, p_r1),
                                 dot(input.position, p_r2),
                                 dot(input.position, p_r3));
    float4 prevClipPos = mul(ViewProjMatrix, prevWorldPos);

    // --- 3. Silhouette Edge Extrusion Check ---
    float3 worldDelta = currWorldPos.xyz - prevWorldPos.xyz;
    float3 worldNormal = mul(NormalMatrix, input.normal);
    float dotProduct = dot(worldDelta, worldNormal);

    output.position = (dotProduct > 0.0f) ? currClipPos : prevClipPos;

    // --- 4. Motion Vector Output ---
    float2 currNDC = currClipPos.xy / currClipPos.w;
    float2 prevNDC = prevClipPos.xy / prevClipPos.w;

    output.velocity.xy = (currNDC - prevNDC) + 0.5f;
    output.velocity.z  = 0.0f;
    output.velocity.w  = 1.0f;

    output.dummyD0 = float4(0.0f, 0.0f, 0.0f, 0.0f);
    output.dummyT0 = float4(0.0f, 0.0f, 0.0f, 0.0f);

    return output;
}
```