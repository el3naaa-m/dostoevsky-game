using UnityEngine;

namespace NightChase.Station
{
    public sealed class PuzzleDependencies : MonoBehaviour
    {
        [SerializeField] string[] evidenceIds = { "SovietRecord", "BrightonNote", "Immigration" };
        [SerializeField] PickupInteractable[] pickups;
        [SerializeField] LockedInteractable drawer;

        internal void Bind(PickupInteractable[] objects, LockedInteractable lockbox)
        { pickups = objects; drawer = lockbox; }

        public bool Complete(StationInventory inventory)
        {
            foreach (string id in evidenceIds)
                if (!inventory.Has(id)) return false;
            return true;
        }

        internal void ResetSearch(StationInventory inventory)
        {
            inventory.Remove("father_note");
            inventory.Remove("Key");
            inventory.Remove("Knife");
            foreach (string id in evidenceIds) inventory.Remove(id);
            foreach (var item in pickups) if (item) item.ResetPickup();
            if (drawer) drawer.ResetLock();
        }
    }
}
// На Scene2 Mechanics. Evidence Ids — обязательные улики, Drawer — запираемый ящик.
