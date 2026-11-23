using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightChase
{
    public sealed class BenchSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] benchPrefabs;
        [SerializeField] Transform[] spawnPoints;

        // Края тротуаров вдоль двух сторон квартала. Не перекрывают центр погони.
        static readonly Vector3[] Defaults =
        {
            new(-30,0,-27.9f), new(-23,0,-27.9f), new(-10,0,-27.9f), new(-2,0,-27.9f),
            new(6,0,-27.9f), new(15,0,-27.9f),
            new(27.1f,0,-13), new(27.1f,0,-7), new(27.1f,0,0),
            new(27.1f,0,7), new(27.1f,0,14), new(27.1f,0,20)
        };
        Material wood, metal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        static void Install(Scene scene,LoadSceneMode mode)
        {
            if (scene.path != ChaseOpening.ScenePath) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<BenchSpawner>(true)) return;
            var spawner = new GameObject("Street Benches");
            SceneManager.MoveGameObjectToScene(spawner,scene);
            spawner.AddComponent<BenchSpawner>();
        }

        void Start()
        {
            bool custom = spawnPoints != null && spawnPoints.Length > 0;
            for (int i = 0; i < (custom ? spawnPoints.Length : Defaults.Length); i++)
            {
                if (custom && !spawnPoints[i]) continue;
                var point = custom ? spawnPoints[i].position : Defaults[i];
                var rotation = custom ? spawnPoints[i].rotation : Quaternion.Euler(0,i < 6 ? 0 : 90,0);
                if (!custom)
                {
                    if (!Physics.Raycast(point + Vector3.up * 1.2f,Vector3.down,out var ground,1.8f,~0,QueryTriggerInteraction.Ignore)
                        || ground.normal.y < .8f) continue;
                    point.y = ground.point.y;
                    if (Physics.CheckBox(point+Vector3.up*.53f,new Vector3(.86f,.38f,.31f),rotation,~0,QueryTriggerInteraction.Ignore))
                        continue;
                }
                var prefab = benchPrefabs != null && benchPrefabs.Length > 0 ? benchPrefabs[i % benchPrefabs.Length] : null;
                var bench = prefab ? Instantiate(prefab,point,rotation,transform) : BuildBench(point,rotation);
                bool solid = false;
                foreach (var collider in bench.GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger) { solid = true; break; }
                if (!solid)
                {
                    var collider = bench.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0,.43f,0);
                    collider.size = new Vector3(1.8f,.86f,.62f);
                }
            }
        }

        GameObject BuildBench(Vector3 point,Quaternion rotation)
        {
            if (!wood)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                wood = new Material(shader) { color = new Color(.34f,.18f,.09f) };
                metal = new Material(shader) { color = new Color(.075f,.085f,.087f) };
            }
            var bench = new GameObject("Sidewalk bench");
            bench.transform.SetParent(transform,false);
            bench.transform.SetPositionAndRotation(point,rotation);
            for (int i = 0; i < 4; i++)
                Piece(bench,wood,new Vector3(0,.46f,-.24f + i*.13f),new Vector3(1.8f,.055f,.105f));
            for (int i = 0; i < 3; i++)
                Piece(bench,wood,new Vector3(0,.61f+i*.12f,-.27f),new Vector3(1.8f,.095f,.045f));
            foreach (float x in new[] { -.68f, .68f })
            {
                Piece(bench,metal,new Vector3(x,.45f,-.28f),new Vector3(.06f,.90f,.065f));
                Piece(bench,metal,new Vector3(x,.23f,.21f),new Vector3(.065f,.46f,.065f));
            }
            return bench;
        }

        static void Piece(GameObject bench,Material material,Vector3 position,Vector3 size)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.transform.SetParent(bench.transform,false);
            piece.transform.localPosition = position;
            piece.transform.localScale = size;
            var collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        void OnDestroy()
        {
            if (wood) Destroy(wood);
            if (metal) Destroy(metal);
        }
    }
}
