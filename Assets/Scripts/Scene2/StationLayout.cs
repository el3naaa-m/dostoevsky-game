using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    sealed class StationLayout : MonoBehaviour
    {
        NavMeshData runtimeMesh;
        NavMeshDataInstance navigation;
        readonly List<(NavMeshAgent agent, MixamoNpcVisual visual)> actors = new();

        internal NavMeshAgent Partner { get; private set; }
        internal NavMeshAgent Suspect { get; private set; }
        internal NavMeshAgent Chief { get; private set; }
        internal Transform[] Route { get; private set; }
        internal Transform[] CellRoute { get; private set; }
        internal Transform CellInterior { get; private set; }
        internal Transform CellDoor { get; private set; }
        
        internal Transform OfficeBoard { get; private set; }
        [SerializeField] Transform chiefOfficeBoard;
        [SerializeField, Min(.3f)] float chiefWalkSpeed = 1.45f;
        internal float ChiefWalkSpeed => chiefWalkSpeed;
        internal NoticeBoard Board { get; private set; }
        internal EquipmentHandover Handover { get; private set; }
        internal Transform SearchStart { get; private set; }
        internal TriggerZone Corridor { get; private set; }
        internal TriggerZone Exit { get; private set; }

        internal IEnumerator Build(Scene2FlowManager flow, PuzzleDependencies puzzle, ExitTrigger exit, Texture2D fatherPhoto)
        {
            var data = Resources.Load<NavMeshData>("StationTwoFloorNavMesh");
            if (!data)
            {
                Physics.SyncTransforms();
                data = runtimeMesh = StationNavigation.Bake();
                yield return null;
            }
            if (data) navigation = NavMesh.AddNavMeshData(data);

            gameObject.AddComponent<StationStairMaterials>().Repair();
            Partner = Actor("Partner", "Ch33", new Vector3(-11.1f, 0, 19.6f), Color.white);
            Suspect = Actor("Suspect", "Ch08", new Vector3(-9.9f, 0, 20.15f), new Color(.12f, .28f, .66f), true);
            Chief = Actor("Chief", "Ch31", new Vector3(-10.15f, 0, 15.35f), new Color(.12f, .16f, .22f));
            foreach (var actor in new[] { Partner, Chief })
            {
                Vector3 facing = flow.Player.transform.position - actor.transform.position;
                facing.y = 0;
                if (facing.sqrMagnitude > .01f) actor.transform.rotation = Quaternion.LookRotation(facing);
            }
            Route = new[]
            {
                Marker("CorridorPoint1", new Vector3(-10.35f, 0, 13f)),
                Marker("InterrogationDoor", new Vector3(-8.6f, 0, 13f)),
                Marker("Station_InterviewAisle", new Vector3(-7.45f, 0, 13.52f)),
                Marker("Station_Interview", FindInterviewStandingPoint())
            };
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>())
                    if (node.name is "MainDoorA (3)" or "MainDoorA (1)" or "Door (1)" or "Door (3)")
                    {
                        if (!node.TryGetComponent<DoorController>(out var door)) door = node.gameObject.AddComponent<DoorController>();
                        door.Bind(flow.Player.transform, Chief.transform);
                    }
            CellRoute = new[]
            {
                Marker("StairsBottom", new Vector3(-11.18f,0,20.42f)),
                Marker("StairsTurnLower", new Vector3(-11.18f,1.8f,25.94f)),
                Marker("StairsTurnUpper", new Vector3(-9.82f,1.8f,25.94f)),
                Marker("StairsTop", new Vector3(-9.82f,3.3f,20.36f)),
                Marker("CellDoor", new Vector3(-5.45f,3.3f,19.1f))
            };
            var entrance = CellRoute[CellRoute.Length - 1];
            float doorDistance = 2f;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var door in root.GetComponentsInChildren<Transform>())
                {
                    if (!door.parent || door.name != "Door" || !door.parent.name.StartsWith("DoorCell")) continue;
                    float distance = Vector3.Distance(door.position, entrance.position);
                    if (distance >= doorDistance) continue;
                    doorDistance = distance;
                    CellDoor = door;
                }

            Vector3 interior = new Vector3(-5.45f, 3.3f, 16.95f);
            if (CellDoor && CellDoor.TryGetComponent<MeshFilter>(out var leaf) && leaf.sharedMesh)
            {
                // The package's door leaf closes at its prefab's identity rotation.
                var hinge = CellDoor.localPosition;
                hinge.x += leaf.sharedMesh.bounds.center.x;
                var threshold = CellDoor.parent.TransformPoint(hinge);
                var inward = CellDoor.parent.forward;
                entrance.position = threshold - inward * .9f;
                interior = threshold + inward * 1.15f;
            }
            CellInterior = Marker("CellInterior", interior);

            OfficeBoard = chiefOfficeBoard;
            if (!OfficeBoard)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var node in root.GetComponentsInChildren<Transform>())
                    {
                        if (node.name == "InformationBoard_06") OfficeBoard = node;
                        else if (!OfficeBoard && node.name == "InformationBoard_06 (1)") OfficeBoard = node;
                    }
            SearchStart = Marker("Station_SearchStart", new Vector3(-13.25f, 0, 1.7f));

            Corridor = Zone("Station_Corridor", new Vector3(-10.5f, .9f, 16.25f), new Vector3(3.4f, 2.2f, 1.6f), flow.CorridorReached);
            Exit = Zone("Station_Exit", new Vector3(-20.4f, .9f, 19.5f), new Vector3(1.5f, 2.2f, 2.5f), exit.Enter);
            // Сотрудник ждёт за правой дверью в конце коридора, у столов архива.
            var clerk = Actor("ArchiveClerk", "Ch07", new Vector3(-13.9f, 0, 3.85f), new Color(.32f,.35f,.4f));
            clerk.enabled = false;
            clerk.transform.rotation = Quaternion.identity;
            var clerkTarget = new GameObject("Приём снаряжения");
            clerkTarget.transform.SetParent(clerk.transform, false);
            var clerkCollider = clerkTarget.AddComponent<BoxCollider>();
            clerkCollider.center = Vector3.up * .9f;
            clerkCollider.size = new Vector3(.66f, 1.75f, .6f);
            Handover = clerkTarget.AddComponent<EquipmentHandover>();
            Handover.Bind(clerk.transform,new Vector3(-14.04f,.77f,4.72f));

            var report = Item.Make("SovietRecord", "Советский документ",
                "СССР · УПРАВЛЕНИЕ КАДРОВ\nЛичное дело № 81-04. Выписка от 14 июня 1986 г.\nДоступ к послужному списку ограничен. Круглая печать смазана.\nНа полях карандашом: «Нью-Йорк. Фамилию не подтверждать».");
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>())
                {
                    if (item.name is "Rack (1)" or "Rack (10)")
                        if (!item.GetComponent<ArchiveRack>()) item.gameObject.AddComponent<ArchiveRack>();
                    if ((item.name is "Clock (1)" or "Clock (2)" or "ClockB (2)") && !item.GetComponent<StationClock>())
                    {
                        if (!item.GetComponentInChildren<Collider>())
                            item.gameObject.AddComponent<BoxCollider>().size = new Vector3(.34f,.34f,.12f);
                        item.gameObject.AddComponent<StationClock>();
                    }
                    if (item.name == "InformationBoard_01")
                        Board = item.TryGetComponent<NoticeBoard>(out var board) ? board : item.gameObject.AddComponent<NoticeBoard>();
                    if (item.name is "FileCabinetA (1)" or "FileCabinetA (6)" or "FileCabinetA (7)")
                        if (!item.GetComponent<ArchiveCabinet>()) item.gameObject.AddComponent<ArchiveCabinet>();
                }
            if (!Board) throw new System.InvalidOperationException("У столов отсутствует доска InformationBoard_01.");
            Board.Bind(report, fatherPhoto, flow);
            InstallTape();
            var knife = Pickup("Station_Knife", new Vector3(-14.4f, .80f, 4.64f), Item.Make("Knife", "Нож"), new Color(.32f, .38f, .48f));
            var key = Pickup("Station_Key", new Vector3(-13.72f, .80f, 4.60f), Item.Make("Key", "Ключ"), new Color(.65f, .48f, .2f));
            var note = Pickup("Station_Clipping", new Vector3(-14.08f, .80f, 4.93f),
                Item.Make("BrightonNote", "Газетная вырезка", "БРАЙТОНСКИЙ ВЕСТНИК · 18 октября 1996 г.\nМарина Левина. «Последний вечер на Брайтоне»\n«Он ушёл через служебную дверь. Пальто осталось на стуле», — говорит работник кафе. В участке ответили, что заявления о пропаже в ту ночь не поступало."), Color.white);
            var drawer = Target<LockedInteractable>("Station_Drawer", new Vector3(-13.72f, .52f, 5.10f),
                new Vector3(.45f, .32f, .3f));
            drawer.Bind("Key", Item.Make("Immigration", "Иммиграционные бумаги",
                "СЛУЖБА ИММИГРАЦИИ США · НЬЮ-ЙОРК\nКарточка прибытия. 11 сентября 1989 г.\nПрежний работодатель: графа перечёркнута. Адрес для корреспонденции: Брайтон-Бич, Бруклин.\nШтамп: «СВЕРЕНО». От руки: «Уточнить дату первого въезда»."));
            AddLabel(drawer.transform, "Ящик");
            puzzle.Bind(new[] { knife, key, note }, drawer);
        }

        void InstallTape()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>())
                {
                    if (node.name != "TV (1)") continue;
                    var deck = TapePart(node,"Видеомагнитофон",new Vector3(0,-.08f,0),new Vector3(.43f,.11f,.28f),new Color(.11f,.115f,.12f));
                    TapePart(deck.transform,"VHS-слот",new Vector3(0,.1f,.505f),new Vector3(.6f,.22f,.03f),Color.black);
                    deck.AddComponent<BoxCollider>();
                    if (!node.GetComponent<TapePlayback>()) node.gameObject.AddComponent<TapePlayback>();
                    return;
                }
            Debug.LogWarning("Телевизор TV (1) не найден, кассету 44-Б негде воспроизвести.",this);
        }

        static GameObject TapePart(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent,false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var material = part.GetComponent<Renderer>().material;
            material.color = color;
            return part;
        }

        internal Transform[] BuildChiefOfficeRoute()
        {
            if (!Chief || !OfficeBoard) return System.Array.Empty<Transform>();
            var origin = Chief.transform.position;
            if (!NavMesh.SamplePosition(origin,out var start,.55f,Chief.areaMask))
                return System.Array.Empty<Transform>();
            var surface = OfficeBoard.GetComponentInChildren<Collider>();
            Vector3 center = surface ? surface.bounds.center : OfficeBoard.position;
            Vector3 front = Vector3.ProjectOnPlane(OfficeBoard.forward,Vector3.up).normalized;
            center.y = start.position.y;
            var path = new NavMeshPath();
            // Точка стоит перед лицевой стороной доски. Оба параметра перепроверяем после привязки к NavMesh.
            foreach (float offset in new[] { 0f, -.40f, .40f })
            {
                Vector3 requested = center + front * 1.28f + Vector3.Cross(Vector3.up,front)*offset;
                if (!NavMesh.SamplePosition(requested,out var goal,.3f,Chief.areaMask)) continue;
                float separation = Vector3.Dot(goal.position-center,front);
                if (separation < 1f || separation > 1.5f || Mathf.Abs(goal.position.y - start.position.y) > .35f) continue;
                if (Physics.CheckCapsule(goal.position+Vector3.up*.36f,goal.position+Vector3.up*1.48f,
                    .24f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                if (!NavMesh.CalculatePath(start.position,goal.position,Chief.areaMask,path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;

                var route = new List<Transform>();
                foreach (var corner in path.corners)
                {
                    if (Mathf.Abs(corner.y-start.position.y) > .55f) { route.Clear(); break; }
                    if (Vector3.Distance(corner,start.position) < .22f) continue;
                    var anchor = Marker("Station_ChiefBoardPath_" + route.Count,corner);
                    anchor.position = corner;
                    route.Add(anchor);
                }
                if (route.Count > 0) return route.ToArray();
            }
            Debug.LogWarning("Шеф: нет доступного пути к лицевой стороне InformationBoard_06.",OfficeBoard);
            return System.Array.Empty<Transform>();
        }

        Vector3 FindInterviewStandingPoint()
        {
            var candidates = new[]
            {
                new Vector3(-5.32f,0,14.72f),
                new Vector3(-5.85f,0,14.5f),
                new Vector3(-7f,0,14.55f),
                new Vector3(-7.45f,0,13.95f)
            };
            foreach (var point in candidates)
            {
                if (!NavMesh.SamplePosition(point,out var hit,.25f,NavMesh.AllAreas)) continue;
                if (Physics.CheckCapsule(hit.position+Vector3.up*.36f,hit.position+Vector3.up*1.48f,
                    .25f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                return hit.position;
            }
            return candidates[candidates.Length-1];
        }

        internal void LookAtPlayer(Transform view)
        {
            if (!Chief || !view) return;
            Vector3 direction = view.position - Chief.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) return;
            // Поворот вокруг вертикали сохраняет естественный наклон головы.
            var target = Quaternion.LookRotation(direction);
            Chief.transform.rotation = Quaternion.Slerp(Chief.transform.rotation,target,1 - Mathf.Exp(-Time.unscaledDeltaTime*7));
        }

        NavMeshAgent Actor(string name, string model, Vector3 point, Color tint, bool suspect = false)
        {
            var anchor = Marker("Station_" + name + "Spawn", point);
            var actor = new GameObject(name);
            actor.transform.SetParent(transform, false);
            var hasMesh = NavMesh.SamplePosition(anchor.position, out var hit, 2f, NavMesh.AllAreas);
            actor.transform.SetPositionAndRotation(hasMesh ? hit.position : anchor.position, anchor.rotation);
            var controller = name == "Chief" ? chiefController : name == "Partner" ? partnerController :
                name == "Suspect" ? suspectController : null;
            var visual = MixamoNpcVisual.Create(actor.transform, "MixamoNpc/" + model, tint, 0, suspect: suspect, controller: controller);
            var agent = actor.AddComponent<NavMeshAgent>();
            agent.radius = .26f;
            agent.height = 1.75f;
            agent.stoppingDistance = .18f;
            if (hasMesh && !agent.isOnNavMesh) agent.Warp(hit.position);
            actors.Add((agent, visual));
            return agent;
        }

        [Header("Контроллеры бюро · параметры Speed (float), Sitting (bool)")]
        [SerializeField] RuntimeAnimatorController partnerController, chiefController, suspectController;
        internal EscortSystem OfficeArrival { get; set; }
        internal bool SeatingChief { get; set; }
        readonly Dictionary<NavMeshAgent, float> smoothedSpeeds = new();
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Sitting = Animator.StringToHash("Sitting");
        readonly Dictionary<NavMeshAgent, Vector3> previousPositions = new();

        void LateUpdate()
        {
            foreach (var (agent, visual) in actors)
            {
                var position = agent.transform.position;
                float speed = previousPositions.TryGetValue(agent, out var previous)
                    ? Vector3.Distance(position, previous) / Mathf.Max(.001f, Time.deltaTime) : 0;
                previousPositions[agent] = position;
                if (!visual) continue;
                smoothedSpeeds.TryGetValue(agent,out float prior);
                speed = Mathf.Lerp(prior,speed < .08f ? 0 : speed,1 - Mathf.Exp(-Time.deltaTime * 12));
                smoothedSpeeds[agent] = speed;
                bool sitting = SeatingChief && agent == Chief;
                var animator = visual.Animator;
                if (animator && animator.runtimeAnimatorController)
                {
                    foreach (var parameter in animator.parameters)
                    {
                        if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
                            animator.SetFloat(Speed,sitting ? 0 : speed);
                        else if (parameter.nameHash == Sitting && parameter.type == AnimatorControllerParameterType.Bool)
                            animator.SetBool(Sitting,sitting);
                    }
                }
                else if (!sitting) visual.Pose(speed,Time.deltaTime);
            }
        }

        Transform Marker(string name, Vector3 point)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
            var marker = new GameObject(name).transform;
            marker.SetParent(transform, false);
            marker.position = point;
            return marker;
        }

        T Target<T>(string name, Vector3 point, Vector3 size) where T : StationInteractable
        {
            var anchor = Marker(name, point);
            var target = new GameObject(name + " Interaction");
            target.transform.SetParent(transform, false);
            target.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0) target.layer = layer;
            target.AddComponent<BoxCollider>().size = size;
            return target.AddComponent<T>();
        }

        TriggerZone Zone(string name, Vector3 point, Vector3 size, System.Action enter)
        {
            var anchor = Marker(name, point);
            var zone = new GameObject(name + " Trigger");
            zone.transform.SetParent(transform, false);
            zone.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            zone.AddComponent<BoxCollider>().size = size;
            var body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var trigger = zone.AddComponent<TriggerZone>();
            trigger.Bind(enter);
            zone.SetActive(false);
            return trigger;
        }

        PickupInteractable Pickup(string name, Vector3 point, Item item, Color color)
        {
            var pickup = Target<PickupInteractable>(name, point, new Vector3(.18f, .16f, .18f));
            pickup.Bind(item);
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = prop.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            prop.transform.SetParent(pickup.transform, false);
            prop.transform.localScale = new Vector3(.12f, .022f, .08f);
            pickup.BindHighlight(prop.GetComponent<Renderer>(), color);
            return pickup;
        }

        static void AddLabel(Transform parent, string label)
        {
            var text = new GameObject(label).AddComponent<TextMesh>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = new Vector3(0, .28f, -.32f);
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 36;
            text.characterSize = .022f;
        }

        void OnDestroy()
        {
            if (navigation.valid) navigation.Remove();
            if (runtimeMesh) Destroy(runtimeMesh);
        }
    }
}
// Создаётся менеджером. Пустые объекты Station_* с указанными именами переопределяют точки.
