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

        static void Capture(Camera camera)
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
                File.WriteAllBytes("Logs/NightChaseCinematic.png",image.EncodeToPNG());
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

