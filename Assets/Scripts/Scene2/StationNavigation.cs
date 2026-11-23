using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    static class StationNavigation
    {
        public static NavMeshData Bake()
        {
            if (NavMesh.GetSettingsCount() == 0) return null;
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(new Vector3(-8, .85f, 7f), new Vector3(36, 3.1f, 26f));
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(source => source.component &&
                (source.component.GetComponentInParent<FirstPersonController>() ||
                source.component.GetComponentInParent<QA_Door>() ||
                (source.component is Collider collider && collider.isTrigger)));
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentClimb = .30f;
            settings.agentRadius = .28f;
            settings.agentHeight = 1.75f;
            return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Night Chase/Bake Police Station NavMesh")]
        static void BakeAsset()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != Scene2FlowManager.ScenePath)
                return;

            var data = Bake();
            if (!data) { Debug.LogError("Не найдено геометрии для NavMesh бюро."); return; }
            const string folder = "Assets/Scripts/Scene2/Resources";
            const string path = folder + "/StationGroundNavMesh.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Scripts/Scene2", "Resources");
            var prior = UnityEditor.AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (prior)
            {
                UnityEditor.EditorUtility.CopySerialized(data, prior);
                UnityEditor.EditorUtility.SetDirty(prior);
                Object.DestroyImmediate(data);
            }
            else UnityEditor.AssetDatabase.CreateAsset(data, path);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}
// Сохранить NavMesh отдельно можно через Tools > Night Chase > Bake Police Station NavMesh.
