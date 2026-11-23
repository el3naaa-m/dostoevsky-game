using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class RouteGizmos : MonoBehaviour
    {
        [SerializeField] Transform[] points;
        internal Transform[] Points => points;
        void Start() => RouteValidator.HasWaypoints(points, this);

        void OnDrawGizmos()
        {
            if (points == null) return;
            bool baked = NavMesh.CalculateTriangulation().vertices.Length > 0;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (!point) continue;
                Gizmos.color = !baked ? Color.yellow : RouteValidator.Sample(point, NavMesh.AllAreas, out _) ? Color.green : Color.red;
                Gizmos.DrawSphere(point.position, .11f);
                if (i > 0 && points[i - 1]) Gizmos.DrawLine(points[i - 1].position, point.position);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(point.position + Vector3.up * .22f, $"{i + 1}. {point.name}");
#endif
            }
        }
    }
}
