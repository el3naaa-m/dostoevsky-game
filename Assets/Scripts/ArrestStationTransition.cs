using System;
using System.Collections;
using NightChase.Station;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightChase
{
    sealed class ArrestStationTransition : MonoBehaviour
    {
        const string Station = "Assets/_Project/Scenes/PoliceStation.unity";
        const float FadeSeconds = 2f;

        FirstPersonController arrivingPlayer;
        bool arrivingWasEnabled;
        DialogueSystem dialogue;
        Image curtain;

        internal static void Begin(FirstPersonController player, Transform eyes, ChaseRunner suspect, ChaseRunner partner)
        {
            var stage = new GameObject("Arrest dialogue and scene transition");
            DontDestroyOnLoad(stage);
            var transition = stage.AddComponent<ArrestStationTransition>();
            transition.BuildOverlay();
            transition.StartCoroutine(transition.Run(player, eyes, suspect, partner));
        }

        IEnumerator Run(FirstPersonController player, Transform eyes, ChaseRunner suspect, ChaseRunner partner)
        {
            player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            var restPosition = eyes.localPosition;
            var restRotation = eyes.localRotation;
            var cameraOrigin = eyes.position;
            var cameraRotation = eyes.rotation;
            Vector3 stageCenter = Focus(suspect, partner, eyes);
            Vector3 cameraDestination = FramedPosition(eyes, stageCenter, suspect, partner);

            for (float t = 0; t < .85f; t += Time.unscaledDeltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, t/.85f);
                eyes.position = Vector3.Lerp(cameraOrigin, cameraDestination, blend);
                eyes.rotation = Quaternion.Slerp(cameraRotation, Quaternion.LookRotation(stageCenter-eyes.position), blend);
                LookAtSpeaker(player.transform, suspect, partner, -1);
                yield return null;
            }
            eyes.SetPositionAndRotation(cameraDestination, Quaternion.LookRotation(stageCenter-cameraDestination));

            player.BeginDialogueLook(pinCamera: true);
            dialogue = DialogueSystem.CreateLinear(gameObject);
            var conversation = Resources.Load<DialogueData>("StreetDialogue/Arrest");
            if (!conversation) Debug.LogError("Не найден диалог StreetDialogue/Arrest.", this);
            bool finished = false;
            dialogue.Play(conversation, () => finished = true);
            while (!finished)
            {
                LookAtSpeaker(player.transform, suspect, partner, dialogue.CurrentIndex);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(.62f);
            yield return Fade(0, 1, FadeSeconds);
            player.EndDialogueLook();
            player.enabled = false;
            var operation = RequestStation();
            if (operation != null)
            {
                float timeout = Time.realtimeSinceStartup + 25;
                while (!operation.isDone && Time.realtimeSinceStartup < timeout) yield return null;
            }
            SceneManager.sceneLoaded -= HoldArrival;

            if (operation != null && operation.isDone && SceneManager.GetActiveScene().path == Station)
            {
                yield return Fade(1, 0, FadeSeconds);
                if (arrivingPlayer && arrivingWasEnabled) Resume(arrivingPlayer);
            }
            else
            {
                Debug.LogWarning("PoliceStation could not be loaded. Returning control to the street.");
                if (eyes)
                {
                    eyes.localPosition = restPosition;
                    eyes.localRotation = restRotation;
                }
                yield return Fade(1, 0, .65f);
                if (player)
                {
                    player.BeginDialogueLook();
                    player.EndDialogueLook();
                }
                dialogue.Play(DialogueData.From(new DialogueData.Line("", "Не удалось загрузить полицейский участок.")));
                yield return new WaitForSecondsRealtime(2.7f);
            }
            Destroy(gameObject);
        }

        AsyncOperation RequestStation()
        {
            if (!Application.CanStreamedLevelBeLoaded(Station)) return null;
            SceneManager.sceneLoaded += HoldArrival;
            try
            {
                return SceneManager.LoadSceneAsync(Station, LoadSceneMode.Single);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not start PoliceStation load: {error.Message}");
                return null;
            }
        }

        void HoldArrival(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != Station) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                arrivingPlayer = root.GetComponentInChildren<FirstPersonController>();
                if (!arrivingPlayer) continue;
                arrivingWasEnabled = arrivingPlayer.enabled;
                arrivingPlayer.enabled = false;
                Cursor.visible = false;
                break;
            }
        }

        static void Resume(FirstPersonController controller)
        {
            controller.enabled = true;
            controller.SendMessage("SetCapture", Application.isFocused, SendMessageOptions.DontRequireReceiver);
        }

        static Vector3 Focus(ChaseRunner suspect, ChaseRunner partner, Transform eyes)
        {
            if (!suspect) return eyes.position + eyes.forward * 3;
            Vector3 center = partner ? (suspect.transform.position + partner.transform.position) * .5f
                : suspect.transform.position;
            return center + Vector3.up * 1.34f;
        }

        static Vector3 FramedPosition(Transform eyes, Vector3 center, ChaseRunner suspect, ChaseRunner partner)
        {
            if (!suspect || !partner) return eyes.position;
            Vector3 back = Vector3.ProjectOnPlane(eyes.position-center, Vector3.up).normalized;
            float separation = Vector3.Distance(suspect.transform.position, partner.transform.position);
            float pullback = Mathf.Clamp(separation*.9f+.6f-Vector3.Distance(eyes.position, center), 0, 1.95f);
            if (pullback <= .03f || back.sqrMagnitude < .01f) return eyes.position;
            var playerRoot = eyes.root;
            foreach (var hit in Physics.SphereCastAll(eyes.position,.18f,back,pullback,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(playerRoot)) continue;
                pullback = Mathf.Min(pullback, Mathf.Max(0,hit.distance-.12f));
            }
            return eyes.position + back*pullback;
        }

        static void LookAtSpeaker(Transform player, ChaseRunner suspect, ChaseRunner partner, int line)
        {
            float angle = 48f * Time.unscaledDeltaTime;
            if (partner && suspect)
            {
                partner.Face(line == 4 ? player.position : suspect.transform.position, angle);
                suspect.Face(line is 1 or 3 ? player.position : partner.transform.position, angle);
            }
            else if (suspect) suspect.Face(player.position, angle);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0,0,0,Mathf.SmoothStep(from,to,t/duration));
                yield return null;
            }
            curtain.color = new Color(0,0,0,to);
        }

        void BuildOverlay()
        {
            var overlay = new GameObject("Cinematic overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            overlay.transform.SetParent(transform,false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.matchWidthOrHeight = .5f;

            curtain = Rect(overlay.transform,"Black fade",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero)
                .gameObject.AddComponent<Image>();
            curtain.color = Color.clear;
            curtain.raycastTarget = false;
        }

        static RectTransform Rect(Transform parent,string name,Vector2 minAnchor,Vector2 maxAnchor,Vector2 min,Vector2 max)
        {
            var item = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            item.SetParent(parent,false);
            item.anchorMin = minAnchor;
            item.anchorMax = maxAnchor;
            item.offsetMin = min;
            item.offsetMax = max;
            return item;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= HoldArrival;
    }
}
