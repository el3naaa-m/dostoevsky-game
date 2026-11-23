using UnityEngine;

namespace NightChase.Station
{
    [CreateAssetMenu(menuName = "Night Chase/Station Item")]
    public sealed class Item : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        [TextArea] public string description;

        internal static Item Make(string id, string name, string description = "")
        {
            var item = CreateInstance<Item>();
            item.id = id;
            item.displayName = name;
            var document = EvidenceLog.Find(id);
            item.description = document ? document.bodyRU : description;
            if (document) item.displayName = document.titleRU;
            var texture = Resources.Load<Texture2D>("InventoryIcons/" + id);
            if (texture)
                item.icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
            return item;
        }
    }
}
// Создавайте через Assets > Create > Night Chase > Station Item. ID уникальны.
