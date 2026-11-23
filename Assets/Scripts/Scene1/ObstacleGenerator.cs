using System.Collections.Generic;
using UnityEngine;

namespace NightChase
{
    public sealed class ObstacleGenerator : MonoBehaviour
    {
        // Бесплатный референс для индустриальных форм: https://kenney.nl/assets/city-kit-industrial (CC0).
        // Подойдут низкие деревянные ограждения, паллеты или низкий бетонный блок.
        // Высокие контейнеры лучше оставить декорациями: игрок должен перепрыгнуть и приземлиться.
        [SerializeField] GameObject[] obstaclePrefabs;
        [SerializeField] Transform[] spawnPoints;
        readonly List<Object> generated = new();
        Material timber, steel, reflector;
        const float JumpHeight = .45f;

        internal float[] Build(Transform owner,float center,float width,float[] defaults,float gateZ,float gateHeight)
        {
            PrepareMaterials();
            var locations = new float[2];
            for (int i = 0; i < locations.Length; i++)
            {
                float z = defaults[i];
                if (spawnPoints != null && i < spawnPoints.Length && spawnPoints[i])
                {
                    float candidate = spawnPoints[i].position.z;
                    if (candidate > gateZ + 3f && candidate < 18f &&
                        Mathf.Abs(candidate - defaults[1-i]) > 1.8f &&
                        (i == 0 || Mathf.Abs(candidate - locations[0]) > 1.8f))
                        z = candidate;
                    else Debug.LogWarning("Точка барьера вне участка прыжков. Использована исходная позиция.",spawnPoints[i]);
                }
                locations[i] = z;
                var root = Root(owner,$"Дорожный барьер {i+1}",new Vector3(center,0,z));
                var prefab = obstaclePrefabs != null && i < obstaclePrefabs.Length ? obstaclePrefabs[i] : null;
                if (!prefab || !FitPrefab(prefab,root,new Vector3(width,JumpHeight,.28f)))
                    BuildBarricade(root,width);
                // Узкая физическая перемычка остаётся в пределах старой траектории прыжка NPC.
                Solid(root,width,JumpHeight,.19f);
            }
            var gate = Root(owner,"Ржавая сетка · ПРОБЕЛ + E",new Vector3(center,0,gateZ));
            BuildGate(gate,width,gateHeight);
            Solid(gate,width,gateHeight,.16f);
            return locations;
        }

        void BuildBarricade(Transform parent,float width)
        {
            for (int i = 0; i < 2; i++)
            {
                float y = .204f + i*.15f;
                Block(parent,"Крашеная доска",new Vector3(0,y,0),new Vector3(width-.085f,.127f,.06f),timber);
                foreach (float side in new[] {-1f,1f})
                {
                    float x = side*(width*.5f-.23f);
                    Block(parent,"Потёртый отражатель",new Vector3(x,y,side*.032f),new Vector3(.22f,.034f,.005f),reflector);
                    Bolt(parent,new Vector3(side*(width*.5f-.11f),y,.037f));
                    Bolt(parent,new Vector3(side*(width*.5f-.11f),y,-.037f));
                }
            }
            foreach (float side in new[] {-1f,1f})
            {
                float x = side*(width*.5f-.07f);
                Block(parent,"Оцинкованная стойка",new Vector3(x,.22f,-.004f),new Vector3(.065f,.44f,.076f),steel);
                Block(parent,"Башмак",new Vector3(x,.014f,0),new Vector3(.135f,.028f,.37f),steel);
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    var brace = Block(parent,"Косынка",new Vector3(x,.064f,direction*.066f),new Vector3(.03f,.14f,.023f),steel);
                    brace.localRotation = Quaternion.Euler(direction*35,0,0);
                }
            }
        }

