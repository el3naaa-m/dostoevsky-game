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
            Audio = GetOrAdd<AudioManager>();
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
                StartCoroutine(PlaceSuspectInCell());
                CorridorReached();
            });
        }

        internal void CorridorReached()
        {
            if (State != Stage.Escort || corridorSpoken || Dialogue.Active) return;
            corridorSpoken = true;
            Dialogue.Play(corridor ? corridor : StationDialogue.Corridor(),
                () => chiefRoute.Begin(layout.Chief, null, Player.transform,
                    interrogationRoute ? interrogationRoute.Points : layout.Route));
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

        IEnumerator PlaceSuspectInCell()
        {
            yield return new WaitUntil(() => escort.Complete);
            var suspect = layout.Suspect;
            if (!suspect || Vector3.Distance(suspect.transform.position, layout.CellInterior.position) > 4f)
                yield break;
            if (!suspect.isOnNavMesh)
            {
                var leaf = layout.CellDoor;
                var closed = leaf ? leaf.localRotation : Quaternion.identity;
                if (leaf) leaf.localRotation = closed * Quaternion.Euler(0, 90, 0);
                foreach (var point in new[] { layout.CellRoute[layout.CellRoute.Length - 1], layout.CellInterior })
                    while ((suspect.transform.position - point.position).sqrMagnitude > .001f)
                    {
                        suspect.transform.position = Vector3.MoveTowards(suspect.transform.position, point.position, 1.8f * Time.deltaTime);
                        yield return null;
                    }
                if (leaf) leaf.localRotation = closed;
                yield break;
            }
            if (!UnityEngine.AI.NavMesh.SamplePosition(layout.CellInterior.position, out var hit, .65f, suspect.areaMask))
                yield break;

            var cellDoor = layout.CellDoor;
            Quaternion closedRotation = cellDoor ? cellDoor.localRotation : Quaternion.identity;
            if (cellDoor) cellDoor.localRotation = closedRotation * Quaternion.Euler(0, 90, 0);
            suspect.isStopped = false;
            if (!suspect.SetDestination(hit.position))
            {
                if (cellDoor) cellDoor.localRotation = closedRotation;
                yield break;
            }
            float deadline = Time.time + 12f;
            yield return new WaitUntil(() => Time.time >= deadline || Vector3.Distance(suspect.transform.position, hit.position) < .4f);
            if (suspect.isOnNavMesh) suspect.isStopped = true;
            if (cellDoor && Vector3.Distance(suspect.transform.position, hit.position) < .4f)
                cellDoor.localRotation = closedRotation;
        }

        void Update()
        {
            if (!ready) return;
            var key = Keyboard.current;
            if (State == Stage.Escort && chiefRoute.Complete && !UI.Modal &&
                Vector3.Distance(layout.Chief.transform.position, ChiefDestination.position) <= 1.35f)
            {
                SetStage(Stage.Interrogation);
                seating = GetOrAdd<InterrogationSequence>();
                StartCoroutine(seating.SeatChief(layout));
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
            if (State == Stage.Interrogation && seating && seating.ChiefSeated &&
                Vector3.Distance(Player.transform.position, seating.PlayerSeatPosition) < 1.45f)
            {
                Highlight(null);
                UI.Prompt.text = "E — Сесть";
                if (key.eKey.wasPressedThisFrame) StartCoroutine(SitForInterview());
                return;
            }
            if (key.jKey.wasPressedThisFrame) { InvestigationNotes.For(this).Show(this); return; }
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

        IEnumerator SitForInterview()
        {
            yield return seating.SeatPlayer(this, eyes);
            Dialogue.Play(interview ? interview : StationDialogue.Interview(), () => StartCoroutine(FinishInterview()));
        }

        IEnumerator FinishInterview()
        {
            Lock(true);
            yield return seating.Stand(layout, Player.transform, eyes);
            SetStage(Stage.Suspension);
            yield return suspensionSequence.Run(this, suspension ? suspension : StationDialogue.Suspension(),
                layout, seating);
            Lock(false);
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
                Stage.HandoverEquipment => "Сдайте снаряжение в архиве. До конца коридора, направо, затем к столам справа",
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
            bool record = Inventory.Has("SovietRecord");
            bool clipping = Inventory.Has("BrightonNote");
            bool arrival = Inventory.Has("Immigration");
            int count = (record ? 1 : 0) + (clipping ? 1 : 0) + (arrival ? 1 : 0);
            string lead;
            if (!clipping) lead = "Осмотрите столы в архиве";
            else if (!arrival) lead = Inventory.Has("Key") ? "Откройте ящик в архиве" : "Обыщите столы рядом с ящиком";
            else lead = Inventory.Has("Knife") ? "Вернитесь к доске в зале следователей" : "Найдите в архиве инструмент для доски";
            return $"{lead}   ·   Улики {count}/3   ·   J — журнал";
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
