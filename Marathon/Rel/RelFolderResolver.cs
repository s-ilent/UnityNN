// File: Marathon/Rel/RelFolderResolver.cs
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SilentTools
{
    public class ResolvedStageContext
    {
        public ObjectParamData ObjectParams { get; set; }
        public ObjectParticleInfoData ParticleInfo { get; set; }
        public FileListData SceneFileList { get; set; }
        public string BaseDirectory { get; set; } = "";
    }

    public static class RelFolderResolver
    {
        private static readonly string[] ParamFileCandidates = {
            "obj_param.xnr", "obj_param.rel", "object_param.xnr", "object_param.rel", "npc_param.xnr", "npc_param.rel"
        };

        private static readonly string[] ParticleInfoCandidates = {
            "obj_particle_info.xnr", "obj_particle_info.rel", "particle_info.xnr", "particle_info.rel"
        };

        private static readonly string[] FileListCandidates = {
            "nbl_scene_filelist.rel", "nbl_scene_filelist.xnr", "filelist.rel", "filelist.xnr"
        };

        public static ResolvedStageContext ResolveAdjacentStageFiles(string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            ResolvedStageContext stageContext = new ResolvedStageContext();
            if (string.IsNullOrEmpty(assetPath)) return stageContext;

            string baseDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            stageContext.BaseDirectory = baseDir;

            stageContext.ObjectParams = TryLoadAdjacentRel<ObjectParamData>(baseDir, ParamFileCandidates, ctx);
            stageContext.ParticleInfo = TryLoadAdjacentRel<ObjectParticleInfoData>(baseDir, ParticleInfoCandidates, ctx);
            stageContext.SceneFileList = TryLoadAdjacentRel<FileListData>(baseDir, FileListCandidates, ctx);

            return stageContext;
        }

        private static T TryLoadAdjacentRel<T>(string baseDir, string[] candidates, UnityEditor.AssetImporters.AssetImportContext ctx) where T : class
        {
            foreach (string candidate in candidates)
            {
                string fullPath = Path.Combine(baseDir, candidate).Replace('\\', '/');
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(fullPath);
                        object parsed = RelResolver.ParseRelBytes(rawBytes, candidate, out _);
                        if (parsed is T result)
                        {
                            ctx?.DependsOnSourceAsset(fullPath);
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RelFolderResolver] Could not load {fullPath}: {ex.Message}");
                    }
                }
            }
            return null;
        }

        public static KeyValuePair<int, ObjectParamEntry>? FindParamEntryForModel(ObjectParamData paramData, string modelFileName)
        {
            if (paramData?.ObjectDefinitions == null || string.IsNullOrEmpty(modelFileName)) return null;
            string cleanModel = NinjaMaterialResolver.StripTextureExtensions(modelFileName);

            foreach (var kvp in paramData.ObjectDefinitions)
            {
                foreach (var mRef in kvp.Value.Models)
                {
                    if (NinjaMaterialResolver.StripTextureExtensions(mRef.FileName).Equals(cleanModel, StringComparison.OrdinalIgnoreCase))
                        return kvp;
                }
            }
            return null;
        }

        public static string FindAnimationFilePath(string animName, string baseDir, bool isMaterialAnim = false)
        {
            if (string.IsNullOrEmpty(animName)) return null;
            string cleanName = NinjaMaterialResolver.StripTextureExtensions(animName);

            string[] extensions = isMaterialAnim 
                ? new[] { ".xnv", ".gnv", ".znv", ".xnm", ".gnm", ".znm" } 
                : new[] { ".xnm", ".gnm", ".znm", ".xnv", ".gnv", ".znv" };

            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(baseDir, cleanName + ext).Replace('\\', '/');
                if (File.Exists(candidate)) return candidate;
            }

            foreach (string guid in AssetDatabase.FindAssets($"{cleanName} t:DefaultAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string pExt = Path.GetExtension(p).ToLowerInvariant();
                if (Path.GetFileNameWithoutExtension(p).Equals(cleanName, StringComparison.OrdinalIgnoreCase) &&
                    pExt is ".xnm" or ".xnv" or ".gnm" or ".gnv" or ".znm" or ".znv")
                    return p;
            }
            return null;
        }

        public static GameObject FindAndInstantiateModelAsset(string modelName, string baseDir)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            string cleanName = NinjaMaterialResolver.StripTextureExtensions(modelName);

            foreach (string ext in new[] { ".xnj", ".xno", ".xna", ".gno", ".zno", ".prefab" })
            {
                string candidate = Path.Combine(baseDir, cleanName + ext).Replace('\\', '/');
                if (File.Exists(candidate))
                {
                    GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(candidate);
                    if (loaded != null)
                    {
                        GameObject instance = UnityEngine.Object.Instantiate(loaded);
                        instance.name = cleanName;
                        return instance;
                    }
                }
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:GameObject {cleanName}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (loaded != null)
                    {
                        GameObject instance = UnityEngine.Object.Instantiate(loaded);
                        instance.name = cleanName;
                        return instance;
                    }
                }
            }
            return null;
        }
    }
}