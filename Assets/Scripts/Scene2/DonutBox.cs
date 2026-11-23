using System.Collections.Generic;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class DonutBox : StationInteractable
    {
        [SerializeField] GameObject boxPrefab;
        [SerializeField] Transform[] donuts;
        readonly List<Material> materials = new();
        Mesh ring, icing;
        int remaining;
        bool built;
        void Start() => Build();

        internal void Build()
        {
            if (built) return;
            built = true;
            if (boxPrefab) Instantiate(boxPrefab,transform,false);
            if (donuts == null || donuts.Length == 0) MakeContents();
            remaining = donuts.Length;
        }

        public override string Prompt(Scene2FlowManager flow) =>
            remaining > 0 ? $"E — Съесть пончик   ({remaining})" : "E — Осмотреть коробку";

        public override void Use(Scene2FlowManager flow)
        {
            if (remaining == 0) { flow.Notify("Пончики закончились.",2); return; }
            var donut = donuts[--remaining];
            if (donut) donut.gameObject.SetActive(false);
            flow.Audio.PlayOneShot(flow.Audio.paper);
            flow.Notify(remaining == 0 ? "Последний пончик." : "Карамазов съел пончик.",2);
        }

        void MakeContents()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var dough = Surface(shader,new Color(.67f,.33f,.095f),.24f);
            var cardboard = Surface(shader,new Color(.69f,.50f,.32f),.15f);
            var chocolate = Surface(shader,new Color(.23f,.10f,.065f),.44f);
            var pink = Surface(shader,new Color(.85f,.33f,.40f),.47f);
            var sugar = Surface(shader,new Color(.94f,.78f,.58f),.48f);
            if (!boxPrefab)
            {
                Part("Дно",new Vector3(0,.012f,0),new Vector3(.48f,.024f,.32f),cardboard);
                foreach (float sign in new[] {-1f,1f})
                {
                    Part("Борт",new Vector3(sign*.23f,.046f,0),new Vector3(.019f,.068f,.32f),cardboard);
                    Part("Борт",new Vector3(0,.046f,sign*.151f),new Vector3(.48f,.068f,.019f),cardboard);
                }
                var lid = Part("Крышка",new Vector3(0,.12f,.211f),new Vector3(.48f,.22f,.016f),cardboard);
                lid.localRotation = Quaternion.Euler(24,0,0);
            }

            var imported = Resources.Load<PoliceDonutAssets>("PoliceDonutAssets");
            bool hasImported = imported && imported.first && imported.second;
            if (!hasImported)
            {
                ring = MakeRing(0,Mathf.PI*2,24);
                icing = MakeRing(0,Mathf.PI,12);
            }
            donuts = new Transform[6];
            var frostings = new[] { chocolate,pink,sugar };
            for (int i = 0; i < donuts.Length; i++)
            {
                var pastry = new GameObject("Пончик").transform;
                pastry.SetParent(transform,false);
                pastry.localPosition = new Vector3(-.145f + i%3*.145f,.049f,-.074f + i/3*.145f);
                if (hasImported) PlaceImported(pastry,i%2 == 0 ? imported.first : imported.second,shader);
                else
                {
                    MeshPart(pastry,ring,dough,Vector3.zero);
                    MeshPart(pastry,icing,frostings[i%frostings.Length],Vector3.up*.003f);
                }
                donuts[i] = pastry;
            }
        }

        readonly Dictionary<Material,Material> converted = new();

        void PlaceImported(Transform pastry, GameObject prefab, Shader shader)
        {
            var model = Instantiate(prefab,pastry,false).transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (!source || source.shader == shader) continue;
                    if (!converted.TryGetValue(source,out var material))
                    {
                        material = Surface(shader,source.color,.36f);
                        material.mainTexture = source.mainTexture;
                        if (source.HasProperty("_BumpMap"))
                        {
                            var normal = source.GetTexture("_BumpMap");
                            if (normal)
                            {
                                material.SetTexture("_BumpMap",normal);
                                material.EnableKeyword("_NORMALMAP");
                            }
                        }
                        converted[source] = material;
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float diameter = Mathf.Max(bounds.size.x,bounds.size.z);
            if (diameter > .001f) model.localScale *= .116f / diameter;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.position += pastry.position - new Vector3(bounds.center.x,bounds.min.y+.015f,bounds.center.z);
        }

        Material Surface(Shader shader, Color color,float gloss)
        {
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness",gloss);
            materials.Add(material);
            return material;
        }

        Transform Part(string label,Vector3 position,Vector3 scale,Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(transform,false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static void MeshPart(Transform parent,Mesh mesh,Material material,Vector3 offset)
        {
            var part = new GameObject(material.color.ToString());
            part.transform.SetParent(parent,false);
            part.transform.localPosition = offset;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Mesh MakeRing(float start,float end,int segments)
        {
            const int around = 36;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = 2*Mathf.PI*i/around;
                for (int j = 0; j <= segments; j++)
                {
                    float b = Mathf.Lerp(start,end,j/(float)segments);
                    float radial = .040f + .021f*Mathf.Cos(b);
                    vertices.Add(new Vector3(radial*Mathf.Cos(a),.017f*Mathf.Sin(b),radial*Mathf.Sin(a)));
                    if (i == around || j == segments) continue;
                    int p = i*(segments+1)+j;
                    int q = p+segments+1;
                    triangles.AddRange(new[] {p,p+1,q,p+1,q+1,q});
                }
            }
            var answer = new Mesh { name = "Donut ring" };
            answer.SetVertices(vertices);
            answer.SetTriangles(triangles,0);
            answer.RecalculateNormals();
            return answer;
        }

        void OnDestroy()
        {
            if (ring) Destroy(ring);
            if (icing) Destroy(icing);
            foreach (var material in materials) Destroy(material);
        }
    }
}
