using UnityEngine;
using UnityEditor;

namespace SilentTools
{
    public class NinjaNextShaderGUI : ShaderGUI
    {
        public enum RenderMode
        {
            Opaque,
            Cutout,
            Transparent,
            Fade,
            Additive,
            Multiply,
            ReverseSubtract,
            Custom
        }

        private MaterialProperty materialFlagsProp;
        private MaterialProperty materialTypeProp;
        private MaterialProperty userDefinedProp;

        private MaterialProperty modeProp;
        private MaterialProperty srcBlendProp;
        private MaterialProperty dstBlendProp;
        private MaterialProperty blendOpProp;
        private MaterialProperty zWriteProp;
        private MaterialProperty zTestProp;
        private MaterialProperty cullProp;
        private MaterialProperty customRenderQueueProp;

        private MaterialProperty unlitProp;
        private MaterialProperty disableFogProp;

        private MaterialProperty alphaTestProp;
        private MaterialProperty cutoffProp;
        private MaterialProperty alphaToMaskProp;

        private MaterialProperty colorProp;
        private MaterialProperty ambientColorProp;
        private MaterialProperty mainTexProp;
        private MaterialProperty vertexColorScaleProp;
        private MaterialProperty hdrIntensityProp;

        private MaterialProperty useMatcapProp;
        private MaterialProperty matcapTexProp;
        private MaterialProperty matcapColorProp;
        private MaterialProperty matcapModeProp;

        private MaterialProperty mainTex2Prop;
        private MaterialProperty mainTex2BlendModeProp;
        private MaterialProperty mainTex3Prop;
        private MaterialProperty mainTex3BlendModeProp;

        private MaterialProperty bumpMapProp;
        private MaterialProperty bumpScaleProp;

        private MaterialProperty specColorProp;
        private MaterialProperty specGlossMapProp;
        private MaterialProperty shininessProp;

        private MaterialProperty emissionColorProp;
        private MaterialProperty emissionMapProp;
        private MaterialProperty emissionPowerProp;

