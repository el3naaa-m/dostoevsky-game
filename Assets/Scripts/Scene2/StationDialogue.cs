namespace NightChase.Station
{
    static class StationDialogue
    {
        public static DialogueData Arrival() => DialogueData.From(
            new("Джо", "Держись рядом. Сначала оформим задержанного."),
            new("Джо", "Если шеф спросит, мы привезли его в том виде, в каком нашли."));

        public static DialogueData Corridor() => DialogueData.From(
            new("Шеф", "Детектив. В допросную. Первая дверь направо."),
            new("Шеф", "Джо, веди задержанного. Дверь за собой закрой."));

        public static DialogueData Interview() => UnityEngine.Resources.Load<DialogueData>("StationInterview");

        public static DialogueData Suspension() => DialogueData.From(
            new("Шеф", "Ты отстранён. Джо займётся задержанным."),
            new("Шеф", "Пистолет, удостоверение, наручники. Сдай в окошко отдела кадров."),
            new("Шеф", "Не заставляй меня повторять."));
    }
}
// Диалоги по умолчанию. В инспекторе можно назначить свои DialogueData.
