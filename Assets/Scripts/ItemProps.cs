using UnityEngine;

namespace NightChase
{
    public enum InventoryItem { IdCard, Wallet, Notebook, Pistol, Handcuffs }

    public static class ItemProps
    {
        public static GameObject Create(InventoryItem item, Transform parent = null)
        {
            var root = new GameObject(item.ToString());
            root.transform.SetParent(parent, false);
            Color leather = new Color(.22f, .102f, .055f);
            Color steel = new Color(.52f, .57f, .60f);
            switch (item)
            {
                case InventoryItem.IdCard:
                    Part(root.transform, Vector3.zero, new Vector3(.115f, .006f, .076f), new Color(.79f, .77f, .65f));
                    Part(root.transform, new Vector3(-.029f,.004f,0),new Vector3(.029f,.002f,.047f),steel);
                    break;
                case InventoryItem.Wallet:
                    Part(root.transform, Vector3.zero, new Vector3(.122f,.028f,.085f), leather);
                    Part(root.transform, new Vector3(0,.011f,.038f), new Vector3(.105f,.009f,.011f), leather * 1.35f);
                    break;
                case InventoryItem.Notebook:
                    Part(root.transform, Vector3.zero, new Vector3(.127f,.021f,.177f), leather);
                    Part(root.transform, new Vector3(.012f,0,0), new Vector3(.102f,.016f,.161f),new Color(.79f,.71f,.51f));
                    Part(root.transform, Vector3.up * .011f, new Vector3(.128f,.005f,.177f), leather);
                    break;
                case InventoryItem.Pistol:
                    Part(root.transform, new Vector3(0,.035f,.025f), new Vector3(.028f,.032f,.158f), steel * .53f);
                    var grip = Part(root.transform,new Vector3(0,-.017f,-.033f),new Vector3(.027f,.081f,.047f),steel * .35f);
                    grip.localRotation = Quaternion.Euler(-15,0,0);
                    break;
                case InventoryItem.Handcuffs:
                    for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * Mathf.PI / 6;
                        var link = Part(root.transform, new Vector3(side * .056f + Mathf.Cos(angle) * .033f,
                            0, Mathf.Sin(angle) * .033f), new Vector3(.009f,.013f,.020f), steel);
                        link.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                    }
                    Part(root.transform,Vector3.zero,new Vector3(.049f,.010f,.012f),steel);
                    break;
            }
            return root;
        }

        static Transform Part(Transform parent, Vector3 point, Vector3 size, Color color)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = primitive.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = point;
            primitive.transform.localScale = size;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            primitive.GetComponent<Renderer>().SetPropertyBlock(properties);
            return primitive.transform;
        }
    }
}
