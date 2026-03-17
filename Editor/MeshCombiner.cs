// Ezpzoptimizer - Mesh Combiner
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class MeshCombiner
    {
        public static void Combine(GameObject root, List<SkinnedMeshRenderer> smrsToCombine, string saveName = "CombinedMesh")
        {
            if (smrsToCombine == null || smrsToCombine.Count == 0 || root == null) return;

            string saveFolder = "Assets/Ezpzoptimizer/Generated";
            if (!Directory.Exists(saveFolder))
                Directory.CreateDirectory(saveFolder);

            // 1. Uniquify Materials and Bones
            List<Material> allMaterials = new List<Material>();
            List<Transform> allBones = new List<Transform>();
            List<Matrix4x4> bindposes = new List<Matrix4x4>();

            int totalVertices = 0;

            foreach (var smr in smrsToCombine)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                totalVertices += smr.sharedMesh.vertexCount;

                foreach (var mat in smr.sharedMaterials)
                {
                    if (mat != null && !allMaterials.Contains(mat))
                        allMaterials.Add(mat);
                }

                if (smr.bones != null && smr.bones.Length > 0 && smr.sharedMesh.bindposes != null)
                {
                    for (int i = 0; i < smr.bones.Length; i++)
                    {
                        var b = smr.bones[i];
                        if (b != null && !allBones.Contains(b))
                        {
                            allBones.Add(b);
                            // Bindpose is local to the SMR, but technically global bindpose for the root. 
                            // If they share the same skeleton, the bindpose for the same bone should be roughly identical.
                            // We just use the first one we find.
                            bindposes.Add(smr.sharedMesh.bindposes[i]);
                        }
                    }
                }
            }

            if (totalVertices == 0) return;

            // Prepare large arrays
            Mesh newMesh = new Mesh();
            newMesh.name = saveName;
            newMesh.indexFormat = totalVertices > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            List<Vector3> vertices = new List<Vector3>(totalVertices);
            List<Vector3> normals = new List<Vector3>(totalVertices);
            List<Vector4> tangents = new List<Vector4>(totalVertices);
            List<Vector2> uv = new List<Vector2>(totalVertices);
            List<Color32> colors = new List<Color32>(totalVertices);
            List<BoneWeight> boneWeights = new List<BoneWeight>(totalVertices);
            List<List<int>> submeshTriangles = new List<List<int>>();

            for (int i = 0; i < allMaterials.Count; i++)
                submeshTriangles.Add(new List<int>());

            // Collect Blendshape data
            Dictionary<string, List<BlendShapeFrame>> blendShapeMap = new Dictionary<string, List<BlendShapeFrame>>();
            int vertexOffset = 0;

            foreach (var smr in smrsToCombine)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh m = smr.sharedMesh;

                int vCount = m.vertexCount;
                if (vCount == 0) continue;

                vertices.AddRange(m.vertices);
                
                if (m.normals != null && m.normals.Length == vCount) normals.AddRange(m.normals);
                else normals.AddRange(new Vector3[vCount]);

                if (m.tangents != null && m.tangents.Length == vCount) tangents.AddRange(m.tangents);
                else tangents.AddRange(new Vector4[vCount]);

                if (m.uv != null && m.uv.Length == vCount) uv.AddRange(m.uv);
                else uv.AddRange(new Vector2[vCount]);

                if (m.colors32 != null && m.colors32.Length == vCount) colors.AddRange(m.colors32);
                else colors.AddRange(new Color32[vCount]);

                // Map bone weights
                var bWeights = m.boneWeights;
                if (bWeights != null && bWeights.Length == vCount)
                {
                    int[] boneIndexMap = new int[smr.bones.Length];
                    for (int i = 0; i < smr.bones.Length; i++)
                        boneIndexMap[i] = allBones.IndexOf(smr.bones[i]);

                    for (int i = 0; i < vCount; i++)
                    {
                        var bw = bWeights[i];
                        bw.boneIndex0 = boneIndexMap[bw.boneIndex0];
                        bw.boneIndex1 = boneIndexMap[bw.boneIndex1];
                        bw.boneIndex2 = boneIndexMap[bw.boneIndex2];
                        bw.boneIndex3 = boneIndexMap[bw.boneIndex3];
                        boneWeights.Add(bw);
                    }
                }
                else
                {
                    boneWeights.AddRange(new BoneWeight[vCount]);
                }

                // Submeshes and Triangles
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = s < smr.sharedMaterials.Length ? smr.sharedMaterials[s] : null;
                    if (mat == null) continue;

                    int matIndex = allMaterials.IndexOf(mat);
                    if (matIndex >= 0)
                    {
                        int[] tris = m.GetTriangles(s);
                        for (int t = 0; t < tris.Length; t++)
                            tris[t] += vertexOffset;
                        submeshTriangles[matIndex].AddRange(tris);
                    }
                }

                // BlendShapes
                int shapeCount = m.blendShapeCount;
                for (int s = 0; s < shapeCount; s++)
                {
                    string shapeName = m.GetBlendShapeName(s);
                    if (!blendShapeMap.ContainsKey(shapeName))
                        blendShapeMap[shapeName] = new List<BlendShapeFrame>();

                    int frames = m.GetBlendShapeFrameCount(s);
                    for (int f = 0; f < frames; f++)
                    {
                        float weight = m.GetBlendShapeFrameWeight(s, f);
                        Vector3[] dVerts = new Vector3[vCount];
                        Vector3[] dNorms = new Vector3[vCount];
                        Vector3[] dTans = new Vector3[vCount];
                        m.GetBlendShapeFrameVertices(s, f, dVerts, dNorms, dTans);

                        blendShapeMap[shapeName].Add(new BlendShapeFrame()
                        {
                            weight = weight,
                            vertexOffset = vertexOffset,
                            vCount = vCount,
                            deltaVertices = dVerts,
                            deltaNormals = dNorms,
                            deltaTangents = dTans
                        });
                    }
                }

                vertexOffset += vCount;
            }

            // Assign to new mesh
            newMesh.SetVertices(vertices);
            newMesh.SetNormals(normals);
            newMesh.SetTangents(tangents);
            newMesh.SetUVs(0, uv);
            newMesh.SetColors(colors);
            newMesh.boneWeights = boneWeights.ToArray();
            newMesh.bindposes = bindposes.ToArray();

            newMesh.subMeshCount = allMaterials.Count;
            for (int i = 0; i < allMaterials.Count; i++)
            {
                newMesh.SetTriangles(submeshTriangles[i], i);
            }

            // Combine Blendshapes
            // Groups frames by weight across all smrs with the same shape name
            foreach (var kvp in blendShapeMap)
            {
                string shapeName = kvp.Key;
                var frames = kvp.Value;
                
                // Group by weight
                var weightGroups = frames.GroupBy(f => f.weight).OrderBy(g => g.Key);
                
                foreach (var group in weightGroups)
                {
                    float weight = group.Key;
                    Vector3[] finalDVerts = new Vector3[totalVertices];
                    Vector3[] finalDNorms = new Vector3[totalVertices];
                    Vector3[] finalDTans = new Vector3[totalVertices];

                    foreach (var frame in group)
                    {
                        for (int i = 0; i < frame.vCount; i++)
                        {
                            finalDVerts[frame.vertexOffset + i] = frame.deltaVertices[i];
                            finalDNorms[frame.vertexOffset + i] = frame.deltaNormals[i];
                            finalDTans[frame.vertexOffset + i] = frame.deltaTangents[i];
                        }
                    }

                    newMesh.AddBlendShapeFrame(shapeName, weight, finalDVerts, finalDNorms, finalDTans);
                }
            }

            // Save Mesh
            string safeName = string.Join("_", saveName.Split(Path.GetInvalidFileNameChars()));
            string path = $"{saveFolder}/{safeName}_{System.Guid.NewGuid().ToString().Substring(0, 5)}.asset";
            AssetDatabase.CreateAsset(newMesh, path);

            // Create new SMR GameObject
            GameObject combinedGo = new GameObject(saveName);
            combinedGo.transform.SetParent(root.transform, false);
            
            // Match transform of the first mesh for path consistency
            combinedGo.transform.localPosition = smrsToCombine[0].transform.localPosition;
            combinedGo.transform.localRotation = smrsToCombine[0].transform.localRotation;
            combinedGo.transform.localScale = smrsToCombine[0].transform.localScale;

            var newSmr = combinedGo.AddComponent<SkinnedMeshRenderer>();
            
            newSmr.sharedMesh = newMesh;
            newSmr.sharedMaterials = allMaterials.ToArray();
            newSmr.bones = allBones.ToArray();
            
            // Try to keep the root bone from the original meshes
            var firstSmr = smrsToCombine.FirstOrDefault(s => s != null && s.rootBone != null);
            newSmr.rootBone = firstSmr != null ? firstSmr.rootBone : (allBones.Count > 0 ? allBones[0] : root.transform);

            Undo.RegisterCreatedObjectUndo(combinedGo, "Combine Meshes");

            // Destroy old SMRs to clean up the hierarchy
            // [Fix] Update VRChat Descriptor references before destroying
#if VRC_SDK_EXISTS
            var descriptor = root.GetComponentInParent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                if (descriptor.VisemeSkinnedMesh != null && smrsToCombine.Contains(descriptor.VisemeSkinnedMesh))
                    descriptor.VisemeSkinnedMesh = newSmr;
                
                if (descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null && smrsToCombine.Contains(descriptor.customEyeLookSettings.eyelidsSkinnedMesh))
                    descriptor.customEyeLookSettings.eyelidsSkinnedMesh = newSmr;
            }
#endif

            foreach (var smr in smrsToCombine)
            {
                if (smr != null && smr.gameObject != null)
                {
                    Undo.DestroyObjectImmediate(smr.gameObject);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        class BlendShapeFrame
        {
            public float weight;
            public int vertexOffset;
            public int vCount;
            public Vector3[] deltaVertices;
            public Vector3[] deltaNormals;
            public Vector3[] deltaTangents;
        }
    }
}

