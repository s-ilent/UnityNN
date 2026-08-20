// File: Marathon/Editor/UnityParsers/ParticleEffectResolver.cs
#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Marathon.Formats.Particle;

namespace SilentTools
{
    public static class ParticleEffectResolver
    {
        private static readonly string[] TextureExtensions = {
            ".png", ".dds", ".tga", ".xvr", ".jpg", ".jpeg", ".bmp", ".psd"
        };

        public static GameObject ResolveParticleEffect(
            ParticleEffectFile effect,
            string assetName,
            float scale,
            AssetImportContext ctx,
            NinjaImportSettings settings = null)
        {
            settings ??= NinjaImportSettings.Default;
            if (effect == null || !effect.IsValid) return null;

            GameObject root = new GameObject(assetName);

            // 1. Attach Root Particle Component
            var comp = root.AddComponent<ParticleEffectComponent>();
            comp.particleType = effect.ParticleType;
            comp.externalBones = effect.ExternalBones;
            comp.resourceFiles = effect.ResourceFiles;
            comp.emitterCount = effect.Emitters.Count;
            comp.behaviorCount = effect.Behaviors.Count;
            comp.sequenceCueCount = effect.SequenceCues.Count;

            string baseDir = ctx != null ? Path.GetDirectoryName(ctx.assetPath) : "";

            // Prioritized Texture Search Folders (TextureSearchPaths evaluated in array order)
            List<string> candidateFolders = BuildCandidateFolders(baseDir, settings.MaterialSearchPath, settings.TextureSearchPaths);
            Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);

            // 2. Generate and Cache Materials for Emitters
            Dictionary<int, Material> emitterMaterials = new Dictionary<int, Material>();
            for (int e = 0; e < effect.Emitters.Count; e++)
            {
                var emitter = effect.Emitters[e];
                Material mat = CreateParticleMaterial(effect, emitter, e, candidateFolders, textureCache, settings, ctx);
                if (mat != null)
                {
                    emitterMaterials[e] = mat;
                }
            }

            // 3. Build Timeline Spatial Cues & Shuriken Systems
            if (effect.SequenceCues.Count > 0)
            {
                for (int i = 0; i < effect.SequenceCues.Count; i++)
                {
                    var cue = effect.SequenceCues[i];
                    string cueName = $"[Cue_{i:00}] EffectID_{cue.EffectId} (Frames {cue.StartTime}-{cue.EndTime})";

                    Vector3 pos = cue.Translation;
                    if (float.IsNaN(pos.x) || float.IsInfinity(pos.x)) pos.x = 0f;
                    if (float.IsNaN(pos.y) || float.IsInfinity(pos.y)) pos.y = 0f;
                    if (float.IsNaN(pos.z) || float.IsInfinity(pos.z)) pos.z = 0f;

                    Vector3 rot = cue.Rotation;
                    if (float.IsNaN(rot.x) || float.IsInfinity(rot.x)) rot.x = 0f;
                    if (float.IsNaN(rot.y) || float.IsInfinity(rot.y)) rot.y = 0f;
                    if (float.IsNaN(rot.z) || float.IsInfinity(rot.z)) rot.z = 0f;

                    GameObject cueGO = new GameObject(cueName);
                    cueGO.transform.SetParent(root.transform, false);
                    cueGO.transform.localPosition = new Vector3(-pos.x * scale, pos.y * scale, pos.z * scale);
                    cueGO.transform.localEulerAngles = new Vector3(rot.x, -rot.y, -rot.z);

                    var cueComp = cueGO.AddComponent<ParticleSequenceCueComponent>();
                    cueComp.effectId = cue.EffectId;
                    cueComp.targetId = cue.TargetId;
                    cueComp.startTime = cue.StartTime;
                    cueComp.endTime = cue.EndTime;
                    cueComp.nextEntryTop = cue.NextEntryTop;
                    cueComp.nextEntryBottom = cue.NextEntryBottom;
                    cueComp.userData1 = cue.UserData1;
                    cueComp.userData2 = cue.UserData2;
                    cueComp.userData3 = cue.UserData3;
                    cueComp.userData4 = cue.UserData4;

                    ParticleBehaviorBlock matchedBehavior = FindBehaviorForEffectId(effect, cue.EffectId);
                    int targetEmitterIdx = matchedBehavior?.GeneratorHeader?.Value0 ?? cue.EffectId;
                    ParticleEmitter matchedEmitter = (targetEmitterIdx >= 0 && targetEmitterIdx < effect.Emitters.Count)
                        ? effect.Emitters[targetEmitterIdx] : null;

                    Material assignedMat = null;
                    if (matchedEmitter != null && emitterMaterials.TryGetValue(targetEmitterIdx, out Material mat))
                        assignedMat = mat;

                    AttachShurikenParticleSystem(cueGO, matchedBehavior, matchedEmitter, assignedMat, cue, scale);
                }
            }
            else
            {
                for (int b = 0; b < effect.Behaviors.Count; b++)
                {
                    var block = effect.Behaviors[b];
                    if (block.BehaviorType == TypdBehaviorType.GenerateParticle)
                    {
                        int emitterIdx = block.GeneratorHeader?.Value0 ?? b;
                        ParticleEmitter emitter = (emitterIdx >= 0 && emitterIdx < effect.Emitters.Count) ? effect.Emitters[emitterIdx] : null;
                        Material mat = emitterMaterials.TryGetValue(emitterIdx, out Material m) ? m : null;

                        GameObject pGO = new GameObject($"Emitter_{emitterIdx:00}_{block.TypeName}");
                        pGO.transform.SetParent(root.transform, false);
                        AttachShurikenParticleSystem(pGO, block, emitter, mat, null, scale);
                    }
                }
            }

