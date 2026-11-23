using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class RouteValidator : MonoBehaviour
    {
        [SerializeField] RouteGizmos route;
        void Start()
        {
            if (!route) route = GetComponent<RouteGizmos>();
            if (HasWaypoints(route ? route.Points : null, this)) Validate(route.Points, null);
        }

        internal static bool HasWaypoints(Transform[] points, Object context)
        {
            if (points != null)
            {
                bool valid = false;
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i]) valid = true;
                    else Debug.LogWarning($"{EscortSystem.MissingWaypoints} Индекс {i} пропущен.", context);
                }
                if (valid) return true;
            }
            Debug.LogError(EscortSystem.MissingWaypoints, context);
            return false;
        }

        internal static void Validate(Transform[] points, NavMeshAgent agent)
        {
            if (points == null || points.Length == 0) { Debug.LogWarning("Маршрут: не хватает точки 1."); return; }
            bool ok = true;
            Vector3? previous = agent && agent.isOnNavMesh ? agent.transform.position : null;
            int mask = agent ? agent.areaMask : NavMesh.AllAreas;
            var path = new NavMeshPath();
            for (int i = 0; i < points.Length; i++)
            {
                if (!points[i]) { Debug.LogWarning($"Маршрут: не хватает точки {i + 1}."); ok = false; continue; }
                if (!Sample(points[i], mask, out var hit))
                {
                    Debug.LogWarning($"Маршрут: точка {i + 1} вне NavMesh.", points[i]);
                    ok = false;
                    continue;
                }
                if (previous.HasValue && (!NavMesh.CalculatePath(previous.Value,hit.position,mask,path) || path.status != NavMeshPathStatus.PathComplete))
                {
                    Debug.LogWarning($"Маршрут: до точки {i + 1} нет полного пути от предыдущей позиции.", points[i]);
                    ok = false;
                }
                for (int j = 0; j < i; j++)
                    if (points[j] == points[i]) { Debug.LogWarning($"Маршрут: точка {i + 1} повторяет точку {j + 1}.", points[i]); ok = false; break; }
                previous = hit.position;
            }
            if (ok) Debug.Log("[RouteValidator] Маршрут ок.", points[0]);
        }

        internal static bool Sample(Transform point, int mask, out NavMeshHit hit) =>
            NavMesh.SamplePosition(point.position, out hit, .45f, mask);
    }
}
