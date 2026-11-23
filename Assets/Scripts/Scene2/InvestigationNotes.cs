using UnityEngine;

namespace NightChase.Station
{
    internal sealed class InvestigationNotes : MonoBehaviour
    {
        internal const string Photo = "Северный вход, 21:40";
        internal const string Excerpt = "Северный вход закрыли в 21:00.";
        internal const string Conclusion = "Время на фото расходится с отчётом. Нужно выяснить, когда северный вход закрыли на самом деле.";
        internal const string Testimony = "После девяти дверь ещё открывалась.";
        internal bool PhotoRead, Pinned, Linked, TapePinned;

        internal static InvestigationNotes For(Scene2FlowManager flow) =>
            flow.TryGetComponent<InvestigationNotes>(out var notes) ? notes : flow.gameObject.AddComponent<InvestigationNotes>();

        internal void Show(Scene2FlowManager flow)
        {
            flow.Lock(true);
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Журнал находок",flow.ClosePanel);
            if (!PhotoRead && !Pinned && !Linked && !TapePinned)
                ui.Label(screen,"Записей пока нет.",new(.08f,.62f), new(.92f,.81f),27);
            if (PhotoRead) ui.Label(screen,Photo + "\nИсточник: оборот фото на доске перед кабинетом шефа.",new(.08f,.64f),new(.92f,.82f),27);
            if (Pinned) ui.Label(screen,Excerpt + "\nИсточник: дело отца, стр. 2, архивный шкаф.",new(.08f,.42f),new(.92f,.6f),27);
            if (Linked) ui.Label(screen,Conclusion,new(.08f,.15f),new(.92f,.38f),27);
            if (TapePinned) ui.Label(screen,Testimony + "\nИсточник: кассета, 00:12.",new(.08f,.04f),new(.62f,.15f),22);
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