        public void FindProperties(MaterialProperty[] props)
        {
            materialFlagsProp = FindProperty("_MaterialFlags", props, false);
            materialTypeProp = FindProperty("_MaterialType", props, false);
            userDefinedProp = FindProperty("_UserDefined", props, false);

            modeProp = FindProperty("_Mode", props, false);
            srcBlendProp = FindProperty("_SrcBlend", props, false);
            dstBlendProp = FindProperty("_DstBlend", props, false);
            blendOpProp = FindProperty("_BlendOp", props, false);
            zWriteProp = FindProperty("_ZWrite", props, false);
            zTestProp = FindProperty("_ZTest", props, false);
            cullProp = FindProperty("_Cull", props, false);
            customRenderQueueProp = FindProperty("_CustomRenderQueue", props, false);

            unlitProp = FindProperty("_Unlit", props, false);
            disableFogProp = FindProperty("_DisableFog", props, false);

            alphaTestProp = FindProperty("_AlphaTest", props, false);
            cutoffProp = FindProperty("_Cutoff", props, false);
            alphaToMaskProp = FindProperty("_AlphaToMask", props, false);

            colorProp = FindProperty("_Color", props, false);
            ambientColorProp = FindProperty("_AmbientColor", props, false);
            mainTexProp = FindProperty("_MainTex", props, false);
            vertexColorScaleProp = FindProperty("_VertexColorScale", props, false);
            hdrIntensityProp = FindProperty("_HDRIntensity", props, false);

            useMatcapProp = FindProperty("_UseMatcap", props, false);
            matcapTexProp = FindProperty("_MatcapTex", props, false);
            matcapColorProp = FindProperty("_MatcapColor", props, false);
            matcapModeProp = FindProperty("_MatcapMode", props, false);

            mainTex2Prop = FindProperty("_MainTex2", props, false);
            mainTex2BlendModeProp = FindProperty("_MainTex2BlendMode", props, false);
            mainTex3Prop = FindProperty("_MainTex3", props, false);
            mainTex3BlendModeProp = FindProperty("_MainTex3BlendMode", props, false);

            bumpMapProp = FindProperty("_BumpMap", props, false);
            bumpScaleProp = FindProperty("_BumpScale", props, false);

            specColorProp = FindProperty("_SpecColor", props, false);
            specGlossMapProp = FindProperty("_SpecGlossMap", props, false);
            shininessProp = FindProperty("_Shininess", props, false);

            emissionColorProp = FindProperty("_EmissionColor", props, false);
            emissionMapProp = FindProperty("_EmissionMap", props, false);
            emissionPowerProp = FindProperty("_EmissionPower", props, false);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            FindProperties(props);
            Material material = materialEditor.target as Material;

            EditorGUI.BeginChangeCheck();

            // 2. Primary Surface
            EditorGUILayout.LabelField("Primary Surface", EditorStyles.boldLabel);
            if (mainTexProp != null && colorProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Main Texture"), mainTexProp, colorProp);
                if (ambientColorProp != null) materialEditor.ShaderProperty(ambientColorProp, "Ambient Color");
                materialEditor.TextureScaleOffsetProperty(mainTexProp);
            }

            if (vertexColorScaleProp != null)
            {
                materialEditor.ShaderProperty(vertexColorScaleProp, "Vertex Color Multiplier");
            }
            if (hdrIntensityProp != null)
            {
                materialEditor.ShaderProperty(hdrIntensityProp, "HDR Intensity");
            }

            if (alphaTestProp != null)
            {
                materialEditor.ShaderProperty(alphaTestProp, "Enable Alpha Testing");
                if (alphaTestProp.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    if (cutoffProp != null) materialEditor.ShaderProperty(cutoffProp, "Alpha Cutoff");
                    if (alphaToMaskProp != null) materialEditor.ShaderProperty(alphaToMaskProp, "Alpha to Coverage");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 3. Matcap Map
            EditorGUILayout.LabelField("Matcap Map", EditorStyles.boldLabel);
            if (useMatcapProp != null)
            {
                materialEditor.ShaderProperty(useMatcapProp, "Enable Matcap");
                if (useMatcapProp.floatValue > 0.5f && matcapTexProp != null && matcapColorProp != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TexturePropertySingleLine(new GUIContent("Matcap Texture"), matcapTexProp, matcapColorProp);
                    if (matcapModeProp != null) materialEditor.ShaderProperty(matcapModeProp, "Matcap Blend Mode");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 4. Multi-Texture Layers (TexMaps)
            EditorGUILayout.LabelField("Multi-Texturing (TexMaps)", EditorStyles.boldLabel);
            if (mainTex2Prop != null && mainTex2BlendModeProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Layer 2"), mainTex2Prop);
                materialEditor.ShaderProperty(mainTex2BlendModeProp, "Layer 2 Blend Mode");
                materialEditor.TextureScaleOffsetProperty(mainTex2Prop);
                EditorGUILayout.Space();
            }

            if (mainTex3Prop != null && mainTex3BlendModeProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Layer 3"), mainTex3Prop);
                materialEditor.ShaderProperty(mainTex3BlendModeProp, "Layer 3 Blend Mode");
                materialEditor.TextureScaleOffsetProperty(mainTex3Prop);
            }

            EditorGUILayout.Space();

            // 5. Normal Mapping
            EditorGUILayout.LabelField("Normal Map", EditorStyles.boldLabel);
            if (bumpMapProp != null && bumpScaleProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Bump Map"), bumpMapProp, bumpScaleProp);
                materialEditor.TextureScaleOffsetProperty(bumpMapProp);
            }

            EditorGUILayout.Space();

            // 6. Specular & Shininess
            EditorGUILayout.LabelField("Specular & Shininess", EditorStyles.boldLabel);
            if (specGlossMapProp != null && specColorProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Specular Map"), specGlossMapProp, specColorProp);
            }
            if (shininessProp != null)
            {
                materialEditor.ShaderProperty(shininessProp, "Shininess / Power");
            }

            EditorGUILayout.Space();

            // 7. Emission
            EditorGUILayout.LabelField("Emission", EditorStyles.boldLabel);
            if (emissionMapProp != null && emissionColorProp != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMapProp, emissionColorProp);
                materialEditor.TextureScaleOffsetProperty(emissionMapProp);
                if (emissionPowerProp != null) materialEditor.ShaderProperty(emissionPowerProp, "Emission Multiplier / HDR");
            }

            // 1. Render Mode & Pipeline State
            if (modeProp != null)
            {
                EditorGUILayout.LabelField("Render & Blend Settings", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                RenderMode mode = (RenderMode)modeProp.floatValue;
                mode = (RenderMode)EditorGUILayout.EnumPopup("Rendering Mode", mode);
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo("Rendering Mode");
                    modeProp.floatValue = (float)mode;
                    SetupMaterialWithRenderMode(material, mode);
                }

                EditorGUI.indentLevel++;
                if (unlitProp != null) materialEditor.ShaderProperty(unlitProp, "Unlit (Disable Lighting)");
                if (disableFogProp != null) materialEditor.ShaderProperty(disableFogProp, "Disable Fog");
                if (srcBlendProp != null) materialEditor.ShaderProperty(srcBlendProp, "Source Blend");
                if (dstBlendProp != null) materialEditor.ShaderProperty(dstBlendProp, "Destination Blend");
                if (blendOpProp != null) materialEditor.ShaderProperty(blendOpProp, "Blend Operation");
                if (zWriteProp != null) materialEditor.ShaderProperty(zWriteProp, "Depth Write (_ZWrite)");
                if (zTestProp != null) materialEditor.ShaderProperty(zTestProp, "Depth Test (_ZTest)");
                if (cullProp != null) materialEditor.ShaderProperty(cullProp, "Cull Mode");

                if (customRenderQueueProp != null)
                {
                    materialEditor.ShaderProperty(customRenderQueueProp, "Custom Render Queue");
                    if (customRenderQueueProp.floatValue >= 0)
                    {
                        material.renderQueue = (int)customRenderQueueProp.floatValue;
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    int newQueue = EditorGUILayout.IntField("Render Queue", material.renderQueue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        materialEditor.RegisterPropertyChangeUndo("Render Queue");
                        material.renderQueue = newQueue;
                    }
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            
            // 0. Sega NN Material Metadata
            if (materialFlagsProp != null || materialTypeProp != null || userDefinedProp != null)
            {
                EditorGUILayout.LabelField("Ninja Material Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                if (materialFlagsProp != null) materialEditor.ShaderProperty(materialFlagsProp, "Material Flags");
                if (materialTypeProp != null) materialEditor.ShaderProperty(materialTypeProp, "Material Type");
                if (userDefinedProp != null) materialEditor.ShaderProperty(userDefinedProp, "User Defined");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var obj in materialEditor.targets)
                {
                    MaterialChanged((Material)obj);
                }
            }
        }

        private static void MaterialChanged(Material material)
        {
            if (material.HasProperty("_Mode"))
            {
                SetupMaterialWithRenderMode(material, (RenderMode)material.GetFloat("_Mode"));
            }
        }

        private static void SetupMaterialWithRenderMode(Material material, RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.Opaque:
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetOverrideTag("Queue", "Geometry");
                    material.SetOverrideTag("IgnoreProjector", "False");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", -1.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.SetShaderPassEnabled("DepthOnly", true);
                    break;

                case RenderMode.Cutout:
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetOverrideTag("Queue", "AlphaTest");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaTest", 1.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.AlphaTest);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.SetShaderPassEnabled("DepthOnly", true);
                    break;

                case RenderMode.Transparent:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.Transparent);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Fade:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.Transparent);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Additive:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.Transparent);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Multiply:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.Transparent);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.ReverseSubtract:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.ReverseSubtract);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    if (material.HasProperty("_CustomRenderQueue")) material.SetFloat("_CustomRenderQueue", (float)UnityEngine.Rendering.RenderQueue.Transparent);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Custom:
                    break;
            }
        }
    }
}