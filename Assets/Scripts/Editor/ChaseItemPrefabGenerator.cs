using System;
using UnityEditor;
using UnityEngine;

namespace NightChase
{
    static class ChaseItemPrefabGenerator
    {
        [MenuItem("Tools/Night Chase/Create Item Prefabs")]
        static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Items")) AssetDatabase.CreateFolder("Assets/Prefabs", "Items");
            foreach (InventoryItem item in Enum.GetValues(typeof(InventoryItem)))
            {
                string path = $"Assets/Prefabs/Items/{item}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
                var root = ItemProps.Create(item);
                try
                {
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        Color color = block.GetColor("_BaseColor");
                        var shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (!shader) continue;
                        string matPath = $"Assets/Prefabs/Items/ItemColor_{ColorUtility.ToHtmlStringRGB(color)}.mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (!material)
                        {
                            material = new Material(shader);
                            material.SetColor("_BaseColor", color);
                            AssetDatabase.CreateAsset(material, matPath);
                        }
                        renderer.sharedMaterial = material;
                        renderer.SetPropertyBlock(null);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
