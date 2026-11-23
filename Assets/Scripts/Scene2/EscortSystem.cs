using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class EscortSystem : MonoBehaviour
    {
        [SerializeField, Min(1)] float waitDistance = 6f;
        [SerializeField, Min(.1f)] float pace = 2.1f;
        NavMeshAgent leader, follower;
        Transform player;
        Transform[] points;
        int index;
        bool issued, running, direct, followerCommanded;
        int followerIndex;
        float nextUpdate, lastProgress;
        Vector3 lastPosition;
        Vector3 destination;
        internal const string MissingWaypoints = "[EscortSystem] Ошибка: не назначены waypoints!";
        public bool Complete { get; private set; }
        internal bool Failed { get; private set; }
        bool requireNavMesh;
        float activePace;

        internal void Begin(NavMeshAgent lead, NavMeshAgent companion, Transform observer, Transform[] route, bool navMeshOnly = false, float movementSpeed = 0)
        {
            requireNavMesh = navMeshOnly;
            activePace = movementSpeed > 0 ? movementSpeed : pace;
            Failed = false;
            leader = lead;
            follower = companion;
            player = observer;
            points = route ?? System.Array.Empty<Transform>();
            index = 0;
            issued = Complete = followerCommanded = false;
            loggedDirectPoints.Clear();
            running = leader;
            direct = false;
            followerIndex = 0;
            if (!leader)
            {
                Debug.LogError("[EscortSystem] У ведущего отсутствует NavMeshAgent.", this);
                return;
            }
            leader.enabled = true;
            if (follower) follower.enabled = true;
            Debug.Log($"[EscortSystem] Запуск {leader.name}, сопровождаемый {follower?.name}, точек {points.Length}.", this);
            if (!RouteValidator.HasWaypoints(points, this)) { running = false; return; }
            RouteValidator.Validate(points, leader);
            LogAgent(leader);
            if (follower) LogAgent(follower);
            lastPosition = leader.transform.position;
            lastProgress = Time.time;
            if (requireNavMesh && !leader.isOnNavMesh &&
                NavMesh.SamplePosition(leader.transform.position,out var nearby,.45f,leader.areaMask))
                leader.Warp(nearby.position);
            if (!leader.isOnNavMesh || (follower && !follower.isOnNavMesh)) UseDirectRoute();
            leader.speed = activePace;
            if (requireNavMesh) leader.stoppingDistance = .14f;
            if (follower && follower.isOnNavMesh) follower.speed = activePace;
            Update();
        }

        static void LogAgent(NavMeshAgent agent)
        {
            Debug.Log($"[EscortSystem] {agent.name}: enabled={agent.enabled}, isOnNavMesh={agent.isOnNavMesh}.", agent);
            if (!agent.isOnNavMesh) Debug.LogWarning($"[EscortSystem] {agent.name} стоит вне NavMesh.", agent);
        }

        void Update()
        {
            if (!running || !leader) return;
            if (direct) { FollowWaypoints(); return; }
            if (!leader.isOnNavMesh) { UseDirectRoute(); return; }
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + .2f;
            bool hasFollower = follower && follower.isOnNavMesh;
            bool separated = hasFollower && index > 0 && Vector3.Distance(leader.transform.position, follower.transform.position) > 3.4f;
            leader.isStopped = separated || (player && Vector3.Distance(player.position,leader.transform.position) > waitDistance);
            if (hasFollower)
            {
                follower.isStopped = Vector3.Distance(follower.transform.position,leader.transform.position) < .95f;
                if (!follower.isStopped && NavMesh.SamplePosition(leader.transform.position, out var followPoint, .65f, follower.areaMask))
                {
                    if (follower.SetDestination(followPoint.position) && !followerCommanded)
                        Debug.Log($"[EscortSystem] {follower.name}: начинаю следовать за {leader.name}.", follower);
                    followerCommanded = true;
                }
            }

            bool waitingForPlayer = player && Vector3.Distance(player.position, leader.transform.position) > waitDistance;
            if (waitingForPlayer || (leader.transform.position - lastPosition).sqrMagnitude > .04f)
            {
                lastPosition = leader.transform.position;
                lastProgress = Time.time;
            }
            if (issued && Time.time - lastProgress > 2f) { UseDirectRoute(); return; }

            float arrivalRadius = requireNavMesh ? .27f : .6f;
            if (issued && (leader.transform.position - destination).sqrMagnitude < arrivalRadius*arrivalRadius && !separated)
            {
                issued = false;
                index++;
            }
            if (issued && !leader.pathPending && leader.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning($"Путь к точке {index + 1} прервался. Перехожу к следующей точке.", this);
                UseDirectRoute();
                return;
            }
            if (issued) return;
            if (index >= points.Length)
            {
                if (hasFollower && Vector3.Distance(follower.transform.position, destination) > 1.15f)
                {
                    UseDirectRoute();
                    return;
                }
                Complete = true;
                leader.isStopped = true;
                if (hasFollower) follower.isStopped = true;
                running = false;
                return;
            }

            int selected = index;
            if (!TryPoint(index, requireNavMesh ? .34f : .65f, out destination))
            {
                Debug.LogWarning($"Точка {index + 1} недоступна. Ищу ближайшую достижимую точку.", this);
                float best = float.PositiveInfinity;
                Vector3 origin = points[index] ? points[index].position : leader.transform.position;
                selected = -1;
                for (int i = index; i < points.Length; i++)
                {
                    if (!TryPoint(i, 1.6f, out var candidate)) continue;
                    float distance = (candidate - origin).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    selected = i;
                    destination = candidate;
                }
            }
            if (selected != index || (requireNavMesh && !FollowsCorridor(destination)) || !leader.SetDestination(destination))
            {
                UseDirectRoute();
                return;
            }
            index = selected;
            lastProgress = Time.time;
            Debug.Log($"[EscortSystem] Начинаю движение к точке {index}: {points[index].name}, {destination}. NPC: {leader.name}.", leader);
            issued = true;
        }

        bool FollowsCorridor(Vector3 goal)
        {
            var start = leader.nextPosition;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start,goal,leader.areaMask,path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            var segment = goal - start;
            foreach (var corner in path.corners)
            {
                float fraction = segment.sqrMagnitude > .001f
                    ? Mathf.Clamp01(Vector3.Dot(corner-start,segment)/segment.sqrMagnitude) : 0;
                // Не принимать обход через соседнюю комнату вместо прохода по заданному коридору.
                if ((corner - (start+segment*fraction)).sqrMagnitude > 1.15f*1.15f) return false;
            }
            return true;
        }

        void UseDirectRoute()
        {
            // Для кабинета попытка пройти напрямую могла бы провести шефа сквозь стены.
            if (requireNavMesh)
            {
                if (leader.isOnNavMesh) leader.isStopped = true;
                Debug.LogWarning($"[EscortSystem] {leader.name}: маршрут остановлен у точки {index}. Проверьте NavMesh и дверной проём.",leader);
                running = false;
                Failed = true;
                return;
            }
            Debug.LogWarning($"{leader.name}: перехожу на движение по точкам.", leader);
            direct = true;
            if (follower)
            {
                float nearest = float.PositiveInfinity;
                for (int i = 0; i <= Mathf.Min(index, points.Length - 1); i++)
                {
                    if (!points[i]) continue;
                    float distance = (follower.transform.position - points[i].position).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    followerIndex = i;
                }
            }
            leader.enabled = false;
            if (follower) follower.enabled = false;
        }

        void FollowWaypoints()
        {
            if (!player || Vector3.Distance(player.position, leader.transform.position) <= waitDistance)
                Walk(leader.transform, ref index, points.Length);
            if (follower) Walk(follower.transform, ref followerIndex, index);
            if (index < points.Length || (follower && followerIndex < points.Length)) return;
            running = false;
            Complete = true;
        }

        readonly System.Collections.Generic.Dictionary<Transform,int> loggedDirectPoints = new();

        void Walk(Transform actor, ref int pointIndex, int limit)
        {
            while (pointIndex < limit && !points[pointIndex]) pointIndex++;
            if (pointIndex >= limit) return;
            var goal = points[pointIndex].position;
            if (!loggedDirectPoints.TryGetValue(actor, out int logged) || logged != pointIndex)
            {
                loggedDirectPoints[actor] = pointIndex;
                Debug.Log($"[EscortSystem] Начинаю движение к точке {pointIndex}: {points[pointIndex].name}, {goal}. NPC: {actor.name}.", actor);
            }
            var direction = Vector3.ProjectOnPlane(goal - actor.position, Vector3.up);
            if (direction.sqrMagnitude > .0025f)
                actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(direction), 540f * Time.deltaTime);
            actor.position = Vector3.MoveTowards(actor.position, goal, activePace * Time.deltaTime);
            if ((actor.position - goal).sqrMagnitude < .001f) pointIndex++;
        }

        bool TryPoint(int i, float radius, out Vector3 result)
        {
            result = default;
            if (!leader || !leader.isOnNavMesh || points == null || i < 0 || i >= points.Length || !points[i] || !NavMesh.SamplePosition(points[i].position, out var hit, radius, leader.areaMask)) return false;
            if (Mathf.Abs(hit.position.y - points[i].position.y) > .6f) return false;

            result = hit.position;
            return true;
        }
    }
}
