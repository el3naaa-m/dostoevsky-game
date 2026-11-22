using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        PlayableGraph graph;
        AnimationClipPlayable walk;
        float cycle;
        Transform upperArm, forearm;
        Transform reachTarget;
        Transform visual;
        ChasePace pace;
        bool traversing;
        float traversalPose, followSpeed;
        ChaseObstacleCourse obstacles;

        public bool Moving { get; private set; }
        public bool Arrived { get; private set; }
        public float LeadOverPlayer => pace?.Gap ?? 0;

        public static ChaseRunner Spawn(Transform parent, string name, Vector3 location, Color shirt)
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
            runner.agent.radius = .30f;
            runner.agent.height = 1.75f;
            runner.agent.acceleration = 16f;
            runner.agent.angularSpeed = 540f;
            runner.agent.stoppingDistance = .13f;
            runner.path = new NavMeshPath();
            runner.LoadModel(shirt);
            return runner;
        }

        public void Follow(Vector3[] points, float speed)
        {
            reachTarget = null;
            nextPoint = 0;
            Arrived = false;
            route = points;
            agent.speed = followSpeed = speed;
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

        public void SetPacing(ChasePace value) => pace = value;

        public void EndPacing()
        {
            pace = null;
            if (agent.enabled) agent.speed = followSpeed;
        }

        void Advance()
        {
            Moving = false;
            if (!agent.isOnNavMesh) return;

            if (nextPoint < route.Length)
            {
                var point = route[nextPoint++];
                if (NavMesh.SamplePosition(point, out var hit, 1.25f, agent.areaMask)
                    && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.isStopped = false;
                    destination = hit.position;
                    agent.autoBraking = nextPoint >= route.Length;
                    retryPathAt = Time.time + .5f;
                    Moving = agent.SetDestination(destination);
                    if (Moving) return;
                }
                Debug.LogWarning($"Нет полного пути для {name} к точке {point}.", this);
            }
            else Arrived = route.Length > 0;

            agent.ResetPath();
            agent.isStopped = true;
        }

        void Update()
        {
            if (!Moving || traversing || !agent.isOnNavMesh || agent.pathPending) return;

            if (obstacles && obstacles.TryTraverse(transform.position, destination, out var landing,
                    out float height, out float duration) &&
                NavMesh.SamplePosition(landing, out var hit, .45f, agent.areaMask))
            {
                StartCoroutine(Traverse(hit.position, height, duration));
                return;
            }

            if (pace != null && nextPoint > 0)
            {
                agent.speed = pace.Speed(transform.position, nextPoint, out float correctedProgress);
                if (pace.TryGetCorrection(correctedProgress, nextPoint, obstacles,
                        transform.position, out var correction, out int segment))
                    KeepAhead(correction, segment);
            }
            var remaining = destination - transform.position;
            remaining.y = 0;
            if (remaining.sqrMagnitude <= .32f * .32f)
            {
                Advance();
                return;
            }

            if (Time.time < retryPathAt) return;
            if (!agent.hasPath || agent.isPathStale || agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                retryPathAt = Time.time + .5f;
                agent.SetDestination(destination);
            }
        }

        void KeepAhead(Vector3 correction, int segment)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(correction, out var hit, .45f, filter) ||
                !IsClearCorrection(hit.position, filter) ||
                !NavMesh.SamplePosition(route[segment - 1], out var waypoint, 1.25f, filter) ||
                !agent.Warp(hit.position)) return;

            nextPoint = segment;
            destination = waypoint.position;
            agent.autoBraking = nextPoint >= route.Length;
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        bool IsClearCorrection(Vector3 point, NavMeshQueryFilter filter)
        {
            if (!NavMesh.Raycast(agent.nextPosition, point, out _, filter)) return true;
            if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            float length = 0;
            Vector3 previous = agent.nextPosition;
            foreach (var corner in path.corners)
            {
                length += Vector3.Distance(previous, corner);
                previous = corner;
            }
            return length <= Vector3.Distance(agent.nextPosition, point) * 1.42f + .45f;
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
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
        }

        void LoadModel(Color shirt)
        {
            const string modelPath = "StreetLife/Man";
            var prefab = Resources.Load<GameObject>(modelPath);
            if (!prefab) return;

            var model = Instantiate(prefab, transform, false);
            visual = model.transform;
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin && skin.bounds.size.y > .01f)
            {
                Bounds bounds = skin.bounds;
                float scale = 1.73f / bounds.size.y;
                visual.localScale *= scale;
                visual.localPosition += Vector3.up * ((transform.position.y - bounds.min.y) * scale);

                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", shirt);
                properties.SetColor("_BaseColor", shirt);
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && materials[i].name == "Shirt") skin.SetPropertyBlock(properties, i);

                foreach (var bone in skin.bones)
                {
                    if (bone.name == "UpperArm.R") upperArm = bone;
                    if (bone.name == "LowerArm.R") forearm = bone;
                }
            }

            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in Resources.LoadAll<AnimationClip>(modelPath))
            {
                if (clip.name != "Walk" || clip.legacy || clip.length <= 0) continue;
                walk = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                break;
            }
        }

        void LateUpdate()
        {
            if (graph.IsValid())
            {
                float speed = traversing ? 0 : agent.velocity.magnitude;
                cycle = speed > .1f
                    ? Mathf.Repeat(cycle + Time.deltaTime * Mathf.Min(speed / 1.25f, 2.6f), walk.GetAnimationClip().length)
                    : .12f;
                walk.SetTime(cycle);
                graph.Evaluate();
            }

            if (!visual) return;
            if (!reachTarget)
            {
                visual.localRotation = Quaternion.Euler(24f * traversalPose, 0, 0);
                return;
            }

            visual.localRotation = Quaternion.Euler(12f, 0, 0);
            if (upperArm && forearm)
            {
                var handDirection = reachTarget.position - upperArm.position;
                if (handDirection.sqrMagnitude > .01f)
                    upperArm.rotation = Quaternion.FromToRotation(forearm.position - upperArm.position, handDirection)
                        * upperArm.rotation;
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
