using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>Project-level settings: sorting layers, physics layers, TextMeshPro resources, player settings.</summary>
    public static class ProjectSetup
    {
        [MenuItem("Tools/RPG/Steps/1. Setup Project Settings", priority = 101)]
        public static void Run()
        {
            EnsureSortingLayers();
            EnsureLayers();
            ImportTMPEssentials();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[RPG] Project settings ready.");
        }

        static SerializedObject TagManager() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        public static void EnsureSortingLayers()
        {
            var tm = TagManager();
            var layers = tm.FindProperty("m_SortingLayers");
            var names = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
                names.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            foreach (var n in SortingLayerNames.All)
            {
                if (names.Contains(n)) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var e = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                e.FindPropertyRelative("name").stringValue = n;
                e.FindPropertyRelative("uniqueID").intValue = (n.GetHashCode() & 0x7FFFFFFF) | 1;
                var locked = e.FindPropertyRelative("locked");
                if (locked != null) locked.boolValue = false;
                names.Add(n);
            }
            // order them as declared (array order = draw order)
            for (int target = 0; target < SortingLayerNames.All.Length; target++)
            {
                string want = SortingLayerNames.All[target];
                int cur = -1;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == want) cur = i;
                if (cur >= 0 && cur != target) layers.MoveArrayElement(cur, target);
            }
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureLayers()
        {
            var tm = TagManager();
            var layers = tm.FindProperty("layers");
            var want = new Dictionary<int, string>
            {
                { Layers.Player, "Player" }, { Layers.Enemy, "Enemy" }, { Layers.Obstacle, "Obstacle" },
                { Layers.Projectile, "Projectile" }, { Layers.Pickup, "Pickup" }, { Layers.NPC, "NPC" },
            };
            foreach (var kv in want) layers.GetArrayElementAtIndex(kv.Key).stringValue = kv.Value;
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ImportTMPEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            if (info == null)
            {
                Debug.LogError("[RPG] com.unity.ugui package not found.");
                return;
            }
            string pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg))
            {
                Debug.LogError("[RPG] TMP Essential Resources not found at " + pkg);
                return;
            }
#pragma warning disable 618
            AssetDatabase.ImportPackage(pkg, false);
#pragma warning restore 618
            AssetDatabase.Refresh();
            Debug.Log("[RPG] Imported TMP Essential Resources.");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "IndieRPG";
            PlayerSettings.productName = "Rung Thi Tham";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }
    }
}
