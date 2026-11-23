using System.Collections;
using NightChase.Station;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NightChase
{
    public sealed class ChaseOpening : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Project/Scenes/Chase_Street.unity";

        [SerializeField] Vector3 openingPosition = new Vector3(-35.49895f, .177f, -25.52321f);
        [SerializeField] string suspectModel = "MixamoNpc/Ch08";
        [Min(0)] public float startDelay = 2.5f;
        [Min(0)] public float endDelay = 2.5f;
        [SerializeField] string partnerModel = "MixamoNpc/Ch33";
        [SerializeField] Vector3 suspectStart = new Vector3(-23.8f, 0, -25.85f);
        [SerializeField] Vector3[] suspectWaypoints =
        {
            new Vector3(-16.3f, 0, -17.45f),
            new Vector3(13f, 0, -17.45f),
            new Vector3(16f, 0, -17.45f),
            new Vector3(24.2f, .142f, -15.1f),
            new Vector3(24.4f, .142f, -2f),
            new Vector3(24.4f, .142f, 17.5f),
            new Vector3(24.4f, 0, 24.4f),
            new Vector3(31.7f, 0, 24.4f),
            new Vector3(37.35f, 0, 23.9f),
            new Vector3(37.35f, 0, 18f),
            new Vector3(37.35f, 0, 1f),
            new Vector3(37.35f, 0, -17f),
            new Vector3(37.35f, 0, -21.9f),
            new Vector3(35.8f, 0, -23.5f),
            new Vector3(33.6f, 0, -23.7f)
        };
        [SerializeField] Vector3[] partnerWaypoints =
        {
            new Vector3(-36.4f, 0, -25.8f),
            new Vector3(-29f, 0, -23.8f),
            new Vector3(-20f, 0, -19.3f),
            new Vector3(-19.5f, 0, -16f),
            new Vector3(-19.5f, 0, 8f)
        };
        [SerializeField, Min(1)] float warningDistance = 22f;
        [SerializeField, Min(1)] float suspectRunSpeed = 6.6f;

        FirstPersonController player;
        StreetOwnership street;
        Transform eyes;
        Vector3 standingPosition;
        Quaternion standingRotation;
        bool controlsLocked;
        NavMeshData ownedNavigation;
        NavMeshDataInstance navigation;
        ChaseRunner suspect;
        ChaseObstacleCourse obstacles;
        bool chaseActive;
        DialogueSystem dialogue;
        GUIStyle label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= StartInStreet;
            SceneManager.sceneLoaded += StartInStreet;
        }

        static void StartInStreet(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<ChaseOpening>(true)) return;

            var opening = new GameObject("Chase Opening");
            SceneManager.MoveGameObjectToScene(opening, scene);
            opening.AddComponent<ChaseOpening>();
        }

        void Awake()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (!player) player = root.GetComponentInChildren<FirstPersonController>();
                if (!street) street = root.GetComponentInChildren<StreetOwnership>();
            }

            var camera = player ? player.GetComponentInChildren<Camera>() : null;
            if (!camera || !street || !player.enabled)
            {
                enabled = false;
                return;
            }

            eyes = camera.transform;
            standingPosition = eyes.localPosition;
            standingRotation = eyes.localRotation;
            var controller = player.GetComponent<CharacterController>();
            if (!player.GetComponent<ChaseTrafficImpact>()) player.gameObject.AddComponent<ChaseTrafficImpact>();
            bool colliderEnabled = controller.enabled;
            controller.enabled = false;
            player.transform.position = openingPosition;
            controller.enabled = colliderEnabled;
            player.enabled = false;
            controlsLocked = true;
            eyes.SetLocalPositionAndRotation(standingPosition - Vector3.up * 1.45f,
                standingRotation * Quaternion.Euler(-17f, 0, -33f));

            var data = Resources.Load<NavMeshData>("ChaseStreetNavMesh");
            if (!data)
            {
                ownedNavigation = ChaseStreetNavigation.Build(street);
                data = ownedNavigation;
            }
            if (data) navigation = NavMesh.AddNavMeshData(data);
            else Debug.LogWarning("NavMesh улицы отсутствует. Напарник и погоня недоступны.", this);
        }

        IEnumerator Start()
        {
            var origin = player.transform.position;
            var forward = player.transform.forward;
            var right = player.transform.right;
            if (navigation.valid)
            {
                suspect = ChaseRunner.Spawn(transform, "Suspect", suspectStart, new Color(.055f, .22f, .65f), suspectModel);
                if (suspect && suspectWaypoints.Length > 0) suspect.Face(suspectWaypoints[0]);
            }
            var partner = navigation.valid
                ? ChaseRunner.Spawn(transform, "Partner", origin + forward * 4.5f + right * .62f, new Color(.42f, .30f, .18f), partnerModel)
                : null;

            if (partner)
            {
                partner.Follow(new[] { origin + forward * 1.45f + right * .62f }, 2.05f);
                float approachDeadline = Time.time + 4f;
                while (partner.Moving && Time.time < approachDeadline) yield return null;
                partner.Follow(System.Array.Empty<Vector3>(), 0);
                partner.Face(origin);
            }
            yield return new WaitForSeconds(startDelay);

            yield return Speak("Wake");
            player.EndDialogueLook();
            player.enabled = false;
            if (partner) partner.Reach(eyes);

            var pronePosition = eyes.localPosition;
            var proneRotation = eyes.localRotation;
            const float liftTime = 1.65f;
            for (float elapsed = 0f; elapsed < liftTime; elapsed += Time.deltaTime)
            {
                float rise = Mathf.SmoothStep(0f, 1f, elapsed / liftTime);
                eyes.SetLocalPositionAndRotation(Vector3.Lerp(pronePosition, standingPosition, rise),
                    Quaternion.Slerp(proneRotation, standingRotation, rise));
                if (elapsed >= .65f && suspect) suspect.Face(origin, 170f * Time.deltaTime);
                yield return null;
            }

            if (partner) partner.Reach(null);
            yield return Speak("GetUp");
            if (!navigation.valid)
            {
                ReleaseControls();
                yield break;
            }

            yield return Speak("Pursuit");
            ReleaseControls();
            if (partner) partner.Follow(partnerWaypoints, 5.1f);
            if (suspect && suspectWaypoints.Length > 0)
            {
                obstacles = gameObject.AddComponent<ChaseObstacleCourse>();
                obstacles.Build(street, player, eyes, suspect, suspectWaypoints[suspectWaypoints.Length - 1]);
                suspect.SetObstacles(obstacles);
                var traffic = FindAnyObjectByType<StreetLife>();
                if (traffic) traffic.ArmChaseTraffic(player.transform);
                suspect.Follow(suspectWaypoints, suspectRunSpeed);
                chaseActive = true;
            }

            if (!partner) yield break;
            yield return new WaitUntil(() => !partner.Moving);
            yield return new WaitUntil(() => Physics.Linecast(eyes.position,
                partner.transform.position + Vector3.up * .9f, ~0, QueryTriggerInteraction.Ignore));
            partner.gameObject.SetActive(false);
        }

        IEnumerator Speak(string assetName)
        {
            if (!dialogue) dialogue = DialogueSystem.CreateLinear(gameObject);
            // Существующий сценарий подъёма и погони продолжится после завершения реплики.
            var data = Resources.Load<DialogueData>("StreetDialogue/" + assetName);
            if (!data) { Debug.LogError($"Не найден диалог улицы: {assetName}.", this); yield break; }
            player.BeginDialogueLook();
            bool finished = false;
            System.Action complete = () => finished = true;
            dialogue.OnDialogueEnd += complete;
            dialogue.Play(data);
            try { while (!finished && dialogue.Active) yield return null; }
            finally { dialogue.OnDialogueEnd -= complete; }
        }

        void Update()
        {
            if (!chaseActive || !suspect || !obstacles.AtFinish(suspect.transform.position) || !suspect.Arrived)
                return;

            chaseActive = false;
            if (obstacles) obstacles.EndTutorial();
            var fight = GetComponent<ChaseFight>();
            if (!fight) fight = gameObject.AddComponent<ChaseFight>();
            fight.Arm(player, eyes, suspect, endDelay);
        }

        void ReleaseControls()
        {
            if (!controlsLocked) return;
            controlsLocked = false;
            if (eyes) eyes.localPosition = standingPosition;
            if (player)
            {
                player.EndDialogueLook();
                player.enabled = true;
            }
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        void OnGUI()
        {
            bool fallingBehind = chaseActive && suspect &&
                FlatDistance(player.transform.position, suspect.transform.position) > warningDistance;
            if (!fallingBehind) return;
            label ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            label.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .027f), 15, 38);
            label.normal.textColor = Color.white;

            if (fallingBehind)
            {
                var box = new Rect(Screen.width * .38f, Screen.height * .08f,
                    Screen.width * .24f, label.fontSize * 1.7f);
                GUI.Box(box, GUIContent.none);
                GUI.Label(box, "Отстаёшь", label);
            }
        }

        void OnDestroy()
        {
            ReleaseControls();
            if (navigation.valid) navigation.Remove();
            if (ownedNavigation) Destroy(ownedNavigation);
        }
    }
}
