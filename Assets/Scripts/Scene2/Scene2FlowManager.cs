using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NightChase.Station
{
    public sealed class Scene2FlowManager : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Project/Scenes/PoliceStation.unity";
        public enum Stage { Arrival, Escort, Interrogation, Suspension, HandoverEquipment, NoticeBoard, SearchPuzzle, Exit }

        [SerializeField] DialogueData arrival, corridor, interview, suspension;
        [SerializeField] Texture2D fatherPhoto;
        [SerializeField, Min(.5f)] float interactionRange = 2.6f;
        public Stage State { get; private set; }
        public FirstPersonController Player { get; private set; }
        public StationInventory Inventory { get; private set; }
        public DialogueSystem Dialogue { get; private set; }
        public event Action<Stage> StageChanged;
        internal AudioManager Audio { get; private set; }
        internal StationUI UI { get; private set; }

        StationLayout layout;
        PuzzleDependencies puzzle;
        EscortSystem escort;
        SuspensionSequence suspensionSequence;
        Transform eyes;
        StationInteractable highlighted;
        bool ready, controlsLocked, restoreMovement, restoreCapture, inventoryOpen, corridorSpoken;
        string message;
        float messageUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath || FindIn<Scene2FlowManager>(scene)) return;
            var container = new GameObject("Scene2 Mechanics");
            SceneManager.MoveGameObjectToScene(container, scene);
            container.AddComponent<Scene2FlowManager>();
        }

        public static AsyncOperation LoadFromStreet() => SceneManager.LoadSceneAsync(ScenePath);

        IEnumerator Start()
        {
            Player = FindIn<FirstPersonController>(gameObject.scene);
            if (!Player) { Debug.LogError("Сцена бюро: не найден FirstPersonController.", this); yield break; }
            var playerCamera = Player.GetComponentInChildren<Camera>();
            eyes = playerCamera ? playerCamera.transform : Player.transform;
            UI = GetOrAdd<StationUI>();
            UI.Build();
            Audio = GetOrAdd<AudioManager>();
            Audio.Bind(Player.transform);
            Inventory = GetOrAdd<StationInventory>();
            Inventory.SeedStreetEquipment();
            Dialogue = GetOrAdd<DialogueSystem>();
            Dialogue.Bind(this);
            puzzle = GetOrAdd<PuzzleDependencies>();
            escort = GetOrAdd<EscortSystem>();
            suspensionSequence = GetOrAdd<SuspensionSequence>();
            var exit = GetOrAdd<ExitTrigger>();
            exit.Bind(this);
            layout = GetOrAdd<StationLayout>();
            yield return layout.Build(this, puzzle, exit, fatherPhoto);
            GetOrAdd<SecurityAttention>().Bind(this, layout.Board);

            // Переход с улицы сам возвращает контроллер после затемнения.
            yield return new WaitUntil(() => Player && Player.isActiveAndEnabled);
            ready = true;
            SetStage(Stage.Arrival);
            Dialogue.Play(arrival ? arrival : StationDialogue.Arrival(), () =>
            {
                SetStage(Stage.Escort);
                if (layout.Corridor.GetComponent<Collider>().bounds.Contains(Player.transform.position + Vector3.up))
                    CorridorReached();
            });
        }

        internal void CorridorReached()
        {
            if (State != Stage.Escort || corridorSpoken || Dialogue.Active) return;
            corridorSpoken = true;
            Dialogue.Play(corridor ? corridor : StationDialogue.Corridor(),
                () => escort.Begin(Player.transform, layout.Partner, layout.Suspect, layout.Chief, layout.Route));
        }

        void Update()
        {
            if (!ready) return;
            var key = Keyboard.current;
            if (State == Stage.Escort && escort.Complete && !UI.Modal &&
                Vector3.Distance(Player.transform.position, escort.Destination.position) <= 2.2f)
            {
                SetStage(Stage.Interrogation);
                Dialogue.Play(interview ? interview : StationDialogue.Interview(), () =>
                {
                    SetStage(Stage.Suspension);
                    StartCoroutine(suspensionSequence.Run(this, suspension ? suspension : StationDialogue.Suspension(),
                        layout.Chief, layout.ChiefExit));
                });
            }

            bool notified = Time.unscaledTime < messageUntil;
            UI.Prompt.text = notified ? message : "";
            if (key == null || Dialogue.Active) { Highlight(null); return; }
            if (UI.Modal)
            {
                if (key.escapeKey.wasPressedThisFrame || (inventoryOpen && key.rKey.wasPressedThisFrame))
                    ClosePanel();
                return;
            }
            if (controlsLocked) { Highlight(null); return; }
            if (key.jKey.wasPressedThisFrame) { GetOrAdd<EvidenceLog>().Show(this); return; }
            if (key.rKey.wasPressedThisFrame) { ShowInventory(); return; }

            if (!Physics.Raycast(eyes.position, eyes.forward, out var hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))
            {
                Highlight(null);
                return;
            }
            var target = hit.collider.GetComponentInParent<StationInteractable>();
            if (!target) { Highlight(null); return; }
            var prompt = target.Prompt(this);
            if (string.IsNullOrEmpty(prompt)) { Highlight(null); return; }
            Highlight(target);
            if (!notified) UI.Prompt.text = "◆  " + prompt;
            if (key.eKey.wasPressedThisFrame) target.Use(this);
        }

        void Highlight(StationInteractable target)
        {
            if (highlighted == target) return;
            if (highlighted) highlighted.SetHighlighted(false);
            highlighted = target;
            if (highlighted) highlighted.SetHighlighted(true);
        }

        void ShowInventory()
        {
            OpenPanel("Инвентарь", Inventory.Items.Any() ? "" : "Пусто");
            UI.ShowItems(Inventory.Items);
            inventoryOpen = true;
            UI.Button("Журнал улик   [J]", () => GetOrAdd<EvidenceLog>().Show(this));
            UI.Button("Закрыть   [R]", ClosePanel);
        }

        internal void SetStage(Stage next)
        {
            State = next;
            UI.Objective.text = next switch
            {
                Stage.Arrival => "Фойе",
                Stage.Escort => "Допросная — первая дверь направо. E — открыть дверь",
                Stage.Interrogation => "Выслушайте шефа",
                Stage.Suspension => "Дождитесь распоряжения шефа",
                Stage.HandoverEquipment => "Сдайте снаряжение в окошко отдела кадров",
                Stage.NoticeBoard => "Осмотрите доску в общем зале со столами следователей",
                Stage.SearchPuzzle => "Найдите три улики в архиве   ·   J — журнал",
                Stage.Exit => "Выйдите из бюро через фойе",
                _ => ""
            };
            layout.Corridor.gameObject.SetActive(next == Stage.Escort);
            layout.Exit.gameObject.SetActive(next == Stage.Exit);
            StageChanged?.Invoke(next);
        }

        internal void CheckEvidence()
        {
            if (State == Stage.SearchPuzzle && puzzle.Complete(Inventory)) SetStage(Stage.Exit);
        }

        internal void RestartSearch()
        {
            puzzle.ResetSearch(Inventory);
            var controller = Player.GetComponent<CharacterController>();
            bool enabled = controller.enabled;
            controller.enabled = false;
            Player.transform.position = layout.SearchStart.position;
            controller.enabled = enabled;
            SetStage(Stage.SearchPuzzle);
        }

        internal void OpenPanel(string title, string body)
        {
            Highlight(null);
            Lock(true);
            UI.Show(title, body);
        }

        internal void ClosePanel()
        {
            UI.Close();
            if (layout && layout.Board) layout.Board.Close();
            inventoryOpen = false;
            Lock(false);
        }

        internal void Lock(bool locked)
        {
            if (controlsLocked == locked) return;
            controlsLocked = locked;
            if (locked)
            {
                restoreMovement = Player.enabled;
                restoreCapture = Cursor.lockState == CursorLockMode.Locked;
                Player.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Player.enabled = restoreMovement;
                if (restoreMovement)
                    Player.SendMessage("SetCapture", restoreCapture && Application.isFocused, SendMessageOptions.DontRequireReceiver);
            }
        }

        internal void Notify(string text, float seconds)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        internal IEnumerator Fade(float target, float duration)
        {
            float from = UI.Fade.alpha;
            UI.Fade.blocksRaycasts = true;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                UI.Fade.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }
            UI.Fade.alpha = target;
            UI.Fade.blocksRaycasts = target > 0;
        }

        T GetOrAdd<T>() where T : Component => TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();

        static T FindIn<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<T>(out var component) || (component = root.GetComponentInChildren<T>(true)))
                    return component;
            return null;
        }
    }
}
// В PoliceStation добавляется автоматически. Для своих диалогов поместите менеджер в сцену и назначьте DialogueData.