            // 4. Attach Auxiliary Lighting Behaviors (Point & Spot Lights)
            for (int b = 0; b < effect.Behaviors.Count; b++)
            {
                var block = effect.Behaviors[b];
                if (block.BehaviorType == TypdBehaviorType.Light && block.Parameters.Count >= 13)
                {
                    GameObject lightGO = new GameObject($"[Light_{b:00}] PointLight");
                    lightGO.transform.SetParent(root.transform, false);
                    Light l = lightGO.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(block.GetFloat(5), block.GetFloat(6), block.GetFloat(7), 1f);
                    l.range = Mathf.Max(0.5f, block.GetFloat(9) * scale);
                }
                else if (block.BehaviorType == TypdBehaviorType.SpotLight && block.Parameters.Count >= 34)
                {
                    GameObject spotGO = new GameObject($"[Light_{b:00}] SpotLight");
                    spotGO.transform.SetParent(root.transform, false);
                    Light sl = spotGO.AddComponent<Light>();
                    sl.type = LightType.Spot;
                    sl.color = Color.white;
                    sl.spotAngle = Mathf.Max(1f, block.GetFloat(3));
                    sl.range = Mathf.Max(0.5f, block.GetFloat(4) * scale);
                }
            }

            return root;
        }

        private static ParticleBehaviorBlock FindBehaviorForEffectId(ParticleEffectFile effect, int effectId)
        {
            if (effect.Behaviors == null || effect.Behaviors.Count == 0) return null;

            foreach (var b in effect.Behaviors)
            {
                if (b.BehaviorType == TypdBehaviorType.GenerateParticle && b.GeneratorHeader != null && b.GeneratorHeader.Value0 == effectId)
                    return b;
            }

            if (effectId >= 0 && effectId < effect.Behaviors.Count)
                return effect.Behaviors[effectId];

            return effect.Behaviors[0];
        }

