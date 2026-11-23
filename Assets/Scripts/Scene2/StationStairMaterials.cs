using System.Collections.Generic;
using UnityEngine;

namespace NightChase.Station
{
    internal sealed class StationStairMaterials : MonoBehaviour
    {
        readonly Dictionary<Material, Material> replacements = new();

        internal void Repair()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) return;
            var stair = new Bounds(new Vector3(-10.5f,2.8f,23.1f),new Vector3(6f,7f,8f));
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.bounds.Intersects(stair)) continue;
                    var slots = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (source && source.shader && source.shader.isSupported &&
                            source.shader.name != "Hidden/InternalErrorShader" && source.shader.name != "Standard") continue;
                        if (!source)
                        {
                            slots[i] = Replacement(lit,null);
                            changed = true;
                            continue;
                        }
                        if (!replacements.TryGetValue(source,out var material))
                            replacements[source] = material = Replacement(lit,source);
                        slots[i] = material;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = slots;
                }
        }

        readonly List<Material> owned = new();
        Material Replacement(Shader shader,Material source)
        {
            var material = new Material(shader) { color = new Color(.54f,.53f,.51f) };
            owned.Add(material);
            if (source && source.HasProperty("_Color")) material.color = source.color;
            if (source && source.HasProperty("_MainTex")) material.mainTexture = source.mainTexture;
            return material;
        }

        void OnDestroy() { foreach (var material in owned) Destroy(material); }
    }
}
