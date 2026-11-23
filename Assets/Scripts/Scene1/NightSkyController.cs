using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightChase
{
    public sealed class NightSkyController : MonoBehaviour
    {
        [Range(30,3000)] public int starCount = 720;
        [Range(0,2)] public float brightness = .7f;
        [Range(0,1)] public float twinkle = .18f;
        [Range(0,5)] public float twinkleSpeed = .85f;
        public Color starColor = new(.82f,.88f,1);
        Material material;
        Mesh mesh;
        Camera view;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded -= Install; SceneManager.sceneLoaded += Install; }
        static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/_Project/Scenes/Chase_Street.unity") return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<NightSkyController>(true)) return;
            var sky = new GameObject("Звёздное небо");
            SceneManager.MoveGameObjectToScene(sky,scene);
            sky.AddComponent<NightSkyController>();
        }
        void Start()
        {
            var shader = Resources.Load<Shader>("StationNightStars");
            if (!shader) return;
            material = new Material(shader);
            var rng = new System.Random(1994);
            var vertices = new Vector3[starCount*3];
            var uv = new Vector2[vertices.Length];
            var indices = new int[vertices.Length];
            for (int i=0;i<starCount;i++)
            {
                float y = .09f + .91f*(float)rng.NextDouble();
                float angle = 2*Mathf.PI*(float)rng.NextDouble();
                float radius = Mathf.Sqrt(1-y*y);
                Vector3 dir = new(radius*Mathf.Cos(angle),y,radius*Mathf.Sin(angle));
                float size = .0006f + .0016f*(float)rng.NextDouble();
                Vector3 right = Vector3.Cross(dir,Vector3.up).normalized*size;
                Vector3 up = Vector3.Cross(right,dir);
                int j = i*3;
                vertices[j]=dir-right-up; vertices[j+1]=dir+right*3-up; vertices[j+2]=dir-right+up*3;
                uv[j]=Vector2.zero; uv[j+1]=new Vector2(2,0); uv[j+2]=new Vector2(0,2);
                for(int k=j;k<j+3;k++) indices[k]=k;
            }
            mesh = new Mesh {vertices=vertices, uv=uv, triangles=indices};
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void LateUpdate()
        {
            if (!material) return;
            if (!view || !view.isActiveAndEnabled) view=Camera.main;
            if (!view) return;
            transform.position=view.transform.position;
            transform.localScale=Vector3.one*(view.farClipPlane*.92f);
            material.SetColor("_Tint",starColor*brightness);
            material.SetFloat("_Twinkle",twinkle);
            material.SetFloat("_Speed",twinkleSpeed);
        }
        void OnDestroy() { if(mesh) Destroy(mesh); if(material) Destroy(material); }
    }
}
