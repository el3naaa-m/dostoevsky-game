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
        EscortSystem escort, chiefRoute;
        InterrogationSequence seating;
        [SerializeField] RouteGizmos interrogationRoute, detentionRoute;
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
            UI = DialogueSystem.CreateView(transform);
            Audio = FindIn<AudioManager>(gameObject.scene);
            if (!Audio) Audio = GetOrAdd<AudioManager>();
            Audio.Bind(Player.transform);
            Inventory = GetOrAdd<StationInventory>();
            Inventory.SeedStreetEquipment();
            Dialogue = GetOrAdd<DialogueSystem>();
            Dialogue.Bind(this);
            puzzle = GetOrAdd<PuzzleDependencies>();
            escort = GetOrAdd<EscortSystem>();
            chiefRoute = gameObject.AddComponent<EscortSystem>();
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
                var route = detentionRoute ? detentionRoute.Points : layout.CellRoute;
                if (detentionRoute && !RouteValidator.HasWaypoints(route, detentionRoute))
                    route = layout.CellRoute;
                if (!layout.Suspect) Debug.LogError("[EscortSystem] У задержанного отсутствует NavMeshAgent.", this);
                escort.Begin(layout.Partner, layout.Suspect, null, route);
                StartCoroutine(PlaceSuspectInCell(route.LastOrDefault(point => point)));
                CorridorReached();
            });
        }

        internal void CorridorReached()
        {
            if (State != Stage.Escort || corridorSpoken || Dialogue.Active) return;
            corridorSpoken = true;
            Dialogue.Play(corridor ? corridor : StationDialogue.Corridor(),
                () => chiefRoute.Begin(layout.Chief, null, Player.transform,
                    interrogationRoute ? interrogationRoute.Points : layout.Route, movementSpeed: layout.ChiefWalkSpeed));
        }

        Transform ChiefDestination
        {
            get
            {
                var points = interrogationRoute ? interrogationRoute.Points : layout.Route;
                if (points != null)
                    for (int i = points.Length - 1; i >= 0; i--)
                        if (points[i]) return points[i];
                return layout.Route[layout.Route.Length - 1];
            }
        }

        IEnumerator PlaceSuspectInCell(Transform routeEnd)
        {
            yield return new WaitUntil(() => escort.Complete);
            var suspect = layout.Suspect;
            if (!suspect) yield break;

            var entrance = layout.CellRoute[layout.CellRoute.Length - 1];
            var interior = layout.CellInterior;
            var door = layout.CellDoor;
            suspect.enabled = false;
            if (door) door.localRotation = Quaternion.Euler(0, -85, 0);

            if (routeEnd != entrance)
                yield return WalkIntoCell(suspect.transform, entrance.position);
            yield return WalkIntoCell(suspect.transform, interior.position);
            if (door) door.localRotation = Quaternion.identity;

            var towardCorridor = Vector3.ProjectOnPlane(entrance.position - interior.position, Vector3.up);
            if (towardCorridor.sqrMagnitude > .01f)
                suspect.transform.rotation = Quaternion.LookRotation(towardCorridor);
        }

        static IEnumerator WalkIntoCell(Transform actor, Vector3 destination)
        {
            while (actor && (actor.position - destination).sqrMagnitude > .0001f)
            {
                var direction = Vector3.ProjectOnPlane(destination - actor.position, Vector3.up);
                if (direction.sqrMagnitude > .001f)
                    actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
                actor.position = Vector3.MoveTowards(actor.position, destination, 1.8f * Time.deltaTime);
                yield return null;
            }
            if (actor) actor.position = destination;
        }

        int displayedEvidence = -1;
        internal bool InvestigationUnlocked { get; private set; }

        void Update()
        {
            if (!ready) return;
            if (State == Stage.Interrogation && Dialogue.Active) layout.LookAtPlayer(eyes);
            int count = InvestigationUnlocked ? InvestigationNotes.For(this).FatherCaseCount : -1;
            if (count != displayedEvidence)
            {
                displayedEvidence = count;
                UI.EvidenceProgress.text = $"Улики {count}/3   ·   J — журнал улик";
            }
            var key = Keyboard.current;
            if (State == Stage.Escort && chiefRoute.Complete && !UI.Modal &&
                Vector3.Distance(layout.Chief.transform.position, ChiefDestination.position) <= 1.35f)
            {
                SetStage(Stage.Interrogation);
                seating = GetOrAdd<InterrogationSequence>();
                seating.PrepareInterview(layout);
            }

            bool notified = Time.unscaledTime < messageUntil;
            UI.Prompt.text = notified ? message : "";
            if (key == null || Dialogue.Active) { Highlight(null); return; }
            if (UI.Modal)
            {
                if (UI.Workspace.Active)
                {
                    if (key.escapeKey.wasPressedThisFrame) UI.Workspace.Back();
                    else if (key.leftArrowKey.wasPressedThisFrame) UI.Workspace.Left?.Invoke();
                    else if (key.rightArrowKey.wasPressedThisFrame) UI.Workspace.Right?.Invoke();
                    return;
                }
                if (key.escapeKey.wasPressedThisFrame || (inventoryOpen && key.rKey.wasPressedThisFrame))
                    ClosePanel();
                return;
            }
            if (controlsLocked) { Highlight(null); return; }
            if (State == Stage.Interrogation && seating && seating.PlayerSeatReady &&
                Vector3.Distance(Player.transform.position, seating.PlayerSeatPosition) < 1.45f)
            {
                Highlight(null);
                UI.Prompt.text = "E — Сесть";
                if (key.eKey.wasPressedThisFrame) StartCoroutine(SitForInterview());
                return;
            }
            if (InvestigationUnlocked && key.jKey.wasPressedThisFrame) { InvestigationNotes.For(this).Show(this); return; }
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
            if (InvestigationUnlocked) UI.Button("Журнал улик   [J]", () => InvestigationNotes.For(this).Show(this));
            UI.Button("Закрыть   [R]", ClosePanel);
        }

        IEnumerator SitForInterview()
        {
            yield return seating.SeatPlayer(this, eyes);
            Dialogue.Play(interview ? interview : StationDialogue.Interview(), () => StartCoroutine(FinishInterview()));
        }

        IEnumerator FinishInterview()
        {
            Lock(true);
            yield return new WaitForSeconds(.65f);

            SetStage(Stage.Suspension);
            yield return suspensionSequence.Run(this, suspension ? suspension : StationDialogue.Suspension(),
                layout, seating, eyes);
            Lock(false);
        }

        internal void BeginInvestigationAfterHandover()
        {
            if (InvestigationUnlocked || State != Stage.HandoverEquipment) return;
            // Доступ открывается после завершения внутренней реплики, а не в момент списания вещей.
            var thought = DialogueData.From(new DialogueData.Line("Карамазов", "Я не могу просто оставаться в стороне. Я должен узнать, что происходит"));
            Dialogue.Play(thought, () =>
            {
                InvestigationUnlocked = true;
                int count = InvestigationNotes.For(this).FatherCaseCount;
                displayedEvidence = count;
                UI.EvidenceProgress.text = $"Улики {count}/3   ·   J — журнал улик";
                UI.EvidenceProgress.gameObject.SetActive(true);
                SetStage(Stage.NoticeBoard);
            });
        }

        internal void SetStage(Stage next)
        {
            State = next;
            UI.Objective.text = next switch
            {
                Stage.Arrival => "Коридор бюро",
                Stage.Escort => "Допросная — прямо по коридору, затем налево",
                Stage.Interrogation => "Подойдите к своему стулу",
                Stage.Suspension => "Дождитесь распоряжения шефа",
                Stage.HandoverEquipment => "Сдайте вещи дежурной у стойки. Архив в конце коридора, справа",
                Stage.NoticeBoard => "Осмотрите доску в общем зале со столами следователей",
                Stage.SearchPuzzle => EvidenceObjective(),
                Stage.Exit => "Выйдите из бюро",
                _ => ""
            };
            layout.Corridor.gameObject.SetActive(next == Stage.Escort);
            layout.Exit.gameObject.SetActive(next == Stage.Exit);
            StageChanged?.Invoke(next);
        }

        string EvidenceObjective()
        {
            bool clipping = Inventory.Has("BrightonNote");
            bool arrival = Inventory.Has("Immigration");
            string lead;
            if (!clipping) lead = "Осмотрите столы в архиве";
            else if (!arrival) lead = Inventory.Has("Key") ? "Откройте ящик в архиве" : "Обыщите столы рядом с ящиком";
            else lead = Inventory.Has("Knife") ? "Вернитесь к доске в зале следователей" : "Найдите в архиве инструмент для доски";
            return lead;
        }

        internal void CheckEvidence()
        {
            if (State != Stage.SearchPuzzle) return;
            if (puzzle.Complete(Inventory)) SetStage(Stage.Exit);
            else UI.Objective.text = EvidenceObjective();
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
            if (layout && layout.Handover) layout.Handover.Close();
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
