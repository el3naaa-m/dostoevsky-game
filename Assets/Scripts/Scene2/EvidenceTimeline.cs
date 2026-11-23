using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    internal sealed class EvidenceTimeline : MonoBehaviour
    {
        readonly Vector2[] order = { new(.69f,.25f), new(.37f,.25f), new(.05f,.25f) };
        static readonly string[] Entries = { "20:45\nОбход дежурного", "21:00\nЗакрытие по отчёту", "21:40\nПодпись на фото" };

        internal void Show(Scene2FlowManager flow, System.Action back, string result = null)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Хронология северного входа",back,"К доске");
            var track = EvidenceWorkspace.Rect(screen,"Timeline",new(.05f,.24f),new(.95f,.75f));
            track.gameObject.AddComponent<Image>().color = new Color(.14f,.18f,.2f);
            for (int i = 0; i < Entries.Length; i++)
            {
                int index = i;
                var card = EvidenceWorkspace.Rect(track,Entries[i],order[i],order[i] + new Vector2(.25f,.43f));
                card.gameObject.AddComponent<Image>().color = new Color(.48f,.38f,.25f);
                ui.Label(card,Entries[i],new(.08f,.1f),new(.92f,.95f),27);
                card.gameObject.AddComponent<EvidenceCardDrag>().Bind(track,p => order[index] = p,() => {});
            }
            ui.Label(screen,result ?? "Перетащите записи слева направо по времени.",new(.05f,.09f),new(.69f,.21f),23);
            ui.Button(screen,"Проверить порядок",new(.71f,.09f),new(.95f,.19f),() => Show(flow,back,
                order[0].x < order[1].x && order[1].x < order[2].x
                    ? "После отметки о закрытии прошло 40 минут. Об этом промежутке в деле ничего нет."
                    : "Порядок пока не сходится."));
        }
    }
}
