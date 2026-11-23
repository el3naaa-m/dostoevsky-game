using UnityEngine;

namespace NightChase.Station
{
    public sealed class LockedInteractable : StationInteractable
    {
        [SerializeField] string requiredItemId = "Key";
        [SerializeField] bool consumeRequiredItem = true;
        [SerializeField] Item reward;
        [SerializeField] string displayName = "Ящик картотеки";
        bool opened;

        internal void Bind(string required, Item item)
        { requiredItemId = required; reward = item; }

        public override string Prompt(Scene2FlowManager flow) =>
            (!opened && flow.State == Scene2FlowManager.Stage.SearchPuzzle || opened && !flow.Inventory.Has("father_note") && (flow.State == Scene2FlowManager.Stage.SearchPuzzle || flow.State == Scene2FlowManager.Stage.Exit)) ? (opened ? "E — достать записку из-под подкладки" : "E — " + displayName) : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (string.IsNullOrEmpty(Prompt(flow))) return;
            if (opened)
            {
                var note = Item.Make("father_note", "Записка от отца");
                flow.Inventory.Add(note);
                flow.Audio.PlayOneShot(flow.Audio.pickup);
                flow.Dialogue.Play(DialogueData.From(new DialogueData.Line("Джо", "Он писал это тебе?")), () => {
                    flow.OpenPanel(note.displayName, note.description);
                    flow.UI.Button("Закрыть", flow.ClosePanel);
                });
                return;
            }
            if (!flow.Inventory.Has(requiredItemId))
            {
                flow.OpenPanel(displayName, "Заперто. Нужен ключ.");
                flow.UI.Button("Назад", flow.ClosePanel);
                return;
            }

            if (!reward || !flow.Inventory.Add(reward)) return;
            if (consumeRequiredItem) flow.Inventory.Remove(requiredItemId);
            opened = true;
            flow.Audio.PlayOneShot(flow.Audio.lockClick);
            flow.Dialogue.Play(DialogueData.From(new DialogueData.Line("Джо", "Где ты взял ключ?")), () => {
            flow.OpenPanel(reward.displayName, reward.description + "\nПод подкладкой виден угол сложенного листка.");
            flow.UI.Button("Закрыть", flow.ClosePanel);
            flow.CheckEvidence();
            });
        }

        internal void ResetLock() => opened = false;
    }
}
// На ящик с Collider. Назначьте Required Item Id и Reward, Consume — расход ключа.
