// Ezpzoptimizer - BlendShape Optimizer
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class BlendShapeOptimizer
    {
        public static Mesh CreateOptimizedMesh(SkinnedMeshRenderer smr, HashSet<string> usedShapes, out int removedCount)
        {
            removedCount = 0;
            if (smr == null || smr.sharedMesh == null) return null;

            Mesh sourceMesh = smr.sharedMesh;
            int shapeCount = sourceMesh.blendShapeCount;
            if (shapeCount == 0) return null;

            List<int> validShapes = new List<int>();
            for (int i = 0; i < shapeCount; i++)
            {
                // Keep shapes that are currently non-zero OR referenced by animation clips.
                float weight = smr.GetBlendShapeWeight(i);
                string shapeName = sourceMesh.GetBlendShapeName(i);
                bool referenced = usedShapes != null && usedShapes.Contains(shapeName);

                if (referenced || weight > 0.001f || weight < -0.001f)
                {
                    validShapes.Add(i);
                }
            }

            removedCount = shapeCount - validShapes.Count;
            if (removedCount == 0) return null; // No need to optimize

            // Duplicate the mesh
            Mesh newMesh = Object.Instantiate(sourceMesh);
            newMesh.name = sourceMesh.name + "_Optimized";
            newMesh.ClearBlendShapes();

            // Copy over the valid blend shapes
            Vector3[] deltaVertices = new Vector3[sourceMesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[sourceMesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[sourceMesh.vertexCount];

            foreach (int i in validShapes)
            {
                string shapeName = sourceMesh.GetBlendShapeName(i);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(i);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float frameWeight = sourceMesh.GetBlendShapeFrameWeight(i, frame);
                    sourceMesh.GetBlendShapeFrameVertices(i, frame, deltaVertices, deltaNormals, deltaTangents);
                    newMesh.AddBlendShapeFrame(shapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
            }

            return newMesh;
        }

        public static int OptimizeAvatar(GameObject avatar)
        {
            if (avatar == null) return 0;

            // Trace blendshape usage from animation clips (AvatarOptimizer-style).
            var usedByPath = BuildUsedBlendShapeMap(avatar);

            string saveFolder = "Assets/Ezpzoptimizer/Generated";
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            int totalRemoved = 0;
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null) continue;

                // Record current weights by shape name
                Dictionary<string, float> activeWeights = new Dictionary<string, float>();
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    float w = smr.GetBlendShapeWeight(i);
                    activeWeights[smr.sharedMesh.GetBlendShapeName(i)] = w;
                }

                int removed;
                string path = GetTransformPath(avatar.transform, smr.transform);
                usedByPath.TryGetValue(path, out var usedShapes);
                Mesh newMesh = CreateOptimizedMesh(smr, usedShapes, out removed);
                if (newMesh != null && removed > 0)
                {
                    totalRemoved += removed;

                    string safeName = string.Join("_", newMesh.name.Split(Path.GetInvalidFileNameChars()));
                    string assetPath = $"{saveFolder}/{safeName}_{smr.gameObject.GetInstanceID()}.asset";

                    AssetDatabase.CreateAsset(newMesh, assetPath);

                    Undo.RecordObject(smr, "Optimize BlendShapes");
                    smr.sharedMesh = newMesh;

                    // Remap weights
                    for (int i = 0; i < newMesh.blendShapeCount; i++)
                    {
                        string shapeName = newMesh.GetBlendShapeName(i);
                        if (activeWeights.TryGetValue(shapeName, out float w))
                        {
                            smr.SetBlendShapeWeight(i, w);
                        }
                    }
                }
            }

            if (totalRemoved > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return totalRemoved;
        }

        static Dictionary<string, HashSet<string>> BuildUsedBlendShapeMap(GameObject avatar)
        {
            var map = new Dictionary<string, HashSet<string>>();
            if (avatar == null) return map;

            var clips = new HashSet<AnimationClip>();

            // Legacy Animation component clips
            foreach (var clip in AnimationUtility.GetAnimationClips(avatar))
            {
                if (clip != null) clips.Add(clip);
            }

            // Animator controllers / overrides
            var animators = avatar.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator == null) continue;
                var controller = animator.runtimeAnimatorController;
                if (controller != null)
                {
                    foreach (var clip in controller.animationClips)
                        if (clip != null) clips.Add(clip);
                }
            }

#if VRC_SDK_EXISTS
            // VRChat Playable Layers
            var descriptor = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                // Playable Layers (Base, Special, Action, etc.)
                foreach (var layer in descriptor.baseAnimationLayers)
                {
                    if (layer.animatorController != null)
                        foreach (var clip in layer.animatorController.animationClips)
                            if (clip != null) clips.Add(clip);
                }
                foreach (var layer in descriptor.specialAnimationLayers)
                {
                    if (layer.animatorController != null)
                        foreach (var clip in layer.animatorController.animationClips)
                            if (clip != null) clips.Add(clip);
                }

                // --- Explicit VRC Protection (Critical for LipSync and Eyelids) ---
                // 1. LipSync
                if (descriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape && descriptor.VisemeSkinnedMesh != null)
                {
                    string path = GetTransformPath(avatar.transform, descriptor.VisemeSkinnedMesh.transform);
                    if (!map.TryGetValue(path, out var set)) { set = new HashSet<string>(); map[path] = set; }
                    foreach (var viseme in descriptor.VisemeBlendShapes)
                    {
                        if (!string.IsNullOrEmpty(viseme)) set.Add(viseme);
                    }
                }

                // 2. Eyelids
                if (descriptor.customEyeLookSettings.eyelidType == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.EyelidType.Blendshapes && descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
                {
                    var eyeMesh = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                    string path = GetTransformPath(avatar.transform, eyeMesh.transform);
                    if (!map.TryGetValue(path, out var set)) { set = new HashSet<string>(); map[path] = set; }
                    
                    foreach (var idx in descriptor.customEyeLookSettings.eyelidsBlendshapes)
                    {
                        if (idx >= 0 && eyeMesh.sharedMesh != null && idx < eyeMesh.sharedMesh.blendShapeCount)
                            set.Add(eyeMesh.sharedMesh.GetBlendShapeName(idx));
                    }
                }
            }
#endif

            // Safety: Protect common VRChat/Tracking BlendShapes by name
            string[] safetyNames = { "vrc.", "eye_look", "blink", "lowerlid", "mouth_", "jaw_", "cheek_", "tongue_" };

            foreach (var clip in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var b in bindings)
                {
                    if (b.type != typeof(SkinnedMeshRenderer)) continue;
                    if (string.IsNullOrEmpty(b.propertyName)) continue;
                    if (!b.propertyName.StartsWith("blendShape.")) continue;

                    string shapeName = b.propertyName.Substring("blendShape.".Length);
                    if (string.IsNullOrEmpty(shapeName)) continue;

                    string path = b.path ?? string.Empty;
                    if (!map.TryGetValue(path, out var set))
                    {
                        set = new HashSet<string>();
                        map[path] = set;
                    }
                    set.Add(shapeName);
                }
            }

            // Apply safety names to all paths
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null) continue;
                string path = GetTransformPath(avatar.transform, smr.transform);
                if (!map.TryGetValue(path, out var set))
                {
                    set = new HashSet<string>();
                    map[path] = set;
                }

                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string name = smr.sharedMesh.GetBlendShapeName(i).ToLower();
                    foreach (var safety in safetyNames)
                    {
                        if (name.Contains(safety))
                        {
                            set.Add(smr.sharedMesh.GetBlendShapeName(i));
                            break;
                        }
                    }
                }
            }

            return map;
        }

        static string GetTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            if (root == target) return string.Empty;

            var stack = new Stack<string>();
            var current = target;
            while (current != null && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }
            if (current != root) return string.Empty;

            return string.Join("/", stack.ToArray());
        }
    }
}

