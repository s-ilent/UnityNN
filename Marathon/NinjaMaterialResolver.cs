using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public enum MaterialLocation
    {
        EmbedInPrefab = 0,
        UseExternalMaterials = 1
    }

    public enum MaterialSearch
    {
        Local = 0,              // Same folder as model asset
        RecursiveSubFolder = 1, // "Materials" subfolder or search dir
        ProjectDir = 2          // Specified search folder or project-wide
    }

    public enum MaterialNaming
    {
        ByMaterialName = 0,          // "3_Texture2_Col_1_Logic_4_TexMap_2"
        ByModelAndMaterialName = 1,  // "Sonic_3_Texture2_Col_1_Logic_4_TexMap_2"
        ByBaseTextureName = 2        // "chr_sonic_dif" (if texture map exists)
    }

    public static class NinjaMaterialResolver
    {
        private static readonly string[] TextureExtensions = new string[] {
            ".png", ".tga", ".dds", ".psd", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"
        };

        private static readonly string[] TextureListExtensions = new string[] {
            ".xnt", ".XNT", ".gnt", ".GNT", ".znt", ".ZNT", ".cnt", ".CNT", ".ent", ".ENT", ".int", ".INT"
        };

        /// <summary>
        /// Auto-resolves associated external texture list files (.xnt, .gnt, .znt) if the embedded texture list is missing.
        /// </summary>
        public static NinjaTextureList ResolveTextureList(
            NinjaTextureList embeddedTexList,
            string assetPath,
            UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            if (embeddedTexList != null && embeddedTexList.NinjaTextureFiles != null && embeddedTexList.NinjaTextureFiles.Count > 0)
                return embeddedTexList;

            if (string.IsNullOrEmpty(assetPath)) return embeddedTexList;

            string baseDirectory = Path.GetDirectoryName(assetPath);
            string baseFileName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in TextureListExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (File.Exists(candidatePath))
                {
                    try
                    {
                        NinjaNext xntLoader = new NinjaNext();
                        xntLoader.Load(candidatePath);
                        if (xntLoader.Data.TextureList != null && xntLoader.Data.TextureList.NinjaTextureFiles != null && xntLoader.Data.TextureList.NinjaTextureFiles.Count > 0)
                        {
                            if (ctx != null)
                            {
                                ctx.DependsOnSourceAsset(candidatePath);
                            }
                            return xntLoader.Data.TextureList;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Could not load associated texture list file {candidatePath}:\n{ex}");
                    }
                }
            }

            return embeddedTexList;
        }

        /// <summary>
        /// Resolves, searches, creates, or embeds materials for a Ninja Object asset.
        /// </summary>
        public static List<Material> ResolveMaterials(
            NinjaObject objData,
            NinjaTextureList texList,
            string modelName,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            MaterialLocation location,
            MaterialSearch searchMode,
            MaterialNaming namingMode,
            string searchDirectory)
        {
            List<Material> materials = new List<Material>();
            Shader stdShader = Shader.Find("NinjaNext/Standard");
            string modelFolderPath = Path.GetDirectoryName(ctx.assetPath).Replace('\\', '/');

            // Automatically check for adjacent .xnt/.gnt texture list if missing
            texList = ResolveTextureList(texList, ctx.assetPath, ctx);

            for (int i = 0; i < objData.Materials.Count; i++)
            {
                NinjaMaterial nMat = objData.Materials[i];

                NinjaMaterialColours matColour = FindMaterialColour(objData, nMat.MaterialColourOffset);
                NinjaMaterialLogic matLogic = FindMaterialLogic(objData, nMat.MaterialLogicOffset);
                NinjaTextureMap texMap = FindTextureMap(objData, nMat.MaterialTexMapDescriptionOffset);

                // Determine Material Name using ID, Type/Flags, Col, Logic, and TexMap
                string matName = DetermineMaterialName(objData, texMap, texList, modelName, i, namingMode);

                Material mat = CreateMaterialData(matColour, matLogic, texMap, texList, i, matName, stdShader, modelFolderPath, searchDirectory, ctx);

                if (location == MaterialLocation.UseExternalMaterials)
                {
                    string foundPath = FindExistingMaterial(matName, modelFolderPath, searchMode, searchDirectory);

                    if (!string.IsNullOrEmpty(foundPath))
                    {
                        Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(foundPath);
                        if (externalMat != null)
                        {
                            ctx.DependsOnSourceAsset(foundPath);
                            materials.Add(externalMat);
                            continue;
                        }
                    }

                    string targetFolder = ResolveTargetFolder(modelFolderPath, searchDirectory);
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                        AssetDatabase.Refresh();
                    }

                    string newMatPath = $"{targetFolder}/{matName}.mat";
                    AssetDatabase.CreateAsset(mat, newMatPath);
                    ctx.DependsOnSourceAsset(newMatPath);
                    materials.Add(mat);
                }
                else
                {
                    ctx.AddObjectToAsset($"Material_{i}_{matName}", mat);
                    materials.Add(mat);
                }
            }

            return materials;
        }

        #region Offset Resolution Helpers
        private static NinjaMaterialColours FindMaterialColour(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData.MaterialColours == null) return null;
            for (int i = 0; i < objData.MaterialColours.Count; i++)
            {
                if (objData.MaterialColours[i].Offset == offset)
                    return objData.MaterialColours[i];
            }
            return null;
        }

        private static NinjaMaterialLogic FindMaterialLogic(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData.MaterialLogics == null) return null;
            for (int i = 0; i < objData.MaterialLogics.Count; i++)
            {
                if (objData.MaterialLogics[i].Offset == offset)
                    return objData.MaterialLogics[i];
            }
            return null;
        }

        private static NinjaTextureMap FindTextureMap(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData.TextureMaps == null) return null;
            for (int i = 0; i < objData.TextureMaps.Count; i++)
            {
                if (objData.TextureMaps[i].Offset == offset)
                    return objData.TextureMaps[i];
            }
            return null;
        }

        private static int FindMaterialColourIndex(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData == null || objData.MaterialColours == null) return -1;
            for (int i = 0; i < objData.MaterialColours.Count; i++)
            {
                if (objData.MaterialColours[i].Offset == offset) return i;
            }
            return -1;
        }

        private static int FindMaterialLogicIndex(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData == null || objData.MaterialLogics == null) return -1;
            for (int i = 0; i < objData.MaterialLogics.Count; i++)
            {
                if (objData.MaterialLogics[i].Offset == offset) return i;
            }
            return -1;
        }

        private static int FindTextureMapIndex(NinjaObject objData, uint offset)
        {
            if (offset == 0 || objData == null || objData.TextureMaps == null) return -1;
            for (int i = 0; i < objData.TextureMaps.Count; i++)
            {
                if (objData.TextureMaps[i].Offset == offset) return i;
            }
            return -1;
        }
        #endregion

        #region Enum Mapping Helpers
        private static UnityEngine.Rendering.BlendMode MapNinjaBlendMode(Marathon.Formats.Mesh.Ninja.BlendMode ninjaBlend, UnityEngine.Rendering.BlendMode defaultBlend)
        {
            switch (ninjaBlend)
            {
                case Marathon.Formats.Mesh.Ninja.BlendMode.NNE_BLENDMODE_SRCALPHA:
                    return UnityEngine.Rendering.BlendMode.SrcAlpha;
                case Marathon.Formats.Mesh.Ninja.BlendMode.NNE_BLENDMODE_INVSRCALPHA:
                    return UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                default:
                    uint val = (uint)ninjaBlend;
                    switch (val)
                    {
                        case 0: return UnityEngine.Rendering.BlendMode.Zero;
                        case 1: return UnityEngine.Rendering.BlendMode.One;
                        case 2: return UnityEngine.Rendering.BlendMode.DstColor;
                        case 3: return UnityEngine.Rendering.BlendMode.SrcColor;
                        case 4: return UnityEngine.Rendering.BlendMode.OneMinusDstColor;
                        case 5: return UnityEngine.Rendering.BlendMode.SrcAlpha;
                        case 6: return UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                        case 7: return UnityEngine.Rendering.BlendMode.DstAlpha;
                        case 8: return UnityEngine.Rendering.BlendMode.OneMinusDstAlpha;
                        case 9: return UnityEngine.Rendering.BlendMode.SrcAlphaSaturate;
                        case 0x304: return UnityEngine.Rendering.BlendMode.DstAlpha;
                        case 0x305: return UnityEngine.Rendering.BlendMode.OneMinusDstAlpha;
                        case 0x306: return UnityEngine.Rendering.BlendMode.DstColor;
                        case 0x307: return UnityEngine.Rendering.BlendMode.OneMinusDstColor;
                        default:
                            if (System.Enum.IsDefined(typeof(UnityEngine.Rendering.BlendMode), (int)val))
                                return (UnityEngine.Rendering.BlendMode)(int)val;
                            return defaultBlend;
                    }
            }
        }

        private static UnityEngine.Rendering.BlendOp MapNinjaBlendOp(Marathon.Formats.Mesh.Ninja.BlendOperation ninjaOp)
        {
            switch (ninjaOp)
            {
                case Marathon.Formats.Mesh.Ninja.BlendOperation.NNE_BLENDOP_ADD:
                    return UnityEngine.Rendering.BlendOp.Add;
                default:
                    uint val = (uint)ninjaOp;
                    switch (val)
                    {
                        case 0: return UnityEngine.Rendering.BlendOp.Add;
                        case 1:
                        case 0x800A: return UnityEngine.Rendering.BlendOp.Subtract;
                        case 2:
                        case 0x800B: return UnityEngine.Rendering.BlendOp.ReverseSubtract;
                        case 3:
                        case 0x8007: return UnityEngine.Rendering.BlendOp.Min;
                        case 4:
                        case 0x8008: return UnityEngine.Rendering.BlendOp.Max;
                        default: return UnityEngine.Rendering.BlendOp.Add;
                    }
            }
        }

        private static UnityEngine.Rendering.CompareFunction MapNinjaCompareFunction(Marathon.Formats.Mesh.Ninja.CMPFunction func)
        {
            switch (func)
            {
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_NEVER:
                    return UnityEngine.Rendering.CompareFunction.Never;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_LESS:
                    return UnityEngine.Rendering.CompareFunction.Less;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_EQUAL:
                    return UnityEngine.Rendering.CompareFunction.Equal;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_LESSEQUAL:
                    return UnityEngine.Rendering.CompareFunction.LessEqual;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_GREATER:
                    return UnityEngine.Rendering.CompareFunction.Greater;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_NOTEQUAL:
                    return UnityEngine.Rendering.CompareFunction.NotEqual;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_GREATEREQUAL:
                    return UnityEngine.Rendering.CompareFunction.GreaterEqual;
                case Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_ALWAYS:
                    return UnityEngine.Rendering.CompareFunction.Always;
                default:
                    uint val = (uint)func;
                    switch (val)
                    {
                        case 1: return UnityEngine.Rendering.CompareFunction.Never;
                        case 2: return UnityEngine.Rendering.CompareFunction.Less;
                        case 3: return UnityEngine.Rendering.CompareFunction.Equal;
                        case 4: return UnityEngine.Rendering.CompareFunction.Equal;
                        case 5: return UnityEngine.Rendering.CompareFunction.Greater;
                        case 6: return UnityEngine.Rendering.CompareFunction.NotEqual;
                        case 7: return UnityEngine.Rendering.CompareFunction.GreaterEqual;
                        case 8: return UnityEngine.Rendering.CompareFunction.Always;
                        default: return UnityEngine.Rendering.CompareFunction.LessEqual;
                    }
            }
        }
        #endregion

        #region Material & Texture Resolution
        private static string DetermineMaterialName(
            NinjaObject objData,
            NinjaTextureMap texMap,
            NinjaTextureList texList,
            string modelName,
            int index,
            MaterialNaming namingMode)
        {
            if (namingMode == MaterialNaming.ByBaseTextureName)
            {
                string texName = GetBaseTextureName(texMap, texList);
                if (!string.IsNullOrEmpty(texName))
                    return Path.GetFileNameWithoutExtension(texName);
            }

            NinjaMaterial nMat = (objData != null && index < objData.Materials.Count) ? objData.Materials[index] : null;

            int colIdx = (objData != null && nMat != null) ? FindMaterialColourIndex(objData, nMat.MaterialColourOffset) : -1;
            int logicIdx = (objData != null && nMat != null) ? FindMaterialLogicIndex(objData, nMat.MaterialLogicOffset) : -1;
            int texMapIdx = (objData != null && nMat != null) ? FindTextureMapIndex(objData, nMat.MaterialTexMapDescriptionOffset) : -1;

            string typeStr = nMat != null ? nMat.Type.ToString().Replace("NND_MATTYPE_", "") : "Standard";
            if (string.IsNullOrEmpty(typeStr) || typeStr == "0") typeStr = "Standard";
            else
            {
                typeStr = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(typeStr.ToLower());
            }

            string colStr = colIdx >= 0 ? $"Col_{colIdx}" : "Col_None";
            string logicStr = logicIdx >= 0 ? $"Logic_{logicIdx}" : "Logic_None";
            string texMapStr = texMapIdx >= 0 ? $"TexMap_{texMapIdx}" : "TexMap_None";

            string detailedName = $"{index}_{typeStr}_{colStr}_{logicStr}_{texMapStr}";

            if (namingMode == MaterialNaming.ByModelAndMaterialName)
            {
                return $"{modelName}_{detailedName}";
            }

            return detailedName;
        }

        private static string GetBaseTextureName(NinjaTextureMap texMap, NinjaTextureList texList)
        {
            if (texMap != null && texMap.NinjaTextureMapDescriptions != null && texMap.NinjaTextureMapDescriptions.Count > 0)
            {
                int texIdx = texMap.NinjaTextureMapDescriptions[0].Index;
                if (texList != null && texList.NinjaTextureFiles != null && texIdx >= 0 && texIdx < texList.NinjaTextureFiles.Count)
                {
                    return texList.NinjaTextureFiles[texIdx].FileName;
                }
            }
            return null;
        }

        private static Material CreateMaterialData(
            NinjaMaterialColours matColour,
            NinjaMaterialLogic matLogic,
            NinjaTextureMap texMap,
            NinjaTextureList texList,
            int index,
            string matName,
            Shader shader,
            string modelFolder,
            string searchDir,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            Material mat = new Material(shader) { name = matName };

            // Default material property values
            mat.SetFloat("_EmissionPower", 1.0f);
            mat.SetFloat("_VertexColorScale", 1.0f);
            mat.SetFloat("_AlphaToMask", 0.0f);
            mat.SetFloat("_Unlit", 0.0f);

            if (matColour != null)
            {
                mat.SetColor("_Color", matColour.Diffuse);
                mat.SetColor("_SpecColor", matColour.Specular);
                mat.SetColor("_EmissionColor", matColour.Emissive);
                mat.SetFloat("_Shininess", Mathf.Clamp01(matColour.Power / 100.0f));
            }

            if (texMap != null && texMap.NinjaTextureMapDescriptions != null && texList != null && texList.NinjaTextureFiles != null)
            {
                for (int d = 0; d < texMap.NinjaTextureMapDescriptions.Count; d++)
                {
                    var desc = texMap.NinjaTextureMapDescriptions[d];
                    if (desc.Index >= 0 && desc.Index < texList.NinjaTextureFiles.Count)
                    {
                        string rawTexFileName = texList.NinjaTextureFiles[desc.Index].FileName;
                        if (string.IsNullOrEmpty(rawTexFileName)) continue;

                        Texture2D tex = FindAndLoadTexture(rawTexFileName, modelFolder, searchDir, ctx);
                        if (tex != null)
                        {
                            string lowerName = rawTexFileName.ToLower();
                            uint descType = desc.Type;

                            bool isEnvMap = ((descType & 0x2000) != 0) || lowerName.Contains("env") || lowerName.Contains("matcap") || lowerName.Contains("refl");

                            if (isEnvMap)
                            {
                                mat.SetTexture("_MatcapTex", tex);
                                mat.SetFloat("_UseMatcap", 1.0f);
                            }
                            else if (d == 0 || lowerName.Contains("diff") || lowerName.Contains("alb") || lowerName.Contains("color") || lowerName.Contains("tex"))
                            {
                                mat.mainTexture = tex;
                            }
                            else if (d == 1)
                            {
                                mat.SetTexture("_MainTex2", tex);
                                mat.SetFloat("_MainTex2BlendMode", 1.0f); // Multiply by default
                            }
                            else if (d == 2)
                            {
                                mat.SetTexture("_MainTex3", tex);
                                mat.SetFloat("_MainTex3BlendMode", 1.0f);
                            }
                            else if (lowerName.Contains("nrm") || lowerName.Contains("norm") || lowerName.Contains("bump"))
                            {
                                mat.SetTexture("_BumpMap", tex);
                                mat.SetFloat("_BumpScale", 1.0f);
                            }
                            else if (lowerName.Contains("spec") || lowerName.Contains("gloss") || lowerName.Contains("pow") || lowerName.Contains("spc"))
                            {
                                mat.SetTexture("_SpecGlossMap", tex);
                            }
                            else if (lowerName.Contains("lmi") || lowerName.Contains("emis"))
                            {
                                mat.SetTexture("_EmissionMap", tex);
                            }
                        }
                    }
                }
            }

            if (matLogic != null)
            {
                UnityEngine.Rendering.BlendMode srcBlend = MapNinjaBlendMode(matLogic.SRCBlend, UnityEngine.Rendering.BlendMode.One);
                UnityEngine.Rendering.BlendMode dstBlend = MapNinjaBlendMode(matLogic.DSTBlend, UnityEngine.Rendering.BlendMode.Zero);
                UnityEngine.Rendering.BlendOp blendOp = MapNinjaBlendOp(matLogic.BlendOperation);
                UnityEngine.Rendering.CompareFunction zTest = MapNinjaCompareFunction(matLogic.ZComparisonFunction);

                mat.SetInt("_SrcBlend", (int)srcBlend);
                mat.SetInt("_DstBlend", (int)dstBlend);
                mat.SetInt("_BlendOp", (int)blendOp);
                mat.SetInt("_ZTest", (int)zTest);
                mat.SetInt("_ZWrite", matLogic.ZUpdate ? 1 : 0);

                if (matLogic.Alpha)
                {
                    mat.SetFloat("_AlphaTest", 1.0f);
                    mat.SetFloat("_Cutoff", matLogic.AlphaRef > 0 ? matLogic.AlphaRef / 255.0f : 0.1f);
                }
                else
                {
                    mat.SetFloat("_AlphaTest", 0.0f);
                }

                // Detect additive glow unlit effect logic (SRCBlend=One & DSTBlend=One)
                bool isAdditiveGlow = (srcBlend == UnityEngine.Rendering.BlendMode.One && dstBlend == UnityEngine.Rendering.BlendMode.One);
                if (isAdditiveGlow)
                {
                    mat.SetFloat("_Unlit", 1.0f);
                }

                // Map Ninja Material Logic state to preset mode enum & update tags, queues and pass states
                if (!matLogic.Blend && !matLogic.Alpha)
                {
                    mat.SetFloat("_Mode", 0.0f); // Opaque
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetOverrideTag("Queue", "Geometry");
                    mat.SetOverrideTag("IgnoreProjector", "False");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    mat.SetShaderPassEnabled("ShadowCaster", true);
                    mat.SetShaderPassEnabled("DepthOnly", true);
                }
                else if (matLogic.Alpha && !matLogic.Blend)
                {
                    mat.SetFloat("_Mode", 1.0f); // Cutout
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.SetOverrideTag("Queue", "AlphaTest");
                    mat.SetOverrideTag("IgnoreProjector", "True");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    mat.SetShaderPassEnabled("ShadowCaster", true);
                    mat.SetShaderPassEnabled("DepthOnly", true);
                }
                else if (matLogic.Blend)
                {
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetOverrideTag("Queue", "Transparent");
                    mat.SetOverrideTag("IgnoreProjector", "True");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.SetShaderPassEnabled("ShadowCaster", false);
                    mat.SetShaderPassEnabled("DepthOnly", false);

                    if (srcBlend == UnityEngine.Rendering.BlendMode.SrcAlpha && dstBlend == UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && blendOp == UnityEngine.Rendering.BlendOp.Add)
                        mat.SetFloat("_Mode", 2.0f); // Transparent
                    else if (srcBlend == UnityEngine.Rendering.BlendMode.One && dstBlend == UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && blendOp == UnityEngine.Rendering.BlendOp.Add)
                        mat.SetFloat("_Mode", 3.0f); // Fade
                    else if (dstBlend == UnityEngine.Rendering.BlendMode.One && blendOp == UnityEngine.Rendering.BlendOp.Add)
                        mat.SetFloat("_Mode", 4.0f); // Additive
                    else if (srcBlend == UnityEngine.Rendering.BlendMode.DstColor && dstBlend == UnityEngine.Rendering.BlendMode.Zero && blendOp == UnityEngine.Rendering.BlendOp.Add)
                        mat.SetFloat("_Mode", 5.0f); // Multiply
                    else if (blendOp == UnityEngine.Rendering.BlendOp.ReverseSubtract)
                        mat.SetFloat("_Mode", 6.0f); // ReverseSubtract
                    else
                        mat.SetFloat("_Mode", 7.0f); // Custom
                }
            }

            return mat;
        }

        private static Texture2D FindAndLoadTexture(
            string texFileName,
            string modelFolder,
            string searchDir,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            string cleanName = Path.GetFileNameWithoutExtension(texFileName);

            List<string> candidateFolders = new List<string> {
                modelFolder,
                $"{modelFolder}/Textures",
                $"{modelFolder}/Materials"
            };

            if (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets"))
            {
                string normSearchDir = searchDir.Replace('\\', '/');
                candidateFolders.Add(normSearchDir);
                candidateFolders.Add($"{normSearchDir}/Textures");
            }

            foreach (string folder in candidateFolders)
            {
                foreach (string ext in TextureExtensions)
                {
                    string candidatePath = $"{folder}/{cleanName}{ext}";
                    if (File.Exists(candidatePath))
                    {
                        Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);
                        if (loadedTex != null)
                        {
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            return loadedTex;
                        }
                    }
                }
            }

            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {cleanName}");
            foreach (string guid in guids)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(foundPath).Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(foundPath);
                    if (tex != null)
                    {
                        if (ctx != null) ctx.DependsOnSourceAsset(foundPath);
                        return tex;
                    }
                }
            }

            return null;
        }

        private static string FindExistingMaterial(string matName, string modelFolder, MaterialSearch searchMode, string searchDir)
        {
            if (!string.IsNullOrEmpty(searchDir))
            {
                string searchDirPath = searchDir.Replace('\\', '/');
                string directPath = $"{searchDirPath}/{matName}.mat";
                if (File.Exists(directPath)) return directPath;
            }

            string localPath = $"{modelFolder}/{matName}.mat";
            if (File.Exists(localPath)) return localPath;

            string subFolderPath = $"{modelFolder}/Materials/{matName}.mat";
            if (File.Exists(subFolderPath)) return subFolderPath;

            string[] guids = AssetDatabase.FindAssets($"t:Material {matName}");
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Equals(matName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }

        private static string ResolveTargetFolder(string modelFolder, string searchDir)
        {
            if (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets"))
            {
                return searchDir.Replace('\\', '/');
            }
            return $"{modelFolder}/Materials";
        }

        public static void ExtractMaterials(string assetPath, SerializedProperty locationProp, SerializedProperty searchDirProp)
        {
            string destinationFolder = EditorUtility.OpenFolderPanel("Select Destination Folder for Extracted Materials", "Assets", "");
            if (string.IsNullOrEmpty(destinationFolder)) return;

            if (!destinationFolder.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please select a destination folder inside the project's Assets directory.", "OK");
                return;
            }

            string relativeFolder = "Assets" + destinationFolder.Substring(Application.dataPath.Length);

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int count = 0;

            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Material mat)
                {
                    string targetPath = $"{relativeFolder}/{mat.name}.mat";
                    Material newMat = Object.Instantiate(mat);
                    AssetDatabase.CreateAsset(newMat, targetPath);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            locationProp.enumValueIndex = (int)MaterialLocation.UseExternalMaterials;
            searchDirProp.stringValue = relativeFolder;

            EditorUtility.DisplayDialog("Material Extraction Complete", $"Successfully extracted {count} materials to:\n{relativeFolder}", "OK");
        }
        #endregion
    }
}