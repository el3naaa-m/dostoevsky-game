using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace NightChase
{
    public sealed class StreetLife : MonoBehaviour
    {
        const string ScenePath = "Assets/_Project/Scenes/Chase_Street.unity";
        const float WestLane = -19.7f;
        const float EastLane = 20.30f;
        const float SouthLane = -21.35f;
        const float NorthEnd = 20.5f;
        const float TurnRadius = 4.2f;
        const float LegLength = NorthEnd - SouthLane - TurnRadius;
        const float BendLength = Mathf.PI * TurnRadius / 2f;
        const float StraightLength = EastLane - WestLane - 2f * TurnRadius;
        const float RouteLength = 2f * (LegLength + BendLength) + StraightLength;
        const float ReentryDelay = 2f;

        [Header("Street traffic")]
        [SerializeField, Min(1)] float sedanSpeed = 5.5f;
        [SerializeField, Min(1)] float hatchbackSpeed = 5.1f;
        [SerializeField, Range(1.15f, 1.55f)] float laneOffset = 1.33f;
        [Header("Oncoming chase car")]
        [SerializeField, Min(1)] float taxiSpeed = 7.4f;
        [SerializeField, Min(0)] float taxiDelay = .18f;
        [SerializeField] Vector3 taxiTriggerCenter = new Vector3(-25.6f, 1, -24.1f);
        [SerializeField] Vector3 taxiTriggerSize = new Vector3(5.6f, 3, 6.2f);
        [SerializeField, Range(-13f, 10f)] float taxiEntryX = -10f;

        const string ModelPath = "StreetLife/";
        readonly Car[] cars = new Car[3];
        readonly Dictionary<Material, Material> convertedMaterials = new();
        readonly Walker[] walkers = new Walker[6];
        float elapsed;
        float taxiAt = float.PositiveInfinity;
        bool taxiArmed;

        sealed class Car
        {
            public Rigidbody Body;
            public readonly List<Transform> Wheels = new();
            public StreetCar Contact;
            public float WheelRadius, Speed, WheelSpeed, Distance, ReenterAt;
            public bool Reverse, Tactical, Finished;
        }

        sealed class Walker
        {
            public Rigidbody Body;
            public Vector3 Start, End;
            public float Speed, Offset;
            public PlayableGraph Graph;
            public AnimationClipPlayable Walk;
            public float WalkCycle;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= AddToStreet;
            SceneManager.sceneLoaded += AddToStreet;
        }

        static void AddToStreet(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath) return;
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.GetComponentInChildren<StreetLife>(true)) return;
            var root = new GameObject("Street Life");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<StreetLife>();
        }

        void Awake()
        {
            cars[0] = CreateCar("sedan", 1.88f, 1.45f, 4.76f, .33f, sedanSpeed, false, 18f);
            cars[1] = CreateCar("hatchback", 1.84f, 1.48f, 4.06f, .32f, hatchbackSpeed, true, 30f);
            float taxiStart = RouteLength - (LegLength + BendLength + taxiEntryX - (WestLane + TurnRadius));
            cars[2] = CreateCar("taxi", 1.88f, 1.65f, 4.76f, .33f, taxiSpeed, true, taxiStart);
            cars[2].Tactical = true;

            walkers[0] = CreateWalker("Man", -28, -12, -27.6f, 1.29f, 0, new Color(.34f, .16f, .24f));
            walkers[1] = CreateWalker("Woman", 7, -7, -27.5f, 1.12f, 3, new Color(.34f, .30f, .15f));
            walkers[2] = CreateWalker("Man", 12, 28, -27.45f, 1.39f, 6, new Color(.20f, .34f, .27f));
            walkers[3] = CreateWalker("Woman", -12, -2, -15.65f, 1.16f, 3, new Color(.38f, .21f, .15f));
            walkers[4] = CreateWalker("Man", 10, 1, -15.75f, 1.33f, 1, new Color(.23f, .26f, .36f));
            walkers[5] = CreateWalker("Woman", -27, -12, -26.75f, 1.21f, 16, new Color(.38f, .38f, .24f));

            UpdateTraffic(true);
        }

        void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            UpdateTraffic(false);
        }

        void UpdateTraffic(bool initial)
        {
            foreach (var car in cars)
                UpdateCar(car, initial);
            foreach (var walker in walkers)
            {
                float length = Vector3.Distance(walker.Start, walker.End);
                float phase = Mathf.Repeat(elapsed * walker.Speed + walker.Offset, 2 * length);
                float progress = Mathf.PingPong(phase, length) / length;
                Vector3 forward = walker.End - walker.Start;
                if (phase >= length) forward = -forward;
                Quaternion facing = Quaternion.LookRotation(forward);
                if (!initial)
                    facing = Quaternion.RotateTowards(walker.Body.rotation, facing, 540 * Time.fixedDeltaTime);
                Place(walker.Body, Vector3.Lerp(walker.Start, walker.End, progress), facing, initial);
            }
        }

        public void ArmChaseTraffic(Transform player)
        {
            if (taxiArmed) return;
            taxiArmed = true;
            ChaseTrigger.Create(transform, "Oncoming taxi trigger", taxiTriggerCenter, taxiTriggerSize,
                player, () => taxiAt = elapsed + taxiDelay);
        }

        void UpdateCar(Car car, bool initial)
        {
            if (car.Finished) return;
            bool entering = !car.Body.gameObject.activeSelf;
            if (entering)
            {
                float availableAt = car.Tactical ? taxiAt : car.ReenterAt;
                if (elapsed < availableAt || !HasEntrySpace(car)) return;
            }

            float step = 0;
            if (!initial && !entering && (!car.Tactical || elapsed >= taxiAt))
            {
                step = car.Speed * Time.fixedDeltaTime;
                foreach (var other in cars)
                {
                    if (other == car || other.Reverse != car.Reverse || !other.Body.gameObject.activeSelf
                        || other.Distance <= car.Distance) continue;
                    step = Mathf.Min(step, Mathf.Max(0, other.Distance - car.Distance - 7.4f));
                }
            }

            car.Distance += step;
            if (car.Distance >= RouteLength)
            {
                car.Body.gameObject.SetActive(false);
                car.Finished = car.Tactical;
                car.Distance = 0;
                car.ReenterAt = elapsed + ReentryDelay;
                return;
            }

            float distance = car.Reverse ? RouteLength - car.Distance : car.Distance;
            SampleRoad(distance, out Vector3 position, out Vector3 forward);
            if (car.Reverse) forward = -forward;
            position += Vector3.Cross(Vector3.up, forward) * laneOffset;

            Vector3 velocity = initial || entering ? Vector3.zero
                : (position - car.Body.position) / Time.fixedDeltaTime;
            Place(car.Body, position, Quaternion.LookRotation(forward), initial || entering);
            if (entering) car.Body.gameObject.SetActive(true);
            car.Contact.SetVelocity(velocity);
            car.WheelSpeed = velocity.magnitude;
        }

        bool HasEntrySpace(Car car)
        {
            foreach (var other in cars)
                if (other != car && other.Reverse == car.Reverse && other.Body.gameObject.activeSelf
                    && Mathf.Abs(other.Distance - car.Distance) < 8f)
                    return false;
            return true;
        }

        void LateUpdate()
        {
            foreach (var car in cars)
            {
                if (!car.Body.gameObject.activeSelf) continue;
                float spin = car.WheelSpeed * Time.deltaTime / car.WheelRadius * Mathf.Rad2Deg;
                foreach (var wheel in car.Wheels)
                    wheel.Rotate(Vector3.right, spin, Space.Self);
            }

            foreach (var walker in walkers)
            {
                if (!walker.Graph.IsValid()) continue;
                walker.WalkCycle = Mathf.Repeat(walker.WalkCycle + Time.deltaTime * walker.Speed / 1.25f,
                    walker.Walk.GetAnimationClip().length);
                walker.Walk.SetTime(walker.WalkCycle);
                walker.Graph.Evaluate();
            }
        }

        void OnDestroy()
        {
            foreach (var walker in walkers)
                if (walker != null && walker.Graph.IsValid()) walker.Graph.Destroy();
            foreach (var material in convertedMaterials.Values)
                Destroy(material);
        }

        static void SampleRoad(float distance, out Vector3 position, out Vector3 forward)
        {
            const float ground = .035f;
            if (distance < LegLength)
            {
                position = new Vector3(WestLane, ground, NorthEnd - distance);
                forward = Vector3.back;
                return;
            }

            distance -= LegLength;
            if (distance < BendLength)
            {
                float angle = distance / TurnRadius;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                position = new Vector3(WestLane + TurnRadius * (1 - cos), ground, SouthLane + TurnRadius * (1 - sin));
                forward = new Vector3(sin, 0, -cos);
                return;
            }

            distance -= BendLength;
            if (distance < StraightLength)
            {
                position = new Vector3(WestLane + TurnRadius + distance, ground, SouthLane);
                forward = Vector3.right;
                return;
            }

            distance -= StraightLength;
            if (distance < BendLength)
            {
                float angle = distance / TurnRadius;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                position = new Vector3(EastLane - TurnRadius * (1 - sin), ground, SouthLane + TurnRadius * (1 - cos));
                forward = new Vector3(cos, 0, sin);
                return;
            }

            position = new Vector3(EastLane, ground, SouthLane + TurnRadius + distance - BendLength);
            forward = Vector3.forward;
        }

        static void Place(Rigidbody body, Vector3 position, Quaternion rotation, bool initial)
        {
            if (initial)
            {
                body.position = position;
                body.rotation = rotation;
                return;
            }

            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        Car CreateCar(string modelName, float width, float height, float length, float wheelRadius, float speed, bool reverse, float distance)
        {
            Rigidbody body = CreateBody(modelName);
            var collision = body.gameObject.AddComponent<BoxCollider>();
            collision.center = Vector3.up * (height * .5f);
            collision.size = new Vector3(width, height, length);

            var car = new Car
            {
                Body = body,
                Contact = body.gameObject.AddComponent<StreetCar>(),
                WheelRadius = wheelRadius,
                Speed = speed,
                Reverse = reverse,
                Distance = distance
            };
            var model = CreateModel(modelName, body.transform);
            if (model)
            {
                foreach (var part in model.GetComponentsInChildren<Transform>())
                    if (part.name.StartsWith("Wheel_", System.StringComparison.Ordinal))
                        car.Wheels.Add(part);
            }
            return car;
        }

        Walker CreateWalker(string modelName, float from, float to, float z, float speed, float offset, Color jacket)
        {
            Rigidbody body = CreateBody("Pedestrian");
            var collision = body.gameObject.AddComponent<CapsuleCollider>();
            collision.center = Vector3.up * .88f;
            collision.height = 1.76f;
            collision.radius = .30f;

            var walker = new Walker
            {
                Body = body,
                Start = new Vector3(from, .142f, z),
                End = new Vector3(to, .142f, z),
                Speed = speed,
                Offset = offset
            };

            var model = CreateModel(modelName, body.transform);
            if (!model) return walker;

            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                float scale = 1.72f / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition -= Vector3.up * (bounds.min.y * scale);

                var color = new MaterialPropertyBlock();
                color.SetColor("_BaseColor", jacket);
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] && materials[i].name == "Shirt") renderer.SetPropertyBlock(color, i);
                }
            }

            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            foreach (var clip in Resources.LoadAll<AnimationClip>(ModelPath + modelName))
            {
                if (clip.legacy || clip.length <= 0 || clip.name != "Walk")
                    continue;

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                walker.Walk = AnimationPlayableUtilities.PlayClip(animator, clip, out walker.Graph);
                walker.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                walker.WalkCycle = Mathf.Repeat(offset, clip.length);
                return walker;
            }

            Debug.LogWarning($"Street Life: missing Walk clip on {modelName}.", model);
            return walker;
        }

        GameObject CreateModel(string modelName, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ModelPath + modelName);
            if (!prefab)
            {
                Debug.LogError($"Street Life: model {modelName} is missing.", parent);
                return null;
            }

            var model = Instantiate(prefab, parent, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!shader || !source || source.shader == shader || !source.HasProperty("_Color")) continue;
                    if (!convertedMaterials.TryGetValue(source, out var material))
                    {
                        material = new Material(shader) { name = source.name, color = source.color };
                        if (source.HasProperty("_MainTex")) material.mainTexture = source.mainTexture;
                        if (source.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                        if (source.HasProperty("_Glossiness")) material.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
                        convertedMaterials.Add(source, material);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }

            return model;
        }

        Rigidbody CreateBody(string name)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(transform, false);
            var body = actor.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }

    }
}
