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
        ByMaterialName = 0,          // "Material_0"
        ByModelAndMaterialName = 1,  // "Sonic_Material_0"
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
            Shader stdShader = Shader.Find("Standard");
            string modelFolderPath = Path.GetDirectoryName(ctx.assetPath).Replace('\\', '/');

            // Automatically check for adjacent .xnt/.gnt texture list if missing
            texList = ResolveTextureList(texList, ctx.assetPath, ctx);

            for (int i = 0; i < objData.Materials.Count; i++)
            {
                NinjaMaterial nMat = objData.Materials[i];

                NinjaMaterialColours matColour = FindMaterialColour(objData, nMat.MaterialColourOffset);
                NinjaMaterialLogic matLogic = FindMaterialLogic(objData, nMat.MaterialLogicOffset);
                NinjaTextureMap texMap = FindTextureMap(objData, nMat.MaterialTexMapDescriptionOffset);

                string matName = DetermineMaterialName(texMap, texList, modelName, i, namingMode);

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
        #endregion

        #region Material & Texture Resolution
        private static string DetermineMaterialName(
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

            if (namingMode == MaterialNaming.ByModelAndMaterialName)
            {
                return $"{modelName}_Material_{index}";
            }

            return $"Material_{index}";
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

            if (matColour != null)
            {
                mat.color = matColour.Diffuse;
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
                            if (d == 0 || lowerName.Contains("diff") || lowerName.Contains("alb") || lowerName.Contains("color") || lowerName.Contains("tex"))
                            {
                                mat.mainTexture = tex;
                            }
                            else if (lowerName.Contains("nrm") || lowerName.Contains("norm") || lowerName.Contains("bump"))
                            {
                                mat.SetTexture("_BumpMap", tex);
                                mat.EnableKeyword("_NORMALMAP");
                            }
                            else if (lowerName.Contains("spec") || lowerName.Contains("gloss"))
                            {
                                mat.SetTexture("_SpecGlossMap", tex);
                                mat.EnableKeyword("_SPECGLOSSMAP");
                            }
                        }
                    }
                }
            }

            if (matLogic != null)
            {
                if (matLogic.Alpha)
                {
                    mat.SetFloat("_Mode", 1); // Cutout
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 2450;
                }
                else if (matLogic.Blend)
                {
                    mat.SetFloat("_Mode", 2); // Fade / Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
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
                            ctx.DependsOnSourceAsset(candidatePath);
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
                        ctx.DependsOnSourceAsset(foundPath);
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