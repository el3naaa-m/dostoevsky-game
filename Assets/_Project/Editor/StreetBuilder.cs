using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace NightChase
{
    public sealed class StreetBuilder : EditorWindow
    {
        const string RootName = "Night Chase — Generated Street";
        const string Base = "Assets/_Project";
        const string ScenePath = Base + "/Scenes/Chase_Street.unity";
        const string Art = Base + "/StreetArt";

        Transform buildings, street, props, lighting, atmosphere, markers;
        Material brick, soot, stone, road, iron, glass, lit, cyan, amber, red, paper, paint;
        Mesh cube;

        [MenuItem("Tools/Night Chase/Street Builder")]
        static void Open() => GetWindow<StreetBuilder>("Night Chase");

        void OnGUI()
        {
            if (GUILayout.Button("Generate Street")) Edit(false, false);
            if (GUILayout.Button("Clear Generated Environment")) Edit(true, false);
            if (GUILayout.Button("Rebuild Street")) Edit(true, true);
        }

        void Edit(bool clear, bool rebuild)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Night Chase", "Open Chase_Street before using this tool.", "OK");
                return;
            }

            var root = Array.Find(scene.GetRootGameObjects(), item => item.name == RootName
                && item.GetComponent<StreetOwnership>() != null);
            if (root != null && clear) Undo.DestroyObjectImmediate(root);
            if (rebuild || (!clear && root == null)) Build();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Night Chase/Create Chase Street Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Chase_Street already exists.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Base + "/Scenes");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var builder = CreateInstance<StreetBuilder>();
            builder.Build();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save " + ScenePath);
            DestroyImmediate(builder);
        }

        void Build()
        {
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var state = Random.state;
            Random.InitState(831);
            try
            {
                var root = Node(null, RootName);
                root.gameObject.AddComponent<StreetOwnership>();
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Generate Night Chase street");
                var environment = Node(root, "Environment");
                buildings = Node(environment, "Buildings");
                street = Node(environment, "Street");
                props = Node(environment, "Props");
                lighting = Node(root, "Lighting");
                atmosphere = Node(root, "Atmosphere");
                markers = Node(root, "GameplayMarkers");
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = primitive.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(primitive);
                Materials();
                Random.InitState(831);
                Layout();
                SetLighting();
                SetMarkers();
                AssetDatabase.SaveAssets();
            }
            finally { Random.state = state; }
        }

        static Transform Node(Transform parent, string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }

        Transform Box(Transform parent, string name, Material material, Vector3 center,
            Vector3 size, bool solid = false)
        {
            var part = Node(parent, name);
            part.localPosition = center;
            part.localScale = size;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = cube;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) part.gameObject.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
            return part;
        }

        void Block(Transform parent, string name, Material material, float x, float y, float z,
            float w, float h, float d, bool solid = false)
            => Box(parent, name, material, new Vector3(x, y, z), new Vector3(w, h, d), solid);

        Material Surface(string name, Color color, float smoothness, float metal = 0,
            Texture2D texture = null, Color emission = default)
        {
            var path = Art + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metal);
            if (texture) material.SetTexture("_BaseMap", texture);
            if (emission.maxColorComponent > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        Texture2D Tile(string name, bool masonry, bool concrete = false)
        {
            var path = Art + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var grain = Random.Range(-.085f, .085f);
                var wear = Mathf.PerlinNoise(x * .056f, y * .056f);
                int row = y / 16;
                int offset = (x + row % 2 * 16) % size;
                float value = .69f + grain + wear * .23f;
                if (masonry)
                {
                    var mortar = y % 16 < 2 || offset % 32 < 2;
                    value = mortar ? .37f + grain * .5f :
                        value * (.77f + Mathf.PerlinNoise(offset / 32 * .83f, row * 1.27f) * .3f);
                    if (!mortar && (y % 16 == 2 || offset % 32 == 2)) value *= .82f;
                }
                else if (concrete && (x < 2 || y < 2)) value *= .53f;
                pixels[y * size + x] = new Color(value, value, value, 1);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        void Materials()
        {
            var masonry = Tile("Brick grain", true);
            brick = Surface("Red masonry", new Color(.40f,.245f,.20f), .22f, texture: masonry);
            soot = Surface("Brown masonry", new Color(.30f,.25f,.23f), .22f, texture: masonry);
            stone = Surface("Concrete", new Color(.43f,.43f,.42f), .24f, texture: Tile("Concrete slabs", false, true));
            road = Surface("Asphalt", new Color(.19f,.20f,.22f), .48f, texture: Tile("Asphalt grain", false));
            iron = Surface("Painted steel", new Color(.16f,.18f,.19f), .42f, .2f);
            glass = Surface("Dark glass", new Color(.11f,.16f,.20f), .78f, .22f);
            lit = Surface("Occupied window", new Color(.60f,.44f,.28f), .33f, emission: new Color(.45f,.25f,.10f));
            cyan = Surface("Cold neon", new Color(.16f,.65f,.79f), .45f, emission: new Color(.15f,1.18f,1.65f));
            amber = Surface("Lamp diffuser", new Color(.88f,.68f,.37f), .45f, emission: new Color(1.9f,1.12f,.46f));
            red = Surface("Oxide paint", new Color(.46f,.12f,.09f), .41f, .12f);
            paper = Surface("Cardboard", new Color(.43f,.31f,.21f), .17f);
            paint = Surface("Worn road paint", new Color(.68f,.65f,.54f), .21f);
        }

        void TiledBlock(Transform parent, string name, Material material, Vector3 center, Vector3 size,
            float horizontalPeriod, float verticalPeriod)
        {
            var part = Box(parent, name, material, center, size, true);
            string key = FormattableString.Invariant($"Tile_{size.x}_{size.y}_{size.z}_{horizontalPeriod}_{verticalPeriod}");
            var path = Art + "/" + key + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = Instantiate(cube);
                mesh.name = key;
                var uv = mesh.uv;
                var normals = mesh.normals;
                for (int i = 0; i < uv.Length; i++)
                {
                    var n = normals[i];
                    var u = Mathf.Abs(n.x) > .5f ? size.z : size.x;
                    var v = Mathf.Abs(n.y) > .5f ? size.z : size.y;
                    uv[i] = Vector2.Scale(uv[i], new Vector2(u / horizontalPeriod,
                        v / (Mathf.Abs(n.y) > .5f ? horizontalPeriod : verticalPeriod)));
                }
                mesh.uv = uv;
                mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh, path);
            }
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Building(string name, float x, float z, float width, float depth, int floors,
            float yaw, bool escape, string sign = null)
        {
            var building = Node(buildings, name);
            building.localPosition = new Vector3(x, 0, z);
            building.localRotation = Quaternion.Euler(0, yaw, 0);
            float height = 3.5f + (floors - 1) * 3f;
            var wall = floors % 2 == 0 ? brick : soot;
            TiledBlock(building, "Brick shell", wall, new Vector3(0,height*.5f,0),
                new Vector3(width,height,depth), 2.1f, 1.18f);
            Block(building,"Foundation",stone,0,.29f,0,width+.14f,.58f,depth+.14f);
            Block(building,"Cornice",iron,0,height-.12f,0,width+.52f,.32f,depth+.52f);
            Block(building,"Parapet",wall,0,height+.28f,0,width+.17f,.55f,depth+.17f);
            Block(building,"Rooftop equipment",iron,width*.2f,height+.85f,-depth*.15f,2.15f,1.2f,1.6f);

            for (int side = 0; side < 4; side++)
            {
                var facade = Node(building, "Facade " + (side + 1));
                facade.localRotation = Quaternion.Euler(0,side*90,0);
                float faceWidth = side % 2 == 0 ? width : depth;
                float faceDepth = side % 2 == 0 ? depth : width;
                facade.localPosition = facade.localRotation * new Vector3(0,0,faceDepth*.5f);
                int bays = Mathf.Max(2, Mathf.FloorToInt(faceWidth / 2.45f));
                for (int floor = 0; floor < floors; floor++)
                for (int bay = 0; bay < bays; bay++)
                {
                    float bx = ((bay+.5f)/bays-.5f)*faceWidth;
                    float y = 1.85f + floor*3f;
                    if (floor == 0 && (bay == bays/2 || (sign != null && side == 0)))
                        continue;
                    Window(facade,bx,y, floor > 0 && Random.value < .17f);
                    if (floor > 0 && Random.value < .16f)
                    {
                        Block(facade,"Window AC",iron,bx+.22f,y-.55f,.27f,.61f,.36f,.55f);
                        for (int louver = 0; louver < 4; louver++)
                            Block(facade,"Grille",stone,bx+.22f,y-.66f+louver*.065f,.551f,.48f,.018f,.017f);
                    }
                }

                Block(facade,"Door surround",stone,0,1.32f,.09f,1.52f,2.56f,.26f);
                Block(facade,"Steel service door",iron,0,1.27f,.24f,1.22f,2.23f,.12f);
                Block(facade,"Door handle",stone,.43f,1.08f,.32f,.1f,.05f,.09f);
                if (side == 0 && escape) FireEscape(facade, floors, faceWidth * .24f);
                if (side == 0 && sign != null) Shop(facade, faceWidth, sign);
            }
        }

        void Window(Transform facade, float x, float y, bool illuminated)
        {
            Block(facade,"Window casing",stone,x,y,.045f,1.31f,1.72f,.14f);
            Block(facade,"Window frame",iron,x,y,.13f,1.14f,1.53f,.12f);
            Block(facade,"Window pane",illuminated ? lit : glass,x,y,.197f,.99f,1.37f,.025f);
            Block(facade,"Sash rail",iron,x,y,.222f,1.08f,.065f,.045f);
            Block(facade,"Sill",stone,x,y-.86f,.17f,1.46f,.12f,.38f);
        }

        void FireEscape(Transform facade, int floors, float x)
        {
            var escape = Node(facade, "Fire escape");
            escape.localPosition = new Vector3(x,0,0);
            for (int i = 1; i < floors; i++)
            {
                float y = .42f + i * 3;
                for (int j = 0; j < 9; j++)
                    Block(escape,"Grated landing",iron,(j-4)*.30f,y,.76f,.091f,.095f,1.23f);
                foreach (float rail in new[] { .21f, .53f, .91f })
                    Block(escape,"Guardrail",iron,0,y+rail,1.38f,2.72f,.062f,.062f);
                for (int j = 0; j < 7; j++)
                    Block(escape,"Picket",iron,(j-3)*.44f,y+.47f,1.38f,.05f,.94f,.05f);
                if (i == floors-1) continue;
                float direction = i % 2 == 0 ? -1 : 1;
                for (int step = 0; step < 12; step++)
                    Block(escape,"Stair tread",iron,direction*(-1.25f+step*.225f),y+.15f+step*.25f,1.06f,.28f,.065f,.56f);
                foreach (float z in new[] { .76f, 1.36f })
                {
                    var stringer = Box(escape,"Stair stringer",iron,new Vector3(0,y+1.50f,z),new Vector3(4.02f,.10f,.07f));
                    stringer.localRotation = Quaternion.Euler(0,0,48*direction);
                }
            }
            for (int j = 0; j < 6; j++)
                Block(escape,"Drop ladder rung",iron,-1.0f,2.03f+j*.235f,1.27f,.48f,.045f,.07f);
            foreach (float xRail in new[] { -1.24f, -.76f })
                Block(escape,"Drop ladder rail",iron,xRail,2.68f,1.27f,.05f,1.48f,.08f);
        }

        void Shop(Transform facade, float width, string name)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Block(facade,"Display glazing",glass,side*2.05f,1.56f,.23f,2.5f,2.31f,.16f);
                Block(facade,"Shop window mullion",iron,side*2.05f,1.56f,.34f,.09f,2.35f,.10f);
            }
            float span = Mathf.Min(width-.5f, 7.6f);
            Block(facade,"Sign cabinet",iron,0,3.01f,.31f,span,.72f,.37f);
            Block(facade,"Neon trim",cyan,0,2.70f,.52f,span-.13f,.027f,.03f);
            Lettering(facade,name,new Vector3(0,3.02f,.513f),.095f,new Color(.49f,.85f,.91f),180);
            Glow(facade,"Storefront spill",new Vector3(0,2.77f,.87f),new Color(.21f,.68f,1),2.3f,6);
        }

        static void Lettering(Transform parent, string words, Vector3 position, float scale, Color color, float yaw)
        {
            var label = Node(parent, words.Replace('\n',' '));
            label.localPosition = position;
            label.localRotation = Quaternion.Euler(0,yaw,0);
            var text = label.gameObject.AddComponent<TextMesh>();
            text.text = words;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 80;
            text.characterSize = scale;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
        }
        void Layout()
        {
            for (int x = -30; x <= 30; x += 20)
            for (int z = -30; z <= 30; z += 20)
                TiledBlock(street,"Asphalt",road,new Vector3(x,-.23f,z),new Vector3(20,.46f,20),2.5f,2.5f);
            Pavement("South sidewalk",0,-27,76,3.5f);
            Pavement("Inner sidewalk",0,-14.8f,29,3.6f);
            Pavement("North service court",0,22,31,16);
            Pavement("West walkway",-24,0,3,48);
            Pavement("Alley gutter",17.1f,1,1.6f,24);
            Pavement("East walkway",24.8f,0,2.7f,48);

            Building("Hudson building",-7.6f,-6,14,13,5,180,true,"HUDSON   DELI");
            Building("Mercer building",8,-6,16,13,4,180,true);
            Building("East tenement",9,7.8f,11,14,5,90,true);
            Building("Courtyard loft",-5.7f,7.8f,17,14,4,0,true);
            Building("Canal apartments",-23,-33.8f,24,10,5,0,true);
            Building("Laundromat",2,-33.8f,23,10,4,0,false,"24 HOUR   LAUNDRY");
            Building("Corner warehouse",24,-33.8f,18,10,3,0,true);
            Building("East service block",31,1,42,10,6,-90,true);
            Building("West service block",-31,1,46,10,4,90,true);
            Building("North lofts",-10.7f,34.1f,24,10,5,180,true);
            Building("North utility block",14,34.1f,24,10,4,180,false);

            for (int i = -6; i <= 6; i++)
                if (Mathf.Abs(i) > 3 || i % 2 == 0)
                    Block(street,"Faded center line",paint,i*4.2f,.009f,-21,2.08f,.015f,.073f);
            foreach (float x in new[] { -20f, 20f })
                for (int i = 0; i < 6; i++)
                    Block(street,"Crosswalk stripe",paint,x,.013f,-25.15f+i*1.32f,2.45f,.016f,.36f);

            foreach (float x in new[] { -21f, 0f, 23f }) Lamp(x,-26.2f,180);
            Lamp(13.1f,-14.85f,0);
            Lamp(24,5,90);
            Lamp(-14.9f,17,270);
            Lamp(11,26,180);
            Lamp(-23.8f,2,90);
            Lamp(-22,22,90);

            Hydrant(-10,-25.5f);
            Hydrant(12.7f,-15.7f);
            Hydrant(-24,11.5f);
            Dumpster(16.4f,3.8f);
            Dumpster(12.8f,26);
            Dumpster(-23.4f,9.2f);

            for (int i = 0; i < 11; i++)
            {
                float x = i < 4 ? 16.5f : i < 8 ? 12.7f : -23.2f;
                float z = i < 4 ? 6+i*.60f : i < 8 ? 23+(i-4)*.65f : 12.6f+(i-8)*.75f;
                var box = Box(props,"Discarded package",paper,new Vector3(x,.23f,z),
                    new Vector3(Random.Range(.37f,.65f),.46f,Random.Range(.39f,.60f)),true);
                box.localRotation = Quaternion.Euler(0,Random.Range(-32f,32f),0);
            }
            Fence(-9,27.7f,9);
            Fence(10,27.7f,6);
            Sign(-15,-16.2f,"ONE WAY  >");
            Sign(23.8f,-12,"NO PARKING");
            Sign(-23.1f,17.7f,"SERVICE");
            for (int i = 0; i < 7; i++)
                Block(street,"Storm drain slot",iron,16.75f,.008f,10+i*.065f,.43f,.012f,.031f);
        }

        void Pavement(string name, float x, float z, float w, float d)
        {
            TiledBlock(street,name,stone,new Vector3(x,.071f,z),new Vector3(w,.142f,d),1.65f,1.65f);
        }

        Transform Cylinder(Transform parent, string name, Material material, Vector3 position,
            Vector3 size, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (!solid) DestroyImmediate(go.GetComponent<Collider>());
            var part = go.transform;
            part.SetParent(parent,false);
            part.localPosition = position;
            part.localScale = size;
            go.name = name;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return part;
        }

        void Lamp(float x, float z, float yaw)
        {
            var lamp = Node(props,"Street lamp");
            lamp.localPosition = new Vector3(x,.14f,z);
            lamp.localRotation = Quaternion.Euler(0,yaw,0);
            Cylinder(lamp,"Pole",iron,new Vector3(0,2.84f,0),new Vector3(.15f,2.84f,.15f),true);
            Cylinder(lamp,"Pole base",iron,new Vector3(0,.27f,0),new Vector3(.30f,.27f,.30f));
            var arm = Box(lamp,"Bracket",iron,new Vector3(0,5.36f,-.54f),new Vector3(.11f,.10f,1.25f));
            arm.localRotation = Quaternion.Euler(-15,0,0);
            Block(lamp,"Lamp housing",iron,0,5.25f,-1.16f,.37f,.17f,.66f);
            Block(lamp,"Lamp diffuser",amber,0,5.15f,-1.16f,.29f,.045f,.51f);
            var bulb = Glow(lighting,"Sodium street light",lamp.TransformPoint(new Vector3(0,5.01f,-1.16f)),
                new Color(1,.70f,.40f),15,16);
            bulb.type = LightType.Spot;
            bulb.spotAngle = 112;
            bulb.innerSpotAngle = 58;
            bulb.transform.rotation = Quaternion.Euler(84,yaw+180,0);
        }

        void Hydrant(float x,float z)
        {
            var hydrant = Node(props,"Fire hydrant");
            hydrant.localPosition = new Vector3(x,.14f,z);
            Cylinder(hydrant,"Body",red,new Vector3(0,.38f,0),new Vector3(.28f,.38f,.28f),true);
            Cylinder(hydrant,"Bonnet",red,new Vector3(0,.78f,0),new Vector3(.35f,.09f,.35f));
            Cylinder(hydrant,"Footing",iron,new Vector3(0,.07f,0),new Vector3(.38f,.07f,.38f));
            var nozzle = Cylinder(hydrant,"Nozzles",red,new Vector3(0,.48f,0),new Vector3(.19f,.23f,.19f));
            nozzle.localRotation = Quaternion.Euler(0,0,90);
        }

        void Dumpster(float x, float z)
        {
            var bin = Node(props,"Waste container");
            bin.localPosition = new Vector3(x,.12f,z);
            Block(bin,"Welded bin",iron,0,.66f,0,1.18f,1.13f,1.66f,true);
            Block(bin,"Reinforced lid",iron,0,1.265f,0,1.24f,.11f,1.72f);
            for (int i = -2; i <= 2; i++)
                Block(bin,"Lid rib",stone,i*.21f,1.326f,0,.055f,.027f,1.49f);
            Block(bin,"Collection label",paint,.598f,.88f,0,.018f,.19f,.30f);
        }

        void Fence(float x, float z, int segments)
        {
            var fence = Node(props,"Steel railing");
            fence.localPosition = new Vector3(x,.14f,z);
            Block(fence,"Top rail",iron,0,1.02f,0,segments,.08f,.08f,true);
            Block(fence,"Lower rail",iron,0,.22f,0,segments,.07f,.08f);
            for (int i = 0; i <= segments*4; i++)
                Block(fence,"Vertical bar",iron,i*.25f-segments*.5f,.62f,0,.046f,.85f,.046f);
        }

        void Sign(float x, float z, string words)
        {
            var sign = Node(props,"Traffic sign");
            sign.localPosition = new Vector3(x,.14f,z);
            Cylinder(sign,"Post",iron,new Vector3(0,1.28f,0),new Vector3(.061f,1.28f,.061f),true);
            Block(sign,"Enamel panel",iron,0,2.35f,-.043f,.96f,.33f,.046f);
            Lettering(sign,words,new Vector3(0,2.35f,-.068f),.036f,new Color(.84f,.86f,.87f),0);
        }

        static Light Glow(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var point = Node(parent,name);
            point.localPosition = position;
            var light = point.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        void SetLighting()
        {
            var moon = Node(lighting,"Moon fill").gameObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(.47f,.62f,1f);
            moon.intensity = .36f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = .73f;
            moon.transform.rotation = Quaternion.Euler(39, -48, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.19f,.22f,.29f);
            RenderSettings.skybox = null;
            RenderSettings.sun = moon;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.065f,.083f,.12f);
            RenderSettings.fogDensity = .013f;

            if (!UnityEngine.Object.FindAnyObjectByType<FirstPersonController>())
            {
                var camera = Node(atmosphere,"First-person preview").gameObject.AddComponent<Camera>();
                camera.gameObject.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = RenderSettings.fogColor;
                camera.fieldOfView = 66;
                camera.nearClipPlane = .06f;
                camera.farClipPlane = 135;
                camera.gameObject.AddComponent<AudioListener>();
                var data = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
            }
            var volume = Node(atmosphere,"Night grading").gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            var profilePath = Art + "/Night grading.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile)
            {
                profile = CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile,profilePath);
                var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>();
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(.23f);
                var mapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();
                mapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
            }
            volume.sharedProfile = profile;
        }

        void SetMarkers()
        {
            var route = Node(markers,"ChaseRoute");
            Vector2[] points = {
                new(-18,-21), new(7,-21), new(18,-19), new(20,-10), new(20,8),
                new(19,17), new(11,22), new(-8,22), new(-18,18), new(-19,7),
                new(-19,-9), new(-19,-18), new(-18,-21)
            };
            for (int i = 0; i < points.Length; i++)
            {
                var waypoint = Node(route,$"{i+1:00} Waypoint");
                waypoint.localPosition = new Vector3(points[i].x,0,points[i].y);
                if (i+1 < points.Length)
                    waypoint.rotation = Quaternion.LookRotation(new Vector3(
                        points[i+1].x-points[i].x,0,points[i+1].y-points[i].y));
            }
            var start = Node(markers,"PlayerStart");
            start.SetPositionAndRotation(route.GetChild(0).position,route.GetChild(0).rotation);
            var end = Node(markers,"ChaseEnd");
            end.SetPositionAndRotation(route.GetChild(11).position,route.GetChild(11).rotation);
            var camera = atmosphere.Find("First-person preview");
            if (camera) camera.SetPositionAndRotation(start.position + Vector3.up*1.68f,
                Quaternion.Euler(-3,80,0));
        }

        [MenuItem("Tools/Night Chase/Verify Chase Route")]
        public static void VerifyScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Chase_Street before verifying the route.");
            var root = Array.Find(scene.GetRootGameObjects(), item => item.GetComponent<StreetOwnership>());
            var route = root.transform.Find("GameplayMarkers/ChaseRoute");
            Physics.SyncTransforms();
            float distance = 0;
            for (int i = 1; i < route.childCount; i++)
            {
                var start = route.GetChild(i-1).position;
                var end = route.GetChild(i).position;
                float length = Vector3.Distance(start,end);
                distance += length;
                int samples = Mathf.CeilToInt(length/.25f);
                for (int j = 0; j <= samples; j++)
                {
                    var point = Vector3.Lerp(start,end,(float)j/samples);
                    var ground = Physics.RaycastAll(point + Vector3.up * 2, Vector3.down, 2.2f);
                    if (!Array.Exists(ground, hit => hit.normal.y >= .8f && hit.collider.transform.IsChildOf(root.transform)))
                        throw new InvalidOperationException("Route missing ground at " + point);
                    var obstacles = Physics.OverlapCapsule(point + Vector3.up * .63f, point + Vector3.up * 1.55f, .34f);
                    if (Array.Exists(obstacles, hit => hit.transform.IsChildOf(root.transform)))
                        throw new InvalidOperationException("Route obstructed at " + point);
                }
            }
            Debug.Log($"Night Chase verified: {route.childCount} waypoints, {distance:F1} m, {root.GetComponentsInChildren<Collider>().Length} colliders.");
        }
    }
}




