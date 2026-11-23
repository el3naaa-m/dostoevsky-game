using UnityEngine;

namespace NightChase.Station
{
    public sealed class NoticeBoard : StationInteractable
    {
        [SerializeField] Item evidence;
        [SerializeField] Texture2D fatherPhoto;
        BoardFocus focus;
        Scene2FlowManager owner;
        bool greeted;
        void Update()
        {
            if (greeted || !owner || owner.State != Scene2FlowManager.Stage.NoticeBoard || owner.UI.Modal ||
                Vector3.Distance(owner.Player.transform.position, transform.position) > 2.6f) return;
            greeted = true;
            owner.Dialogue.Play(DialogueData.From(new DialogueData.Line("Джо", "Ты чего застыл? У нас мало времени.")));
        }
        public bool Inspecting { get; private set; }

        internal void Bind(Item report, Texture2D portrait, Scene2FlowManager flow)
        {
            owner = flow;
            focus = gameObject.AddComponent<BoardFocus>();
            evidence = report;
            fatherPhoto = portrait ? portrait : Resources.Load<Texture2D>("StationEvidence/Portrait");
        }
        public override string Prompt(Scene2FlowManager flow) =>
            flow.State == Scene2FlowManager.Stage.NoticeBoard || flow.State == Scene2FlowManager.Stage.SearchPuzzle
                ? "E — осмотреть доску" : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (string.IsNullOrEmpty(Prompt(flow))) return;
            if (flow.State == Scene2FlowManager.Stage.NoticeBoard)
                flow.SetStage(Scene2FlowManager.Stage.SearchPuzzle);
            Inspecting = true;
            Show(flow, "Фотография отца, газета и документы.");
        }

        void Show(Scene2FlowManager flow, string text)
        {
            flow.OpenPanel("Доска расследования", text);
            flow.UI.BoardStyle();
            flow.UI.Button("Фото в форме", () => { Inspect(flow, -.35f); Show(flow, "Служебный портрет. Фамилия на обороте стёрта. Бруклин, 1994 год."); flow.UI.ShowPhoto(fatherPhoto); }, true, fatherPhoto);
            flow.UI.Button("Газетная вырезка", () => { Inspect(flow, .35f); Show(flow, EvidenceLog.Find("BrightonNote").bodyRU); }, true, Resources.Load<Texture2D>("StationEvidence/BrightonNoteIcon"));
            flow.UI.Button("Снять советский документ [нож]", () =>
            {
                Inspect(flow, 0);
                if (!flow.Inventory.Has("Knife")) { Show(flow, "Бумага заклеена. Нужен нож."); return; }
                if (evidence && flow.Inventory.Add(evidence))
                {
                    flow.Audio.PlayOneShot(flow.Audio.pickup);
                    flow.CheckEvidence();
                    flow.Dialogue.Play(DialogueData.From(new DialogueData.Line("Джо", "Это... твой отец?")), () => {
                        Inspecting = true;
                        Show(flow, evidence.description);
                    });
                    return;
                }
                Show(flow, evidence ? evidence.description : "Документ отсутствует.");
            }, true, Resources.Load<Texture2D>("StationEvidence/SovietRecordIcon"));
            flow.UI.Button("Закрыть", flow.ClosePanel);
        }

        void Inspect(Scene2FlowManager flow, float offset)
        {
            flow.Audio.PlayOneShot(flow.Audio.paper);
            flow.UI.MagnifyText();
            focus.Inspect(flow.Player.GetComponentInChildren<Camera>(), GetComponent<BoxCollider>().bounds.center + transform.right * offset);
        }
        internal void Close() { Inspecting = false; if (focus) focus.Release(); }
    }
}
// На доску или её датчик. Назначьте Evidence; три зоны доступны после сдачи.
