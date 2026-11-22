using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NightChase
{
    static class ChaseStreetNavigation
    {
        public static NavMeshData Build(StreetOwnership street)
        {
            if (NavMesh.GetSettingsCount() == 0) return null;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(street.transform, ~0, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);

            var settings = NavMesh.GetSettingsByIndex(0);
            var bounds = new Bounds(Vector3.up, new Vector3(80, 6, 80));
            return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, street.transform.position, Quaternion.identity);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Night Chase/Bake Chase NavMesh")]
        static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            var street = Object.FindAnyObjectByType<StreetOwnership>();
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                || scene.path != ChaseOpening.ScenePath || !street || scene.isDirty)
            {
                Debug.LogWarning("Открой сохранённую сцену Chase_Street перед запеканием NavMesh.");
                return;
            }

            var data = Build(street);
            if (!data)
            {
                Debug.LogError("Не удалось запечь NavMesh улицы.");
                return;
            }

            const string resources = "Assets/Scripts/Resources";
            const string path = resources + "/ChaseStreetNavMesh.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder(resources))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Scripts", "Resources");
            var previous = UnityEditor.AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (previous)
            {
                UnityEditor.EditorUtility.CopySerialized(data, previous);
                UnityEditor.EditorUtility.SetDirty(previous);
                Object.DestroyImmediate(data);
            }
            else
                UnityEditor.AssetDatabase.CreateAsset(data, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("NavMesh улицы запечён в " + path);
        }
#endif
    }
}
