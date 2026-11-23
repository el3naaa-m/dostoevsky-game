using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class EscortSystem : MonoBehaviour
    {
        [SerializeField, Min(1)] float waitDistance = 6f;
        [SerializeField, Min(.1f)] float pace = 2.1f;
        Transform[] waypoints;
        NavMeshAgent partner, suspect, chief;
        Transform player;
        int index;
        float updateAt;
        bool running;
        public bool Complete { get; private set; }
        internal Transform Destination => waypoints[waypoints.Length - 1];

        internal void Begin(Transform target, NavMeshAgent leader, NavMeshAgent detainee, NavMeshAgent supervisor, Transform[] route)
        {
            player = target;
            partner = leader;
            suspect = detainee;
            chief = supervisor;
            waypoints = route;
            running = true;
            if (OnMesh(partner)) partner.speed = pace;
            if (OnMesh(suspect)) suspect.speed = pace;
            if (OnMesh(chief)) chief.speed = pace + .15f;
        }

        void Update()
        {
            if (!running || Time.time < updateAt) return;
            updateAt = Time.time + .22f;
            if (index >= waypoints.Length)
            {
                Stop(partner);
                Stop(chief);
                Stop(suspect);
                Complete = true;
                running = false;
                return;
            }

            var goal = waypoints[index].position;
            if (!OnMesh(partner)) return;

            bool atGoal = Vector3.Distance(partner.transform.position, goal) < .85f;
            if (atGoal)
            {
                if (!OnMesh(suspect) || Vector3.Distance(suspect.transform.position, partner.transform.position) < 2.45f)
                    index++;
            }

            bool wait = Vector3.Distance(player.position, partner.transform.position) > waitDistance;
            partner.isStopped = wait;
            if (OnMesh(chief))
            {
                chief.isStopped = wait;
                if (!wait) SetGoal(chief, index == waypoints.Length - 1 ? goal + Vector3.forward * .85f : goal);
            }
            if (!wait) SetGoal(partner, goal);
            if (!OnMesh(suspect)) return;
            suspect.isStopped = wait || Vector3.Distance(suspect.transform.position, partner.transform.position) < 1.05f;
            if (!suspect.isStopped) SetGoal(suspect, partner.transform.position);
        }

        readonly NavMeshPath plannedPath = new();
        void SetGoal(NavMeshAgent agent, Vector3 goal)
        {
            if (!NavMesh.SamplePosition(goal, out var hit, .8f, agent.areaMask)) return;
            if (agent.hasPath && (agent.destination - hit.position).sqrMagnitude < .09f) return;
            if (!agent.CalculatePath(hit.position, plannedPath) || plannedPath.status != NavMeshPathStatus.PathComplete)
            {
                agent.ResetPath();
                return;
            }
            foreach (var corner in plannedPath.corners)
                if (Mathf.Abs(corner.y - hit.position.y) > .85f) { agent.ResetPath(); return; }
            agent.SetPath(plannedPath);
        }

        static bool OnMesh(NavMeshAgent agent) => agent && agent.isOnNavMesh;
        static void Stop(NavMeshAgent agent) { if (OnMesh(agent)) agent.isStopped = true; }
    }
}
// На Scene2 Mechanics. Точки — пустые GameObject от коридора до допросной.
