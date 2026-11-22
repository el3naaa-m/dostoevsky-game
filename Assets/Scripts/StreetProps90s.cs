using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NightChase
{
    public sealed class StreetProps90s : MonoBehaviour
    {
        const string AssetPath = "StreetProps90s/";
        readonly List<Material> materials = new();
        readonly Dictionary<Font, Material> labels = new();
        readonly Dictionary<MeshRenderer, Material> oldLabels = new();
        Shader alphaShader;
        Mesh quad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= AddToStreet;
            SceneManager.sceneLoaded += AddToStreet;
        }

        static void AddToStreet(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ChaseOpening.ScenePath) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<StreetProps90s>(true)) return;

            var props = new GameObject("Props_90s");
            SceneManager.MoveGameObjectToScene(props, scene);
            props.AddComponent<StreetProps90s>();
        }

        void Awake()
        {
            alphaShader = Resources.Load<Shader>(AssetPath + "StreetAlphaDepth");
            FixLabels();
            AddTag("WallGreen", new Vector3(36f, .94f, 17.15f), Vector3.right, new Vector2(1.83f, .59f));
            AddTag("WallAmber", new Vector3(36f, 2.94f, 4.8f), Vector3.right, new Vector2(2.00f, .60f));
            AddTag("WallBlue", new Vector3(36f, .94f, -9.15f), Vector3.right, new Vector2(1.81f, .59f));
            AddTag("FenceRose", new Vector3(38.75f, 1.09f, 11.65f), Vector3.left, new Vector2(1.78f, .58f));

            var dumpsterSize = new Vector3(1.65f, 1.03f, 1.05f);
            AddProp("Dumpster", "Dumpster by the turn", new Vector3(30.5f, 0, 22.87f), dumpsterSize);
            AddProp("Dumpster", "Second dumpster by the turn", new Vector3(33.8f, 0, 22.87f), dumpsterSize);
            var dimensions = new Vector3(.26f, .34f, .26f);
            AddProp("Cone", "Cone by alley entry", new Vector3(38.43f, 0, 18.7f), dimensions);
            AddProp("Cone", "Cone by first dumpster", new Vector3(36.51f, 0, 13.45f), dimensions);
            AddProp("Cone", "Cone by second dumpster", new Vector3(38.43f, 0, 6.35f), dimensions);
            AddProp("Cone", "Cone by alley exit", new Vector3(36.52f, 0, -12.15f), dimensions);
        }

        void AddTag(string textureName, Vector3 wallPoint, Vector3 normal, Vector2 size)
        {
            if (!alphaShader) return;
            var texture = Resources.Load<Texture2D>(AssetPath + textureName);
            if (!texture) return;
            if (!quad)
            {
                quad = new Mesh { name = "Street graffiti quad" };
                quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                    new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateBounds();
            }

            var tag = new GameObject("Graffiti — " + textureName);
            tag.transform.SetParent(transform, false);
            tag.transform.SetPositionAndRotation(wallPoint + normal * .01f, Quaternion.LookRotation(normal));
            tag.transform.localScale = new Vector3(size.x, size.y, 1);
            tag.AddComponent<MeshFilter>().sharedMesh = quad;
            var ink = new Material(alphaShader) { mainTexture = texture, color = new Color(.46f,.44f,.42f,.77f) };
            materials.Add(ink);
            tag.AddComponent<MeshRenderer>().sharedMaterial = ink;
        }

        void AddProp(string asset, string name, Vector3 position, Vector3 size)
        {
            var prefab = Resources.Load<GameObject>(AssetPath + asset);
            if (!prefab) return;
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            var model = Instantiate(prefab, root.transform, false).transform;
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
            {
                Destroy(root);
                return;
            }

            Bounds bounds = BoundsOf(renderers);
            if (bounds.size.x <= .001f || bounds.size.y <= .001f || bounds.size.z <= .001f)
            {
                Destroy(root);
                return;
            }

            model.localScale = Vector3.Scale(model.localScale,
                new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            bounds = BoundsOf(renderers);
            model.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            UseUrpMaterials(renderers);

            var collision = root.AddComponent<BoxCollider>();
            collision.center = Vector3.up * (size.y * .5f);
            collision.size = size;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collision.center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        void UseUrpMaterials(MeshRenderer[] renderers)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) return;
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (!source || source.shader == lit) continue;
                    var material = new Material(lit) { color = source.color };
                    material.SetFloat("_Smoothness", .19f);
                    materials.Add(material);
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }
        }

        void FixLabels()
        {
            if (!alphaShader) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            {
                var font = text.font;
                var renderer = text.GetComponent<MeshRenderer>();
                if (!font || !renderer) continue;
                if (!labels.TryGetValue(font, out var depthMaterial))
                {
                    depthMaterial = new Material(alphaShader) { name = font.name + " Street depth" };
                    depthMaterial.SetFloat("_Glyph", 1f);
                    labels.Add(font, depthMaterial);
                    materials.Add(depthMaterial);
                    RefreshFont(font);
                }
                oldLabels.Add(renderer, renderer.sharedMaterial);
                renderer.sharedMaterial = depthMaterial;
            }
            Font.textureRebuilt += RefreshFont;
        }

        void RefreshFont(Font font)
        {
            if (labels.TryGetValue(font, out var material) && font.material)
                material.mainTexture = font.material.mainTexture;
        }

        static Bounds BoundsOf(MeshRenderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshFont;
            foreach (var pair in oldLabels)
                if (pair.Key) pair.Key.sharedMaterial = pair.Value;
            foreach (var material in materials) Destroy(material);
            if (quad) Destroy(quad);
        }
    }
}