        private static void AttachShurikenParticleSystem(
            GameObject targetGO,
            ParticleBehaviorBlock behavior,
            ParticleEmitter emitter,
            Material material,
            ParticleSequenceCue cue,
            float scale)
        {
            ParticleSystem ps = targetGO.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRenderer = targetGO.GetComponent<ParticleSystemRenderer>();

            var hdr = behavior?.GeneratorHeader;
            var subKeyframes = behavior?.ParticleSubKeyframes;

            // --- 1. Main Module Configuration ---
            var main = ps.main;
            float duration = (cue != null && cue.EndTime > cue.StartTime)
                ? (cue.EndTime - cue.StartTime) / 60.0f
                : (hdr != null && hdr.ParticleLife > 0 ? hdr.ParticleLife : 1.0f);

            main.duration = Mathf.Max(0.1f, duration);
            main.loop = (cue == null || (cue.StartTime == 0 && cue.EndTime == 0));
            main.startLifetime = (hdr != null && hdr.ParticleLife > 0) ? hdr.ParticleLife : 1.0f;
            main.startSpeed = (hdr != null) ? (hdr.InitialSpeed * (hdr.VelocityScale > 0 ? hdr.VelocityScale : 1f)) : 1.0f;

            // Determine start size
            float startSize = 1.0f;
            if (subKeyframes != null && subKeyframes.Count > 0 && subKeyframes[0].StartSize > 0)
                startSize = subKeyframes[0].StartSize;
            else if (emitter != null && emitter.SpriteSubRecords.Count > 0 && emitter.SpriteSubRecords[0].Size > 0)
                startSize = emitter.SpriteSubRecords[0].Size;
            main.startSize = Mathf.Max(0.01f, startSize * scale);

            // Determine start color
            Color startColor = Color.white;
            if (subKeyframes != null && subKeyframes.Count > 0 && subKeyframes[0].StartColor.a > 0)
                startColor = subKeyframes[0].StartColor;
            main.startColor = startColor;

            main.gravityModifier = (hdr != null) ? hdr.Gravity : 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;

            // --- 2. Emission Module ---
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 30.0f;

            // --- 3. Shape Module & Axis Orientation (+Z -> +Y Upward) ---
            var shape = ps.shape;
            shape.enabled = true;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            if (hdr != null && hdr.Radius > 0.001f)
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = hdr.Radius * scale;
            }
            else if (hdr != null && (Mathf.Abs(hdr.SpawnArea.x) > 0.01f || Mathf.Abs(hdr.SpawnArea.y) > 0.01f || Mathf.Abs(hdr.SpawnArea.z) > 0.01f))
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(
                    Mathf.Max(0.01f, Mathf.Abs(hdr.SpawnArea.x) * scale),
                    Mathf.Max(0.01f, Mathf.Abs(hdr.SpawnArea.z) * scale),
                    Mathf.Max(0.01f, Mathf.Abs(hdr.SpawnArea.y) * scale)
                );
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 0f;
                shape.radius = 0.001f;
            }

            // --- 4. Color over Lifetime Module ---
            if (subKeyframes != null && subKeyframes.Count > 0)
            {
                var colModule = ps.colorOverLifetime;
                colModule.enabled = true;

                Gradient grad = new Gradient();
                var kf = subKeyframes[0];

                GradientColorKey[] colorKeys = new[] {
                    new GradientColorKey(kf.StartColor, 0.0f),
                    new GradientColorKey(kf.EndColor, 1.0f)
                };

                GradientAlphaKey[] alphaKeys = new[] {
                    new GradientAlphaKey(kf.StartColor.a, 0.0f),
                    new GradientAlphaKey(kf.EndColor.a, 1.0f)
                };

                grad.SetKeys(colorKeys, alphaKeys);
                colModule.color = new ParticleSystem.MinMaxGradient(grad);
            }

            // --- 5. Size over Lifetime Module ---
            if (subKeyframes != null && subKeyframes.Count > 0)
            {
                var sizeModule = ps.sizeOverLifetime;
                sizeModule.enabled = true;

                AnimationCurve sizeCurve = new AnimationCurve();
                var kf = subKeyframes[0];
                if (kf.CurveParameters != null && kf.CurveParameters.Length >= 20)
                {
                    for (int c = 0; c < 20; c++)
                    {
                        float t = c / 19.0f;
                        sizeCurve.AddKey(t, kf.CurveParameters[c]);
                    }
                }
                else
                {
                    sizeCurve.AddKey(0f, 1f);
                    sizeCurve.AddKey(1f, kf.EndSize > 0 ? (kf.EndSize / Mathf.Max(0.01f, kf.StartSize)) : 0f);
                }
                sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            }

            // --- 6. Renderer & Material Configuration ---
            if (psRenderer != null)
            {
                psRenderer.renderMode = (emitter?.Type == EmitterType.Mesh)
                    ? ParticleSystemRenderMode.Mesh
                    : ParticleSystemRenderMode.Billboard;

                if (material != null)
                {
                    psRenderer.sharedMaterial = material;
                }
            }
        }

        private static List<string> BuildCandidateFolders(string baseDir, string materialSearchDir, string[] textureSearchPaths)
        {
            List<string> folders = new List<string>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            void AddFolder(string dir)
            {
                if (string.IsNullOrEmpty(dir)) return;
                string norm = dir.Replace('\\', '/').TrimEnd('/');
                if (seen.Add(norm)) folders.Add(norm);
                if (seen.Add($"{norm}/Textures")) folders.Add($"{norm}/Textures");
                if (seen.Add($"{norm}/textures")) folders.Add($"{norm}/textures");
            }

            if (textureSearchPaths != null)
            {
                foreach (string dir in textureSearchPaths) AddFolder(dir);
            }

            AddFolder(baseDir);
            AddFolder(materialSearchDir);

            return folders;
        }

        private static Material CreateParticleMaterial(
            ParticleEffectFile effect,
            ParticleEmitter emitter,
            int emitterIndex,
            List<string> candidateFolders,
            Dictionary<string, Texture2D> textureCache,
            NinjaImportSettings settings,
            AssetImportContext ctx)
        {
            if (emitter == null) return null;

            string resName = (emitter.ResourceIndex >= 0 && emitter.ResourceIndex < effect.ResourceFiles.Count)
                ? effect.ResourceFiles[emitter.ResourceIndex] : "";

            Shader shader = Shader.Find("NinjaNext/Standard") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            Material mat = new Material(shader) { name = $"ParticleMat_{emitterIndex}_{Path.GetFileNameWithoutExtension(resName)}" };

            bool isAdditive = (emitter.Flags & 1) != 0 || (emitter.SpriteSubRecords.Count > 0 && emitter.SpriteSubRecords[0].BlendMode == 1);
            mat.SetFloat("_Mode", isAdditive ? 4.0f : 2.0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", isAdditive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Unlit", 1.0f);
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_AmbientColor", Color.white);
            mat.SetFloat("_EmissionPower", 1.0f);
            mat.SetFloat("_HDRIntensity", 1.0f);
            mat.SetFloat("_VertexColorScale", 1.0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            Texture2D tex = null;
            if (settings?.TextureRemaps != null)
            {
                foreach (var remap in settings.TextureRemaps)
                {
                    if (remap.textureIndex == emitter.ResourceIndex && remap.overrideTexture != null)
                    {
                        tex = remap.overrideTexture;
                        break;
                    }
                }
            }

            if (tex == null && !string.IsNullOrEmpty(resName))
            {
                tex = FindAndLoadTexture(resName, candidateFolders, textureCache, ctx);
            }

            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.SetTexture("_MainTex", tex);
            }

            if (ctx != null)
            {
                ctx.AddObjectToAsset($"Material_Emitter_{emitterIndex}", mat);
            }

            return mat;
        }

        private static Texture2D FindAndLoadTexture(
            string texFileName,
            List<string> candidateFolders,
            Dictionary<string, Texture2D> textureCache,
            AssetImportContext ctx)
        {
            string cleanName = StripTextureExtensions(texFileName);
            if (string.IsNullOrEmpty(cleanName)) return null;

            if (textureCache != null && textureCache.TryGetValue(cleanName, out Texture2D cached))
                return cached;

            Texture2D result = null;

            // Direct candidate folders check ($O(1)$ fast file lookup)
            foreach (string folder in candidateFolders)
            {
                foreach (string ext in TextureExtensions)
                {
                    string p = $"{folder}/{cleanName}{ext}";
                    if (File.Exists(p))
                    {
                        if (ext == ".xvr")
                        {
                            try
                            {
                                byte[] rawXvr = File.ReadAllBytes(p);
                                result = XvrDecoder.DecodeXvrFile(rawXvr);
                                if (result != null)
                                {
                                    result.name = cleanName;
                                    ctx?.DependsOnSourceAsset(p);
                                    ctx?.AddObjectToAsset($"Tex_{cleanName}", result);
                                    break;
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            result = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                            if (result != null)
                            {
                                ctx?.DependsOnSourceAsset(p);
                                break;
                            }
                        }
                    }
                }
                if (result != null) break;

                string exactPath = $"{folder}/{Path.GetFileName(texFileName)}";
                if (File.Exists(exactPath))
                {
                    if (exactPath.EndsWith(".xvr", System.StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[] rawXvr = File.ReadAllBytes(exactPath);
                            result = XvrDecoder.DecodeXvrFile(rawXvr);
                            if (result != null)
                            {
                                result.name = cleanName;
                                ctx?.DependsOnSourceAsset(exactPath);
                                ctx?.AddObjectToAsset($"Tex_{cleanName}", result);
                                break;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        result = AssetDatabase.LoadAssetAtPath<Texture2D>(exactPath);
                        if (result != null)
                        {
                            ctx?.DependsOnSourceAsset(exactPath);
                            break;
                        }
                    }
                }
                if (result != null) break;
            }

            if (textureCache != null)
            {
                textureCache[cleanName] = result;
            }

            return result;
        }

        private static string StripTextureExtensions(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string name = Path.GetFileName(fileName);
            while (true)
            {
                string ext = Path.GetExtension(name);
                if (string.IsNullOrEmpty(ext)) break;
                string extLower = ext.ToLowerInvariant();
                if (extLower is ".xvr" or ".dds" or ".tga" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".psd")
                    name = Path.GetFileNameWithoutExtension(name);
                else
                    break;
            }
            return name;
        }
    }
}
#endif