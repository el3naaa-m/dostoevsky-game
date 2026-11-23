using UnityEngine;

namespace NightChase.Station
{
    public sealed class PickupInteractable : StationInteractable
    {
        [SerializeField] Item item;
        bool unrestricted;
        internal void Bind(Item value, bool available = false) { item = value; unrestricted = available; }
        public override string Prompt(Scene2FlowManager flow) =>
            item && (unrestricted || flow.State == Scene2FlowManager.Stage.SearchPuzzle) ? $"E — взять: {item.displayName}" : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (string.IsNullOrEmpty(Prompt(flow)) || !flow.Inventory.Add(item)) return;
            flow.Audio.PlayOneShot(flow.Audio.pickup);
            gameObject.SetActive(false);
            flow.CheckEvidence();
            if (!string.IsNullOrEmpty(item.description))
            {
                flow.OpenPanel(item.displayName, item.description);
                flow.UI.Button("Закрыть", flow.ClosePanel);
            }
        }

        internal void ResetPickup() => gameObject.SetActive(true);
    }
}
// Назначьте Item, добавьте Collider и слой Interactable, если он создан в проекте.
