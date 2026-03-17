using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class AnimationPathFixer
    {
        public class BrokenBinding
        {
            public AnimationClip clip;
            public EditorCurveBinding binding;
            public bool isObjectRef;
            public string suggestedPath;
            public bool selected;
            public bool isMaybeMAManaged;
        }

        public static List<BrokenBinding> Scan(GameObject avatar)
        {
            var broken = new List<BrokenBinding>();
            if (avatar == null) return broken;

            var allTransforms = avatar.GetComponentsInChildren<Transform>(true);
            var nameMap = new Dictionary<string, List<Transform>>();
            foreach (var t in allTransforms)
            {
                if (!nameMap.ContainsKey(t.name)) nameMap[t.name] = new List<Transform>();
                nameMap[t.name].Add(t);
            }

            var clips = CollectAllClips(avatar);
            var seen = new HashSet<string>();

            foreach (var clip in clips)
            {
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(b.path) && avatar.transform.Find(b.path) == null)
                    {
                        string key = $"{clip.GetInstanceID()}|{b.path}|{b.propertyName}";
                        if (seen.Add(key))
                            broken.Add(MakeEntry(avatar, clip, b, false, nameMap));
                    }
                }
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(b.path) && avatar.transform.Find(b.path) == null)
                    {
                        string key = $"{clip.GetInstanceID()}|{b.path}|{b.propertyName}|ref";
                        if (seen.Add(key))
                            broken.Add(MakeEntry(avatar, clip, b, true, nameMap));
                    }
                }
            }

            return broken;
        }

        static BrokenBinding MakeEntry(GameObject avatar, AnimationClip clip, EditorCurveBinding b, bool isRef, Dictionary<string, List<Transform>> nameMap)
        {
            string objName = b.path.Contains("/")
                ? b.path.Substring(b.path.LastIndexOf('/') + 1)
                : b.path;

            bool existsAnywhere = nameMap.ContainsKey(objName) && nameMap[objName].Count > 0;
            string suggested = existsAnywhere ? FindCorrectPath(avatar, b.path, nameMap) : null;

            return new BrokenBinding
            {
                clip = clip,
                binding = b,
                isObjectRef = isRef,
                suggestedPath = suggested,
                selected = suggested != null,
                isMaybeMAManaged = !existsAnywhere
            };
        }

        static string FindCorrectPath(GameObject avatar, string brokenPath, Dictionary<string, List<Transform>> nameMap)
        {
            string objName = brokenPath.Contains("/")
                ? brokenPath.Substring(brokenPath.LastIndexOf('/') + 1)
                : brokenPath;

            if (!nameMap.TryGetValue(objName, out var matches)) return null;
            var valid = matches.Where(t => t != avatar.transform).ToList();
            if (valid.Count == 1)
                return GetPath(avatar.transform, valid[0]);

            return null;
        }

        static string GetPath(Transform root, Transform target)
        {
            var parts = new Stack<string>();
            var cur = target;
            while (cur != null && cur != root)
            {
                parts.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", parts);
        }

        public static int Fix(List<BrokenBinding> bindings)
        {
            int count = 0;
            var targets = bindings.Where(b => b.selected && b.suggestedPath != null).GroupBy(b => b.clip);

            foreach (var group in targets)
            {
                var clip = group.Key;
                Undo.RecordObject(clip, "Fix Animation Paths");

                foreach (var entry in group)
                {
                    if (entry.isObjectRef)
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, entry.binding);
                        AnimationUtility.SetObjectReferenceCurve(clip, entry.binding, null);
                        var nb = entry.binding;
                        nb.path = entry.suggestedPath;
                        AnimationUtility.SetObjectReferenceCurve(clip, nb, keys);
                    }
                    else
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, entry.binding);
                        AnimationUtility.SetEditorCurve(clip, entry.binding, null);
                        var nb = entry.binding;
                        nb.path = entry.suggestedPath;
                        AnimationUtility.SetEditorCurve(clip, nb, curve);
                    }
                    count++;
                }

                EditorUtility.SetDirty(clip);
            }

            if (count > 0)
                AssetDatabase.SaveAssets();

            return count;
        }

        static HashSet<AnimationClip> CollectAllClips(GameObject avatar)
        {
            var clips = new HashSet<AnimationClip>();

            foreach (var clip in AnimationUtility.GetAnimationClips(avatar))
                if (clip != null) clips.Add(clip);

            foreach (var animator in avatar.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null) continue;
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null) clips.Add(clip);
            }

#if VRC_SDK_EXISTS
            var desc = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (desc != null)
            {
                if (desc.baseAnimationLayers != null)
                {
                    foreach (var layer in desc.baseAnimationLayers)
                        if (layer.animatorController != null)
                            foreach (var clip in layer.animatorController.animationClips)
                                if (clip != null) clips.Add(clip);
                }

                if (desc.specialAnimationLayers != null)
                {
                    foreach (var layer in desc.specialAnimationLayers)
                        if (layer.animatorController != null)
                            foreach (var clip in layer.animatorController.animationClips)
                                if (clip != null) clips.Add(clip);
                }
            }
#endif

            return clips;
        }
    }
}
