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
        private static readonly string[] ParamFileCandidates = new string[]
        {
            "obj_param.xnr", "obj_param.rel", "object_param.xnr", "object_param.rel", "npc_param.xnr", "npc_param.rel"
        };

        private static readonly string[] ParticleInfoCandidates = new string[]
        {
            "obj_particle_info.xnr", "obj_particle_info.rel", "particle_info.xnr", "particle_info.rel"
        };

        private static readonly string[] FileListCandidates = new string[]
        {
            "nbl_scene_filelist.rel", "nbl_scene_filelist.xnr", "filelist.rel", "filelist.xnr"
        };

        public static ResolvedStageContext ResolveAdjacentStageFiles(string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            ResolvedStageContext stageContext = new ResolvedStageContext();
            if (string.IsNullOrEmpty(assetPath)) return stageContext;

            string baseDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            stageContext.BaseDirectory = baseDir;

            // 1. Auto-load obj_param / npc_param
            foreach (string candidate in ParamFileCandidates)
            {
                string fullPath = Path.Combine(baseDir, candidate).Replace('\\', '/');
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(fullPath);
                        RelFileType rType;
                        object parsed = RelResolver.ParseRelBytes(rawBytes, candidate, out rType);
                        if (parsed is ObjectParamData paramData)
                        {
                            stageContext.ObjectParams = paramData;
                            if (ctx != null) ctx.DependsOnSourceAsset(fullPath);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RelFolderResolver] Could not load {fullPath}: {ex.Message}");
                    }
                }
            }

            // 2. Auto-load obj_particle_info
            foreach (string candidate in ParticleInfoCandidates)
            {
                string fullPath = Path.Combine(baseDir, candidate).Replace('\\', '/');
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(fullPath);
                        RelFileType rType;
                        object parsed = RelResolver.ParseRelBytes(rawBytes, candidate, out rType);
                        if (parsed is ObjectParticleInfoData partData)
                        {
                            stageContext.ParticleInfo = partData;
                            if (ctx != null) ctx.DependsOnSourceAsset(fullPath);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RelFolderResolver] Could not load {fullPath}: {ex.Message}");
                    }
                }
            }

            // 3. Auto-load nbl_scene_filelist.rel
            foreach (string candidate in FileListCandidates)
            {
                string fullPath = Path.Combine(baseDir, candidate).Replace('\\', '/');
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(fullPath);
                        RelFileType rType;
                        object parsed = RelResolver.ParseRelBytes(rawBytes, candidate, out rType);
                        if (parsed is FileListData fileListData)
                        {
                            stageContext.SceneFileList = fileListData;
                            if (ctx != null) ctx.DependsOnSourceAsset(fullPath);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RelFolderResolver] Could not load {fullPath}: {ex.Message}");
                    }
                }
            }

            return stageContext;
        }

        public static GameObject FindAndInstantiateModelAsset(string modelName, string baseDir)
        {
            if (string.IsNullOrEmpty(modelName)) return null;

            string cleanName = NinjaMaterialResolver.StripTextureExtensions(modelName);
            string[] extensions = new string[] { ".xnj", ".xno", ".xna", ".gno", ".zno", ".prefab" };

            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(baseDir, cleanName + ext).Replace('\\', '/');
                if (File.Exists(candidate))
                {
                    GameObject loadedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(candidate);
                    if (loadedAsset != null)
                    {
                        GameObject instance = UnityEngine.Object.Instantiate(loadedAsset);
                        instance.name = cleanName;
                        return instance;
                    }
                }
            }

            string[] guids = AssetDatabase.FindAssets($"t:GameObject {cleanName}");
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    GameObject loadedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (loadedAsset != null)
                    {
                        GameObject instance = UnityEngine.Object.Instantiate(loadedAsset);
                        instance.name = cleanName;
                        return instance;
                    }
                }
            }

            return null;
        }
    }
}