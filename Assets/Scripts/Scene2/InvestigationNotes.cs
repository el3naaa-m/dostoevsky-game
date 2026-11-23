using UnityEngine;

namespace NightChase.Station
{
    internal sealed class InvestigationNotes : MonoBehaviour
    {
        internal const string Photo = "Северный вход, 21:40";
        internal const string Conclusion = "Подпись на фото и показания свидетеля указывают на северный вход после девяти. Нужно выяснить, когда его закрыли на самом деле.";
        internal const string Testimony = "После девяти дверь ещё открывалась.";
        internal const string BoardEntry = "Квитанция оформлена сегодня на Кравца, человека с фотографии. Его инициалы стоят на схеме рядом с датой заседания — 15 сентября.";
        const string MillerEntry = "В августе Миллер связал отца с людьми из порта. Судя по отчёту, они знакомы давно.";
        const string TapeEntry = "Это голос отца. Он говорит о нападении как о деле, которое ещё впереди. Люди из порта уже согласились помочь.";
        const string FinalEntry = "Отец и люди из порта готовят теракт. Цель — федеральный суд. Заседание через три дня.";

        internal bool PhotoRead, Linked, TapePinned;
        internal bool CourtSolved, Tape44BHeard, Tape44BTaken, MillerRead;
        bool finalRecorded;
        internal bool FinalRecorded => finalRecorded;
        internal int FatherCaseCount => (MillerRead ? 1 : 0) + (Tape44BHeard ? 1 : 0) + (CourtSolved ? 1 : 0);

        internal static InvestigationNotes For(Scene2FlowManager flow) =>
            flow.TryGetComponent<InvestigationNotes>(out var notes) ? notes : flow.gameObject.AddComponent<InvestigationNotes>();

        internal void Show(Scene2FlowManager flow)
        {
            if (!flow.InvestigationUnlocked) return;
            flow.Lock(true);
            var ui = flow.UI.Workspace;
            int count = FatherCaseCount;
            var screen = ui.Page($"Дело отца  {count}/3",flow.ClosePanel);
            ui.Label(screen,"Сегодня · 12 сентября 1999 года",new(.08f,.78f),new(.92f,.87f),25);
            float row = .74f;
            if (MillerRead) Entry("Архивное дело Миллера",MillerEntry);
            if (Tape44BHeard) Entry("Кассета 44-Б",TapeEntry);
            if (CourtSolved) Entry("Связь на доске",BoardEntry);
            if (count == 0)
                ui.Label(screen,"Пока нет записей.",new(.08f,.54f),new(.92f,.71f),26);
            if (count == 3)
            {
                ui.Label(screen,"Сегодня · 12 сентября     Заседание · 15 сентября",new(.08f,.025f),new(.57f,.14f),23);
                ui.Button(screen,finalRecorded ? "Читать вывод" : "Собрать вывод", new(.60f,.045f),new(.94f,.14f),
                    () => ShowConclusion(flow));
            }
            ui.Button(screen,"Прочие находки",new(.08f,.15f),new(.38f,.22f),() => ShowOther(flow));

            void Entry(string title,string line)
            {
                ui.Label(screen,title + "\n" + line,new(.08f,row-.15f),new(.93f,row),25);
                row -= .17f;
            }
        }

        void ShowConclusion(Scene2FlowManager flow)
        {
            if (FatherCaseCount != 3 || (!finalRecorded && flow.State != Scene2FlowManager.Stage.SearchPuzzle)) return;
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Вывод записан",finalRecorded ? () => Show(flow) : null,"К делу отца");
            ui.Label(screen,"Сегодня · 12 сентября 1999\nЗаседание · 15 сентября 1999",new(.10f,.62f),new(.91f,.84f),28);
            ui.Label(screen,FinalEntry,new(.10f,.41f),new(.91f,.60f),32);
            if (finalRecorded) return;
            finalRecorded = true;
            flow.Ending.Begin(this);
        }

        void ShowOther(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Прочие находки",() => Show(flow),"К делу отца");
            if (!PhotoRead && !Linked && !TapePinned)
                ui.Label(screen,"Пока нет записей.",new(.08f,.62f),new(.92f,.81f),27);
            if (PhotoRead) ui.Label(screen,Photo + "\nИсточник: оборот фото на доске перед кабинетом шефа.",new(.08f,.64f),new(.92f,.82f),27);
            if (Linked) ui.Label(screen,Conclusion,new(.08f,.4f),new(.92f,.59f),27);
            if (TapePinned) ui.Label(screen,Testimony + "\nИсточник: кассета, 00:12.",new(.08f,.17f),new(.92f,.35f),22);
        }
    }
}
