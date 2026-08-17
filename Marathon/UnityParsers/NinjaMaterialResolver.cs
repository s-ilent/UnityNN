// File: Marathon/UnityParsers/NinjaMaterialResolver.cs
using UnityEngine;
using UnityEditor;
using System;
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
        private static readonly string[] TextureExtensions = {
            ".png", ".PNG", ".tga", ".TGA", ".dds", ".DDS", ".psd", ".PSD",
            ".jpg", ".JPG", ".jpeg", ".JPEG", ".bmp", ".BMP", ".tif", ".TIF",
            ".tiff", ".TIFF", ".xvr", ".XVR"
        };

        private static readonly string[] TextureListExtensions = {
            ".xnt", ".XNT", ".gnt", ".GNT", ".znt", ".ZNT", ".cnt", ".CNT", ".ent", ".ENT", ".int", ".INT"
        };

        /// <summary>
        /// Iteratively strips known texture/support extensions (.xvr, .dds, .tga, .png, etc.) from a filename.
        /// </summary>
        public static string StripTextureExtensions(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string name = Path.GetFileName(fileName);
            while (true)
            {
                string ext = Path.GetExtension(name);
                if (string.IsNullOrEmpty(ext)) break;
                string extLower = ext.ToLowerInvariant();
                if (extLower is ".xvr" or ".dds" or ".tga" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".psd" or ".xnt" or ".gnt" or ".znt" or ".cnt" or ".ent" or ".int")
                    name = Path.GetFileNameWithoutExtension(name);
                else
                    break;
            }
            return name;
        }

        /// <summary>
        /// Auto-resolves associated external texture list files (.xnt, .gnt, .znt) if the embedded texture list is missing.
        /// </summary>
        public static NinjaTextureList ResolveTextureList(NinjaTextureList embeddedTexList, string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            if (embeddedTexList?.NinjaTextureFiles != null && embeddedTexList.NinjaTextureFiles.Count > 0)
                return embeddedTexList;

            if (string.IsNullOrEmpty(assetPath)) return embeddedTexList;
            string baseDir = Path.GetDirectoryName(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in TextureListExtensions)
            {
                string candidate = Path.Combine(baseDir, baseName + ext).Replace('\\', '/');
                if (File.Exists(candidate))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidate);
                        if (loader.Data.TextureList?.NinjaTextureFiles != null && loader.Data.TextureList.NinjaTextureFiles.Count > 0)
                        {
                            ctx?.DependsOnSourceAsset(candidate);
                            return loader.Data.TextureList;
                        }
                    }
                    catch { }
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
            texList = ResolveTextureList(texList, ctx.assetPath, ctx);

            for (int i = 0; i < objData.Materials.Count; i++)
            {
                NinjaMaterial nMat = objData.Materials[i];
                var matColour = objData.MaterialColours?.Find(c => c.Offset == nMat.MaterialColourOffset);
                var matLogic = objData.MaterialLogics?.Find(l => l.Offset == nMat.MaterialLogicOffset);
                var texMap = objData.TextureMaps?.Find(t => t.Offset == nMat.MaterialTexMapDescriptionOffset);

                string matName = DetermineMaterialName(objData, texMap, texList, modelName, i, namingMode);
                Material mat = CreateMaterialData(nMat, matColour, matLogic, texMap, texList, i, matName, stdShader, searchMode, modelFolderPath, searchDirectory, ctx);

                if (location == MaterialLocation.UseExternalMaterials)
                {
                    string foundPath = FindExistingMaterial(matName, modelFolderPath, searchMode, searchDirectory);
                    if (!string.IsNullOrEmpty(foundPath))
                    {
                        Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(foundPath);
                        if (externalMat != null) { materials.Add(externalMat); continue; }
                    }

                    string targetFolder = ResolveTargetFolder(modelFolderPath, searchDirectory);
                    if (!Directory.Exists(targetFolder)) { Directory.CreateDirectory(targetFolder); AssetDatabase.Refresh(); }

                    string newMatPath = $"{targetFolder}/{matName}.mat";
                    AssetDatabase.CreateAsset(mat, newMatPath);
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

        private static UnityEngine.Rendering.BlendMode MapNinjaBlendMode(Marathon.Formats.Mesh.Ninja.BlendMode ninjaBlend, UnityEngine.Rendering.BlendMode defaultBlend) => ninjaBlend switch
        {
            Marathon.Formats.Mesh.Ninja.BlendMode.NNE_BLENDMODE_SRCALPHA => UnityEngine.Rendering.BlendMode.SrcAlpha,
            Marathon.Formats.Mesh.Ninja.BlendMode.NNE_BLENDMODE_INVSRCALPHA => UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
            (Marathon.Formats.Mesh.Ninja.BlendMode)0 => UnityEngine.Rendering.BlendMode.Zero,
            (Marathon.Formats.Mesh.Ninja.BlendMode)1 => UnityEngine.Rendering.BlendMode.One,
            (Marathon.Formats.Mesh.Ninja.BlendMode)2 or (Marathon.Formats.Mesh.Ninja.BlendMode)0x306 => UnityEngine.Rendering.BlendMode.DstColor,
            (Marathon.Formats.Mesh.Ninja.BlendMode)3 => UnityEngine.Rendering.BlendMode.SrcColor,
            (Marathon.Formats.Mesh.Ninja.BlendMode)4 or (Marathon.Formats.Mesh.Ninja.BlendMode)0x307 => UnityEngine.Rendering.BlendMode.OneMinusDstColor,
            (Marathon.Formats.Mesh.Ninja.BlendMode)5 => UnityEngine.Rendering.BlendMode.SrcAlpha,
            (Marathon.Formats.Mesh.Ninja.BlendMode)6 => UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
            (Marathon.Formats.Mesh.Ninja.BlendMode)7 or (Marathon.Formats.Mesh.Ninja.BlendMode)0x304 => UnityEngine.Rendering.BlendMode.DstAlpha,
            (Marathon.Formats.Mesh.Ninja.BlendMode)8 or (Marathon.Formats.Mesh.Ninja.BlendMode)0x305 => UnityEngine.Rendering.BlendMode.OneMinusDstAlpha,
            (Marathon.Formats.Mesh.Ninja.BlendMode)9 => UnityEngine.Rendering.BlendMode.SrcAlphaSaturate,
            _ => defaultBlend
        };

        private static UnityEngine.Rendering.BlendOp MapNinjaBlendOp(Marathon.Formats.Mesh.Ninja.BlendOperation ninjaOp) => (uint)ninjaOp switch
        {
            1 or 0x800A => UnityEngine.Rendering.BlendOp.Subtract,
            2 or 0x800B => UnityEngine.Rendering.BlendOp.ReverseSubtract,
            3 or 0x8007 => UnityEngine.Rendering.BlendOp.Min,
            4 or 0x8008 => UnityEngine.Rendering.BlendOp.Max,
            _ => UnityEngine.Rendering.BlendOp.Add
        };

        private static UnityEngine.Rendering.CompareFunction MapNinjaCompareFunction(Marathon.Formats.Mesh.Ninja.CMPFunction func) => (uint)func switch
        {
            1 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_NEVER => UnityEngine.Rendering.CompareFunction.Never,
            2 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_LESS => UnityEngine.Rendering.CompareFunction.Less,
            3 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_EQUAL => UnityEngine.Rendering.CompareFunction.Equal,
            4 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_LESSEQUAL => UnityEngine.Rendering.CompareFunction.LessEqual,
            5 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_GREATER => UnityEngine.Rendering.CompareFunction.Greater,
            6 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_NOTEQUAL => UnityEngine.Rendering.CompareFunction.NotEqual,
            7 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_GREATEREQUAL => UnityEngine.Rendering.CompareFunction.GreaterEqual,
            8 or (uint)Marathon.Formats.Mesh.Ninja.CMPFunction.NNE_CMPFUNC_ALWAYS => UnityEngine.Rendering.CompareFunction.Always,
            _ => UnityEngine.Rendering.CompareFunction.LessEqual
        };

        private static string DetermineMaterialName(NinjaObject objData, NinjaTextureMap texMap, NinjaTextureList texList, string modelName, int index, MaterialNaming namingMode)
        {
            if (namingMode == MaterialNaming.ByBaseTextureName && texMap?.NinjaTextureMapDescriptions != null && texMap.NinjaTextureMapDescriptions.Count > 0)
            {
                int texIdx = texMap.NinjaTextureMapDescriptions[0].Index;
                if (texList?.NinjaTextureFiles != null && texIdx >= 0 && texIdx < texList.NinjaTextureFiles.Count)
                {
                    string baseTex = StripTextureExtensions(texList.NinjaTextureFiles[texIdx].FileName);
                    if (!string.IsNullOrEmpty(baseTex)) return baseTex;
                }
            }

            NinjaMaterial nMat = (objData != null && index < objData.Materials.Count) ? objData.Materials[index] : null;
            int colIdx = objData?.MaterialColours?.FindIndex(c => c.Offset == nMat?.MaterialColourOffset) ?? -1;
            int logicIdx = objData?.MaterialLogics?.FindIndex(l => l.Offset == nMat?.MaterialLogicOffset) ?? -1;
            int texMapIdx = objData?.TextureMaps?.FindIndex(t => t.Offset == nMat?.MaterialTexMapDescriptionOffset) ?? -1;

            string typeStr = nMat != null ? nMat.Type.ToString().Replace("NND_MATTYPE_", "") : "Standard";
            if (string.IsNullOrEmpty(typeStr) || typeStr == "0") typeStr = "Standard";
            else typeStr = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(typeStr.ToLower());

            string detailedName = $"{index}_{typeStr}_{(colIdx >= 0 ? $"Col_{colIdx}" : "Col_None")}_{(logicIdx >= 0 ? $"Logic_{logicIdx}" : "Logic_None")}_{(texMapIdx >= 0 ? $"TexMap_{texMapIdx}" : "TexMap_None")}";
            return namingMode == MaterialNaming.ByModelAndMaterialName ? $"{modelName}_{detailedName}" : detailedName;
        }

        private static Material CreateMaterialData(
            NinjaMaterial nMat, NinjaMaterialColours matColour, NinjaMaterialLogic matLogic,
            NinjaTextureMap texMap, NinjaTextureList texList, int index, string matName, Shader shader,
            MaterialSearch searchMode, string modelFolder, string searchDir, UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            Material mat = new Material(shader) { name = matName };
            uint rawFlags = nMat != null ? (uint)nMat.Flag : 0;
            uint rawType = nMat != null ? (uint)nMat.Type : 0;

            if (nMat != null)
            {
                mat.SetFloat("_MaterialFlags", rawFlags);
                mat.SetFloat("_MaterialType", rawType);
                mat.SetFloat("_UserDefined", nMat.UserDefined);
            }

            mat.SetFloat("_EmissionPower", 1.0f);
            mat.SetFloat("_HDRIntensity", 1.0f);
            mat.SetFloat("_VertexColorScale", 1.0f);
            mat.SetFloat("_AlphaToMask", 0.0f);
            mat.SetInt("_Cull", (rawFlags & 0x02) != 0 ? (int)UnityEngine.Rendering.CullMode.Off : (int)UnityEngine.Rendering.CullMode.Back);
            mat.SetFloat("_Unlit", (rawFlags & 0x04) != 0 ? 1.0f : 0.0f);
            mat.SetFloat("_DisableFog", (rawFlags & 0x08) != 0 ? 1.0f : 0.0f);

            if (matColour != null)
            {
                mat.SetColor("_Color", matColour.Diffuse);
                mat.SetColor("_AmbientColor", matColour.Ambient);
                mat.SetColor("_SpecColor", matColour.Specular);
                mat.SetColor("_EmissionColor", matColour.Emissive);
                mat.SetFloat("_Shininess", Mathf.Clamp01(matColour.Power / 100.0f));
            }

            if (texMap?.NinjaTextureMapDescriptions != null && texList?.NinjaTextureFiles != null)
            {
                for (int d = 0; d < texMap.NinjaTextureMapDescriptions.Count; d++)
                {
                    var desc = texMap.NinjaTextureMapDescriptions[d];
                    if (desc.Index >= 0 && desc.Index < texList.NinjaTextureFiles.Count)
                    {
                        string rawTex = texList.NinjaTextureFiles[desc.Index].FileName;
                        if (string.IsNullOrEmpty(rawTex)) continue;

                        Texture2D tex = FindAndLoadTexture(rawTex, searchMode, modelFolder, searchDir, ctx);
                        if (tex == null) continue;

                        string lower = rawTex.ToLower();
                        if (((desc.Type & 0x2000) != 0) || lower.Contains("env") || lower.Contains("matcap") || lower.Contains("refl"))
                        {
                            mat.SetTexture("_MatcapTex", tex);
                            mat.SetFloat("_UseMatcap", 1.0f);
                        }
                        else if (d == 0 || lower.Contains("diff") || lower.Contains("alb") || lower.Contains("color") || lower.Contains("tex"))
                        {
                            mat.mainTexture = tex;
                        }
                        else if (d == 1)
                        {
                            mat.SetTexture("_MainTex2", tex);
                            mat.SetFloat("_MainTex2BlendMode", (rawFlags & 0x20) != 0 ? 2.0f : 7.0f);
                        }
                        else if (d == 2)
                        {
                            mat.SetTexture("_MainTex3", tex);
                            mat.SetFloat("_MainTex3BlendMode", 1.0f);
                        }
                        else if (lower.Contains("nrm") || lower.Contains("norm") || lower.Contains("bump") || (desc.Type & 0x1000) != 0)
                        {
                            mat.SetTexture("_BumpMap", tex);
                            mat.SetFloat("_BumpScale", 1.0f);
                        }
                        else if (lower.Contains("spec") || lower.Contains("gloss") || lower.Contains("pow") || lower.Contains("spc") || (desc.Type & 0x4000) != 0)
                        {
                            mat.SetTexture("_SpecGlossMap", tex);
                        }
                        else if (lower.Contains("lmi") || lower.Contains("emis"))
                        {
                            mat.SetTexture("_EmissionMap", tex);
                        }
                    }
                }
            }

            if (matLogic != null)
            {
                var srcBlend = MapNinjaBlendMode(matLogic.SRCBlend, UnityEngine.Rendering.BlendMode.One);
                var dstBlend = MapNinjaBlendMode(matLogic.DSTBlend, UnityEngine.Rendering.BlendMode.Zero);
                var blendOp = MapNinjaBlendOp(matLogic.BlendOperation);
                var zTest = MapNinjaCompareFunction(matLogic.ZComparisonFunction);

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

                if ((rawFlags & 0x18) != 0 || (srcBlend == UnityEngine.Rendering.BlendMode.One && dstBlend == UnityEngine.Rendering.BlendMode.One))
                    mat.SetFloat("_Unlit", 1.0f);

                bool isOpaque = (!matLogic.Blend) || (srcBlend == UnityEngine.Rendering.BlendMode.One && dstBlend == UnityEngine.Rendering.BlendMode.Zero);
                if (!matLogic.Blend || isOpaque || matLogic.ZUpdate)
                {
                    bool isCutout = matLogic.Alpha && matLogic.AlphaRef > 0;
                    mat.SetFloat("_Mode", isCutout ? 1.0f : 0.0f);
                    mat.SetOverrideTag("RenderType", isCutout ? "TransparentCutout" : "Opaque");
                    mat.SetOverrideTag("Queue", isCutout ? "AlphaTest" : "Geometry");
                    mat.SetOverrideTag("IgnoreProjector", isCutout ? "True" : "False");
                    mat.renderQueue = (int)(isCutout ? UnityEngine.Rendering.RenderQueue.AlphaTest : UnityEngine.Rendering.RenderQueue.Geometry);
                    mat.SetFloat("_CustomRenderQueue", (float)mat.renderQueue);
                    mat.SetShaderPassEnabled("ShadowCaster", true);
                    mat.SetShaderPassEnabled("DepthOnly", true);
                }
                else
                {
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetOverrideTag("Queue", "Transparent");
                    mat.SetOverrideTag("IgnoreProjector", "True");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.SetFloat("_CustomRenderQueue", (float)mat.renderQueue);
                    mat.SetShaderPassEnabled("ShadowCaster", false);
                    mat.SetShaderPassEnabled("DepthOnly", false);

                    if (srcBlend == UnityEngine.Rendering.BlendMode.SrcAlpha && dstBlend == UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && blendOp == UnityEngine.Rendering.BlendOp.Add) mat.SetFloat("_Mode", 2.0f);
                    else if (srcBlend == UnityEngine.Rendering.BlendMode.One && dstBlend == UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && blendOp == UnityEngine.Rendering.BlendOp.Add) mat.SetFloat("_Mode", 3.0f);
                    else if (dstBlend == UnityEngine.Rendering.BlendMode.One && blendOp == UnityEngine.Rendering.BlendOp.Add) mat.SetFloat("_Mode", 4.0f);
                    else if (srcBlend == UnityEngine.Rendering.BlendMode.DstColor && dstBlend == UnityEngine.Rendering.BlendMode.Zero && blendOp == UnityEngine.Rendering.BlendOp.Add) mat.SetFloat("_Mode", 5.0f);
                    else if (blendOp == UnityEngine.Rendering.BlendOp.ReverseSubtract) mat.SetFloat("_Mode", 6.0f);
                    else mat.SetFloat("_Mode", 7.0f);
                }
            }
            return mat;
        }

        private static HashSet<string> BuildCandidateFolders(string modelFolder, string searchDir, MaterialSearch searchMode)
        {
            HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddFolderWithSubs(string root)
            {
                if (string.IsNullOrEmpty(root)) return;
                string n = root.Replace('\\', '/');
                folders.Add(n);
                folders.Add($"{n}/Textures");
                folders.Add($"{n}/textures");
                folders.Add($"{n}/Materials");
                folders.Add($"{n}/materials");

                if ((searchMode == MaterialSearch.RecursiveSubFolder || searchMode == MaterialSearch.ProjectDir) && Directory.Exists(n))
                {
                    try { foreach (string sub in Directory.GetDirectories(n, "*", SearchOption.AllDirectories)) folders.Add(sub.Replace('\\', '/')); }
                    catch { }
                }
            }

            AddFolderWithSubs(modelFolder);
            if (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets")) AddFolderWithSubs(searchDir);
            return folders;
        }

        private static Texture2D FindAndLoadTexture(string texFileName, MaterialSearch searchMode, string modelFolder, string searchDir, UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            string cleanName = StripTextureExtensions(texFileName);
            if (string.IsNullOrEmpty(cleanName)) return null;

            foreach (string folder in BuildCandidateFolders(modelFolder, searchDir, searchMode))
            {
                foreach (string ext in TextureExtensions)
                {
                    string p = $"{folder}/{cleanName}{ext}";
                    if (File.Exists(p)) { var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p); if (t != null) return t; }
                }
                string dp = $"{folder}/{Path.GetFileName(texFileName)}";
                if (File.Exists(dp)) { var t = AssetDatabase.LoadAssetAtPath<Texture2D>(dp); if (t != null) return t; }
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:Texture2D {cleanName}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (StripTextureExtensions(p).Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static string FindExistingMaterial(string matName, string modelFolder, MaterialSearch searchMode, string searchDir)
        {
            if (!string.IsNullOrEmpty(searchDir) && File.Exists($"{searchDir.Replace('\\', '/')}/{matName}.mat")) return $"{searchDir.Replace('\\', '/')}/{matName}.mat";
            if (File.Exists($"{modelFolder}/{matName}.mat")) return $"{modelFolder}/{matName}.mat";
            if (File.Exists($"{modelFolder}/Materials/{matName}.mat")) return $"{modelFolder}/Materials/{matName}.mat";

            foreach (string guid in AssetDatabase.FindAssets($"t:Material {matName}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Equals(matName, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        private static string ResolveTargetFolder(string modelFolder, string searchDir) =>
            (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets")) ? searchDir.Replace('\\', '/') : $"{modelFolder}/Materials";

        public static void ExtractMaterials(string assetPath, SerializedProperty locationProp, SerializedProperty searchDirProp)
        {
            string dest = EditorUtility.OpenFolderPanel("Select Destination Folder for Extracted Materials", "Assets", "");
            if (string.IsNullOrEmpty(dest) || !dest.StartsWith(Application.dataPath)) return;

            string rel = "Assets" + dest.Substring(Application.dataPath.Length);
            int count = 0;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (sub is Material mat)
                {
                    AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(mat), $"{rel}/{mat.name}.mat");
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            locationProp.enumValueIndex = (int)MaterialLocation.UseExternalMaterials;
            searchDirProp.stringValue = rel;
            EditorUtility.DisplayDialog("Material Extraction Complete", $"Successfully extracted {count} materials to:\n{rel}", "OK");
        }
    }
}