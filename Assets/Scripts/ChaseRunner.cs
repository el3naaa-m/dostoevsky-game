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

            runner.LoadModel(modelPath ?? "StreetLife/Man", shirt);
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
            var prefab = Resources.Load<GameObject>(modelPath);
            if (!prefab)
            {
                Debug.LogWarning($"NPC модель не найдена по Resources пути: {modelPath}");
                return;
            }

            var model = Instantiate(prefab, transform, false);
            visual = model.transform;
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin && skin.bounds.size.y > .01f)
            {
                Bounds bounds = skin.bounds;
                float scale = 1.75f / bounds.size.y;
                visual.localScale *= scale;
                visual.localPosition += Vector3.up * ((transform.position.y - bounds.min.y) * scale);

                // Материалы под URP-пайплайн проекта. Кофта преступника — синяя.
                bool isCriminal = shirt.r < 0.2f && shirt.g < 0.4f && shirt.b > 0.5f;
                var materials = skin.sharedMaterials;
                bool shirtTinted = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] && materials[i].name == "Shirt")
                    {
                        var properties = new MaterialPropertyBlock();
                        properties.SetColor("_Color", shirt);
                        properties.SetColor("_BaseColor", shirt);
                        skin.SetPropertyBlock(properties, i);
                        shirtTinted = true;
                    }
                }
                if (!shirtTinted)
                {
                    var fallback = isCriminal
                        ? Resources.Load<Material>("MixamoNpc/Materials/NPC_Criminal")
                        : Resources.Load<Material>("MixamoNpc/Materials/NPC_Civilian");
                    if (fallback)
                    {
                        for (int i = 0; i < materials.Length; i++)
                            if (materials[i]) materials[i] = fallback;
                        skin.sharedMaterials = materials;
                    }
                }

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
            string walkPath = modelPath.StartsWith("MixamoNpc/")
                ? "MixamoNpc/Motion/Walk"
                : modelPath;
            AnimationClip walkClip = null;
            foreach (var clip in Resources.LoadAll<AnimationClip>(walkPath))
            {
                if (clip.legacy || clip.length <= 0) continue;
                if (clip.name.IndexOf("walk", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    walkClip = clip;
                    break;
                }
                if (walkClip == null) walkClip = clip;
            }
            if (walkClip)
            {
                walk = AnimationPlayableUtilities.PlayClip(animator, walkClip, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
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
