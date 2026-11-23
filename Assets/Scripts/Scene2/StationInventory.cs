using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class StationInventory : MonoBehaviour
    {
        readonly Dictionary<string, Item> items = new();
        public IEnumerable<Item> Items => items.Values;
        public event Action Changed;

        public bool Has(string id) => !string.IsNullOrEmpty(id) && items.ContainsKey(id);
        public bool Add(Item item)
        {
            if (!item || string.IsNullOrEmpty(item.id) || !items.TryAdd(item.id, item)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string id)
        {
            if (!Has(id) || !items.Remove(id)) return false;
            Changed?.Invoke();
            return true;
        }

        internal bool RemoveAll(IReadOnlyList<string> ids)
        {
            foreach (string id in ids) if (!Has(id)) return false;
            foreach (string id in ids) items.Remove(id);
            Changed?.Invoke();
            return true;
        }

        internal void SeedStreetEquipment()
        {
            Add(Item.Make(InventoryItem.IdCard.ToString(), "Удостоверение"));
            Add(Item.Make(InventoryItem.Wallet.ToString(), "Кошелёк"));
            Add(Item.Make(InventoryItem.Notebook.ToString(), "Кожаный блокнот"));
            Add(Item.Make(InventoryItem.Pistol.ToString(), "Пистолет"));
            Add(Item.Make(InventoryItem.Handcuffs.ToString(), "Наручники"));
        }
    }
}
// Добавляется к Scene2 Mechanics автоматически. Инвентарь улицы остаётся отдельным.
