namespace NightChase.Station
{
    static class StationDialogue
    {
        public static DialogueData Arrival() => DialogueData.From(
            new("Джо", "Держись рядом. Сначала оформим задержанного."),
            new("Джо", "Если шеф спросит, мы привезли его в том виде, в каком нашли."));

        public static DialogueData Corridor() => DialogueData.From(
            new("Шеф", "Детектив. В допросную. Прямо по коридору, потом налево."),
            new("Шеф", "Джо, отведи задержанного в камеру наверху."));

        public static DialogueData Interview() => UnityEngine.Resources.Load<DialogueData>("StationInterview");

        public static DialogueData Suspension() => DialogueData.From(
            new("Шеф", "Сдай пистолет, удостоверение и наручники. Архив в конце коридора, дверь справа. Вещи примет дежурная у стойки"),
            new("Шеф", "Все улики, которые у нас есть, уже опечатаны. Если ты сунешься туда — это будет взлом. Я ясно выражаюсь?"));
    }
}
// Диалоги по умолчанию. В инспекторе можно назначить свои DialogueData.
