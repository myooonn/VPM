using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class MABakeExporter
    {
        const string MA_PROCESSOR_TYPE = "nadena.dev.modular_avatar.core.editor.AvatarProcessor";

        public static bool IsMAAvailable()
        {
            string[] typeNames = {
                "nadena.dev.modular_avatar.core.ModularAvatarMeshSettings, nadena.dev.modular_avatar.core",
                "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy, nadena.dev.modular_avatar.core",
                "nadena.dev.modular_avatar.core.AvatarTagComponent, nadena.dev.modular_avatar.core",
            };
            foreach (var t in typeNames)
                if (System.Type.GetType(t) != null) return true;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = asm.FullName;
                if (asmName.Contains("modular_avatar") || asmName.Contains("ModularAvatar") || asmName.Contains("nadena.dev"))
                    return true;
            }

            return false;
        }

        public static GameObject Bake(GameObject avatar)
        {
            if (avatar == null) return null;

            var copy = Object.Instantiate(avatar);
            copy.name = avatar.name + "_MABaked";
            copy.SetActive(true);

            Undo.RecordObject(avatar, "MA Bake Export");
            avatar.SetActive(false);
            Undo.RegisterCreatedObjectUndo(copy, "MA Bake Export");

            if (IsMAAvailable())
                RunMAProcessor(copy);

            return copy;
        }

        static void RunMAProcessor(GameObject avatar)
        {
            System.Type processorType = null;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.FullName.Contains("modular_avatar") && !asm.FullName.Contains("nadena.dev")) continue;
                processorType = asm.GetType(MA_PROCESSOR_TYPE);
                if (processorType != null) break;
            }

            if (processorType == null) return;

            var method = processorType.GetMethod(
                "ProcessAvatar",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(GameObject) },
                null
            );

            method?.Invoke(null, new object[] { avatar });
        }
    }
}
