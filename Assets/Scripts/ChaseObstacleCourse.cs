using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase
{
    sealed class ChaseObstacleCourse : MonoBehaviour
    {
        const float CorridorX = 37.44f;
        const float CorridorWidth = 2.92f;
        const float GateZ = -1.8f;
        const float GateHeight = 1.52f;
        static readonly float[] DefaultHurdles = { 15.2f, 8.3f };
        float[] hurdles = DefaultHurdles;

        readonly List<ChaseTrigger> tutorialZones = new();
        readonly List<Hint> hints = new();
        CharacterController player;
        ChaseTrigger finishZone;
        bool tutorialActive;
        GUIStyle hintStyle;

        sealed class Hint
        {
            public string Text;
            public float Until;
        }

        public void Build(StreetOwnership street, FirstPersonController controller, Transform eyes, ChaseRunner suspect, Vector3 finish)
        {
            player = controller.GetComponent<CharacterController>();
            Transform package = FindArt(street, "Discarded package");
            Transform bin = FindArt(street, "Waste container");

            PlaceArtObstacle(package, "Sidewalk packages", new Vector3(-15.6f, .142f, -26.3f),
                new Vector3(1.10f, .63f, .93f), true);
            PlaceArtObstacle(bin, "Road waste container", new Vector3(-9.8f, 0, -18.35f),
                new Vector3(1.82f, .92f, .90f), true);

            var generator = FindAnyObjectByType<ObstacleGenerator>();
            if (!generator || generator.gameObject.scene != gameObject.scene)
                generator = gameObject.AddComponent<ObstacleGenerator>();
            hurdles = generator.Build(transform,CorridorX,CorridorWidth,DefaultHurdles,GateZ,GateHeight);
            var gateZone = ChaseTrigger.Create(transform, "Climb interaction trigger",
                new Vector3(CorridorX, 1.15f, GateZ), new Vector3(2.70f, 2.8f, 3.15f));
            gameObject.AddComponent<ChasePlayerClimb>().Initialize(controller, eyes, gateZone, GateZ);
            finishZone = ChaseTrigger.Create(transform, "Chase finish trigger", finish + Vector3.up,
                new Vector3(.96f, 3f, .96f), suspect.transform);
            var marker = new GameObject("Suspect finish sensor");
            marker.transform.SetParent(suspect.transform, false);
            marker.transform.localPosition = Vector3.up * .80f;
            var sensor = marker.AddComponent<SphereCollider>();
            sensor.isTrigger = true;
            sensor.radius = .10f;

            tutorialActive = true;
            AddHint("Sprint tutorial trigger", player.bounds.center, new Vector3(4, 3, 4),
                "SHIFT — удерживайте, чтобы бежать");
            AddHint("Jump tutorial trigger", new Vector3(CorridorX, 1.2f, 20f), new Vector3(2.7f, 3.3f, 7.7f),
                "ПРОБЕЛ — прыжок");
            AddHint("Climb tutorial trigger", new Vector3(CorridorX, 1.2f, 3.2f), new Vector3(2.7f, 3.3f, 7.8f),
                "ПРОБЕЛ + E — перелезть");
            tutorialZones[0].Check(player.bounds.center);
        }

        public bool AtFinish(Vector3 position)
        {
            if (!finishZone) return false;
            finishZone.Check(position + Vector3.up * .8f);
            return finishZone.Fired;
        }

        public void EndTutorial()
        {
            tutorialActive = false;
            hints.Clear();
        }

        void AddHint(string name, Vector3 position, Vector3 size, string text)
        {
            tutorialZones.Add(ChaseTrigger.Create(transform, name, position, size, player.transform, () =>
            {
                if (tutorialActive)
                    hints.Add(new Hint { Text = text, Until = Time.time + 3f });
            }));
        }

        void Update()
        {
            if (!tutorialActive || !player) return;
            foreach (var zone in tutorialZones)
                zone.Check(player.bounds.center);
            hints.RemoveAll(hint => Time.time >= hint.Until);
        }

        public bool TryTraverse(Vector3 position, Vector3 target, out Vector3 landing, out float height, out float duration)
        {
            landing = default;
            height = duration = 0;
            if (Mathf.Abs(position.x - CorridorX) > 1.28f || target.z >= position.z) return false;

            foreach (float z in hurdles)
                if (TryCross(position, target, z, .82f, out landing))
                {
                    height = .70f;
                    duration = .41f;
                    return true;
                }

            if (!TryCross(position, target, GateZ, 1.10f, out landing)) return false;
            height = 1.83f;
            duration = .73f;
            return true;
        }

        static bool TryCross(Vector3 position, Vector3 target, float z, float step, out Vector3 landing)
        {
            landing = new Vector3(position.x, position.y, z - step);
            return position.z > z && position.z - z <= step + .24f && target.z < z - step;
        }

        void OnGUI()
        {
            if (hints.Count == 0) return;
            hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            hintStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .028f), 14, 36);
            hintStyle.normal.textColor = Color.white;
            float width = Mathf.Min(Screen.width * .86f, 680f);
            float height = hintStyle.fontSize * 2.6f;
            Color previous = GUI.color;
            for (int i = 0; i < hints.Count; i++)
            {
                float alpha = Mathf.Clamp01((hints[i].Until - Time.time) / .55f);
                GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
                var panel = new Rect((Screen.width - width) / 2, Screen.height * .19f + i * (height + 6),
                    width, height);
                GUI.Box(panel, GUIContent.none);
                GUI.Label(new Rect(panel.x + 12, panel.y, panel.width - 24, panel.height), hints[i].Text, hintStyle);
            }
            GUI.color = previous;
        }

        static Transform FindArt(StreetOwnership street, string name)
        {
            foreach (var candidate in street.GetComponentsInChildren<Transform>())
                if (candidate.name == name && candidate.GetComponentInChildren<MeshRenderer>())
                    return candidate;
            Debug.LogWarning($"Не найден объект уличного ассета: {name}.", street);
            return null;
        }

        void PlaceArtObstacle(Transform source, string name, Vector3 position, Vector3 size, bool carve)
        {
            if (!source) return;
            Transform root = Anchor(name, position);
            CopyArt(source, root, size);
            Solid(root, size, carve);
        }

        Transform Anchor(string name, Vector3 position)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.position = position;
            return root;
        }

        static void CopyArt(Transform source, Transform root, Vector3 size)
        {
            var visual = Instantiate(source.gameObject, root, false).transform;
            visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var originalCollider in visual.GetComponentsInChildren<Collider>())
                originalCollider.enabled = false;

            var renderers = visual.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = CombinedBounds(renderers);
            Vector3 scale = bounds.size;
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(
                size.x / Mathf.Max(scale.x, .01f), size.y / Mathf.Max(scale.y, .01f), size.z / Mathf.Max(scale.z, .01f)));
            bounds = CombinedBounds(renderers);
            visual.position += root.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        static Bounds CombinedBounds(MeshRenderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void Solid(Transform root, Vector3 size, bool carve)
        {
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * (size.y * .5f);
            collider.size = size;
            if (!carve) return;
            var obstacle = root.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

    }
}
