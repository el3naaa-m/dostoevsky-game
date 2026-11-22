using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase
{
    sealed class ChaseRunner : MonoBehaviour
    {
        NavMeshAgent agent;
        NavMeshPath path;
        Vector3[] route;
        int nextPoint;
        Vector3 destination;
        float retryPathAt;
        Transform upperArm, forearm;
        Transform reachTarget;
        MixamoNpcVisual visual;
        public Animator Anim => visual ? visual.Animator : null;
        bool traversing;
        float traversalPose;
        Vector3 navTarget;
        ChaseObstacleCourse obstacles;

        public bool Moving { get; private set; }
        public bool Arrived { get; private set; }

        public static ChaseRunner Spawn(Transform parent, string name, Vector3 location, Color shirt, string modelPath = null)
        {
            if (!NavMesh.SamplePosition(location, out var hit, 1.5f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"Нет NavMesh в точке появления {name}: {location}.");
                return null;
            }

            var actor = new GameObject(name);
            actor.transform.SetParent(parent);
            actor.transform.position = hit.position;
            var runner = actor.AddComponent<ChaseRunner>();
            runner.agent = actor.AddComponent<NavMeshAgent>();
            runner.agent.radius = 0.40f;
            runner.agent.height = 1.75f;
            runner.agent.acceleration = 16f;
            runner.agent.angularSpeed = 540f;
            runner.agent.stoppingDistance = .13f;
            runner.path = new NavMeshPath();

            var capsule = actor.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0, 0.9f, 0);

            runner.LoadModel(modelPath ?? "MixamoNpc/Ch06", shirt);
            return runner;
        }

        public void Follow(Vector3[] points, float speed)
        {
            reachTarget = null;
            nextPoint = 0;
            Arrived = false;
            route = points;
            agent.speed = speed;
            Advance();
        }

        public void Face(Vector3 point, float maxDegrees = 360f)
        {
            var direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), maxDegrees);
        }

        public void Reach(Transform target) => reachTarget = target;
        public void SetObstacles(ChaseObstacleCourse course) => obstacles = course;

        void Advance()
        {
            Moving = nextPoint < route.Length;
            if (!agent.isOnNavMesh) return;
            if (!Moving)
            {
                Arrived = route.Length > 0;
                agent.ResetPath();
                agent.isStopped = true;
                return;
            }

            destination = navTarget = route[nextPoint++];
            agent.autoBraking = nextPoint >= route.Length;
            TryRoute();
        }

        void TryRoute()
        {
            retryPathAt = Time.time + .25f;
            if (NavMesh.SamplePosition(destination, out var hit, .75f, agent.areaMask)
                && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete
                && agent.SetPath(path))
            {
                navTarget = hit.position;
                agent.isStopped = false;
                return;
            }
            agent.ResetPath();
            agent.isStopped = true;
        }

        void Update()
        {
            if (!Moving || traversing || !agent.isOnNavMesh || agent.pathPending) return;

            var remaining = navTarget - transform.position;
            remaining.y = 0;
            if (remaining.sqrMagnitude <= .32f * .32f)
            {
                Advance();
                return;
            }

            if ((!agent.hasPath || agent.isPathStale || agent.pathStatus != NavMeshPathStatus.PathComplete)
                && Time.time >= retryPathAt)
                TryRoute();
            if (!agent.hasPath) return;

            Vector3 direction = agent.steeringTarget - transform.position;
            direction.y = 0;
            bool blocked = StreetCar.Blocks(transform.position, direction, agent.speed, agent.radius);
            if (blocked)
            {
                agent.isStopped = true;
                return;
            }
            agent.isStopped = false;

            if (obstacles && obstacles.TryTraverse(transform.position, destination, out var landing,
                    out float height, out float duration) &&
                NavMesh.SamplePosition(landing, out var hit, .45f, agent.areaMask))
                StartCoroutine(Traverse(hit.position, height, duration));
        }

        IEnumerator Traverse(Vector3 landing, float height, float duration)
        {
            Vector3 from = transform.position;
            traversing = true;
            agent.enabled = false;
            Face(landing);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;
                traversalPose = Mathf.Sin(Mathf.PI * t);
                transform.position = ChasePlayerClimb.Arc(from, landing, t, height);
                yield return null;
            }

            transform.position = landing;
            traversalPose = 0;
            traversing = false;
            agent.enabled = true;
            if (agent.isOnNavMesh) TryRoute();
        }

        void LoadModel(string modelPath, Color shirt)
        {
            bool suspect = name == "Suspect";
            visual = MixamoNpcVisual.Create(transform, modelPath,
                suspect ? new Color(.14f, .42f, .98f) : shirt, 0f, suspect: suspect);
            if (!visual) return;
            upperArm = visual.Bone(HumanBodyBones.RightUpperArm, "RightArm");
            forearm = visual.Bone(HumanBodyBones.RightLowerArm, "RightForeArm");
        }

        void LateUpdate()
        {
            if (!visual) return;
            visual.Pose(traversing ? 0 : agent.velocity.magnitude, Time.deltaTime);
            if (!reachTarget)
            {
                visual.transform.localRotation = Quaternion.Euler(24f * traversalPose, 0, 0);
                return;
            }

            visual.transform.localRotation = Quaternion.Euler(12f, 0, 0);
            if (upperArm && forearm)
            {
                var handDirection = reachTarget.position - upperArm.position;
                if (handDirection.sqrMagnitude > .01f)
                    upperArm.rotation = Quaternion.FromToRotation(forearm.position - upperArm.position, handDirection)
                        * upperArm.rotation;
            }
        }

    }
}
