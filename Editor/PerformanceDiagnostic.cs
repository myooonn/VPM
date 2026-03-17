// Ezpzoptimizer - Performance Diagnostic
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class PerformanceDiagnostic
    {
        public class AvatarStats
        {
            public int polygons;
            public int skinnedMeshes;
            public int materialSlots;
            public int bones;
            public int physBoneComponents;
            public int physBoneTransforms;
            
            // Official VRChat PC Performance Thresholds (Poor limit)
            // Excellent / Good / Medium / Poor
            // Beyond Poor is Very Poor.
            
            public string PolyRank => polygons > 70000 ? "VeryPoor" : polygons > 32000 ? "Poor" : polygons > 32000 ? "Medium" : "Excellent"; // Note: Triangles are 32k/70k
            public string SkinnedRank => skinnedMeshes > 16 ? "VeryPoor" : skinnedMeshes > 8 ? "Poor" : skinnedMeshes > 2 ? "Medium" : skinnedMeshes > 1 ? "Good" : "Excellent";
            public string MatRank => materialSlots > 32 ? "VeryPoor" : materialSlots > 16 ? "Poor" : materialSlots > 8 ? "Medium" : materialSlots > 4 ? "Good" : "Excellent";
            public string BonesRank => bones > 400 ? "VeryPoor" : bones > 256 ? "Poor" : bones > 150 ? "Medium" : bones > 75 ? "Good" : "Excellent";
            public string PBRank => physBoneComponents > 32 ? "VeryPoor" : physBoneComponents > 16 ? "Poor" : physBoneComponents > 8 ? "Medium" : physBoneComponents > 4 ? "Good" : "Excellent";
            public string PBTransRank => physBoneTransforms > 256 ? "VeryPoor" : physBoneTransforms > 128 ? "Poor" : physBoneTransforms > 64 ? "Medium" : physBoneTransforms > 16 ? "Good" : "Excellent";
            
            // Helper for overall rank
            public string OverallRank
            {
                get {
                    var ranks = new List<string> { PolyRank, SkinnedRank, MatRank, BonesRank, PBRank, PBTransRank };
                    if (ranks.Contains("VeryPoor")) return "VeryPoor";
                    if (ranks.Contains("Poor")) return "Poor";
                    if (ranks.Contains("Medium")) return "Medium";
                    if (ranks.Contains("Good")) return "Good";
                    return "Excellent";
                }
            }
        }

        public class DiagResult
        {
            public List<AudioSource> unusedAudio = new List<AudioSource>();
            public List<ParticleSystem> emptyParticles = new List<ParticleSystem>();
            public int missingScriptCount = 0;
            public List<GameObject> missingScriptObjects = new List<GameObject>();
            public List<Component> tooManyPhysBones = new List<Component>();
            public AvatarStats stats = new AvatarStats();
        }

        public static DiagResult Scan(GameObject root)
        {
            var result = new DiagResult();
            if (root == null) return result;

            // 1. Unused / Useless AudioSources
            var audios = root.GetComponentsInChildren<AudioSource>(true);
            foreach (var a in audios)
            {
                if (!a.gameObject.activeInHierarchy) continue;
                if (a.clip == null || a.volume <= 0.001f)
                    result.unusedAudio.Add(a);
            }

            // 2. Empty Particle Systems
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in particles)
            {
                if (!p.gameObject.activeInHierarchy) continue;
                if (!p.emission.enabled || p.emission.rateOverTime.constant <= 0 && p.emission.rateOverDistance.constant <= 0)
                {
                    result.emptyParticles.Add(p);
                }
            }

            // 3. Missing Scripts
            ScanMissingScripts(root, result);

            // 4. Stats Check
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bonesSet = new HashSet<Transform>();
            foreach (var r in renderers)
            {
                if (!r.gameObject.activeInHierarchy) continue;

                if (r is SkinnedMeshRenderer smr)
                {
                    result.stats.skinnedMeshes++;
                    if (smr.sharedMesh != null)
                        result.stats.polygons += smr.sharedMesh.triangles.Length / 3;
                    if (smr.bones != null)
                    {
                        foreach (var b in smr.bones)
                            if (b != null) bonesSet.Add(b);
                    }
                }
                else if (r is MeshRenderer mr)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        result.stats.polygons += mf.sharedMesh.triangles.Length / 3;
                }
                
                if (r.sharedMaterials != null)
                    result.stats.materialSlots += r.sharedMaterials.Length;
            }
            result.stats.bones = bonesSet.Count;

#if VRC_SDK_EXISTS
            // 5. PhysBone stats
            var pbs = root.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true);
            int pbCompCount = 0;
            foreach (var pb in pbs)
            {
                if (!pb.gameObject.activeInHierarchy) continue;
                pbCompCount++;
                
                var tr = pb.GetRootTransform();
                if (tr != null)
                {
                    int transCount = tr.GetComponentsInChildren<Transform>(true).Length;
                    result.stats.physBoneTransforms += transCount;
                }
            }
            result.stats.physBoneComponents = pbCompCount;

            if (pbCompCount > 16)
            {
                int count = 0;
                foreach (var pb in pbs)
                {
                    if (!pb.gameObject.activeInHierarchy) continue;
                    count++;
                    if (count > 16) result.tooManyPhysBones.Add(pb);
                }
            }
#endif

            return result;
        }

        static void ScanMissingScripts(GameObject obj, DiagResult res)
        {
            if (!obj.activeInHierarchy) return;

            var components = obj.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    res.missingScriptCount++;
                    if (!res.missingScriptObjects.Contains(obj))
                        res.missingScriptObjects.Add(obj);
                }
            }
            for (int i = 0; i < obj.transform.childCount; i++)
                ScanMissingScripts(obj.transform.GetChild(i).gameObject, res);
        }

        public static int RemoveUnusedAudio(List<AudioSource> list)
        {
            int count = 0;
            foreach (var item in list)
            {
                if (item != null)
                {
                    Undo.DestroyObjectImmediate(item);
                    count++;
                }
            }
            return count;
        }

        public static int DisableEmptyParticles(List<ParticleSystem> list)
        {
            int count = 0;
            foreach (var item in list)
            {
                if (item != null && item.gameObject.activeSelf)
                {
                    Undo.RecordObject(item.gameObject, "Disable Particle System");
                    item.gameObject.SetActive(false);
                    count++;
                }
            }
            return count;
        }

        public static int CleanMissingScripts(List<GameObject> objects)
        {
            int count = 0;
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                Undo.RegisterCompleteObjectUndo(obj, "Remove Missing Scripts");
                int serializedRemoved = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                count += serializedRemoved;
            }
            return count;
        }
        public static int RemovePhysBones(List<Component> list)
        {
            int count = 0;
            foreach (var item in list)
            {
                if (item != null)
                {
                    Undo.DestroyObjectImmediate(item);
                    count++;
                }
            }
            return count;
        }
    }
}
