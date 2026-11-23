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
        internal Transform ChiefExit { get; private set; }
        internal NoticeBoard Board { get; private set; }
        internal Transform SearchStart { get; private set; }
        internal TriggerZone Corridor { get; private set; }
        internal TriggerZone Exit { get; private set; }

        internal IEnumerator Build(Scene2FlowManager flow, PuzzleDependencies puzzle, ExitTrigger exit, Texture2D fatherPhoto)
        {
            var data = Resources.Load<NavMeshData>("StationGroundNavMesh");
            if (!data)
            {
                data = runtimeMesh = StationNavigation.Bake();
                yield return null;
            }
            if (data) navigation = NavMesh.AddNavMeshData(data);

            Partner = Actor("Partner", "Ch33", new Vector3(-11.35f, 0, 18.7f), Color.white);
            Suspect = Actor("Suspect", "Ch08", new Vector3(-9.8f, 0, 18.4f), new Color(.12f, .28f, .66f), true);
            Chief = Actor("Chief", "Ch06", new Vector3(-10.75f, 0, 15.55f), new Color(.12f, .16f, .22f));
            Route = new[]
            {
                Marker("Station_InterviewApproach", new Vector3(-10.7f, 0, 10.48f)),
                Marker("Station_InterviewThreshold", new Vector3(-12.1f, 0, 10.48f)),
                Marker("Station_Interview", new Vector3(-14.15f, 0, 10.72f))
            };
            ChiefExit = Marker("Station_ChiefExit", new Vector3(-10.2f, 0, 13.5f));
            SearchStart = Marker("Station_SearchStart", new Vector3(-13.25f, 0, 1.7f));

            Corridor = Zone("Station_Corridor", new Vector3(-10.5f, .9f, 16.25f), new Vector3(3.4f, 2.2f, 1.6f), flow.CorridorReached);
            Exit = Zone("Station_Exit", new Vector3(-20.4f, .9f, 19.5f), new Vector3(1.5f, 2.2f, 2.5f), exit.Enter);
            var counter = Target<EquipmentHandover>("Station_Handover", new Vector3(-9.65f, 1.05f, 12.1f),
                new Vector3(.8f, .75f, .6f));
            AddLabel(counter.transform, "Кадры");

            var report = Item.Make("SovietRecord", "Советский документ",
                "СССР · УПРАВЛЕНИЕ КАДРОВ\nЛичное дело № 81-04. Выписка от 14 июня 1986 г.\nДоступ к послужному списку ограничен. Круглая печать смазана.\nНа полях карандашом: «Нью-Йорк. Фамилию не подтверждать».");
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var surface in root.GetComponentsInChildren<BoxCollider>())
                    if (surface.name == "InformationBoard_06 (1)")
                        Board = surface.gameObject.AddComponent<NoticeBoard>();
            if (!Board) throw new System.InvalidOperationException("В зале следователей отсутствует доска InformationBoard_06 (1).");
            Board.Bind(report, fatherPhoto, flow);
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

        NavMeshAgent Actor(string name, string model, Vector3 point, Color tint, bool suspect = false)
        {
            var anchor = Marker("Station_" + name + "Spawn", point);
            var actor = new GameObject(name);
            actor.transform.SetParent(transform, false);
            var hasMesh = NavMesh.SamplePosition(anchor.position, out var hit, 1.6f, NavMesh.AllAreas);
            actor.transform.SetPositionAndRotation(hasMesh ? hit.position : anchor.position, anchor.rotation);
            var visual = MixamoNpcVisual.Create(actor.transform, "MixamoNpc/" + model, tint, 0, suspect: suspect);
            var agent = actor.AddComponent<NavMeshAgent>();
            agent.radius = .26f;
            agent.height = 1.75f;
            agent.stoppingDistance = .18f;
            actors.Add((agent, visual));
            return agent;
        }

        void Update()
        {
            foreach (var (agent, visual) in actors)
                if (visual) visual.Pose(agent.isOnNavMesh ? agent.velocity.magnitude : 0, Time.deltaTime);
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
