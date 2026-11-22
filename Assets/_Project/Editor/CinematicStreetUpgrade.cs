using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NightChase
{
    [InitializeOnLoad]
    static class CinematicStreetUpgrade
    {
        const string ScenePath = "Assets/_Project/Scenes/Chase_Street.unity";
        const string Folder = "Assets/_Project/CinematicStreet";
        const string LayerName = "Cinematic Street Details";
        static Mesh cube, cylinder;
        static Material iron, zinc, trim, burgundy, cream, blue, amber, rubber, poster;

        
        [MenuItem("Tools/Night Chase/Apply Cinematic Street Upgrade")]
        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || GameObject.Find(LayerName)) return;
            var ownership = UnityEngine.Object.FindAnyObjectByType<StreetOwnership>();
            var root = ownership.transform;
            var buildings = root.Find("Environment/Buildings");
            var streets = root.Find("Environment/Street");
            var lighting = root.Find("Lighting");
            var profile = root.Find("Atmosphere/Night grading").GetComponent<Volume>();
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            var camera = player.GetComponentInChildren<Camera>();

            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            iron = MaterialAsset("Blackened iron", new Color(.21f,.23f,.24f), .28f, .43f);
            zinc = MaterialAsset("Weathered galvanized steel", new Color(.44f,.45f,.43f), .26f, .36f);
            trim = MaterialAsset("Cast sandstone", new Color(.54f,.45f,.36f), .12f);
            burgundy = MaterialAsset("Oxblood enamel",new Color(.34f,.095f,.079f),.23f);
            cream = MaterialAsset("Aged utility enamel",new Color(.67f,.60f,.43f),.18f);
            rubber = MaterialAsset("Black refuse plastic",new Color(.13f,.14f,.135f),.28f);
            poster = MaterialAsset("Weathered cream paper",new Color(.64f,.56f,.42f),.08f);
            blue = MaterialAsset("Ice blue sign",new Color(.31f,.74f,.88f),.20f,0,new Color(.12f,.77f,1.35f));
            amber = MaterialAsset("Warm entrance lamps",new Color(.95f,.66f,.29f),.22f,0,new Color(1.7f,.86f,.25f));

            var brick = Surface("Red fired brick","Bricks001",new Color(.85f,.78f,.73f),.12f);
            var umber = Surface("Weathered brown brick","Bricks075B",new Color(.71f,.57f,.49f),.11f);
            var paving = Surface("Dry sidewalk pavers","PavingStones034",new Color(.68f,.66f,.63f),.095f);
            var asphalt = Surface("Dry cracked asphalt","Asphalt026C",new Color(.81f,.81f,.79f),.075f);
            foreach (Transform building in buildings)
            {
                var renderer = building.Find("Brick shell").GetComponent<MeshRenderer>();
                var selected = building.name.Contains("warehouse") || building.name.Contains("lofts")
                    || building.name.Contains("service") || building.name.Contains("Mercer") ? umber : brick;
                if (selected) renderer.sharedMaterial = selected;
            }
            foreach (Transform item in streets)
            {
                var renderer = item.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.sharedMaterial) continue;
                if (renderer.sharedMaterial.name == "Asphalt")
                {
                    if (asphalt) renderer.sharedMaterial = asphalt;
                }
                else if (renderer.sharedMaterial.name == "Concrete" && paving)
                    renderer.sharedMaterial = paving;
            }

            var layer = Node(root,LayerName);
            var facades = Node(layer,"Facades");
            foreach (Transform building in buildings)
            {
                Transform facade = building.Find("Facade 1");
                var finish = Node(facades,building.name);
                finish.SetPositionAndRotation(facade.position,facade.rotation);
                var shell = building.Find("Brick shell").localScale;
                DressFacade(finish,shell.x,shell.y,building.GetSiblingIndex());
            }
            Storefront(facades.Find("Hudson building"),"H U D S O N   D E L I",burgundy,amber);
            Storefront(facades.Find("Laundromat"),"L A U N D R Y",iron,blue);
            Entrance(facades.Find("East tenement"),"117",blue);
            Entrance(facades.Find("Courtyard loft"),"MERCER  /  SERVICE",amber);
            Entrance(facades.Find("West service block"),"DELIVERIES",amber);
            BladeSign(facades.Find("Laundromat"));
            WaterTower(layer,new Vector3(29,18.5f,-3));
            WaterTower(layer,new Vector3(-12,15.5f,32));

            var streetDetails = Node(layer,"Street furniture and paving");
            foreach (var position in new[] { new Vector3(-10,.012f,-21.9f), new Vector3(10,.012f,-19.3f),
                                             new Vector3(19.3f,.012f,6.5f),new Vector3(-18.8f,.012f,13) })
                Manhole(streetDetails,position);
            Curb(streetDetails,-24.7f,-25.25f,24,0);
            Curb(streetDetails,12.2f,-25.25f,49,0);
            Curb(streetDetails,0,-16.59f,28.9f,0);
            Curb(streetDetails,23.47f,0,45,90);
            foreach (var position in new[] { new Vector3(17.3f,.012f,2.2f),new Vector3(23.0f,.012f,-9),
                                              new Vector3(3,.012f,-25), new Vector3(-22,.012f,-4) })
                Drain(streetDetails,position);
            Refuse(streetDetails,new Vector3(17.05f,.16f,4.75f),18);
            Refuse(streetDetails,new Vector3(13.5f,.16f,26.6f),-22);
            Refuse(streetDetails,new Vector3(-24.05f,.16f,10.65f),37);
            Poster(facades.Find("East tenement"),-1.7f,"NO PARKING\nACTIVE LOADING");
            Poster(facades.Find("Courtyard loft"),-2.8f,"MERCER ST\nNIGHT MARKET");
            Poster(facades.Find("West service block"),-2.3f,"KEEP CLEAR\nFIRE ACCESS");

            FinishFacadeLighting(buildings);
            Illuminate(lighting,layer);
            Grade(profile);
            RefineStreetFronts(root, layer);
            StreetBuilder.VerifyScene();
            VerifyPlayer(camera,player);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Chase_Street.");
            Debug.Log($"Night Chase cinematic upgrade saved. Imported surface sets: {new[] { brick, umber, paving, asphalt }.Count(material => material)} / 4.");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                Capture(camera);
        }

        [MenuItem("Tools/Night Chase/Polish Existing Storefronts")]
        static void PolishStorefronts()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || !GameObject.Find(LayerName)
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = UnityEngine.Object.FindAnyObjectByType<StreetOwnership>().transform;
            FinishFacadeLighting(root.Find("Environment/Buildings"));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Chase_Street.");
            Debug.Log("Night Chase: storefront lettering and window lighting polished.");
            var camera = Camera.main;
            if (camera && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Capture(camera);
        }

        static void FinishFacadeLighting(Transform buildings)
        {
            foreach (string building in new[] { "Hudson building", "Laundromat" })
            {
                var oldStorefront = buildings.Find(building + "/Facade 1");
                foreach (var text in oldStorefront.GetComponentsInChildren<TextMesh>())
                    text.GetComponent<MeshRenderer>().enabled = false;
                var tube = oldStorefront.Find("Neon trim");
                if (tube) tube.GetComponent<MeshRenderer>().enabled = false;
            }
            var tungsten = MaterialAsset("Warm residential window", new Color(.64f,.48f,.31f), .21f, 0,
                new Color(1.27f,.68f,.29f));
            var shade = MaterialAsset("Shaded residential window", new Color(.39f,.35f,.26f), .20f, 0,
                new Color(.61f,.47f,.28f));
            int index = 0;
            foreach (var renderer in buildings.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.sharedMaterial || renderer.sharedMaterial.name != "Occupied window") continue;
                renderer.sharedMaterial = index++ % 3 == 0 ? tungsten : shade;
            }
        }
        const string RefinedFronts = "Warm brick and storefronts";

        static CinematicStreetUpgrade()
        {
            EditorApplication.delayCall += RefineExistingStreet;
            EditorApplication.delayCall += ExtendStreetEdges;
            EditorApplication.delayCall += ClosePerimeterOpenings;
        }

        const string EdgeLayer = "Street beyond the chase route";

        [MenuItem("Tools/Night Chase/Extend Street Edges")]
        static void ExtendStreetEdges()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var owner = UnityEngine.Object.FindAnyObjectByType<StreetOwnership>();
            if (!owner) return;
            var layer = owner.transform.Find(LayerName);
            if (!layer || layer.Find(EdgeLayer)) return;

            cube = PrimitiveMesh(PrimitiveType.Cube);
            BuildStreetEdges(layer);
            StreetBuilder.VerifyScene();
            Physics.SyncTransforms();
            foreach (var direction in new[] {Vector3.right, Vector3.left, Vector3.forward, Vector3.back})
            {
                if (!Physics.Raycast(direction*36 + Vector3.up*1.1f, direction, out var hit,5f)
                    || !hit.collider.transform.IsChildOf(owner.transform))
                    throw new InvalidOperationException("The street boundary is open toward " + direction);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Chase_Street.");
            Debug.Log("Night Chase: distant street vistas and four perimeter limits saved.");

            var camera = Camera.main;
            if (camera && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                var position = camera.transform.position;
                var rotation = camera.transform.rotation;
                try
                {
                    camera.transform.SetPositionAndRotation(new Vector3(27,1.7f,-23.4f),
                        Quaternion.LookRotation(new Vector3(1,.018f,-.04f)));
                    Capture(camera,"Logs/NightChaseStreetEdge.png");
                }
                finally { camera.transform.SetPositionAndRotation(position,rotation); }
            }
        }

        [MenuItem("Tools/Night Chase/Close Perimeter Openings")]
        static void ClosePerimeterOpenings()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var owner = UnityEngine.Object.FindAnyObjectByType<StreetOwnership>();
            if (!owner) return;
            var edge = owner.transform.Find(LayerName + "/" + EdgeLayer);
            if (!edge || edge.Find("Continuous iron perimeter")) return;
            cube = PrimitiveMesh(PrimitiveType.Cube);
            var perimeter = Node(edge,"Continuous iron perimeter");
            var ironMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Perimeter weathered iron.mat");
            const float corner = 38.85f;
            foreach (var side in new[] { -1f, 1f })
            {
                string direction = side < 0 ? "West" : "East";
                // Match the end piers of each existing access gate.
                FenceSpan(perimeter,direction+" lower return",new Vector3(side*corner,0,-corner),
                    new Vector3(side*corner,0,-30.55f),ironMaterial);
                FenceSpan(perimeter,direction+" side railing",new Vector3(side*corner,0,-17.65f),
                    new Vector3(side*corner,0,corner),ironMaterial);
                FenceSpan(perimeter,side < 0 ? "South rear railing" : "North rear railing",
                    new Vector3(-corner,0,side*corner),new Vector3(corner,0,side*corner),ironMaterial);
            }

            Physics.SyncTransforms();
            StreetBuilder.VerifyScene();
            foreach (Transform span in perimeter)
            {
                var rail = span.GetComponent<BoxCollider>();
                var direction = span.forward;
                var point = span.position + Vector3.up * 1.1f;
                if (!rail.Raycast(new Ray(point - direction, direction),out _,2f))
                    throw new InvalidOperationException("Unblocked iron railing: " + span.name);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Chase_Street.");
            Debug.Log($"Night Chase: all six perimeter railing spans verified and saved ({perimeter.childCount} spans).");
        }

        static void FenceSpan(Transform parent,string name,Vector3 start,Vector3 end,Material material)
        {
            float length = Vector3.Distance(start,end);
            var span = Node(parent,name);
            span.localPosition = (start+end)*.5f;
            span.localRotation = Quaternion.LookRotation(Vector3.Cross((end-start).normalized,Vector3.up));
            var collision = span.gameObject.AddComponent<BoxCollider>();
            collision.center = Vector3.up*1.12f;
            collision.size = new Vector3(length+.20f,2.24f,.20f);

            // Separate short meshes so off-screen sections can be culled independently.
            int count = Mathf.CeilToInt(length/8f);
            float width = length/count;
            var path = Folder + "/Iron railing " + width.ToString("F4",System.Globalization.CultureInfo.InvariantCulture) + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                var parts = new System.Collections.Generic.List<CombineInstance>();
                foreach (float y in new[] {.25f,1.06f,2.12f})
                    FencePart(parts,new Vector3(0,y,0),new Vector3(width,.075f,.085f));
                int bars = Mathf.RoundToInt(width/.285f);
                for (int i = 0; i < bars; i++)
                    FencePart(parts,new Vector3(-width*.5f+(i+.5f)*width/bars,1.13f,0),
                        new Vector3(.037f,2.20f,.047f));
                FencePart(parts,new Vector3(-width*.5f,1.16f,0),new Vector3(.19f,2.32f,.20f));
                mesh = new Mesh { name = "Iron railing section" };
                mesh.CombineMeshes(parts.ToArray());
                AssetDatabase.CreateAsset(mesh,path);
            }
            for (int i = 0; i < count; i++)
                Shape(span,"Iron railing section",material,new Vector3(-length*.5f+(i+.5f)*width,0,0),Vector3.one,mesh);
            Box(span,"End pier",material,length*.5f,1.16f,0,.19f,2.32f,.20f);
        }

        static void FencePart(System.Collections.Generic.List<CombineInstance> parts,Vector3 position,Vector3 size)
        {
            parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(position,Quaternion.identity,size) });
        }
        static void BuildStreetEdges(Transform layer)
        {
            var edge = Node(layer,EdgeLayer);
            var barrier = MaterialAsset("Perimeter weathered iron",new Color(.22f,.218f,.20f),.22f,.32f);
            var darkGlass = MaterialAsset("Distant window glass",new Color(.16f,.207f,.24f),.36f);
            var blind = MaterialAsset("Distant blind glass",new Color(.24f,.223f,.187f),.31f);
            var stone = MaterialAsset("Perimeter cast stone",new Color(.35f,.325f,.29f),.15f);
            var asphalt = MaterialAsset("Distant matte asphalt",new Color(.17f,.177f,.191f),.055f);
            var darkBrick = MaterialAsset("Distant soot brick",new Color(.26f,.221f,.208f),.1f);
            var warmBrick = MaterialAsset("Distant brown brick",new Color(.32f,.239f,.207f),.1f);
            var lane = MaterialAsset("Distant faded lane paint",new Color(.49f,.464f,.389f),.1f);
            var bulb = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Opal wall lantern.mat");

            foreach (float side in new[] {-1f,1f})
            {
                var vista = Node(edge,side < 0 ? "West street continuation" : "East street continuation");
                vista.localPosition = new Vector3(side*40,0,-24.1f);
                vista.localRotation = Quaternion.Euler(0,side*90,0);
                Box(vista,"Road extension",asphalt,0,-.14f,18,13,.28f,36);
                foreach (float bank in new[] {-1f,1f})
                {
                    Box(vista,"Sidewalk continuation",stone,bank*6.2f,.066f,18,1.9f,.132f,36);
                    for (int i = 0; i < 3; i++)
                    {
                        var block = Node(vista,"Background brick row");
                        block.localPosition = new Vector3(bank*7.2f,0,6+i*11.7f);
                        block.localRotation = Quaternion.Euler(0,-bank*90,0);
                        BackdropFacade(block,11.4f,10.2f+((i+(side>0 ? 1 : 0))%3)*2.6f,
                            bank < 0 ? darkBrick : warmBrick,stone,darkGlass,blind);
                    }
                }

                for (int i = 0; i < 7; i++)
                    Box(vista,"Distant broken lane line",lane,0,.008f,2.6f+i*4.2f,.09f,.012f,2.1f);

                for (int i = 0; i < 2; i++)
                {
                    float z = 8+i*13;
                    Box(vista,"Distant lamp post",barrier,5.6f,2.2f,z,.102f,4.4f,.102f);
                    Box(vista,"Distant lamp bracket",barrier,5.28f,4.28f,z,.75f,.089f,.10f);
                    if (bulb) Box(vista,"Distant lamp diffuser",bulb,4.95f,4.21f,z,.24f,.066f,.26f);
                    Spot(vista,"Distant sodium pool",vista.TransformPoint(new Vector3(4.95f,4.14f,z)),
                        vista.TransformPoint(new Vector3(3.7f,.1f,z)),new Color(1,.73f,.48f),14,9,108);
                }

                // Recessed see-through maintenance gates sit on the playable side of the seam.
                var gate = Node(vista,"Closed municipal access");
                gate.localPosition = new Vector3(0,0,-1.15f);
                foreach (float x in new[] {-6.45f,0f,6.45f})
                    Box(gate,"Gate pier",barrier,x,1.16f,0,.19f,2.32f,.20f);
                foreach (float y in new[] {.25f,1.06f,2.12f})
                    Box(gate,"Gate cross rail",barrier,0,y,0,13.05f,.075f,.085f);
                for (int i = -22; i <= 22; i++)
                    Box(gate,"Iron gate picket",barrier,i*.285f,1.13f,0,.037f,2.20f,.047f);
                Box(gate,"Opaque service plaque",stone,0,1.48f,-.065f,.83f,.28f,.035f);
                Box(gate,"Gate lock",barrier,.12f,1.10f,-.085f,.13f,.20f,.14f);
                var gateCollider = gate.gameObject.AddComponent<BoxCollider>();
                gateCollider.center = new Vector3(0,1.12f,0);
                gateCollider.size = new Vector3(13.2f,2.24f,.20f);
                Spot(vista,"Gate marker light",vista.TransformPoint(new Vector3(-3.2f,4.7f,1f)),
                    vista.TransformPoint(new Vector3(-1f,.7f,-1f)),new Color(1,.73f,.46f),10,10,104);
            }

            // A second row of roofs covers side openings and the horizon behind the original block.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 6; i++)
                {
                    var rear = Node(edge,"Rear skyline block");
                    rear.localPosition = new Vector3(-47+i*18.4f,-.05f,side*(49+i%2*2));
                    rear.localRotation = Quaternion.Euler(0,side > 0 ? 180 : 0,0);
                    BackdropFacade(rear,17.9f,15+(i*7)%11,i%2 == 0 ? darkBrick : warmBrick,stone,darkGlass,blind);
                }
                foreach (float z in new[] { -46f, 6f, 21f, 36f })
                {
                    var rear = Node(edge,"Side skyline block");
                    rear.localPosition = new Vector3(side*48,-.05f,z);
                    rear.localRotation = Quaternion.Euler(0,-side*90,0);
                    BackdropFacade(rear,14.7f,13+Mathf.Abs(z)%9,darkBrick,stone,darkGlass,blind);
                }
                PerimeterLimit(edge,new Vector3(side*39.65f,3,0),new Vector3(.35f,6,80));
                PerimeterLimit(edge,new Vector3(0,3,side*39.65f),new Vector3(80,6,.35f));
            }
        }

        static void PerimeterLimit(Transform parent, Vector3 position, Vector3 size)
        {
            var limit = Node(parent,"Perimeter safety collider");
            limit.localPosition = position;
            limit.gameObject.AddComponent<BoxCollider>().size = size;
        }

        static void BackdropFacade(Transform parent,float width,float height,Material wall,Material stone,
            Material darkGlass,Material blind)
        {
            Box(parent,"Distant masonry",wall,0,height*.5f,-4.1f,width,height,8.2f);
            Box(parent,"Roof silhouette",stone,0,height,.015f,width+.15f,.29f,.31f);
            Box(parent,"Ground floor band",stone,0,3.18f,.06f,width,.14f,.23f);
            int bays = Mathf.FloorToInt(width/2.55f);
            int floors = Mathf.FloorToInt((height-1.1f)/2.7f);
            for (int floor = 0; floor < floors; floor++)
            for (int bay = 0; bay < bays; bay++)
            {
                float x = ((bay+.5f)/bays-.5f)*width;
                float y = 1.7f+floor*2.7f;
                Box(parent,"Distant window", (bay+floor*3)%7 == 2 ? blind : darkGlass,
                    x,y,.018f,1.01f,1.51f,.059f);
                Box(parent,"Sill silhouette",stone,x,y-.79f,.057f,1.14f,.084f,.19f);
            }
        }

        [MenuItem("Tools/Night Chase/Refine Storefronts")]
        static void RefineExistingStreet()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var ownership = UnityEngine.Object.FindAnyObjectByType<StreetOwnership>();
            if (!ownership) return;
            var layer = ownership.transform.Find(LayerName);
            if (!layer) return;
            if (layer.Find(RefinedFronts))
            {
                if (RestoreGlazingEmission() && Camera.main
                    && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Capture(Camera.main);
                return;
            }
            cube = PrimitiveMesh(PrimitiveType.Cube);
            RefineStreetFronts(ownership.transform, layer);
            StreetBuilder.VerifyScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Chase_Street.");
            Debug.Log("Night Chase: luminous signage replaced with quiet, warm storefronts.");
            if (Camera.main && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Capture(Camera.main);
        }

        static bool RestoreGlazingEmission()
        {
            bool changed = false;
            foreach (string name in new[] { "Amber ground-floor glazing", "Amber transom glazing",
                "Shaded shop glazing", "Opal wall lantern", "Warm residential window", "Shaded residential window" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
                if (!material || material.IsKeywordEnabled("_EMISSION")) continue;
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                material.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
            return changed;
        }

        static void RefineStreetFronts(Transform root, Transform layer)
        {
            var finishes = Node(layer, RefinedFronts);
            var wood = MaterialAsset("Painted storefront timber",new Color(.225f,.211f,.177f),.28f);
            var coping = MaterialAsset("Aged limestone dressings",new Color(.51f,.459f,.39f),.15f);
            var glazing = MaterialAsset("Amber ground-floor glazing",new Color(.55f,.41f,.27f),.25f,0,
                new Color(.90f,.47f,.175f));
            var shadeGlass = MaterialAsset("Amber transom glazing",new Color(.57f,.45f,.31f),.27f,0,
                new Color(1.28f,.78f,.39f));
            var shadowGlass = MaterialAsset("Shaded shop glazing",new Color(.38f,.32f,.23f),.25f,0,
                new Color(.32f,.21f,.11f));
            var wallLamp = MaterialAsset("Opal wall lantern",new Color(.94f,.72f,.43f),.23f,0,
                new Color(2.3f,1.45f,.70f));

            foreach (string name in new[] { "Hudson building", "Laundromat" })
            {
                Transform oldFront = layer.Find("Facades/" + name);
                foreach (Transform item in oldFront)
                {
                    switch (item.name)
                    {
                        case "Storefront fascia": case "Fascia cap": case "Illuminated underline":
                        case "Lettering": case "Storefront canopy": case "Canopy valance":
                        case "Storefront mullion": case "Laundry corner blade sign":
                            item.gameObject.SetActive(false);
                            break;
                    }
                }
                var original = root.Find("Environment/Buildings/" + name + "/Facade 1");
                foreach (Transform item in original)
                {
                    if (item.name == "Sign cabinet" || item.name == "Neon trim" ||
                        item.name == "Storefront spill" || item.GetComponent<TextMesh>())
                        item.gameObject.SetActive(false);
                }

                var front = Node(finishes,name);
                front.SetPositionAndRotation(oldFront.position,oldFront.rotation);
                bool deli = name == "Hudson building";
                float warm = deli ? 1 : -1;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side*2.07f;
                    var bayGlass = side == warm ? glazing : shadowGlass;
                    Box(front,"Recessed shop glazing",bayGlass,x,1.43f,.362f,2.35f,1.80f,.041f);
                    Box(front,"Warm transom behind sash",side == warm ? shadeGlass : shadowGlass,
                        x,2.49f,.363f,2.35f,.36f,.042f);
                    Box(front,"Painted kick panel",wood,x,.28f,.414f,2.51f,.46f,.139f);
                    Box(front,"Sill and drip edge",coping,x,.545f,.435f,2.56f,.074f,.25f);
                    Box(front,"Window head",wood,x,2.72f,.414f,2.52f,.128f,.16f);
                    Box(front,"Transom rail",wood,x,2.295f,.414f,2.49f,.087f,.123f);
                    for (int mullion = -1; mullion <= 1; mullion++)
                        Box(front,"Timber glazing mullion",wood,x+mullion*.81f,1.60f,.438f,.067f,2.09f,.093f);
                    // Low silhouettes keep the shopfront from reading as a single neon panel.
                    for (int display = 0; display < 4; display++)
                        Box(front,"Silhouette behind glass",wood,x-.85f+display*.49f,.67f+(display%2)*.046f,
                            .387f,.19f+.045f*display,.16f+(display%3)*.056f,.025f);
                }

                Box(front,"Recessed timber fascia",wood,0,2.985f,.32f,7.26f,.38f,.24f);
                Box(front,"Sandstone shop cornice",coping,0,3.18f,.348f,7.52f,.13f,.354f);
                Box(front,"Cornice shadow joint",wood,0,3.08f,.515f,7.42f,.041f,.034f);
                foreach (float x in new[] { -3.39f,-.79f,.79f,3.39f })
                {
                    Box(front,"Pilaster casing",wood,x,1.49f,.382f,.17f,2.73f,.236f);
                    Box(front,"Plinth block",coping,x,.31f,.46f,.24f,.28f,.31f);
                    Box(front,"Pilaster cap",coping,x,2.81f,.436f,.24f,.17f,.29f);
                }
                Box(front,"Narrow door glazing",shadowGlass,0,1.62f,.321f,.57f,.89f,.032f);
                Box(front,"Door lintel",coping,0,2.43f,.313f,1.35f,.105f,.24f);
                foreach (float x in new[] { -3.57f,3.57f })
                {
                    Box(front,"Lantern backplate",wood,x,2.48f,.15f,.21f,.36f,.12f);
                    Box(front,"Lantern glass",wallLamp,x,2.48f,.26f,.118f,.22f,.118f);
                    Box(front,"Lantern cap",wood,x,2.616f,.26f,.168f,.057f,.161f);
                }

                var windowCenter = front.TransformPoint(new Vector3(warm*2.1f,2.07f,1.08f));
                Spot(front,"Soft amber spill on pavement",windowCenter,front.TransformPoint(new Vector3(warm*1.5f,.1f,3.15f)),
                    new Color(1,.74f,.48f),19,9,105);
                Spot(front,"Facade and fire escape fill",front.TransformPoint(new Vector3(warm*2.75f,3.8f,4.5f)),
                    front.TransformPoint(new Vector3(warm*1.35f,6.1f,.1f)),new Color(1,.80f,.60f),14,13,87);
            }

            var priorAccents = layer.Find("Entrance and corner lights");
            foreach (string name in new[] { "Hudson canopy", "Laundry blade spill" })
            {
                var prior = priorAccents.Find(name);
                if (prior) prior.gameObject.SetActive(false);
            }
        }
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var source = GameObject.CreatePrimitive(type);
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(source);
            return mesh;
        }

        static Transform Node(Transform parent,string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent,false);
            return node;
        }

        static Transform Shape(Transform parent,string name,Material material,Vector3 position, Vector3 size,
            Mesh mesh = null)
        {
            var item = Node(parent,name);
            item.localPosition = position;
            item.localScale = size;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh ? mesh : cube;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(item.gameObject,StaticEditorFlags.BatchingStatic);
            return item;
        }

        static void Box(Transform parent,string name,Material material,float x,float y,float z,float w,float h,float d)
            => Shape(parent,name,material,new Vector3(x,y,z),new Vector3(w,h,d));

        static Material MaterialAsset(string name,Color color,float smoothness,float metal = 0,Color emission = default)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",color);
            material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Metallic",metal);
            if (emission.maxColorComponent > 0)
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",emission);
            }
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        static Texture2D Texture(string id,string map)
        {
            var files = Directory.GetFiles(Folder+"/Surfaces",id+"_*_"+map+".jpg");
            if (files.Length == 0) return null;
            var path = files[0].Replace('\\','/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer) return null;
            var type = map == "NormalGL" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = map == "Color";
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.anisoLevel != 8)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Surface(string name,string id,Color color,float smoothness)
        {
            var albedo = Texture(id,"Color");
            if (!albedo) return null;
            var material = MaterialAsset(name,color,smoothness);
            material.SetTexture("_BaseMap",albedo);
            var normal = Texture(id,"NormalGL");
            if (normal)
            {
                material.SetTexture("_BumpMap",normal);
                material.SetFloat("_BumpScale",.78f);
                material.EnableKeyword("_NORMALMAP");
            }
            var ao = Texture(id,"AmbientOcclusion");
            if (ao)
            {
                material.SetTexture("_OcclusionMap",ao);
                material.SetFloat("_OcclusionStrength",.7f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void DressFacade(Transform facade,float width,float height,int index)
        {
            var band = index % 3 == 0 ? zinc : trim;
            Box(facade,"Ground floor stringcourse",band,0,3.47f,.085f,width-.08f,.17f,.26f);
            Box(facade,"Corbel cornice",band,0,height-.37f,.12f,width,.22f,.39f);
            Box(facade,"Upper cornice drip edge",iron,0,height-.21f,.22f,width+.1f,.075f,.47f);
            for (int i = 0; i < (int)(width/1.1f); i++)
                Box(facade,"Cornice bracket",band,-width*.5f+.52f+i*1.1f,height-.60f,.16f,.16f,.31f,.30f);
            foreach (float sign in new[] {-1f,1f})
            {
                float x = sign * (width*.5f-.31f);
                Box(facade,"Masonry pilaster",band,x,1.7f,.105f,.42f,3.35f,.25f);
                Box(facade,"Pilaster footing",band,x,.34f,.19f,.53f,.29f,.36f);
            }
            float pipeX = -width*.5f+.71f;
            Shape(facade,"Soil stack",iron,new Vector3(pipeX,height*.5f,.20f),new Vector3(.09f,height*.48f,.09f),cylinder);
            for (float y = 1.3f; y < height; y += 2.5f)
                Shape(facade,"Downpipe collar",zinc,new Vector3(pipeX,y,.20f),new Vector3(.122f,.025f,.122f),cylinder);
            float serviceX = index % 2 == 0 ? 1.22f : -1.22f;
            Box(facade,"Electrical service box",zinc,serviceX,1.49f,.13f,.39f,.53f,.23f);
            Box(facade,"Inset meter face",iron,serviceX,1.49f,.253f,.30f,.41f,.024f);
            Box(facade,"Service box label",cream,serviceX,1.57f,.269f,.19f,.088f,.012f);
            Box(facade,"Conduit riser",iron,serviceX,2.41f,.10f,.042f,1.42f,.049f);
            Box(facade,"Clipped facade cable",iron,0,3.26f,.16f,width-.65f,.025f,.036f);
            if (index % 3 == 1)
                Box(facade,"Recessed iron door panel",burgundy,0,1.36f,.306f,.73f,1.86f,.025f);
        }

        static void Storefront(Transform facade,string title,Material board,Material light)
        {
            Box(facade,"Storefront fascia",board,0,3.02f,.60f,7.75f,.75f,.17f);
            Box(facade,"Fascia cap",zinc,0,3.42f,.66f,7.84f,.085f,.28f);
            Box(facade,"Illuminated underline",light,0,2.73f,.697f,7.62f,.036f,.038f);
            Text(facade,title,new Vector3(0,3.05f,.699f),.068f,new Color(.97f,.87f,.67f),180);
            Box(facade,"Storefront canopy",board,0,2.60f,.61f,7.65f,.10f,1.18f);
            Box(facade,"Canopy valance",board,0,2.51f,1.19f,7.65f,.16f,.07f);
            foreach (float x in new[] {-3.26f,-.78f,.78f,3.26f})
                Box(facade,"Storefront mullion",iron,x,1.47f,.377f,.08f,2.30f,.10f);
        }

        static void Entrance(Transform facade,string title,Material light)
        {
            Box(facade,"Pressed steel hood",iron,0,2.67f,.57f,1.81f,.10f,1.12f);
            Box(facade,"Bulkhead diffuser",light,0,2.585f,.59f,.45f,.045f,.27f);
            Box(facade,"Address plate",iron,0,2.83f,.126f,1.5f,.23f,.08f);
            Text(facade,title,new Vector3(0,2.83f,.175f),.032f,new Color(.92f,.84f,.65f),180);
        }

        static void BladeSign(Transform facade)
        {
            var sign = Node(facade,"Laundry corner blade sign");
            sign.localPosition = new Vector3(8.7f,4.33f,.88f);
            Box(sign,"Sign housing",iron,0,0,0,.20f,2.26f,1.32f);
            Box(sign,"Neon edge",blue,0,0,.68f,.10f,2.15f,.048f);
            foreach (float side in new[] {-1f,1f})
                Text(sign,"W\nA\nS\nH",new Vector3(.108f*side,0,0),.061f,new Color(.70f,.92f,1),-90*side);
        }

        static void Text(Transform parent,string value,Vector3 position,float scale,Color color,float yaw)
        {
            var go = Node(parent,"Lettering");
            go.localPosition = position;
            go.localRotation = Quaternion.Euler(0,yaw,0);
            var text = go.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = value;
            text.characterSize = scale;
            text.fontSize = 80;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
        }

        static void Poster(Transform facade,float x,string title)
        {
            Box(facade,"Pasted street notice",poster,x,1.48f,.022f,.48f,.64f,.015f);
            Box(facade,"Printed header",burgundy,x,1.73f,.032f,.44f,.095f,.006f);
            Text(facade,title,new Vector3(x,1.49f,.035f),.022f,new Color(.23f,.15f,.12f),180);
            Box(facade,"Printed footer",iron,x,1.23f,.033f,.39f,.031f,.006f);
        }

        static void WaterTower(Transform parent,Vector3 position)
        {
            var tank = Node(parent,"Rooftop water tank");
            tank.localPosition = position;
            Shape(tank,"Tank shell",burgundy,new Vector3(0,2.25f,0),new Vector3(2.36f,1.33f,2.36f),cylinder);
            Shape(tank,"Tank lid",iron,new Vector3(0,3.62f,0),new Vector3(2.53f,.085f,2.53f),cylinder);
            for (int i = 0; i < 5; i++)
                Shape(tank,"Steel hoop",iron,new Vector3(0,1.07f+i*.55f,0),new Vector3(2.38f,.031f,2.38f),cylinder);
            foreach (float x in new[] {-.70f,.70f})
            foreach (float z in new[] {-.70f,.70f})
                Box(tank,"Tank support",iron,x,.57f,z,.115f,1.36f,.115f);
        }

        static void Manhole(Transform parent,Vector3 position)
        {
            var access = Node(parent,"Recessed utility cover");
            access.localPosition = position;
            Shape(access,"Cast rim",iron,Vector3.zero,new Vector3(.79f,.014f,.79f),cylinder);
            Shape(access,"Stamped inset",zinc,Vector3.up*.014f,new Vector3(.66f,.008f,.66f),cylinder);
            for (int i = -4; i <= 4; i++)
            {
                float z = i*.064f;
                float w = 2*Mathf.Sqrt(.31f*.31f-z*z);
                Box(access,"Grip groove",iron,0,.023f,z,w,.005f,.019f);
            }
        }

        static void Curb(Transform parent,float x,float z,float length,float yaw)
        {
            var curb = Node(parent,"Jointed curb edge");
            curb.localPosition = new Vector3(x,0,z);
            curb.localRotation = Quaternion.Euler(0,yaw,0);
            int sections = Mathf.CeilToInt(length/1.8f);
            float span = length/sections;
            for (int i = 0; i < sections; i++)
                Box(curb,"Curb unit",trim,-length*.5f+(i+.5f)*span,.081f,0,span-.015f,.126f,.13f);
        }

        static void Drain(Transform parent,Vector3 position)
        {
            var drain = Node(parent,"Cast iron storm inlet");
            drain.localPosition = position;
            Box(drain,"Recess shadow",rubber,0,0,0,.52f,.019f,.62f);
            for (int i = -4; i <= 4; i++)
                Box(drain,"Drain grate bar",zinc,i*.052f,.015f,0,.024f,.015f,.59f);
            foreach (float z in new[] {-.29f,.29f})
                Box(drain,"Drain frame rail",iron,0,.018f,z,.52f,.016f,.028f);
        }

        static void Refuse(Transform parent,Vector3 position,float yaw)
        {
            var cluster = Node(parent,"Refuse by service access");
            cluster.localPosition = position;
            cluster.localRotation = Quaternion.Euler(0,yaw,0);
            Shape(cluster,"Galvanized bin",zinc,new Vector3(0,.40f,0),new Vector3(.51f,.40f,.51f),cylinder);
            for (int i = 0; i < 16; i++)
            {
                float angle = i*22.5f;
                var rotation = Quaternion.Euler(0,angle,0);
                var rib = Shape(cluster,"Rolled bin rib",zinc,rotation*new Vector3(0,.42f,.251f),
                    new Vector3(.032f,.65f,.026f));
                rib.localRotation = rotation;
            }
            Shape(cluster,"Press-formed bin lid",iron,new Vector3(0,.82f,0),new Vector3(.56f,.044f,.56f),cylinder);
            Box(cluster,"Lid handle",zinc,0,.884f,0,.14f,.039f,.059f);
            var sphere = PrimitiveMesh(PrimitiveType.Sphere);
            for (int i = 0; i < 2; i++)
            {
                var bag = Shape(cluster,"Tied refuse sack",rubber,new Vector3(.42f+i*.29f,.19f,-i*.29f),
                    new Vector3(.41f,.46f-i*.065f,.37f),sphere);
                bag.localRotation = Quaternion.Euler(0,37*i,13+i*13);
                Box(cluster,"Bag tie",rubber,.46f+i*.29f,.42f-i*.04f,-i*.29f,.089f,.095f,.068f);
            }
        }

        static void Illuminate(Transform lighting,Transform details)
        {
            RenderSettings.ambientLight = new Color(.205f,.236f,.30f);
            RenderSettings.fogColor = new Color(.052f,.069f,.106f);
            RenderSettings.fogDensity = .016f;
            foreach (var light in lighting.GetComponentsInChildren<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = .29f;
                    light.color = new Color(.52f,.67f,1);
                    continue;
                }
                if (light.name != "Sodium street light") continue;
                bool main = light.transform.position.z < -12;
                light.color = new Color(1,.68f,.37f);
                light.intensity = main ? 27 : 15;
                light.range = main ? 15 : 12;
                light.spotAngle = 111;
                light.innerSpotAngle = 46;
                if (main && Mathf.Abs(light.transform.position.x) < 15)
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = .72f;
                }
            }
            var accents = Node(details,"Entrance and corner lights");
            Spot(accents,"Hudson canopy",new Vector3(-7.6f,2.54f,-13.50f),new Vector3(-7.6f,.5f,-16),
                new Color(1,.69f,.40f),14,8,97);
            Spot(accents,"Laundry blade spill",new Vector3(9.2f,4.35f,-27.7f),new Vector3(8.8f,1.6f,-26.8f),
                new Color(.36f,.68f,1),11,7,100);
            Spot(accents,"Service court bulkhead",new Vector3(-5.7f,2.52f,15.44f),new Vector3(-5.7f,.3f,19),
                new Color(1,.75f,.50f),13,8,92);
            Spot(accents,"East alley entrance",new Vector3(15.50f,2.52f,7.8f),new Vector3(18.5f,.15f,7.8f),
                new Color(.54f,.74f,1),9,7,94);
        }

        static void Spot(Transform parent,string name,Vector3 position,Vector3 target,Color color,float intensity,
            float range,float angle)
        {
            var node = Node(parent,name);
            node.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var light = node.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.intensity = intensity;
            light.range = range;
            light.color = color;
            light.spotAngle = angle;
            light.innerSpotAngle = angle*.55f;
        }

        static void Grade(Volume volume)
        {
            var path = Folder+"/Street Grade.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile,path);
                profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
                var bloom = profile.Add<Bloom>();
                bloom.threshold.Override(1.12f);
                bloom.intensity.Override(.26f);
                bloom.scatter.Override(.57f);
                var grade = profile.Add<ColorAdjustments>();
                grade.postExposure.Override(.30f);
                grade.contrast.Override(11);
                grade.saturation.Override(-9);
                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(.16f);
                vignette.smoothness.Override(.43f);
                foreach (var effect in profile.components) AssetDatabase.AddObjectToAsset(effect,profile);
                EditorUtility.SetDirty(profile);
            }
            volume.sharedProfile = profile;
        }

        static void VerifyPlayer(Camera camera,FirstPersonController player)
        {
            if (Camera.main != camera || !player.GetComponent<CharacterController>() || !camera.isActiveAndEnabled)
                throw new InvalidOperationException("The active player camera or controller is missing.");
            var start = GameObject.Find("PlayerStart").transform;
            if (Vector3.Distance(start.position,player.transform.position) > .05f)
                throw new InvalidOperationException("Player is not at PlayerStart.");
            Debug.Log("Cinematic verification: PlayerStart, main camera, and CharacterController are present.");
        }

        static void Capture(Camera camera, string outputPath = "Logs/NightChaseCinematic.png")
        {
            var frame = new RenderTexture(1440,810,24,RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = frame });
                RenderTexture.active = frame;
                image = new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);
                File.WriteAllBytes(outputPath,image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image) UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(frame);
            }
        }
    }
}

