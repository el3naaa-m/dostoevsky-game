using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    public sealed class NoticeBoard : StationInteractable
    {
        Item evidence;
        Texture2D fatherPhoto;
        readonly Vector2[] positions = { new(.06f,.42f), new(.36f,.42f), new(.66f,.42f), new(.36f,.02f) };
        static readonly Vector2 CardSize = new(.26f,.46f);
        int linking = -1;
        string feedback;
        RectTransform thread;
        public bool Inspecting { get; private set; }

        internal void Bind(Item report, Texture2D portrait, Scene2FlowManager flow)
        {
            evidence = report;
            fatherPhoto = portrait ? portrait : Resources.Load<Texture2D>("StationEvidence/Portrait");
        }

        public override string Prompt(Scene2FlowManager flow) => "E — осмотреть доску";

        public override void Use(Scene2FlowManager flow)
        {
            if (flow.State == Scene2FlowManager.Stage.NoticeBoard)
                flow.SetStage(Scene2FlowManager.Stage.SearchPuzzle);
            flow.Lock(true);
            flow.Audio.PlayOneShot(flow.Audio.paper);
            Inspecting = true;
            ShowBoard(flow, true);
        }

        void ShowBoard(Scene2FlowManager flow, bool zoom = false)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Доска с уликами", () =>
            {
                if (linking < 0) { flow.ClosePanel(); return; }
                linking = -1;
                feedback = null;
                ShowBoard(flow);
            }, linking < 0 ? "Закрыть  [Esc]" : "Отменить  [Esc]");
            var board = EvidenceWorkspace.Rect(screen,"Corkboard",new(.035f,.11f),new(.965f,.885f));
            board.gameObject.AddComponent<Image>().color = new Color(.43f,.30f,.19f);
            var notes = InvestigationNotes.For(flow);
            thread = null;
            if (notes.Linked)
            {
                thread = EvidenceWorkspace.Rect(board,"Нить",Vector2.zero,Vector2.zero);
                var line = thread.gameObject.AddComponent<Image>();
                line.color = new Color(.93f,.23f,.17f);
                line.raycastTarget = false;
                UpdateThread(board);
            }
            string hint = linking >= 0 ? $"Выбрано: {CardTitle(linking)}. Нажмите другую карточку."
                : feedback ?? "Нажмите на улику или перетащите её.";
            ui.Label(screen,hint,new(.05f,.025f),new(notes.Linked ? .50f : .73f,.09f),22);
            AddCard(flow,board,0,"Служебный портрет",fatherPhoto);
            AddCard(flow,board,1,"Брайтонский вестник",null);
            if (notes.Pinned) AddCard(flow,board,2,CardTitle(2),null);
            if (notes.TapePinned) AddCard(flow,board,3,CardTitle(3),null);
            Canvas.ForceUpdateCanvases();
            UpdateThread(board);
            if (flow.State == Scene2FlowManager.Stage.SearchPuzzle && evidence && !flow.Inventory.Has(evidence.id))
                ui.Button(screen,"Снять документ", new(.76f,.025f),new(.965f,.09f),() => TakeDocument(flow));
            if (notes.Linked)
                ui.Button(screen,"Хронология",new(.52f,.025f),new(.74f,.09f),() =>
                {
                    if (!TryGetComponent<EvidenceTimeline>(out var timeline)) timeline = gameObject.AddComponent<EvidenceTimeline>();
                    timeline.Show(flow,() => ShowBoard(flow));
                });
            if (zoom) ui.Reveal(board);
        }

        void AddCard(Scene2FlowManager flow, RectTransform board, int index, string title, Texture picture)
        {
            var ui = flow.UI.Workspace;
            var card = EvidenceWorkspace.Rect(board,title,positions[index],positions[index] + CardSize);
            card.gameObject.AddComponent<Image>().color = index == 0 ? new Color(.86f,.82f,.72f) : new Color(.81f,.72f,.51f);
            var heading = ui.Label(card,title,new(.07f,.83f),new(.94f,.98f),24);
            heading.color = new Color(.15f,.12f,.08f);
            if (picture)
            {
                var area = EvidenceWorkspace.Rect(card,"Photo area",new(.08f,.07f),new(.92f,.81f));
                var image = EvidenceWorkspace.Rect(area,"Photo",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
                image.texture = picture;
                image.raycastTarget = false;
                var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = (float)picture.width / picture.height;
            }
            else
            {
                var text = ui.Label(card,index == 3 ? InvestigationNotes.Testimony + "\n\nКассета, 00:12" : index == 2 ? InvestigationNotes.Excerpt + "\n\nДело отца, стр. 2" : "18 октября 1996 года\n\nПоследний вечер на Брайтоне\n\nСвидетельница видела, как мужчина вышел через кухню.",new(.08f,.08f),new(.92f,.8f),24);
                text.color = new Color(.15f,.12f,.08f);
            }
            if (linking == index) card.GetComponent<Image>().color = new Color(.91f,.67f,.34f);
            card.gameObject.AddComponent<EvidenceCardDrag>().Bind(board, p =>
            {
                positions[index] = p;
                UpdateThread(board);
            }, () =>
            {
                if (linking >= 0) Connect(flow,index);
                else Inspect(flow,index,title,picture);
            });
        }

        void Inspect(Scene2FlowManager flow,int index,string title,Texture picture,bool reversed = false)
        {
            flow.Audio.PlayOneShot(flow.Audio.paper);
            var ui = flow.UI.Workspace;
            var screen = ui.Page(title,() => ShowBoard(flow),"К доске");
            string text = index switch
            {
                0 => reversed ? "Рукописная подпись\n\n" + InvestigationNotes.Photo : "Служебная фотография. Бруклин, 1994 год.",
                3 => InvestigationNotes.Testimony + "\n\nИсточник: кассета, 00:12.",
                2 => InvestigationNotes.Excerpt + "\n\nИсточник: дело отца, стр. 2.",
                _ => EvidenceLog.Find("BrightonNote")?.bodyRU ?? "Свидетельница видела, как мужчина вышел через кухню."
            };
            if (reversed) picture = null;
            if (index == 0)
                ui.Button(screen,reversed ? "Лицевая сторона" : "Перевернуть",new(.1f,.025f),new(.34f,.105f),() =>
                {
                    if (!reversed) InvestigationNotes.For(flow).PhotoRead = true;
                    Inspect(flow,index,title,fatherPhoto,!reversed);
                });
            ui.Button(screen,"Связать",new(.69f,.025f),new(.91f,.105f),() =>
            {
                linking = index;
                feedback = null;
                ShowBoard(flow);
            });
            var detail = EvidenceWorkspace.Rect(screen,"Evidence detail",new(.11f,.12f),new(.89f,.85f));
            if (picture)
            {
                var area = EvidenceWorkspace.Rect(detail,"Photo area",new(0,0),new(.57f,1));
                var image = EvidenceWorkspace.Rect(area,"Photo",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
                image.texture = picture;
                var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = (float)picture.width / picture.height;
            }
            ui.Label(detail,text,new(picture ? .59f : .06f,.07f),new(.96f,.94f),33);
            ui.Reveal(detail);
        }

        static string CardTitle(int index) => index == 0 ? "Служебный портрет" : index == 1 ? "Брайтонский вестник" : index == 2 ? "Выписка из дела" : "Показания свидетеля";

        void Connect(Scene2FlowManager flow, int second)
        {
            int first = linking;
            if (first == second) return;
            linking = -1;
            var notes = InvestigationNotes.For(flow);
            bool correctPair = (first == 0 && second == 2) || (first == 2 && second == 0);
            if (!correctPair || !notes.PhotoRead || !notes.Pinned)
            {
                feedback = correctPair ? "Проверьте оборот фотографии." : "Пока не вижу связи.";
                ShowBoard(flow);
                return;
            }
            notes.Linked = true;
            feedback = null;
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Вывод", () => ShowBoard(flow), "К доске");
            ui.Label(screen, InvestigationNotes.Conclusion, new(.12f,.26f), new(.88f,.72f), 32);
        }

        void UpdateThread(RectTransform board)
        {
            if (!thread) return;
            Vector2 a = Vector2.Scale(positions[0] + CardSize * .5f, board.rect.size);
            Vector2 b = Vector2.Scale(positions[2] + CardSize * .5f, board.rect.size);
            var delta = b - a;
            thread.anchoredPosition = (a + b) * .5f;
            thread.sizeDelta = new Vector2(delta.magnitude,4);
            thread.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg);
            thread.SetAsLastSibling();
        }

        void TakeDocument(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            if (!flow.Inventory.Has("Knife"))
            {
                var screen = ui.Page("Документ закреплён", () => ShowBoard(flow),"К доске");
                ui.Label(screen,"Чтобы снять документ, нужен нож.",new(.12f,.28f),new(.88f,.6f),30);
                return;
            }
            if (evidence && flow.Inventory.Add(evidence))
            {
                flow.Audio.PlayOneShot(flow.Audio.pickup);
                flow.CheckEvidence();
            }
            ShowBoard(flow);
        }

        internal void Close()
        {
            Inspecting = false;
            linking = -1;
            feedback = null;
        }
    }
}
