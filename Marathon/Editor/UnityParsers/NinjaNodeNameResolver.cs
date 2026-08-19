using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public static class NinjaNodeNameResolver
    {
        private static readonly string[] NodeNameListExtensions = new string[] {
            ".xnn", ".XNN", ".xna", ".XNA", ".xno", ".XNO", ".gnn", ".GNN", ".gna", ".GNA", ".znn", ".ZNN"
        };

        /// <summary>
        /// Auto-resolves node names from adjacent linked files (.xnn, .xna, .xno) if embedded node names are missing or generic.
        /// </summary>
        public static NinjaNodeNameList ResolveNodeNames(
            NinjaObject objData,
            NinjaNodeNameList embeddedNameList,
            string assetPath,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out string sourceDescription)
        {
            sourceDescription = "Embedded";

            if (objData == null || objData.Nodes == null || objData.Nodes.Count == 0)
                return embeddedNameList;

            bool hasCustomNames = false;
            for (int i = 0; i < objData.Nodes.Count; i++)
            {
                if (!string.IsNullOrEmpty(objData.Nodes[i].Name) && !objData.Nodes[i].Name.StartsWith("Node_"))
                {
                    hasCustomNames = true;
                    break;
                }
            }

            if (hasCustomNames && embeddedNameList != null)
                return embeddedNameList;

            if (string.IsNullOrEmpty(assetPath)) return embeddedNameList;

            string baseDirectory = Path.GetDirectoryName(assetPath);
            string baseFileName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in NodeNameListExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(candidatePath))
                {
                    try
                    {
                        NinjaNext candidateLoader = new NinjaNext();
                        candidateLoader.Load(candidatePath);

                        NinjaNodeNameList candidateNames = candidateLoader.Data.NodeNameList;
                        List<string> nameStrings = null;

                        if (candidateNames != null && candidateNames.NinjaNodeNames != null && candidateNames.NinjaNodeNames.Count == objData.Nodes.Count)
                        {
                            nameStrings = candidateNames.NinjaNodeNames;
                        }
                        else if (candidateLoader.Data.Object != null && candidateLoader.Data.Object.Nodes != null && candidateLoader.Data.Object.Nodes.Count == objData.Nodes.Count)
                        {
                            bool validCandidate = false;
                            List<string> objNames = new List<string>();
                            for (int i = 0; i < candidateLoader.Data.Object.Nodes.Count; i++)
                            {
                                string nName = candidateLoader.Data.Object.Nodes[i].Name;
                                if (!string.IsNullOrEmpty(nName) && !nName.StartsWith("Node_"))
                                    validCandidate = true;
                                objNames.Add(nName);
                            }
                            if (validCandidate) nameStrings = objNames;
                        }

                        if (nameStrings != null && nameStrings.Count == objData.Nodes.Count)
                        {
                            for (int i = 0; i < objData.Nodes.Count; i++)
                            {
                                if (!string.IsNullOrEmpty(nameStrings[i]))
                                    objData.Nodes[i].Name = nameStrings[i];
                            }

                            if (ctx != null)
                            {
                                ctx.DependsOnSourceAsset(candidatePath);
                            }

                            sourceDescription = $"External: {Path.GetFileName(candidatePath)}";

                            if (candidateNames == null)
                            {
                                candidateNames = new NinjaNodeNameList();
                                candidateNames.NinjaNodeNames = nameStrings;
                            }

                            return candidateNames;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Could not load associated node name file {candidatePath}:\n{ex}");
                    }
                }
            }

            return embeddedNameList;
        }
    }
}