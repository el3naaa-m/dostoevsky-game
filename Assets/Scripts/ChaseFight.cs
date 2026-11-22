using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightChase
{
    public sealed class ChaseFight : MonoBehaviour
    {
        [SerializeField, Min(.3f)] float roundTime = 1.55f;
        [SerializeField, Min(1)] int hitsToWin = 5;
        [SerializeField, Min(1)] int missesToLose = 3;
        [SerializeField, Min(.5f)] float interactionDistance = 2.8f;
        [SerializeField] TMP_FontAsset uiFont;
        [SerializeField] Key[] keys = { Key.Q, Key.F, Key.R, Key.T, Key.G, Key.Z, Key.X, Key.C };

        [SerializeField] UnityEngine.Events.UnityEvent onArrested = new();
        public event Action Arrested;

        const float ReactionTime = .58f;
        enum Phase { Waiting, Fighting, Defeated, Arrested, Lost }
        Phase phase;
        Transform player, eyes;
        Quaternion eyeRest, eyeOriginal;
        ChaseRunner suspect;
        FirstPersonController movement;
        MixamoNpcVisual opponentVisual, hands;
        Animator playerAnimator, opponentAnimator;
        TMP_FontAsset generatedFont;
        Texture2D ringTexture;
        Sprite ringSprite;
        Canvas canvas;
        Image ring;
        TMP_Text keyLabel, status;
        Key currentKey = Key.None;
        Key[] availableKeys;
        float deadline, reactionUntil, cameraDirection;
        int hits, misses;
        bool roundActive, restoreCapture, movementLocked;

        internal void Arm(FirstPersonController controller, Transform cameraEyes, ChaseRunner opponent)
        {
            if (suspect) return;
            movement = controller;
            player = controller.transform;
            eyes = cameraEyes;
            suspect = opponent;
            opponentAnimator = suspect.Anim;
            opponentVisual = suspect.GetComponentInChildren<MixamoNpcVisual>();
            playerAnimator = player.GetComponentInChildren<Animator>();
            availableKeys = Array.FindAll(keys ?? Array.Empty<Key>(), IsQteInput);
            BuildUi();
        }

        void Update()
        {
            if (!suspect) return;
            var keyboard = Keyboard.current;
            if (phase == Phase.Waiting || phase == Phase.Defeated)
            {
                bool near = CanInteract();
                status.text = phase == Phase.Waiting ? "E — начать драку" : "E — надеть наручники";
                status.gameObject.SetActive(near);
                if (!near || keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;
                if (phase == Phase.Defeated)
                    StartCoroutine(Arrest());
                else
                    Begin();
                return;
            }

            if (phase != Phase.Fighting) return;
            if (!roundActive)
            {
                if (Time.time >= reactionUntil) NextRound();
                return;
            }

            ring.fillAmount = Mathf.Clamp01((deadline - Time.time) / roundTime);
            if (Time.time >= deadline)
            {
                Resolve(false);
                return;
            }
            if (keyboard == null) return;
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame || !IsQteInput(key.keyCode)) continue;
                Resolve(key.keyCode == currentKey);
                break;
            }
        }

        bool CanInteract()
        {
            Vector3 target = suspect.transform.position + Vector3.up;
            if ((target - eyes.position).sqrMagnitude > interactionDistance * interactionDistance) return false;
            Vector3 ray = target - eyes.position;
            foreach (var hit in Physics.RaycastAll(eyes.position, ray.normalized, ray.magnitude, ~0,
                         QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(suspect.transform))
                    return false;
            return true;
        }

        static bool IsQteInput(Key key) =>
            (key >= Key.A && key <= Key.Z) || (key >= Key.Digit1 && key <= Key.Digit0)
            || key == Key.Space || key == Key.Comma || key == Key.Period || key == Key.Slash
            || key == Key.Minus || key == Key.Equals;

        void Begin()
        {
            phase = Phase.Fighting;
            status.gameObject.SetActive(false);
            suspect.Face(player.position);
            eyeOriginal = eyes.localRotation;
            Vector3 look = suspect.transform.position + Vector3.up * 1.45f - eyes.position;
            eyeRest = look.sqrMagnitude > .001f
                ? Quaternion.Inverse(eyes.parent.rotation) * Quaternion.LookRotation(look) : eyeOriginal;
            if (!playerAnimator || !playerAnimator.runtimeAnimatorController)
            {
                hands = MixamoNpcVisual.Create(player, "MixamoNpc/FightArms", Color.white, 0, false);
                if (hands)
                {
                    hands.transform.rotation = Quaternion.Euler(0, Quaternion.LookRotation(look).eulerAngles.y, 0);
                    hands.gameObject.SetActive(false);
                }
            }
            restoreCapture = Cursor.lockState == CursorLockMode.Locked;
            movementLocked = movement.enabled;
            movement.enabled = false;
            if (restoreCapture)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            reactionUntil = Time.time + .25f;
        }

        void NextRound()
        {
            Key previous = currentKey;
            int index = availableKeys.Length > 0 ? UnityEngine.Random.Range(0, availableKeys.Length) : 0;
            if (availableKeys.Length > 1 && availableKeys[index] == previous) index = (index + 1) % availableKeys.Length;
            currentKey = availableKeys.Length > 0 ? availableKeys[index] : Key.F;
            string display = Keyboard.current?[currentKey]?.displayName;
            keyLabel.text = string.IsNullOrEmpty(display) ? currentKey.ToString() : display.ToUpperInvariant();
            roundActive = true;
            deadline = Time.time + roundTime;
            ring.fillAmount = 1f;
            ring.transform.parent.gameObject.SetActive(true);
        }

        void Resolve(bool hit)
        {
            roundActive = false;
            ring.transform.parent.gameObject.SetActive(false);
            reactionUntil = Time.time + ReactionTime;
            cameraDirection = hit ? 1f : -1f;

            if (hit)
            {
                hits++;
                Trigger(playerAnimator, "Punch");
                ShowHands("Punch", false);
                Trigger(opponentAnimator, "Hit");
                if (opponentVisual) opponentVisual.FightMotion("Hit", true);
                if (hits >= hitsToWin) StartCoroutine(FinishWin());
            }
            else
            {
                misses++;
                Trigger(playerAnimator, "Hit");
                ShowHands("Hit", true);
                Trigger(opponentAnimator, "Punch");
                if (opponentVisual) opponentVisual.FightMotion("Punch", false);
                if (misses >= missesToLose) StartCoroutine(FinishLoss());
            }
        }

        void ShowHands(string motion, bool hit)
        {
            if (!hands) return;
            hands.gameObject.SetActive(true);
            hands.FightMotion(motion, hit);
        }

        void LateUpdate()
        {
            if (!movementLocked || !eyes) return;
            float t = Mathf.Clamp01((reactionUntil - Time.time) / ReactionTime);
            if (hands && hands.gameObject.activeSelf)
            {
                if (t <= 0) hands.gameObject.SetActive(false);
                else hands.Pose(0, Time.deltaTime);
            }
            eyes.localRotation = eyeRest * Quaternion.Euler(cameraDirection * 8f * Mathf.Sin(Mathf.PI * t),
                0f, -cameraDirection * 5f * Mathf.Sin(Mathf.PI * t));
        }

        IEnumerator FinishWin()
        {
            phase = Phase.Arrested;
            yield return new WaitForSeconds(ReactionTime);
            Trigger(opponentAnimator, "Lose");
            if (opponentVisual) opponentVisual.Fall();
            RestoreControl();
            phase = Phase.Defeated;
        }

        IEnumerator Arrest()
        {
            phase = Phase.Arrested;
            status.text = "Наручники надеты";
            yield return new WaitForSeconds(1.3f);
            status.gameObject.SetActive(false);
            onArrested.Invoke();
            Arrested?.Invoke();
        }

        IEnumerator FinishLoss()
        {
            phase = Phase.Lost;
            status.text = "Преступник убежал";
            status.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.2f);
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield break;
            }
#endif
            SceneManager.LoadScene(path);
        }

        static void Trigger(Animator target, string name)
        {
            if (!target || !target.runtimeAnimatorController) return;
            foreach (var parameter in target.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name)
                {
                    target.SetTrigger(name);
                    return;
                }
        }

        void RestoreControl()
        {
            if (!movementLocked) return;
            movementLocked = false;
            if (eyes) eyes.localRotation = eyeOriginal;
            if (hands) hands.gameObject.SetActive(false);
            if (!movement) return;
            movement.enabled = true;
            movement.SendMessage("SetCapture", restoreCapture && Application.isFocused, SendMessageOptions.DontRequireReceiver);
        }

        void BuildUi()
        {
            var root = new GameObject("Fight HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            if (!uiFont)
                uiFont = generatedFont = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            ringSprite = CreateRing();
            status = Text(root.transform, "Fight status", new Vector2(0, -235), new Vector2(980, 90), 41);
            status.gameObject.SetActive(false);

            var circle = Rectangle(root.transform, "QTE circle", new Vector2(0, -100), new Vector2(154, 154));
            var background = NewImage(circle, "Ring background", new Color(.04f, .07f, .09f, .78f));
            background.type = Image.Type.Simple;
            ring = NewImage(circle, "Remaining time", new Color(.96f, .83f, .46f));
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            keyLabel = Text(circle, "Key", Vector2.zero, new Vector2(150, 150), 63);
            circle.gameObject.SetActive(false);
        }

        Image NewImage(Transform parent, string name, Color color)
        {
            var rect = Rectangle(parent, name, Vector2.zero, new Vector2(154, 154));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ringSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
        {
            var text = Rectangle(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (uiFont) text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform Rectangle(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        Sprite CreateRing()
        {
            const int size = 128;
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                float a = Mathf.Clamp01(62.5f - d) * Mathf.Clamp01(d - 55f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            ringTexture.SetPixels32(pixels);
            ringTexture.Apply(false, true);
            return Sprite.Create(ringTexture, new Rect(0, 0, size, size), Vector2.one * .5f);
        }

        void OnDestroy()
        {
            RestoreControl();
            if (hands) Destroy(hands.gameObject);
            if (ringSprite) Destroy(ringSprite);
            if (ringTexture) Destroy(ringTexture);
            if (generatedFont) Destroy(generatedFont);
        }
    }
}
