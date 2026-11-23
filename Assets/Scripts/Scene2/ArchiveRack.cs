using UnityEngine;

namespace NightChase.Station
{
    public sealed class ArchiveRack : StationInteractable
    {
        static readonly string[] Labels = { "Коробка 01 · Журналы", "Коробка 02 · Переписка", "Коробка 03 · Фотографии" };
        static readonly string[] Contents =
        {
            "Журналы дежурной смены. Последний лист заполнен в октябре 1994 года.",
            "Копии внутренних запросов. На одной папке карандашом написано «Брайтон-Бич».",
            "Конверты с фотографиями. На обороте одной подписано «Служебный вход»."
        };

        public override string Prompt(Scene2FlowManager flow) => "E — осмотреть коробки";

        public override void Use(Scene2FlowManager flow)
        {
            flow.Lock(true);
            ShowRack(flow);
        }

        void ShowRack(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Стеллаж с коробками", flow.ClosePanel);
            for (int i = 0; i < Labels.Length; i++)
            {
                int index = i;
                float y = .64f - i * .17f;
                ui.Button(screen, Labels[index], new(.12f,y), new(.88f,y+.12f), () => OpenBox(flow,index));
            }
        }

        void OpenBox(Scene2FlowManager flow, int index)
        {
            flow.Audio.PlayOneShot(flow.Audio.paper);
            var ui = flow.UI.Workspace;
            var screen = ui.Page(Labels[index], () => ShowRack(flow), "К стеллажу");
            ui.Label(screen, Contents[index], new(.13f,.25f), new(.87f,.7f), 30);
        }
    }
}