        void BuildGate(Transform parent,float width,float height)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side*(width*.5f-.046f);
                Block(parent,"Стойка сетки",new Vector3(x,height*.5f,0),new Vector3(.072f,height,.077f),steel);
                Block(parent,"Монтажная пятка",new Vector3(x,.018f,0),new Vector3(.091f,.036f,.21f),steel);
                Block(parent,"Крышка стойки",new Vector3(x,height-.019f,0),new Vector3(.09f,.038f,.094f),steel);
                foreach (float y in new[] { .26f,.72f,1.22f })
                    Block(parent,"Скоба",new Vector3(x,y,.045f),new Vector3(.109f,.043f,.02f),reflector);
            }
            Block(parent,"Верхняя труба",new Vector3(0,height-.057f,0),new Vector3(width-.1f,.052f,.065f),steel);
            Block(parent,"Нижняя труба",new Vector3(0,.085f,0),new Vector3(width-.1f,.043f,.059f),steel);
            Block(parent,"Полоса с отражателями",new Vector3(0,height-.12f,.025f),
                new Vector3(width-.23f,.085f,.03f),timber);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float innerWidth = width-.13f;
            float innerHeight = height-.21f;
            for (float start = -innerHeight; start < innerWidth; start += .14f)
            {
                float left = Mathf.Max(0,start);
                float right = Mathf.Min(innerWidth,start+innerHeight);
                if (left >= right) continue;
                float x0 = left-innerWidth*.5f, x1 = right-innerWidth*.5f;
                float y0 = left-start, y1 = right-start;
                AddWire(vertices,indices,new Vector3(x0,.105f+y0,0),new Vector3(x1,.105f+y1,0));
                AddWire(vertices,indices,new Vector3(x0,.105f+innerHeight-y0,.005f),new Vector3(x1,.105f+innerHeight-y1,.005f));
            }
            var surface = Root(parent,"Ромбы оцинкованной сетки",parent.position);
            var mesh = new Mesh { name = "Galvanized chain link" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();
            generated.Add(mesh);
            surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial = steel;
        }

        static void AddWire(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b)
        {
            var edge = Vector3.Cross(Vector3.forward,(b-a).normalized)*.004f;
            int i=vertices.Count;
            vertices.Add(a-edge);
            vertices.Add(b-edge);
            vertices.Add(b+edge);
            vertices.Add(a+edge);
            vertices.Add(a-edge);
            vertices.Add(b-edge);
            vertices.Add(b+edge);
            vertices.Add(a+edge);
            indices.AddRange(new[] {i,i+1,i+2,i,i+2,i+3,i+4,i+6,i+5,i+4,i+7,i+6});
        }

        void Bolt(Transform parent,Vector3 point)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = "Головка болта";
            bolt.transform.SetParent(parent,false);
            bolt.transform.localPosition = point;
            bolt.transform.localRotation = Quaternion.Euler(90,0,0);
            bolt.transform.localScale = new Vector3(.038f,.007f,.038f);
            FinishPart(bolt,steel);
        }

        static Transform Block(Transform parent,string name,Vector3 point,Vector3 size,Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent,false);
            piece.transform.localPosition = point;
            piece.transform.localScale = size;
            FinishPart(piece,material);
            return piece.transform;
        }

        static void FinishPart(GameObject piece,Material material)
        {
            var collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Transform Root(Transform parent,string name,Vector3 point)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent,false);
            root.position = point;
            return root;
        }

        static void Solid(Transform root,float width,float height,float depth)
        {
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up*height*.5f;
            collider.size = new Vector3(width,height,depth);
        }

        static bool FitPrefab(GameObject source,Transform root,Vector3 fit)
        {
            var model = Instantiate(source,root,false).transform;
            model.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            var renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (renderers.Length == 0) { Destroy(model.gameObject); return false; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var size = bounds.size;
            model.localScale = Vector3.Scale(model.localScale,new Vector3(
                fit.x/Mathf.Max(.01f,size.x),fit.y/Mathf.Max(.01f,size.y),fit.z/Mathf.Max(.01f,size.z)));
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.position += root.position - new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            return true;
        }

        void PrepareMaterials()
        {
            if (timber) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            timber = Material(shader,new Color(.85f,.82f,.76f),.12f,.22f);
            steel = Material(shader,new Color(.42f,.42f,.39f),.63f,.38f);
            reflector = Material(shader,new Color(.66f,.63f,.50f),.12f,.48f);
            timber.mainTexture = SurfaceTexture(512,128,true);
            steel.mainTexture = SurfaceTexture(128,128,false);
        }

        Material Material(Shader shader,Color color,float metallic,float smoothness)
        {
            var material = new Material(shader) { color = color };
            material.SetFloat("_Metallic",metallic);
            material.SetFloat("_Smoothness",smoothness);
            generated.Add(material);
            return material;
        }

        Texture2D SurfaceTexture(int width,int height,bool wood)
        {
            var texture = new Texture2D(width,height,TextureFormat.RGB24,true);
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[width*height];
            var random = new System.Random(1999);
            for (int y=0;y<height;y++)
                for (int x=0;x<width;x++)
                {
                    float u = x/(float)width, v = y/(float)height;
                    float grain = Mathf.PerlinNoise(u*11,v*58);
                    float chips = Mathf.PerlinNoise(u*43+18,v*19);
                    float stain = Mathf.PerlinNoise(u*14,v*3);
                    bool orange = Mathf.Repeat(u*6-v*.21f,1) < .5f;
                    Color color = wood ? (orange ? new Color(.78f,.31f,.10f) : new Color(.82f,.78f,.67f)) :
                        Color.Lerp(new Color(.72f,.61f,.50f),new Color(.94f,.94f,.91f),chips);
                    if (wood && (chips > .66f || ((y<4 || y>height-5) && chips > .49f)))
                        color = Color.Lerp(color,new Color(.43f,.29f,.16f),.78f);
                    float grainLevel = .62f + grain*.31f + stain*.19f + (float)random.NextDouble()*.10f;
                    pixels[y*width+x] = color*grainLevel;
                }
            texture.SetPixels32(pixels);
            texture.Apply(true,true);
            generated.Add(texture);
            return texture;
        }

        void OnDestroy()
        {
            foreach (var asset in generated) if (asset) Destroy(asset);
        }
    }
}
