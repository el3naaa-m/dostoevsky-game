using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    public sealed class ArchiveRack : StationInteractable
    {
        static readonly string[] DispatchLabels = { "Журнал выдачи", "Конверты Миллера", "Возвраты в архив" };
        static readonly string[] EvidenceLabels = { "Документы порта", "Газетные вырезки", "Коробка информатора" };
        const string TapeDescription = "В коробке лежит прозрачный пакет. На бирке написано: «Вещдок № 44-Б. Изъято у информатора».";
        bool EvidenceRack => gameObject.name == "Rack (10)";

        public override string Prompt(Scene2FlowManager flow) => flow.InvestigationUnlocked ? "E — осмотреть коробки" : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (!flow.InvestigationUnlocked) return;
            flow.Lock(true);
            ShowRack(flow);
        }

        void ShowRack(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page(EvidenceRack ? "Стеллаж · Вещдоки" : "Стеллаж · Ведомости",flow.ClosePanel);
            var labels = EvidenceRack ? EvidenceLabels : DispatchLabels;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                float y = .64f - i * .17f;
                ui.Button(screen,labels[i],new(.12f,y),new(.88f,y+.12f),() => OpenBox(flow,index));
            }
        }

        void ShowDocument(Scene2FlowManager flow, RectTransform screen, int index)
        {
            if (EvidenceRack && index == 1)
            {
                ShowNewspaper(flow,screen,2);
                return;
            }

            var ui = flow.UI.Workspace;
            var paper = Paper(screen);
            if (index == 0)
            {
                bool port = EvidenceRack;
                ui.Label(paper,port ? "ПОРТОВАЯ СЛУЖБА · ВЕДОМОСТЬ ПОСЕТИТЕЛЕЙ" : "БЮРО · ЖУРНАЛ ВЫДАЧИ АРХИВНЫХ ДЕЛ",
                    new(.07f,.80f),new(.93f,.95f),28).color = Ink;
                string[] rows = port
                    ? new[] { "Дата           Объект          Запись", "08.08.99      Склад № 4       Осмотр пломб", "09.08.99      Склад № 4       Лист изъят", "10.08.99      Терминал       Передано следователю" }
                    : new[] { "Дата           Дело             Выдано             Возврат", "10.08.99      Склад № 4       Миллер             —", "11.08.99      Камера № 4      Миллер             —", "13.09.99      Портовое дело  В канцелярию    13.09.99" };
                for (int row = 0; row < rows.Length; row++)
                {
                    float y = .64f - row*.116f;
                    var stripe = EvidenceWorkspace.Rect(paper,"Строка",new(.07f,y),new(.93f,y+.10f));
                    stripe.gameObject.AddComponent<Image>().color = new Color(.69f,.64f,.52f,.28f);
                    ui.Label(stripe,rows[row],new(.025f,.08f),new(.97f,.92f),22).color = Ink;
                }
                ui.Label(paper,port ? "Примечание: лист от 9 августа изъят вместе с записью камеры № 4."
                    : "Примечание: срок возврата дела склада № 4 не указан.",new(.07f,.065f),new(.93f,.18f),23).color = Ink;
                return;
            }

            string record = index == 1
                ? "МИЛЛЕР · ВНУТРЕННЯЯ ПОМЕТКА\n\nСклад № 4. Распечатку сохранить вместе с делом.\n\nБелая нашивка на рукаве. Сверить фигуру справа на групповом снимке."
                : "АКТ ПРИЁМА · ОПИСЬ ПАПОК\n\nПапки возвращены пустыми. Материалы информатора перенесены на соседний стеллаж.\n\nВещдок № 44-Б хранить в запечатанном пакете.";
            if (index == 1) CourtCaseBoard.AddArt(paper,1,new(.06f,.15f),new(.48f,.78f));
            ui.Label(paper,record,new(index == 1 ? .52f : .08f,.12f),new(.93f,.90f),27).color = Ink;
        }

        void ShowNewspaper(Scene2FlowManager flow, RectTransform screen, int article)
        {
            var ui = flow.UI.Workspace;
            var paper = Paper(screen);
            CourtCaseBoard.AddArt(paper,article,new(.055f,.18f),new(.40f,.89f));
            ui.Label(paper,CourtCaseBoard.Bodies[article],new(.43f,.08f),new(.95f,.94f),27).color = Ink;
            ui.Button(screen,article == 2 ? "К вырезке о суде →" : "← К вырезке о складе",
                new(.65f,.035f),new(.94f,.12f),() =>
                {
                    var next = ui.Page("Газетные вырезки",() => ShowRack(flow),"К стеллажу");
                    ShowNewspaper(flow,next,article == 2 ? 3 : 2);
                });
        }

        static readonly Color Ink = new(.13f,.11f,.09f);
        static RectTransform Paper(RectTransform screen)
        {
            var page = EvidenceWorkspace.Rect(screen,"Архивный лист",new(.10f,.15f),new(.90f,.87f));
            page.gameObject.AddComponent<Image>().color = new Color(.89f,.84f,.72f);
            return page;
        }

        void OpenBox(Scene2FlowManager flow,int index)
        {
            flow.Audio.PlayOneShot(flow.Audio.paper);
            var ui = flow.UI.Workspace;
            bool hasTape = EvidenceRack && index == 2;
            var notes = InvestigationNotes.For(flow);
            var labels = EvidenceRack ? EvidenceLabels : DispatchLabels;
            var screen = ui.Page(labels[index],() => ShowRack(flow),"К стеллажу");
            if (!hasTape)
            {
                ShowDocument(flow,screen,index);
                return;
            }
            if (notes.Tape44BTaken)
            {
                ui.Label(screen,"Кассета 44-Б уже забрана. Осталась опись содержимого коробки.",
                    new(.13f,.25f),new(.87f,.7f),30);
                return;
            }

            var sleeve = EvidenceWorkspace.Rect(screen,"Пакет с вещдоком",new(.15f,.35f),new(.55f,.76f));
            sleeve.gameObject.AddComponent<Image>().color = new Color(.48f,.67f,.73f,.33f);
            var cassette = EvidenceWorkspace.Rect(sleeve,"VHS",new(.1f,.19f),new(.91f,.83f));
            cassette.gameObject.AddComponent<Image>().color = new Color(.09f,.10f,.11f);
            var tag = EvidenceWorkspace.Rect(sleeve,"Бирка",new(.52f,.09f),new(.95f,.48f));
            tag.gameObject.AddComponent<Image>().color = new Color(.85f,.81f,.70f);
            ui.Label(tag,"44-Б",new(.1f,.14f),new(.9f,.86f),29).color = Color.black;
            ui.Label(screen,TapeDescription,new(.59f,.35f),new(.91f,.76f),27);
            ui.Button(screen,"Забрать кассету 44-Б",new(.59f,.19f),new(.91f,.31f),() =>
            {
                if (notes.Tape44BTaken) return;
                var tape = Item.Make(TapePlayback.TapeId,"Кассета 44-Б");
                if (!flow.Inventory.Add(tape)) return;
                notes.Tape44BTaken = true;
                OpenBox(flow,index);
                flow.Audio.PlayOneShot(flow.Audio.pickup);
            });
        }
    }
}
