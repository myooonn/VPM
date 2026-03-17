// Ezpzoptimizer - Bone Optimizer
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class BoneOptimizer
    {
        public static int Optimize(GameObject avatar)
        {
            if (avatar == null) return 0;

            var protectedBones = new HashSet<Transform>();
            
            // Function to protect a transform and all of its parents up to the avatar root
            System.Action<Transform> protectChain = (t) => {
                var curr = t;
                while (curr != null && curr != avatar.transform)
                {
                    if (!protectedBones.Add(curr)) break;
                    curr = curr.parent;
                }
            };

            // 1. Protect all bones used by SkinnedMeshRenderers
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b != null) protectChain(b);
                }
                // Also protect the mesh object itself
                protectChain(smr.transform);
            }

            // 2. Protect any object that has ANY component other than Transform
            var allComponents = avatar.GetComponentsInChildren<Component>(true);
            foreach (var c in allComponents)
            {
                if (c == null || c is Transform) continue;
                // If it has a component, protect it and its parents
                protectChain(c.transform);
                
                // Special case for PhysBone: keep the whole child chain starting from this root
                string typeName = c.GetType().FullName;
                if (typeName != null && typeName.Contains("VRCPhysBone") && !typeName.Contains("Collider"))
                {
                    foreach (var t in c.transform.GetComponentsInChildren<Transform>(true))
                        protectedBones.Add(t);
                }
            }

            // 3. Protect Humanoid bones if Animator exists
            var animator = avatar.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                foreach (HumanBodyBones hbb in System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (hbb == HumanBodyBones.LastBone) continue;
                    var t = animator.GetBoneTransform(hbb);
                    if (t != null) protectChain(t);
                }
            }

            // 4. Prune unused transforms (Bottom-up)
            var allTransforms = avatar.GetComponentsInChildren<Transform>(true).ToList();
            allTransforms.Reverse();

            int count = 0;
            foreach (var t in allTransforms)
            {
                if (t == avatar.transform) continue;
                if (protectedBones.Contains(t)) continue;

                // Extra safety: never delete if it has components or child nodes (should be protected anyway)
                if (t.GetComponents<Component>().Length > 1) continue;
                if (t.childCount > 0) continue;

                Undo.DestroyObjectImmediate(t.gameObject);
                count++;
            }

            return count;
        }

        public static int DeleteSelectedBones(List<Transform> bones)
        {
            int count = 0;
            foreach (var t in bones)
            {
                if (t != null && t.gameObject != null)
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    count++;
                }
            }
            return count;
        }

        public static int DeleteSelectedComponents(List<Transform> targets)
        {
            int count = 0;
            foreach (var t in targets)
            {
                if (t == null) continue;

                // Find specific VRC components or Constraints
                var comps = t.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c is Transform) continue;

                    string typeName = c.GetType().FullName;
                    if (typeName.Contains("VRCPhysBone") || 
                        typeName.Contains("VRCContact") ||
                        typeName.Contains("Constraint"))
                    {
                        Undo.DestroyObjectImmediate(c);
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
