using UnityEngine;

namespace NightChase.Station
{
    internal sealed class InvestigationNotes : MonoBehaviour
    {
        internal const string Photo = "Северный вход, 21:40";
        internal const string Conclusion = "Подпись на фото и показания свидетеля указывают на северный вход после девяти. Нужно выяснить, когда его закрыли на самом деле.";
        internal const string Testimony = "После девяти дверь ещё открывалась.";
        internal bool PhotoRead, Linked, TapePinned;

        internal bool CourtSolved, Tape44BHeard;
        internal string CourtConclusion =>
            "Кравец стоит рядом с отцом на снимке. На его имя взяли фургон за день до слушания. Его инициалы стоят рядом с отметкой на схеме. Они готовили нападение на суд пятнадцатого сентября."
            + (Tape44BHeard ? "\n\nТеперь понятно, о каком плане отец говорил на записи." : "");

        internal static InvestigationNotes For(Scene2FlowManager flow) =>
            flow.TryGetComponent<InvestigationNotes>(out var notes) ? notes : flow.gameObject.AddComponent<InvestigationNotes>();

        internal void Show(Scene2FlowManager flow)
        {
            flow.Lock(true);
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Журнал находок",flow.ClosePanel);
            if (!PhotoRead && !Linked && !TapePinned && !CourtSolved && !Tape44BHeard)
                ui.Label(screen,"Записей пока нет.",new(.08f,.62f), new(.92f,.81f),27);
            if (PhotoRead) ui.Label(screen,Photo + "\nИсточник: оборот фото на доске перед кабинетом шефа.",new(.08f,.64f),new(.92f,.82f),27);
            if (Linked) ui.Label(screen,Conclusion,new(.08f,.15f),new(.92f,.38f),27);
            if (TapePinned) ui.Label(screen,Testimony + "\nИсточник: кассета, 00:12.",new(.08f,.04f),new(.62f,.15f),22);
            if (Tape44BHeard && !CourtSolved)
                ui.Label(screen,"Кассета 44-Б · Запись отца отмечена.",new(.08f,.43f),new(.92f,.58f),24);
            if (CourtSolved) ui.Button(screen,"Дело Ист-Ривер",new(.65f,.13f),new(.95f,.21f),() =>
            {
                var detail = ui.Page("Вывод по делу Ист-Ривер",() => Show(flow),"К журналу");
                ui.Label(detail,CourtConclusion,new(.10f,.22f),new(.90f,.78f),32);
            });
            ui.Button(screen,"Документы",new(.65f,.035f),new(.95f,.12f),() =>
            {
                ui.Close();
                var archive = flow.GetComponent<EvidenceLog>();
                if (!archive) archive = flow.gameObject.AddComponent<EvidenceLog>();
                archive.Show(flow);
            });
        }
    }
}
